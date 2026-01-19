# BATCH-18: Type Auto-Discovery + Read vs Take API

**Batch Number:** BATCH-18  
**Tasks:** FCDC-EXT00 (Type Auto-Discovery & Topic Management), FCDC-EXT01 (Read vs Take with Condition Masks)  
**Phase:** Stage 3.75 - Extended DDS API - Modern C# Idioms  
**Estimated Effort:** 4-6 days  
**Priority:** **CRITICAL** (Foundation for all other Stage 3.75 tasks)  
**Dependencies:** Stage 3 complete (BATCH-13 through BATCH-15.3), Stage 4 complete (BATCH-15)

---

## 📋 Onboarding & Workflow

### Developer Instructions

Welcome to **BATCH-18**, the first batch of **Stage 3.75: Extended DDS API**! This is a critical milestone that transforms the FastCycloneDDS bindings from a high-performance core into a production-ready, idiomatic C# API.

You will implement two foundational features:
1. **FCDC-EXT00:** Type Auto-Discovery – Eliminate manual descriptor passing, auto-register topics
2. **FCDC-EXT01:** Read vs Take – Non-destructive reads + state masks for precise data selection

These features are the **foundation** for all subsequent Extended API tasks (Async/Await, Content Filtering, Discovery, Instance Management).

### Required Reading (IN ORDER)

**READ THESE BEFORE STARTING:**

1. **Workflow Guide:** `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\README.md`  
   - Understand batch system, report requirements, testing standards

2. **Task Definitions:** `d:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md`  
   - Section: FCDC-EXT00 (lines 1632-1680)
   - Section: FCDC-EXT01 (lines 1682-1720)

3. **Design Document:** `d:\Work\FastCycloneDdsCsharpBindings\docs\EXTENDED-DDS-API-DESIGN.md` ← **CRITICAL**
   - Section 4: Type Auto-Discovery (lines 75-290)
   - Section 5: Read vs Take (lines 293-400)
   - Read entire sections – contains implementation patterns, P/Invoke details, examples

4. **Previous Batch Review:** `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reviews\BATCH-17-REVIEW.md`  
   - Learn from Stage 2 completion feedback

### Repository Structure

```
d:\Work\FastCycloneDdsCsharpBindings\
├── Src\
│   ├── CycloneDDS.Core\              # CDR serialization (no changes)
│   ├── CycloneDDS.Schema\            # Attributes (no changes)
│   └── CycloneDDS.Runtime\           # ← YOU WORK HERE
│       ├── DdsWriter.cs              # ← MODIFY (remove descriptor param)
│       ├── DdsReader.cs              # ← MODIFY (add Read, masks)
│       ├── DdsParticipant.cs         # ← MODIFY (add topic cache)
│       ├── DdsTypeSupport.cs         # ← NEW FILE (reflection helpers)
│       ├── DdsStateEnums.cs          # ← NEW FILE (state enums)
│       ├── ViewScope.cs              # ← MODIFY (state info access)
│       └── Interop\
│           └── DdsApi.cs             # ← MODIFY (add dds_readcdr)
│
├── tests\
│   └── CycloneDDS.Runtime.Tests\     # Runtime tests
│       ├── AutoDiscoveryTests.cs     # ← NEW FILE (4 tests)
│       └── ReadTakeTests.cs          # ← NEW FILE (3 tests)
│
├── cyclone-compiled\                 # Cyclone DDS native binaries
│   └── bin\
│       └── ddsc.dll                  # DDS native library (custom build with serdata)
│
└── .dev-workstream\
    ├── batches\
    │   └── BATCH-18-INSTRUCTIONS.md  # ← This file
    └── reports\
        └── BATCH-18-REPORT.md        # ← Submit your report here
```

### Critical Tool & Library Locations

**DDS Native Library:**
- **Location:** `d:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\ddsc.dll`
- **Usage:** Runtime tests link against this (custom build with serdata exports from BATCH-13.2)
- **Do NOT modify:** Already configured

**Projects to Build:**

