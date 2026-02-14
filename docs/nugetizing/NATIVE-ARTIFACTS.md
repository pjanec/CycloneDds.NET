# Native Artifacts Reference

**Location:** `D:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin`

## Windows x64 Artifacts

### Runtime Dependencies

| File | Purpose | Required At |
|------|---------|-------------|
| **ddsc.dll** | Main Cyclone DDS runtime library | Runtime (P/Invoke) |
| **idlc.exe** | IDL compiler tool | Build-time (code generation) |
| **cycloneddsidl.dll** | IDL support library | Build-time (idlc.exe dependency) |
| **cycloneddsidlc.dll** | IDL compiler library | Build-time (idlc.exe dependency) |
| **cycloneddsidljson.dll** | IDL JSON output library | Build-time (idlc.exe dependency) |

### VC++ Runtime Dependencies

These are Microsoft Visual C++ 2015-2022 Redistributable DLLs, required by the native Cyclone DDS binaries:

| File | Purpose |
|------|---------|
| **msvcp140.dll** | C++ Standard Library |
| **msvcp140_1.dll** | C++ Standard Library (additional) |
| **msvcp140_2.dll** | C++ Standard Library (additional) |
| **msvcp140_atomic_wait.dll** | C++ atomic wait support |
| **msvcp140_codecvt_ids.dll** | C++ code conversion |
| **vcruntime140.dll** | Visual C++ Runtime |
| **vcruntime140_1.dll** | Visual C++ Runtime (additional) |
| **concrt140.dll** | Concurrency Runtime |

### Optional Tools

| File | Purpose | Include in Package? |
|------|---------|---------------------|
| **ddsperf.exe** | DDS performance testing utility | Optional (useful for testing) |

## Usage in Project

### Current Implementation

**Runtime (CycloneDDS.Core):**
```csharp
// src/CycloneDDS.Runtime/Interop/DdsApi.cs
public const string DLL_NAME = "ddsc";  // Resolves to ddsc.dll on Windows
```

**Code Generation (CycloneDDS.CodeGen):**
- Calls `idlc.exe` during build
- `IdlcRunner.cs` searches for idlc.exe in multiple locations

**Test Projects:**
- Copy all DLLs from `cyclone-compiled\bin\*.dll` to output
- Example: `tests/CycloneDDS.Core.Tests/CycloneDDS.Core.Tests.csproj`

### NuGet Package Strategy

**Include in `runtimes/win-x64/native/`:**
- ✅ All DLLs (ddsc.dll + dependencies)
- ✅ idlc.exe (required for code generation)
- ✅ cycloneddsidl*.dll (required by idlc.exe)
- ✅ VC++ runtime DLLs (required by native code)
- ⚠️ ddsperf.exe (optional, but include for completeness)

**MSBuild targets will:**
1. Locate all files from package's `runtimes/<rid>/native/`
2. Copy all to `$(OutputPath)` after build
3. Ensures both runtime and build-time tools are available

## Linux Support (Future)

When Linux x64 support is added, equivalent files will be:

| Windows | Linux |
|---------|-------|
| ddsc.dll | libddsc.so |
| idlc.exe | idlc (ELF executable) |
| cycloneddsidl.dll | libcycloneddsidl.so |
| cycloneddsidlc.dll | libcycloneddsidlc.so |
| cycloneddsidljson.dll | libcycloneddsidljson.so |

VC++ runtime dependencies are Windows-specific and not needed on Linux.

## Build Process

**Native build produces these artifacts:**

```powershell
# Build native Cyclone DDS
cd cyclonedds\build
cmake .. -DCMAKE_INSTALL_PREFIX=..\..\cyclone-compiled
cmake --build . --config Release --target install

# Outputs to: cyclone-compiled\bin\
# - All DLLs and EXEs listed above
```

**Packaging workflow:**

```powershell
# 1. Build native components
.\build\native-win.ps1

# 2. Native artifacts staged to artifacts\native\win-x64\
Copy-Item cyclone-compiled\bin\* artifacts\native\win-x64\

# 3. Pack NuGet includes artifacts\native\win-x64\* in package
dotnet pack -c Release
```

## Important Notes

1. **ddsc.dll is the main runtime DLL** - NOT "cyclonedds.dll" (that name doesn't exist)

2. **All IDL libraries must be included** - idlc.exe cannot run without:
   - cycloneddsidl.dll
   - cycloneddsidlc.dll  
   - cycloneddsidljson.dll

3. **VC++ Runtime requirements:**
   - These DLLs are compiler-generated dependencies
   - Must be distributed with the package OR users must have VC++ Redistributable installed
   - Package them to ensure "zero-install" experience

4. **Copy all DLLs, not selective** - Safest approach for NuGet package:
   - Include all `*.dll` files from cyclone-compiled\bin
   - Ensures no missing dependencies
   - Disk space cost is minimal (~few MB total)

## Validation Checklist

Before packing NuGet package, verify all files exist:

```powershell
$required = @(
    "ddsc.dll",
    "idlc.exe",
    "cycloneddsidl.dll",
    "cycloneddsidlc.dll",
    "cycloneddsidljson.dll",
    "msvcp140.dll",
    "vcruntime140.dll",
    "concrt140.dll"
)

foreach ($file in $required) {
    if (!(Test-Path "artifacts\native\win-x64\$file")) {
        Write-Error "Missing required file: $file"
    }
}
```

---

**Last Updated:** February 14, 2026  
**Source:** Analysis of actual repository and build outputs
