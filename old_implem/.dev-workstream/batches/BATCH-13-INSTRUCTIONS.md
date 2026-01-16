# BATCH-13: Topic Descriptor Builder (CppAst-Based)

**Batch Number:** BATCH-13  
**Type:** CRITICAL BLOCKER for BATCH-12  
**Tasks:** Build `dds_topic_descriptor_t` in C# by parsing idlc output  
**Phase:** Phase 3 - Runtime Components  
**Estimated Effort:** 5-7 days  
**Priority:** CRITICAL (Unblocks data transmission)  
**Dependencies:** BATCH-12 (partially complete, blocked on descriptors)

---

## 🎯 **CRITICAL**: This Unblocks BATCH-12

**Problem:** BATCH-12 implemented `DdsWriter`/`DdsReader` but cannot transmit data because:
- Current code passes `IntPtr.Zero` as topic descriptor
- `dds_create_topic` succeeds but `dds_write`/`dds_take` fail
- **Need valid `dds_topic_descriptor_t` structures**

**Solution:** Parse idlc-generated C code, extract descriptor data, build native struct in C#.

**Design Reference:** `docs/TOPIC-DESCRIPTOR-DESIGN.md`

---

## 📋 Required Reading

1. **Design:** `docs/TOPIC-DESCRIPTOR-DESIGN.md` (MANDATORY - read entire document)
2. **BATCH-12 Report:** `.dev-workstream/reports/BATCH-12-REPORT.md`
3. **Cyclone Source:** Study `src/core/ddsc/include/dds/ddsc/dds_public_impl.h`

**Report:** `.dev-workstream/reports/BATCH-13-REPORT.md`

---

## ⚠️ CRITICAL: Treat These Tools as Permanent Infrastructure

**IMPORTANT:** The code you write in this batch is **NOT** temporary or throwaway code. These are **permanent, reusable tools** that will be used repeatedly in the future when:

1. **Cyclone DDS updates** - Offsets may change between versions
2. **Platform changes** - Different platforms may have different ABIs
3. **Type addition** - New types will need descriptor generation
4. **Troubleshooting** - Debugging ABI issues requires regeneration

### Required Tool Quality Standards:

**✅ DO:**
- Write production-quality, maintainable code
- Add comprehensive error handling and validation
- Create clear usage documentation (README in tools/)
- Make tools runnable via simple commands
- Add logging for troubleshooting
- Structure code for easy maintenance
- Version control ALL tools and scripts
- Test on multiple inputs (not just one sample)

**❌ DON'T:**
- Write quick-and-dirty "get it working" code
- Hardcode paths or assumptions
- Skip error handling ("it works on my machine")
- Leave undocumented command-line args
- Mix concerns (separate extraction, generation, validation)

### Tool Structure Requirements:

```
tools/
  ├── CycloneDDS.CodeGen/
  │   ├── OffsetGeneration/
  │   │   ├── AbiOffsetGenerator.cs       ← Production quality
  │   │   ├── OffsetValidator.cs          ← Validates generated offsets
  │   │   └── README.md                   ← Usage instructions
  │   ├── DescriptorExtraction/
  │   │   ├── DescriptorExtractor.cs      ← Production quality
  │   │   ├── IdlcOutputParser.cs         ← Reusable parser
  │   │   └── README.md                   ← How to use
  │   └── Program.cs                      ← CLI entry point
  ├── generate-offsets.ps1                ← User-friendly script
  ├── regenerate-all.ps1                  ← Full regeneration
  └── README.md                           ← Tool overview
```

### Documentation Requirements:

Create `tools/README.md` with:

```markdown
# Cyclone DDS Tools

## Offset Generation

**When to run:** After updating Cyclone DDS to a new version.

**Command:**
```powershell
.\tools\generate-offsets.ps1 -CycloneSourcePath "D:\cyclone-src"
```

**Output:** `src\CycloneDDS.Runtime\Descriptors\AbiOffsets.g.cs`

## Descriptor Extraction

**When to run:** During code generation for new types.

**Command:**
```powershell
dotnet tools\CycloneDDS.CodeGen\bin\...\CycloneDDS.CodeGen.dll extract-descriptor --input Type.c
```

**Output:** `DescriptorData` object for Type
```