Build order (dependencies):
```powershell
# 1. Core (CDR serialization)
dotnet build d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Core\CycloneDDS.Core.csproj

# 2. Runtime (DDS API)
dotnet build d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\CycloneDDS.Runtime.csproj

# 3. Tests
dotnet build d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj

# 4. Run all tests
dotnet test d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj
```

### Report Submission

**When done, submit your report to:**  
`d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reports\BATCH-18-REPORT.md`

**Use template:**  
`d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\templates\BATCH-REPORT-TEMPLATE.md`

**If you have questions before starting, create:**  
`d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\questions\BATCH-18-QUESTIONS.md`

---

## Context

### Why This Batch Matters

**User Pain Point (Current State):**
```csharp
// TODAY: Manual boilerplate
var descriptorOps = TestMessage.GetDescriptorOps();
var descriptorPtr = /* ... marshal to native ... */;
var writer = new DdsWriter<TestMessage>(participant, "Test", descriptorPtr);
```

**Desired State (After This Batch):**
```csharp
// AFTER: Auto-magic type discovery
var writer = new DdsWriter<TestMessage>(participant, "Test");
// ↑ Type info automatically discovered via reflection!
```

**Production Requirement:**

1. **Type Auto-Discovery** (FCDC-EXT00) enables:
   - Idiomatic C# API (no manual descriptor passing)
   - Type safety enforced via generics
   - Automatic QoS from attributes (`[DdsQos]`)
   - Topic lifecycle managed by participant

2. **Read vs Take** (FCDC-EXT01) enables:
   - Non-destructive reads (monitoring/inspection patterns)
   - Precise state filtering ("only unread samples", "only alive instances")
   - Foundation for async/await and advanced patterns

