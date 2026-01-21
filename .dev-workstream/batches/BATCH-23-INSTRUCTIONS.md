# BATCH-23: Sender Tracking (FCDC-EXT06, FCDC-EXT07)

**Developer Onboarding Document**  
**Version:** 1.0  
**Date:** 2026-01-21  
**Tasks:** FCDC-EXT06 (Infrastructure), FCDC-EXT07 (Integration)  
**Estimated Effort:** 3-5 days  
**Batch Report:** Submit to `.dev-workstream/reports/BATCH-23-REPORT.md`

---

## 1. Welcome & Context

Welcome! This batch implements **optional sender tracking** for DDS samples, allowing you to identify which application instance (computer, process, AppDomainId) sent each received sample. This is critical for debugging, security auditing, and multi-tenant systems.

**Your Mission:**
- Implement `SenderRegistry` infrastructure for tracking remote participant identities
- Integrate tracking into `DdsParticipant`, `DdsWriter`, `DdsReader`, and `ViewScope`
- Ensure **zero overhead** when feature is disabled
- All tests must pass (minimum 8 new tests)

**Key Design Principle:** **Opt-In, Zero-Overhead**  
If a user doesn't call `EnableSenderTracking()`, there should be ZERO performance impact—no listeners, no dictionaries, no allocations.

---

## 2. Workspace Orientation

### 2.1 Project Structure

```
D:\Work\FastCycloneDdsCsharpBindings\
├── Src\
│   └── CycloneDDS.Runtime\         # Main runtime library
│       ├── DdsParticipant.cs       # 📝 UPDATE: Add EnableSenderTracking()
│       ├── DdsWriter.cs            # 📝 UPDATE: Add RegisterWriter hooks
│       ├── DdsReader.cs            # 📝 UPDATE: Add EnableSenderTracking() & matched pubs
│       ├── Interop\
│       │   └── DdsApi.cs           # 📝 UPDATE: Add P/Invokes (dds_get_guid, etc.)
│       └── Tracking\               # 🆕 CREATE: New folder
│           ├── SenderIdentity.cs   # 🆕 CREATE: Identity struct
│           ├── SenderIdentityConfig.cs # 🆕 CREATE: Config record
│           └── SenderRegistry.cs   # 🆕 CREATE: Core tracking logic
├── tests\
│   └── CycloneDDS.Runtime.Tests\   # Test project
│       └── SenderTrackingTests.cs  # 🆕 CREATE: Minimum 8 tests
├── docs\
│   └── SENDER-TRACKING-DESIGN.md   # ⭐ PRIMARY DESIGN DOC (READ FIRST!)
└── .dev-workstream\
    └── reports\
        └── BATCH-23-REPORT.md      # 📝 SUBMIT: Your completion report
```

### 2.2 Build & Test Commands

```powershell
# Build entire solution
dotnet build D:\Work\FastCycloneDdsCsharpBindings\FastCycloneDdsCsharpBindings.sln

# Run tests
dotnet test D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj

# Run specific test
dotnet test --filter "FullyQualifiedName~SenderTrackingTests"
```

---

## 3. Required Reading (CRITICAL!)

### 3.1 Primary Design Document

📖 **`D:\Work\FastCycloneDdsCsharpBindings\docs\SENDER-TRACKING-DESIGN.md`**

This is your **PRIMARY reference**. Read the ENTIRE document before writing any code.

**Key Sections:**
- Section 2: Architecture ("Identity Registry" pattern)
- Section 3: Data Schema (`SenderIdentity`, `DdsGuid`)
- Section 4: Configuration API (`SenderIdentityConfig`)
- Section 5: `SenderRegistry` implementation
- Section 6: `DdsReader` integration
- Section 7: `DdsWriter` lifecycle hooks
- Section 8: Required P/Invokes
- Section 9: Usage examples

### 3.2 Supporting Documents

📖 **`D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md`** (Lines 1962-2054)
- FCDC-EXT06 task definition
- FCDC-EXT07 task definition
- Test requirements

📖 **`D:\Work\FastCycloneDdsCsharpBindings\docs\EXTENDED-DDS-API-DESIGN.md`**
- Context for async/await patterns (used by `SenderRegistry`)
- Event system (used for `SubscriptionMatched` hookup)

### 3.3 Existing Code to Study

