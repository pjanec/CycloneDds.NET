# BATCH-13: Stage 3 - Runtime Integration (Complete)

**Batch Number:** BATCH-13  
**Tasks:** FCDC-S017, FCDC-S018, FCDC-S019, FCDC-S020, FCDC-S021, FCDC-S022  
**Phase:** Stage 3 - Runtime Integration  
**Estimated Effort:** 18-24 days (3-4 weeks)  
**Priority:** CRITICAL - Blocking Stage 4  
**Dependencies:** Stage 2 Complete (BATCH-01 through BATCH-12.1)

---

## 📋 Onboarding & Workflow

### Developer Instructions

**Welcome to BATCH-13!** This batch implements the **entire Stage 3: Runtime Integration**, connecting the code generator (Stage 2) with Cyclone DDS via the new serdata-based APIs. You will build the DDS runtime wrappers (`DdsWriter<T>`, `DdsReader<T>`, `DdsParticipant`) that use the generated serializers from Stage 2.

**CRITICAL:** You are a **new developer** to this project. Follow these instructions **exactly** and in **order**. Every path is absolute. Every file is specified. You have **zero excuse** for guessing.

---

### Required Reading (IN ORDER - READ EVERYTHING)

**⚠️ MANDATORY: Read these documents BEFORE writing any code:**

1. **Workflow Guide:** `D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\README.md`  
   How to work with batches, submit reports, ask questions.

2. **Task Definitions:** `D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md`  
   Read **Stage 3** section (lines ~954-1200). This defines FCDC-S017 through FCDC-S022.

3. **Design Document:** `D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-DESIGN.md`  
   Read sections 3 (System Architecture), 8 (Memory Management), 11 (Integration with Cyclone DDS).

4. **Previous Review:** `D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reviews\BATCH-12.1-REVIEW.md`  
   See the quality standards expected. Stage 2 is now 100% complete and production-ready.

