# Sender Tracking Feature Design

**Version:** 1.0  
**Date:** 2026-01-18  
**Status:** Approved for Implementation  
**Priority:** MEDIUM

---

## 1. Overview

**Goal:** Provide optional, zero-overhead sender identification for received samples. Each sample can be attributed to a specific application instance with full process-level details.

**Use Cases:**
- Multi-instance debugging (which process sent this data?)
- Security audit trails (who is publishing data?)
- Performance monitoring (track data source latency)
- Multi-tenant systems (isolate data by sender)

**Design Philosophy:**
- **Opt-in:** Zero overhead when disabled
- **Zero-Copy:** O(1) dictionary lookups, no allocations per sample
- **Automatic:** No manual bookkeeping required by user
- **Thread-Safe:** Works in multi-threaded environments

---

## 2. Architecture: "Identity Registry" Pattern

### 2.1 Conceptual Model

**The Problem:**
DDS `DdsSampleInfo` contains a `PublicationHandle` (local integer), but no application-level metadata (computer name, process ID, custom IDs).

**The Solution:**
Two-phase discovery system:
1. **Identity Topic:** Special topic `__FcdcSenderIdentity` where participants announce themselves
2. **Handshake:** Map DDS handles to application identities via GUID correlation

**Data Flow:**
```
Sender Process                    Network                   Receiver Process
──────────────                    ───────                   ────────────────
[DdsParticipant]                                           [DdsParticipant]
      │                                                           │
      ├─ EnableSenderTracking(config) ───────────────────────────┤
      │                                                           │
      ├─ Create DdsWriter<Msg>                                   │
      │       │                                                   │
      │       └─► Publish SenderIdentity      ────────────────►  │
      │          (AppId, ProcessId, etc.)                         │
      │                                                           ├─ SenderRegistry
      │                                                           │   └─ Cache identity
      ├─ Write(msg)                                              │
      │                                                           │
      │     msg + PublicationHandle        ────────────────►     ├─ DdsReader.Take()
      │                                                           │        │
      │                                                           │        └─► GetSender(index)
      │                                                           │               └─> O(1) lookup
      │                                                           │                   return Identity
```

---

## 3. Data Schema

### 3.1 SenderIdentity Structure

```csharp
namespace CycloneDDS.Runtime.Tracking
{
    /// <summary>
    /// Application-level identity broadcast by each participant.
    /// Used to correlate native DDS handles to user metadata.
    /// </summary>
    [DdsTopic("__FcdcSenderIdentity")]
    [DdsQos(Reliability = DdsReliability.Reliable, Durability = DdsDurability.TransientLocal)]
    public partial struct SenderIdentity
    {
        /// <summary>
        /// Native DDS Participant GUID (16 bytes) - used as correlation key.
        /// </summary>
        [DdsKey, DdsId(0)]
        public DdsGuid ParticipantGuid;

        /// <summary>
        /// User-defined application domain identifier.
        /// </summary>
        [DdsId(1)]
        public int AppDomainId;

        /// <summary>
        /// User-defined application instance identifier.
        /// </summary>
        [DdsId(2)]
        public int AppInstanceId;

        /// <summary>
        /// Machine name where process is running.
        /// </summary>
        [DdsManaged, DdsId(3)]
        public string ComputerName;

        /// <summary>
        /// Process executable name.
        /// </summary>
        [DdsManaged, DdsId(4)]
        public string ProcessName;

        /// <summary>
        /// OS Process ID (disambiguates multiple instances of same exe).
        /// </summary>
        [DdsId(5)]
        public int ProcessId;
    }
}
```

### 3.2 DdsGuid Structure

```csharp
/// <summary>
/// Represents a 16-byte DDS GUID (Globally Unique Identifier).
/// Used for high-performance correlation without string comparisons.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct DdsGuid : IEquatable<DdsGuid>
{
    /// <summary>
    /// High 64 bits (Prefix: first 8 bytes).
    /// </summary>
    public long High;

    /// <summary>
    /// Low 64 bits (Prefix last 4 bytes + Entity ID 4 bytes).
    /// </summary>
    public long Low;

    public bool Equals(DdsGuid other) => High == other.High && Low == other.Low;
    public override int GetHashCode() => HashCode.Combine(High, Low);
    public override string ToString() => $"{High:X16}{Low:X16}";
}
```

---

## 4. Configuration API

### 4.1 SenderIdentityConfig