**Study these existing implementations:**

1. **`Src/CycloneDDS.Runtime/DdsReader.cs`**:
   - Async/Await patterns (`WaitDataAsync`)
   - Event implementation (`SubscriptionMatched`)
   - Listener lifecycle management

2. **`Src/CycloneDDS.Runtime/DdsParticipant.cs`**:
   - Topic caching pattern (for `GetOrRegisterTopic`)
   - Resource lifecycle management

3. **`Src/CycloneDDS.Runtime/Interop/DdsApi.cs`**:
   - P/Invoke patterns (for new native calls)
   - Struct marshalling examples

---

## 4. Task Breakdown

### **FCDC-EXT06: Sender Tracking Infrastructure** (Priority: MEDIUM, 2-3 days)

Implement the core tracking infrastructure without touching existing runtime classes.

#### Task 1: Define Core Data Structures (30 min)

**File:** `Src/CycloneDDS.Runtime/Interop/DdsApi.cs`

**Add `DdsGuid` struct:**

```csharp
namespace CycloneDDS.Runtime.Interop
{
    /// <summary>
    /// Represents a 16-byte DDS GUID (Globally Unique Identifier).
    /// Maps to native dds_guid_t (uint8_t v[16]).
    /// Used for O(1) participant/writer correlation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DdsGuid : IEquatable<DdsGuid>
    {
        /// <summary>
        /// High 64 bits (first 8 bytes of v[16]).
        /// </summary>
        public long High;

        /// <summary>
        /// Low 64 bits (last 8 bytes of v[16]).
        /// </summary>
        public long Low;

        public bool Equals(DdsGuid other) => High == other.High && Low == other.Low;
        public override bool Equals(object? obj) => obj is DdsGuid other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(High, Low);
        public override string ToString() => $"{High:X16}{Low:X16}";

        public static bool operator ==(DdsGuid left, DdsGuid right) => left.Equals(right);
        public static bool operator !=(DdsGuid left, DdsGuid right) => !left.Equals(right);
    }
}
```

**IMPORTANT:** This struct MUST match the native `dds_guid_t` layout (check `Native API Analysis.md` for confirmation).

#### Task 2: Create SenderIdentity Struct (15 min)

**File:** `Src/CycloneDDS.Runtime/Tracking/SenderIdentity.cs`

Create the `Tracking` subfolder first:
```powershell
mkdir D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\Tracking
```

```csharp
using System;
using CycloneDDS.Runtime.Interop;
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tracking
{
    /// <summary>
    /// Application-level identity broadcast by each participant.
    /// Used to correlate DDS publication handles to user metadata.
    /// </summary>
    [DdsTopic("__FcdcSenderIdentity")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
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

**NOTE:** The `[DdsTopic]` attribute is a custom attribute (you'll need to check if this exists or create it). If it doesn't exist, just use the struct without it and rely on manual topic name passing.

#### Task 3: Create SenderIdentityConfig Record (10 min)

**File:** `Src/CycloneDDS.Runtime/Tracking/SenderIdentityConfig.cs`

```csharp
using System;

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

#### Task 4: Add P/Invokes to DdsApi (30 min)

**File:** `Src/CycloneDDS.Runtime/Interop/DdsApi.cs`

Add these P/Invoke declarations:

