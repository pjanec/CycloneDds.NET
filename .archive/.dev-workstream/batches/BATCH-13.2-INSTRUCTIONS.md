# BATCH-13.2: Performance & Correctness Corrections

**Batch Number:** BATCH-13.2 (Corrective)  
**Parent Batch:** BATCH-13 + BATCH-13.1  
**Estimated Effort:** 3-4 days  
**Priority:** CRITICAL - Performance Blocker

---

## 📋 Context

You successfully delivered a **functionally working** Runtime implementation with all tests passing. Excellent work on fixing the struct layout bug!

However, an **independent performance analysis** of the hot path (Read/Write) revealed **3 Critical Issues** that prevent achieving BATCH-13's core goal: **Zero-Allocation, High-Performance DDS Integration**.

**Current State:**
- ✅ 21/21 Runtime tests passing
- ✅ 95/95 CodeGen tests passing
- ✅ Functional correctness achieved
- ❌ Performance goals NOT met
- ❌ Serdata APIs still disabled (!)
- ❌ Integration tests missing (FCDC-S022)

**This batch fixes the performance issues and completes the missing work.**

---

## 🎯 Objectives

Fix **3 Critical Performance Issues** + Complete Integration Tests:

1. **DdsWriter Allocation:** Remove heap allocation on every write
2. **DdsReader Correctness:** Fix potential data corruption
3. **DdsReader Performance:** Implement lazy deserialization
4. **Enable Serdata APIs:** Actually use the DDS integration!
5. **Integration Tests:** Prove end-to-end with 15+ tests

---

## 🔴 Critical Issue #1: DdsWriter Heap Allocation

### Problem

**Location:** `Src\CycloneDDS.Runtime\DdsWriter.cs` lines 95-96

```csharp
var writerWrapper = new ArrayBufferWriterWrapper(buffer);  // ❌ HEAP ALLOC!
var cdr = new CdrWriter(writerWrapper);
```

**Why This is Bad:**
- `ArrayBufferWriterWrapper` is a **class** (reference type)
- Allocated on **heap** for EVERY `Write()` call
- Defeats "Zero-Allocation" goal
- GC pressure on hot path

**Root Cause:**
`CdrWriter` only has constructor for `IBufferWriter<byte>` (requires class).
No constructor for `Span<byte>` (zero-alloc).

### Fix Part 1: Add CdrWriter Span Constructor

**File:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Core\CdrWriter.cs`

**Changes:**

1. Make `_output` nullable
2. Add `Span<byte>` constructor
3. Update `EnsureSize()` to handle fixed buffers

```csharp
public ref struct CdrWriter
{
    private IBufferWriter<byte>? _output; // ← Make nullable
    private Span<byte> _span;
    private int _buffered;
    private int _totalWritten;

    // NEW: Zero-Alloc Constructor for Fixed Buffers
    public CdrWriter(Span<byte> buffer)
    {
        _output = null;  // Fixed buffer mode - no IBufferWriter
        _span = buffer;
        _buffered = 0;
        _totalWritten = 0;
    }

    // EXISTING: Keep this for dynamic buffers
    public CdrWriter(IBufferWriter<byte> output)
    {
        _output = output;
        _span = output.GetSpan();
        _buffered = 0;
        _totalWritten = 0;
    }

    // UPDATE: Handle fixed vs dynamic buffers
    private void EnsureSize(int size)
    {
        // If fixed buffer mode (_output == null)
        if (_output == null)
        {
            if (_buffered + size > _span.Length)
                throw new InvalidOperationException(
                    $"CdrWriter buffer overflow. Needed {_buffered + size}, " +
                    $"Capacity {_span.Length}");
            return;
        }

        // Existing dynamic resize logic
        if (_buffered + size > _span.Length)
        {
            _output.Advance(_buffered);
            _totalWritten += _buffered;
            _buffered = 0;
            _span = _output.GetSpan(size);
        }
    }
    
    public void Complete()
    {
        if (_output != null && _buffered > 0)
        {
            _output.Advance(_buffered);
            _totalWritten += _buffered;
            _buffered = 0;
        }
        // For fixed buffer, Complete() is no-op
    }
    
    // ... rest of existing methods unchanged ...
}
```

### Fix Part 2: Update DdsWriter to Use Span

**File:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsWriter.cs`

