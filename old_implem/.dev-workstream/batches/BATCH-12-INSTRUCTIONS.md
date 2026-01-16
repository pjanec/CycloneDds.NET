# BATCH-12: DdsParticipant + DdsWriter/Reader (COMBINED)

**Batch Number:** BATCH-12  
**Tasks:** FCDC-016 (DdsParticipant), FCDC-017 (DdsWriter), FCDC-018 (DdsReader - inline only)  
**Phase:** Phase 3 - Runtime Components  
**Estimated Effort:** 9-12 days  
**Priority:** CRITICAL  
**Dependencies:** BATCH-11 (Arena, P/Invoke)

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

1. **Task 1 (DdsParticipant):** Implement → Write tests → **ALL tests pass** ✅
2. **Task 2 (DdsWriter):** Implement → Write tests → **ALL tests pass** ✅
3. **Task 3 (DdsReader):** Implement → Write tests → **ALL tests pass** ✅

---

## 📋 Required Reading

1. **Tasks:** `docs/FCDC-TASK-MASTER.md` → FCDC-016, FCDC-017, FCDC-018
2. **Design:** `docs/FCDC-DETAILED-DESIGN.md` → §6.1-6.3, §11.2 Error Handling
3. **Previous:** `.dev-workstream/reviews/BATCH-11-REVIEW.md`

**Report:** `.dev-workstream/reports/BATCH-12-REPORT.md`

---

## 🎯 Objectives

**Part 1: DdsParticipant**
1. Wrapper for dds_create_participant
2. Partition configuration
3. Deterministic disposal (cleanup native handle)
4. QoS management

**Part 2: DdsWriter<TNative>**
5. Auto-discover topic metadata from registry
6. Create publisher, topic, writer
7. Write(), WriteDispose(), TryWrite()
8. Error mapping to DdsException

**Part 3: DdsReader<TNative>**
9. Zero-copy read strategy
10. Take(), TryTake(), Read()
11. Handle DDS loans/return_loan
12. Sample info wrapper

---

## ✅ Task 1: DdsParticipant Implementation

**File:** `src/CycloneDDS.Runtime/DdsParticipant.cs` (NEW)

```csharp
using System;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime;

public sealed class DdsParticipant : IDisposable
{
    private DdsEntityHandle? _handle;
    private readonly uint _domainId;
    private readonly string[] _partitions;
    
    public DdsParticipant(uint domainId = 0, params string[] partitions)
    {
        _domainId = domainId;
        _partitions = partitions ?? Array.Empty<string>();
        
        // Create participant (QoS=null, listener=null for now)
        var entity = DdsApi.dds_create_participant(domainId, IntPtr.Zero, IntPtr.Zero);
        
        if (!entity.IsValid)
            throw new DdsException("Failed to create DDS participant", DdsReturnCode.Error);
        
        _handle = new DdsEntityHandle(entity);
    }
    
    public uint DomainId => _domainId;
    public IReadOnlyList<string> Partitions => _partitions;
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
```

**File:** `src/CycloneDDS.Runtime/DdsException.cs` (NEW)

```csharp
using System;

namespace CycloneDDS.Runtime;

public enum DdsReturnCode
{
    Ok = 0,
    Error = -1,
    Timeout = -2,
    OutOfResources = -3,
    BadParameter = -4
}

public class DdsException : Exception
{
    public DdsReturnCode Code { get; }
    
    public DdsException(string message, DdsReturnCode code) 
        : base($"{message} (Code: {code})")
    {
        Code = code;
    }
}
```

---

## ✅ Task 2: DdsWriter<TNative>

**File:** `src/CycloneDDS.Runtime/DdsWriter.cs` (NEW)