### Future-Proofing Checklist:

Before submitting BATCH-13, verify:

- [ ] Tools work with Cyclone 0.10.x, 0.11.x, 0.12.x (test multiple versions if possible)
- [ ] Clear error messages when Cyclone source not found
- [ ] Graceful handling of unexpected struct layouts
- [ ] Logging output for debugging future issues
- [ ] README documents all command-line options
- [ ] Scripts work from any directory (use relative paths)
- [ ] No hardcoded absolute paths in code
- [ ] Validation catches ABI changes vs expectations

**Remember:** Future maintainers (possibly you in 6 months) will thank you for writing robust, documented tools now!

---

## 🔄 MANDATORY WORKFLOW

**Three phases - complete sequentially:**

1. **Phase 0:** Generate ABI offsets from Cyclone source → **Offsets file created** ✅
2. **Phase 1:** Extract descriptor data from idlc output → **Extraction works** ✅
3. **Phase 2:** Build native descriptors at runtime → **Descriptors testable** ✅
4. **Phase 3:** Integrate with code generator → **ALL tests pass** ✅

---

## ✅ Phase 0: ABI Offset Generation

**Goal:** Auto-generate field offsets for `dds_topic_descriptor_t` from Cyclone DDS source.

### Task 0.1: Add CppAst Package

**File:** `tools/CycloneDDS.CodeGen/CycloneDDS.CodeGen.csproj` (MODIFY)

```xml
<ItemGroup>
  <PackageReference Include="CppAst" Version="0.9.0" />
</ItemGroup>
```

### Task 0.2: Implement Offset Generator

**File:** `tools/CycloneDDS.CodeGen/OffsetGeneration/AbiOffsetGenerator.cs` (NEW)