```csharp
namespace CycloneDDS.Runtime.Tracking
{
    /// <summary>
    /// Configuration for sender tracking feature.
    /// If this is not provided to DdsParticipant, feature is disabled (zero overhead).
    /// </summary>
    public record SenderIdentityConfig
    {
        /// <summary>
        /// Application domain identifier (user-defined grouping).
        /// </summary>
        public int AppDomainId { get; init; }

        /// <summary>
        /// Application instance identifier (user-defined instance ID).
        /// </summary>
        public int AppInstanceId { get; init; }

        /// <summary>
        /// Optional override for process name (defaults to Process.ProcessName).
        /// </summary>
        public string? ProcessName { get; init; }

        /// <summary>
        /// Optional override for computer name (defaults to Environment.MachineName).
        /// </summary>
        public string? ComputerName { get; init; }

        /// <summary>
        /// If true (default), identity is kept alive until Participant disposal.
        /// If false, identity is disposed when last Writer is disposed.
        /// RECOMMENDATION: Keep true to avoid race conditions.
        /// </summary>
        public bool KeepAliveUntilParticipantDispose { get; init; } = true;
    }
}
```

### 4.2 DdsParticipant API

```csharp
public sealed class DdsParticipant : IDisposable
{
    /// <summary>
    /// Enable sender tracking for this participant.
    /// MUST be called before creating any DdsWriter or DdsReader.
    /// </summary>
    /// <param name="config">Configuration with AppDomainId, AppInstanceId</param>
    /// <exception cref="InvalidOperationException">If writers already created</exception>
    public void EnableSenderTracking(SenderIdentityConfig config);

    // Internal hooks (called by DdsWriter constructor/dispose)
    internal void RegisterWriter();
    internal void UnregisterWriter();
}
```

---

## 5. SenderRegistry Implementation

### 5.1 Core Registry

```csharp
namespace CycloneDDS.Runtime.Tracking
{
    /// <summary>
    /// Central registry that correlates DDS publication handles to application identities.
    /// Singleton per DdsParticipant.
    /// </summary>
    public sealed class SenderRegistry : IDisposable
    {
        // Identity cache: GUID -> Identity (populated from Identity Topic)
        private readonly ConcurrentDictionary<DdsGuid, SenderIdentity> _guidToIdentity = new();

        // Fast lookup: PublicationHandle -> Identity (O(1))
        private readonly ConcurrentDictionary<long, SenderIdentity> _handleToIdentity = new();

        // Background reader for identity topic
        private readonly DdsReader<SenderIdentity, SenderIdentityView> _identityReader;
        private readonly CancellationTokenSource _cancellation = new();

        internal SenderRegistry(DdsParticipant participant)
        {
            // Subscribe to identity announcements
            _identityReader = new DdsReader<SenderIdentity, SenderIdentityView>(
                participant, "__FcdcSenderIdentity");

            // Start async monitoring
            _ = MonitorIdentitiesAsync();
        }

        /// <summary>
        /// Background task: Updates _guidToIdentity as remote participants announce themselves.
        /// </summary>
        private async Task MonitorIdentitiesAsync()
        {
            try
            {
                while (await _identityReader.WaitDataAsync(_cancellation.Token))
                {
                    using var scope = _identityReader.Take();
                    foreach (var view in scope)
                    {
                        // Cache identity by GUID
                        var identity = view.ToOwned();
                        _guidToIdentity[identity.ParticipantGuid] = identity;
                    }
                }
            }
            catch (OperationCanceledException) { /* Expected on dispose */ }
        }

        /// <summary>
        /// Called when DdsReader detects a new remote writer.
        /// Maps PublicationHandle -> ParticipantGuid -> Identity.
        /// </summary>
        public void RegisterRemoteWriter(long publicationHandle, DdsGuid writerGuid)
        {
            // Extract participant GUID from writer GUID
            // (Writer GUID = Participant Prefix (12 bytes) + Entity ID (4 bytes))
            var participantGuid = ExtractParticipantGuid(writerGuid);

            if (_guidToIdentity.TryGetValue(participantGuid, out var identity))
            {
                _handleToIdentity[publicationHandle] = identity;
            }
            // If identity not found yet, it may arrive later (race condition)
            // Lazy resolution will handle this
        }

        /// <summary>
        /// Fast O(1) lookup for UI/processing code.
        /// </summary>
        public bool TryGetIdentity(long publicationHandle, out SenderIdentity identity)
        {
            if (_handleToIdentity.TryGetValue(publicationHandle, out identity))
                return true;

            // Lazy fallback: Identity topic might have arrived after data connection
            // Try resolving now (slower path, only happens once per handle)
            return TryResolveLazy(publicationHandle, out identity);
        }

        private bool TryResolveLazy(long publicationHandle, out SenderIdentity identity)
        {
            // Implementation requires querying DDS for writer GUID given handle
            // Then checking _guidToIdentity again
            // (Details omitted for brevity)
            identity = default;
            return false;
        }

        private DdsGuid ExtractParticipantGuid(DdsGuid writerGuid)
        {
            // DDS GUID structure: [Prefix: 12 bytes][EntityId: 4 bytes]
            // Participant GUID uses same prefix, EntityId = 0x01C1
            // For simplicity, we can use the full writer GUID and rely on Cyclone API
            // OR we can mask the EntityId portion
            return writerGuid; // Simplified - actual implementation may vary
        }

        public void Dispose()
        {
            _cancellation.Cancel();
            _identityReader?.Dispose();
            _cancellation.Dispose();
        }
    }
}
```

