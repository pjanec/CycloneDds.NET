# Topic Descriptor Architecture Design

**Document Version:** 1.0  
**Date:** 2026-01-16  
**Status:** Approved for BATCH-13 Implementation

---

## 1. Problem Statement

### 1.1 The Challenge

Cyclone DDS requires **topic descriptors** (`dds_topic_descriptor_t`) to create topics. These descriptors contain:

- Type metadata (name, size, alignment)
- **Serialization bytecode** (CDR encoding rules via `ops` array)
- Key field information
- XTypes metadata (type information blobs)

**Only the idlc compiler can generate this serialization bytecode.** It outputs C code that must be compiled into a native library.

### 1.2 Initial Approaches Considered

#### Option A: C Wrapper Generation (Initially Planned)
- Generate C wrapper functions: `const dds_topic_descriptor_t* get_{Type}_descriptor()`
- Compile with idlc output → `descriptors.dll`
- P/Invoke to get descriptors

**Rejected:** Adds build complexity, requires C compiler in build chain.

#### Option B: Manual Test Descriptors
- Hand-write simple types in IDL
- Pre-compile for testing only

**Rejected:** Not scalable to production.

#### Option C: Runtime C Code Parsing ✅ **CHOSEN**
- Parse idlc-generated `.c` files using **CppAst**
- Extract data arrays (ops, type_info, type_map)
- Build `dds_topic_descriptor_t` in C# using `Marshal`

**Advantages:**
- ✅ No C compiler required in build chain
- ✅ Works with any idlc-generated code
- ✅ Fully automated
- ✅ Pure C# solution

---

## 2. Architecture: CppAst-Based Descriptor Builder

### 2.1 Two-Phase Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ PHASE 1: Build-Time Extraction (Code Generator)                │
│                                                                 │
│  IDL File  →  idlc  →  TypeName.c  →  CppAst Parser            │
│                                         ↓                       │
│                              Extract Arrays & Metadata          │
│                                         ↓                       │
│                     Generate C# DescriptorData class           │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ PHASE 2: Runtime Assembly (Application)                        │
│                                                                 │
│  DescriptorData  →  NativeDescriptorBuilder                    │
│                              ↓                                  │
│                   Marshal.AllocHGlobal for arrays              │
│                              ↓                                  │
│            Write fields to native memory by offset             │
│                              ↓                                  │
│                   Return IntPtr to descriptor                  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Component Design

#### Component 1: CppAst Extractor

**Input:** idlc-generated `.c` file  
**Output:** Structured C# `DescriptorData` object

```csharp
public class DescriptorData
{
    public string TypeName;          // "Net::AppId"
    public int Size;                 // sizeof(Net_AppId)
    public int Align;                // alignof(Net_AppId)
    public uint NKeys;               // Number of key fields
    public uint[] Ops;               // Serialization opcodes
    public byte[] TypeInfo;          // XTypes type information blob
    public byte[] TypeMap;           // XTypes type mapping blob
    public KeyDescriptor[] Keys;     // Key field descriptors
}

public class KeyDescriptor
{
    public string Name;
    public ushort Flags;
    public ushort Index;
}
```

**Extraction Logic:**

```csharp
public static DescriptorData ExtractDescriptor(string cFilePath, string cycloneIncludePath)
{
    var options = new CppParserOptions();
    options.IncludeFolders.Add(cycloneIncludePath); // Resolve DDS_OP_* macros
    
    var compilation = CppParser.ParseFile(cFilePath, options);
    
    // 1. Find the descriptor variable (e.g., Net_AppId_desc)
    var descriptorVar = compilation.Fields
        .FirstOrDefault(f => f.Name.EndsWith("_desc"));
    
    // 2. Walk initializer to extract field values
    var data = new DescriptorData();
    
    // 3. Find ops array (e.g., Net_AppId_ops)
    var opsVar = compilation.Fields
        .FirstOrDefault(f => f.Name == descriptorVar.Name.Replace("_desc", "_ops"));
    data.Ops = ExtractUInt32Array(opsVar.InitValue);
    
    // 4. Extract TYPE_INFO_CDR macro
    data.TypeInfo = ExtractByteArrayFromMacro(compilation, "TYPE_INFO_CDR_");
    
    // 5. Extract TYPE_MAP_CDR macro
    data.TypeMap = ExtractByteArrayFromMacro(compilation, "TYPE_MAP_CDR_");
    
    return data;
}
```

#### Component 2: Native Descriptor Builder

**Input:** `DescriptorData`  
**Output:** `IntPtr` to native `dds_topic_descriptor_t`

**Critical Design Decision:** Use offset-based writes, NOT `StructLayout`.

**Why:** C ABI padding rules differ across compilers (MSVC vs GCC). Cannot rely on C# padding matching native layout.