**Replace Write() method (lines 78-137):**

```csharp
public void Write(in T sample)
{
    if (_writerHandle == null)
        throw new ObjectDisposedException(nameof(DdsWriter<T>));

    // 1. Get Size (no alloc)
    int size = _sizer!(sample, 0); 

    // 2. Rent Buffer (no alloc - pooled)
    byte[] buffer = Arena.Rent(size);
    
    try
    {
        // 3. Serialize (ZERO ALLOC via new Span overload)
        var span = buffer.AsSpan(0, size);
        var cdr = new CdrWriter(span);  // ✅ No wrapper allocation!
        
        _serializer!(sample, ref cdr);
        cdr.Complete();
        
        // 4. Write to DDS via Serdata
        unsafe
        {
            fixed (byte* p = buffer)
            {
                IntPtr dataPtr = (IntPtr)p;
                
                // Create serdata from CDR bytes
                IntPtr serdata = DdsApi.dds_create_serdata_from_cdr(
                    _topicDescriptor,
                    dataPtr,
                    (uint)size);
                
                if (serdata == IntPtr.Zero)
                    throw new DdsException(
                        DdsApi.DdsReturnCode.Error,
                        "Failed to create serdata");
                
                try
                {
                    // Write serdata to DDS
                    int ret = DdsApi.dds_write_serdata(
                        _writerHandle.NativeHandle,
                        serdata);
                    
                    if (ret < 0)
                        throw new DdsException(
                            (DdsApi.DdsReturnCode)ret,
                            "dds_write_serdata failed");
                }
                finally
                {
                    DdsApi.dds_free_serdata(serdata);
                }
            }
        }
    }
    finally
    {
        Arena.Return(buffer);
    }
}
```

**What Changed:**
- ❌ REMOVED: `new ArrayBufferWriterWrapper(buffer)` allocation
- ✅ ADDED: `var span = buffer.AsSpan(0, size)` (zero-alloc)
- ✅ ADDED: `new CdrWriter(span)` (zero-alloc)
- ✅ ENABLED: Serdata APIs (no longer disabled!)

**Delete:** Remove `ArrayBufferWriterWrapper` class entirely (lines 199-219)

---

## 🔴 Critical Issue #2: DdsReader Data Corruption Risk

### Problem

`dds_take()` by default returns **deserialized C-structs**, NOT CDR bytes.

Your code assumes `ptr` points to CDR bytes:
```csharp
var span = new ReadOnlySpan<byte>((void*)ptr, 4096);
var reader = new CdrReader(span);  // ❌ Interpreting C-struct as CDR!
```

**Reality:**
- `ptr` points to native C struct memory
- Interpreting as CDR → garbage data
- Silent data corruption!

**Why Tests Don't Catch This:**
- No data validation (only check "no exception")
- No roundtrip verification

### Fix: Configure Reader for CDR Format

**File:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\Interop\DdsApi.cs`

**Add QoS APIs:**

```csharp
// QoS Management
[DllImport(DLL_NAME)]
public static extern IntPtr dds_create_qos();

[DllImport(DLL_NAME)]
public static extern void dds_delete_qos(IntPtr qos);

// Data Representation QoS
[DllImport(DLL_NAME)]
public static extern void dds_qset_data_representation(
    IntPtr qos,
    uint n,
    IntPtr[] values);

public const uint DDS_DATA_REPRESENTATION_XCDR1 = 0;
public const uint DDS_DATA_REPRESENTATION_XCDR2 = 1;
```

**File:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsReader.cs`

**Update constructor:**

```csharp
public DdsReader(DdsParticipant participant, string topicName, IntPtr topicDescriptor)
{
    // ... create topic ...
    
    // CRITICAL FIX: Configure QoS to receive CDR format
    var qos = DdsApi.dds_create_qos();
    
    try
    {
        // Request XCDR format (not C-struct!)
        IntPtr[] repr = new IntPtr[] { (IntPtr)DdsApi.DDS_DATA_REPRESENTATION_XCDR1 };
        DdsApi.dds_qset_data_representation(qos, 1, repr);
        
        // Create reader with QoS
        var reader = DdsApi.dds_create_reader(
            participant.Entity,
            topic,
            qos,  // ✅ Use QoS, not IntPtr.Zero!
            IntPtr.Zero);
        
        if (!reader.IsValid)
        {
            _topicHandle.Dispose();
            throw new DdsException(
                DdsApi.DdsReturnCode.Error,
                $"Failed to create reader for {topicName}");
        }
        
        _readerHandle = new DdsEntityHandle(reader);
    }
    finally
    {
        DdsApi.dds_delete_qos(qos);
    }
}
```