```csharp
using CppAst;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CycloneDDS.CodeGen.OffsetGeneration;

public static class AbiOffsetGenerator
{
    public static void GenerateFromSource(string cycloneSourcePath, string outputPath)
    {
        var headerPath = Path.Combine(cycloneSourcePath, 
            "src/core/ddsc/include/dds/ddsc/dds_public_impl.h");
        
        if (!File.Exists(headerPath))
            throw new FileNotFoundException($"Cyclone header not found: {headerPath}");
        
        var options = new CppParserOptions();
        options.IncludeFolders.Add(Path.Combine(cycloneSourcePath, "src/core/ddsc/include"));
        options.IncludeFolders.Add(Path.Combine(cycloneSourcePath, "src/ddsrt/include"));
        
        var compilation = CppParser.ParseFile(headerPath, options);
        
        if (compilation.HasErrors)
        {
            var errors = string.Join("\n", compilation.Diagnostics.Messages);
            throw new Exception($"Failed to parse Cyclone headers:\n{errors}");
        }
        
        // Find dds_topic_descriptor_t
        var descriptorStruct = compilation.Classes
            .FirstOrDefault(c => c.Name == "dds_topic_descriptor_t");
        
        if (descriptorStruct == null)
            throw new Exception("Could not find dds_topic_descriptor_t in headers");
        
        // Extract version
        var version = ExtractCycloneVersion(cycloneSourcePath);
        
        // Generate C# code
        var code = GenerateCode(descriptorStruct, version);
        
        File.WriteAllText(outputPath, code);
        Console.WriteLine($"Generated ABI offsets for Cyclone DDS {version}");
        Console.WriteLine($"  Struct size: {descriptorStruct.SizeOf} bytes");
        Console.WriteLine($"  Output: {outputPath}");
    }
    
    private static string GenerateCode(CppClass descriptorStruct, string version)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated from Cyclone DDS source>");
        sb.AppendLine($"// Cyclone DDS Version: {version}");
        sb.AppendLine($"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("namespace CycloneDDS.Runtime.Descriptors;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// ABI offsets for dds_topic_descriptor_t.");
        sb.AppendLine("/// Auto-generated from Cyclone DDS source code.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class AbiOffsets");
        sb.AppendLine("{");
        sb.AppendLine($"    public const string CycloneVersion = \"{version}\";");
        sb.AppendLine();
        
        // Map C field names to C# friendly names
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
                sb.AppendLine($"    public const int {csName} = {field.Offset};");
                
                // For nested structs (type_information, type_mapping)
                // They have structure: { unsigned char* data; uint32_t sz; }
                if (field.Name == "type_information" || field.Name == "type_mapping")
                {
                    sb.AppendLine($"    public const int {csName}_Data = {field.Offset};");
                    // Pointer size depends on platform (8 on x64, 4 on x86)
                    // CppAst should give us the correct offset
                    var dataField = ((CppClass)field.Type).Fields.FirstOrDefault(f => f.Name == "data");
                    var szField = ((CppClass)field.Type).Fields.FirstOrDefault(f => f.Name == "sz");
                    if (szField != null)
                        sb.AppendLine($"    public const int {csName}_Size = {field.Offset + szField.Offset};");
                }
            }
        }
        
        sb.AppendLine();
        sb.AppendLine($"    public const int DescriptorSize = {descriptorStruct.SizeOf};");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private static string ExtractCycloneVersion(string sourcePath)
    {
        // Try VERSION file
        var versionFile = Path.Combine(sourcePath, "VERSION");
        if (File.Exists(versionFile))
            return File.ReadAllText(versionFile).Trim();
        
        // Try CMakeLists.txt
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

### Task 0.3: Create Build Script

**File:** `tools/generate-offsets.ps1` (NEW)

```powershell
# Generate ABI offsets from Cyclone DDS source
param(
    [string]$CycloneSourcePath = "D:\Work\FastCycloneDdsCsharpBindings\cyclonedds-src",
    [string]$OutputPath = "src\CycloneDDS.Runtime\Descriptors\AbiOffsets.g.cs"
)

$generatorDll = "tools\CycloneDDS.CodeGen\bin\Debug\net8.0\CycloneDDS.CodeGen.dll"

Write-Host "Building code generator..." -ForegroundColor Cyan
dotnet build tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj

Write-Host "Generating ABI offsets..." -ForegroundColor Cyan
Write-Host "  Cyclone source: $CycloneSourcePath"
Write-Host "  Output: $OutputPath"

dotnet $generatorDll generate-offsets --source $CycloneSourcePath --output $OutputPath

Write-Host "Done!" -ForegroundColor Green
```

### Testing Phase 0

**Run:** `.\tools\generate-offsets.ps1`

**Expected Output:**
```
Generated ABI offsets for Cyclone DDS 0.11.0
  Struct size: 88 bytes
  Output: src\CycloneDDS.Runtime\Descriptors\AbiOffsets.g.cs
```

**Verify:** `AbiOffsets.g.cs` exists and contains correct constants.

---

## ✅ Phase 1: Descriptor Data Extraction

**Goal:** Parse idlc-generated `.c` files to extract descriptor data.

### Task 1.1: Descriptor Data Model

**File:** `src/CycloneDDS.Runtime/Descriptors/DescriptorData.cs` (NEW)

```csharp
namespace CycloneDDS.Runtime.Descriptors;

public class DescriptorData
{
    public string TypeName { get; set; } = "";
    public uint Size { get; set; }
    public uint Align { get; set; }
    public uint Flagset { get; set; }
    public uint NKeys { get; set; }
    public uint NOps { get; set; }
    public uint[] Ops { get; set; } = Array.Empty<uint>();
    public byte[] TypeInfo { get; set; } = Array.Empty<byte>();
    public byte[] TypeMap { get; set; } = Array.Empty<byte>();
    public KeyDescriptor[] Keys { get; set; } = Array.Empty<KeyDescriptor>();
    public string Meta { get; set; } = "";
}

