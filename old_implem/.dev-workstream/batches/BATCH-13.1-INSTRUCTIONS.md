# BATCH-13.1: Topic Descriptor Builder (CORRECTIVE)

**Batch Number:** BATCH-13.1  
**Type:** CORRECTIVE BATCH (fixing rejected BATCH-13)  
**Tasks:** Complete descriptor builder with tests, fix compilation  
**Phase:** Phase 3 - Runtime Components  
**Estimated Effort:** 3-4 days  
**Priority:** CRITICAL  
**Dependencies:** BATCH-13 (rejected)

---

## ⚠️ BATCH-13 WAS REJECTED FOR:

1. ❌ Code doesn't compile
2. ❌ ZERO tests (required 12, delivered 0)
3. ❌ Incomplete implementation (keys skipped with TODO)
4. ❌ No validation or documentation

**You will NOW complete this properly.**

---

## ⚠️ CRITICAL: Treat Tools as Permanent Infrastructure

**These are NOT throw-away tools!** They will be used:
- When Cyclone DDS updates
- For new type generation
- For debugging ABI issues
- On different platforms

**Required Quality:**
- ✅ Production code quality
- ✅ Comprehensive error handling
- ✅ Full documentation (README files)
- ✅ Extensive testing
- ✅ No hardcoded paths
- ✅ Clear logging

**Deliverables:**
- `tools/README.md` - Tool overview and usage
- `tools/OffsetGeneration/README.md` - Offset generation guide
- `tools/generate-offsets.ps1` - User-friendly script
- All tools fully tested and documented

---

## 🔄 MANDATORY WORKFLOW

1. **Fix compilation** → Build succeeds ✅
2. **Implement key descriptors** → No TODO comments ✅
3. **Write 12+ tests** → All passing ✅
4. **Add documentation** → READMEs complete ✅
5. **Validate tools** → Scripts work from any directory ✅


IMPORTANT: you need to force the xtypes 'appendable' mode when generating the code with idlc!
---

## ✅ Task 1: Fix Compilation

**Current Error:** CodeGen doesn't build

**Action:**
1. Run `dotnet build tools/CycloneDDS.CodeGen/CycloneDDS.CodeGen.csproj`
2. Fix ALL compilation errors
3. Verify clean build: `dotnet build` from root

**Success Criteria:** Zero build errors

---

## ✅ Task 2: Complete Key Descriptor Implementation

**Current Issue:** `NativeDescriptor.cs` line 42 sets Keys to `IntPtr.Zero`

**Required Implementation:**

```csharp
// Instead of:
WriteIntPtr(Ptr, AbiOffsets.Keys, IntPtr.Zero); // WRONG!

// Implement:
IntPtr ptrKeys = AllocKeyDescriptors(data.Keys);
WriteIntPtr(Ptr, AbiOffsets.Keys, ptrKeys);

private IntPtr AllocKeyDescriptors(KeyDescriptor[]? keys)
{
    if (keys == null || keys.Length == 0) return IntPtr.Zero;
    
    // dds_key_descriptor_t has: char* name, uint32_t flags, uint32_t index
    // Size: IntPtr.Size + 4 + 4 (platform dependent)
    int keyDescSize = IntPtr.Size + 8;
    int totalSize = keys.Length * keyDescSize;
    
    var ptr = AllocRaw(totalSize);
    
    for (int i = 0; i < keys.Length; i++)
    {
        int offset = i * keyDescSize;
        IntPtr namePtr = AllocString(keys[i].Name);
        
        WriteIntPtr(ptr, offset, namePtr);
        WriteInt32(ptr, offset + IntPtr.Size, (int)keys[i].Flags);
        WriteInt32(ptr, offset + IntPtr.Size + 4, (int)keys[i].Index);
    }
    
    return ptr;
}
```

