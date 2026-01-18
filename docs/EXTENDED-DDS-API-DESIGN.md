# Extended DDS API Design - Modern C# Idiomatic Interface

**Document Version:** 1.0  
**Date:** 2026-01-18  
**Status:** Approved for Implementation  
**Priority:** HIGH (Insert before Stage 4-Deferred and Stage 5)

---

## 1. Executive Summary

This document defines the extended DDS API features that transform the FastCycloneDDS C# bindings from a "Zero-Copy Fast Core" into a complete, production-ready DDS implementation with modern .NET idioms.

**Goal:** Provide essential DDS features (Read/Take filtering, Async/Await, Discovery, Content Filtering, Instance Management) in a way that feels natural to C# developers while maintaining zero-allocation performance where it matters.

**Strategic Position:** These features are foundational requirements that must be implemented BEFORE advanced optimizations (Stage 4-Deferred) and production packaging (Stage 5), as they represent core DDS functionality that users expect.

---

## 2. Design Principles

### 2.1 Core Tenets

1. **Zero-Allocation Hot Path:** Performance-critical loops (Take/Read) must remain allocation-free
2. **Async-First:** Modern .NET apps use `async/await`, not blocking threads
3. **Type Safety:** Use strong typing over `IntPtr` and magic constants
4. **Idiomatic C#:** Leverage `[Flags]` enums, Events, Properties, and `ValueTask`
5. **Progressive Disclosure:** Simple APIs for common cases, powerful APIs for advanced users

### 2.2 Anti-Patterns to Avoid

❌ **Don't:** Implement `IEnumerable<T>` on Reader (lazy evaluation doesn't fit DDS semantics)  
❌ **Don't:** Use C# Events for high-frequency data (use events only for status/discovery)  
❌ **Don't:** Attempt LINQ-to-SQL translation for filters (use string pass-through)  
❌ **Don't:** Pull in heavy dependencies like Reactive Extensions

---

## 3. Feature Overview

### 3.1 Feature Priorities

| Feature | Priority | Rationale |
|---------|----------|-----------|
| **Read vs Take + Masks** | 🔴 Critical | Essential for correct event processing patterns |
| **Async/Await (WaitDataAsync)** | 🔴 Critical | Required for modern .NET applications |
| **Discovery (WaitForReader)** | 🟡 High | Solves "lost first message" problem |
| **Content Filtering** | 🟡 High | Bandwidth/CPU optimization for distributed systems |
| **Instance Management** | 🟢 Medium | Required for keyed topics, O(1) lookups |

### 3.2 Dependency Graph

```
Foundation (Existing):
  ├─ Serdata Zero-Copy Core
  ├─ DdsWriter<T>
  └─ DdsReader<T, TView>

Stage 3.75 (This Document):
  ├─ Feature 1: Read/Take + Masks ────────► Required by all other features
  ├─ Feature 2: Async/Await ──────────────► Independent, uses listeners
  ├─ Feature 3: Content Filtering ───────► Simple: just ViewScope enumerator
  ├─ Feature 4: Status/Discovery ────────► Uses async infrastructure
  └─ Feature 5: Instance Management ─────► Uses Read/Take infrastructure

Stage 4-Deferred:
  └─ XCDR2 Compliance & Evolution

Stage 5:
  └─ Production Readiness (Benchmarks, NuGet, Docs)
```

---

## 4. Feature 0: Type Auto-Discovery & Topic Management

### 4.1 Conceptual Model

**Challenge:**
Raw DDS requires manually creating Topic Descriptors, registering them, and managing Topic entity lifecycles. This boilerplate (getting `_ops` pointers, marshalling descriptors) creates friction for C# developers and breaks the "idiomatic C#" goal.

**Current State (Boilerplate):**
```csharp
// User must manually create and pass descriptors
var descriptor = DescriptorContainer.CreateFor<SensorData>();
var writer = new DdsWriter<SensorData>(participant, topicName, descriptor);
```

**Desired State (Elegant):**
```csharp
// Type information automatically discovered from T
var writer = new DdsWriter<SensorData>(participant, "Sensors");
```

**Solution:**
The Runtime automatically discovers type metadata from the `T` generic argument using the code-generated static methods (`GetDescriptorOps`, `GetTypeName`). It also manages topic lifecycles within the `DdsParticipant`, ensuring a specific Topic name is only created once per domain.