```csharp
/// <summary>
/// Get the GUID of a DDS entity (participant, reader, writer).
/// </summary>
/// <param name="entity">Entity handle</param>
/// <param name="guid">Output GUID</param>
/// <returns>0 on success, negative error code on failure</returns>
[DllImport(DLL_NAME)]
public static extern int dds_get_guid(int entity, out DdsGuid guid);

/// <summary>
/// Get list of publication handles currently matched to a reader.
/// </summary>
/// <param name="reader">Reader entity</param>
/// <param name="publication_handles">Output array of handles</param>
/// <param name="max_handles">Size of array</param>
/// <returns>Number of handles returned, or negative error code</returns>
[DllImport(DLL_NAME)]
public static extern int dds_get_matched_publications(
    int reader,
    [In, Out] long[] publication_handles,
    uint max_handles);

/// <summary>
/// Publication metadata returned by dds_get_matched_publication_data.
/// IMPORTANT: Caller must free allocated strings using dds_free().
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct DdsPublicationMatchedData
{
    public int TopicHandle;
    public IntPtr TopicName;    // char* - must free with dds_free()
    public IntPtr TypeName;     // char* - must free with dds_free()
    public IntPtr Qos;          // dds_qos_t* - must free with dds_delete_qos()
    public DdsGuid Guid;        // The actual writer GUID
}

/// <summary>
/// Get detailed information about a matched publication.
/// IMPORTANT: Caller must free allocated strings in DdsPublicationMatchedData.
/// </summary>
/// <param name="reader">Reader entity</param>
/// <param name="publication_handle">Handle of matched publication</param>
/// <param name="data">Output metadata</param>
/// <returns>0 on success, negative error code on failure</returns>
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

#### Task 5: Implement SenderRegistry (THE BIG ONE - 4-6 hours)

**File:** `Src/CycloneDDS.Runtime/Tracking/SenderRegistry.cs`

This is the core of the tracking system. Study the design doc Section 5 carefully.

**Key Requirements:**
1. **Background async monitoring** using `WaitDataAsync()` and `Take()`
2. **ConcurrentDictionary** for thread-safe lookups
3. **Zero allocation** on hot path (`TryGetIdentity()` must be allocation-free)
4. **Proper disposal** (cancel background task, dispose reader)

**Implementation Outline:**

```csharp
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime.Interop;

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
        private readonly DdsReader<SenderIdentity, SenderIdentity> _identityReader;
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _monitorTask;

        internal SenderRegistry(DdsParticipant participant)
        {
            // Subscribe to identity announcements
            _identityReader = new DdsReader<SenderIdentity, SenderIdentity>(
                participant, "__FcdcSenderIdentity");

            // Start async monitoring
            _monitorTask = MonitorIdentitiesAsync();
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
                    for (int i = 0; i < scope.Count; i++)
                    {
                        var identity = scope[i];
                        _guidToIdentity[identity.ParticipantGuid] = identity;
                    }
                }
            }
            catch (OperationCanceledException) 
            { 
                // Expected on dispose 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SenderRegistry] Monitor task failed: {ex}");
            }
        }

        /// <summary>
        /// Called when DdsReader detects a new remote writer.
        /// Maps PublicationHandle -> ParticipantGuid -> Identity.
        /// </summary>
        public void RegisterRemoteWriter(long publicationHandle, DdsGuid writerGuid)
        {
            // Extract participant GUID from writer GUID
            var participantGuid = ExtractParticipantGuid(writerGuid);

            if (_guidToIdentity.TryGetValue(participantGuid, out var identity))
            {
                _handleToIdentity[publicationHandle] = identity;
            }
            // If identity not found yet, it may arrive later (race condition)
            // Lazy resolution will handle this in TryGetIdentity
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
            // TODO: Query DDS for writer GUID, then check _guidToIdentity
            // For now, return false (identity not available yet)
            identity = default;
            return false;
        }

        private DdsGuid ExtractParticipantGuid(DdsGuid writerGuid)
        {
            // DDS GUID structure: [Prefix: 12 bytes][EntityId: 4 bytes]
            // Participant GUID uses same prefix, EntityId = 0x000001C1
            // For simplicity, we use the writer GUID directly for lookup
            // (Assuming each participant only publishes one identity)
            return writerGuid; // Simplified - may need refinement
        }

        public void Dispose()
        {
            _cancellation.Cancel();
            _identityReader?.Dispose();
            _cancellation.Dispose();
            
            // Wait for monitor task to complete (with timeout)
            try 
            { 
                _monitorTask?.Wait(TimeSpan.FromSeconds(1)); 
            }
            catch { /* Best effort */ }
        }
    }
}
```

**CRITICAL NOTES:**
- The `ExtractParticipantGuid` logic may need refinement based on Cyclone DDS internals
- `TryResolveLazy` is a placeholder - you may implement it later or leave as-is (rare edge case)
- Ensure proper exception handling in background task

---

### **FCDC-EXT07: Sender Tracking Integration** (Priority: MEDIUM, 1-2 days)

Integrate the tracking system into existing runtime classes.

#### Task 6: Update DdsParticipant (1-2 hours)

**File:** `Src/CycloneDDS.Runtime/DdsParticipant.cs`

**Add Fields:**
```csharp
private SenderIdentityConfig? _identityConfig;
private DdsWriter<SenderIdentity>? _identityWriter;
private int _activeWriterCount = 0;
private readonly object _trackingLock = new();
internal SenderRegistry? _senderRegistry;
```

**Add Public API:**
```csharp
/// <summary>
/// Enable sender tracking for this participant.
/// MUST be called before creating any DdsWriter or DdsReader.
/// </summary>
/// <param name="config">Configuration with AppDomainId, AppInstanceId</param>
/// <exception cref="InvalidOperationException">If writers already created</exception>
public void EnableSenderTracking(SenderIdentityConfig config)
{
    lock (_trackingLock)
    {
        if (_activeWriterCount > 0)
            throw new InvalidOperationException("EnableSenderTracking must be called before creating writers");

        _identityConfig = config;
        _senderRegistry = new SenderRegistry(this);
    }
}