**Test Required:**
```csharp
[Fact]
public void NativeDescriptor_WithKeys_AllocatesCorrectly()
{
    var data = new DescriptorData
    {
        TypeName = "Test",
        NKeys = 2,
        Keys = new[]
        {
            new KeyDescriptor { Name = "Id", Flags = 0, Index = 0 },
            new KeyDescriptor { Name = "Name", Flags = 0, Index = 1 }
        }
    };
    
    using var descriptor = new NativeDescriptor(data);
    
    var keysPtr = Marshal.ReadIntPtr(descriptor.Ptr, AbiOffsets.Keys);
    Assert.NotEqual(IntPtr.Zero, keysPtr);
    
    // Verify first key
    var key0NamePtr = Marshal.ReadIntPtr(keysPtr, 0);
    var key0Name = Marshal.PtrToStringAnsi(key0NamePtr);
    Assert.Equal("Id", key0Name);
}
```

---

## ✅ Task 3: Write Required Tests (12 minimum)

**File:** `tests/CycloneDDS.Runtime.Tests/Descriptors/NativeDescriptorTests.cs` (NEW)

```csharp
using Xunit;
using CycloneDDS.Runtime.Descriptors;
using System.Runtime.InteropServices;

namespace CycloneDDS.Runtime.Tests.Descriptors;

public class NativeDescriptorTests
{
    [Fact]
    public void NativeDescriptor_Build_WritesCorrectOffsets()
    {
        var data = new DescriptorData
        {
            TypeName = "Test::Message",
            Size = 24,
            Align = 8,
            NOps = 5,
            Ops = new uint[] { 0x01, 0x02, 0x03 }
        };
        
        using var descriptor = new NativeDescriptor(data);
        
        Assert.Equal(24, Marshal.ReadInt32(descriptor.Ptr, AbiOffsets.Size));
        Assert.Equal(8, Marshal.ReadInt32(descriptor.Ptr, AbiOffsets.Align));
        Assert.Equal(5, Marshal.ReadInt32(descriptor.Ptr, AbiOffsets.NOps));
    }
    
    [Fact]
    public void NativeDescriptor_TypeName_AllocatedCorrectly()
    {
        var data = new DescriptorData { TypeName = "Test::Type" };
        using var descriptor = new NativeDescriptor(data);
        
        var namePtr = Marshal.ReadIntPtr(descriptor.Ptr, AbiOffsets.TypeName);
        var name = Marshal.PtrToStringAnsi(namePtr);
        
        Assert.Equal("Test::Type", name);
    }
    
    [Fact]
    public void NativeDescriptor_OpsArray_CopiedCorrectly()
    {
        var ops = new uint[] { 0xDEADBEEF, 0xCAFEBABE, 0x12345678 };
        var data = new DescriptorData { Ops = ops, NOps = 3 };
        
        using var descriptor = new NativeDescriptor(data);
        
        var opsPtr = Marshal.ReadIntPtr(descriptor.Ptr, AbiOffsets.Ops);
        var readOps = new int[3];
        Marshal.Copy(opsPtr, readOps, 0, 3);
        
        Assert.Equal((int)0xDEADBEEF, readOps[0]);
    }
    
    [Fact]
    public void NativeDescriptor_WithKeys_AllocatesKeyArray()
    {
        // As shown above
    }
    
    [Fact]
    public void NativeDescriptor_Dispose_FreesAllMemory()
    {
        var data = new DescriptorData
        {
            TypeName = "Test",
            Ops = new uint[] { 1, 2, 3 },
            TypeInfo = new byte[] { 0x60, 0x00 }
        };
        
        var descriptor = new NativeDescriptor(data);
        var ptr = descriptor.Ptr;
        
        descriptor.Dispose();
        
        Assert.Equal(IntPtr.Zero, descriptor.Ptr);
        // Memory freed - no way to verify without native tools
    }
    
    [Fact]
    public void NativeDescriptor_TypeInfoBlob_CopiedCorrectly()
    {
        var typeInfo = new byte[] { 0x60, 0x00, 0x00, 0x00 };
        var data = new DescriptorData { TypeInfo = typeInfo };
        
        using var descriptor = new NativeDescriptor(data);
        
        var infoPtr = Marshal.ReadIntPtr(descriptor.Ptr, AbiOffsets.TypeInfo_Data);
        var size = Marshal.ReadInt32(descriptor.Ptr, AbiOffsets.TypeInfo_Size);
        
        Assert.NotEqual(IntPtr.Zero, infoPtr);
        Assert.Equal(4, size);
        
        var readBytes = new byte[4];
        Marshal.Copy(infoPtr, readBytes, 0, 4);
        Assert.Equal(typeInfo, readBytes);
    }
    
    // ... 6 more tests as required
}
```