### 4.2 API Design

**DdsParticipant (Topic Factory):**
```csharp
public sealed class DdsParticipant : IDisposable
{
    // Internal cache: TopicName -> Native Handle
    private readonly Dictionary<string, IntPtr> _topicCache = new();
    
    /// <summary>
    /// Gets an existing topic or registers a new one automatically based on T.
    /// Thread-safe. Calls dds_create_topic only once per topic name.
    /// </summary>
    internal IntPtr GetOrRegisterTopic<T>(string topicName, DdsQos? manualQos = null);
}
```

**DdsWriter / DdsReader Constructors (Simplified):**
```csharp
// No TopicDescriptor parameter needed!
public DdsWriter(DdsParticipant participant, string topicName, DdsQos? qos = null)
{
    // Internally calls participant.GetOrRegisterTopic<T>(topicName, qos)
    var topicHandle = participant.GetOrRegisterTopic<T>(topicName, qos);
    _writerHandle = DdsApi.dds_create_writer(participant.Handle, topicHandle, qos, null);
}

public DdsReader(DdsParticipant participant, string topicName, DdsQos? qos = null)
{
    var topicHandle = participant.GetOrRegisterTopic<T>(topicName, qos);
    _readerHandle = DdsApi.dds_create_reader(participant.Handle, topicHandle, qos, null);
}
```

### 4.3 Implementation Strategy

**1. Metadata Extraction (Reflection):**
Since `GetDescriptorOps()` is a static method on the generated struct, we can't access it via an interface on `T`.

**Mechanism:**
```csharp
internal static class DdsTypeSupport
{
    private static readonly ConcurrentDictionary<Type, Func<uint[]>> _opsCache = new();
    private static readonly ConcurrentDictionary<Type, DdsQosAttribute?> _qosCache = new();

    public static uint[] GetDescriptorOps<T>()
    {
        return _opsCache.GetOrAdd(typeof(T), type =>
        {
            // Reflection (one-time cost)
            var method = type.GetMethod("GetDescriptorOps", 
                BindingFlags.Static | BindingFlags.Public);
            
            if (method == null)
                throw new InvalidOperationException(
                    $"Type {typeof(T).Name} does not have GetDescriptorOps. " +
                    "Did you forget [DdsTopic] attribute?");
            
            return (Func<uint[]>)Delegate.CreateDelegate(typeof(Func<uint[]>), method);
        })();
    }

    public static DdsQosAttribute? GetDefaultQos<T>()
    {
        return _qosCache.GetOrAdd(typeof(T), type =>
            type.GetCustomAttribute<DdsQosAttribute>());
    }
}
```

**2. Topic Caching:**
Calling `dds_create_topic` twice for the same name is valid in DDS, but creates multiple entities that must be deleted.

**Optimization:**
```csharp
// Inside DdsParticipant
private readonly Dictionary<string, IntPtr> _topicCache = new();
private readonly object _topicLock = new();

internal IntPtr GetOrRegisterTopic<T>(string topicName, DdsQos? manualQos)
{
    lock (_topicLock)
    {
        // Return existing if already created
        if (_topicCache.TryGetValue(topicName, out var existing))
            return existing;

        // 1. Get descriptor ops from static method
        uint[] ops = DdsTypeSupport.GetDescriptorOps<T>();

        // 2. Get default QoS from attribute if not provided
        DdsQos qos = manualQos ?? DdsTypeSupport.GetDefaultQos<T>()?.ToQos() ?? new DdsQos();

        // 3. Create native topic descriptor
        IntPtr descriptorPtr = MarshalDescriptor(ops, typeof(T).Name);

        // 4. Create topic entity
        IntPtr topicHandle = DdsApi.dds_create_topic(
            _nativeHandle.Handle,
            topicName,
            descriptorPtr,
            qos,
            null);

        // 5. Cache and return
        _topicCache[topicName] = topicHandle;
        return topicHandle;
    }
}
```

**3. Lifecycle Management:**
When `DdsParticipant` disposes, it must delete all cached topics.

```csharp
public void Dispose()
{
    lock (_topicLock)
    {
        foreach (var topicHandle in _topicCache.Values)
        {
            DdsApi.dds_delete(topicHandle);
        }
        _topicCache.Clear();
    }
    
    // ... rest of disposal
}
```

### 4.4 Usage Examples