/// <summary>
/// Provides access to the sender registry (if tracking enabled).
/// </summary>
public SenderRegistry? SenderRegistry => _senderRegistry;
```

**Add Internal Lifecycle Hooks:**
```csharp
internal void RegisterWriter()
{
    if (_identityConfig == null) return; // Feature disabled

    lock (_trackingLock)
    {
        _activeWriterCount++;
        if (_activeWriterCount == 1)
        {
            PublishIdentity();
        }
    }
}

internal void UnregisterWriter()
{
    if (_identityConfig == null) return;

    lock (_trackingLock)
    {
        _activeWriterCount--;
        if (_activeWriterCount == 0 && !_identityConfig.KeepAliveUntilParticipantDispose)
        {
            DisposeIdentityWriter();
        }
    }
}

private void PublishIdentity()
{
    var process = System.Diagnostics.Process.GetCurrentProcess();

    // Get native participant GUID
    DdsApi.dds_get_guid(NativeEntity.Handle, out var myGuid);

    var identity = new SenderIdentity
    {
        ParticipantGuid = myGuid,
        AppDomainId = _identityConfig!.AppDomainId,
        AppInstanceId = _identityConfig.AppInstanceId,
        ProcessId = process.Id,
        ProcessName = _identityConfig.ProcessName ?? process.ProcessName,
        ComputerName = _identityConfig.ComputerName ?? Environment.MachineName
    };

    // Create identity writer with TransientLocal durability
    // (We need to check if QoS can be passed, or create manually)
    _identityWriter = new DdsWriter<SenderIdentity>(this, "__FcdcSenderIdentity");
    _identityWriter.Write(identity);
}

private void DisposeIdentityWriter()
{
    _identityWriter?.Dispose();
    _identityWriter = null;
}
```

**Update Dispose:**
```csharp
public void Dispose()
{
    // ... existing dispose logic ...

    _senderRegistry?.Dispose();
    _identityWriter?.Dispose();

    // ... rest of dispose ...
}
```

#### Task 7: Update DdsWriter (15 min)

**File:** `Src/CycloneDDS.Runtime/DdsWriter.cs`

**Update Constructor:**
```csharp
public DdsWriter(DdsParticipant participant, string topicName, IntPtr qos = default)
{
    _participant = participant;
    // ... existing creation logic ...

    // Notify participant (triggers identity publishing if enabled)
    // Skip for the identity writer itself to avoid recursion
    if (typeof(T) != typeof(SenderIdentity))
    {
        _participant.RegisterWriter();
    }
}
```

**Update Dispose:**
```csharp
public void Dispose()
{
    // ... existing cleanup ...

    if (typeof(T) != typeof(SenderIdentity))
    {
        _participant?.UnregisterWriter();
    }

    // ... rest of dispose ...
}
```

#### Task 8: Update DdsReader (1-2 hours)

**File:** `Src/CycloneDDS.Runtime/DdsReader.cs`

**Add Field:**
```csharp
private SenderRegistry? _registry;
```

**Add Public API:**
```csharp
/// <summary>
/// Enable sender tracking for this reader.
/// After this, ViewScope.GetSender(index) will return sender information.
/// </summary>
public void EnableSenderTracking(SenderRegistry registry)
{
    _registry = registry;
    this.SubscriptionMatched += OnSenderTrackingSubscriptionMatched;
}
```

**Add Event Handler:**
```csharp
private void OnSenderTrackingSubscriptionMatched(object? sender, DdsApi.DdsSubscriptionMatchedStatus e)
{
    if (e.CurrentCountChange > 0 && _registry != null)
    {
        // New writer(s) connected - register them
        try
        {
            var handles = GetMatchedPublicationHandles();
            foreach (var handle in handles)
            {
                var writerGuid = GetMatchedPublicationGuid(handle);
                _registry.RegisterRemoteWriter(handle, writerGuid);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DdsReader] Failed to register matched publications: {ex}");
        }
    }
}
```

**Add Helper Methods:**
```csharp
private long[] GetMatchedPublicationHandles()
{
    if (_readerHandle == null) return Array.Empty<long>();

    // Query Cyclone DDS for matched publication count
    var handles = new long[64]; // Reasonable max
    int count = DdsApi.dds_get_matched_publications(
        _readerHandle.NativeHandle.Handle,
        handles,
        (uint)handles.Length);

    if (count < 0)
    {
        Console.WriteLine($"[DdsReader] dds_get_matched_publications failed: {count}");
        return Array.Empty<long>();
    }

    // Return only valid handles
    var result = new long[count];
    Array.Copy(handles, result, count);
    return result;
}