---

## 🔴 Critical Issue #3: DdsReader Eager Deserialization

### Problem

**Location:** `DdsReader.cs` `ViewScope` constructor

```csharp
_views = new TView[count];  // ❌ HEAP ALLOCATION

for (int i = 0; i < count; i++)  // ❌ EAGER - deserializes ALL
{
    // Deserialize every sample immediately
}
```

**Issues:**
1. Allocates managed array for views
2. Deserializes ALL samples even if you only access one
3. Wasted CPU for unused samples

### Fix: Lazy Deserialization via Indexer

**File:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsReader.cs`

**Replace `ViewScope` implementation:**

```csharp
public ref struct ViewScope<TView> where TView : struct
{
    private DdsApi.DdsEntity _reader;
    private IntPtr[] _samples;
    private DdsApi.DdsSampleInfo[] _infos;
    private int _count;
    
    // Deserializer delegate (from DdsReader)
    private delegate void DeserializeDelegate(ref CdrReader reader, out TView view);
    private DeserializeDelegate? _deserializer;
    
    // REMOVED: TView[] _views array (no eager deserialization!)
    
    internal ViewScope(
        DdsApi.DdsEntity reader,
        IntPtr[]? samples,
        DdsApi.DdsSampleInfo[]? infos,
        int count,
        DeserializeDelegate? deserializer)
    {
        _reader = reader;
        _samples = samples ?? Array.Empty<IntPtr>();
        _infos = infos ?? Array.Empty<DdsApi.DdsSampleInfo>();
        _count = count;
        _deserializer = deserializer;
    }

    public int Count => _count;

    public ReadOnlySpan<DdsApi.DdsSampleInfo> Infos =>
        _infos != null ? new ReadOnlySpan<DdsApi.DdsSampleInfo>(_infos, 0, _count) : default;

    // ✅ Lazy Accessor - Deserializes on demand
    public TView this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new IndexOutOfRangeException();
            
            if (!_infos[index].ValidData || _samples[index] == IntPtr.Zero)
                return default;
            
            // Deserialize ONLY when accessed
            unsafe
            {
                // Use large span - CdrReader handles bounds checking
                var span = new ReadOnlySpan<byte>(
                    (void*)_samples[index],
                    int.MaxValue);
                
                var reader = new CdrReader(span);
                
                // Call generated deserializer
                TView view;
                _deserializer!(ref reader, out view);
                return view;
            }
        }
    }

    public void Dispose()
    {
        if (_count > 0 && _samples != null)
        {
            DdsApi.dds_return_loan(_reader, _samples, _count);
        }
        
        _samples = Array.Empty<IntPtr>();
        _infos = Array.Empty<DdsApi.DdsSampleInfo>();
        _count = 0;
    }
}
```

**Update DdsReader.Take():**

```csharp
public ViewScope<TView> Take(int maxSamples = 32)
{
    if (_readerHandle == null)
        throw new ObjectDisposedException(nameof(DdsReader<T, TView>));
    
    IntPtr[] samples = new IntPtr[maxSamples];
    DdsApi.DdsSampleInfo[] infos = new DdsApi.DdsSampleInfo[maxSamples];
    
    int count = DdsApi.dds_take(
        _readerHandle.NativeHandle,
        samples,
        infos,
        (UIntPtr)maxSamples,
        (uint)maxSamples);
    
    if (count < 0)
    {
        if (count == (int)DdsApi.DdsReturnCode.NoData)
            return new ViewScope<TView>(_readerHandle.NativeHandle, null, null, 0, null);
        
        throw new DdsException((DdsApi.DdsReturnCode)count, "dds_take failed");
    }
    
    // Pass deserializer delegate
    return new ViewScope<TView>(
        _readerHandle.NativeHandle,
        samples,
        infos,
        count,
        _deserializer);  // ✅ Lazy deserialization!
}
```

---

## ✅ Task: Add Integration Tests (FCDC-S022)

**This was MISSING from BATCH-13!**

**File:** `D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\IntegrationTests.cs` (NEW FILE)

**Minimum 15 Tests Required:**

```csharp
using System;
using System.Threading;
using Xunit;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Tests;