**Elegant (Auto-Magic):**
```csharp
// 1. Definition includes QoS defaults
[DdsTopic("SensorData")]
[DdsQos(Reliability = DdsReliability.Reliable)]
public partial struct SensorData 
{ 
    [DdsKey, DdsId(0)] public int SensorId;
    [DdsId(1)] public double Value;
}

// 2. Creation matches C# idiom - NO manual descriptor!
using var participant = new DdsParticipant();
using var writer = new DdsWriter<SensorData>(participant, "Sensors");
// -> Auto-detects Ops via GetDescriptorOps()
// -> Auto-detects QoS via [DdsQos] attribute
// -> Registers Topic once
```

**Overridden QoS:**
```csharp
// Override default QoS from attribute
var volatileQos = new DdsQos { Reliability = DdsReliability.BestEffort };
using var writer = new DdsWriter<SensorData>(participant, "Sensors", volatileQos);
```

**Multi-Writer Same Topic:**
```csharp
// Both writers share the same topic entity (cached)
using var writer1 = new DdsWriter<SensorData>(participant, "Sensors");
using var writer2 = new DdsWriter<SensorData>(participant, "Sensors");
// dds_create_topic called only ONCE internally
```

### 4.5 Testing Requirements

**Tests (Minimum 4):**
1. **TopicCache_SameName_ReturnsSameHandle**
   - Create two writers for "TopicA"
   - Success: Both use same topic handle (verified via internal cache)

2. **AutoDiscovery_ValidType_Succeeds**
   - Instantiate `DdsWriter<TestMessage>` without manual descriptor
   - Success: Writer created successfully, can send/receive data

3. **AutoDiscovery_InvalidType_Throws**
   - Instantiate `DdsWriter<int>` (primitive has no descriptor ops)
   - Success: Throws `InvalidOperationException` with helpful message

4. **Qos_AttributeApplied**
   - Define type with `[DdsQos(Reliability = Reliable)]`
   - Create writer without explicit QoS parameter
   - Success: Topic uses Reliable QoS (verify via native query)