5. **Old Implementation Reference:** `D:\Work\FastCycloneDdsCsharpBindings\old_implem\src\CycloneDDS.Runtime\`  
   This is the **OLD** native-struct-based implementation. Use it **for inspiration only**. You are **NOT** copying it. You are building the **NEW** serdata-based version. Study these files:
   - `DdsParticipant.cs` - Participant wrapper pattern
   - `DdsWriter.cs` - Old writer (uses native structs; you'll use serdata instead)
   - `DdsReader.cs` - Old reader (uses native structs; you'll use view scopes instead)
   - `Interop\DdsApi.cs` - P/Invoke declarations (you'll extend this for serdata APIs)
   - `Interop\DdsEntityHandle.cs` - Entity lifetime management pattern
   - `Memory\Arena.cs` - Memory pooling pattern (you'll enhance this)

---

### Source Code Locations (ABSOLUTE PATHS)

**Repository Root:**  
`D:\Work\FastCycloneDdsCsharpBindings\`

**Your Work Area (NEW CODE - Stage 3):**
- **Runtime Package:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\`  
  You will CREATE this project from scratch.
  
- **Test Project:** `D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\`  
  You will CREATE this project from scratch.

**Dependencies (EXISTING CODE - Do NOT Modify):**
- **Core Package:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Core\`  
  Contains `CdrWriter`, `CdrReader`, `CdrSizer` - already complete.
  
- **Schema Package:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Schema\`  
  Contains attributes (`[DdsTopic]`, etc.) - already complete.
  
- **Code Generator:** `D:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\`  
  Generates serializers - already complete.

**Native Cyclone DDS Binaries:**
- **Location:** `D:\Work\FastCycloneDdsCsharpBindings\cyclone-bin\Release\`
- **Files:**
  - `ddsc.dll` (1,042,944 bytes) - **Core DDS library you P/Invoke**
  - `idlc.exe` (213,504 bytes) - IDL compiler (for descriptor generation)
  - `cycloneddsidl.dll` (143,872 bytes) - IDL support library

**Cyclone DDS C Sources (Reference Only):**
- **Location:** `D:\Work\FastCycloneDdsCsharpBindings\cyclonedds\`
- **Key Headers (for P/Invoke signature reference):**
  - `cyclonedds\src\core\ddsc\include\dds\dds.h` - Main DDS API
  - `cyclonedds\src\core\ddsc\include\dds\ddsc\dds_public_impl.h` - Entity handles
  - Look for serdata APIs (search for "serdata" in headers)

**Old Implementation (Reference Only - DO NOT COPY):**
- **Location:** `D:\Work\FastCycloneDdsCsharpBindings\old_implem\src\CycloneDDS.Runtime\`

---

### Report Submission

**When done, submit your report to:**  
`D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reports\BATCH-13-REPORT.md`

**Use this template:**  
`D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\templates\BATCH-REPORT-TEMPLATE.md`

**If you have questions, create:**  
`D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\questions\BATCH-13-QUESTIONS.md`

**Use this template:**  
`D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\templates\QUESTIONS-TEMPLATE.md`

---

## Context

**Stage 2 Status:** ✅ 100% COMPLETE  
- Code generator fully operational
- 162 passing tests
- Wire format verified byte-perfect with Cyclone DDS C

**Your Mission:**  
Integrate the generated serializers with Cyclone DDS using the **serdata** APIs. This enables:
1. **Zero-copy writes** - Serialize directly to pooled buffers, pass CDR to DDS
2. **Zero-copy reads** - DDS loans CDR buffers, deserialize to view structs
3. **Zero GC allocations** in steady state (ArrayPool for writes, loaned buffers for reads)

**Related Tasks:**
- [FCDC-S017](D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md#fcdc-s017-runtime-package-setup--pinvoke) - Runtime Package + P/Invoke
- [FCDC-S018](D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md#fcdc-s018-ddsparticipant-migration) - DdsParticipant Migration
- [FCDC-S019](D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md#fcdc-s019-arena-enhancement-for-cdr) - Arena Enhancement
- [FCDC-S020](D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md#fcdc-s020-ddswritert-serdata-based) - DdsWriter<T> (Serdata)
- [FCDC-S021](D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md#fcdc-s021-ddsreadert--viewscope) - DdsReader<T> + ViewScope
- [FCDC-S022](D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md#fcdc-s022-end-to-end-integration-tests-validation-gate) - Integration Tests (VALIDATION GATE)

---

## 🎯 Batch Objectives

This batch implements **ALL of Stage 3**, connecting the code generator to Cyclone DDS:

1. Create `CycloneDDS.Runtime` package with serdata P/Invoke definitions
2. Implement `DdsParticipant` wrapper
3. Enhance Arena for CDR buffer management
4. Implement `DdsWriter<T>` using `ArrayPool` and serdata APIs
5. Implement `DdsReader<T>` using loaned buffers and view scopes
6. **VALIDATION GATE:** End-to-end integration tests proving zero-alloc pub/sub

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1:** FCDC-S017 (Runtime Package + P/Invoke) → Write tests → **ALL tests pass** ✅
2. **Task 2:** FCDC-S018 (DdsParticipant) → Write tests → **ALL tests pass** ✅  
3. **Task 3:** FCDC-S019 (Arena Enhancement) → Write tests → **ALL tests pass** ✅
4. **Task 4:** FCDC-S020 (DdsWriter) → Write tests → **ALL tests pass** ✅
5. **Task 5:** FCDC-S021 (DdsReader) → Write tests → **ALL tests pass** ✅
6. **Task 6:** FCDC-S022 (Integration Tests) → Write tests → **ALL tests pass** ✅

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written
- ✅ **ALL tests passing** (including all previous tests from Stage 1 and Stage 2)

**Why:** Ensures each component is solid before building on top of it. Prevents cascading failures.

---

## ✅ Tasks

### Task 1: Runtime Package Setup + P/Invoke (FCDC-S017)

**File:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\CycloneDDS.Runtime.csproj` (NEW FILE)  
**Task Definition:** See [SERDATA-TASK-MASTER.md](D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md#fcdc-s017-runtime-package-setup--pinvoke)

**Description:**  
Create the Runtime package and define P/Invoke signatures for Cyclone DDS serdata APIs.

**Requirements:**

1. **Create Project:**
   ```
   Location: D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\
   Type: Class Library
   Target: net8.0
   ```

2. **Add Project References:**
   ```xml
   <ItemGroup>
     <ProjectReference Include="..\CycloneDDS.Core\CycloneDDS.Core.csproj" />
     <ProjectReference Include="..\CycloneDDS.Schema\CycloneDDS.Schema.csproj" />
   </ItemGroup>
   ```

3. **Create P/Invoke Declarations:**

   **File:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\Interop\DdsApi.cs` (NEW FILE)

   Study the old implementation at:  
   `D:\Work\FastCycloneDdsCsharpBindings\old_implem\src\CycloneDDS.Runtime\Interop\DdsApi.cs`

   **Extend it with serdata APIs:** (Find signatures in `D:\Work\FastCycloneDdsCsharpBindings\cyclonedds\src\core\ddsc\include\dds\dds.h`)

   ```csharp
   using System;
   using System.Runtime.InteropServices;
   
   namespace CycloneDDS.Runtime.Interop
   {
       public static class DdsApi
       {
           private const string DLL_NAME = "ddsc.dll";
           
           // Basic types
           public struct DdsEntity
           {
               public IntPtr Handle;
               public bool IsValid => Handle != IntPtr.Zero && Handle.ToInt64() > 0;
           }
           
           public enum DdsReturnCode : int
           {
               Ok = 0,
               Error = -1,
               // Add more from dds.h
           }
           
           // Participant
           [DllImport(DLL_NAME)]
           public static extern DdsEntity dds_create_participant(
               uint domain_id,
               IntPtr qos,
               IntPtr listener);
           
           // Topic
           [DllImport(DLL_NAME)]
           public static extern DdsEntity dds_create_topic(
               DdsEntity participant,
               IntPtr desc,
               [MarshalAs(UnmanagedType.LPStr)] string name,
               IntPtr qos,
               IntPtr listener);
           
           // Writer
           [DllImport(DLL_NAME)]
           public static extern DdsEntity dds_create_writer(
               DdsEntity participant_or_publisher,
               DdsEntity topic,
               IntPtr qos,
               IntPtr listener);
           
           // 🔥 NEW SERDATA APIS (CRITICAL - Find exact signatures in dds.h)
           [DllImport(DLL_NAME)]
           public static extern IntPtr dds_create_serdata_from_cdr(
               IntPtr topic_desc,
               IntPtr data,
               uint size);
           
           [DllImport(DLL_NAME)]
           public static extern int dds_write_serdata(
               DdsEntity writer,
               IntPtr serdata);
           
           [DllImport(DLL_NAME)]
           public static extern void dds_free_serdata(IntPtr serdata);
           
           // Reader
           [DllImport(DLL_NAME)]
           public static extern DdsEntity dds_create_reader(
               DdsEntity participant_or_subscriber,
               DdsEntity topic,
               IntPtr qos,
               IntPtr listener);
           
           // Take with loaned buffers (find exact signature)
           [DllImport(DLL_NAME)]
           public static extern int dds_take(
               DdsEntity reader,
               IntPtr[] samples,  // OUT: loaned pointers
               IntPtr[] infos,    // OUT: sample info
               uint max_samples,
               uint mask);
           
           // Return loaned buffers
           [DllImport(DLL_NAME)]
           public static extern int dds_return_loan(
               DdsEntity reader,
               IntPtr[] samples,
               int count);
           
           // Cleanup
           [DllImport(DLL_NAME)]
           public static extern int dds_delete(DdsEntity entity);
       }
   }
   ```

   **CRITICAL NOTES:**
   - The DLL path is relative: `ddsc.dll` will be found in the output directory
   - You MUST copy `D:\Work\FastCycloneDdsCsharpBindings\cyclone-bin\Release\ddsc.dll` to test output directory
   - Verify P/Invoke signatures against `cyclonedds\src\core\ddsc\include\dds\dds.h`
   - Add more APIs as needed (publishers, subscribers, QoS, etc.)

4. **Create Entity Handle Wrapper:**

   **File:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\Interop\DdsEntityHandle.cs` (NEW FILE)

   Study the old implementation at:  
   `D:\Work\FastCycloneDdsCsharpBindings\old_implem\src\CycloneDDS.Runtime\Interop\DdsEntityHandle.cs`

   ```csharp
   using System;
   
   namespace CycloneDDS.Runtime.Interop
   {
       internal sealed class DdsEntityHandle : IDisposable
       {
           private DdsApi.DdsEntity _entity;
           
           public DdsEntityHandle(DdsApi.DdsEntity entity)
           {
               _entity = entity;
           }
           
           public DdsApi.DdsEntity Entity => _entity;
           
           public void Dispose()
           {
               if (_entity.IsValid)
               {
                   DdsApi.dds_delete(_entity);
                   _entity = default;
               }
           }
       }
   }
   ```

5. **Create Exception Types:**

   **File:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsException.cs` (NEW FILE)

   Study:  
   `D:\Work\FastCycloneDdsCsharpBindings\old_implem\src\CycloneDDS.Runtime\DdsException.cs`

   ```csharp
   using System;
   using CycloneDDS.Runtime.Interop;
   
   namespace CycloneDDS.Runtime
   {
       public class DdsException : Exception
       {
           public DdsReturnCode ReturnCode { get; }
           
           public DdsException(string message, DdsReturnCode code) 
               : base(message)
           {
               ReturnCode = code;
           }
       }
   }
   ```

**Tests Required:**

**File:** `D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\PInvokeTests.cs` (NEW FILE)

- ✅ Test: Can load `ddsc.dll` (test `dds_create_participant` doesn't crash)
- ✅ Test: Can create participant (domain 0)
- ✅ Test: Can cleanup participant (no leaks)
- ✅ Test: Invalid entity returns error code
- ✅ Test: DdsEntityHandle disposes correctly

**Test Setup:**
1. Create test project: `D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj`
2. Add project reference to `CycloneDDS.Runtime`
3. **CRITICAL:** Add build task to copy `ddsc.dll`:
   ```xml
   <ItemGroup>
     <None Include="..\..\cyclone-bin\Release\ddsc.dll">
       <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
     </None>
   </ItemGroup>
   ```

**Validation:**
- All P/Invoke tests pass
- No DllNotFoundException
- Participant creation/deletion works

---

### Task 2: DdsParticipant Migration (FCDC-S018)

**File:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsParticipant.cs` (NEW FILE)  
**Task Definition:** See [SERDATA-TASK-MASTER.md](D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md#fcdc-s018-ddsparticipant-migration)

**Description:**  
Implement the `DdsParticipant` class (domain participant wrapper).

**Reference Implementation:**  
`D:\Work\FastCycloneDdsCsharpBindings\old_implem\src\CycloneDDS.Runtime\DdsParticipant.cs`

**Requirements:**

```csharp
using System;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime
{
    public sealed class DdsParticipant : IDisposable
    {
        private DdsEntityHandle? _handle;
        private readonly uint _domainId;
        
        public DdsParticipant(uint domainId = 0)
        {
            _domainId = domainId;
            
            // Create participant (QoS=null, listener=null for now)
            var entity = DdsApi.dds_create_participant(domainId, IntPtr.Zero, IntPtr.Zero);
            
            if (!entity.IsValid)
                throw new DdsException("Failed to create DDS participant", DdsReturnCode.Error);
            
            _handle = new DdsEntityHandle(entity);
        }
        
        public uint DomainId => _domainId;
        public bool IsDisposed => _handle == null;
        
        internal DdsApi.DdsEntity Entity
        {
            get
            {
                if (_handle == null)
                    throw new ObjectDisposedException(nameof(DdsParticipant));
                return _handle.Entity;
            }
        }
        
        public void Dispose()
        {
            _handle?.Dispose();
            _handle = null;
        }
    }
}
```

**Tests Required:**

**File:** `D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\DdsParticipantTests.cs` (NEW FILE)

- ✅ Test: Create participant (domain 0)
- ✅ Test: Create participant (domain 100)
- ✅ Test: DomainId property returns correct value
- ✅ Test: IsDisposed property correct
- ✅ Test: Dispose idempotent (calling twice is safe)
- ✅ Test: Accessing Entity after dispose throws ObjectDisposedException

**Validation:**
- All tests pass
- No memory leaks (verify with profiler if available)

---

### Task 3: Arena Enhancement for CDR (FCDC-S019)

**File:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\Memory\Arena.cs` (NEW FILE)  
**Task Definition:** See [SERDATA-TASK-MASTER.md](D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md#fcdc-s019-arena-enhancement-for-cdr)

**Description:**  
Enhance the Arena allocator for CDR buffer management (optional - can use `ArrayPool<byte>.Shared` directly).

**Reference Implementation:**  
`D:\Work\FastCycloneDdsCsharpBindings\old_implem\src\CycloneDDS.Runtime\Memory\Arena.cs`

**Simplified Version (Use ArrayPool Wrapper):**

```csharp
using System;
using System.Buffers;

namespace CycloneDDS.Runtime.Memory
{
    /// <summary>
    /// Lightweight wrapper around ArrayPool for CDR buffer management.
    /// </summary>
    public static class Arena
    {
        public static byte[] Rent(int minimumLength)
        {
            return ArrayPool<byte>.Shared.Rent(minimumLength);
        }
        
        public static void Return(byte[] buffer, bool clearArray = false)
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray);
        }
    }
}
```

**Tests Required:**

**File:** `D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\ArenaTests.cs` (NEW FILE)

- ✅ Test: Rent returns buffer of at least requested size
- ✅ Test: Return accepts buffer without error
- ✅ Test: Rent/Return cycle works (can rent again after return)
- ✅ Test: Can rent large buffer (1 MB)

**Validation:**
- All tests pass
- No out-of-memory errors

---

### Task 4: DdsWriter<T> (Serdata-Based) (FCDC-S020)

**File:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsWriter.cs` (NEW FILE)  
**Task Definition:** See [SERDATA-TASK-MASTER.md](D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md#fcdc-s020-ddswritert-serdata-based)

**Description:**  
Implement `DdsWriter<T>` that uses generated serializers and serdata APIs.

**Reference (OLD APPROACH - DO NOT COPY):**  
`D:\Work\FastCycloneDdsCsharpBindings\old_implem\src\CycloneDDS.Runtime\DdsWriter.cs`

**NEW APPROACH (Serdata-Based):**

**Prerequisites:**
- Type `T` must have generated `GetSerializedSize()` and `Serialize(ref CdrWriter)` methods
- Use a simple test type to start (e.g., struct with int + double)

**Implementation:**

```csharp
using System;
using System.Buffers;
using CycloneDDS.Core;
using CycloneDDS.Runtime.Interop;
using CycloneDDS.Runtime.Memory;

namespace CycloneDDS.Runtime
{
    public sealed class DdsWriter<T> : IDisposable
    {
        private DdsEntityHandle? _writerHandle;
        private DdsEntityHandle? _topicHandle;
        private readonly DdsParticipant _participant;
        private readonly string _topicName;
        
        // For testing: Accept topic descriptor as IntPtr
        // In production, auto-discover from metadata registry
        public DdsWriter(DdsParticipant participant, string topicName, IntPtr topicDescriptor)
        {
            _participant = participant ?? throw new ArgumentNullException(nameof(participant));
            _topicName = topicName;
            
            // Create topic
            var topic = DdsApi.dds_create_topic(
                participant.Entity,
                topicDescriptor,  // From idlc-generated descriptor
                topicName,
                IntPtr.Zero,      // QoS
                IntPtr.Zero);     // listener
            
            if (!topic.IsValid)
                throw new DdsException($"Failed to create topic {topicName}", DdsReturnCode.Error);
            
            _topicHandle = new DdsEntityHandle(topic);
            
            // Create writer
            var writer = DdsApi.dds_create_writer(
                participant.Entity,
                topic,
                IntPtr.Zero,  // QoS
                IntPtr.Zero); // listener
            
            if (!writer.IsValid)
            {
                _topicHandle.Dispose();
                throw new DdsException($"Failed to create writer for {topicName}", DdsReturnCode.Error);
            }
            
            _writerHandle = new DdsEntityHandle(writer);
        }
        
        // CRITICAL: This method requires T to have:
        // - int GetSerializedSize(int currentOffset)
        // - void Serialize(ref CdrWriter writer)
        public void Write(in T sample)
        {
            if (_writerHandle == null)
                throw new ObjectDisposedException(nameof(DdsWriter<T>));
            
            // 1. Calculate size (cast to dynamic to call generated method)
            dynamic dynamicSample = sample;
            int size = dynamicSample.GetSerializedSize(0);
            
            // 2. Rent buffer from Arena (ArrayPool)
            byte[] buffer = Arena.Rent(size);
            
            try
            {
                // 3. Serialize to buffer
                var writer = new ArrayBufferWriter<byte>(buffer, size);
                var cdrWriter = new CdrWriter(writer);
                dynamicSample.Serialize(ref cdrWriter);
                cdrWriter.Complete();
                
                // 4. Create serdata from CDR
                // NOTE: You need topic descriptor IntPtr here (stored from constructor)
                // For now, pass IntPtr.Zero and fix later
                IntPtr serdata;
                unsafe
                {
                    fixed (byte* ptr = buffer)
                    {
                        serdata = DdsApi.dds_create_serdata_from_cdr(
                            IntPtr.Zero,  // TODO: Store topic descriptor
                            new IntPtr(ptr),
                            (uint)size);
                    }
                }
                
                if (serdata == IntPtr.Zero)
                    throw new DdsException("Failed to create serdata", DdsReturnCode.Error);
                
                try
                {
                    // 5. Write serdata
                    int result = DdsApi.dds_write_serdata(_writerHandle.Entity, serdata);
                    if (result < 0)
                        throw new DdsException("Write failed", (DdsReturnCode)result);
                }
                finally
                {
                    // 6. Free serdata
                    DdsApi.dds_free_serdata(serdata);
                }
            }
            finally
            {
                // 7. Return buffer to pool
                Arena.Return(buffer);
            }
        }
        
        public void Dispose()
        {
            _writerHandle?.Dispose();
            _writerHandle = null;
            _topicHandle?.Dispose();
            _topicHandle = null;
        }
    }
}
```

**Tests Required:**

**File:** `D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\DdsWriterTests.cs` (NEW FILE)

**Test Setup:**
1. Define a simple test type in the test project:
   ```csharp
   // TestData.cs
   public partial struct TestData
   {
       public int Id;
       public double Value;
   }
   ```

2. Run code generator on test project to generate:
   - `TestData.Serialization.g.cs` with `GetSerializedSize()` and `Serialize()`

3. Use idlc to generate descriptor for TestData (or mock IntPtr.Zero for now)

**Tests:**
- ✅ Test: Create writer (with participant + topic name)
- ✅ Test: Write single sample (verify no exception)
- ✅ Test: Write 100 samples (verify no GC allocations - use `GC.GetTotalAllocatedBytes()`)
- ✅ Test: Dispose idempotent
- ✅ Test: Writing after dispose throws ObjectDisposedException

**Validation:**
- All tests pass
- **CRITICAL:** Verify zero GC allocations in steady state (measure with allocation profiler)
- Writer creates successfully
- No DDS errors in console

---

### Task 5: DdsReader<T> + ViewScope (FCDC-S021)

**File:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsReader.cs` (NEW FILE)  
**Task Definition:** See [SERDATA-TASK-MASTER.md](D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md#fcdc-s021-ddsreadert--viewscope)

**Description:**  
Implement `DdsReader<T, TView>` that returns view scopes over loaned CDR buffers.

**Reference (OLD APPROACH - DO NOT COPY):**  
`D:\Work\FastCycloneDdsCsharpBindings\old_implem\src\CycloneDDS.Runtime\DdsReader.cs`

**NEW APPROACH (View Scopes):**

```csharp
using System;
using CycloneDDS.Core;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime
{
    public sealed class DdsReader<T, TView> : IDisposable
        where TView : struct  // View must be ref struct, but C# doesn't allow that constraint
    {
        private DdsEntityHandle? _readerHandle;
        private DdsEntityHandle? _topicHandle;
        
        public DdsReader(DdsParticipant participant, string topicName, IntPtr topicDescriptor)
        {
            // Similar to DdsWriter: Create topic, create reader
            var topic = DdsApi.dds_create_topic(
                participant.Entity,
                topicDescriptor,
                topicName,
                IntPtr.Zero,
                IntPtr.Zero);
            
            if (!topic.IsValid)
                throw new DdsException($"Failed to create topic {topicName}", DdsReturnCode.Error);
            
            _topicHandle = new DdsEntityHandle(topic);
            
            var reader = DdsApi.dds_create_reader(
                participant.Entity,
                topic,
                IntPtr.Zero,
                IntPtr.Zero);
            
            if (!reader.IsValid)
            {
                _topicHandle.Dispose();
                throw new DdsException($"Failed to create reader for {topicName}", DdsReturnCode.Error);
            }
            
            _readerHandle = new DdsEntityHandle(reader);
        }
        
        // Take samples (returns view scope that must be disposed)
        public ViewScope<TView> Take(int maxSamples = 32)
        {
            if (_readerHandle == null)
                throw new ObjectDisposedException(nameof(DdsReader<T, TView>));
            
            IntPtr[] samples = new IntPtr[maxSamples];
            IntPtr[] infos = new IntPtr[maxSamples];  // DdsSampleInfo structs
            
            int count = DdsApi.dds_take(
                _readerHandle.Entity,
                samples,
                infos,
                (uint)maxSamples,
                0xFFFFFFFF);  // Mask: any state
            
            if (count < 0)
                throw new DdsException("Take failed", (DdsReturnCode)count);
            
            // Wrap in ViewScope
            return new ViewScope<TView>(_readerHandle.Entity, samples, count);
        }
        
        public void Dispose()
        {
            _readerHandle?.Dispose();
            _readerHandle = null;
            _topicHandle?.Dispose();
            _topicHandle = null;
        }
    }
    
    // View scope (loans CDR buffers from DDS)
    public ref struct ViewScope<TView> where TView : struct
    {
        private DdsApi.DdsEntity _readerEntity;
        private IntPtr[] _samples;
        private int _count;
        private TView[] _views;
        
        internal ViewScope(DdsApi.DdsEntity readerEntity, IntPtr[] samples, int count)
        {
            _readerEntity = readerEntity;
            _samples = samples;
            _count = count;
            _views = new TView[count];  // TODO: Stack allocate or reuse buffer
            
            // Deserialize each sample
            for (int i = 0; i < count; i++)
            {
                if (samples[i] != IntPtr.Zero)
                {
                    // Wrap CDR buffer in ReadOnlySpan
                    // NOTE: You need to know buffer length - get from DDS or use large max
                    unsafe
                    {
                        byte* ptr = (byte*)samples[i];
                        var span = new ReadOnlySpan<byte>(ptr, 4096);  // TODO: Use actual size
                        
                        var reader = new CdrReader(span);
                        
                        // Call generated Deserialize (dynamic for now)
                        // In production, use generic constraint or interface
                        // T.Deserialize(ref reader, out _views[i]);
                        
                        // For testing, manually deserialize TestDataView
                        _views[i] = default; // TODO: Implement deserialization
                    }
                }
            }
        }
        
        public ReadOnlySpan<TView> Samples => _views.AsSpan(0, _count);
        
        public void Dispose()
        {
            // Return loan to DDS
            if (_count > 0)
            {
                DdsApi.dds_return_loan(_readerEntity, _samples, _count);
                _count = 0;
            }
        }
    }
}
```

**Tests Required:**

**File:** `D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\DdsReaderTests.cs` (NEW FILE)

**Test Setup:**
1. Use same TestData type from DdsWriterTests
2. Generate `TestDataView` ref struct via code generator

**Tests:**
- ✅ Test: Create reader
- ✅ Test: Take with no data returns empty scope
- ✅ Test: Dispose idempotent
- ✅ Test: ViewScope disposes correctly (returns loan)

**Validation:**
- All tests pass
- No memory leaks
- ViewScope returns loan on dispose

---

### Task 6: End-to-End Integration Tests (FCDC-S022) 🚨 VALIDATION GATE

**File:** `D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\IntegrationTests.cs` (NEW FILE)  
**Task Definition:** See [SERDATA-TASK-MASTER.md](D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md#fcdc-s022-end-to-end-integration-tests-validation-gate)

**Description:**  
End-to-end tests proving the entire stack works: Code generation → Serialization → DDS pub/sub → Deserialization → Views.

**CRITICAL:** This is a **VALIDATION GATE**. Stage 4 cannot begin until these tests pass.

**Requirements:**

1. **Full Roundtrip Test:**
   ```csharp
   [Fact]
   public void FullRoundtrip_SimpleStruct_Success()
   {
       using var participant = new DdsParticipant(0);
       using var writer = new DdsWriter<TestData>(participant, "TestTopic", IntPtr.Zero);
       using var reader = new DdsReader<TestData, TestDataView>(participant, "TestTopic", IntPtr.Zero);
       
       // Write
       var sample = new TestData { Id = 42, Value = 3.14 };
       writer.Write(sample);
       
       // Wait for data (polling)
       Thread.Sleep(100);
       
       // Read
       using var scope = reader.Take();
       
       Assert.Equal(1, scope.Samples.Length);
       Assert.Equal(42, scope.Samples[0].Id);
       Assert.Equal(3.14, scope.Samples[0].Value, precision: 2);
   }
   ```

2. **Performance Test (Zero Allocations):**
   ```csharp
   [Fact]
   public void Write1000Samples_ZeroGCAllocations()
   {
       using var participant = new DdsParticipant(0);
       using var writer = new DdsWriter<TestData>(participant, "PerfTopic", IntPtr.Zero);
       
       var sample = new TestData { Id = 1, Value = 1.0 };
       
       // Warmup
       for (int i = 0; i < 10; i++)
           writer.Write(sample);
       
       // Measure allocations
       long before = GC.GetTotalAllocatedBytes(precise: true);
       
       for (int i = 0; i < 1000; i++)
           writer.Write(sample);
       
       long after = GC.GetTotalAllocatedBytes(precise: true);
       long allocated = after - before;
       
       // Allow small overhead (e.g., < 10 KB for 1000 writes)
       Assert.True(allocated < 10_000, 
           $"Expected < 10 KB allocated, got {allocated} bytes");
   }
   ```

3. **Multiple Partitions Test:**
   - Test partition isolation (if implemented)

4. **Large Data Test:**
   - Test struct with variable-size fields (strings, sequences)

5. **Error Handling Tests:**
   - Test write after dispose throws
   - Test read after dispose throws

**Minimum Tests:** 15 integration tests

**Tests Required:**
- ✅ Full roundtrip (simple struct)
- ✅ Full roundtrip (struct with string)
- ✅ Full roundtrip (struct with sequence)
- ✅ Write 1000 samples (zero GC allocations)
- ✅ Read 1000 samples (zero GC allocations)
- ✅ Multiple readers receive same data
- ✅ Partition isolation (if implemented)
- ✅ Write after dispose throws
- ✅ Read after dispose throws
- ✅ ViewScope dispose returns loan
- ✅ Large data (1 MB payload)
- ✅ Empty read returns empty scope
- ✅ Concurrent writes (thread safety)
- ✅ Concurrent reads (thread safety)
- ✅ Dispose order (participant last)

**Validation:**
- ✅ **ALL 15+ integration tests pass**
- ✅ Zero GC allocations in steady state (measured)
- ✅ No memory leaks (verify with profiler)
- ✅ No DDS errors in console
- ✅ Wire format matches Cyclone DDS C (if cross-testing)

---

## 🧪 Testing Requirements

**Minimum Test Counts:**
- Task 1 (P/Invoke): 5 tests
- Task 2 (Participant): 6 tests
- Task 3 (Arena): 4 tests
- Task 4 (Writer): 5 tests
- Task 5 (Reader): 4 tests
- Task 6 (Integration): 15 tests

**Total Minimum:** 39 tests

**Quality Standards:**
- **NOT ACCEPTABLE:** Tests that only verify "can I create this object"
- **REQUIRED:** Tests that verify actual behavior and edge cases
- **REQUIRED:** Performance tests verifying zero allocations
- **REQUIRED:** All tests must pass (no skipped tests)

**Test Execution:**

Run all tests:
```powershell
cd D:\Work\FastCycloneDdsCsharpBindings
dotnet test tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj
```

Run with verbose output:
```powershell
dotnet test --logger "console;verbosity=detailed"
```

**Expected Output:**
```
Passed! - Failed: 0, Passed: 39+, Skipped: 0
```

---

## 📊 Report Requirements

**Focus on Developer Insights, Not Understanding Checks**

The report should gather valuable professional feedback, not test your understanding.

**What to Document:**

### 1. Implementation Summary
- Brief summary of what you implemented
- Any architectural decisions you made beyond the spec
- Deviations from instructions (if any, with justification)

### 2. Issues Encountered
**Q1:** What issues did you encounter during implementation? How did you resolve them?

**Q2:** Did you spot any weak points in the existing codebase (Stage 1-2)? What would you improve?

### 3. Design Decisions
**Q3:** What design decisions did you make beyond the instructions? What alternatives did you consider?

**Q4:** The old implementation used native structs. This implementation uses serdata/CDR. What are the tradeoffs you observed?

### 4. Edge Cases and Testing
**Q5:** What edge cases did you discover that weren't mentioned in the spec?

**Q6:** Are there any performance concerns or optimization opportunities you noticed?

### 5. Stage 3 Completion Checklist
- [ ] All 6 tasks (FCDC-S017 through S022) complete
- [ ] All tests passing (39+ tests, 0 failures)
- [ ] Zero GC allocations verified in performance tests
- [ ] ddsc.dll copies correctly to test output
- [ ] No memory leaks (if profiled)
- [ ] Integration tests prove end-to-end functionality

---

## 🎯 Success Criteria

This batch is DONE when:

- [ ] **FCDC-S017** Complete: Runtime package + P/Invoke declarations
- [ ] **FCDC-S018** Complete: DdsParticipant implemented and tested
- [ ] **FCDC-S019** Complete: Arena implemented (or ArrayPool wrapper)
- [ ] **FCDC-S020** Complete: DdsWriter<T> implemented with serdata
- [ ] **FCDC-S021** Complete: DdsReader<T> + ViewScope implemented
- [ ] **FCDC-S022** Complete: 15+ integration tests passing
- [ ] **ALL** tests passing (39+ tests from Stage 3 + 162 tests from Stage 1-2)
- [ ] Zero GC allocations measured in performance tests
- [ ] Report submitted with insights and lessons learned

---

## ⚠️ Common Pitfalls to Avoid

1. **DLL Not Found:**
   - **Fix:** Ensure `ddsc.dll` is copied to test output directory (add to test csproj)
   - **Verify:** Check `bin\Debug\net8.0\ddsc.dll` exists

2. **P/Invoke Signature Mismatch:**
   - **Fix:** Cross-reference `cyclonedds\src\core\ddsc\include\dds\dds.h`
   - **Tool:** Use `dumpbin.exe` (Visual Studio) to inspect exported functions in `ddsc.dll`

3. **Dynamic Invocation Failure:**
   - **Fix:** Ensure test type has generated `GetSerializedSize()` and `Serialize()` methods
   - **Verify:** Run code generator on test project, check for `.g.cs` files

4. **Memory Leaks:**
   - **Fix:** Always return buffers to Arena/ArrayPool in `finally` block
   - **Fix:** Always return loans to DDS in ViewScope.Dispose()

5. **Topic Descriptor Issue:**
   - **Fix:** For MVP, pass `IntPtr.Zero` if descriptor optional
   - **Fix:** Use idlc to generate descriptor if required

6. **Integration Tests Fail:**
   - **Debug:** Add `Thread.Sleep()` between write and read (DDS discovery delay)
   - **Debug:** Check DDS console output for errors
   - **Debug:** Verify participant on same domain ID

---

## 📚 Reference Materials

**Task Definitions:**
- [SERDATA-TASK-MASTER.md](D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md) - Stage 3 tasks (FCDC-S017 through S022)

**Design Documents:**
- [SERDATA-DESIGN.md](D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-DESIGN.md) - Section 3 (Architecture), Section 8 (Memory), Section 11 (Integration)

**Old Implementation (Reference Only):**
- `D:\Work\FastCycloneDdsCsharpBindings\old_implem\src\CycloneDDS.Runtime\` - Study patterns, DO NOT copy

**Cyclone DDS Resources:**
- C Headers: `D:\Work\FastCycloneDdsCsharpBindings\cyclonedds\src\core\ddsc\include\dds\`
- Binaries: `D:\Work\FastCycloneDdsCsharpBindings\cyclone-bin\Release\`

**Code Generator (Already Complete):**
- `D:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\`

**Core Libraries (Already Complete):**
- `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Core\` - CdrWriter/Reader/Sizer
- `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Schema\` - Attributes

---

## 🎓 Learning Resources

**If you get stuck with Cyclone DDS:**

1. **Cyclone DDS C API Documentation:**
   - Look in `cyclonedds\docs\` (if available)
   - Search for "Cyclone DDS C API reference" online

2. **Serdata API Examples:**
   - Search `cyclonedds\` source tree for `dds_create_serdata_from_cdr` usage examples
   - Check `cyclonedds\src\core\ddsc\tests\` for test examples

3. **P/Invoke Debugging:**
   - Use `dumpbin /exports ddsc.dll` to see exported functions
   - Compare signatures with `dds.h` header file

4. **Ask Questions:**
   - Create `D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\questions\BATCH-13-QUESTIONS.md`
   - Use template from `D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\templates\QUESTIONS-TEMPLATE.md`

---

**Good luck! This is a critical batch. Stage 4 and beyond depend on your solid foundation. Take your time, test thoroughly, and report all insights.**