private DdsGuid GetMatchedPublicationGuid(long handle)
{
    if (_readerHandle == null) return default;

    int ret = DdsApi.dds_get_matched_publication_data(
        _readerHandle.NativeHandle.Handle,
        handle,
        out var data);

    if (ret < 0)
    {
        Console.WriteLine($"[DdsReader] dds_get_matched_publication_data failed: {ret}");
        return default;
    }

    var guid = data.Guid;

    // Free allocated native memory
    if (data.TopicName != IntPtr.Zero) DdsApi.dds_free(data.TopicName);
    if (data.TypeName != IntPtr.Zero) DdsApi.dds_free(data.TypeName);
    if (data.Qos != IntPtr.Zero) DdsApi.dds_delete_qos(data.Qos);

    return guid;
}
```

**Update ViewScope Creation:**

In `ReadOrTake` and `ReadOrTakeInstance`, pass `_registry` to `ViewScope`:

```csharp
return new ViewScope<TView>(_readerHandle.NativeHandle, samples, infos, count, _deserializer, _filter, _registry);
```

#### Task 9: Update ViewScope (30 min)

**File:** `Src/CycloneDDS.Runtime/DdsReader.cs` (ViewScope is nested in this file)

**Add Field:**
```csharp
private SenderRegistry? _registry;
```

**Update Constructor:**
```csharp
internal ViewScope(
    DdsApi.DdsEntity reader, 
    IntPtr[]? samples, 
    DdsApi.DdsSampleInfo[]? infos, 
    int count, 
    DeserializeDelegate<TView>? deserializer, 
    Predicate<TView>? filter,
    SenderRegistry? registry = null)  // ← ADD THIS PARAMETER
{
    _reader = reader;
    _samples = samples;
    _infos = infos;
    _count = count;
    _deserializer = deserializer;
    _filter = filter;
    _registry = registry;  // ← ADD THIS ASSIGNMENT
}
```

**Add Public API:**
```csharp
/// <summary>
/// Get sender information for sample at specified index.
/// Returns null if tracking is not enabled or sender is unknown.
/// O(1) operation.
/// </summary>
public SenderIdentity? GetSender(int index)
{
    if (_registry == null || index < 0 || index >= Count)
        return null;

    if (_infos == null) return null;

    long pubHandle = _infos[index].PublicationHandle;

    if (_registry.TryGetIdentity(pubHandle, out var identity))
    {
        return identity;
    }

    return null; // Sender not registered yet (rare)
}
```

---

## 5. Testing Requirements (CRITICAL!)

### 5.1  Create Test File

**File:** `tests/CycloneDDS.Runtime.Tests/SenderTrackingTests.cs`

### 5.2 Minimum Required Tests (8 total)

**FCDC-EXT06 Tests (5):**

1. **IdentityPublishing_WriterCreated_PublishesSenderInfo**
   - Enable tracking with custom AppDomainId=100, AppInstanceId=1
   - Create a writer
   - Verify: SenderIdentity published to `__FcdcSenderIdentity` topic
   - Verify: AppDomainId, AppInstanceId, ProcessId are correct

2. **IdentityCache_RemoteIdentity_CachedInRegistry**
   - Create two participants (sender & receiver)
   - Enable tracking on both
   - Sender publishes identity
   - Verify: Receiver's registry caches sender's identity

3. **HandleCorrelation_PublicationHandle_MapsToIdentity**
   - Two participants: sender (AppDomainId=1) and receiver (AppDomainId=2)
   - Sender writes data
   - Receiver takes data
   - Verify: PublicationHandle from SampleInfo maps to correct SenderIdentity

4. **GetSender_O1Lookup_FastPerformance**
   - Receive 100 samples
   - Benchmark `GetSender(index)` for each
   - Verify: Average lookup time < 100ns (dictionary overhead)

5. **DisabledOverhead_TrackingOff_ZeroImpact**
   - Create participant WITHOUT calling EnableSenderTracking
   - Create writer & reader
   - Verify: No identity writer created
   - Verify: No sender registry created
   - Verify: GetSender() returns null

**FCDC-EXT07 Tests (3):**

6. **SenderTracking_MultiProcess_CorrectIdentity**
   - Two participants with different AppDomainIds (100 vs 200)
   - Participant A (AppDomainId=100) sends data
   - Participant B (AppDomainId=200) receives
   - Verify: B's `GetSender()` returns A's identity with AppDomainId=100

7. **SenderTracking_LateJoiner_TransientLocalWorks**
   - Start sender participant, enable tracking, publish identity
   - Wait 500ms
   - Start receiver participant, enable tracking
   - Receiver should retrieve sender identity from TransientLocal history
   - Verify: GetSender() succeeds even though receiver joined late

8. **SenderTracking_MultiInstance_ProcessIdDisambiguates**
   - Simulate two senders with same AppDomainId but different ProcessIds
   - (Use two participants in same test process - they'll have same ProcessId, so verify AppInstanceId instead)
   - Verify: ProcessId correctly distinguishes instances

### 5.3 Test Quality Standards

**From DEV-LEAD-GUIDE.md:**
> Tests MUST verify actual behavior, not just string presence or method existence.

**Checklist for Each Test:**
- ✅ Verify ACTUAL data correctness (AppDomainId matches, ProcessId matches)
- ✅ Verify ACTUAL lookups work (GetSender returns non-null)
- ✅ Verify ACTUAL performance (benchmark if needed)
- ❌ DO NOT just check for exceptions or method compilation

---

## 6. Common Pitfalls & Tips

### 6.1 Recursive Identity Publishing

**PROBLEM:** When creating the identity writer, it triggers `RegisterWriter()`, which tries to publish identity again → infinite loop.

**SOLUTION:** Skip registration for `SenderIdentity` type:
```csharp
if (typeof(T) != typeof(SenderIdentity))
{
    _participant.RegisterWriter();
}
```

### 6.2 Memory Leaks from P/Invoke

**PROBLEM:** `dds_get_matched_publication_data` allocates strings that must be freed.

**SOLUTION:** Always call `dds_free()` on returned pointers:
```csharp
if (data.TopicName != IntPtr.Zero) DdsApi.dds_free(data.TopicName);
if (data.TypeName != IntPtr.Zero) DdsApi.dds_free(data.TypeName);
```

### 6.3 Race Conditions

**PROBLEM:** Identity topic may arrive AFTER data topic (late discovery).

**SOLUTION:** Implement lazy resolution in `TryResolveLazy()` or accept that GetSender() may initially return null.

### 6.4 Thread Safety

**PROBLEM:** Background monitoring task and main thread access dictionaries concurrently.

**SOLUTION:** Use `ConcurrentDictionary` for all shared state.

### 6.5 Zero-Overhead Requirement

**PROBLEM:** Adding checks everywhere slows down normal code path.

**SOLUTION:** All tracking checks must be `if (_registry == null) return;` - JIT will eliminate these branches when registry is null.

---

## 7. Definition of Done

### 7.1 Code Quality

- ✅ All 8 tests pass
- ✅ No compiler warnings
- ✅ No memory leaks (all P/Invoke strings freed)
- ✅ Proper disposal (background task cancelled, reader disposed)
- ✅ Zero overhead when tracking disabled

### 7.2 Performance

- ✅ `GetSender(index)` is O(1) (dictionary lookup)
- ✅ Background monitoring has no impact on hot path
- ✅ When tracking disabled: zero allocations, zero listeners

### 7.3 Documentation

- ✅ XML comments on all public APIs
- ✅ Usage examples in test code
- ✅ Report submitted to `.dev-workstream/reports/BATCH-23-REPORT.md`

---

## 8. Report Template

**File:** `.dev-workstream/reports/BATCH-23-REPORT.md`

```markdown
# BATCH-23 Report: Sender Tracking (FCDC-EXT06, FCDC-EXT07)

