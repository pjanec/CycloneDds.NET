# FCDC-038: Arena-Backed Unmarshalling

**Task ID:** FCDC-038  
**Phase:** 5 - Advanced Features & Polish  
**Priority:** High (GC Optimization)  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-014 (Arena), FCDC-011 (Marshallers)  
**Design Reference:** `docs/ADVANCED-OPTIMIZATIONS-DESIGN.md` §3

---

## Objective

Reduce GC pressure by **50%** in high-throughput readers by allocating sequences/strings from Arena instead of GC heap.

---

## Problem Statement

**Current Unmarshalling:**
```csharp
var managed = new Message();
managed.SensorData = new double[100];  // GC allocation!
managed.Timestamps = new long[100];    // GC allocation!
managed.Name = "Sample";               // GC allocation!
```

**High-throughput impact:**
- 10K reads/sec → 30K+ GC allocations/sec (data + arrays + strings)
- Gen0 collection every 50-100ms
- Latency spikes from GC pauses

---

## Solution: Arena Allocations

**API:**
```csharp
using var arena = new Arena(8192);
using var scope = reader.TakeWithArena(arena);

foreach (var sample in scope.Samples)
{
    // sample.SensorData backed by arena, not GC heap
    ProcessSample(sample);
}
// arena.Dispose() frees all allocations
```

**Performance Target:** 50% fewer GC allocations, 30% fewer Gen0 collections.

---

## Implementation Steps

### Step 1: Extend IMarshaller Interface

**File:** `src/CycloneDDS.CodeGen.Runtime/IMarshaller.cs`

```csharp
public interface IMarshaller<TManaged, TNative>
    where TNative : unmanaged
{
    // Existing
    void Marshal(TManaged managed, ref TNative native);
    void Unmarshal(in TNative native, ref TManaged managed);
    
    // NEW: Arena-backed unmarshalling
    /// <summary>
    /// Unmarshals native data to managed using arena memory for sequences/strings.
    /// Managed object is valid only while arena is alive.
    /// </summary>
    void UnmarshalWithArena(in TNative native, ref TManaged managed, Arena arena);
}
```

### Step 2: Extend Arena with Span Allocation

**File:** `src/CycloneDDS.Runtime/Memory/Arena.cs`

```csharp
public unsafe class Arena : IDisposable
{
    // Existing: AllocBytes, Dispose, etc.
    
    /// <summary>
    /// Allocates a Span of T from arena memory.
    /// Valid until arena disposed.
    /// </summary>
    public Span<T> AllocSpan<T>(int count) where T : unmanaged
    {
        int size = sizeof(T) * count;
        byte* ptr = AllocBytes(size);
        return new Span<T>(ptr, count);
    }
    
    /// <summary>
    /// Allocates string from arena and returns as managed string.
    /// String data backed by arena until dispose.
    /// </summary>
    public string AllocString(IntPtr sourcePtr, int length)
    {
        if (sourcePtr == IntPtr.Zero || length == 0)
            return string.Empty;
        
        Span<byte> arenaBytes = AllocBytes(length + 1);
        var sourceSpan = new Span<byte>((void*)sourcePtr, length);
        sourceSpan.CopyTo(arenaBytes);
        arenaBytes[length] = 0;
        
        return Encoding.UTF8.GetString(arenaBytes[..length]);
    }
}
```

### Step 3: Update Marshaller Code Generation

**File:** `tools/CycloneDDS.CodeGen/Emitters/MarshallerEmitter.cs`

Add arena-backed unmarshal methods:

```csharp
// Generated marshaller example:
public unsafe class MessageMarshaller : IMarshaller<Message, MessageNative>
{
    // Existing Unmarshal...
    
    public void UnmarshalWithArena(in MessageNative native, ref Message managed, Arena arena)
    {
        managed.Id = native.Id;
        managed.Timestamp = native.Timestamp;
        
        // Sequence: Allocate from arena
        if (native.SensorData.Data != IntPtr.Zero)
        {
            int length = (int)native.SensorData.Length;
            Span<double> arenaSpan = arena.AllocSpan<double>(length);
            
            var nativeSpan = new Span<double>((void*)native.SensorData.Data, length);
            nativeSpan.CopyTo(arenaSpan);
            
            managed.SensorData = arenaSpan.ToArray(); // Still converts to array
            // Alternative: Custom ArenaArray<T> that wraps Span
        }
        
        // String: Allocate from arena
        if (native.Name != IntPtr.Zero)
        {
            int len = strlen(native.Name);
            managed.Name = arena.AllocString(native.Name, len);
        }
    }
}
```

**Code Generation Changes:**
1. Detect `IMarshaller` interface
2. Generate `UnmarshalWithArena` method
3. Use `arena.AllocSpan<T>()` for sequences
4. Use `arena.AllocString()` for strings
5. Copy native data to arena, not GC heap

### Step 4: Add TakeWithArena to DdsReader

**File:** `src/CycloneDDS.Runtime/DdsReader.cs`