**Related Tasks:**
- [FCDC-EXT00](../docs/SERDATA-TASK-MASTER.md#fcdc-ext00-type-auto-discovery--topic-management) - Type Auto-Discovery
- [FCDC-EXT01](../docs/SERDATA-TASK-MASTER.md#fcdc-ext01-read-vs-take-with-condition-masks) - Read vs Take

---

## 🎯 Batch Objectives

**You will accomplish:**

### FCDC-EXT00 Deliverables:
1. ✅ Create `DdsTypeSupport` static class with reflection-based descriptor extraction
2. ✅ Add `_topicCache` dictionary to `DdsParticipant`
3. ✅ Implement `GetOrRegisterTopic<T>(string, DdsQos?)` in `DdsParticipant`
4. ✅ Update `DdsWriter<T>` constructor to remove `descriptorPtr` parameter
5. ✅ Update `DdsReader<T, TView>` constructor to remove `descriptorPtr` parameter
6. ✅ Add topic cleanup to `DdsParticipant.Dispose()`
7. ✅ Write 4 tests (discovery validation, QoS, topic caching)

### FCDC-EXT01 Deliverables:
1. ✅ Define state enums (`DdsSampleState`, `DdsViewState`, `DdsInstanceState`)
2. ✅ Add `dds_readcdr` P/Invoke to `DdsApi.cs`
3. ✅ Refactor `DdsReader.Take()` to use unified `ReadOrTake()` helper
4. ✅ Add `Read()` methods with mask parameters
5. ✅ Expose sample state info in `ViewScope`
6. ✅ Write 3 tests (non-destructive, filtering, masks)

**Success:** Idiomatic C# API for type discovery + precise data access control.

---

## ✅ Tasks

### Task 1: Create DdsTypeSupport (FCDC-EXT00 Part 1)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsTypeSupport.cs` **(NEW FILE)**

**Task Definition:** [FCDC-EXT00](../docs/SERDATA-TASK-MASTER.md#fcdc-ext00-type-auto-discovery--topic-management)

**Description:**  
Create internal static class that uses reflection to extract type metadata from generated DDS types.

**Requirements:**

```csharp
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace CycloneDDS.Runtime
{
    /// <summary>
    /// Internal helper for extracting type metadata via reflection.
    /// Caches delegates to amortize reflection overhead.
    /// </summary>
    internal static class DdsTypeSupport
    {
        // Cache: Type -> GetDescriptorOps delegate
        private static readonly ConcurrentDictionary<Type, Func<uint[]>> _opsCache = new();
        
        /// <summary>
        /// Get descriptor ops array for type T using reflection.
        /// Throws if T doesn't have GetDescriptorOps() method (not a DDS type).
        /// </summary>
        public static uint[] GetDescriptorOps<T>()
        {
            var func = _opsCache.GetOrAdd(typeof(T), type =>
            {
                // Look for: public static uint[] GetDescriptorOps()
                var method = type.GetMethod("GetDescriptorOps", 
                    BindingFlags.Static | BindingFlags.Public, 
                    null, 
                    Type.EmptyTypes, 
                    null);
                
                if (method == null || method.ReturnType != typeof(uint[]))
                {
                    throw new InvalidOperationException(
                        $"Type '{type.Name}' does not have a public static GetDescriptorOps() method. " +
                        "Did you forget to add [DdsTopic] or [DdsStruct] attribute?");
                }
                
                // Create delegate for zero-overhead invocation
                return (Func<uint[]>)Delegate.CreateDelegate(typeof(Func<uint[]>), method);
            });
            
            return func();
        }
        
        /// <summary>
        /// Get type name for DDS topic registration.
        /// </summary>
        public static string GetTypeName<T>()
        {
            return typeof(T).Name;
        }
    }
}
```

**Design Reference:** EXTENDED-DDS-API-DESIGN.md Section 4.3 (Implementation Strategy, lines 134-168)

**Tests Required:**
- ✅ `GetDescriptorOps_ValidType_ReturnsOps`: Call with `TestMessage` → returns uint[]
- ✅ `GetDescriptorOps_InvalidType_Throws`: Call with `int` → throws `InvalidOperationException`
- ✅ `GetDescriptorOps_CacheWorks`: Call twice → second call uses cached delegate (verify via reflection hook or timing)

---

### Task 2: Add Topic Cache to DdsParticipant (FCDC-EXT00 Part 2)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsParticipant.cs` **(MODIFY)**

**Task Definition:** [FCDC-EXT00](../docs/SERDATA-TASK-MASTER.md#fcdc-ext00-type-auto-discovery--topic-management)

**Description:**  
Add internal topic management to `DdsParticipant` to ensure each topic name is registered only once.

**Requirements:**

1. **Add Fields:**
   ```csharp
   // Inside DdsParticipant class
   private readonly Dictionary<string, DdsApi.DdsEntity> _topicCache = new();
   private readonly object _topicLock = new();
   ```

2. **Add Method:**
   ```csharp
   /// <summary>
   /// Get or register a topic for type T.
   /// Thread-safe. Returns cached topic if already created for this name.
   /// </summary>
   internal DdsApi.DdsEntity GetOrRegisterTopic<T>(string topicName, IntPtr qos = default)
   {
       lock (_topicLock)
       {
           // Check cache first
           if (_topicCache.TryGetValue(topicName, out var existing))
           {
               return existing;
           }
           
           // 1. Get descriptor ops from static method (via reflection)
           uint[] ops = DdsTypeSupport.GetDescriptorOps<T>();
           
           // 2. Marshal descriptor to native
           IntPtr descriptorPtr = MarshalDescriptor(ops, DdsTypeSupport.GetTypeName<T>());
           
           // 3. Create native topic
           DdsApi.DdsEntity topic = DdsApi.dds_create_topic(
               _nativeHandle,
               descriptorPtr,
               topicName,
               qos,
               IntPtr.Zero);
           
           if (!topic.IsValid)
           {
               throw new DdsException(DdsApi.DdsReturnCode.Error, 
                   $"Failed to create topic '{topicName}' for type '{DdsTypeSupport.GetTypeName<T>()}'");
           }
           
           // 4. Cache and return
           _topicCache[topicName] = topic;
           return topic;
       }
   }
   
   /// <summary>
   /// Marshal descriptor ops array to native dds_topic_descriptor_t struct.
   /// </summary>
   private IntPtr MarshalDescriptor(uint[] ops, string typeName)
   {
       // IMPLEMENTATION NOTE: Copy logic from existing code in DdsWriter/DdsReader
       // that marshals descriptors. Logic should be:
       // 1. Allocate native struct
       // 2. Pin ops array
       // 3. Set struct fields (m_ops, m_nops, m_typename, etc.)
       // 4. Return IntPtr to struct
       //
       // CRITICAL: This native memory must be freed in Dispose()!
       // Consider tracking allocated descriptors in a list for cleanup.
       
       throw new NotImplementedException("Copy descriptor marshalling from existing code");
   }
   ```

3. **Update Dispose():**
   ```csharp
   public void Dispose()
   {
       lock (_topicLock)
       {
           // Delete all cached topics
           foreach (var topic in _topicCache.Values)
           {
               DdsApi.dds_delete(topic);
           }
           _topicCache.Clear();
       }
       
       // ... existing disposal logic ...
   }
   ```

**Design Reference:** EXTENDED-DDS-API-DESIGN.md Section 4.3 (Topic Caching, lines 169-207)

**Tests Required:**
- ✅ `TopicCache_SameName_ReturnsSameHandle`: Create two writers for "TopicA" → both use same topic handle
- ✅ `TopicCache_DifferentNames_CreatesSeparateTopics`: Create writers for "TopicA" and "TopicB" → different handles

---

### Task 3: Update DdsWriter Constructor (FCDC-EXT00 Part 3)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsWriter.cs` **(MODIFY)**

**Task Definition:** [FCDC-EXT00](../docs/SERDATA-TASK-MASTER.md#fcdc-ext00-type-auto-discovery--topic-management)

**Description:**  
Remove `topicDescriptor` parameter from constructor. Use participant's `GetOrRegisterTopic<T>()` instead.

**Requirements:**

**BEFORE (Current Code):**
```csharp
public DdsWriter(DdsParticipant participant, string topicName, IntPtr topicDescriptor)
{
    // ... creates topic manually ...
}
```

**AFTER (New Code):**
```csharp
public DdsWriter(DdsParticipant participant, string topicName, IntPtr qos = default)
{
    if (_sizer == null || _serializer == null)
    {
        throw new InvalidOperationException($"Type {typeof(T).Name} does not have expected DDS generated methods.");
    }

    _topicName = topicName;
    _participant = participant;
    
    // 1. Get or register topic (auto-discovery)
    DdsApi.DdsEntity topic = participant.GetOrRegisterTopic<T>(topicName, qos);
    _topicHandle = new DdsEntityHandle(topic);

    // 2. Create Writer
    DdsApi.DdsEntity writer = DdsApi.dds_create_writer(
        participant.NativeEntity,
        topic,
        qos,
        IntPtr.Zero);

    if (!writer.IsValid)
    {
        throw new DdsException(DdsApi.DdsReturnCode.Error, "Failed to create writer");
    }
    _writerHandle = new DdsEntityHandle(writer);
}
```

**CRITICAL:** Remove the old constructor entirely or mark `[Obsolete]` with error level.

**Design Reference:** EXTENDED-DDS-API-DESIGN.md Section 4.2 (API Design, lines 100-130)

**Tests Required:**
- ✅ `AutoDiscovery_ValidType_Succeeds`: Create `DdsWriter<TestMessage>` without descriptor → succeeds
- ✅ `AutoDiscovery_InvalidType_Throws`: Create `DdsWriter<int>` → throws with helpful message

---

### Task 4: Update DdsReader Constructor (FCDC-EXT00 Part 4)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsReader.cs` **(MODIFY)**

**Task Definition:** [FCDC-EXT00](../docs/SERDATA-TASK-MASTER.md#fcdc-ext00-type-auto-discovery--topic-management)

**Description:**  
Same as Task 3, but for `DdsReader`.

**Requirements:**

```csharp
public DdsReader(DdsParticipant participant, string topicName, IntPtr qos = default)
{
    if (_deserializer == null)
    {
        throw new InvalidOperationException($"Type {typeof(T).Name} does not have expected DDS generated methods.");
    }
    
    _topicName = topicName;
    
    // 1. Get or register topic (auto-discovery)
    DdsApi.DdsEntity topic = participant.GetOrRegisterTopic<T>(topicName, qos);
    
    // 2. Create Reader
    int reader = DdsApi.dds_create_reader(
        participant.NativeEntity.Handle,
        topic.Handle,
        qos,
        IntPtr.Zero);

    if (reader < 0)
    {
        throw new DdsException((DdsApi.DdsReturnCode)reader, $"Failed to create reader for '{topicName}'");
    }
    _readerHandle = new DdsEntityHandle(new DdsApi.DdsEntity(reader));
}
```

---

### Task 5: Define State Enums (FCDC-EXT01 Part 1)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsStateEnums.cs` **(NEW FILE)**

**Task Definition:** [FCDC-EXT01](../docs/SERDATA-TASK-MASTER.md#fcdc-ext01-read-vs-take-with-condition-masks)

**Description:**  
Define DDS state enums matching Cyclone DDS constants.

**Requirements:**

```csharp
using System;

namespace CycloneDDS.Runtime
{
    /// <summary>
    /// Sample state: has this sample been read before?
    /// </summary>
    [Flags]
    public enum DdsSampleState : uint
    {
        /// <summary>Sample has been read previously</summary>
        Read = 0x0001,
        
        /// <summary>Sample has not been read yet</summary>
        NotRead = 0x0002,
        
        /// <summary>Any sample state</summary>
        Any = Read | NotRead
    }

    /// <summary>
    /// View state: is this instance new to this reader?
    /// </summary>
    [Flags]
    public enum DdsViewState : uint
    {
        /// <summary>First sample for this instance</summary>
        New = 0x0004,
        
        /// <summary>Not the first sample for this instance</summary>
        NotNew = 0x0008,
        
        /// <summary>Any view state</summary>
        Any = New | NotNew
    }

    /// <summary>
    /// Instance state: is the instance still alive?
    /// </summary>
    [Flags]
    public enum DdsInstanceState : uint
    {
        /// <summary>Instance is alive (writer still exists)</summary>
        Alive = 0x0010,
        
        /// <summary>Instance was disposed (DisposeInstance called)</summary>
        NotAliveDisposed = 0x0020,
        
        /// <summary>Instance has no writers (all writers unregistered or gone)</summary>
        NotAliveNoWriters = 0x0040,
        
        /// <summary>Any not-alive state</summary>
        NotAlive = NotAliveDisposed | NotAliveNoWriters,
        
        /// <summary>Any instance state</summary>
        Any = Alive | NotAlive
    }
}
```

**Design Reference:** EXTENDED-DDS-API-DESIGN.md Section 5.2 (State Enums, lines 309-336)

**Tests Required:**
- ✅ `StateEnums_FlagsWork`: `DdsSampleState.Any == (Read | NotRead)` → true

---

### Task 6A: Verify Native Export for dds_readcdr (FCDC-EXT01 Part 2A) ⚠️ CRITICAL

**Location:** Native Cyclone DDS library check

**Description:**  
Verify that `dds_readcdr` is exported from the native `ddsc.dll`. This function should already be exported (declared with `DDS_EXPORT` in `dds.h` line 4437), but we need to confirm the current build includes it.

**Verification Steps:**

1. **Check if dds_readcdr exists in native DLL:**
   ```powershell
   # Using Windows dumpbin (Visual Studio required)
   cd "d:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin"
   dumpbin /EXPORTS ddsc.dll | Select-String "readcdr"
   ```

   **Expected output:** You should see entries like:
   ```
   dds_readcdr
   dds_readcdr_instance
   dds_peekcdr
   dds_peekcdr_instance
   ```

2. **If `dds_readcdr` is MISSING (unlikely but possible):**
   
   The function is already declared in the Cyclone DDS source (`cyclonedds\src\core\ddsc\include\dds\dds.h` line 4437 with `DDS_EXPORT`), so it should be in the DLL. However, if it's missing, you need to rebuild:

   **Rebuild Cyclone DDS:**
   ```powershell
   # Navigate to Cyclone build directory
   cd "d:\Work\FastCycloneDdsCsharpBindings\cyclonedds\build"
   
   # Rebuild using existing CMake configuration
   cmake --build . --config Release
   
   # Copy new binaries to cyclone-compiled folder
   Copy-Item ".\src\core\Release\ddsc.dll" `
       -Destination "d:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\ddsc.dll" `
       -Force
   
   Copy-Item ".\src\core\Release\ddsc.lib" `
       -Destination "d:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\lib\ddsc.lib" `
       -Force
   ```

3. **Verify after potential rebuild:**
   ```powershell
   dumpbin /EXPORTS d:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\ddsc.dll | Select-String "readcdr"
   ```

**Design Reference:** 
- Native function: `cyclonedds\src\core\ddsc\src\dds_read.c` line 423
- Export declaration: `cyclonedds\src\core\ddsc\include\dds\dds.h` line 4437

**Success Criteria:**
- ✅ `dds_readcdr` appears in DLL export list
- ✅ `dds_readcdr_instance` appears in DLL export list (bonus)
- ✅ No rebuild needed (function should already be there)

**Note:** Unlike `dds_writecdr`, `dds_dispose_serdata`, and `dds_unregister_serdata` (which you added in BATCH-13.2), `dds_readcdr` is part of the **standard Cyclone DDS API** and should already be exported. This task is verification only.

---

### Task 6B: Add dds_readcdr P/Invoke (FCDC-EXT01 Part 2B)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\Interop\DdsApi.cs` **(MODIFY)**

**Task Definition:** [FCDC-EXT01](../docs/SERDATA-TASK-MASTER.md#fcdc-ext01-read-vs-take-with-condition-masks)

**Description:**  
Add non-destructive read P/Invoke (now verified to exist in native DLL from Task 6A).

**Requirements:**

```csharp
// Inside DdsApi class
[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
public static extern int dds_readcdr(
    DdsEntity reader,
    [In, Out] IntPtr[] samples,
    [In, Out] DdsSampleInfo[] infos,
    uint max_samples,
    uint sample_state,
    uint view_state,
    uint instance_state);
```

**Design Reference:** EXTENDED-DDS-API-DESIGN.md Section 5.3 (P/Invoke, lines 371-380)

---

### Task 7: Implement Read/Take with Masks (FCDC-EXT01 Part 3)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsReader.cs` **(MODIFY)**

**Task Definition:** [FCDC-EXT01](../docs/SERDATA-TASK-MASTER.md#fcdc-ext01-read-vs-take-with-condition-masks)

**Description:**  
Refactor `Take()` and add `Read()` methods with state masks.

**Requirements:**

1. **Add Unified Method:**
   ```csharp
   private ViewScope<TView> ReadOrTake(
       int maxSamples,
       DdsSampleState sampleState,
       DdsViewState viewState,
       DdsInstanceState instanceState,
       bool destructive)
   {
       if (_readerHandle == null) throw new ObjectDisposedException(nameof(DdsReader<T, TView>));
       
       IntPtr[]? samples = null;
       DdsApi.DdsSampleInfo[]? infos = null;
       int count = 0;
       
       try
       {
           samples = new IntPtr[maxSamples];
           infos = new DdsApi.DdsSampleInfo[maxSamples];
           
           // Combine masks
           uint mask = (uint)sampleState | (uint)viewState | (uint)instanceState;
           
           // Call appropriate API
           if (destructive)
           {
               count = DdsApi.dds_takecdr(_readerHandle.NativeHandle, samples, infos, (uint)maxSamples, mask, 0, 0);
           }
           else
           {
               count = DdsApi.dds_readcdr(_readerHandle.NativeHandle, samples, infos, (uint)maxSamples, mask, 0, 0);
           }
           
           if (count < 0)
           {
               throw new DdsException((DdsApi.DdsReturnCode)count, "Read/Take failed");
           }
           
           return new ViewScope<TView>(_readerHandle.NativeHandle, samples, infos, count, _deserializer);
       }
       catch
       {
           // Cleanup on error
           if (samples != null && count > 0)
           {
               for (int i = 0; i < count; i++)
               {
                   if (samples[i] != IntPtr.Zero)
                   {
                       DdsApi.ddsi_serdata_unref(samples[i]);
                   }
               }
           }
           throw;
       }
   }
   ```

2. **Update Existing Take():**
   ```csharp
   public ViewScope<TView> Take(int maxSamples = 32)
   {
       return ReadOrTake(maxSamples, 
           DdsSampleState.Any, 
           DdsViewState.Any, 
           DdsInstanceState.Any, 
           destructive: true);
   }
   
   public ViewScope<TView> Take(
       int maxSamples,
       DdsSampleState sampleState,
       DdsViewState viewState = DdsViewState.Any,
       DdsInstanceState instanceState = DdsInstanceState.Any)
   {
       return ReadOrTake(maxSamples, sampleState, viewState, instanceState, destructive: true);
   }
   ```

3. **Add Read() Methods:**
   ```csharp
   /// <summary>
   /// Non-destructive read. Data remains in cache after ViewScope disposal.
   /// </summary>
   public ViewScope<TView> Read(
       int maxSamples = 32,
       DdsSampleState sampleState = DdsSampleState.Any,
       DdsViewState viewState = DdsViewState.Any,
       DdsInstanceState instanceState = DdsInstanceState.Any)
   {
       return ReadOrTake(maxSamples, sampleState, viewState, instanceState, destructive: false);
   }
   ```

**Design Reference:** EXTENDED-DDS-API-DESIGN.md Section 5.2-5.3 (API Design, lines 307-380)

**Tests Required:**
- ✅ `Read_IsNonDestructive_CallTwice_GetSameData`: Write sample, Read twice → identical results
- ✅ `Take_IsDestructive_CallTwice_SecondEmpty`: Write sample, Take, Take again → second returns 0
- ✅ `TakeWithMask_NotRead_FiltersCorrectly`: Write 3, Read 1, Take(NotRead) → only 2 returned

---

## 🧪 Testing Requirements

### Minimum Test Counts

**FCDC-EXT00 Tests:** 4 minimum
- 2 Auto-discovery tests (valid type, invalid type)
- 2 Topic cache tests (same name, different names)

**FCDC-EXT01 Tests:** 3 minimum
- 1 Non-destructive read test
- 1 Destructive take test
- 1 Mask filtering test

**Total:** 7 tests minimum

### Test Quality Standards

**❌ BAD TEST (Shallow):**
```csharp
// This is UNACCEPTABLE
var writer = new DdsWriter<TestMessage>(participant, "Test");
Assert.NotNull(writer); // Tests nothing meaningful
```

**✅ GOOD TEST (Actual Behavior):**
```csharp
// This is REQUIRED
var writer1 = new DdsWriter<TestMessage>(participant, "Test");
var writer2 = new DdsWriter<TestMessage>(participant, "Test");

// Verify same topic handle used (via internal cache access or indirect verification)
writer1.Write(new TestMessage { Id = 1 });
using var scope = reader.Take();
Assert.Equal(1, scope.Count); // Actual DDS communication verified
```

**Tests MUST verify:**
- ✅ Actual DDS communication (write → read)
- ✅ State changes (Read doesn't remove data, Take does)
- ✅ Error messages are helpful (invalid type → clear error)
- ✅ Thread safety (topic cache handles concurrent access)

---

## 📊 Report Requirements

### Focus: Developer Insights, Not Understanding Checks

**✅ ANSWER THESE:**

**Q1:** What issues did you encounter during implementation? How did you solve them?

**Q2:** Did you spot any weak points in the existing codebase? What would you improve?

**Q3:** What design decisions did you make beyond the instructions? What alternatives did you consider?

**Q4:** What edge cases did you discover that weren't mentioned in the spec?

**Q5:** Are there any performance concerns or optimization opportunities you noticed?

**Q6:** How did you handle descriptor marshalling? Did you reuse existing code or refactor it?

**Q7:** Did reflection overhead in `DdsTypeSupport` cause any measurable performance impact?

**Q8:** How did you verify thread safety of the topic cache?

### Report Must Include

1. **Completion Status:** Which tasks completed, test counts
2. **Code Changes:** Files modified/created
3. **Test Results:** Pass/fail counts, any skipped tests
4. **Issues Encountered:** Problems and solutions
5. **Design Decisions:** Choices you made beyond spec
6. **Edge Cases:** Scenarios discovered during testing

---

## 🎯 Success Criteria

This batch is DONE when:

### FCDC-EXT00 Complete:
- [x] `DdsTypeSupport` class created with reflection helpers
- [x] `DdsParticipant` has topic cache (`_topicCache`, `GetOrRegisterTopic<T>()`)
- [x] `DdsWriter<T>` constructor no longer requires descriptor parameter
- [x] `DdsReader<T, TView>` constructor no longer requires descriptor parameter
- [x] Topic cleanup in `DdsParticipant.Dispose()`
- [x] 4 tests passing (auto-discovery, caching)

### FCDC-EXT01 Complete:
- [x] State enums defined (`DdsSampleState`, `DdsViewState`, `DdsInstanceState`)
- [x] `dds_readcdr` P/Invoke added
- [x] `Read()` methods added to `DdsReader`
- [x] `Take()` refactored to use unified `ReadOrTake()` helper
- [x] 3 tests passing (non-destructive, filtering)

### Overall:
- [x] All 7+ tests passing
- [x] No breaking changes to existing Stage 3 tests (35 existing tests still pass)
- [x] Zero regression in performance (allocations, throughput)
- [x] API is idiomatic C# (no manual descriptor passing)

---

## ⚠️ Common Pitfalls to Avoid

1. **Descriptor Marshalling Memory Leak:**
   - `MarshalDescriptor()` allocates native memory
   - MUST be tracked and freed in `DdsParticipant.Dispose()`
   - Consider using `List<IntPtr>` to track allocated descriptors

2. **Topic Cache Thread Safety:**
   - Multiple threads may call `GetOrRegisterTopic<T>()` concurrently
   - Use `lock (_topicLock)` consistently
   - Avoid TOCTOU (Time-of-Check-Time-of-Use) races

3. **Reflection Performance:**
   - Reflection is expensive, but amortized by caching
   - Ensure `ConcurrentDictionary` is used correctly
   - `Delegate.CreateDelegate()` to get native performance after first call

4. **State Mask Bitwise OR:**
   - Masks MUST be combined with `|` operator: `(uint)sampleState | (uint)viewState | (uint)instanceState`
   - Wrong: `sampleState + viewState` (arithmetic, not bitwise)

5. **Read/Take Parameter Order:**
   - Cyclone API: `dds_readcdr(reader, samples, infos, max, sample_state, view_state, instance_state)`
   - Note: Last 3 parameters are separate `uint` values, NOT a single mask

6. **ViewScope Lifetime:**
   - Non-destructive Read: serdata stays in cache (DDS owns it)
   - Destructive Take: serdata removed from cache (we must unref)
   - Ensure `ViewScope.Dispose()` handles both cases correctly

---

## 📚 Reference Materials

- **Task Defs:** [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md)
  - FCDC-EXT00 (lines 1632-1680)
  - FCDC-EXT01 (lines 1682-1720)
  
- **Design:** [EXTENDED-DDS-API-DESIGN.md](../docs/EXTENDED-DDS-API-DESIGN.md)
  - Section 4: Type Auto-Discovery (lines 75-290)
  - Section 5: Read vs Take (lines 293-400)
  
- **Previous Review:** [BATCH-17-REVIEW.md](.dev-workstream/reviews/BATCH-17-REVIEW.md)
  - Stage 2 completion feedback
  
- **Cyclone DDS Documentation:**
  - Read vs Take semantics: https://cyclonedds.io/docs/cyclonedds/latest/api/c/group__reader.html
  - State masks: https://cyclonedds.io/docs/cyclonedds/latest/api/c/group__dds__sample__states.html

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **FCDC-EXT00:** Implement → Write tests → **ALL tests pass** ✅
2. **FCDC-EXT01:** Implement → Write tests → **ALL tests pass** ✅

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written
- ✅ **ALL tests passing** (including previous batch tests)

**Why:** Ensures each component is solid before building on top of it. Prevents cascading failures.

---

**Good luck! This is a critical batch that transforms the bindings into an idiomatic C# API. Focus on correctness first, then performance validation. The reflection overhead is acceptable as it's one-time cost amortized across all operations.**