**File:** `tests/CycloneDDS.CodeGen.Tests/OffsetGeneration/AbiOffsetGeneratorTests.cs` (NEW)

```csharp
[Fact]
public void AbiOffsets_GeneratedFile_HasRequiredConstants()
{
    // Verify AbiOffsets.g.cs was generated correctly
    Assert.True(AbiOffsets.DescriptorSize > 0);
    Assert.True(AbiOffsets.Size >= 0);
    Assert.True(AbiOffsets.TypeName >= 0);
}

[Fact]
public void AbiOffsets_DescriptorSize_MatchesExpected()
{
    // For x64, descriptor should be ~88-96 bytes
    Assert.InRange(AbiOffsets.DescriptorSize, 80, 120);
}
```

**Minimum 12 tests total** across:
- NativeDescriptor (6 tests)
- AbiOffsets (2 tests)
- DescriptorExtractor (2 tests)
- Integration (2 tests)

---

## ✅ Task 4: Add Tool Documentation

**File:** `tools/README.md` (NEW)

```markdown
# Cyclone DDS Code Generation Tools

Permanent infrastructure for working with Cyclone DDS descriptors and offsets.

## Offset Generation

**Purpose:** Extract ABI offsets from Cyclone DDS source code.

### When to Run

- After updating to a new Cyclone DDS version
- When supporting a new platform (ARM, x86, etc.)
- When ABI-related bugs are suspected

### Usage

```powershell
.\tools\generate-offsets.ps1 -CycloneSourcePath "D:\cyclone-src"
```

**Output:** `src\CycloneDDS.Runtime\Descriptors\AbiOffsets.g.cs`

### Requirements

- Cyclone DDS source code (from GitHub or release)
- .NET 8.0 SDK
- CppAst NuGet package (auto-installed)

### Troubleshooting

**Error: "Could not find dds_public_impl.h"**
- Verify Cyclone source path is correct
- Check that Cyclone headers are in `src/core/ddsc/include/`

**Error: "Failed to parse Cyclone headers"**
- Ensure all Cyclone include paths are accessible
- Check for Cyclone version compatibility

## Descriptor Extraction

**Purpose:** Parse idlc-generated C code to extract topic descriptors.

### When to Run

Automatically runs during code generation for each `[DdsTopic]` type.

### Manual Usage

```powershell
dotnet run --project tools/CycloneDDS.CodeGen -- extract-descriptor --input Type.c
```

## Maintenance

These tools are **permanent infrastructure**. When updating:

1. Test with multiple Cyclone versions
2. Update documentation
3. Add tests for new functionality
4. Verify scripts work from any directory
```

**File:** `tools/OffsetGeneration/README.md` (NEW)

```markdown
# ABI Offset Generation

Extracts struct field offsets from Cyclone DDS headers using CppAst.

## How It Works

1. Parses `dds_public_impl.h` using CppAst (libclang-based parser)
2. Finds `dds_topic_descriptor_t` struct definition
3. Extracts field offsets (CppAst provides these from C compiler)
4. Generates `AbiOffsets.g.cs` with constants

## Why This Approach

- **No C compiler needed at runtime** - Offsets extracted at build time
- **Version-safe** - Detects Cyclone version automatically
- **Cross-platform** - Works on Windows/Linux/macOS

## Output Format

```csharp
public static class AbiOffsets
{
    public const string CycloneVersion = "0.11.0";
    public const int Size = 0;
    public const int Align = 4;
    // ... all field offsets
    public const int DescriptorSize = 88;
}
```
```

---

## ✅ Task 5: Create User-Friendly Scripts

**File:** `tools/generate-offsets.ps1` (UPDATE - add error handling)