namespace CycloneDDS.Runtime.Tests
{
    public class IntegrationTests
    {
        [Fact]
        public void FullRoundtrip_SimpleMessage_DataMatches()
        {
            using var participant = new DdsParticipant(0);
            using var desc = new DescriptorContainer(
                TestMessage.GetDescriptorOps(), 8, 4, 16, "TestMessage");
            
            using var writer = new DdsWriter<TestMessage>(
                participant, "RoundtripTopic", desc.Ptr);
            using var reader = new DdsReader<TestMessage, TestMessage>(
                participant, "RoundtripTopic", desc.Ptr);
            
            // Write sample
            var sent = new TestMessage { Id = 42, Value = 3.14 };
            writer.Write(sent);
            
            // Wait for delivery
            Thread.Sleep(100);
            
            // Read sample
            using var scope = reader.Take();
            
            Assert.Equal(1, scope.Count);
            Assert.Equal(42, scope[0].Id);
            Assert.InRange(scope[0].Value, 3.13, 3.15);
        }

        [Fact]
        public void Write1000Samples_ZeroGCAllocations()
        {
            using var participant = new DdsParticipant(0);
            using var desc = new DescriptorContainer(
                TestMessage.GetDescriptorOps(), 8, 4, 16);
            using var writer = new DdsWriter<TestMessage>(
                participant, "PerfTopic", desc.Ptr);
            
            var sample = new TestMessage { Id = 1, Value = 1.0 };
            
            // Warmup
            for (int i = 0; i < 10; i++)
                writer.Write(sample);
            
            // Measure allocations
            long before = GC.GetTotalAllocatedBytes(precise: true);
            
            for (int i = 0; i < 1000; i++)
                writer.Write(sample);
            
            long after = GC.GetTotalAllocatedBytes(precise: true);
            long allocated = after - before;
            
            // ✅ CRITICAL: Verify zero allocations!
            Assert.True(allocated < 10_000,
                $"Expected < 10 KB allocated, got {allocated} bytes");
        }

        [Fact]
        public void Read100Samples_MinimalAllocations()
        {
            using var participant = new DdsParticipant(0);
            using var desc = new DescriptorContainer(
                TestMessage.GetDescriptorOps(), 8, 4, 16);
            
            using var writer = new DdsWriter<TestMessage>(
                participant, "ReadPerfTopic", desc.Ptr);
            using var reader = new DdsReader<TestMessage, TestMessage>(
                participant, "ReadPerfTopic", desc.Ptr);
            
            // Write 100 samples
            for (int i = 0; i < 100; i++)
                writer.Write(new TestMessage { Id = i, Value = i });
            
            Thread.Sleep(200);
            
            // Measure read allocations
            long before = GC.GetTotalAllocatedBytes(precise: true);
            
            using var scope = reader.Take(100);
            
            // Access all samples (lazy deserialization)
            for (int i = 0; i < scope.Count; i++)
            {
                var _ = scope[i];
            }
            
            long after = GC.GetTotalAllocatedBytes(precise: true);
            long allocated = after - before;
            
            // Allow some allocation for deserialized structs
            Assert.True(allocated < 50_000,
                $"Expected < 50 KB allocated, got {allocated} bytes");
        }

        [Fact]
        public void MultipleReaders_ReceiveSameData()
        {
            using var participant = new DdsParticipant(0);
            using var desc = new DescriptorContainer(
                TestMessage.GetDescriptorOps(), 8, 4, 16);
            
            using var writer = new DdsWriter<TestMessage>(
                participant, "MultiTopic", desc.Ptr);
            using var reader1 = new DdsReader<TestMessage, TestMessage>(
                participant, "MultiTopic", desc.Ptr);
            using var reader2 = new DdsReader<TestMessage, TestMessage>(
                participant, "MultiTopic", desc.Ptr);
            
            // Write
            writer.Write(new TestMessage { Id = 99, Value = 9.9 });
            Thread.Sleep(100);
            
            // Both readers should get it
            using var scope1 = reader1.Take();
            using var scope2 = reader2.Take();
            
            Assert.Equal(1, scope1.Count);
            Assert.Equal(1, scope2.Count);
            Assert.Equal(99, scope1[0].Id);
            Assert.Equal(99, scope2[0].Id);
        }

