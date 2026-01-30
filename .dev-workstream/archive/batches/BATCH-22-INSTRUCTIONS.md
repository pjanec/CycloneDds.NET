# BATCH-22: Instance Management (Keyed Topics)

**Batch Number:** BATCH-22  
**Tasks:** FCDC-EXT05 (Instance Management for Keyed Topics)  
**Phase:** Stage 3.75 - Extended DDS API - Modern C# Idioms  
**Estimated Effort:** 3-4 days  
**Priority:** **MEDIUM** (Essential for keyed topic production systems)  
**Dependencies:** BATCH-18 (Type Auto-Discovery + Read/Take), BATCH-19 (Async/Events), BATCH-14 (Instance Lifecycle), BATCH-21 (Keyed Serialization Tests)

---

## 📋 Onboarding & Workflow

### Developer Instructions

Welcome to **BATCH-22**, continuing **Stage 3.75: Extended DDS API**! This batch implements **Instance Management** for keyed topics, enabling O(1) lookup and filtering by instance handle. This is CRITICAL for systems tracking many individual objects (e.g., fleet management, sensor networks, air traffic control).

**What This Batch Completes:**
1. **Instance Lookup** - Get handle for specific key (`LookupInstance`)
2. **Instance-Specific Read/Take** - Read or take data for one specific instance only
3. **Instance Handle Management** - Handle persistence, Nil handling, lifecycle tracking
4. **Re-Enable Skipped Tests** - Fix 3 tests from BATCH-18 that were skipped pending keyed topic support

**Why This Matters:**
- **Performance:** O(1) access to specific instances vs iterating through all data
- **Use Cases:** "Give me the latest state for *this specific* vehicle/sensor/entity"
- **Lifecycle:** Proper tracking of instance states (ALIVE, DISPOSED, NO_WRITERS)

**YOU ARE A NEW DEVELOPER**:  
This batch assumes you're joining the project fresh. All tools, paths, and build instructions are provided. ASK NO QUESTIONS - this document is complete.

---

### Required Reading (IN ORDER)

**READ THESE BEFORE STARTING:**

1. **Workflow Guide:** `D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\README.md`  
   - Batch system, report requirements, testing standards

2. **Dev Lead Guide:** `D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\DEV-LEAD-GUIDE.md`  
   - Section: "Test Quality Standards" (lines 407-667) ← **CRITICAL**