public class KeyDescriptor
{
    public string Name { get; set; } = "";
    public ushort Flags { get; set; }
    public ushort Index { get; set; }
}
```

### Task 1.2: CppAst Descriptor Extractor

**File:** `tools/CycloneDDS.CodeGen/DescriptorExtraction/DescriptorExtractor.cs` (NEW)

```csharp
using CppAst;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CycloneDDS.CodeGen.DescriptorExtraction;

public static class DescriptorExtractor
{
    public static DescriptorData ExtractFromIdlcOutput(string cFilePath, string cycloneIncludePath)
    {
        var options = new CppParserOptions();
        options.IncludeFolders.Add(cycloneIncludePath);
        
        var compilation = CppParser.ParseFile(cFilePath, options);
        
        if (compilation.HasErrors)
            throw new Exception("Parse failed: " + string.Join("\n", compilation.Diagnostics.Messages));
        
        // Find descriptor variable (e.g., Net_AppId_desc)
        var descriptorVar = compilation.Fields
            .FirstOrDefault(f => f.Name.EndsWith("_desc") && f.Type.GetDisplayName().Contains("dds_topic_descriptor"));
        
        if (descriptorVar == null)
            throw new Exception("Could not find topic descriptor variable");
        
        var data = new DescriptorData();
        
        // Extract from descriptor initializer
        var init = descriptorVar.InitValue as CppInitListExpr;
        // ... (extract m_size, m_align, m_typename, etc. from initializer)
        
        // Extract ops array
        var opsVarName = descriptorVar.Name.Replace("_desc", "_ops");
        var opsVar = compilation.Fields.FirstOrDefault(f => f.Name == opsVarName);
        if (opsVar != null && opsVar.InitValue is CppInitListExpr opsList)
        {
            data.Ops = ExtractUInt32Array(opsList);
        }
        
        // Extract TYPE_INFO_CDR macro
        data.TypeInfo = ExtractByteArrayFromMacro(compilation, "TYPE_INFO_CDR_");
        
        // Extract TYPE_MAP_CDR macro
        data.TypeMap = ExtractByteArrayFromMacro(compilation, "TYPE_MAP_CDR_");
        
        return data;
    }
    
    private static uint[] ExtractUInt32Array(CppInitListExpr list)
    {
        var results = new List<uint>();
        foreach (var element in list.Elements)
        {
            if (element is CppLiteralExpression lit)
            {
                results.Add((uint)Convert.ToUInt64(lit.Value));
            }
        }
        return results.ToArray();
    }
    
    private static byte[] ExtractByteArrayFromMacro(CppCompilation compilation, string macroPrefix)
    {
        var macro = compilation.Macros.FirstOrDefault(m => m.Name.StartsWith(macroPrefix));
        if (macro == null) return Array.Empty<byte>();
        
        // Macro format: (unsigned char []){ 0x60, 0x00, ... }
        var content = macro.Value;
        var match = Regex.Match(content, @"\{(.+?)\}");
        if (!match.Success) return Array.Empty<byte>();
        
        var hexValues = match.Groups[1].Value.Split(',');
        return hexValues.Select(h => Convert.ToByte(h.Trim(), 16)).ToArray();
    }
}
```

### Testing Phase 1

**Test:** Extract from sample idlc output

```csharp
[Fact]
public void ExtractDescriptor_FromIdlcOutput_ParsesCorrectly()
{
    var testFile = "tests/data/AppId.c"; // Sample idlc output
    var data = DescriptorExtractor.ExtractFromIdlcOutput(testFile, cycloneIncludePath);
    
    Assert.Equal("Net::AppId", data.TypeName);
    Assert.True(data.Ops.Length > 0);
    Assert.True(data.TypeInfo.Length > 0);
}
```

---

## ✅ Phase 2: Native Descriptor Builder

**Goal:** Build native `dds_topic_descriptor_t` from extracted data.

### Task 2.1: Native Descriptor Builder

**File:** `src/CycloneDDS.Runtime/Descriptors/NativeDescriptorBuilder.cs` (NEW)

Implementation in design document - use offset-based writes with `AbiOffsets.g.cs`.

### Testing Phase 2

```csharp
[Fact]
public void NativeDescriptor_Build_CreatesValidStruct()
{
    var data = new DescriptorData { /* test data */ };
    var descriptor = new NativeDescriptor(data);
    
    // Verify fields written correctly
    Assert.Equal(data.Size, Marshal.ReadInt32(descriptor.Ptr, AbiOffsets.Size));
    Assert.Equal(data.TypeName, Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(descriptor.Ptr, AbiOffsets.TypeName)));
}
```

---

## ✅ Phase 3: Integration with Code Generator

### Task 3.1: Add idlc Invocation

**Modify:** `tools/CycloneDDS.CodeGen/CodeGenerator.cs`

Add step: After generating `.idl`, invoke `idlc` → extract descriptor → generate C# data class.

### Task 3.2: Update BATCH-12 Code

**Modify:** `src/CycloneDDS.Runtime/DdsWriter.cs`

Replace `IntPtr.Zero` with actual descriptor:

```csharp
// OLD (BATCH-12):
var topic = DdsApi.dds_create_topic(participant, IntPtr.Zero, ...);