**Validation:**
- ✅ No manual descriptor passing required
- ✅ Type safety enforced (can't mix types)
- ✅ Topic created only once per name
- ✅ QoS from attributes applied automatically

---

## 5. Feature 1: Read vs Take with Condition Masks

### 4.1 Conceptual Model

**DDS Semantics:**
- `Take`: Destructive read (removes data from cache)
- `Read`: Non-destructive read (leaves data for re-reading)

**DDS State Model:**
Each sample has 3 independent state dimensions:
1. **Sample State:** Read vs NotRead (have I accessed this sample before?)
2. **View State:** New vs NotNew (have I seen this instance before?)
3. **Instance State:** Alive, Disposed, or NoWriters (is the object still active?)

### 4.2 API Design

**State Enums** (`CycloneDDS.Runtime`):
```csharp
[Flags]
public enum DdsSampleState : uint
{
    Read = 0x0001,
    NotRead = 0x0002,
    Any = Read | NotRead
}

[Flags]
public enum DdsViewState : uint
{
    New = 0x0004,
    NotNew = 0x0008,
    Any = New | NotNew
}

[Flags]
public enum DdsInstanceState : uint
{
    Alive = 0x0010,
    NotAliveDisposed = 0x0020,
    NotAliveNoWriters = 0x0040,
    NotAlive = NotAliveDisposed | NotAliveNoWriters,
    Any = Alive | NotAlive
}
```

**DdsReader API:**
```csharp
public sealed class DdsReader<T, TView> : IDisposable where TView : struct
{
    // Simple Take (existing, preserved)
    public ViewScope<TView> Take(int maxSamples = 32);

    // Full Take with masks
    public ViewScope<TView> Take(
        int maxSamples,
        DdsSampleState sampleState,
        DdsViewState viewState = DdsViewState.Any,
        DdsInstanceState instanceState = DdsInstanceState.Any);

    // New: Non-destructive Read
    public ViewScope<TView> Read(
        int maxSamples = 32,
        DdsSampleState sampleState = DdsSampleState.Any,
        DdsViewState viewState = DdsViewState.Any,
        DdsInstanceState instanceState = DdsInstanceState.Any);
}
```

### 4.3 Implementation Strategy

**Refactoring Pattern:**
Extract common logic into `ReadOrTake(mask, operation)` to avoid duplication.

**Mask Calculation:**
```csharp
uint mask = (uint)sampleState | (uint)viewState | (uint)instanceState;
```

**P/Invoke:**
```csharp
[DllImport(DLL_NAME)]
public static extern int dds_readcdr(
    int reader,
    [In, Out] IntPtr[] samples,
    uint maxs,
    [In, Out] DdsSampleInfo[] infos,
    uint mask);
```

### 4.4 Usage Examples

**Common Pattern: "New Data Only"**
```csharp
// Process only samples we haven't seen yet
using var scope = reader.Take(32, DdsSampleState.NotRead);
foreach (var sample in scope)
{
    Process(sample);
}
```

**Monitoring Pattern: Non-Destructive Peek**
```csharp
// Inspector/debugger that doesn't disturb main consumer
using var scope = reader.Read(10, instanceState: DdsInstanceState.Alive);
// Data remains in cache after scope disposal
```

---

## 6. Feature 2: Async/Await Support

### 6.1 Conceptual Model

**Challenge:** Bridge DDS's blocking WaitSets or callbacks to .NET's Task-Based Asynchrony.

**Solution:** Use DDS Listeners (callbacks) to signal a `TaskCompletionSource<bool>`.

### 6.2 API Design

```csharp
public sealed class DdsReader<T, TView> : IDisposable
{
    // Core: Wait for data availability
    public ValueTask<bool> WaitDataAsync(CancellationToken ct = default);

    // Convenience: Async stream (allocates, copies data)
    public IAsyncEnumerable<T> StreamAsync(CancellationToken ct = default);
}
```

### 6.3 Implementation Strategy

**Lazy Listener Attachment:**
- Listener is NOT created until first `WaitDataAsync()` call
- Avoids overhead for polling-only users

**Native Callback Bridge:**
```csharp
// Static callback prevents GC issues
[MonoPInvokeCallback(typeof(DdsApi.DdsOnDataAvailable))]
private static void OnDataAvailableNative(IntPtr entity, IntPtr arg)
{
    var handle = GCHandle.FromIntPtr(arg);
    if (handle.Target is DdsReader<T, TView> reader)
    {
        reader._dataAvailableTcs?.TrySetResult(true);
    }
}
```

**GC Pinning:**
- Use `GCHandle.Alloc(this)` to pass reader instance to native callback
- Free in `Dispose()` to prevent memory leaks

### 6.4 Usage Examples

**High-Performance Async Loop:**
```csharp
Console.WriteLine("Waiting for data...");
while (await reader.WaitDataAsync())
{
    using var scope = reader.Take(); // Zero-Copy
    if (scope.Count == 0) continue; // Spurious wakeup

    foreach (var sample in scope)
    {
        await ProcessAsync(sample);
    }
}
```

**Convenience Stream Pattern (UI Apps):**
```csharp
await foreach (var message in reader.StreamAsync(cancellationToken))
{
    UpdateUI(message); // Allocates, but convenient
}
```

---

## 7. Feature 3: Content Filtering (Reader-Side Predicates)

### 7.1 Conceptual Model

**Challenge:** Filter high-frequency data streams to process only relevant samples, reducing CPU overhead.

**Design Decision:** Client-side filtering using C# lambda expressions on the `TView` struct.

**Why NOT SQL/DdsTopic:**
- SQL parsing has overhead and limited flexibility
- DdsTopic class adds unnecessary boilerplate for client-side filtering
- C# predicates leverage JIT optimization for zero-allocation filtering
- Simpler API: no topic abstraction needed

### 7.2 API Design

**DdsReader Enhancement:**
```csharp
public sealed class DdsReader<T, TView> : IDisposable where TView : struct
{
    /// <summary>
    /// Set a client-side filter predicate.
    /// Only samples passing the predicate will be visible in ViewScope iteration.
    /// Hot-swappable at runtime (thread-safe assignment).
    /// </summary>
    /// <param name="filter">Predicate acting on the view struct, or null to disable</param>
    public void SetFilter(Predicate<TView>? filter);
}
```

**ViewScope Enhancement:**
```csharp
public ref struct ViewScope<TView> where TView : struct
{
    private Predicate<TView>? _filter;

    // Enumerator skips samples that don't pass the filter
    public Enumerator GetEnumerator() => new Enumerator(this, _filter);

    public ref struct Enumerator
    {
        private Predicate<TView>? _filter;
        private int _currentIndex;

        public bool MoveNext()
        {
            while (++_currentIndex < _count)
            {
                // Only yield samples passing the filter
                if (_filter == null || _filter(_scope[_currentIndex]))
                    return true;
            }
            return false;
        }

        public ref readonly TView Current => ref _scope[_currentIndex];
    }
}
```

### 7.3 Implementation Strategy

**Zero Allocation:**
- `Predicate<TView>` is stored as a field (one reference assignment)
- Filter is checked during `MoveNext()` (no intermediate collections)
- JIT can inline simple predicates for near-zero overhead

**Thread Safety:**
- Filter assignment is atomic (reference write)
- No locks required on hot path
- Safe to call `SetFilter()` from any thread

### 7.4 Usage Examples

**Simple Filter:**
```csharp
var reader = new DdsReader<SensorData, SensorDataView>(participant, "Sensors");

// Elegant lambda on the view struct
reader.SetFilter(view => view.Temperature > 100.0 && view.Status == SensorStatus.Active);

// Iteration automatically skips filtered samples
using var scope = reader.Take();
foreach (var sample in scope)
{
    // Only high-temp active sensors here
    Process(sample);
}
```

**Dynamic Filter Update:**
```csharp
// Change filter at runtime (no reader recreation needed)
reader.SetFilter(view => view.Temperature > 80.0);

// Disable filtering
reader.SetFilter(null);
```

**Complex Filter:**
```csharp
// Multi-field logic with local variables
int threshold = GetCurrentThreshold();
reader.SetFilter(view =>
{
    if (view.Status != SensorStatus.Active)
        return false;

    return view.Temperature > threshold ||
           view.Pressure < 10.0;
});
```

### 7.5 Performance Characteristics

| Scenario | Cost |
|----------|------|
| **No Filter** | Zero overhead (null check) |
| **Simple Filter** | ~1-2 CPU cycles (JIT inline) |
| **Complex Filter** | Delegate invocation + predicate logic |
| **Filter Update** | Atomic reference write (~1ns) |

**Comparison to SQL:**

| Approach | Setup | Runtime | Flexibility |
|----------|-------|---------|-------------|
| **SQL String** | String parsing | Interpreted evaluation | Limited operators |
| **C# Lambda** | **JIT compile** | **Native code** | **Full C# expressiveness** |

### 7.6 Testing Requirements

**Tests (Minimum 3):**
1. **Filter_Applied_OnlyMatchingSamples**
   - Write samples with values 1, 5, 10
   - Set filter `view.Value > 3`
   - Success: Only 5, 10 iterated

2. **Filter_UpdatedAtRuntime_NewFilterApplied**
   - Initial filter: `view.Value > 5`
   - Take samples (verify filtered)
   - Update filter: `view.Value < 8`
   - Take again (verify new filter)

3. **Filter_Null_AllSamplesReturned**
   - Set filter to `view.Value > 100`
   - Set filter to `null`
   - Success: All samples visible

---

## 8. Feature 4: Status & Discovery

### 8.1 Conceptual Model

**Need:** Know when readers/writers connect/disconnect, detect liveliness loss, handle QoS violations.

**Solution:** Map DDS status callbacks to C# `event EventHandler<TStatus>`.

### 8.2 API Design

**Status Structs:**
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct DdsPublicationMatchedStatus
{
    public int TotalCount;
    public int TotalCountChange;
    public int CurrentCount;         // How many readers now?
    public int CurrentCountChange;   // Delta since last event
    public long LastSubscriptionHandle;
}

[StructLayout(LayoutKind.Sequential)]
public struct DdsSubscriptionMatchedStatus
{
    public int TotalCount;
    public int TotalCountChange;
    public int CurrentCount;         // How many writers now?
    public int CurrentCountChange;
    public long LastPublicationHandle;
}
```

**DdsWriter API:**
```csharp
public sealed class DdsWriter<T> : IDisposable
{
    // Synchronous property (polling)
    public DdsPublicationMatchedStatus PublicationMatchedStatus { get; }

    // Event (low-frequency status changes)
    public event EventHandler<DdsPublicationMatchedStatus> PublicationMatched;

    // Async wait for discovery
    public Task WaitForReaderAsync(TimeSpan timeout);
}
```

**DdsReader API:**
```csharp
public sealed class DdsReader<T, TView> : IDisposable
{
    public DdsSubscriptionMatchedStatus SubscriptionMatchedStatus { get; }
    public event EventHandler<DdsSubscriptionMatchedStatus> SubscriptionMatched;
    public event EventHandler<DdsLivelinessChangedStatus> LivelinessChanged;
}
```

### 8.3 Implementation Strategy

**Event Keyword Benefits:**
- Prevents accidental overwrite (`= null`)
- Prevents external invocation
- Supports interface declarations
- Standard .NET idiom

**Lazy Listener Pattern:**
Only attach native listener when event is subscribed to (via `add` accessor).

### 8.4 Usage Examples

**Reliable Startup (Avoid "Lost First Message"):**
```csharp
var writer = new DdsWriter<Message>(participant, "Chat");

Console.WriteLine("Waiting for subscriber...");
await writer.WaitForReaderAsync(TimeSpan.FromSeconds(10));

Console.WriteLine("Subscriber found! Sending hello.");
writer.Write(new Message { Text = "Hello" });
```

**Connectivity Monitoring:**
```csharp
writer.PublicationMatched += (sender, status) =>
{
    if (status.CurrentCountChange > 0)
        Console.WriteLine($"New reader connected! Total: {status.CurrentCount}");
    else
        Console.WriteLine($"Reader lost. Remaining: {status.CurrentCount}");
};
```

---

## 9. Feature 5: Instance Management (Keyed Topics)

### 9.1 Conceptual Model

**Challenge:** For keyed topics (objects with unique IDs), efficiently access specific instance history without iterating all data.

**Solution:** DDS InstanceHandle (64-bit integer representing key hash) enables O(1) lookups.

### 9.2 API Design

**DdsInstanceHandle (Strong Type):**
```csharp
[StructLayout(LayoutKind.Sequential)]
public readonly struct DdsInstanceHandle : IEquatable<DdsInstanceHandle>
{
    public readonly long Value;
    public static readonly DdsInstanceHandle Nil = new DdsInstanceHandle(0);
    public bool IsNil => Value == 0;
    
    public bool Equals(DdsInstanceHandle other) => Value == other.Value;
    public override string ToString() => $"Handle(0x{Value:X})";
}
```

**DdsReader API:**
```csharp
public sealed class DdsReader<T, TView> : IDisposable
{
    // Convert key fields to handle
    public DdsInstanceHandle LookupInstance(in T keySample);

    // Take/Read specific instance only
    public ViewScope<TView> TakeInstance(DdsInstanceHandle handle, int maxSamples = 32);
    public ViewScope<TView> ReadInstance(DdsInstanceHandle handle, int maxSamples = 32);
}
```

**DdsWriter API (Optional):**
```csharp
public sealed class DdsWriter<T> : IDisposable
{
    // Pre-register instance for QoS/performance
    public DdsInstanceHandle RegisterInstance(in T keySample);
}
```

### 9.3 Implementation Strategy

**Key Hashing:**
Reuse serialization infrastructure to create temporary serdata from key sample, ensuring hash consistency with writers.

**P/Invoke:**
```csharp
[DllImport(DLL_NAME)]
public static extern long dds_lookup_instance(IntPtr reader, IntPtr serdata);

[DllImport(DLL_NAME)]
public static extern int dds_take_instance(
    IntPtr reader,
    [In, Out] IntPtr[] samples,
    [In, Out] DdsSampleInfo[] infos,
    uint max_samples,
    long handle);
```

### 9.4 Usage Examples

**Tracked Object Pattern (GUI):**
```csharp
// User selects "Robot 5" from list
var key = new RobotStatus { Id = 5 };
var handle = reader.LookupInstance(key);

if (handle.IsNil)
{
    Console.WriteLine("Robot 5 never seen!");
    return;
}

// Get history for ONLY Robot 5 (O(1), ignores other robots)
using var history = reader.ReadInstance(handle, maxSamples: 100);
foreach (var status in history)
{
    PlotGraph(status.Timestamp, status.BatteryLevel);
}
```

**Instance Lifecycle Monitoring:**
```csharp
var key = new ServerInfo { Hostname = "CriticalDB" };
var handle = reader.LookupInstance(key);

using var scope = reader.ReadInstance(handle, 1);
if (scope.Count > 0)
{
    var state = scope.Infos[0].InstanceState;
    if (state.HasFlag(DdsInstanceState.NotAliveNoWriters))
    {
        Alert("CriticalDB publisher disconnected!");
    }
}
```

---

## 10. Testing Strategy

### 10.1 Testing Philosophy

**Quality Over Quantity:**
- Focus on tests that verify correct behavior, edge cases, and integration
- Each test must have clear success/failure conditions
- Tests should be fast and deterministic

### 10.2 Test Coverage Requirements

**Feature 1: Read/Take + Masks**
- Verify `Read()` is non-destructive (call twice, get same data)
- Verify `Take()` is destructive (second call returns empty)
- Verify `DdsSampleState.NotRead` filters correctly
- Verify mask combination (e.g., `NotRead & Alive`)

**Feature 2: Async/Await**
- Verify `WaitDataAsync()` completes when data arrives
- Verify cancellation works correctly
- Verify no listener overhead when only polling
- Verify no handle/memory leaks on create/dispose cycles

**Feature 3: Content Filtering**
- Write samples with values 1, 5, 10
- Set filter `view.Value > 3`
- Verify only 5, 10 iterated
- Verify filter can be updated at runtime

**Feature 4: Status/Discovery**
- Verify `PublicationMatched` event fires when reader appears
- Verify `WaitForReaderAsync()` completes on discovery
- Verify event fires when reader disposes
- Verify `CurrentCount` accuracy

**Feature 5: Instance Management**
- Write instances with `Id=1` and `Id=2`
- Lookup `Id=1`, get handle
- `TakeInstance(handle1)` returns only `Id=1` data
- Verify `Id=2` data remains in reader cache

---

## 11. Migration & Compatibility

### 11.1 Backward Compatibility

**Preserved:**
- Existing `DdsReader.Take()` continues to work with same signature
- Existing `DdsWriter.Write()` unchanged
- Zero-Copy `ViewScope<T>` infrastructure unchanged

**New:**
- All new APIs are additive (overloads, new methods)
- No breaking changes to existing code

### 11.2 Migration Path

**Users can adopt incrementally:**
1. Start with basic `Take()` (existing)
2. Add state masks when needed
3. Adopt `async/await` for new code
4. Use content filtering for performance optimization
5. Add discovery/status monitoring for production robustness

---

## 12. Implementation Roadmap

### 12.1 Sequencing

**Phase 0 (Foundation - Must Come First):**
- Task FCDC-EXT00: Type Auto-Discovery & Topic Management

**Phase 1 (Parallel - Depends on EXT00):**
- Task FCDC-EXT01: Read/Take + Masks
- Task FCDC-EXT02: Async/Await

**Phase 2 (Depends on Phase 1):** Advanced
- Task FCDC-EXT03: Content Filtering (simple ViewScope update)
- Task FCDC-EXT04: Status/Discovery (uses async infrastructure)

**Phase 3 (Independent):** Keyed Topics
- Task FCDC-EXT05: Instance Management (uses Read/Take infrastructure)

### 12.2 Estimated Effort

| Task | Estimated Days | Risk |
|------|----------------|------|
| FCDC-EXT00 | 2-3 days | Medium (reflection, caching, lifecycle) |
| FCDC-EXT01 | 2-3 days | Low (P/Invoke + refactor) |
| FCDC-EXT02 | 3-4 days | Medium (GC pinning, callbacks) |
| FCDC-EXT03 | 1-2 days | Low (ViewScope enumerator update) |
| FCDC-EXT04 | 2-3 days | Low (reuses async patterns) |
| FCDC-EXT05 | 2-3 days | Low (reuses serialization) |
| **Total (Core Extended API)** | **12-18 days** | **2.5-3.5 weeks** |

---

## 13. Success Criteria

**Functional:**
- ✅ All new APIs work correctly with existing zero-copy core
- ✅ All tests pass (minimum 15 tests total, 3 per feature)
- ✅ No memory leaks (handle cleanup verified)
- ✅ No breaking changes to existing APIs

**Performance:**
- ✅ Zero-Copy path still allocation-free
- ✅ Async overhead only when listener used
- ✅ Content filtering reduces network traffic (measurable)

**Usability:**
- ✅ APIs feel natural to C# developers
- ✅ IntelliSense provides correct guidance
- ✅ Common patterns require minimal code

---

**Document Prepared By:** Development Lead  
**Date:** 2026-01-18  
**Status:** Approved for Task Creation  
**Next Step:** Create tasks in SERDATA-TASK-MASTER.md