3. **🌟 PRIMARY DESIGN (ADVISOR'S RECOMMENDATIONS):** `D:\Work\FastCycloneDdsCsharpBindings\docs\instance-management-xcdr2-design-talk.md` ← **READ FIRST - MOST IMPORTANT**  
   - **THIS IS YOUR IMPLEMENTATION GUIDE** - Advisor has superior understanding vs outdated task specs
   - Zero-allocation serdata-based lookup approach
   - Native extension implementation (Steps 1-5 with exact C code)
   - Lines 169-232: **Exact LookupInstance implementation pattern**
   - Lines 237-299: **Exact Read/TakeInstance implementation pattern**

4. **XCDR2 Internals:** `D:\Work\FastCycloneDdsCsharpBindings\docs\cyclone-dds-xcdr2-serialization-internal.md`  
   - Key hash computation, composite key handling (lines 33-42)
   - Serdata ops and kind parameter (SDK_KEY vs SDK_DATA)
   - Read this to understand WHY the advisor's approach works

5. **Reference Only (Outdated):** `D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md`  
   - Section: FCDC-EXT05 (lines 1858-1959)
   - ⚠️ **NOTE:** Original task spec is outdated - advisor's design-talk supersedes this
   - Use only for test requirements and success criteria, NOT implementation approach

6. **Reference Only (Alternative Approach):** `D:\Work\FastCycloneDdsCsharpBindings\docs\EXTENDED-DDS-API-DESIGN.md`  
   - Section 8: Instance Management
   - ⚠️ **NOTE:** This suggests standard DDS APIs - advisor's serdata approach is superior
   - Reference only for understanding DDS concepts

7. **Native DDS Headers:** (Reference as needed during implementation)  
   - `D:\Work\FastCycloneDdsCsharpBindings\cyclonedds\src\core\ddsc\include\dds\ddsc\dds_public_api.h`
   - `D:\Work\FastCycloneDdsCsharpBindings\cyclonedds\src\core\ddsi\include\dds\ddsi\ddsi_serdata.h`
   - Contains native function signatures, parameter types, return codes

8. **Previous Batch Reviews:**  
   - `D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reviews\BATCH-18-REVIEW.md` - Type Auto-Discovery
   - `D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reviews\BATCH-19-REVIEW.md` - Async/Events
   - `D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reviews\BATCH-14-REVIEW.md` - Instance Lifecycle (Dispose/Unregister)

---

### Repository Structure

```
D:\Work\FastCycloneDdsCsharpBindings\
├── Src\
│   └── CycloneDDS.Runtime\           # ← YOU WORK HERE
│       ├── DdsReader.cs              # ← MODIFY (add LookupInstance, ReadInstance, TakeInstance)
│       ├── DdsInstanceHandle.cs      # ← NEW FILE (handle struct)
│       └── Interop\
│           └── DdsApi.cs             # ← MODIFY (add instance P/Invoke APIs)
│
├── tests\
│   └── CycloneDDS.Runtime.Tests\     # Runtime tests
│       ├── KeyedTestMessage.cs       # ← NEW FILE (keyed test type with [DdsKey])
│       ├── InstanceManagementTests.cs# ← NEW FILE (9+ new tests)
│       └── IntegrationTests.cs       # ← MODIFY (re-enable 3 skipped tests lines 365-428)
│
├── cyclonedds\                       # ← NATIVE CYCLONE DDS SOURCE CODE
│   ├── src\
│   │   ├── core\
│   │   │   ├── ddsc\                 # DDS C API implementation
│   │   │   │   ├── include\dds\ddsc\
│   │   │   │   │   └── dds_public_api.h    # ← Public API declarations
│   │   │   │   └── src\
│   │   │   │       ├── dds_topic.c         # Topic/instance operations
│   │   │   │       ├── dds_reader.c        # Reader implementation
│   │   │   │       └── dds_write.c         # Writer implementation
│   │   │   └── ddsi\                # DDSI/RTPS protocol layer
│   │   │       ├── include\dds\ddsi\
│   │   │       │   ├── ddsi_serdata.h      # ← Serdata structures
│   │   │       │   └── ddsi_sertype.h      # ← Sertype structures
│   │   │       └── src\
│   │   │           └── ddsi_serdata_default.c  # Default serdata implementation
│   │   └── CMakeLists.txt            # Build configuration
│   └── build\                        # ← CMAKE BUILD DIRECTORY
│
├── cyclone-compiled\                 # ← COMPILED BINARIES OUTPUT
│   └── bin\
│       ├── ddsc.dll                  # ← DDS C library (runtime)
│       ├── ddsc.lib                  # Import library
│       ├── idlc.exe                  # IDL compiler
│       └── (other tools)
│
└── .dev-workstream\
    ├── batches\
    │   └── BATCH-22-INSTRUCTIONS.md  # ← This file
    └── reports\
        └── BATCH-22-REPORT.md        # ← Submit your report here
```

---

### Critical Tool & Library Locations

#### Native Cyclone DDS Source Code

**Location:** `D:\Work\FastCycloneDdsCsharpBindings\cyclonedds\`  
**Purpose:** You can inspect and ADD DEBUG PRINTS to the native code as needed  
**Key Directories:**
- `cyclonedds\src\core\ddsc\src\` - DDS C API implementation (you may add debug prints here)
- `cyclonedds\src\core\ddsi\src\` - Protocol layer internals

**Important Headers to Reference:**
- `cyclonedds\src\core\ddsc\include\dds\ddsc\dds_public_api.h` - Public API signatures
- `cyclonedds\src\core\ddsc\include\dds\ddsc\dds_public_impl.h` - Internal structures
- `cyclonedds\src\core\ddsi\include\dds\ddsi\ddsi_serdata.h` - Serdata operations

**Can I Modify Native Code?**  
✅ YES - You may add debug `printf()` statements to understand behavior  
✅ YES - You may modify native code if needed for debugging (document changes)  
⚠️ IMPORTANT - If you modify native code, you MUST rebuild ddsc.dll (instructions below)

---

#### Compiling Native Cyclone DDS (CMake System)

**When to Rebuild:**
- You added debug prints to native `.c` files
- You modified native function implementations
- You want to trace native function calls

**Build Steps (PowerShell):**

```powershell
# 1. Navigate to build directory
cd D:\Work\FastCycloneDdsCsharpBindings\cyclonedds\build

# 2. Configure CMake (only needed once or after clean)
cmake .. -G "Visual Studio 17 2022" -A x64 `
  -DCMAKE_INSTALL_PREFIX="D:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled" `
  -DBUILD_SHARED_LIBS=ON `
  -DENABLE_SECURITY=OFF `
  -DENABLE_LIFESPAN=OFF

# 3. Build (Debug or Release)
cmake --build . --config Debug

# OR for Release build (faster, no debug symbols):
cmake --build . --config Release

# 4. Install to cyclone-compiled directory
cmake --install . --config Debug
# OR
cmake --install . --config Release
```

**Output Locations After Build:**
- DLL: `D:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\ddsc.dll`
- LIB: `D:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\lib\ddsc.lib`
- Headers: `D:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\include\`

**⚠️ IMPORTANT:** After rebuilding, run tests to ensure no regressions:
```powershell
dotnet test D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj
```

---

#### DDS Native Library (Current Build)

**Location:** `D:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\ddsc.dll`  
**Custom Exports:** Already includes serdata extensions from previous batches:
- `dds_create_serdata_from_cdr`
- `dds_takecdr` / `dds_readcdr`
- `dds_dispose_serdata`
- `dds_unregister_serdata`
- `ddsi_serdata_unref`
- `dds_get_topic_sertype`
- `ddsi_serdata_from_ser_iov`

**You MAY need to add:** (Check if these exist using `dumpbin` or implement if missing)
- `dds_lookup_instance` (standard API - should exist)
- `dds_take_instance` (standard API - should exist)
- `dds_read_instance` (standard API - should exist)

**How to Check Exports:**

```powershell
# List all exports from ddsc.dll
dumpbin /EXPORTS D:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\ddsc.dll | Select-String "dds_lookup_instance|dds_take_instance|dds_read_instance"
```

If missing, you'll need to expose them (they exist internally but may not be exported).

---

#### Projects to Build

Build order (dependencies):

```powershell
# 1. Runtime (DDS API)
dotnet build D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\CycloneDDS.Runtime.csproj

# 2. Tests
dotnet build D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj

# 3. Run all tests
dotnet test D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj --no-build
```

**Expected Test Count:**
- After BATCH-19: 57 passing tests
- After BATCH-22: 66+ passing tests (57 + 9 new tests)

---

### Report Submission

**When done, submit your report to:**  
`D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reports\BATCH-22-REPORT.md`

**If you have questions, create:**  
`D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\questions\BATCH-22-QUESTIONS.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1 (Keyed Test Type):** Create → Generate code → **Compiles** ✅
2. **Task 2 (P/Invoke + Handle Struct):** Implement → **Compiles** ✅  
3. **Task 3 (Reader APIs):** Implement → Write tests → **ALL tests pass** ✅
4. **Task 4 (Re-Enable Tests):** Fix tests → **ALL tests pass** ✅

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task compiles with no errors/warnings
- ✅ **ALL tests passing** (including all 57 tests from BATCH-19)

**After EACH task completion:**
```powershell
# Verify ALL tests pass (not just new ones)
dotnet test D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj --no-build
# Expected progression:
# After Task 1-2: 57 passing (BATCH-19 baseline)
# After Task 3: 63+ passing (57 + 6 new instance management tests)
# After Task 4: 66+ passing (63 + 3 re-enabled lifecycle tests)
```

---

## Context

This batch completes the **Stage 3.75 Extended DDS API** work. Instance management is the final core DDS feature needed for production systems dealing with keyed data.

**Strategic Position:**
- BATCH-14 implemented `DisposeInstance()` and `UnregisterInstance()` but tests were skipped (no keyed topic)
- BATCH-21 validated XCDR2 keyed topic serialization (key extraction working)
- **This batch:** Connects the dots - implements instance lookup/filtering and re-enables lifecycle tests

**Related Tasks:**
- [FCDC-EXT05](D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md#fcdc-ext05-instance-management-keyed-topics) - Instance Management (lines 1858-1959)
- [FCDC-S022b](D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md#fcdc-s022b-instance-lifecycle-management-disposeunregister) - Instance Lifecycle (Dispose/Unregister from BATCH-14)

**Design Context:**
All implementation patterns, P/Invoke signatures, and usage examples are in:
- `EXTENDED-DDS-API-DESIGN.md` Section 8
- `instance-management-xcdr2-design-talk.md` (advisor recommendations)

---

## 🎯 Batch Objectives

**Goal:** Enable production-ready instance management for keyed topics with zero-allocation O(1) lookups.

**Why It Matters:**
- **Fleet Management:** "Give me the state of vehicle VIN=ABC123" (not all vehicles)
- **Air Traffic Control:** "Get latest position for flight AA1234" (not all flights)
- **Sensor Networks:** "Read temperature from sensor ID=5" (not all sensors)
- **Performance:** O(1) lookup vs O(N) iteration through all instances

**Success Metrics:**
- ✅ 9 new tests passing (3 lookup + 3 lifecycle + 3 multi-instance)
- ✅ 3 skipped tests from BATCH-18 re-enabled and passing
- ✅ Zero allocation for instance operations (same as Write/Take)
- ✅ Instance handles persist across updates
- ✅ All 66+ tests passing (no regressions)

---

## ✅ Tasks

### Task 1: Create Keyed Test Message Type

**Priority:** CRITICAL (Foundation)  
**Estimated Effort:** 30 minutes  
**File:** NEW

#### Overview

Create a keyed message type for testing. This type will have a `[DdsKey]` field, enabling instance-based operations.

#### Files to Create

**File:** `tests\CycloneDDS.Runtime.Tests\KeyedTestMessage.cs`

```csharp
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests;

/// <summary>
/// Keyed test message for instance management tests.
/// Simulates a sensor reading where SensorId is the key.
/// </summary>
[DdsTopic("KeyedTestTopic")]
public partial struct KeyedTestMessage
{
    /// <summary>
    /// Key field - identifies the instance (sensor).
    /// </summary>
    [DdsKey, DdsId(0)]
    public int SensorId;
    
    /// <summary>
    /// Data field - sensor reading value.
    /// </summary>
    [DdsId(1)]
    public int Value;
}
```

#### Code Generation

This type is `partial`, so code generation will run automatically during build:

```powershell
# Build test project to trigger code generation
dotnet build D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj
```

**Verify generated files exist:**
- `tests\CycloneDDS.Runtime.Tests\obj\Debug\net8.0\generated\KeyedTestMessage.g.cs`
- Generated code should include `_SensorId_IsKey = true` marking in descriptor

#### Validation Checklist

- ✅ `KeyedTestMessage.cs` created in test project
- ✅ Build succeeds (no compilation errors)
- ✅ Generated code includes `_SensorId_IsKey = true` (check `.g.cs` file)
- ✅ Descriptor ops array marks field 0 as key (inspect generated `GetDescriptorOps()`)

---

### Task 2: P/Invoke Layer & Instance Handle Struct

**Priority:** CRITICAL  
**Estimated Effort:** 1-2 hours  
**Files:** 2 files (1 new, 1 modified)

#### Overview

Add P/Invoke declarations for instance operations and create a `DdsInstanceHandle` struct to wrap the native `dds_instance_handle_t` (which is a `long`).

#### File 1: DdsInstanceHandle.cs (NEW)

**File:** `Src\CycloneDDS.Runtime\DdsInstanceHandle.cs`

**Design doc reference:** `EXTENDED-DDS-API-DESIGN.md` Section 8.2 - DdsInstanceHandle struct

```csharp
using System;

namespace CycloneDDS.Runtime;

/// <summary>
/// Represents a DDS instance handle for keyed topics.
/// An instance handle uniquely identifies an instance of a keyed data type.
/// </summary>
public readonly struct DdsInstanceHandle : IEquatable<DdsInstanceHandle>
{
    private readonly long _handle;
    
    /// <summary>
    /// Nil (invalid) instance handle constant.
    /// </summary>
    public static readonly DdsInstanceHandle Nil = new DdsInstanceHandle(0);
    
    /// <summary>
    /// Creates an instance handle from a native handle value.
    /// </summary>
    internal DdsInstanceHandle(long handle)
    {
        _handle = handle;
    }
    
    /// <summary>
    /// Gets whether this handle is valid (not Nil).
    /// </summary>
    public bool IsValid => _handle != 0;
    
    /// <summary>
    /// Gets the native handle value.
    /// </summary>
    internal long Value => _handle;
    
    public bool Equals(DdsInstanceHandle other) => _handle == other._handle;
    
    public override bool Equals(object? obj) => obj is DdsInstanceHandle handle && Equals(handle);
    
    public override int GetHashCode() => _handle.GetHashCode();
    
    public static bool operator ==(DdsInstanceHandle left, DdsInstanceHandle right) => left.Equals(right);
    
    public static bool operator !=(DdsInstanceHandle left, DdsInstanceHandle right) => !left.Equals(right);
    
    public override string ToString() => _handle == 0 ? "Nil" : $"0x{_handle:X16}";
}
```

#### File 2: DdsApi.cs (MODIFY)

**File:** `Src\CycloneDDS.Runtime\Interop\DdsApi.cs`

**Design doc reference:** `EXTENDED-DDS-API-DESIGN.md` Section 8.3 - P/Invoke declarations

**Add these P/Invoke declarations (ADVISOR'S SERDATA-BASED APPROACH):**

```csharp
// Instance Management APIs - Serdata-Based Zero-Allocation Approach
// Based on advisor's design-talk.md recommendations

/// <summary>
/// Lookup instance handle using pre-serialized key (serdata with SDK_KEY).
/// This is a custom extension - you may need to add it to cyclonedds native code.
/// See instance-management-xcdr2-design-talk.md lines 22-91 for native implementation.
/// </summary>
[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
public static extern long dds_lookup_instance_serdata(DdsEntity writer_or_reader, IntPtr serdata);

/// <summary>
/// Read CDR samples for a specific instance (non-destructive).
/// This is a custom extension - you may need to add it to cyclonedds native code.
/// See instance-management-xcdr2-design-talk.md lines 116-133 for native implementation.
/// </summary>
[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
public static extern int dds_readcdr_instance(
    DdsEntity reader,
    long instance_handle,
    [In, Out] IntPtr[] samples,
    uint max_samples,
    [In, Out] DdsSampleInfo[] infos,
    uint mask);

/// <summary>
/// Take CDR samples for a specific instance (destructive).
/// This is a custom extension - you may need to add it to cyclonedds native code.
/// See instance-management-xcdr2-design-talk.md lines 96-114 for native implementation.
/// </summary>
[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
public static extern int dds_takecdr_instance(
    DdsEntity reader,
    long instance_handle,
    [In, Out] IntPtr[] samples,
    uint max_samples,
    [In, Out] DdsSampleInfo[] infos,
    uint mask);
```

**🔧 NATIVE CODE EXTENSIONS REQUIRED:**

These APIs are **custom extensions** (not standard DDS). You will likely need to add them to the native cyclonedds code:

**File to modify:** `cyclonedds\src\core\ddsc\src\dds_topic.c` (or `dds_reader.c`)

**Reference implementation:** See `instance-management-xcdr2-design-talk.md`:
- Lines 22-91: `dds_lookup_instance_serdata()` native C implementation
- Lines 96-114: `dds_takecdr_instance()` native C implementation  
- Lines 116-133: `dds_readcdr_instance()` native C implementation

**Alternative if you don't want to modify native code:**

If you prefer NOT to modify cyclonedds, you can use the standard APIs instead:
```csharp
// Standard DDS APIs (already in ddsc.dll)
[DllImport(DLL_NAME)]
public static extern long dds_lookup_instance(DdsEntity entity, IntPtr sample);

[DllImport(DLL_NAME)]
public static extern int dds_read_instance(...); // Standard API

[DllImport(DLL_NAME)]
public static extern int dds_take_instance(...); // Standard API
```

⚠️ **However:** Standard APIs require unmanaged memory allocation (not zero-allocation). Advisor's approach is superior.

**Recommendation:** Implement the native extensions. They're small additions (~50 lines C code total).

#### Validation Checklist

- ✅ `DdsInstanceHandle.cs` created with all required members
- ✅ `DdsApi.cs` updated with instance P/Invoke declarations
- ✅ Code compiles with no errors/warnings
- ✅ Native APIs verified to exist in ddsc.dll (or alternative approach documented)

---

### Task 3: Implement Instance Management APIs on DdsReader

**Priority:** CRITICAL  
**Estimated Effort:** 3-4 hours  
**File:** Modify existing

#### Overview

Add instance lookup, read, and take methods to `DdsReader`. These methods enable O(1) access to specific instances.

#### File: DdsReader.cs (MODIFY)

**File:** `Src\CycloneDDS.Runtime\DdsReader.cs`

**Design doc reference:** `EXTENDED-DDS-API-DESIGN.md` Section 8.4-8.5 - DdsReader methods

#### Implementation Requirements

**Add these public methods:**

```csharp
/// <summary>
/// Lookup the instance handle for a given key value.
/// Uses zero-allocation serdata-based approach per advisor's design-talk.md.
/// </summary>
/// <param name="keySample">Sample containing the key fields to lookup.
/// Non-key fields are ignored.</param>
/// <returns>Instance handle, or DdsInstanceHandle.Nil if instance not found.</returns>
public DdsInstanceHandle LookupInstance(in T keySample)
{
    if (_readerHandle == null) 
        throw new ObjectDisposedException(nameof(DdsReader<T, TView>));
    
    // IMPLEMENTATION: Follow advisor's serdata approach (instance-management-xcdr2-design-talk.md lines 169-232)
    
    // Step 1: Calculate key size using existing sizer
    int size = _sizer!(keySample, 4, isXcdr2: true);
    byte[] buffer = Arena.Rent(size + 4);
    
    try
    {
        var span = buffer.AsSpan(0, size + 4);
        var cdr = new CdrWriter(span, isXcdr2: true);
        
        // Step 2: Write XCDR2 header
        if (BitConverter.IsLittleEndian) 
        { 
            cdr.WriteByte(0x00); 
            cdr.WriteByte(0x09); // XCDR2 LE
        }
        else 
        { 
            cdr.WriteByte(0x00); 
            cdr.WriteByte(0x08); // XCDR2 BE
        }
        cdr.WriteByte(0x00); 
        cdr.WriteByte(0x00);
        
        // Step 3: Serialize the sample (only key fields will be used by serdata)
        _serializer!(keySample, ref cdr);
        
        unsafe
        {
            fixed (byte* p = buffer)
            {
                // Step 4: Create serdata with kind=SDK_KEY (1)
                // This tells Cyclone: "This buffer contains ONLY key fields"
                IntPtr serdata = DdsApi.dds_create_serdata_from_cdr(
                    _topicHandle, 
                    (IntPtr)p, 
                    (uint)(size + 4), 
                    kind: 1); // SDK_KEY = 1, SDK_DATA = 2
                
                if (serdata == IntPtr.Zero) 
                    return DdsInstanceHandle.Nil;
                
                try
                {
                    // Step 5: Lookup using serdata (custom native extension)
                    long handle = DdsApi.dds_lookup_instance_serdata(_readerHandle.NativeHandle, serdata);
                    return new DdsInstanceHandle(handle);
                }
                finally
                {
                    // Step 6: Release serdata reference (we own it from create_serdata)
                    DdsApi.ddsi_serdata_unref(serdata);
                }
            }
        }
    }
    finally
    {
        Arena.Return(buffer);
    }
}

/// <summary>
/// Take samples for a specific instance (destructive).
/// </summary>
/// <param name="handle">Instance handle from LookupInstance.</param>
/// <param name="maxSamples">Maximum number of samples to take.</param>
/// <returns>ViewScope containing samples for this instance only.</returns>
public ViewScope<TView> TakeInstance(DdsInstanceHandle handle, int maxSamples = 32)
{
    return ReadOrTakeInstance(handle, maxSamples, isTake: true);
}

/// <summary>
/// Read samples for a specific instance (non-destructive).
/// </summary>
/// <param name="handle">Instance handle from LookupInstance.</param>
/// <param name="maxSamples">Maximum number of samples to read.</param>
/// <returns>ViewScope containing samples for this instance only.</returns>
public ViewScope<TView> ReadInstance(DdsInstanceHandle handle, int maxSamples = 32)
{
    return ReadOrTakeInstance(handle, maxSamples, isTake: false);
}

/// <summary>
/// Internal helper for read/take instance operations.
/// </summary>
private ViewScope<TView> ReadOrTakeInstance(
    DdsInstanceHandle handle, 
    int maxSamples, 
    bool isTake)
{
    if (_readerHandle == null) 
        throw new ObjectDisposedException(nameof(DdsReader<T, TView>));
    
    if (!handle.IsValid)
        return new ViewScope<TView>(_readerHandle.NativeHandle, null, null, 0, null, _filter);
    
    var samples = ArrayPool<IntPtr>.Shared.Rent(maxSamples);
    var infos = ArrayPool<DdsApi.DdsSampleInfo>.Shared.Rent(maxSamples);
    
    Array.Clear(samples, 0, maxSamples);
    Array.Clear(infos, 0, maxSamples);
    
    int count;
    const uint ANY_STATE = 0xFFFFFFFF; // ANY_SAMPLE_STATE | ANY_VIEW_STATE | ANY_INSTANCE_STATE
    
    if (isTake)
    {
        // Use advisor's dds_takecdr_instance (returns serdata directly)
        count = DdsApi.dds_takecdr_instance(
            _readerHandle.NativeHandle,
            handle.Value,
            samples,
            (uint)maxSamples,
            infos,
            ANY_STATE);
    }
    else
    {
        // Use advisor's dds_readcdr_instance (returns serdata directly)
        count = DdsApi.dds_readcdr_instance(
            _readerHandle.NativeHandle,
            handle.Value,
            samples,
            (uint)maxSamples,
            infos,
            ANY_STATE);
    }
    
    if (count < 0)
    {
        ArrayPool<IntPtr>.Shared.Return(samples);
        ArrayPool<DdsApi.DdsSampleInfo>.Shared.Return(infos);
        
        // Handle errors
        if (count == (int)DdsApi.DdsReturnCode.BadParameter)
            throw new ArgumentException("Invalid instance handle", nameof(handle));
        
        return new ViewScope<TView>(_readerHandle.NativeHandle, null, null, 0, null, _filter);
    }
    
    return new ViewScope<TView>(
        _readerHandle.NativeHandle, 
        samples, 
        infos, 
        count, 
        _deserializer,
        _filter);
}
```

**⚠️ IMPLEMENTATION NOTES:**

1. **Follow Advisor's Approach EXACTLY** - See `instance-management-xcdr2-design-talk.md`:
   - Lines 169-232: Complete `LookupInstance` implementation with serdata
   - Lines 237-299: Complete `ReadOrTakeInstance` implementation
   - Zero-allocation guarantee maintained throughout
   
2. **Native Extensions Required** - You'll need to add 3 functions to cyclonedds:
   - `dds_lookup_instance_serdata()` - Takes serdata with SDK_KEY, returns handle
   - `dds_readcdr_instance()` - Returns serdata for specific instance
   - `dds_takecdr_instance()` - Returns serdata for specific instance (destructive)
   
3. **Mask Values:** `0xFFFFFFFF` = ANY state (accept all samples regardless of state)

4. **Error Handling:** Return empty ViewScope on error (same pattern as Read/Take)

5. **Key Serialization:** Serdata with `kind=1` (SDK_KEY) tells Cyclone to extract ONLY key fields for hash computation

#### Edge Cases to Handle

1. **Nil handle:** Return empty ViewScope (no exception)
2. **Unknown instance:** `dds_lookup_instance` returns 0 (Nil handle)
3. **No samples for instance:** `dds_read_instance` returns 0 (valid case)
4. **Disposed reader:** Throw `ObjectDisposedException`

#### Tests Required (Minimum 6)

**File:** `tests\CycloneDDS.Runtime.Tests\InstanceManagementTests.cs` (NEW)

**Test Group A: Instance Lookup & Filtering (3 tests)**

1. **`LookupInstance_ReturnsValidHandle`**
   - Write sample `{SensorId=5, Value=100}`
   - Lookup with key `{SensorId=5, Value=0}` (Value ignored - not a key)
   - Verify: Returns non-Nil handle
   - Verify: `handle.IsValid == true`

2. **`TakeInstance_OnlyReturnsMatchingData`**
   - Write `{SensorId=1, Value=100}` and `{SensorId=2, Value=200}`
   - Lookup handle for `SensorId=1`
   - Call `reader.TakeInstance(handle1, 10)`
   - Verify: Returns only samples with `SensorId=1`
   - Verify: Count == 1
   - Verify: After take, `SensorId=2` still in cache (call `Take()` to verify)

3. **`LookupInstance_UnknownKey_ReturnsNil`**
   - Never write `SensorId=999`
   - Lookup `{SensorId=999, Value=0}`
   - Verify: Returns `DdsInstanceHandle.Nil`
   - Verify: `handle.IsValid == false`
   - Verify: `handle == DdsInstanceHandle.Nil`

**Test Group B: Multi-Instance Operations (3 tests)**

4. **`ReadInstance_NonDestructive_DataRemains`**
   - Write `{SensorId=1, Value=100}` and `{SensorId=2, Value=200}`
   - Lookup handle for `SensorId=1`
   - Call `ReadInstance(handle1)` twice
   - Verify: Both reads return same data (non-destructive)
   - Verify: Subsequent `TakeInstance(handle1)` still returns data

5. **`MultipleInstances_IndependentLifecycles`**
   - Write 5 instances: `SensorId=1,2,3,4,5` with different values
   - Take instance 1 (should disappear from cache)
   - Dispose writer instance 3 (using `DisposeInstance` from BATCH-14)
   - Take instance 2
   - Verify: `Take()` returns instances 3,4,5 only
   - Verify: Instance 3 has `InstanceState == NotAliveDisposed`
   - Verify: Instances 4,5 have `InstanceState == Alive`

6. **`InstanceHandle_PersistsAcrossUpdates`**
   - Write `{SensorId=10, Value=100}`
   - Lookup handle H1
   - Write `{SensorId=10, Value=200}` (update same instance)
   - Lookup handle H2
   - Verify: H1 == H2 (same instance, handle persists)
   - Verify: `TakeInstance(H1)` returns the updated value (200)

#### Success Criteria

- ✅ All 6 new tests pass
- ✅ Lookup returns correct handles (non-Nil for existing, Nil for unknown)
- ✅ Instance filtering works (only matching data returned)
- ✅ ReadInstance is non-destructive (data remains after read)
- ✅ TakeInstance is destructive (data removed)
- ✅ Multiple instances tracked independently
- ✅ No regressions (all 57 BATCH-19 tests still pass)

---

### Task 4: Re-Enable Skipped Instance Lifecycle Tests

**Priority:** HIGH  
**Estimated Effort:** 1-2 hours  
**File:** Modify existing

#### Overview

BATCH-14 implemented `DisposeInstance()` and `UnregisterInstance()` but 3 tests were skipped because keyed topics weren't fully supported. Now that we have keyed message types and instance management, re-enable and fix these tests.

#### File: IntegrationTests.cs (MODIFY)

**File:** `tests\CycloneDDS.Runtime.Tests\IntegrationTests.cs`

**Find these skipped tests (lines 365-428):**
1. `DisposeInstance_ValidSample_MarksDisposed`
2. `UnregisterInstance_ValidSample_ReleasesOwnership`
3. `InstanceLifecycle_MultipleInstances_TrackedSeparately`

**Current state:** Tests are marked with `[Fact(Skip = "Keyed topic support not yet implemented")]`

#### Required Changes

**Step 1: Change Test Message Type**

Replace `TestMessage` with `KeyedTestMessage`:

```csharp
// OLD (BATCH-14 - skipped):
[Fact(Skip = "Keyed topic support not yet implemented")]
public void DisposeInstance_ValidSample_MarksDisposed()
{
    using var participant = new DdsParticipant(domainId: 0);
    using var writer = new DdsWriter<TestMessage>(participant, "TestTopic");  // ← WRONG
    using var reader = new DdsReader<TestMessage>(participant, "TestTopic");  // ← WRONG
    // ...
}

// NEW (BATCH-22 - enabled):
[Fact]  // ← Remove Skip attribute
public void DisposeInstance_ValidSample_MarksDisposed()
{
    using var participant = new DdsParticipant(domainId: 0);
    using var writer = new DdsWriter<KeyedTestMessage>(participant, "KeyedTestTopic");  // ← FIX
    using var reader = new DdsReader<KeyedTestMessage>(participant, "KeyedTestTopic");  // ← FIX
    
    // Write a sample
    writer.Write(new KeyedTestMessage { SensorId = 1, Value = 100 });
    Thread.Sleep(100); // Discovery
    
    // Dispose instance
    writer.DisposeInstance(new KeyedTestMessage { SensorId = 1, Value = 0 });  // Value ignored
    Thread.Sleep(50);
    
    // Read samples
    using var scope = reader.Take();
    Assert.Equal(2, scope.Samples.Count()); // Data sample + Dispose sample
    
    // Find dispose sample (has InstanceState != Alive)
    var disposeSample = scope.Infos
        .First(info => info.InstanceState == DdsInstanceState.NotAliveDisposed);
    
    Assert.NotNull(disposeSample);
}
```

**Step 2: Remove Skip Attribute**

For all 3 tests:
1. Remove `[Fact(Skip = "...")]`
2. Replace with `[Fact]`

**Step 3: Fix Sample Creation**

Use `KeyedTestMessage` with `SensorId` as key:
- Write: `{SensorId=1, Value=100}`
- Dispose/Unregister: `{SensorId=1, Value=0}` (Value is ignored for lifecycle ops)

#### Tests to Re-Enable

**Test 1: DisposeInstance_ValidSample_MarksDisposed**
- Create writer/reader with `KeyedTestMessage`
- Write sample: `{SensorId=1, Value=100}`
- Call `writer.DisposeInstance({SensorId=1, Value=0})`
- Reader takes samples
- **Verify:** At least one sample has `info.InstanceState == DdsInstanceState.NotAliveDisposed`

**Test 2: UnregisterInstance_ValidSample_ReleasesOwnership**
- Create writer/reader with `KeyedTestMessage`
- Write sample: `{SensorId=2, Value=200}`
- Call `writer.UnregisterInstance({SensorId=2, Value=0})`
- Reader takes samples
- **Verify:** At least one sample has `info.InstanceState == DdsInstanceState.NotAliveNoWriters`

**Test 3: InstanceLifecycle_MultipleInstances_TrackedSeparately**
- Write 3 instances: `SensorId=1,2,3` with values 100,200,300
- Dispose only `SensorId=2`
- Take all samples
- **Verify:** Only instance 2 marked as DISPOSED, others ALIVE or NO_WRITERS
- **Verify:** Each instance tracked independently

#### Validation Checklist

- ✅ All 3 tests have `[Fact(Skip = "...")]` removed
- ✅ All 3 tests use `KeyedTestMessage` instead of `TestMessage`
- ✅ All 3 tests pass (verify instance states correct)
- ✅ No other tests broken (all 63+ tests pass)

---

## 🧪 Testing Requirements

### Test Counts

**Minimum Total:** 9 new tests (6 instance management + 3 re-enabled lifecycle)  
**Target:** 10-12 tests (add bonus edge case tests)

**Expected Test Progression:**
- Before BATCH-22: 57 passing (BATCH-19 baseline)
- After Task 3: 63 passing (57 + 6 new)
- After Task 4: 66 passing (63 + 3 re-enabled)

### Test Categories

1. **Instance Lookup & Filtering (InstanceManagementTests.cs):**
   - Lookup returns valid handles
   - Unknown keys return Nil
   - Instance-specific take filters correctly

2. **Multi-Instance Operations (InstanceManagementTests.cs):**
   - ReadInstance non-destructive
   - Multiple instances independent
   - Handles persist across updates

3. **Instance Lifecycle (IntegrationTests.cs - re-enabled):**
   - DisposeInstance marks correct state
   - UnregisterInstance releases ownership
   - Multiple instances tracked separately

### Test Quality Standards

**⚠️ CRITICAL: ALL TESTS MUST VERIFY ACTUAL BEHAVIOR**

❌ **NOT ACCEPTABLE:**
```csharp
[Fact]
public void LookupInstance_Exists()
{
    var reader = new DdsReader<KeyedTestMessage>(...);
    Assert.NotNull(reader.LookupInstance); // Tests API exists, not behavior
}
```

✅ **REQUIRED:**
```csharp
[Fact]
public void LookupInstance_ReturnsValidHandle()
{
    using var participant = new DdsParticipant(domainId: 0);
    using var writer = new DdsWriter<KeyedTestMessage>(participant, "KeyedTestTopic");
    using var reader = new DdsReader<KeyedTestMessage>(participant, "KeyedTestTopic");
    
    // Write data
    writer.Write(new KeyedTestMessage { SensorId = 5, Value = 100 });
    Thread.Sleep(100); // Allow discovery
    
    // Lookup instance
    var handle = reader.LookupInstance(new KeyedTestMessage { SensorId = 5, Value = 0 });
    
    // Verify ACTUAL BEHAVIOR
    Assert.NotEqual(DdsInstanceHandle.Nil, handle); // Not Nil
    Assert.True(handle.IsValid); // IsValid property
    
    // Verify we can use this handle
    using var scope = reader.TakeInstance(handle, 10);
    Assert.Single(scope.Samples); // Exactly one sample for this instance
    Assert.Equal(5, scope.Samples.First().SensorId); // Correct instance data
}
```

**All tests must:**
- Write ACTUAL data to DDS
- Perform ACTUAL operations (Lookup, Read, Take, Dispose)
- Verify ACTUAL results (handle values, sample counts, instance states)
- Check edge cases (Nil handles, unknown instances, multiple instances)

### Verification Commands

After completing EACH task:

```powershell
# Build
dotnet build D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\CycloneDDS.Runtime.csproj

# Run ALL tests (verify no regressions)
dotnet test D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj --no-build -v normal

# Expected progression:
# After Task 1-2: 57 passing (BATCH-19 baseline)
# After Task 3: 63+ passing (57 + 6 new instance management)
# After Task 4: 66+ passing (63 + 3 re-enabled lifecycle)
```

---

## 📊 Report Requirements

### Report File

Submit to: `D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reports\BATCH-22-REPORT.md`

Use template: `D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\templates\BATCH-REPORT-TEMPLATE.md`

### Mandatory Sections

**1. Completion Checklist**
- [ ] Task 1: KeyedTestMessage created and generates code
- [ ] Task 2: P/Invoke + DdsInstanceHandle implemented
- [ ] Task 3: DdsReader instance APIs implemented (LookupInstance, ReadInstance, TakeInstance)
- [ ] Task 4: 3 skipped tests re-enabled and passing
- [ ] All new tests passing (9+ total: 6 new + 3 re-enabled)
- [ ] All existing tests still passing (66+ total)
- [ ] No compiler warnings

**2. Test Results**

```
Total tests: XX
Passed: XX (target: 66+)
Failed: 0
Skipped: XX (if any - justify why)

Test breakdown:
- BATCH-19 (existing): 57 passing
- InstanceManagementTests.cs: 6 passing
- IntegrationTests.cs (re-enabled): 3 passing
```

**3. Implementation Decisions**

Document your choices:

**Q1: LookupInstance Implementation Strategy**
Which approach did you use for `LookupInstance`?
- [ ] Strategy A: Standard `dds_lookup_instance` with unmanaged memory
- [ ] Strategy B: Serdata-based approach from design-talk.md

**Rationale:** [Why you chose this approach]

**Challenges:** [What problems did you encounter? How did you solve them?]

**Q2: Native API Availability**
Did `dds_lookup_instance`, `dds_read_instance`, `dds_take_instance` exist in ddsc.dll?
- [ ] Yes, all standard APIs available
- [ ] No, I had to implement custom versions

**If custom:** [What did you implement? Where is the code?]

**Q3: Native Code Modifications**
Did you add debug prints or modify native Cyclone DDS code?
- [ ] No modifications
- [ ] Added debug printf() statements only
- [ ] Modified implementation logic

**If modified:** [What files? What changes? Why?]

**4. Developer Insights (CRITICAL)**

Answer these questions:

**Q1: Instance Lookup Challenge**
What was the HARDEST part of implementing `LookupInstance`? How did you solve it?

**Q2: Serdata vs Standard API**
If you used the serdata approach, how did it compare to using standard `dds_lookup_instance`? Performance? Code complexity?

**Q3: Key Extraction**
How does Cyclone DDS extract the key from a keyed sample? Did you verify this in the native code? (Hint: See `cyclone-dds-xcdr2-serialization-internal.md` section "Key Hash Computation")

**Q4: Re-Enabled Tests**
What changes were needed to make the 3 skipped tests pass? Any surprises or edge cases discovered?

**Q5: Testing Approach**
How did you verify that instance filtering actually works (O(1) access)? What would happen if the implementation was broken?

**Q6: Code Quality**
What areas of the instance management code could be improved? Any technical debt introduced? Refactoring opportunities?

---

## 🎯 Success Criteria

This batch is DONE when:

- ✅ **Task 1 Complete:**
  - `KeyedTestMessage` created with `[DdsKey]` on `SensorId`
  - Code generation successful
  - Descriptor marks `SensorId` as key field

- ✅ **Task 2 Complete:**
  - `DdsInstanceHandle` struct implemented (Nil, IsValid, equality)
  - P/Invoke declarations added to `DdsApi.cs`
  - Code compiles, native APIs verified

- ✅ **Task 3 Complete:**
  - `LookupInstance`, `ReadInstance`, `TakeInstance` implemented on `DdsReader`
  - 6 new tests passing (lookup + multi-instance)
  - Instance filtering verified (O(1) access, only matching data returned)

- ✅ **Task 4 Complete:**
  - 3 skipped tests re-enabled and passing
  - DisposeInstance and UnregisterInstance verified with keyed topics
  - Instance states correct (DISPOSED, NO_WRITERS)

- ✅ **Quality Standards:**
  - All tests verify ACTUAL BEHAVIOR (not shallow)
  - No compiler warnings
  - Zero allocation verified (same as Write/Take)
  - All 66+ tests passing (no regressions)

- ✅ **Documentation:**
  - Report submitted with all mandatory sections
  - Implementation decisions documented and justified
  - Native code modifications (if any) clearly documented
  - Code comments for complex logic (serdata handling, key extraction)

---

## ⚠️ Common Pitfalls to Avoid

### LookupInstance Serialization

❌ **Don't:** Try to get unmanaged pointer to managed struct
```csharp
// WRONG - 'in T' parameter doesn't give you an unmanaged pointer
public DdsInstanceHandle LookupInstance(in T keySample)
{
    var ptr = /* how do I get this? */;  // ← Can't do this safely
    return DdsApi.dds_lookup_instance(_readerHandle, ptr);
}
```

✅ **Do:** Use serdata approach OR allocate temporary unmanaged memory
```csharp
// Strategy A: Allocate unmanaged memory (if you must use standard API)
var size = Marshal.SizeOf<T>();
var ptr = Marshal.AllocHGlobal(size);
try {
    Marshal.StructureToPtr(keySample, ptr, false);
    var handle = DdsApi.dds_lookup_instance(_readerHandle, ptr);
    return new DdsInstanceHandle(handle);
} finally {
    Marshal.FreeHGlobal(ptr);
}

// Strategy B: Serdata approach (RECOMMENDED - see design-talk.md)
// See instance-management-xcdr2-design-talk.md lines 169-232
```

### Instance Handle Validation

❌ **Don't:** Throw exception on Nil handle
```csharp
public ViewScope<TView> TakeInstance(DdsInstanceHandle handle, int maxSamples)
{
    if (!handle.IsValid)
        throw new ArgumentException("Invalid handle"); // ← WRONG
}
```

✅ **Do:** Return empty ViewScope (consistent with Take behavior)
```csharp
public ViewScope<TView> TakeInstance(DdsInstanceHandle handle, int maxSamples)
{
    if (!handle.IsValid)
        return new ViewScope<TView>(..., count: 0, ...); // Empty result
}
```

### Key Fields in Lifecycle Operations

❌ **Don't:** Use all fields for Dispose/Unregister
```csharp
// WRONG - DisposeInstance should only care about key fields
writer.DisposeInstance(new KeyedTestMessage { SensorId = 1, Value = 100 });
// ↑ Value=100 is ignored, but misleading to user
```

✅ **Do:** Set only key fields (document that non-key fields ignored)
```csharp
// CORRECT - Only key field matters for lifecycle operations
writer.DisposeInstance(new KeyedTestMessage { SensorId = 1, Value = 0 });
// ↑ Value=0 is a signal that "this field doesn't matter"

// EVEN BETTER - Add XML comment
/// <summary>
/// Disposes an instance. Only key fields are used; non-key fields are ignored.
/// </summary>
public void DisposeInstance(in T sample) { ... }
```

### Test Timing Issues

❌ **Don't:** Assume immediate discovery
```csharp
var writer = new DdsWriter<KeyedTestMessage>(...);
var reader = new DdsReader<KeyedTestMessage>(...);
writer.Write(new KeyedTestMessage { SensorId = 1, Value = 100 });
var handle = reader.LookupInstance(...); // ← May return Nil (not yet discovered)
```

✅ **Do:** Add discovery delay or use WaitForReaderAsync
```csharp
var writer = new DdsWriter<KeyedTestMessage>(...);
var reader = new DdsReader<KeyedTestMessage>(...);

// Option 1: Simple delay
Thread.Sleep(100); // Allow discovery

// Option 2: Explicit wait (from BATCH-19)
await writer.WaitForReaderAsync(TimeSpan.FromSeconds(2));

// Now safe to write/lookup
writer.Write(new KeyedTestMessage { SensorId = 1, Value = 100 });
Thread.Sleep(50); // Allow data arrival
var handle = reader.LookupInstance(...);
```

### Multiple Instances Confusion

❌ **Don't:** Assume instance handle == sample value
```csharp
// WRONG - Instance handle is NOT the key value
var handle = reader.LookupInstance(new KeyedTestMessage { SensorId = 5, Value = 0 });
Assert.Equal(5, handle.Value); // ← WRONG! Handle is a DDS entity ID, not the key
```

✅ **Do:** Treat handle as opaque identifier
```csharp
// CORRECT - Handle is opaque, use it for lookup only
var handle = reader.LookupInstance(new KeyedTestMessage { SensorId = 5, Value = 0 });
Assert.True(handle.IsValid); // ← Check validity, not value

// Use handle to retrieve actual samples
using var scope = reader.TakeInstance(handle);
Assert.Equal(5, scope.Samples.First().SensorId); // ← Verify key from actual data
```

---

## 📚 Reference Materials

### Essential Reading Order

1. **`EXTENDED-DDS-API-DESIGN.md` Section 8** - API specification
2. **`instance-management-xcdr2-design-talk.md`** - Implementation strategy (advisor recommendations)
3. **`cyclone-dds-xcdr2-serialization-internal.md`** - Key hash computation internals

### Native Code References

**If you need to inspect or debug native code:**

**Key Hash Computation:**
- File: `cyclonedds\src\core\ddsi\src\ddsi_serdata_default.c`
- Function: `get_keyhash()` - Shows how Cyclone computes key hashes
- Explanation: `cyclone-dds-xcdr2-serialization-internal.md` lines 33-42

**Instance Lookup:**
- File: `cyclonedds\src\core\ddsc\src\dds_topic.c`
- Function: `dds_lookup_instance()` - Standard API
- File: `cyclonedds\src\core\ddsc\src\dds_reader.c`
- Function: `dds_read_instance()` / `dds_take_instance()` - Instance filtering

**Serdata Creation:**
- File: `cyclonedds\src\core\ddsi\src\ddsi_serdata_default.c`
- Function: `ddsi_serdata_from_ser_iov()` - Creates serdata from CDR buffer
- Explanation: `cyclone-dds-xcdr2-serialization-internal.md` lines 5-16

### Debug Print Examples

**If you want to add debug prints to native code:**

```c
// File: cyclonedds/src/core/ddsc/src/dds_topic.c
// Function: dds_lookup_instance

dds_instance_handle_t dds_lookup_instance(dds_entity_t entity, const void *sample)
{
    printf("[DEBUG] dds_lookup_instance called, entity=%ld\n", (long)entity);
    
    // ... existing implementation ...
    
    dds_keyhash_t kh;
    ddsi_serdata_get_keyhash(serdata, &kh);
    
    printf("[DEBUG] Keyhash: %02x%02x%02x%02x...\n", 
           kh.value[0], kh.value[1], kh.value[2], kh.value[3]);
    
    // ... rest of implementation ...
}
```

**After adding debug prints, REBUILD ddsc.dll:**
```powershell
cd D:\Work\FastCycloneDdsCsharpBindings\cyclonedds\build
cmake --build . --config Debug
cmake --install . --config Debug
```

---

## ⭐ Bonus Challenges (Optional)

If you finish early and want to go above and beyond:

### Bonus 1: Performance Benchmark

Measure lookup performance:
```csharp
[Fact]
public void LookupInstance_Performance_UnderOneMicrosecond()
{
    // Setup: Write 100 instances
    // Benchmark: Lookup 1000 times
    // Verify: Average < 1μs per lookup
}
```

### Bonus 2: Handle Serialization

Implement `ToString()` that shows handle in hex:
```csharp
var handle = reader.LookupInstance(...);
Console.WriteLine(handle); // Output: "0x000000000001A4F3"
```

### Bonus 3: Batch Instance Operations

Add `LookupInstances()` that takes multiple keys:
```csharp
public DdsInstanceHandle[] LookupInstances(params T[] keysamples)
{
    // Return array of handles for each key
}
```

### Bonus 4: Instance Count Query

Add property to get total instance count:
```csharp
public int InstanceCount
{
    get
    {
        // Query native reader for total instances
        // Useful for monitoring/diagnostics
    }
}
```

---

## 🎓 Learning Objectives

By the end of this batch, you should understand:

✅ **DDS Instance Lifecycle:**
- How keyed topics differ from non-keyed topics
- What an instance handle represents (unique ID per key value)
- How instances transition through states (ALIVE → DISPOSED → NO_WRITERS)

✅ **Key Hash Computation:**
- How Cyclone DDS computes 16-byte key hashes
- When MD5 is used vs direct key bytes
- Why XCDR2 alignment affects key size (see `cyclone-dds-xcdr2-serialization-internal.md` lines 36-39)

✅ **Serdata Architecture:**
- Difference between `SDK_DATA` (full sample) and `SDK_KEY` (keys only)
- How `dds_create_serdata_from_cdr` with `kind=1` creates key-only serdata
- Why serdata approach is zero-allocation

✅ **P/Invoke Patterns:**
- How to declare DDS instance APIs
- Parameter marshalling for instance operations
- Error handling and return code interpretation

✅ **Zero-Allocation Patterns:**
- Why instance operations can be zero-allocation
- How ViewScope reuses existing infrastructure
- Performance characteristics of instance filtering (O(1) vs O(N))

---

**Good luck! Read the design documents carefully, and don't hesitate to inspect the native code if you get stuck. The answers are all in the documentation and source code.**

**Remember:** All tests must pass. Quality over speed. Document your decisions.