// NEW (BATCH-13):
var descriptor = TestMessageDescriptor.GetNative(); // From generated code
var topic = DdsApi.dds_create_topic(participant, descriptor.Ptr, ...);
```

---

## 🧪 Testing Requirements

**Minimum 12 Tests:**

### Offset Generation (3 tests)
1. ✅ `AbiOffsets_GeneratedFromSource_HasCorrectFields`
2. ✅ `AbiOffsets_DescriptorSize_MatchesHeader`
3. ✅ `AbiOffsets_Version_ExtractedCorrectly`

### Descriptor Extraction (4 tests)
4. ✅ `Extractor_ParsesOpsArray_Correctly`
5. ✅ `Extractor_ParsesTypeInfoBlob_Correctly`
6. ✅ `Extractor_ParsesTypeName_Correctly`
7. ✅ `Extractor_HandlesKeysDescriptor_Correctly`

### Native Builder (3 tests)
8. ✅ `NativeDescriptor_Build_WritesCorrectOffsets`
9. ✅ `NativeDescriptor_Dispose_FreesMemory`
10. ✅ `NativeDescriptor_MultipleFields_AllCorrect`

### Integration (2 tests)
11. ✅ `DdsWriter_WithRealDescriptor_CreatesTopicSuccessfully`
12. ✅ `DdsWriter_Write_WithRealDescriptor_Succeeds` (THIS UNBLOCKS BATCH-12!)

---

## 📊 Report Requirements

1. **Implementation Summary**
2. **Test Results** (12+ tests passing)
3. **Developer Insights:**
   - **Q1:** What challenges did you face parsing idlc output with CppAst?
   - **Q2:** How did you verify the native descriptors were built correctly?
   - **Q3:** What edge cases did you discover in descriptor formats?
   - **Q4:** How can this be improved for complex types (unions, sequences)?

---

## 🎯 Success Criteria

1. ✅ `AbiOffsets.g.cs` auto-generated from Cyclone source
2. ✅ Descriptor extraction works for struct types
3. ✅ Native descriptors buildable at runtime
4. ✅ `dds_create_topic` accepts built descriptors
5. ✅ **`dds_write` works with real data** ← KEY SUCCESS
6. ✅ BATCH-12 unblocked - data transmission works
7. ✅ 12+ tests passing

---

## ⚠️ Common Pitfalls

1. **CppAst include paths** - Must point to ALL Cyclone headers
2. **Macro expansion** - Byte array macros need careful parsing
3. **Pointer lifetime** - Descriptors must stay alive while topic exists
4. **Alignment** - Verify offset-based writes preserve alignment
5. **Endianness** - Byte blobs are platform-specific

---

**Focus: Parse idlc C output, build native descriptors, UNBLOCK data transmission in BATCH-12.**