**Developer:** [Your Name]  
**Date:** [Completion Date]  
**Status:** ✅ COMPLETE / ⏳ IN PROGRESS / ❌ BLOCKED

---

## Summary

[Brief overview of what you implemented]

---

## Implementation Notes

### FCDC-EXT06: Infrastructure

**Files Created:**
- `Src/CycloneDDS.Runtime/Tracking/SenderIdentity.cs`
- `Src/CycloneDDS.Runtime/Tracking/SenderIdentityConfig.cs`
- `Src/CycloneDDS.Runtime/Tracking/SenderRegistry.cs`

**Files Modified:**
- `Src/CycloneDDS.Runtime/Interop/DdsApi.cs` (added DdsGuid, P/Invokes)

**Key Decisions:**
- [Explain any design choices you made]
- [Explain any deviations from the design doc]

### FCDC-EXT07: Integration

**Files Modified:**
- `Src/CycloneDDS.Runtime/DdsParticipant.cs`
- `Src/CycloneDDS.Runtime/DdsWriter.cs`
- `Src/CycloneDDS.Runtime/DdsReader.cs`

**Challenges:**
- [Describe any difficulties you encountered]
- [Describe how you solved them]

---

## Test Results

**Test File:** `tests/CycloneDDS.Runtime.Tests/SenderTrackingTests.cs`

