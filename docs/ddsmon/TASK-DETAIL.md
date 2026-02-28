# DDS Monitor — Task Details

**Design reference:** [DESIGN.md](./DESIGN.md)  
**Task tracker:** [TASK-TRACKER.md](./TASK-TRACKER.md)

Each task below is self-contained. The task description gives enough detail to implement without re-reading the design talk. Where the design document provides the broader context, a section reference is given.

---

<!-- ═══════════════════════════════════════════════════════════════════ -->
<!-- PHASE 1 — HEADLESS DATA ENGINE                                    -->
<!-- ═══════════════════════════════════════════════════════════════════ -->

## DMON-001 — Create Solution & Project Scaffolding

**Phase:** 1 — Foundation  
**Design ref:** [§2](./DESIGN.md#2-technology-stack), [§14](./DESIGN.md#14-application-lifecycle--dependency-injection)

### Description

Create the solution structure for the DDS Monitor application:

1. **`tools/DdsMonitor/DdsMonitor.csproj`** — Blazor Server executable targeting `net8.0`. References `CycloneDDS.Runtime`.
2. **`tools/DdsMonitor/DdsMonitor.Engine/DdsMonitor.Engine.csproj`** — Class library containing the headless data engine (no Blazor dependency).
3. **`tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj`** — xUnit test project for the engine library.
4. Add all three projects to `CycloneDDS.NET.sln`.
5. Add NuGet references: `Fasterflect.Netstandard` to Engine, `System.Linq.Dynamic.Core` to Engine, `Microsoft.AspNetCore.Components` to DdsMonitor.

### Success Conditions

1. **Build** — `dotnet build` succeeds for all three projects with zero errors and zero warnings.
2. **Test scaffold** — `dotnet test` on the test project passes a placeholder `[Fact] Scaffold_Builds() => Assert.True(true)`.
3. **Engine isolation** — `DdsMonitor.Engine.csproj` has no reference to `Microsoft.AspNetCore.*` or any Blazor package.

---

## DMON-002 — TopicMetadata & FieldMetadata Types

**Phase:** 1 — Foundation  
**Design ref:** [§5.2](./DESIGN.md#52-topicmetadata), [§5.3](./DESIGN.md#53-fieldmetadata)

### Description

Create the metadata model types in the Engine project:

```csharp
public class TopicMetadata
{
    public Type TopicType { get; }
    public string TopicName { get; }
    public string ShortName { get; }
    public string Namespace { get; }
    public bool IsKeyed { get; }
    public IReadOnlyList<FieldMetadata> AllFields { get; }
    public IReadOnlyList<FieldMetadata> KeyFields { get; }
}

public class FieldMetadata
{
    public string StructuredName { get; }   // e.g. "Position.X"
    public string DisplayName { get; }
    public Type ValueType { get; }
    public Func<object, object?> Getter { get; }
    public Action<object, object?> Setter { get; }
    public bool IsSynthetic { get; }        // true for computed fields like Delay
}
```

The `Getter`/`Setter` delegates are compiled via `Fasterflect` (or expression trees as fallback). The constructor takes `Type topicType` and reflection-walks all public properties, flattening nested structs into dot-separated paths.

### Success Conditions

1. **Unit test** — `TopicMetadata_FlattensNestedProperties`:
   - Define a test type: `struct Inner { public double X; public double Y; }` and `[DdsTopic("TestTopic")] struct Outer { public int Id; public Inner Position; }`
   - Construct `TopicMetadata` from `typeof(Outer)`.
   - Assert `AllFields` contains entries with `StructuredName` values: `"Id"`, `"Position.X"`, `"Position.Y"`.
2. **Unit test** — `TopicMetadata_IdentifiesKeyFields`:
   - Define `[DdsTopic("Keyed")] struct KeyedType { [DdsKey] public int Id; public string Name; }`
   - Assert `KeyFields.Count == 1` and `KeyFields[0].StructuredName == "Id"`.
3. **Unit test** — `FieldMetadata_Getter_ReturnsCorrectValue`:
   - Build metadata for a type, create an instance, set a known value, call `field.Getter(instance)`, assert the returned value matches.
4. **Unit test** — `FieldMetadata_Setter_SetsCorrectValue`:
   - Build metadata, create an instance, call `field.Setter(instance, 42.0)`, verify the field value changed.

---

## DMON-003 — TopicDiscoveryService (Assembly Scanning)

**Phase:** 1 — Foundation  
**Design ref:** [§5.1](./DESIGN.md#51-assembly-scanning)

### Description

Implement `TopicDiscoveryService` that:

1. Accepts one or more directory paths.
2. Enumerates all `.dll` files in those directories.
3. Loads each into a collectible `AssemblyLoadContext`.
4. Scans exported types for the `[DdsTopic]` attribute.
5. Builds `TopicMetadata` for each discovered type and registers it in an `ITopicRegistry`.

```csharp
public interface ITopicRegistry
{
    IReadOnlyList<TopicMetadata> AllTopics { get; }
    TopicMetadata? GetByType(Type topicType);
    TopicMetadata? GetByName(string topicName);
    void Register(TopicMetadata meta);
}
```

### Success Conditions

1. **Unit test** — `TopicDiscoveryService_FindsTopicInAssembly`:
   - Point the service at a test directory containing a pre-compiled DLL with a `[DdsTopic]` type.
   - Assert `registry.AllTopics.Count >= 1` and the type's `TopicName` matches the expected value.
2. **Unit test** — `TopicDiscoveryService_IgnoresDllsWithoutTopics`:
   - Point at a directory containing a DLL with no `[DdsTopic]` types.
   - Assert `registry.AllTopics.Count == 0`.
3. **Unit test** — `TopicDiscoveryService_IsolatesAssemblyLoadContext`:
   - Verify that loading a plugin DLL does not pollute the default `AssemblyLoadContext`.

---

## DMON-004 — Synthetic (Computed) Fields

**Phase:** 1 — Foundation  
**Design ref:** [§5.4](./DESIGN.md#54-synthetic-computed-fields)

### Description

Extend `TopicMetadata` construction to inject synthetic computed fields:

| Field | StructuredName | ValueType | Getter Logic |
|---|---|---|---|
| Delay | `Delay [ms]` | `double` | `(LocalReceiveTime - DdsSourceTimestamp).TotalMilliseconds` |
| Size | `Size [B]` | `int` | Read from `SampleData.SizeBytes` |

Synthetic fields have `IsSynthetic = true`. Their getters receive the `SampleData` record (not the raw payload). They appear at the end of `AllFields` and are available in the Column Picker and Filter Builder.

### Success Conditions

1. **Unit test** — `SyntheticFields_AppearInAllFields`:
   - Build `TopicMetadata` for any topic type.
   - Assert `AllFields.Any(f => f.StructuredName == "Delay [ms]" && f.IsSynthetic)`.
2. **Unit test** — `SyntheticField_DelayGetter_ComputesCorrectly`:
   - Create a `SampleData` with `Timestamp = T1` and `SampleInfo.SourceTimestamp = T0`.
   - Call the `Delay [ms]` getter with this sample.
   - Assert result equals `(T1 - T0).TotalMilliseconds` within ±0.1.

---

## DMON-005 — SampleData Record

**Phase:** 1 — Foundation  
**Design ref:** [§4.2](./DESIGN.md#42-the-sampledata-record)

### Description

Define the core data carrier:

```csharp
public record SampleData
{
    public long Ordinal { get; init; }
    public object Payload { get; init; }
    public TopicMetadata TopicMetadata { get; init; }
    public DdsSampleInfo SampleInfo { get; init; }
    public SenderIdentity? Sender { get; init; }
    public DateTime Timestamp { get; init; }
    public int SizeBytes { get; init; }
}

public record SenderIdentity
{
    public uint ProcessId { get; init; }
    public string? MachineName { get; init; }
    public string? IpAddress { get; init; }
}
```

### Success Conditions

1. **Compile** — the types compile and are usable from test code.
2. **Unit test** — `SampleData_WithInitSyntax_SetsAllProperties`:
   - Create a `SampleData` with all properties via `with {}` syntax, assert each is stored.
3. **Unit test** — `SampleData_RecordEquality_WorksByValue`:
   - Two `SampleData` records with identical property values are `Equal`.

---

## DMON-006 — IDynamicReader / IDynamicWriter Interfaces

**Phase:** 1 — Foundation  
**Design ref:** [§4.3](./DESIGN.md#43-dynamic-dds-bridge)

### Description

Define the non-generic DDS reader/writer abstractions:

```csharp
public interface IDynamicReader : IDisposable
{
    Type TopicType { get; }
    TopicMetadata TopicMetadata { get; }
    void Start(string? partition);
    void Stop();
    event Action<SampleData>? OnSampleReceived;
}

public interface IDynamicWriter : IDisposable
{
    Type TopicType { get; }
    void Write(object payload);
    void DisposeInstance(object payload);
}
```

### Success Conditions

1. **Compile** — interfaces compile, can be referenced from other Engine types.
2. **Unit test** — `MockDynamicReader_FiresOnSampleReceived`:
   - Create a mock implementing `IDynamicReader`, fire the event, verify a subscriber receives it.

---

## DMON-007 — DynamicReader\<T\> Implementation

**Phase:** 1 — Foundation  
**Design ref:** [§4.3](./DESIGN.md#43-dynamic-dds-bridge)

### Description

Implement the generic concrete class `DynamicReader<T>` that wraps `DdsReader<T>`:

1. Constructor takes `DdsParticipant`, `TopicMetadata`, and optional `partition` string.
2. `Start(partition)` creates a `DdsReader<T>` with the given partition.
3. Runs a background task calling `reader.WaitDataAsync()` in a loop.
4. On data arrival, takes the loan, boxes the payload as `object`, extracts `DdsSampleInfo`, creates `SampleData`, fires `OnSampleReceived`.
5. `Stop()` cancels the background task and disposes the reader.

Runtime instantiation via:
```csharp
var readerType = typeof(DynamicReader<>).MakeGenericType(topicMeta.TopicType);
var reader = (IDynamicReader)Activator.CreateInstance(readerType, participant, topicMeta, partition);
```

### Success Conditions

1. **Unit test** — `DynamicReader_CanBeConstructedViaReflection`:
   - Use `MakeGenericType` + `Activator.CreateInstance` with a known `[DdsTopic]` type. Assert the returned object implements `IDynamicReader`.
2. **Integration test** — `DynamicReader_ReceivesSample_FromDynamicWriter`:
   - Create participant, writer, and reader for the same topic.
   - Write a sample, wait for `OnSampleReceived` with a 5s timeout.
   - Assert the received `SampleData.Payload` matches the written value.

---

## DMON-008 — DynamicWriter\<T\> Implementation

**Phase:** 1 — Foundation  
**Design ref:** [§4.3](./DESIGN.md#43-dynamic-dds-bridge)

### Description

Implement `DynamicWriter<T>` wrapping `DdsWriter<T>`:

1. Constructor takes `DdsParticipant`, `TopicMetadata`, optional `partition`.
2. `Write(object payload)` unboxes to `T` and calls `writer.Write((T)payload)`.
3. `DisposeInstance(object payload)` calls the equivalent dispose method.

### Success Conditions

1. **Unit test** — `DynamicWriter_Write_DoesNotThrow`:
   - Create a `DynamicWriter<TestType>`, call `Write(new TestType { ... })`, assert no exception.
2. **Unit test** — `DynamicWriter_DisposeInstance_DoesNotThrow`:
   - For a keyed type, call `DisposeInstance(...)`, assert no exception.

---

## DMON-009 — DdsBridge Service

**Phase:** 1 — Foundation  
**Design ref:** [§4.3](./DESIGN.md#43-dynamic-dds-bridge), [§4.4](./DESIGN.md#44-partition-management)

### Description

Implement the `DdsBridge` as a singleton service managing DDS communication:

```csharp
public interface IDdsBridge : IDisposable
{
    DdsParticipant Participant { get; }
    string? CurrentPartition { get; }

    IDynamicReader Subscribe(TopicMetadata meta);
    void Unsubscribe(TopicMetadata meta);
    IDynamicWriter GetWriter(TopicMetadata meta);

    void ChangePartition(string? newPartition);

    IReadOnlyDictionary<Type, IDynamicReader> ActiveReaders { get; }
}
```

**Partition change logic:**
1. Record which topics are currently subscribed.
2. Stop and dispose all active readers.
3. Update `CurrentPartition`.
4. Recreate all readers with the new partition.
5. The `DdsParticipant` object remains untouched.

### Success Conditions

1. **Unit test** — `DdsBridge_Subscribe_CreatesReader`:
   - Subscribe to a topic, assert `ActiveReaders` contains that type.
2. **Unit test** — `DdsBridge_Unsubscribe_RemovesReader`:
   - Subscribe then unsubscribe, assert `ActiveReaders` does not contain that type.
3. **Unit test** — `DdsBridge_ChangePartition_RecreatesReaders`:
   - Subscribe to two topics, change partition, assert `ActiveReaders.Count == 2` and all readers are new instances.

---

## DMON-010 — SampleStore (Chronological Ledger)

**Phase:** 1 — Foundation  
**Design ref:** [§6](./DESIGN.md#6-samplestore)

### Description

Implement the `SampleStore` as specified in [§6](./DESIGN.md#6-samplestore):

1. **Append-only base** — thread-safe `List<SampleData>` protected by a lock or concurrent collection.
2. **Per-topic index** — `ConcurrentDictionary<Type, TopicSamples>` for fast topic-scoped queries.
3. **Filtered list** — samples passing the active filter are appended to `_filteredSamples`.
4. **Sorted view** — a background `SortMergeWorker` wakes every ~50ms, sorts new arrivals, merges with existing sorted array, publishes via `Interlocked.Exchange`.
5. **Virtual view** — `GetVirtualView(startIndex, count)` returns a `ReadOnlySpan<SampleData>` slice suitable for `<Virtualize>`.

```csharp
public interface ISampleStore
{
    IReadOnlyList<SampleData> AllSamples { get; }
    ITopicSamples GetTopicSamples(Type topicType);

    void Append(SampleData sample);
    void Clear();

    int CurrentFilteredCount { get; }
    ReadOnlyMemory<SampleData> GetVirtualView(int startIndex, int count);

    void SetFilter(Func<SampleData, bool>? compiledFilterPredicate);
    void SetSortSpec(FieldMetadata field, SortDirection direction);

    event Action? OnViewRebuilt;
}
```

### Success Conditions

1. **Unit test** — `SampleStore_Append_IncrementsCount`:
   - Append 100 samples, assert `AllSamples.Count == 100`.
2. **Unit test** — `SampleStore_GetTopicSamples_ReturnsOnlyMatchingTopic`:
   - Append samples for two different topics, call `GetTopicSamples(topicA)`, assert count matches only topic A samples.
3. **Unit test** — `SampleStore_SetFilter_ReducesFilteredCount`:
   - Append 100 samples with `Ordinal` 1..100.
   - Set filter `s => s.Ordinal > 50`.
   - Assert `CurrentFilteredCount == 50`.
4. **Unit test** — `SampleStore_SetSortSpec_SortsDescending`:
   - Append samples with random ordinals.
   - Set sort by Ordinal descending.
   - Wait for `OnViewRebuilt`.
   - Read `GetVirtualView(0, 10)` and assert items are in descending order.
5. **Unit test** — `SampleStore_Clear_ResetsEverything`:
   - Append samples, clear, assert `AllSamples.Count == 0` and `CurrentFilteredCount == 0`.
6. **Unit test** — `SampleStore_GetVirtualView_ReturnsCorrectSlice`:
   - Append 100 samples, request `GetVirtualView(10, 5)`, assert exactly 5 items starting at index 10.
7. **Thread-safety test** — `SampleStore_ConcurrentAppendAndRead_DoesNotThrow`:
   - Spawn 4 tasks appending 1000 samples each while another task reads `CurrentFilteredCount` in a loop. Assert no exceptions.

---

## DMON-011 — InstanceStore (Keyed Instance Tracking)

**Phase:** 1 — Foundation  
**Design ref:** [§7](./DESIGN.md#7-instancestore)

### Description

Implement the `InstanceStore` as specified in [§7](./DESIGN.md#7-instancestore):

1. **Key extraction** from `TopicMetadata.KeyFields` using compiled getters.
2. **State machine** — Alive ↔ Dead transitions as described in the design pseudocode.
3. **Instance journal** — append-only log of `InstanceTransitionEvent` records.
4. **Observable** — `IObservable<InstanceTransitionEvent>` for plugin subscriptions.

```csharp
public interface IInstanceStore
{
    ITopicInstances GetTopicInstances(Type topicType);
    IObservable<InstanceTransitionEvent> OnInstanceChanged { get; }
    void Clear();
}

public enum InstanceState { Alive, Disposed, NoWriters }
public enum TransitionKind { Added, Updated, Removed }
```

### Success Conditions

1. **Unit test** — `InstanceStore_NewKey_CreatesAliveInstance`:
   - Process a sample with a new key. Assert the instance is `Alive` and `LiveCount == 1`.
2. **Unit test** — `InstanceStore_DisposeKey_MarksAsDead`:
   - Add an alive instance, then process a Disposed sample. Assert `LiveCount == 0` and instance state is `Disposed`.
3. **Unit test** — `InstanceStore_RebirthKey_ResetsCounters`:
   - Add alive → dispose → add alive again. Assert `LiveCount == 1` and `NumSamplesRecent == 1`.
4. **Unit test** — `InstanceStore_FiresTransitionEvents`:
   - Subscribe to `OnInstanceChanged`. Process Added/Updated/Removed transitions. Assert the correct `TransitionKind` is received for each.
5. **Unit test** — `InstanceStore_ExtractsCompositeKey`:
   - For a type with two `[DdsKey]` fields, process two samples with same Key1 but different Key2. Assert two distinct instances are tracked.

---

## DMON-012 — FilterCompiler (Dynamic LINQ)

**Phase:** 1 — Foundation  
**Design ref:** [§6.3](./DESIGN.md#63-filter-application)

### Description

Implement the `FilterCompiler` service that compiles user-typed text expressions into executable predicates:

```csharp
public interface IFilterCompiler
{
    FilterResult Compile(string expression, TopicMetadata? topicMeta);
}

public record FilterResult(
    bool IsValid,
    Func<SampleData, bool>? Predicate,
    string? ErrorMessage);
```

Uses `System.Linq.Dynamic.Core` under the hood. The expression is evaluated against `SampleData` properties (e.g. `Ordinal > 50`, `TopicMetadata.TopicName.Contains("Robot")`).

For payload field access, the compiler should support a shorthand: `Payload.Position.X > 10` which is rewritten to use the appropriate `FieldMetadata.Getter` delegate.

### Success Conditions

1. **Unit test** — `FilterCompiler_SimpleExpression_Compiles`:
   - Compile `"Ordinal > 50"`. Assert `result.IsValid == true` and `result.Predicate != null`.
2. **Unit test** — `FilterCompiler_Predicate_FiltersCorrectly`:
   - Compile `"Ordinal > 50"`, evaluate against `SampleData` with `Ordinal = 100`. Assert predicate returns `true`.
   - Evaluate against `SampleData` with `Ordinal = 10`. Assert predicate returns `false`.
3. **Unit test** — `FilterCompiler_InvalidExpression_ReturnsError`:
   - Compile `"))) garbage ((("`. Assert `result.IsValid == false` and `result.ErrorMessage` is not null/empty.
4. **Unit test** — `FilterCompiler_PayloadFieldAccess_Works`:
   - Compile an expression referencing a payload field (e.g. `"Payload.Id == 42"`).
   - Evaluate against matching and non-matching samples.

---

## DMON-013 — DdsIngestionService (Background Worker)

**Phase:** 1 — Foundation  
**Design ref:** [§4.1](./DESIGN.md#41-ingestion-flow), [§14.3](./DESIGN.md#143-background-ingestion-service)

### Description

Implement `DdsIngestionService` as an `IHostedService` that:

1. Reads `SampleData` from a `Channel<SampleData>` (the channel is fed by `DynamicReader` instances).
2. Appends each sample to `ISampleStore`.
3. If the sample's topic is keyed, passes it to `IInstanceStore.ProcessSample()`.
4. Runs until `CancellationToken` is cancelled.

```csharp
public class DdsIngestionService : BackgroundService
{
    private readonly ChannelReader<SampleData> _channelReader;
    private readonly ISampleStore _sampleStore;
    private readonly IInstanceStore _instanceStore;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var sample in _channelReader.ReadAllAsync(ct))
        {
            _sampleStore.Append(sample);
            if (sample.TopicMetadata.IsKeyed)
                _instanceStore.ProcessSample(sample);
        }
    }
}
```

### Success Conditions

1. **Unit test** — `IngestionService_ProcessesSamplesFromChannel`:
   - Write 10 samples into the channel. Start the service. Wait 500ms. Assert `SampleStore.AllSamples.Count == 10`.
2. **Unit test** — `IngestionService_RoutesKeyedSamplesToInstanceStore`:
   - Write a keyed sample into the channel. Assert `InstanceStore` received a `ProcessSample` call (verify via mock).
3. **Unit test** — `IngestionService_StopsGracefullyOnCancellation`:
   - Start the service, cancel the token. Assert the task completes within 1 second without throwing.

---

## DMON-014 — Application Host & DI Wiring

**Phase:** 1 — Foundation  
**Design ref:** [§14.1](./DESIGN.md#141-startup-sequence), [§14.2](./DESIGN.md#142-di-strategy)

### Description

Wire up the ASP.NET Core host with all services as specified in [§14](./DESIGN.md#14-application-lifecycle--dependency-injection):

- **Singletons:** `ITopicRegistry`, `IDdsBridge`, `ISampleStore`, `IInstanceStore`, `IEventBroker`, `IFilterCompiler`, `Channel<SampleData>`
- **Scoped:** `IWindowManager`, `IWorkspaceState`
- **Hosted Service:** `DdsIngestionService`
- **Blazor Server:** `AddRazorComponents().AddInteractiveServerComponents()`
- **Configuration:** Read `DdsSettings` from `appsettings.json` (domain ID, plugin directories)

Add `appsettings.json` with default configuration:
```json
{
  "DdsSettings": {
    "DomainId": 0,
    "PluginDirectories": ["plugins"],
    "UiRefreshHz": 30
  }
}
```

### Success Conditions

1. **Smoke test** — The application starts, opens Kestrel on `localhost:5000`, and the Blazor page loads in a browser without errors.
2. **DI resolution test** — All registered services can be resolved from the built `IServiceProvider` without exceptions.

---

<!-- ═══════════════════════════════════════════════════════════════════ -->
<!-- PHASE 2 — BLAZOR SHELL & CORE UI                                  -->
<!-- ═══════════════════════════════════════════════════════════════════ -->

## DMON-015 — EventBroker (Pub/Sub)

**Phase:** 2 — Blazor Shell & Core UI  
**Design ref:** [§9](./DESIGN.md#9-event-broker-panel-communication)

### Description

Implement the type-safe pub/sub broker for panel-to-panel communication:

```csharp
public interface IEventBroker
{
    void Publish<TEvent>(TEvent eventMessage);
    IDisposable Subscribe<TEvent>(Action<TEvent> handler);
}
```

Must be thread-safe. Subscriptions return `IDisposable` for deterministic cleanup when a panel is closed.

Define event record types:
- `SampleSelectedEvent(string SourcePanelId, SampleData Sample)`
- `CloneAndSendRequestEvent(TopicMetadata TopicMeta, object Payload)`
- `SpawnPanelEvent(string PanelTypeName, Dictionary<string, object>? State)`
- `AddColumnRequestEvent(string TargetPanelId, string FieldPath)`

### Success Conditions

1. **Unit test** — `EventBroker_PublishAndSubscribe_DeliversEvent`:
   - Subscribe to `SampleSelectedEvent`. Publish one. Assert the handler was invoked with the correct data.
2. **Unit test** — `EventBroker_Dispose_StopsDelivery`:
   - Subscribe, dispose the subscription, publish. Assert the handler was NOT invoked.
3. **Unit test** — `EventBroker_MultipleSubscribers_AllReceive`:
   - Two subscribers for the same event type. Publish. Assert both handlers were invoked.
4. **Unit test** — `EventBroker_DifferentEventTypes_DoNotCrossTalk`:
   - Subscribe to type A and type B separately. Publish type A. Assert only type A handler fires.

---

## DMON-016 — PanelState Model & IWindowManager Interface

**Phase:** 2 — Blazor Shell & Core UI  
**Design ref:** [§8.2](./DESIGN.md#82-panel-state), [§8.3](./DESIGN.md#83-iwindowmanager)

### Description

Implement the window management model:

```csharp
public class PanelState { ... }           // As specified in DESIGN.md §8.2
public interface IWindowManager { ... }   // As specified in DESIGN.md §8.3
```

Key behaviors:
- `SpawnPanel` generates auto-incrementing IDs (e.g. `SamplesPanel.1`, `SamplesPanel.2`).
- `BringToFront` sets the target panel's `ZIndex` to `max(all) + 1`.
- `SaveWorkspace` / `LoadWorkspace` serialize/deserialize the full panel list to/from JSON.
- `ClosePanel` removes the panel and fires an event for cleanup.

### Success Conditions

1. **Unit test** — `WindowManager_SpawnPanel_AssignsUniqueId`:
   - Spawn two panels of the same type. Assert their `PanelId` values differ (e.g. `.1` vs `.2`).
2. **Unit test** — `WindowManager_ClosePanel_RemovesFromList`:
   - Spawn, close. Assert `ActivePanels` does not contain the panel.
3. **Unit test** — `WindowManager_BringToFront_SetsHighestZIndex`:
   - Spawn three panels. Bring the first to front. Assert its `ZIndex` is the highest.
4. **Unit test** — `WindowManager_SaveAndLoad_RoundTrips`:
   - Spawn panels with various positions/sizes. Save to a temp file. Clear. Load. Assert all panels are restored with matching state.

---

## DMON-017 — Desktop.razor Shell & Panel Chrome

**Phase:** 2 — Blazor Shell & Core UI  
**Design ref:** [§8.4](./DESIGN.md#84-rendering)

### Description

Create the root `Desktop.razor` component that renders the Web Desktop:

1. Iterates `IWindowManager.ActivePanels`.
2. Each panel renders as an absolute-positioned `<div>` with:
   - **Title bar** — draggable (mouse events `@onmousedown`, `@onmousemove`, `@onmouseup`), showing `Title`.
   - **Minimize button** — toggles `IsMinimized`.
   - **Close button** — calls `WindowManager.ClosePanel(panelId)`.
   - **Body** — renders a `<DynamicComponent Type="resolvedType" Parameters="componentState">`.
   - **Resize handles** — corner/edge `<div>` elements that update `Width`/`Height` on drag.
3. Clicking anywhere on a panel calls `BringToFront`.
4. Minimized panels collapse to title-bar-only strips at the bottom of the viewport.

### Success Conditions

1. **Manual test** — panels can be dragged by their title bar. Position updates live.
2. **Manual test** — panels can be resized from edges/corners. Width/Height update live.
3. **Manual test** — clicking a background panel brings it to front (z-order changes).
4. **Manual test** — minimize collapses to strip; clicking strip restores the panel.
5. **Manual test** — close button removes the panel from the desktop.

---

## DMON-018 — Topic Explorer Panel

**Phase:** 2 — Blazor Shell & Core UI  
**Design ref:** [§10.1](./DESIGN.md#101-topic-explorer)

### Description

Implement the `TopicExplorerPanel.razor` component:

1. Lists all topics from `ITopicRegistry` in a table.
2. Columns: Subscribe checkbox, Topic Name, Sample Count, Instance Count, Action buttons.
3. **Tri-state display filters** at the top (Received / Subscribed / Keyed / Alive) cycling Include → Exclude → Ignore.
4. **Search box** filters topic list by name (includes both ShortName and Namespace).
5. **Subscribe checkbox** toggles `DdsBridge.Subscribe(meta)` / `DdsBridge.Unsubscribe(meta)`.
6. **[Grid] button** / double-click spawns a `SamplesPanel` filtered to that topic.
7. **[Instances] button** spawns an `InstancesPanel` (visible only for keyed topics).
8. **Live statistics** update via a 30 Hz timer polling `SampleStore` and `InstanceStore`.

### Success Conditions

1. **Manual test** — all registered topics appear in the list.
2. **Manual test** — search box narrows the displayed topics in real-time.
3. **Manual test** — subscribing a topic starts receiving data (sample count climbs).
4. **Manual test** — [Grid] button opens a new SamplesPanel.
5. **Unit test** — `TopicExplorerPanel_TriStateFilter_CyclesCorrectly`:
   - Simulate three clicks on a filter button. Assert state cycles: Ignore → Include → Exclude → Ignore.

---

## DMON-019 — Topic Picker (Reusable Component)

**Phase:** 2 — Blazor Shell & Core UI  
**Design ref:** [§10.2](./DESIGN.md#102-topic-picker)

### Description

Implement `TopicPicker.razor` — a reusable incremental search dropdown:

1. Text input with debounced filtering (100ms).
2. Two-column results: ShortName (highlighted) | Namespace (dimmed).
3. Keyboard navigation: Arrow Up/Down to highlight, Enter to select, Escape to close.
4. Fires `EventCallback<TopicMetadata> OnTopicSelected`.

### Success Conditions

1. **Unit test** — `TopicPicker_FiltersOnKeystroke`:
   - Provide a list of 100 topics. Type `"rob"`. Assert only topics containing `"rob"` appear.
2. **Unit test** — `TopicPicker_MatchesBothNameAndNamespace`:
   - Assert a query matching only the namespace still returns the topic.
3. **Manual test** — Arrow keys navigate the list, Enter selects.

---

## DMON-020 — Column Picker Dialog

**Phase:** 2 — Blazor Shell & Core UI  
**Design ref:** [§10.3](./DESIGN.md#103-column-picker)

### Description

Implement `ColumnPickerDialog.razor`:

1. **Dual-list** layout: Available Fields (left) and Selected Columns (right).
2. Both lists have independent search boxes.
3. `[ADD >]` and `[< REMOVE]` buttons to move fields between panes.
4. Drag-and-drop reordering within the Selected pane.
5. `[Apply]` fires `EventCallback<List<FieldMetadata>>` with the final column order.
6. `[Cancel]` discards changes.

### Success Conditions

1. **Unit test** — `ColumnPicker_AddField_MovesToSelected`:
   - Start with field in Available. Click Add. Assert it moved to Selected.
2. **Unit test** — `ColumnPicker_RemoveField_MovesToAvailable`:
   - Start with field in Selected. Click Remove. Assert it moved to Available.
3. **Unit test** — `ColumnPicker_Apply_ReturnsSelectedOrder`:
   - Select three fields. Apply. Assert callback receives them in the correct order.
4. **Manual test** — drag-and-drop reordering works within the Selected pane.

---

## DMON-021 — Samples Panel (Virtualized Data Grid)

**Phase:** 2 — Blazor Shell & Core UI  
**Design ref:** [§10.4](./DESIGN.md#104-sample-list-data-grid)

### Description

Implement `SamplesPanel.razor` — the core data grid panel:

1. Uses Blazor `<Virtualize>` to render only visible rows (30-50 at a time).
2. **Standard columns:** Ordinal, Status icon, Topic Name, Size [B], Timestamp, Delay [ms].
3. **Custom columns:** Populated from Column Picker; values read via `FieldMetadata.Getter`.
4. **Sorting:** Column header click sets `SampleStore.SetSortSpec()`. Background sort keeps UI free.
5. **Track mode ([🔗]):** Single-click publishes `SampleSelectedEvent` to the `EventBroker` for linked inspector panes.
6. **Double-click:** Spawns a new detached `DetailPanel` with the selected sample frozen.
7. **Filter input:** Text box at the top compiles via `FilterCompiler` and applies to local view.
8. **Lifecycle icons:** Alive = gray, Disposed = red, NoWriters = red X.
9. **Status bar:** Shows `"Showing N of M matching samples"`.
10. **~30 Hz refresh:** A timer polls `CurrentFilteredCount`; only calls `StateHasChanged()` if the count changed.

### Success Conditions

1. **Manual test** — scrolling through 100,000+ samples is smooth (no jank).
2. **Manual test** — clicking a column header sorts; large datasets sort without freezing the UI.
3. **Manual test** — typing a filter expression narrows the displayed rows.
4. **Manual test** — single-click in Track mode updates a linked Inspector panel.
5. **Manual test** — double-click opens a frozen detail window.
6. **Unit test** — `SamplesPanel_VirtualizeCallback_RequestsCorrectRange`:
   - Mock the `ItemsProvider`. Assert it receives calls with correct `startIndex` and `count` based on scroll position.

---

## DMON-022 — Sample Detail Panel (Inspector)

**Phase:** 2 — Blazor Shell & Core UI  
**Design ref:** [§10.5](./DESIGN.md#105-sample-detail-inspector)

### Description

Implement `DetailPanel.razor`:

1. **Tabbed renderers:** Tree (default), Table, JSON, Sample Info, Sender.
2. **Tree view:** Recursive expandable property tree with color-coded types (CSS classes for string, number, boolean, null, enum).
3. **Link modes:**
   - **Linked:** Subscribes to `SampleSelectedEvent` from a specific source panel.
   - **Detached:** Holds a static `SampleData` reference. Ignores events.
   - Padlock icon in title bar toggles between modes.
4. **Debounce:** If arrow keys are held, waits 50ms after the last event before re-rendering the tree (grid highlight moves instantly).
5. **Clone to Send:** Button deep-copies payload via JSON round-trip and publishes `CloneAndSendRequestEvent`.
6. **Quick-Add pin icon:** Next to each field in Tree view, clicking pins the field as a column in the source grid via `AddColumnRequestEvent`.

### Success Conditions

1. **Manual test** — switching tabs renders the correct view (tree, table, JSON, etc.).
2. **Manual test** — in Linked mode, selecting a row in the source grid updates the inspector.
3. **Manual test** — in Detached mode, selecting rows does NOT update the inspector.
4. **Manual test** — Clone to Send opens a Send panel pre-populated with the sample.
5. **Unit test** — `DetailPanel_Debounce_WaitsBeforeRender`:
   - Publish 10 `SampleSelectedEvent` in rapid succession (1ms apart). Assert the render callback was invoked ≤ 2 times (initial + one debounced).

---

## DMON-023 — Hover JSON Tooltip

**Phase:** 2 — Blazor Shell & Core UI  
**Design ref:** [§10.5](./DESIGN.md#105-sample-detail-inspector)

### Description

When a string field in the Inspector or Data Grid contains valid JSON, hovering over it displays a floating tooltip with syntax-highlighted, pretty-printed JSON.

Implementation:
1. On `@onmouseenter`, check if the cell value is a string starting with `{` or `[`.
2. Attempt `JsonDocument.Parse()`. If valid, render into a **Global Tooltip Portal** — a single absolute-positioned `<div id="tooltip-root">` at the document root (avoids DOM bloat from per-cell tooltips).
3. Apply syntax highlighting via CSS classes on JSON tokens.
4. On `@onmouseleave`, hide the tooltip.

### Success Conditions

1. **Manual test** — hovering over a JSON string field shows a formatted, colored tooltip.
2. **Manual test** — hovering over a non-JSON string does nothing extra.
3. **Unit test** — `HoverTooltip_ValidJson_ParsesWithoutError`:
   - Pass `"{\"key\": 42}"` to the detection logic. Assert it returns `true`.
4. **Unit test** — `HoverTooltip_InvalidJson_ReturnsFalse`:
   - Pass `"just a string"`. Assert `false`.

---

## DMON-024 — Text View Panel

**Phase:** 2 — Blazor Shell & Core UI  
**Design ref:** [§10.6](./DESIGN.md#106-text-view-panel)

### Description

Implement `TextViewPanel.razor` — a floating window for large string values:

1. Opened from Inspector context menu ("Show in Separate Window").
2. Auto-detects JSON (starts with `{` or `[`) and formats with indentation.
3. Mode toggles: Plain Text / JSON.
4. Read-only by default, with an optional "Edit" toggle if the caller provides a setter.
5. Syntax highlighting for JSON mode via `highlight.js` or CSS-based tokenizer.

### Success Conditions

1. **Manual test** — opening a JSON string shows formatted output.
2. **Manual test** — toggling to Plain Text removes formatting.
3. **Manual test** — large strings (10KB+) render without visible lag.

---

## DMON-025 — Keyboard Navigation

**Phase:** 2 — Blazor Shell & Core UI  
**Design ref:** [§10.4](./DESIGN.md#104-sample-list-data-grid)

### Description

Implement keyboard navigation in the Samples Panel:

1. **Arrow Up/Down:** Move selection (highlighted row) by one row.
2. **Page Up/Down:** Move selection by one visible page.
3. **Home/End:** Jump to first/last sample.
4. **Enter:** Open detail view for selected sample (same as double-click).
5. In Track mode, arrow key movement publishes `SampleSelectedEvent` (debounced at 50ms).
6. Focus management: clicking inside a panel gives it keyboard focus (via JS `focus()`).

### Success Conditions

1. **Manual test** — arrow keys move the highlight row by row.
2. **Manual test** — Page Up/Down scrolls by a page.
3. **Manual test** — linked inspector updates as arrow keys move (with debounce).

---

## DMON-026 — Context Menu System

**Phase:** 2 — Blazor Shell & Core UI  
**Design ref:** [§10.4](./DESIGN.md#104-sample-list-data-grid), [§10.5](./DESIGN.md#105-sample-detail-inspector)

### Description

Implement a reusable right-click context menu component `ContextMenu.razor`:

1. Rendered as a portal (absolute-positioned `<div>` at document root).
2. Opened on `@oncontextmenu` (prevent default browser menu).
3. Menu items: text label + optional icon + `Action` callback.
4. Auto-close on click-outside or Escape.
5. **Data Grid context menu items:**
   - "Show Detail (New Window)" — spawns detached `DetailPanel`
   - "Clone to Send/Emulator" — fires `CloneAndSendRequestEvent`
6. **Inspector context menu items:**
   - "Copy to Clipboard" — `navigator.clipboard.writeText(value)`
   - "Show in Separate Window" — spawns `TextViewPanel`

### Success Conditions

1. **Manual test** — right-click in grid shows the menu at cursor position.
2. **Manual test** — clicking a menu item performs the action and closes the menu.
3. **Manual test** — clicking outside the menu closes it.
4. **Manual test** — "Copy to Clipboard" copies the correct value.

---

## DMON-027 — Dark/Light Theme Toggle

**Phase:** 2 — Blazor Shell & Core UI  
**Design ref:** [§15.6](./DESIGN.md#156-darklight-theme-toggle)

### Description

Implement CSS-based theming:

1. Define CSS custom properties (variables) for all colors in `:root` and `[data-theme="dark"]`.
2. Default to dark theme (VS Code-inspired palette).
3. Toggle button in the top bar switches `data-theme` attribute on `<html>`.
4. Persist preference in `localStorage`.

### Success Conditions

1. **Manual test** — dark theme is applied by default.
2. **Manual test** — clicking toggle switches to light theme immediately.
3. **Manual test** — refreshing the browser preserves the last selected theme.

---

## DMON-028 — Workspace Persistence (Save/Load Layout)

**Phase:** 2 — Blazor Shell & Core UI  
**Design ref:** [§8.5](./DESIGN.md#85-state-persistence)

### Description

Implement auto-save and manual save/load for the panel workspace:

1. **Auto-save** — on every panel move/resize/close, debounced to 2 seconds, serialize to `workspace.json`.
2. **Manual save/load** — File picker dialogs for saving/loading workspace files.
3. **Browser refresh** — on Blazor circuit reconnect, reload from `workspace.json` and restore all panels to saved positions.
4. Serialized data includes: all `PanelState` items with positions, sizes, z-indices, and `ComponentState` dictionaries.

### Success Conditions

1. **Manual test** — arrange panels → refresh browser → panels restore to exact positions.
2. **Unit test** — `WorkspacePersistence_SerializeDeserialize_RoundTrips`:
   - Create a list of `PanelState`, serialize to JSON, deserialize. Assert equality.

---

<!-- ═══════════════════════════════════════════════════════════════════ -->
<!-- PHASE 3 — ADVANCED UI FEATURES                                    -->
<!-- ═══════════════════════════════════════════════════════════════════ -->

## DMON-029 — Visual Filter Builder

**Phase:** 3 — Advanced UI  
**Design ref:** [§10.7](./DESIGN.md#107-visual-filter-builder)

### Description

Implement `FilterBuilderPanel.razor` — a no-code tree-based UI for building complex boolean filters:

1. **Recursive AST** — AND/OR group nodes with leaf condition nodes.
2. **Group operations:** `[+ Condition]` adds a leaf, `[+ Sub-Group]` adds a nested AND/OR group, `[X]` removes the node.
3. **Leaf condition UI:**
   - Field selector via Topic Picker (two-column search).
   - Operator dropdown (context-aware: numbers get `>`, `<`, `>=`, `<=`; strings get `StartsWith`, `EndsWith`, `Contains`; booleans/enums get `==`, `!=`).
   - Value input adapts to type (enum → dropdown, boolean → checkbox, datetime → date picker).
   - Negation toggle `[!]` wraps the condition in `NOT`.
4. **Save/Load** — export/import filter as `.samplefilter` JSON.
5. **Apply** — calls `RootNode.ToDynamicLinqString()` → `FilterCompiler.Compile()` → applies to the target grid.

### Success Conditions

1. **Unit test** — `FilterNode_ToDynamicLinq_SimpleCondition`:
   - Build `Payload.Id == 42`. Assert output string is `"Payload.Id == 42"`.
2. **Unit test** — `FilterNode_ToDynamicLinq_NestedAndOr`:
   - Build `AND(Payload.Id == 42, OR(TopicName == "A", TopicName == "B"))`. Assert valid Dynamic LINQ string.
3. **Unit test** — `FilterNode_ToDynamicLinq_NegatedCondition`:
   - Build `NOT(Payload.Active == true)`. Assert output contains `"!"` or `"not"`.
4. **Manual test** — the full builder UI works, Apply filters the grid, Clear resets.

---

## DMON-030 — Expand All Mode (JSON Tree per Row)

**Phase:** 3 — Advanced UI  
**Design ref:** [§10.4](./DESIGN.md#104-sample-list-data-grid)

### Description

Add a toggle to the SamplesPanel toolbar that switches from table view to a vertically scrolling list of fully expanded, colored JSON trees — one per sample. Still virtualized via `<Virtualize>`.

Each expanded item shows:
- Ordinal, Timestamp, Status in a compact header.
- Full `SampleData.Payload` rendered as a color-coded recursive tree (reusing `DetailPanel`'s tree renderer).

### Success Conditions

1. **Manual test** — toggling Expand All switches the view.
2. **Manual test** — scrolling through 10,000+ expanded samples is smooth.
3. **Manual test** — toggling back to table view restores the previous column layout.

---

## DMON-031 — Grid Settings Export/Import

**Phase:** 3 — Advanced UI  
**Design ref:** [§10.4](./DESIGN.md#104-sample-list-data-grid)

### Description

Add toolbar buttons to SamplesPanel for saving/loading panel configuration:

**Exported state (`.samplepanelsettings` JSON):**
- Column list and order
- Sort field and direction
- Active filter text
- Panel dimensions (optional)

### Success Conditions

1. **Unit test** — `GridSettings_SerializeDeserialize_RoundTrips`:
   - Create settings, serialize, deserialize. Assert all fields match.
2. **Manual test** — export → close panel → open new panel → import → columns and filter are restored.

---

## DMON-032 — Sparkline Charts in Topic Explorer

**Phase:** 3 — Advanced UI  
**Design ref:** [§15.4](./DESIGN.md#154-visual-message-frequency-sparklines)

### Description

Add a mini sparkline chart next to each topic in the Topic Explorer, showing messages-per-second over the last 10 seconds.

1. Maintain a rolling `RingBuffer<int>` per topic (10 slots, 1 per second).
2. Each second, record the number of new samples received for that topic.
3. Render as an inline SVG `<polyline>` or CSS-driven bar chart (no external charting library required).

### Success Conditions

1. **Manual test** — sparklines appear and update live as data flows.
2. **Unit test** — `RingBuffer_RecordsCorrectFrequency`:
   - Add 50 samples in the first second, 30 in the second. Assert buffer values are `[50, 30, 0, ...]`.

---

## DMON-033 — Quick-Add Column from Inspector

**Phase:** 3 — Advanced UI  
**Design ref:** [§15.3](./DESIGN.md#153-quick-add-columns-from-inspector)

### Description

In the Inspector's Tree view, render a small pin/plus icon next to each field. Clicking it fires `AddColumnRequestEvent(targetPanelId, fieldPath)` to the source grid, which adds that field as a new column without opening the Column Picker.

### Success Conditions

1. **Manual test** — clicking the pin icon in the Inspector adds the column to the linked grid.
2. **Manual test** — the grid immediately re-renders with the new column visible.

---

<!-- ═══════════════════════════════════════════════════════════════════ -->
<!-- PHASE 4 — OPERATIONAL TOOLS                                       -->
<!-- ═══════════════════════════════════════════════════════════════════ -->

## DMON-034 — Send Sample Panel (Message Emulator)

**Phase:** 4 — Operational Tools  
**Design ref:** [§10.8](./DESIGN.md#108-send-sample-message-emulator)

### Description

Implement `SendSamplePanel.razor`:

1. **Topic selection** via `TopicPicker`. On selection, creates a blank instance via `Activator.CreateInstance`.
2. **Dynamic form** using the recursive `<DynamicObjectEditor>` architecture from [§11](./DESIGN.md#11-dynamic-form-architecture).
3. **Clone workflow** — when spawned from "Clone to Send" event, the payload is deep-copied via JSON round-trip.
4. **Bind controls** dynamically: booleans → checkboxes, enums → dropdowns, numbers → numeric inputs, all driven by `Fasterflect` setters.
5. **Collection editing** — arrays/lists render with per-item editors, `[+ Add]` and `[X Remove]`.
6. **[Send]** button calls `IDynamicWriter.Write(payload)`.
7. **[Send Dispose]** button calls `IDynamicWriter.DisposeInstance(payload)` — only enabled for keyed topics.

### Success Conditions

1. **Manual test** — selecting a topic renders an editable form with all fields.
2. **Manual test** — modifying fields and clicking Send writes a valid DDS sample (visible in another monitor instance or subscriber).
3. **Manual test** — Clone workflow pre-populates all fields correctly.
4. **Unit test** — `DynamicObjectEditor_CreatesCorrectInputTypes`:
   - For a type with int, bool, string, enum fields — assert the component tree contains numeric input, checkbox, text input, and dropdown respectively.

---

## DMON-035 — Dynamic Form Components (Recursive Editors)

**Phase:** 4 — Operational Tools  
**Design ref:** [§11](./DESIGN.md#11-dynamic-form-architecture)

### Description

Implement the three recursive form components:

1. **`<DynamicObjectEditor>`** — iterates `TopicMetadata.AllFields`. For each field:
   - Primitive/enum → `<DynamicPrimitiveEditor>`
   - Collection → `<DynamicCollectionEditor>`
   - Nested struct → recursive `<DynamicObjectEditor>`
2. **`<DynamicPrimitiveEditor>`** — switches on `TypeCode`: checkbox for `bool`, numeric input for `int/double/float`, text for `string`, dropdown for `Enum`.
3. **`<DynamicCollectionEditor>`** — reads `IList` from payload, renders per-item editors with `[+ Add]` and `[X Remove]`.

Both Send (read-write) and Inspector (read-only) panels use these components, differing only in the `IsReadOnly` parameter.

### Success Conditions

1. **Unit test** — `DynamicPrimitiveEditor_Bool_RendersCheckbox`:
   - Render with a `bool` field. Assert output HTML contains `<input type="checkbox">`.
2. **Unit test** — `DynamicPrimitiveEditor_Enum_RendersDropdown`:
   - Render with an `enum` field. Assert output HTML contains `<select>` with enum values as `<option>`.
3. **Unit test** — `DynamicCollectionEditor_List_RendersItems`:
   - Render with a `List<int>` containing 3 items. Assert 3 item editors and an Add button are present.
4. **Unit test** — `DynamicObjectEditor_NestedType_RecursesCorrectly`:
   - Type with `Inner { string Name; }` inside `Outer { Inner Child; }`. Assert the Name field is rendered.

---

## DMON-036 — Custom Type Drawer Registry

**Phase:** 4 — Operational Tools  
**Design ref:** [§11.2](./DESIGN.md#112-custom-type-drawers)

### Description

Implement `ICustomComponentRegistry` allowing plugins to register type-specific editors and viewers:

```csharp
public interface ICustomComponentRegistry
{
    void RegisterEditor<T>(Type blazorComponentType);
    void RegisterViewer<T>(Type blazorComponentType);
    Type? GetEditor(Type valueType);
    Type? GetViewer(Type valueType);
}
```

The `<DynamicPrimitiveEditor>` checks this registry before falling back to the default input.

### Success Conditions

1. **Unit test** — `CustomComponentRegistry_RegisterAndRetrieve`:
   - Register a component for `Vector3`. Call `GetEditor(typeof(Vector3))`. Assert it returns the registered type.
2. **Unit test** — `CustomComponentRegistry_FallbackToNull_ForUnregistered`:
   - Call `GetEditor(typeof(string))` without prior registration. Assert `null`.

---

## DMON-037 — Export Service (Streaming JSON Write)

**Phase:** 4 — Operational Tools  
**Design ref:** [§10.9](./DESIGN.md#109-export--import)

### Description

Implement `ImportExportService.ExportAsync()`:

1. Accepts `IReadOnlyList<SampleData>` (or the full `SampleStore`) and a target file path.
2. Uses `Utf8JsonWriter` coupled to a `FileStream` for near-zero memory footprint.
3. Writes each sample as a JSON object including `TopicName` (polymorphic type tag), `Ordinal`, `Timestamp`, `Payload` (serialized via `System.Text.Json`), and `SampleInfo`.
4. Reports progress via `IProgress<ExportProgress>` (e.g. `{ Exported: 5000, Total: 14000 }`).
5. Supports `CancellationToken` for cancel.

Output format: `.samples` file (JSON array, one object per sample).

### Success Conditions

1. **Unit test** — `Export_WritesAllSamples`:
   - Export 100 samples to a temp file. Read the file back. Assert 100 JSON objects.
2. **Unit test** — `Export_IncludesTopicName`:
   - Export a sample with `TopicName = "RobotState"`. Assert the JSON contains `"TopicName": "RobotState"`.
3. **Unit test** — `Export_CancellationStopsEarly`:
   - Cancel after 50 samples. Assert the file contains ≤ 50 objects.
4. **Unit test** — `Export_ProgressReportsCorrectly`:
   - Export 100 samples. Assert progress callback was invoked with increasing `Exported` values.

---

## DMON-038 — Import Service (Streaming JSON Read)

**Phase:** 4 — Operational Tools  
**Design ref:** [§10.9](./DESIGN.md#109-export--import)

### Description

Implement `ImportExportService.ImportAsync()`:

1. Opens a `.samples` JSON file and streams via `Utf8JsonReader` / `JsonSerializer.DeserializeAsyncEnumerable`.
2. For each JSON object, reads `TopicName`, looks up the `Type` in `ITopicRegistry`, deserializes `Payload` to the correct type.
3. Halts live DDS ingestion (pauses all `IDynamicReader` instances).
4. Clears `SampleStore` and `InstanceStore`.
5. Pushes deserialized `SampleData` into the same `Channel<SampleData>` pipeline. All filtering/sorting/UI updates work automatically.
6. Reports progress and supports cancellation.

### Success Conditions

1. **Unit test** — `Import_RestoresAllSamples`:
   - Export 100 samples, import the file. Assert `SampleStore.AllSamples.Count == 100`.
2. **Unit test** — `Import_ClearsPreviousData`:
   - Pre-populate store with 50 samples. Import a file with 30. Assert count is 30 (not 80).
3. **Unit test** — `Import_RoundTripPreservesPayload`:
   - Export, import, compare payload field values. Assert exact match.
4. **Unit test** — `Import_UnknownTopicName_SkipsGracefully`:
   - Import a file referencing a topic not in the registry. Assert it skips without crashing.

---

## DMON-039 — Replay Engine

**Phase:** 4 — Operational Tools  
**Design ref:** [§10.10](./DESIGN.md#1010-replay-engine)

### Description

Implement `PlaybackEngine`:

```csharp
public interface IPlaybackEngine
{
    PlaybackState State { get; }             // Stopped, Playing, Paused
    int CurrentIndex { get; }
    int TotalSamples { get; }
    double PlaybackRate { get; set; }        // 0.01 to 100.0, or double.MaxValue for MAX

    Task LoadAsync(string filePath, CancellationToken ct);
    Task PlayAsync(CancellationToken ct);
    void Pause();
    void Step();                             // Advance one sample
    void Stop();
    void SeekTo(int sampleIndex);

    int? PauseOnOrdinal { get; set; }        // Pause when reaching this ordinal

    IReadOnlyList<SampleData> ReplayBuffer { get; }
    Task DisposeAllSentInstancesAsync();     // Sends DDS Dispose for all keyed instances written during replay
}
```

**Timing logic:**
```
while (index < total && !cancelled):
    sample = buffer[index]
    if rate != MAX:
        delay = (buffer[index+1].Timestamp - sample.Timestamp) / rate
        await Task.Delay(delay)
    writer.Write(sample.Payload)
    if PauseOnOrdinal == sample.Ordinal:
        state = Paused; return
    index++
```

### Success Conditions

1. **Unit test** — `PlaybackEngine_PlayAtMaxSpeed_CompletesQuickly`:
   - Load 1000 samples. Play at MAX speed. Assert completes in < 2 seconds.
2. **Unit test** — `PlaybackEngine_PauseOnOrdinal_StopsAtCorrectSample`:
   - Set `PauseOnOrdinal = 500`. Play. Assert `CurrentIndex` points to sample with `Ordinal == 500` and `State == Paused`.
3. **Unit test** — `PlaybackEngine_Step_AdvancesOneAtATime`:
   - Call `Step()` three times. Assert `CurrentIndex` increments by 1 each time.
4. **Unit test** — `PlaybackEngine_SeekTo_JumpsToIndex`:
   - Load 1000 samples. `SeekTo(500)`. Assert `CurrentIndex == 500`.
5. **Unit test** — `PlaybackEngine_DisposeAllSentInstances_SendsDisposeMessages`:
   - Mock `IDynamicWriter`. Play keyed samples. Call `DisposeAllSentInstancesAsync()`. Assert `DisposeInstance` was called for each unique key.

---

## DMON-040 — Replay Panel UI

**Phase:** 4 — Operational Tools  
**Design ref:** [§10.10](./DESIGN.md#1010-replay-engine)

### Description

Implement `ReplayPanel.razor`:

1. Displays file name, total samples, total duration.
2. Transport controls: Play, Pause, Step, Stop.
3. Speed slider: 0.01x to 100x plus MAX.
4. Pause-on-Ordinal input field with Clear button.
5. Timeline scrubber: draggable progress bar bound to `CurrentIndex`.
6. **[Open Slaved Grid]** — spawns a `SamplesPanel` bound to `ReplayBuffer` instead of live network.
7. **[Dispose Sent Instances]** — calls `PlaybackEngine.DisposeAllSentInstancesAsync()`.

### Success Conditions

1. **Manual test** — play/pause/stop/step buttons control playback correctly.
2. **Manual test** — speed slider changes playback rate in real-time.
3. **Manual test** — dragging the timeline scrubber jumps to the correct position.
4. **Manual test** — Slaved Grid shows replay data, supports filtering/sorting.

---

<!-- ═══════════════════════════════════════════════════════════════════ -->
<!-- PHASE 5 — PLUGIN ARCHITECTURE                                     -->
<!-- ═══════════════════════════════════════════════════════════════════ -->

## DMON-041 — Plugin Loading Infrastructure

**Phase:** 5 — Plugin Architecture  
**Design ref:** [§12](./DESIGN.md#12-plugin-architecture)

### Description

Implement the plugin loading system:

1. **`IMonitorPlugin` interface** as defined in [§12.1](./DESIGN.md#121-plugin-contract).
2. **`IMonitorContext` interface** providing access to live data, stores, and UI registries.
3. **`PluginLoader`** that:
   - Enumerates `.dll` files in configured plugin directories.
   - Loads each into its own collectible `AssemblyLoadContext`.
   - Discovers types implementing `IMonitorPlugin`.
   - Calls `ConfigureServices()` during DI setup.
   - Calls `Initialize()` after host starts.
4. Handle load failures gracefully (log error, skip plugin, don't crash).

### Success Conditions

1. **Unit test** — `PluginLoader_LoadsPluginFromDirectory`:
   - Place a test plugin DLL in a temp directory. Load. Assert the plugin's `Name` and `Version` are accessible.
2. **Unit test** — `PluginLoader_ConfigureServices_Called`:
   - Mock `IServiceCollection`. Load plugin. Assert `ConfigureServices` was invoked.
3. **Unit test** — `PluginLoader_Initialize_ReceivesContext`:
   - Load plugin. Call Initialize. Assert the plugin received an `IMonitorContext` with valid properties.
4. **Unit test** — `PluginLoader_BadDll_SkipsGracefully`:
   - Place a corrupt DLL in the directory. Assert loading completes without exception and the bad DLL is skipped.

---

## DMON-042 — Plugin Panel Registration

**Phase:** 5 — Plugin Architecture  
**Design ref:** [§12.3](./DESIGN.md#123-plugin-capabilities)

### Description

Extend `IWindowManager.RegisterPanelType(name, blazorType)` to allow plugins to register custom panel types. These panels:

1. Appear in a "Plugin Panels" menu in the top bar.
2. Can be spawned like any built-in panel.
3. Receive DI services from the host container.

### Success Conditions

1. **Unit test** — `WindowManager_RegisterPanelType_AddsToAvailable`:
   - Register a type. Assert `RegisteredPanelTypes` contains the new entry.
2. **Unit test** — `WindowManager_SpawnRegisteredPlugin_CreatesPanel`:
   - Register, spawn. Assert `ActivePanels` contains a panel of the registered type.

---

## DMON-043 — Plugin Menu Registration

**Phase:** 5 — Plugin Architecture  
**Design ref:** [§12.3](./DESIGN.md#123-plugin-capabilities)

### Description

Implement `IMenuRegistry` for plugins to add items to the application's top menu bar:

```csharp
public interface IMenuRegistry
{
    void AddMenuItem(string menuPath, string label, Action onClick);
    void AddMenuItem(string menuPath, string label, Func<Task> onClickAsync);
}
```

Menu path format: `"Plugins/BDC/Show Entities"` → creates nested menu structure.

### Success Conditions

1. **Unit test** — `MenuRegistry_AddItem_AppearsInMenu`:
   - Add `"Plugins/Test/Do Thing"`. Assert the menu tree contains the item.
2. **Manual test** — clicking the registered menu item invokes the callback.

---

## DMON-044 — IFormatterRegistry (Custom Value Formatters)

**Phase:** 5 — Plugin Architecture  
**Design ref:** [§12.3](./DESIGN.md#123-plugin-capabilities)

### Description

Implement `IFormatterRegistry`:

```csharp
public interface IFormatterRegistry
{
    void RegisterFormatter<T>(Func<T, string> formatter);
    string Format(object value);    // Falls back to ToString()
}
```

Data Grid cells and Inspector values check this registry before defaulting to `ToString()`.

### Success Conditions

1. **Unit test** — `FormatterRegistry_RegisteredType_FormatsCorrectly`:
   - Register a formatter for `DateTime` that outputs ISO 8601. Assert `Format(dateValue)` matches expected string.
2. **Unit test** — `FormatterRegistry_UnregisteredType_FallsBackToString`:
   - Call `Format(42)` without registering an `int` formatter. Assert result is `"42"`.

---

<!-- ═══════════════════════════════════════════════════════════════════ -->
<!-- PHASE 6 — DOMAIN ENTITY PLUGINS (BDC / TKB)                      -->
<!-- ═══════════════════════════════════════════════════════════════════ -->

## DMON-045 — EntityStore Core (Aggregation Engine)

**Phase:** 6 — Domain Entity Plugins  
**Design ref:** [§13.1](./DESIGN.md#131-concepts--glossary), [§13.2](./DESIGN.md#132-entity-states), [§13.3](./DESIGN.md#133-aggregation-algorithm)

### Description

Implement the generic `EntityStore` (usable by both BDC and TKB plugins):

1. Subscribes to `IInstanceStore.OnInstanceChanged`.
2. Filters events by a configurable namespace prefix (e.g. `"company.BDC."` or `"company.TKB."`).
3. Extracts `EntityId` (Key1) and optional `PartId` (Key2) from the sample.
4. Maintains `Dictionary<int, Entity>` mapping EntityId → aggregated Entity.
5. Each Entity tracks:
   - `Descriptors: Dictionary<DescriptorIdentity, SampleData>` (latest sample per descriptor)
   - `State: EntityState { Alive, Zombie, Dead }`
   - `Journal: List<EntityJournalRecord>`
6. State evaluation: has Master → Alive; has descriptors but no Master → Zombie; empty → Dead.

### Success Conditions

1. **Unit test** — `EntityStore_NewMasterDescriptor_CreatesAliveEntity`:
   - Process a Master alive sample. Assert entity exists with `State == Alive`.
2. **Unit test** — `EntityStore_NonMasterOnly_CreatesZombieEntity`:
   - Process a non-master descriptor (no Master seen). Assert `State == Zombie`.
3. **Unit test** — `EntityStore_DisposeMaster_TransitionsToZombie`:
   - Add Master + non-master. Dispose Master. Assert `State == Zombie`.
4. **Unit test** — `EntityStore_DisposeAllDescriptors_TransitionsToDead`:
   - Add Master + non-master. Dispose both. Assert `State == Dead`.
5. **Unit test** — `EntityStore_MultiInstanceDescriptor_TracksPartIdsSeparately`:
   - Process two samples for the same EntityId + same topic but different PartId. Assert `entity.Descriptors.Count == 2` (plus others).
6. **Unit test** — `EntityStore_Journal_RecordsTransitions`:
   - Exercise the full lifecycle (birth → update → dispose). Assert journal entries match expected transitions.

---

## DMON-046 — BDC Entity Grid Panel

**Phase:** 6 — Domain Entity Plugins  
**Design ref:** [§13.4](./DESIGN.md#134-entity-ui--live-grid-bdc)

### Description

Implement `BdcEntityGridPanel.razor`:

1. Lists all BDC entities from the `EntityStore`.
2. Columns: Entity ID, State (icon), Info Name, Last Update, Actions.
3. **Virtual columns** can pull values from any aggregated descriptor (e.g. `Info.Name` from the `EntityInfo` topic).
4. **Live/History toggle** switches between current entities and chronological `EntityJournal`.
5. **[Detail] button** spawns an `EntityDetailPanel` for the selected entity.
6. Virtualized and sortable like `SamplesPanel`.

### Success Conditions

1. **Manual test** — entities appear and update live.
2. **Manual test** — state icons (🟢🟡⚫) reflect the correct entity state.
3. **Manual test** — Live/History toggle switches views.
4. **Manual test** — Detail button opens the entity inspection panel.

---

## DMON-047 — TKB Entity Folder Tree Panel

**Phase:** 6 — Domain Entity Plugins  
**Design ref:** [§13.5](./DESIGN.md#135-entity-ui--folder-tree-tkb)

### Description

Implement `TkbEntityTreePanel.razor`:

1. Reads `master.Path` and `master.Name` to build folder hierarchy.
2. **Flat/Tree toggle** — flat list vs. hierarchical tree.
3. Rendered as flat rows with CSS `padding-left` for indentation (virtualized).
4. Each entity row shows state icon, name, ID, and a Detail button.

### Success Conditions

1. **Manual test** — tree structure matches the Path values.
2. **Manual test** — collapsing/expanding folders works.
3. **Manual test** — Flat toggle shows a simple list.

---

## DMON-048 — Entity Detail Inspector

**Phase:** 6 — Domain Entity Plugins  
**Design ref:** [§13.6](./DESIGN.md#136-entity-detail-inspector)

### Description

Implement `EntityDetailPanel.razor`:

1. Displays entity state, last update timestamp.
2. Lists each descriptor with its latest sample rendered as a collapsible tree (reusing `<DynamicObjectEditor>` in read-only mode).
3. **[⏱️ Hist] button** spawns a `SamplesPanel` pre-filtered to `TopicType == X AND EntityId == Y` since last entity birth.
4. **[Show All Entity Samples]** spawns a `SamplesPanel` with all descriptors for this entity.
5. Multi-instance descriptors grouped by `PartId`.

### Success Conditions

1. **Manual test** — all descriptors for an entity are listed.
2. **Manual test** — Hist button opens a correctly filtered samples panel.
3. **Manual test** — multi-instance descriptors each show their PartId.

---

## DMON-049 — Historical State (Time-Travel)

**Phase:** 6 — Domain Entity Plugins  
**Design ref:** [§13.7](./DESIGN.md#137-historical-state-time-travel)

### Description

Implement the time-travel algorithm as detailed in [§13.7](./DESIGN.md#137-historical-state-time-travel):

1. Input: `TargetEntityId`, `TargetTimestamp T`.
2. Query `EntityJournal` for the most recent "Master Added" before T.
3. For each known descriptor topic, binary-search `SampleStore` for the latest sample before T with matching EntityId.
4. For multi-instance, continue searching for each unique PartId.
5. Discard descriptors whose found sample is a Dispose.
6. Render in a frozen "Historical State" panel with a banner: `"⚠️ YOU ARE VIEWING A FROZEN HISTORICAL SNAPSHOT"`.

### Success Conditions

1. **Unit test** — `TimeTravel_FindsCorrectDescriptorsAtTimestamp`:
   - Populate store with samples at T=1, T=2, T=3. Query at T=2.5. Assert returned descriptors are latest-before-T versions.
2. **Unit test** — `TimeTravel_ExcludesDisposedDescriptors`:
   - Descriptor disposed at T=1.5. Query at T=2. Assert that descriptor is not in the result.
3. **Unit test** — `TimeTravel_EntityDeadAtTimestamp_ReturnsEmpty`:
   - Master disposed at T=1. Query at T=2 (no rebirth). Assert empty result or error.
4. **Unit test** — `TimeTravel_MultiInstance_FindsAllPartIds`:
   - Two PartIds for the same descriptor. Query. Assert both are returned.

---

<!-- ═══════════════════════════════════════════════════════════════════ -->
<!-- PHASE 7 — POLISH & UX ENHANCEMENTS                                -->
<!-- ═══════════════════════════════════════════════════════════════════ -->

## DMON-050 — Remote Monitoring Support

**Phase:** 7 — Polish & UX Enhancements  
**Design ref:** [§15.1](./DESIGN.md#151-remote-monitoring)

### Description

Configure Kestrel to listen on all network interfaces (`0.0.0.0`) instead of only `localhost`, controlled by a configuration flag:

```json
{ "DdsSettings": { "AllowRemoteAccess": false } }
```

When enabled, also add:
1. A status bar indicator showing the URL clients can connect to.
2. Basic IP whitelist support (optional, configurable).

### Success Conditions

1. **Manual test** — with `AllowRemoteAccess: true`, connecting from another machine on the LAN loads the UI.
2. **Manual test** — with `AllowRemoteAccess: false`, remote connections are refused.
3. **Unit test** — `KestrelConfig_RemoteAccessTrue_BindsToAllInterfaces`:
   - Assert the configured endpoints include `0.0.0.0` or `*`.

---

## DMON-051 — Multi-Tab Workspace Isolation

**Phase:** 7 — Polish & UX Enhancements  
**Design ref:** [§15.2](./DESIGN.md#152-multi-monitor-via-browser-tabs)

### Description

Ensure that each browser tab/window gets its own isolated `IWindowManager` and `IWorkspaceState` (via Scoped DI), while sharing the same Singleton data stores.

1. Verify the Scoped lifetime for `WindowManager`/`WorkspaceState` works correctly per Blazor circuit.
2. Each tab can have independent panel layouts.
3. Shared data: one tab subscribing to a topic makes data visible in all tabs.

### Success Conditions

1. **Manual test** — open two tabs. Arrange panels differently in each. Verify they don't interfere.
2. **Manual test** — subscribe to a topic in tab 1. Tab 2 also sees the data in `SampleStore`.

---

## DMON-052 — Unified Search Bar (Power User Text Filter)

**Phase:** 7 — Polish & UX Enhancements  
**Design ref:** [§15.5](./DESIGN.md#155-unified-search-bar)

### Description

Add a simple text input at the top of each `SamplesPanel` (in addition to the Visual Filter Builder) where power users can type Dynamic LINQ expressions directly. Features:

1. Syntax error feedback: red border + error tooltip on invalid expression.
2. Auto-complete hints for known field names (optional stretch goal).
3. Enter to apply. Escape or empty to clear.

### Success Conditions

1. **Manual test** — typing `Ordinal > 50` and pressing Enter filters the grid.
2. **Manual test** — typing invalid syntax shows a red border and error message.
3. **Unit test** — already covered by `FilterCompiler` tests (DMON-012).

---

## DMON-053 — Notification Toast System

**Phase:** 7 — Polish & UX Enhancements

### Description

Implement a lightweight notification toast system for surfacing non-blocking status messages:

1. Import/Export progress and completion.
2. Plugin load success/failure.
3. DDS connection events (partition changed, reader created/destroyed).
4. Filter compilation errors.

Toasts appear in the bottom-right corner, stack vertically, auto-dismiss after 5 seconds (configurable), and can be manually dismissed with a close button.

**Severity levels:** Info (blue), Success (green), Warning (yellow), Error (red).

### Success Conditions

1. **Manual test** — toasts appear and auto-dismiss after 5 seconds.
2. **Manual test** — clicking the close button dismisses immediately.
3. **Unit test** — `ToastService_Show_AddsToQueue`:
   - Call `Show("message", Severity.Info)`. Assert the queue contains the toast.

---

## DMON-054 — Drag-and-Drop Panel Docking

**Phase:** 7 — Polish & UX Enhancements

### Description

Enhance the Web Desktop with dock-to-edge support:

1. When a panel is dragged near a screen edge (within 20px), show a dock preview highlight.
2. Releasing the mouse docks the panel to fill 50% of that edge (left, right, top, bottom).
3. Dragging away from the edge undocks and restores free-floating mode.
4. Double-clicking the title bar maximizes/restores the panel.

### Success Conditions

1. **Manual test** — dragging to the left edge docks the panel to the left half.
2. **Manual test** — dragging away restores free-floating.
3. **Manual test** — double-click title bar maximizes; double-click again restores.

---

## DMON-055 — Sample Diff View

**Phase:** 7 — Polish & UX Enhancements

### Description

Add a "Compare with Previous" feature to the Inspector and Data Grid:

1. Select two samples (Ctrl+Click in grid, or "Compare" from context menu).
2. Open a `DiffPanel.razor` showing a side-by-side or unified diff of the two payloads.
3. Changed fields are highlighted (added = green, removed = red, modified = yellow).
4. Unchanged fields are collapsed by default but expandable.

### Success Conditions

1. **Manual test** — selecting two samples and clicking Compare shows the diff.
2. **Unit test** — `PayloadDiff_DetectsChangedField`:
   - Two objects with one different field. Assert the diff result contains one changed entry.
3. **Unit test** — `PayloadDiff_IdenticalObjects_NoDifferences`:
   - Two identical objects. Assert diff is empty.

---

## DMON-056 — Keyboard Shortcut System

**Phase:** 7 — Polish & UX Enhancements

### Description

Implement a global keyboard shortcut system:

| Shortcut | Action |
|---|---|
| `Ctrl+Shift+T` | Open Topic Explorer |
| `Ctrl+Shift+S` | Open Send Sample panel |
| `Ctrl+Shift+R` | Open Replay panel |
| `Ctrl+F` | Focus filter input in active grid |
| `Ctrl+W` | Close active panel |
| `F11` | Toggle fullscreen for active panel |
| `Ctrl+D` | Toggle dark/light theme |

Shortcuts are intercepted via JS `document.addEventListener('keydown', ...)` and forwarded to Blazor via JS interop.

### Success Conditions

1. **Manual test** — each shortcut performs the expected action.
2. **Manual test** — shortcuts don't fire when typing in a text input.

---

## DMON-057 — Performance Metrics Dashboard

**Phase:** 7 — Polish & UX Enhancements

### Description

Add a collapsible performance metrics bar at the bottom of the Desktop showing:

1. **Samples/sec** — rolling 1-second average of ingested samples.
2. **Store size** — total number of samples in `SampleStore`.
3. **Memory** — `GC.GetTotalMemory()` formatted as MB.
4. **Active readers** — count of `DdsBridge.ActiveReaders`.
5. **UI render time** — time spent in the last `StateHasChanged()` cycle (measured via `Stopwatch`).

Metrics update at ~1 Hz (not 30 Hz) to avoid unnecessary DOM work.

### Success Conditions

1. **Manual test** — metrics bar is visible and updates every second.
2. **Manual test** — collapsing hides the bar; expanding restores it.
3. **Unit test** — `MetricsCollector_SamplesPerSecond_CalculatesCorrectly`:
   - Feed 100 samples in 1 second. Assert rate is ~100.