```csharp
public class NativeDescriptor : IDisposable
{
    private List<IntPtr> _allocations = new();
    public IntPtr Ptr { get; private set; }
    
    public NativeDescriptor(DescriptorData data)
    {
        // Allocate arrays
        IntPtr ptrOps = AllocArray(data.Ops);
        IntPtr ptrTypeInfo = AllocBytes(data.TypeInfo);
        IntPtr ptrTypeMap = AllocBytes(data.TypeMap);
        IntPtr ptrTypeName = AllocString(data.TypeName);
        
        // Allocate main descriptor struct
        Ptr = AllocRaw(AbiOffsets.DescriptorSize);
        
        // Write fields by offset (ABI-safe)
        WriteInt32(Ptr, AbiOffsets.Size, data.Size);
        WriteInt32(Ptr, AbiOffsets.Align, data.Align);
        WriteUInt32(Ptr, AbiOffsets.NKeys, data.NKeys);
        WriteIntPtr(Ptr, AbiOffsets.TypeName, ptrTypeName);
        WriteIntPtr(Ptr, AbiOffsets.Ops, ptrOps);
        
        // Nested struct: type_information { void* data; uint32_t sz; }
        WriteIntPtr(Ptr, AbiOffsets.TypeInfo_Data, ptrTypeInfo);
        WriteUInt32(Ptr, AbiOffsets.TypeInfo_Size, (uint)data.TypeInfo.Length);
        
        WriteIntPtr(Ptr, AbiOffsets.TypeMap_Data, ptrTypeMap);
        WriteUInt32(Ptr, AbiOffsets.TypeMap_Size, (uint)data.TypeMap.Length);
    }
    
    // ... allocation helpers ...
}
```

### 2.3 ABI Offset Auto-Generation from Source

**Critical:** Must know exact field offsets for target platform.

**Solution:** Leverage Cyclone DDS source code to auto-generate offsets using CppAst.

**File:** `tools/CycloneDDS.CodeGen/OffsetGeneration/AbiOffsetGenerator.cs`

```csharp
public static class AbiOffsetGenerator
{
    public static void GenerateOffsetsFromSource(string cycloneSourcePath, string outputPath)
    {
        var headerPath = Path.Combine(cycloneSourcePath, 
            "src/core/ddsc/include/dds/ddsc/dds_public_impl.h");
        
        var options = new CppParserOptions();
        options.IncludeFolders.Add(Path.Combine(cycloneSourcePath, "src/core/ddsc/include"));
        options.IncludeFolders.Add(Path.Combine(cycloneSourcePath, "src/ddsrt/include"));
        
        var compilation = CppParser.ParseFile(headerPath, options);
        
        if (compilation.HasErrors)
            throw new Exception("Failed to parse Cyclone headers: " + 
                string.Join("\n", compilation.Diagnostics.Messages));
        
        // Find dds_topic_descriptor_t struct
        var descriptorStruct = compilation.Classes
            .FirstOrDefault(c => c.Name == "dds_topic_descriptor_t");
        
        if (descriptorStruct == null)
            throw new Exception("Could not find dds_topic_descriptor_t in headers");
        
        // Extract version from source
        var version = ExtractCycloneVersion(cycloneSourcePath);
        
        // Generate C# code
        var code = new StringBuilder();
        code.AppendLine("// <auto-generated from Cyclone DDS source>");
        code.AppendLine($"// Cyclone DDS Version: {version}");
        code.AppendLine($"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        code.AppendLine();
        code.AppendLine("namespace CycloneDDS.Runtime.Descriptors;");
        code.AppendLine();
        code.AppendLine("public static class AbiOffsets");
        code.AppendLine("{");
        code.AppendLine($"    public const string CycloneVersion = \"{version}\";\n");
        
        // Extract field offsets (CppAst provides Offset and SizeOf)
        var fieldMap = new Dictionary<string, string>
        {
            { "m_size", "Size" },
            { "m_align", "Align" },
            { "m_flagset", "Flagset" },
            { "m_nkeys", "NKeys" },
            { "m_typename", "TypeName" },
            { "m_keys", "Keys" },
            { "m_nops", "NOps" },
            { "m_ops", "Ops" },
            { "m_meta", "Meta" },
            { "type_information", "TypeInfo" },
            { "type_mapping", "TypeMap" }
        };
        
        foreach (var field in descriptorStruct.Fields)
        {
            if (fieldMap.TryGetValue(field.Name, out var csName))
            {
                code.AppendLine($"    public const int {csName} = {field.Offset};");
                
                // For nested structs (type_information, type_mapping)
                if (field.Type is CppClass nestedStruct)
                {
                    // Assume { void* data; uint32_t sz; }
                    code.AppendLine($"    public const int {csName}_Data = {field.Offset};");
                    code.AppendLine($"    public const int {csName}_Size = {field.Offset + IntPtr.Size};");
                }
            }
        }
        
        code.AppendLine($"\n    public const int DescriptorSize = {descriptorStruct.SizeOf};");
        code.AppendLine("}");
        
        File.WriteAllText(outputPath, code.ToString());
    }
    
    private static string ExtractCycloneVersion(string sourcePath)
    {
        // Read VERSION file or CMakeLists.txt
        var versionFile = Path.Combine(sourcePath, "VERSION");
        if (File.Exists(versionFile))
            return File.ReadAllText(versionFile).Trim();
        
        // Fallback: parse CMakeLists.txt
        var cmakePath = Path.Combine(sourcePath, "CMakeLists.txt");
        if (File.Exists(cmakePath))
        {
            var cmake = File.ReadAllText(cmakePath);
            var match = Regex.Match(cmake, @"project\([^)]*VERSION\s+([\d.]+)");
            if (match.Success)
                return match.Groups[1].Value;
        }
        
        return "unknown";
    }
}
```