        [Fact]
        public void LazyDeserialization_OnlyAccessedSamplesDeserialized()
        {
            using var participant = new DdsParticipant(0);
            using var desc = new DescriptorContainer(
                TestMessage.GetDescriptorOps(), 8, 4, 16);
            
            using var writer = new DdsWriter<TestMessage>(
                participant, "LazyTopic", desc.Ptr);
            using var reader = new DdsReader<TestMessage, TestMessage>(
                participant, "LazyTopic", desc.Ptr);
            
            // Write 10 samples
            for (int i = 0; i < 10; i++)
                writer.Write(new TestMessage { Id = i, Value = i });
            
            Thread.Sleep(100);
            
            using var scope = reader.Take(10);
            Assert.Equal(10, scope.Count);
            
            // Only access first sample
            var first = scope[0];
            Assert.Equal(0, first.Id);
            
            // Other samples not deserialized (test passes if no error)
        }

        // Add 10 more tests to reach 15+:
        // - Write after dispose throws
        // - Read after dispose throws
        // - ViewScope dispose returns loan
        // - Empty read returns empty scope
        // - Large data test (1 MB)
        // - Concurrent writes (thread safety)
        // - Concurrent reads
        // - Dispose order (participant last)
        // - Invalid topic descriptor fails gracefully
        // - Multiple writers same topic
    }
}
```

**Add remaining 10 tests yourself to reach 15+ total.**

---

## 🧪 Testing Requirements

**Test Execution:**

```powershell
cd D:\Work\FastCycloneDdsCsharpBindings

# Run all tests
dotnet test

# Run with allocation tracking
dotnet test --logger "console;verbosity=detailed"

# Check final count should be 248+ (21 Runtime + 15 Integration + 95 CodeGen + Stage 1-2)
```

**Validation:**
- [ ] All 248+ tests passing
- [ ] Zero GC allocations verified in `Write1000Samples_ZeroGCAllocations`
- [ ] Read allocations minimal in `Read100Samples_MinimalAllocations`
- [ ] Full roundtrip data matches
- [ ] Lazy deserialization working

---

## 📊 Report Requirements

**File:** `D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reports\BATCH-13.2-REPORT.md`

**Answer These:**

**Q1:** Did the performance fixes work? Show GC allocation measurements before/after.

**Q2:** Did enabling serdata APIs work? Any issues?

**Q3:** How does lazy deserialization impact performance vs eager?

**Q4:** What was the most challenging fix? Why?

**Q5:** Are there any remaining allocation hotspots you noticed?

**CRITICAL:** Include profiler screenshots showing zero allocations in write path!

---

## 🎯 Success Criteria

This batch is DONE when:

- [ ] CdrWriter has `Span<byte>` constructor
- [ ] DdsWriter Write() is zero-alloc (verified with profiler)
- [ ] Serdata APIs enabled and working
- [ ] DdsReader configured for CDR format (QoS)
- [ ] ViewScope uses lazy deserialization
- [ ] 15+ integration tests passing
- [ ] **ALL** 248+ tests passing (0 failures)
- [ ] Zero GC allocations measured in performance tests
- [ ] Report submitted with profiler evidence

---

## ⚠️ Common Pitfalls

1. **Forgetting to Remove ArrayBufferWriterWrapper Class**
   - Delete the entire class after updating DdsWriter

2. **QoS APIs Not in DLL**
   - If `dds_qset_data_representation` missing, use default QoS for now
   - Document in report

3. **Lazy Deserialization Crashes**
   - Ensure CdrReader bounds checking works with large span
   - Test with invalid data

4. **Tests Still Allocating**
   - Use `dotnet-counters` or profiler, not just `GC.GetTotalAllocatedBytes()`
   - Check for boxing, lambda captures, etc.

---

**Good luck! This completes the performance work for Stage 3. After this, Stage 3 is TRULY complete!** 🚀