```powershell
# Generate ABI offsets from Cyclone DDS source
param(
    [string]$CycloneSourcePath = "$PSScriptRoot\..\cyclonedds-src",
    [string]$OutputPath = "$PSScriptRoot\..\src\CycloneDDS.Runtime\Descriptors\AbiOffsets.g.cs"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Cyclone DDS Offset Generator ===" -ForegroundColor Cyan

# Validate Cyclone source
if (-not (Test-Path $CycloneSourcePath)) {
    Write-Host "ERROR: Cyclone source not found at: $CycloneSourcePath" -ForegroundColor Red
    Write-Host "Please specify correct path with -CycloneSourcePath parameter" -ForegroundColor Yellow
    exit 1
}

$headerPath = Join-Path $CycloneSourcePath "src\core\ddsc\include\dds\ddsc\dds_public_impl.h"
if (-not (Test-Path $headerPath)) {
    Write-Host "ERROR: Header not found: $headerPath" -ForegroundColor Red
    Write-Host "Is this a valid Cyclone DDS source directory?" -ForegroundColor Yellow
    exit 1
}

# Build code generator
Write-Host "Building code generator..." -ForegroundColor Cyan
dotnet build "$PSScriptRoot\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj" -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to build code generator" -ForegroundColor Red
    exit 1
}

# Run generator
Write-Host "Generating offsets from Cyclone source..." -ForegroundColor Cyan
Write-Host "  Source: $CycloneSourcePath" -ForegroundColor Gray
Write-Host "  Output: $OutputPath" -ForegroundColor Gray

$generatorDll = "$PSScriptRoot\CycloneDDS.CodeGen\bin\Release\net8.0\CycloneDDS.CodeGen.dll"
dotnet $generatorDll generate-offsets --source $CycloneSourcePath --output $OutputPath

if ($LASTEXITCODE -eq 0) {
    Write-Host "SUCCESS: Offsets generated!" -ForegroundColor Green
} else {
    Write-Host "ERROR: Offset generation failed" -ForegroundColor Red
    exit 1
}
```

---

## 🧪 Testing Requirements

**ALL 12 tests MUST pass:**

1. ✅ `NativeDescriptor_Build_WritesCorrectOffsets`
2. ✅ `NativeDescriptor_TypeName_AllocatedCorrectly`
3. ✅ `NativeDescriptor_OpsArray_CopiedCorrectly`
4. ✅ `NativeDescriptor_WithKeys_AllocatesKeyArray`
5. ✅ `NativeDescriptor_Dispose_FreesAllMemory`
6. ✅ `NativeDescriptor_TypeInfoBlob_CopiedCorrectly`
7. ✅ `AbiOffsets_GeneratedFile_HasRequiredConstants`
8. ✅ `AbiOffsets_DescriptorSize_MatchesExpected`
9. ✅ `DescriptorExtractor_ParsesIdlcOutput`
10. ✅ `DescriptorExtractor_ExtractsOpsArray`
11. ✅ `Integration_DdsWriter_WithDescriptor_Works`
12. ✅ `Integration_DdsReader_WithDescriptor_Works`

---

## 📊 Report Requirements

1. **Fixed Issues** - List each BATCH-13 issue and how you fixed it
2. **Test Results** - All 12+ tests passing
3. **Tool Documentation** - Verify READMEs are complete
4. **Developer Insights:**
   - **Q1:** What did you learn from having BATCH-13 rejected?
   - **Q2:** How did you test the key descriptor implementation?
   - **Q3:** What challenges did you face with tool documentation?

---

## 🎯 Success Criteria

1. ✅ Code compiles cleanly
2. ✅ ALL 12+ tests passing
3. ✅ Key descriptors fully implemented (NO TODO comments)
4. ✅ `tools/README.md` complete with usage examples
5. ✅ `tools/OffsetGeneration/README.md` complete
6. ✅ `generate-offsets.ps1` works from any directory
7. ✅ DdsWriter/DdsReader work with real descriptors
8. ✅ No hardcoded paths in production code

---

## ⚠️ Final Warning

**This is a corrective batch. No excuses, no shortcuts.**

- Code MUST compile
- Tests MUST exist
- Implementation MUST be complete
- Documentation MUST be thorough

**Remember:** These tools are PERMANENT INFRASTRUCTURE, not throwaway code.

---

**Focus: Complete BATCH-13 properly - compiling code, full tests, production quality tools.**