**Generated Output:** `src/CycloneDDS.Runtime/Descriptors/AbiOffsets.g.cs`

```csharp
// <auto-generated from Cyclone DDS source>
// Cyclone DDS Version: 0.11.0
// Generated: 2026-01-16 00:25:00 UTC

namespace CycloneDDS.Runtime.Descriptors;

public static class AbiOffsets
{
    public const string CycloneVersion = "0.11.0";

    public const int Size = 0;
    public const int Align = 4;
    public const int Flagset = 8;
    public const int NKeys = 12;
    public const int TypeName = 16;
    public const int Keys = 24;
    public const int NOps = 32;
    public const int Ops = 40;
    public const int Meta = 48;
    public const int TypeInfo = 56;
    public const int TypeInfo_Data = 56;
    public const int TypeInfo_Size = 64;
    public const int TypeMap = 72;
    public const int TypeMap_Data = 72;
    public const int TypeMap_Size = 80;

    public const int DescriptorSize = 88;
}
```

**Advantages:**
- ✅ **Zero manual maintenance** - Offsets extracted from source
- ✅ **Version-aware** - Tracks Cyclone version
- ✅ **Validates compatibility** - Detects ABI changes
- ✅ **No C compiler needed** - Pure CppAst solution

---

## 3. Integration with Code Generator

### 3.1 Build Workflow

```
User's C# Type Definition
    ↓
Code Generator (existing)
    ↓
Generates .idl file
    ↓
Invoke: idlc {Type}.idl  →  {Type}.c / {Type}.h
    ↓
CppAst Extractor parses {Type}.c
    ↓
Generate: {Type}DescriptorData.g.cs
    ↓
Compile into user's assembly
```

### 3.2 Generated Code Example

**File:** `TestMessageDescriptorData.g.cs`

```csharp
// <auto-generated/>
namespace MyApp.Generated;

public static class TestMessageDescriptorData
{
    public static readonly DescriptorData Data = new DescriptorData
    {
        TypeName = "MyApp::TestMessage",
        Size = 24,
        Align = 8,
        NKeys = 1,
        Ops = new uint[] { 0x01100001, 0x00000000, ... },
        TypeInfo = new byte[] { 0x60, 0x00, 0x00, ... },
        TypeMap = new byte[] { 0x4b, 0x00, 0x00, ... }
    };
}
```

### 3.3 Runtime Usage

```csharp
// At application startup or first topic creation
var descriptor = new NativeDescriptor(TestMessageDescriptorData.Data);

// Pass to DDS
var topic = DdsApi.dds_create_topic(
    participant,
    descriptor.Ptr,  // ← Native descriptor pointer
    "TestTopic",
    IntPtr.Zero,
    IntPtr.Zero);

// Descriptor stays alive as long as topic exists
// Dispose when topic deleted
```

---

## 4. Implementation Plan (BATCH-13)

### 4.1 Phase 0: Source-Based Offset Generation

**Tasks:**
1. Add CppAst NuGet package to code generator
2. Implement `AbiOffsetGenerator` to parse Cyclone headers
3. Run generator with Cyclone source path
4. Generate `AbiOffsets.g.cs` with version tracking
5. Validate generated offsets match expected layout

**Deliverables:**
- `tools/CycloneDDS.CodeGen/OffsetGeneration/AbiOffsetGenerator.cs`
- `src/CycloneDDS.Runtime/Descriptors/AbiOffsets.g.cs` (generated)
- Build script to regenerate offsets when Cyclone updates

### 4.2 Phase 1: CppAst Descriptor Extraction

**Tasks:**
1. Implement `CycloneDescriptorExtractor` class
2. Parse idlc-generated `.c` files
3. Extract ops arrays, type_info, type_map blobs
4. Handle macro expansion for byte arrays
5. Extract key descriptors