**Test Summary:**
- Total Tests: [count]
- Passing: [count]
- Failing: [count]

**Test Details:**
1. IdentityPublishing_WriterCreated_PublishesSenderInfo: ✅ PASS
2. IdentityCache_RemoteIdentity_CachedInRegistry: ✅ PASS
3. HandleCorrelation_PublicationHandle_MapsToIdentity: ✅ PASS
4. GetSender_O1Lookup_FastPerformance: ✅ PASS (avg: XXns)
5. DisabledOverhead_TrackingOff_ZeroImpact: ✅ PASS
6. SenderTracking_MultiProcess_CorrectIdentity: ✅ PASS
7. SenderTracking_LateJoiner_TransientLocalWorks: ✅ PASS
8. SenderTracking_MultiInstance_ProcessIdDisambiguates: ✅ PASS

**Full Test Output:**
```
[Paste full `dotnet test` output here]
```

---

## Known Issues / Deferred Work

[List any known issues or features you didn't implement]

---

## Questions for Review

[Any questions you have for the dev lead]

---

## Acknowledgments

[Any resources or people that helped you]
```

---

## 9. Additional Resources

### 9.1 Native Cyclone DDS Source

If you need to inspect native implementations:
- **Participant GUID:** `cyclonedds/src/core/ddsi/src/ddsi_participant.c`
- **Matched Publications:** `cyclonedds/src/core/ddsc/src/dds__reader.h`

### 9.2 Example: Existing Event System

Study how `SubscriptionMatched` event is currently implemented in `DdsReader.cs` as a reference for the tracking hookup.

### 9.3 Debugging Native Calls

If P/Invokes fail:
1. Check return codes (negative = error)
2. Use `Marshal.GetLastWin32Error()` if needed
3. Add debug prints to native Cyclone DDS (you have permission!)

---

## 10. Success Checklist

Before submitting your report:

- [ ] All 8 tests passing
- [ ] No compiler warnings
- [ ] `DdsGuid` struct verified against native layout
- [ ] Memory leaks checked (P/Invoke strings freed)
- [ ] Zero-overhead verified (tracking disabled = no allocations)
- [ ] Performance benchmarked (GetSender < 100ns)
- [ ] Report submitted with full test output
- [ ] Code reviewed for thread safety

---

**Good luck! Remember: Read the design doc FIRST, then code. The design doc is your map.**

**Questions? Stuck? Add debug logging and check native return codes!**