```csharp
public sealed class DdsReader<TNative> where TNative : unmanaged
{
    // Existing Take methods...
    
    /// <summary>
    /// Takes samples with arena-backed memory for sequences/strings.
    /// Samples valid only while returned scope is alive.
    /// </summary>
    public TakeScope<TManaged, TNative> TakeWithArena<TManaged>(
        Arena arena,
        IMarshaller<TManaged, TNative> marshaller,
        int maxSamples = 32)
    {
        // dds_take loan
        IntPtr samples = IntPtr.Zero;
        IntPtr infos = IntPtr.Zero;
        
        var result = DdsApi.dds_take(
            _readerHandle.Entity,
            ref samples,
            ref infos,
            maxSamples,
            maxSamples);
        
        if (result < 0)
            throw new DdsException("Take failed", (DdsReturnCode)result);
        
        // Unmarshal with arena
        var managedSamples = new TManaged[result];
        unsafe
        {
            TNative* nativePtr = (TNative*)samples;
            for (int i = 0; i < result; i++)
            {
                marshaller.UnmarshalWithArena(
                    nativePtr[i],
                    ref managedSamples[i],
                    arena);
            }
        }
        
        return new TakeScope<TManaged, TNative>(
            this,
            samples,
            managedSamples,
            result);
    }
}
```

---

## Testing Requirements

### Unit Tests

```csharp
[Fact]
public void ArenaUnmarshal_Sequence_UsesArena()
{
    using var arena = new Arena(4096);
    using var participant = new DdsParticipant();
    using var writer = new DdsWriter<ArrayMessageNative>(participant);
    using var reader = new DdsReader<ArrayMessageNative>(participant);
    
    Thread.Sleep(100);
    
    // Write
    var sent = new ArrayMessageNative
    {
        Id = 1,
        FixedIntArray = new int[] { 1, 2, 3, 4, 5 }
    };
    writer.Write(ref sent);
    
    // Read with arena
    var marshaller = new ArrayMessageMarshaller();
    using var scope = reader.TakeWithArena(arena, marshaller, 1);
    
    Assert.Single(scope.Samples);
    Assert.Equal(1, scope.Samples[0].Id);
    Assert.Equal(new int[] { 1, 2, 3, 4, 5 }, scope.Samples[0].FixedIntArray);
}

[Fact]
public void ArenaUnmarshal_GCReduction_Measured()
{
    // Measure GC allocations with/without arena
    long allocsBefore = GC.GetAllocatedBytesForCurrentThread();
    
    using var arena = new Arena(8192);
    // Unmarshal 100 messages with arena
    for (int i = 0; i < 100; i++)
    {
        var msg = new Message();
        marshaller.UnmarshalWithArena(native, ref msg, arena);
    }
    
    long allocsAfter = GC.GetAllocatedBytesForCurrentThread();
    long arenaAllocs = allocsAfter - allocsBefore;
    
    // Compare to non-arena
    allocsBefore = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 100; i++)
    {
        var msg = new Message();
        marshaller.Unmarshal(native, ref msg);
    }
    allocsAfter = GC.GetAllocatedBytesForCurrentThread();
    long gcAllocs = allocsAfter - allocsBefore;
    
    // Arena should use <50% allocations
    Assert.True(arenaAllocs < gcAllocs * 0.5);
}
```

### Performance Benchmarks

```csharp
[MemoryDiagnoser]
public class ArenaUnmarshalBenchmark
{
    [Benchmark(Baseline = true)]
    public void UnmarshalGC_100Messages()
    {
        for (int i = 0; i < 100; i++)
        {
            var msg = new Message();
            marshaller.Unmarshal(native, ref msg);
        }
    }
    
    [Benchmark]
    public void UnmarshalArena_100Messages()
    {
        using var arena = new Arena(16384);
        for (int i = 0; i < 100; i++)
        {
            var msg = new Message();
            marshaller.UnmarshalWithArena(native, ref msg, arena);
        }
    }
}
```

**Expected:** 50% reduction in "Allocated" column.

---

## Documentation Requirements

1. **WARNING:** Managed objects invalid after arena disposal
2. **Usage pattern:** High-throughput reader example
3. **When to use:** > 1000 reads/sec benefit significantly
4. **When NOT to use:** Long-lived data (arena ties up memory)

---

## Acceptance Criteria

1. ✅ `UnmarshalWithArena` generates correctly for all types
2. ✅ Sequences allocated from arena, not GC
3. ✅ Strings allocated from arena, not GC
4. ✅ ≥50% reduction in GC allocations (measured)
5. ✅ ≥30% reduction in Gen0 collections (long-running test)
6. ✅ Clear documentation of lifetime constraints
7. ✅ All integration tests pass

---

## Design Reference

See `docs/ADVANCED-OPTIMIZATIONS-DESIGN.md` Section 3: Arena-Backed Unmarshalling

**Key Design Points:**
- Extends IMarshaller with UnmarshalWithArena method
- Arena.AllocSpan<T>() for sequences
- Arena.AllocString() for UTF-8 strings
- TakeScope manages arena lifetime
- 50% GC reduction target