**Deliverables:**
- `tools/CycloneDDS.CodeGen/DescriptorExtraction/CppAstExtractor.cs`
- `tools/CycloneDDS.CodeGen/DescriptorExtraction/DescriptorData.cs`

### 4.3 Phase 2: Native Builder

**Tasks:**
1. Implement `NativeDescriptorBuilder` class
2. Memory management (allocation tracking, disposal)
3. Offset-based field writing helpers

**Deliverables:**
- `src/CycloneDDS.Runtime/Descriptors/NativeDescriptorBuilder.cs`
- `src/CycloneDDS.Runtime/Descriptors/DescriptorData.cs`

### 4.4 Phase 3: Generator Integration

**Tasks:**
1. Add idlc invocation to code generator
2. Integrate CppAst extraction
3. Generate `{Type}DescriptorData.g.cs` files
4. Update metadata registry to include descriptor data

**Deliverables:**
- Modified `CodeGenerator.cs` with descriptor generation
- Integration tests

---

## 5. Validation Strategy

### 5.1 Source-Based Validation

**Test 1:** Validate offsets against Cyclone test suite

```csharp
[Fact]
public void AbiOffsets_MatchCycloneSource()
{
    // Cyclone source has test cases with known descriptors
    var cycloneTestPath = Path.Combine(CycloneSourcePath, "src/core/ddsc/tests");
    
    // Compare our generated offsets with test expectations
    // This ensures we're reading the source correctly
}
```

**Test 2:** Validate descriptor structure size

```csharp
[Fact]
public void DescriptorSize_MatchesSourceDefinition()
{
    // Re-parse header at test time
    var actualSize = ParseDescriptorSizeFromSource(CycloneSourcePath);
    
    Assert.Equal(actualSize, AbiOffsets.DescriptorSize);
}
```

**Test 3:** Compare with idlc reference output

```csharp
[Fact]
public void Descriptor_MatchesIdlcOutput()
{
    // Use Cyclone's own test IDL files
    var testIdl = Path.Combine(CycloneSourcePath, "src/idl/tests/data.idl");
    
    // Run idlc → compile to C library → load descriptor
    var referenceDesc = GetIdlcGeneratedDescriptor(testIdl);
    
    // Build using our extractor
    var ourDesc = BuildDescriptorFromIdlcOutput(testIdl);
    
    // Compare ops arrays byte-by-byte
    Assert.Equal(referenceDesc.Ops, ourDesc.Ops);
}
```

### 5.2 Functional Validation

**Test:** Use C#-built descriptor to create actual DDS topic and publish data.

```csharp
[Fact]
public void Descriptor_WorksWithActualDDS()
{
    var participant = new DdsParticipant();
    var descriptor = new NativeDescriptor(TestMessageDescriptorData.Data);
    
    // This should succeed if descriptor is valid
    var topic = DdsApi.dds_create_topic(
        participant.Entity,
        descriptor.Ptr,
        "TestTopic",
        IntPtr.Zero,
        IntPtr.Zero);
    
    Assert.True(topic.IsValid);
    
    // Further: create writer, publish sample, verify with reader
}
```

---

## 6. Advantages of This Approach

1. **No Build Complexity:** idlc runs as part of code generation, no separate C build
2. **Cross-Platform:** CppAst works on Windows/Linux/macOS
3. **Maintainable:** Pure C# solution, easier to debug than P/Invoke glue
4. **Automated:** No manual descriptor maintenance
5. **Flexible:** Can easily adapt to different Cyclone DDS versions

---

## 7. Risks & Mitigations

### Risk 1: ABI Changes Between Cyclone Versions

**Mitigation:** 
- ✅ **Auto-regenerate offsets** from source when Cyclone updates
- ✅ **Version tracking** in generated code
- ✅ **Runtime validation** - Compare AbiOffsets.CycloneVersion with loaded library version
- ✅ **Multi-version support** - Generate offsets for multiple Cyclone versions if needed

### Risk 2: CppAst Parsing Failures

**Mitigation:**
- Comprehensive error reporting
- Fallback to manual descriptor specification
- Unit tests for known idlc output patterns

### Risk 3: Complex Type Nesting

**Mitigation:**
- Start with simple struct types
- Add union/sequence support incrementally
- Test with progressively complex schemas

---

## 8. Future Enhancements

1. **Caching:** Cache extracted descriptors to avoid re-parsing unchanged types
2. **Validation:** Add runtime validation of descriptor correctness
3. **Multi-Version:** Support multiple Cyclone DDS versions simultaneously
4. **Optimization:** Lazy descriptor building (only when topic created)

---

## 9. References

- **CppAst:** https://github.com/xoofx/CppAst
- **Cyclone DDS:** https://github.com/eclipse-cyclonedds/cyclonedds
- **XTypes Spec:** OMG DDS-XTypes v1.3

---

**Approval:** Proceed with BATCH-13 implementation using this design.
