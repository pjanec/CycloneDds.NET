# FCDC-037: Multi-Platform ABI Support

**Task ID:** FCDC-037  
**Phase:** 5 - Advanced Features & Polish  
**Priority:** Medium  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-015 (AbiOffsets)  
**Design Reference:** `docs/ADVANCED-OPTIMIZATIONS-DESIGN.md` §6

---

## Objective

Address cross-platform ABI compatibility for scenarios where build platform ≠ deployment platform.

---

## Problem Statement

**Current Limitation:**
```
Developer builds on: Windows x64
Application deploys to: Linux x64
```

**Risk:**
- `AbiOffsets.g.cs` generated for Windows ABI
- Linux has different `sizeof(long)`, struct padding
- Result: **CRASH** or silent data corruption!

**Root Cause:** `AbiOffsetGenerator` runs on build machine, uses build machine's C compiler ABI.

---

## Solution Options

### Option A: Document Limitation (Phase 1 - NOW)

**Immediate action with zero code changes.**

**File:** `README.md`

```markdown
## Known Limitations

### Cross-Platform Builds

⚠️ **Build platform MUST match deployment platform.**

| Build On | Deploy To | Status |
|----------|-----------|--------|
| Windows x64 | Windows x64 | ✅ Supported |
| Linux x64 | Linux x64 | ✅ Supported |
| macOS ARM64 | macOS ARM64 | ✅ Supported |
| Windows x64 | Linux x64 | ❌ **UNSUPPORTED** - May crash! |
| Linux x64 | Windows x64 | ❌ **UNSUPPORTED** - Will crash! |

**Reason:** ABI offsets (struct layout, padding, alignment) are generated 
at build time for the build machine's architecture.

**Workaround:** Build on the same platform you deploy to:
- Use Docker for Linux builds
- Use GitHub Actions matrix for multi-platform CI
- Separate build pipelines per platform

**Future:** This limitation will be removed in a future release.
```

**Deliverables:**
1. Update README.md with limitation
2. Add to docs/KNOWN-LIMITATIONS.md (create if missing)
3. Add warning in NuGet package description

**Effort:** 1 hour

---

### Option B: Multi-Platform Generation (Phase 2 - FUTURE)

**Generate platform-specific offset files.**

**Approach:**
```csharp
// tools/AbiOffsetGenerator/Program.cs

// Generate for multiple platforms
var platforms = new[]
{
    ("win-x64", windowsHeaders),
    ("linux-x64", linuxHeaders),
    ("linux-arm64", linuxArmHeaders),
    ("osx-arm64", macOsHeaders)
};

foreach (var (rid, headers) in platforms)
{
    var offsets = ExtractOffsets(headers);
    EmitPlatformSpecificFile(rid, offsets);
}
```

**Generated Files:**
```
src/CycloneDDS.Runtime/Descriptors/AbiOffsets.win-x64.g.cs
src/CycloneDDS.Runtime/Descriptors/AbiOffsets.linux-x64.g.cs
src/CycloneDDS.Runtime/Descriptors/AbiOffsets.linux-arm64.g.cs
```

**Runtime Selection:**
```csharp
// AbiOffsets.cs (partial)
public static partial class AbiOffsets
{
    static AbiOffsets()
    {
        // Select correct platform at runtime
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && 
            RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            LoadWindowsX64Offsets();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
                 RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            LoadLinuxX64Offsets();
        }
        // ... etc
    }
}
```

**Requirements:**
1. Access to Cyclone DDS headers for each platform
2. Cross-compilation toolchain or pre-built headers
3. CI pipeline for multi-platform generation

**Effort:** 3-4 days

---

### Option C: Runtime Detection (Phase 3 - ADVANCED)

**Native shim exports offsets at runtime.**

**Native Shim (cycshim.c):**
```c
// Compiled separately per platform
typedef struct {
    size_t descriptor_size;
    size_t type_name_offset;
    size_t keys_offset;
    // ... all offsets
} abi_offsets_t;

DLL_EXPORT void get_abi_offsets(abi_offsets_t* offsets)
{
    offsets->descriptor_size = sizeof(dds_topic_descriptor_t);
    offsets->type_name_offset = offsetof(dds_topic_descriptor_t, m_typename);
    // ... etc
}
```

**C# Side:**
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct AbiOffsetsNative
{
    public int DescriptorSize;
    public int TypeNameOffset;
    // ... etc
}

[DllImport("cycshim")]
private static extern void get_abi_offsets(out AbiOffsetsNative offsets);

public static class AbiOffsets
{
    static AbiOffsets()
    {
        get_abi_offsets(out var native);
        DescriptorSize = native.DescriptorSize;
        TypeName = native.TypeNameOffset;
        // ... etc
    }
}
```

**Benefits:**
- 100% correct for deployed platform
- No cross-compilation needed
- Single build works everywhere

**Costs:**
- Adds native dependency (cycshim.dll/so)
- Slightly slower startup (one-time native call)

**Effort:** 4-5 days (includes native build integration)

---

## Recommended Implementation Plan

**Phase 1 (This Task):**
1. Document limitation in README.md
2. Add to KNOWN-LIMITATIONS.md
3. Test multi-platform failure mode (Windows build → Linux run)
4. Document expected error (access violation or assertion)

**Phase 2 (Future - FCDC-037.1):**
5. Implement Option B (multi-platform generation)
6. Add CI matrix for multi-platform builds
7. Verify each platform can run its own build

**Phase 3 (Future - FCDC-037.2):**
8. Evaluate Option C if cross-platform builds required
9. Only if users demand Windows build → Linux deploy

---

## Testing Requirements

### Phase 1: Limitation Documentation

1. ✅ README updated with clear warning
2. ✅ Docs/KNOWN-LIMITATIONS.md created
3. ✅ Manual test: Build on Windows, run on Linux (expected crash)
4. ✅ Document crash symptom (access violation at descriptor creation)

### Phase 2: Multi-Platform (Future)

1. Generate offsets for win-x64, linux-x64, linux-arm64
2. Verify runtime platform detection
3. Test each platform with its generated offsets
4. Cross-platform CI pipeline

---

## Acceptance Criteria (Phase 1)

1. ✅ README contains clear cross-platform limitation section
2. ✅ KNOWN-LIMITATIONS.md exists and documents ABI issue
3. ✅ Tested: Windows build crashes gracefully on Linux
4. ✅ Error message suggests checking platform match
5. ✅ NuGet package description warns about limitation

---

## Design Reference

See `docs/ADVANCED-OPTIMIZATIONS-DESIGN.md` Section 6: Multi-Platform ABI Constraints

**Key Design Points:**
- Phase 1: Document limitation (no code changes)
- Phase 2: Multi-platform offset generation
- Phase 3: Runtime detection via native shim
- Crash mode: Access violation or assertion in dds_create_topic