---

## 6. Integration with DdsReader

### 6.1 DdsReader Enhancements

```csharp
public sealed class DdsReader<T, TView> : IDisposable where TView : struct
{
    private SenderRegistry? _registry;

    /// <summary>
    /// Enable sender tracking for this reader.
    /// After this, ViewScope.GetSender(index) will return sender information.
    /// </summary>
    public void EnableSenderTracking(SenderRegistry registry)
    {
        _registry = registry;
        this.SubscriptionMatched += OnSubscriptionMatched;
    }

    private void OnSubscriptionMatched(object sender, DdsSubscriptionMatchedStatus e)
    {
        if (e.CurrentCountChange > 0 && _registry != null)
        {
            // New writer(s) connected - register them
            long[] handles = GetMatchedPublicationHandles();

            foreach (var handle in handles)
            {
                var writerGuid = GetMatchedPublicationGuid(handle);
                _registry.RegisterRemoteWriter(handle, writerGuid);
            }
        }
    }

    // Helper methods using new P/Invokes
    private long[] GetMatchedPublicationHandles() { /* Implementation */ }
    private DdsGuid GetMatchedPublicationGuid(long handle) { /* Implementation */ }
}
```

### 6.2 ViewScope Enhancements

```csharp
public ref struct ViewScope<TView> where TView : struct
{
    private SenderRegistry? _registry;

    /// <summary>
    /// Get sender information for sample at specified index.
    /// Returns null if tracking is not enabled or sender is unknown.
    /// O(1) operation.
    /// </summary>
    public SenderIdentity?  GetSender(int index)
    {
        if (_registry == null || index < 0 || index >= Count)
            return null;

        long pubHandle = _infos[index].PublicationHandle;

        if (_registry.TryGetIdentity(pubHandle, out var identity))
        {
            return identity;
        }

        return null; // Sender not registered yet (rare)
    }
}
```

---

## 7. Sender Side: Automatic Publishing

### 7.1 DdsWriter Lifecycle Hooks

```csharp
public sealed class DdsWriter<T> : IDisposable
{
    private readonly DdsParticipant _participant;

    public DdsWriter(DdsParticipant participant, string topicName)
    {
        _participant = participant;
        // ... creation logic ...

        // Notify participant (triggers identity publishing if enabled)
        // Skip for the identity writer itself to avoid recursion
        if (typeof(T) != typeof(SenderIdentity))
        {
            _participant.RegisterWriter();
        }
    }

    public void Dispose()
    {
        // ... cleanup ...

        if (typeof(T) != typeof(SenderIdentity))
        {
            _participant.UnregisterWriter();
        }
    }
}
```

### 7.2 DdsParticipant Lifecycle Management

```csharp
// Inside DdsParticipant
private SenderIdentityConfig? _identityConfig;
private DdsWriter<SenderIdentity>? _identityWriter;
private int _activeWriterCount = 0;
private readonly object _trackingLock = new();

internal void RegisterWriter()
{
    if (_identityConfig == null) return; // Feature disabled

    lock (_trackingLock)
    {
        _activeWriterCount++;
        if (_activeWriterCount == 1)
        {
            StartPublishingIdentity();
        }
    }
}

private void StartPublishingIdentity()
{
    var process = Process.GetCurrentProcess();

    // Get native participant GUID
    DdsApi.dds_get_guid(_nativeHandle.Handle, out var myGuid);

    var identity = new SenderIdentity
    {
        ParticipantGuid = myGuid,
        AppDomainId = _identityConfig!.AppDomainId,
        AppInstanceId = _identityConfig.AppInstanceId,
        ProcessId = process.Id,
        ProcessName = _identityConfig.ProcessName ?? process.ProcessName,
        ComputerName = _identityConfig.ComputerName ?? Environment.MachineName
    };

    // Create identity writer with TransientLocal
    _identityWriter = new DdsWriter<SenderIdentity>(this, "__FcdcSenderIdentity");
    _identityWriter.Write(identity);
}
```

---

## 8. Required P/Invokes

### 8.1 Participant GUID Retrieval

```csharp
/// <summary>
/// Get the GUID of a DDS entity (participant, reader, writer).
/// </summary>
[DllImport(DLL_NAME)]
public static extern int dds_get_guid(int entity, out DdsGuid guid);
```

### 8.2 Matched Publications Query

```csharp
/// <summary>
/// Get list of publication handles currently matched to a reader.
/// </summary>
[DllImport(DLL_NAME)]
public static extern int dds_get_matched_publications(
    int reader,
    [In, Out] long[] publication_handles,
    uint max_handles);
```