```csharp
using System;
using System.Runtime.InteropServices;
using CycloneDDS.Runtime.Interop;
using CycloneDDS.CodeGen.Runtime;

namespace CycloneDDS.Runtime;

public sealed class DdsWriter<TNative> : IDisposable where TNative : unmanaged
{
    private DdsEntityHandle? _writerHandle;
    private readonly DdsParticipant _participant;
    private readonly TopicMetadata _metadata;
    
    public DdsWriter(DdsParticipant participant)
    {
        _participant = participant ?? throw new ArgumentNullException(nameof(participant));
        
        // Auto-discover topic metadata
        var typeName = typeof(TNative).Name;
        if (typeName.EndsWith("Native"))
            typeName = typeName[..^6]; // Remove "Native" suffix
        
        var allTopics = MetadataRegistry.GetAllTopics();
        _metadata = allTopics.FirstOrDefault(m => m.NativeType == typeof(TNative))
            ?? throw new DdsException($"No topic metadata found for {typeof(TNative).Name}", 
                DdsReturnCode.BadParameter);
        
        // Create topic
        var topic = DdsApi.dds_create_topic(
            participant.Entity,
            IntPtr.Zero, // descriptor (TODO: generate from metadata)
            _metadata.TopicName,
            IntPtr.Zero, // QoS
            IntPtr.Zero); // listener
        
        if (!topic.IsValid)
            throw new DdsException($"Failed to create topic {_metadata.TopicName}", 
                DdsReturnCode.Error);
        
        // Create writer
        var writer = DdsApi.dds_create_writer(
            participant.Entity,
            topic,
            IntPtr.Zero, // QoS
            IntPtr.Zero); // listener
        
        if (!writer.IsValid)
        {
            DdsApi.dds_delete(topic);
            throw new DdsException($"Failed to create writer for {_metadata.TopicName}", 
                DdsReturnCode.Error);
        }
        
        _writerHandle = new DdsEntityHandle(writer);
    }
    
    public unsafe void Write(ref TNative sample)
    {
        if (_writerHandle == null)
            throw new ObjectDisposedException(nameof(DdsWriter<TNative>));
        
        fixed (TNative* ptr = &sample)
        {
            var result = DdsApi.dds_write(_writerHandle.Entity, new IntPtr(ptr));
            if (result < 0)
                throw new DdsException("Write failed", (DdsReturnCode)result);
        }
    }
    
    public unsafe bool TryWrite(ref TNative sample)
    {
        if (_writerHandle == null)
            return false;
        
        fixed (TNative* ptr = &sample)
        {
            var result = DdsApi.dds_write(_writerHandle.Entity, new IntPtr(ptr));
            return result >= 0;
        }
    }
    
    public void Dispose()
    {
        _writerHandle?.Dispose();
        _writerHandle = null;
    }
}
```

---

## ✅ Task 3: DdsReader<TNative>

**File:** `src/CycloneDDS.Runtime/DdsReader.cs` (NEW)

```csharp
using System;
using System.Runtime.InteropServices;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime;

public sealed class DdsReader<TNative> : IDisposable where TNative : unmanaged
{
    private DdsEntityHandle? _readerHandle;
    private readonly DdsParticipant _participant;
    
    public DdsReader(DdsParticipant participant)
    {
        _participant = participant ?? throw new ArgumentNullException(nameof(participant));
        
        // Similar topic discovery as Writer
        // Create topic, then reader
        // ... (similar to Writer logic)
    }
    
    public unsafe int Take(Span<TNative> buffer, int maxSamples = 32)
    {
        if (_readerHandle == null)
            throw new ObjectDisposedException(nameof(DdsReader<TNative>));
        
        var samples = new IntPtr[maxSamples];
        var info = new IntPtr[maxSamples];
        
        var count = DdsApi.dds_take(
            _readerHandle.Entity,
            samples,
            info,
            maxSamples,
            0); // mask
        
        if (count < 0)
            throw new DdsException("Take failed", (DdsReturnCode)count);
        
        // Copy to buffer
        for (int i = 0; i < count && i < buffer.Length; i++)
        {
            buffer[i] = Marshal.PtrToStructure<TNative>(samples[i]);
        }
        
        // Return loan
        if (count > 0)
            DdsApi.dds_return_loan(_readerHandle.Entity, samples, count);
        
        return count;
    }
    
    public unsafe bool TryTake(out TNative sample)
    {
        sample = default;
        if (_readerHandle == null)
            return false;
        
        Span<TNative> buffer = stackalloc TNative[1];
        var count = Take(buffer, 1);
        
        if (count > 0)
        {
            sample = buffer[0];
            return true;
        }
        
        return false;
    }
    
    public void Dispose()
    {
        _readerHandle?.Dispose();
        _readerHandle = null;
    }
}
```

---

## 🧪 Testing Requirements

**CRITICAL: We have the native Cyclone DDS library at `cyclone-bin/Release/`**

### Test Project Setup

**File:** `tests/CycloneDDS.Runtime.Tests/CycloneDDS.Runtime.Tests.csproj` (MODIFY)

Add native library copy to test output:

```xml
<ItemGroup>
  <None Include="..\..\cyclone-bin\Release\*.dll" LinkBase="native">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### Testing Strategy: ACTUAL Native Integration Tests

**All tests should call the ACTUAL native DDS library and verify it works.**

**Minimum 15 Integration Tests:**

**Part 1: DdsParticipant (5 tests)**
1. ✅ `DdsParticipant_Create_InitializesNativeParticipant` - ACTUAL dds_create_participant call
2. ✅ `DdsParticipant_Dispose_DeletesNativeHandle` - Verify dds_delete called
3. ✅ `DdsParticipant_DoubleDispose_Safe` - No crash on second dispose
4. ✅ `DdsParticipant_AfterDispose_ThrowsObjectDisposed` - State check
5. ✅ `DdsParticipant_InvalidDomain_HandlesError` - Test error handling

**Part 2: DdsWriter (5 tests)**
6. ✅ `DdsWriter_Create_InitializesNativeTopic` - ACTUAL topic creation
7. ✅ `DdsWriter_Write_CallsNativeWrite` - ACTUAL dds_write call (can write null data for test)
8. ✅ `DdsWriter_TryWrite_ReturnsTrue` - Verify success code
9. ✅ `DdsWriter_AfterDispose_ThrowsObjectDisposed` - State validation
10. ✅ `DdsWriter_InvalidType_ThrowsDdsException` - Missing metadata handling

**Part 3: DdsReader (5 tests)**
11. ✅ `DdsReader_Create_InitializesNativeReader` - ACTUAL reader creation
12. ✅ `DdsReader_Take_WithNoData_ReturnsZero` - ACTUAL dds_take call
13. ✅ `DdsReader_TryTake_WithNoData_ReturnsFalse` - Empty reader behavior
14. ✅ `DdsReader_AfterDispose_ThrowsObjectDisposed` - State check
15. ✅ `DdsReader_Take_ReturnsLoan_FreesCorrectly` - VERIFY dds_return_loan called

**Example Integration Test:**

```csharp
[Fact]
public void DdsParticipant_Create_InitializesNativeParticipant()
{
    // This should ACTUALLY call dds_create_participant and succeed
    using var participant = new DdsParticipant(domainId: 0);
    
    Assert.False(participant.IsDisposed);
    Assert.Equal(0u, participant.DomainId);
    
    // Verify native handle is valid (not IntPtr.Zero)
    // If creation failed, constructor should have thrown DdsException
}

[Fact]
public void DdsWriter_Create_InitializesNativeTopic()
{
    using var participant = new DdsParticipant();
    
    // This should ACTUALLY create topic and writer
    // Assumes TestMessageNative is in metadata registry
    using var writer = new DdsWriter<TestMessageNative>(participant);
    
    // If creation succeeded, writer is not null and not disposed
    Assert.False(writer.IsDisposed);
}
```

### Test Data Setup

Create a simple test type for writer/reader tests:

**File:** `tests/CycloneDDS.Runtime.Tests/TestTypes.cs` (NEW)

```csharp
using System.Runtime.InteropServices;

namespace CycloneDDS.Runtime.Tests;

[StructLayout(LayoutKind.Sequential)]
public struct TestMessageNative
{
    public int Id;
    public int Value;
}

// Register in metadata for auto-discovery
// (Or manually test with known type)
```

---

## 📊 Report Requirements

1. **Implementation Summary**
2. **Test Results** (141+ tests: 126 previous + 15 new)
3. **Developer Insights:**
   - **Q1:** What issues did you encounter? How did you resolve them?
   - **Q2:** What weak points did you spot? What would you improve?
   - **Q3:** What design decisions did you make beyond the spec?
   - **Q4:** What edge cases did you discover?

---

## 🎯 Success Criteria

1. ✅ DdsParticipant with disposal
2. ✅ DdsWriter with metadata auto-discovery
3. ✅ DdsReader with zero-copy Take
4. ✅ DdsException with return codes
5. ✅ All handle cleanup deterministic
6. ✅ Native DLLs copied to test output
7. ✅ 15+ INTEGRATION tests calling ACTUAL native library
8. ✅ All 126 previous tests still passing (141 total)
9. ✅ Tests verify participant/topic/writer/reader creation succeeds

---

## ⚠️ Common Pitfalls

1. **Native DLL not found** - Ensure cyclone-bin DLLs copied to test output
2. **Metadata discovery** - Handle missing types gracefully
3. **Native handle lifecycle** - Always dispose in reverse creation order
4. **Loan return** - MUST call dds_return_loan after dds_take
5. **Test cleanup** - Dispose all DDS entities or tests will leak resources
6. **Domain isolation** - Use different domain IDs per test to avoid interference

---

**Focus: Core DDS runtime - participant, writer, reader with proper resource management.**