### 8.3 Publication Metadata Retrieval

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct DdsPublicationMatchedData
{
    public int topic_handle;
    public IntPtr topic_name;  // char* - must free
    public IntPtr type_name;   // char* - must free
    public IntPtr qos;         // dds_qos_t* -must free
    public DdsGuid guid;       // The actual writer GUID
}

/// <summary>
/// Get detailed information about a matched publication.
/// IMPORTANT: Caller must free allocated strings in DdsPublicationMatchedData.
/// </summary>
[DllImport(DLL_NAME)]
public static extern int dds_get_matched_publication_data(
    int reader,
    long publication_handle,
    out DdsPublicationMatchedData data);

/// <summary>
/// Free memory allocated by Cyclone DDS.
/// </summary>
[DllImport(DLL_NAME)]
public static extern void dds_free(IntPtr ptr);
```

---

## 9. Usage Examples

### 9.1 Sender Application

```csharp
// Application startup
var participant = new DdsParticipant();

// Enable tracking with custom IDs
participant.EnableSenderTracking(new SenderIdentityConfig
{
    AppDomainId = 100,
    AppInstanceId = 1
});

// Create writers (identity automatically published)
using var writer = new DdsWriter<SensorData>(participant, "Sensors");
writer.Write(new SensorData { Temperature = 25.0 });
// Identity remains published...

// Dispose writer (identity still alive until participant disposed)
```

### 9.2 Receiver Application

```csharp
var participant = new DdsParticipant();

// Enable tracking (starts listening to identity topic)
participant.EnableSenderTracking(new SenderIdentityConfig
{
    AppDomainId = 200,
    AppInstanceId = 1
});

// Create reader with tracking enabled
var reader = new DdsReader<SensorData>(participant, "Sensors");
reader.EnableSenderTracking(participant.SenderRegistry);

// Process with sender information
while (await reader.WaitDataAsync())
{
    using var scope = reader.Take();
    for (int i = 0; i < scope.Count; i++)
    {
        var data = scope[i];
        var sender = scope.GetSender(i); // O(1) lookup

        if (sender != null)
        {
            Console.WriteLine($"Data from {sender.ComputerName} " +
                            $"(AppId: {sender.AppDomainId}.{sender.AppInstanceId}, " +
                            $"PID: {sender.ProcessId})");
        }

        ProcessSample(data);
    }
}
```

---

## 10. Performance Characteristics

| Operation | Cost | Allocation |
|-----------|------|------------|
| **EnableSenderTracking()** | One-time setup | Creates reader + dictionaries |
| **RegisterWriter()** | Lock + dictionary insert | One SenderIdentity write |
| **MonitorIdentitiesAsync()** | Background task | Zero (ViewScope is ref struct) |
| **RegisterRemoteWriter()** | Dictionary insert | Zero |
| **GetSender(index)** | **O(1) dictionary lookup** | **Zero** |

**Disabled Overhead:** If `EnableSenderTracking()` not called: **Zero** - all checks short-circuit immediately.

---

## 11. Thread Safety

- **ConcurrentDictionary:** Used for all lookups (lock-free reads)
- **Lock on Writer Count:** Only during rare events (writer creation/disposal)
- **Background Task:** Runs independently, no blocking on hot path

---

## 12. Race Condition Handling

**Scenario:** Identity topic arrival may lag behind data connection.

**Solutions:**
1. **TransientLocal QoS:** Late joiners get historical identity data
2. **Lazy Resolution:** `GetSender()` retries lookup if initially failed
3. **Keep Alive Flag:** Identity persists until participant disposal (avoids "goodbye" race)

---

## 13. Testing Strategy

### 13.1 Unit Tests (Minimum 5)

1. **Identity Publishing:** Verify SenderIdentity is published when first writer created
2. **Identity Caching:** Verify remote identities are cached in registry
3. **Handle Correlation:** Verify publication handle maps to correct identity
4. **GetSender O(1):** Benchmark lookup performance (<10ns)
5. **Disabled Overhead:** Verify zero overhead when feature not enabled

### 13.2 Integration Tests (Minimum 3)

1. **Multi-Process:** Two processes, verify sender info received correctly
2. **Late Joiner:** Start sender first, then receiver - verify TransientLocal works
3. **Multi-Instance:** Same AppDomainId, different ProcessId - verify disambiguation

---

## 14. Implementation Tasks

See `SERDATA-TASK-MASTER.md` for task breakdown:
- FCDC-EXT06: Sender Tracking Infrastructure
- FCDC-EXT07: Sender Tracking Integration

**Total Effort:** 4-5 days

---

**Document Status:** Approved for Implementation  
**Next Step:** Create tasks in task tracker
