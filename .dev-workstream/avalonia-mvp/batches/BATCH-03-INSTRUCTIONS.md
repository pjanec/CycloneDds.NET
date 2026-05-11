# BATCH-03 Instructions — Phase 4: Firehose UI + Debt Resolution

**Branch:** `ddsmon-avalonia`  
**Developer model:** Claude Sonnet 4.6  
**Design references:** DESIGN.md §8, TASK-DETAIL.md TASK-E001 and TASK-E002  
**Predecessor:** BATCH-02 committed at `e6b44d6`

---

## Context

BATCH-01 and BATCH-02 are committed and all 745 tests pass:
- Engine: 643 | Blazor: 16 | Avalonia.Core: 24 | Avalonia.Tests: 32 | StandardPlugin: 30

BATCH-03 delivers Phase 4 — Firehose UI. Two debt items are also addressed.

---

## Pre-Task: Debt Resolution

Before writing any new code, resolve the two debt items carried from earlier batches.

### DT-002 — Rename `blazorComponentType` param in `IWindowManager`

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/IWindowManager.cs` (or wherever `RegisterPanelType` is defined)

Find the parameter named `blazorComponentType` in `RegisterPanelType` (or `RegisterPanel`) and rename it to `viewModelType`. This is a straightforward rename — check all callers using the IDE rename tool or grep. Verify no tests break.

### DT-003 — Add `_HiddenSample` test type for IsHidden filter coverage

**File:** `tests/DdsMonitor.Avalonia.StandardPlugin.Tests/Stubs.cs` (or a new file `HiddenSampleType.cs` in the same test project)

Add a DDS-topic-annotated CLR type whose name starts with `_`:

```csharp
[DdsTopic]
public struct _HiddenSample
{
    [DdsKey] public int Id;
    public int Value;
}
```

Then add a test in `StandardPluginSuite.cs` in the `TopicExplorerViewModelTests` class:

```csharp
[Fact]
public void TopicExplorerViewModel_ShowHidden_False_DoesNotShowHiddenTopic()
{
    var registry = new StubTopicRegistry();
    registry.Register(new TopicMetadata(typeof(_HiddenSample)));  // ShortName = "_HiddenSample"

    var vm = CreateVm(topicRegistry: registry);
    vm.Initialize(new Dictionary<string, object>());

    // Hidden topic must not appear when ShowHidden=false
    Assert.Empty(vm.Topics);

    vm.ShowHidden = true;

    // Hidden topic must appear when ShowHidden=true
    Assert.Single(vm.Topics);
    Assert.Equal("_HiddenSample", vm.Topics[0].ShortName);
}
```

Run `dotnet test tests/DdsMonitor.Avalonia.StandardPlugin.Tests/` and confirm it passes before proceeding.

---

## Task 1 — TASK-E001: SamplesViewerPlugin

**Design Reference:** [DESIGN.md §8](../DESIGN.md#8-phase-4--firehose-ui)  
**Task Detail Reference:** [TASK-DETAIL.md TASK-E001](../TASK-DETAIL.md#task-e001--samplesviewerplugin-grid--filtering)

### New files in `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/`:

**`SamplesViewerPlugin.cs`**
- `IMonitorPlugin` class; in `Initialize`:
  - Subscribe to `SpawnPanelEvent` on UI thread (via `SubscribeOnUiThread<SpawnPanelEvent>`) where `ev.PanelTypeName == "SamplesViewer"`.
  - On event: `IWindowManager.SpawnPanel($"SamplesViewer_{ev.State?["TopicName"]}", ev.State)`.
  - Register `SamplesViewerViewModel` view factory in `IAvaloniaViewRegistry`.
  - Register `SamplesViewerView.axaml` factory.
- In `ConfigureServices`: no service registrations needed (ViewModel is created on-demand via DI).
- Store subscription token in `_spawnToken`; Plugin does NOT implement `IDisposable` (it lives for the app lifetime; token management is fine-grained only for panel ViewModels).

**`SamplesViewerViewModel.cs`**
- Constructor: `IFilterCompiler filterCompiler, ISampleStore? store = null, TopicMetadata? meta = null`  
  (store and meta are nullable because they are injected via state parameterisation, not directly via DI)
- Fields:
  - `ISampleView _view` (created in `Initialize` after meta is resolved)
  - `IFilterCompiler _filterCompiler`
  - `TopicMetadata? _meta`
  - `IDictionary<string, object>? _state`
  - `string _filterText = ""`
  - `string? _filterError = null`
  - `int _filteredCount = 0`
- Implements `IStatefulViewModel`:
  ```csharp
  public void Initialize(IDictionary<string, object> componentState)
  {
      _state = componentState;
      _filterText = componentState.GetValueOrDefault("FilterText", "");
      // Write discriminator key back so WindowManager can restore on restart
      if (_meta != null)
          componentState["TopicName"] = _meta.TopicName;
      if (_meta != null)
          StartView(_meta);
  }
  ```
- `StartView(TopicMetadata meta)`: creates `SampleView(ISampleStore)` — the store must be obtained via `IDdsBridge.GetOrCreateStore(meta)` or equivalent Engine API. Check the Engine for the correct way to obtain a topic's `ISampleStore` (look in `IDdsBridge` or `ITopicRegistry`). If needed, accept `ISampleStore` as constructor parameter.
- `OnViewRebuilt` subscription: `_view.OnViewRebuilt += OnViewRebuilt;` — fires on background thread. Handler:
  ```csharp
  private void OnViewRebuilt()
  {
      // Must NOT touch UI controls here — background thread
      var count = _view.CurrentFilteredCount;
      Dispatcher.UIThread.Post(() => { FilteredCount = count; }, DispatcherPriority.Normal);
  }
  ```
- `FilterText` property: on set → compile via `_filterCompiler.Compile(value, _meta)`:
  - If `result.IsValid`: `_view.SetFilter(result.Predicate)`, clear `FilterError`, update state dict.
  - If `!result.IsValid`: set `FilterError = result.ErrorMessage`, do NOT call `SetFilter`.
- `FilterError` property (string?): binds to an inline error label in the view.
- `FilteredCount` property (int): binds to a count label.
- `GetVirtualSlice(int start, int count)` method: returns `_view.GetVirtualView(start, count)`.
- Implements `IDisposable`:
  ```csharp
  public void Dispose()
  {
      _view?.OnViewRebuilt -= OnViewRebuilt;
      _view?.Dispose();
  }
  ```

**`SamplesViewerView.axaml` + `.axaml.cs`**
- `TextBox` bound to `FilterText`.
- `TextBlock` bound to `FilterError`, visible only when non-null/non-empty (use `IsVisible` binding or a converter).
- `TextBlock` showing `FilteredCount`.
- A `ListBox` (NOT `TreeDataGrid` for V1 — Avalonia `TreeDataGrid` virtualization requires a `HierarchicalDataGridSource` that is out of scope for V1; use a virtualized `ListBox` instead). Bind to a backing collection that gets refreshed when `OnViewRebuilt` fires.
  - The ListBox shows `SampleData.Info.ReceptionTimestamp` and `SampleData.TopicMetadata.ShortName` per row.

> **V1 Simplification Note:** Full `TreeDataGrid` virtualization is a Phase 7 polish item. For TASK-E001 V1, use a virtualized `ListBox` that fetches the top 200 samples from `_view.GetVirtualView(0, 200)` on each `OnViewRebuilt`. This is sufficient to prove the filtering pipeline and disposal lifecycle.

**Key constraints (mandatory):**
- `OnViewRebuilt` handler must NEVER touch UI controls on the background thread.
- `SamplesViewerViewModel` must implement `IDisposable` and dispose `_view`.
- `SamplesViewerPlugin` must NOT reference `TopicExplorerPlugin` types.
- Filter errors must be shown inline — never throw, never show a dialog.
- `ComponentState["TopicName"]` must be written in `Initialize` so the WindowManager can restore parameterized panels.

### How to obtain ISampleStore for a topic

Look in the Engine for the right API. Check `IDdsBridge`, `ITopicRegistry`, or `ISampleStoreRegistry`. If a `GetSampleStore(TopicMetadata)` or `GetOrCreateStore(TopicMetadata)` method exists on `IDdsBridge`, use that. If not, look at how the Blazor `SamplesPanel.razor` obtains its store. Do not guess — read the Engine code first.

---

## Task 2 — TASK-E002: DetailInspectorPlugin

**Design Reference:** [DESIGN.md §8 DetailInspectorPlugin](../DESIGN.md#8-phase-4--firehose-ui)  
**Task Detail Reference:** [TASK-DETAIL.md TASK-E002](../TASK-DETAIL.md#task-e002--detailinspectorplugin-linked-inspector-panel)

### New files in `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/`:

**`DetailInspectorPlugin.cs`**
- `IMonitorPlugin`; in `Initialize`:
  - Register context menu provider for `SampleData` via `IContextMenuRegistry.RegisterProvider<SampleData>`:
    ```csharp
    registry.RegisterProvider<SampleData>(sample =>
        [new ContextMenuItem("Open Inspector", () => OpenInspector(sample, sourcePanelId))]);
    ```
    Where `sourcePanelId` is passed via closure from the `SampleSelectedEvent` context. If not available at context menu time, use `sample.TopicMetadata.TopicName` as a fallback.
  - Subscribe to `SampleSelectedEvent` on UI thread to route samples to open inspectors.
- In `ConfigureServices`: no DI registrations.

**`DetailInspectorViewModel.cs`**
- Constructor: `IEventBroker broker, ISampleViewRegistry? sampleViewRegistry = null`
- State:
  - `bool IsLinked` (default true)
  - `string? SourcePanelId`
  - `SampleData? CurrentSample`
  - `List<FieldInspectorItemViewModel> FieldTree` (derived from `CurrentSample`)
  - `IDisposable? _sampleSubscription` — subscription to `SampleSelectedEvent`
- Implements `IStatefulViewModel`:
  ```csharp
  public void Initialize(IDictionary<string, object> componentState)
  {
      _state = componentState;
      IsLinked = componentState.GetValueOrDefault("IsLinked", true);
      SourcePanelId = componentState.GetValueOrDefault<string>("SourcePanelId", null);
      SubscribeIfLinked();
  }
  ```
- `SubscribeIfLinked()`: if `IsLinked && SourcePanelId != null`, subscribe to `SampleSelectedEvent` filtered to `SourcePanelId`, store token in `_sampleSubscription`.
- `OnSampleReceived(SampleSelectedEvent ev)`: (called on UI thread via `SubscribeOnUiThread`) if `IsLinked && ev.SourcePanelId == SourcePanelId`, update `CurrentSample` and rebuild `FieldTree`.
- `IsLinked` setter: when toggled, dispose `_sampleSubscription` (if un-linking), or call `SubscribeIfLinked()` (if re-linking). Update `_state["IsLinked"]`.
- `RebuildFieldTree(SampleData? sample)`: uses `sample?.TopicMetadata.AllFields` to build a flat list of `FieldInspectorItemViewModel`. Each item holds: `string Name`, `string ValueText`, `bool IsNested`. For V1, a flat list is acceptable (no TreeView hierarchy needed). Use `fieldMeta.Getter(sample.Payload)?.ToString() ?? "<null>"` for leaf values.
  - **MUST only be called on the UI thread.** `FieldMetadata.Getter` is a compiled expression delegate; invoke it only on the UI thread.
  - If `sample` is null or `sample.Payload` is null: set `FieldTree` to empty list.
- `SampleInfoViewModel`: expose `WriteTimestamp`, `ReceptionTimestamp`, `GenerationRank` as properties read from `sample.Info` (check Engine's `SampleInfo` type for exact property names).
- Implements `IDisposable`: dispose `_sampleSubscription`.

**`FieldInspectorItemViewModel.cs`** (record or simple class)
```csharp
public sealed class FieldInspectorItemViewModel
{
    public string Name { get; init; } = "";
    public string ValueText { get; init; } = "<null>";
    public bool IsNested { get; init; }
    public int Depth { get; init; }
}
```

**`DetailInspectorView.axaml` + `.axaml.cs`**
- A `ToggleButton` bound to `IsLinked` with tooltip "Link/Unlink".
- A `TextBlock` showing `SourcePanelId`.
- A `ListBox` bound to `FieldTree` showing `Name` + `ValueText` per row (flat list for V1).
- A read-only property grid section for sample info (write timestamp, reception timestamp, generation rank as `TextBlock` pairs).
- If no sample selected: show a centered `TextBlock "Select a sample in the linked viewer"`.

**Key constraints (mandatory):**
- `FieldMetadata.Getter` must ONLY be invoked on the UI thread. `OnSampleReceived` is called via `SubscribeOnUiThread` so it is already on the UI thread — do not dispatch again inside it.
- Inspector must not crash if `sample.Payload` is null or `TopicMetadata.AllFields` is empty.
- `IsLinked` and `SourcePanelId` must be persisted in `ComponentState`.
- `DetailInspectorPlugin` must NOT reference `SamplesViewerPlugin` types; cross-plugin communication is exclusively via `IEventBroker` and `IContextMenuRegistry`.
- `DetailInspectorViewModel` implements `IDisposable`.

---

## Task 3 — Tests

Add tests to `tests/DdsMonitor.Avalonia.StandardPlugin.Tests/StandardPluginSuite.cs`.

### SamplesViewerViewModel tests (minimum 10 tests):

1. `SamplesViewerPlugin_Initialize_RegistersSpawnPanelSubscription` — `IEventBroker.Subscribe<SpawnPanelEvent>` called
2. `SamplesViewerPlugin_OnSpawnPanelEvent_CallsWindowManagerSpawnPanel` — send `SpawnPanelEvent("SamplesViewer", {...})` → `IWindowManager.SpawnPanel` called with `SamplesViewer_<topic>` panelId
3. `SamplesViewerPlugin_OnSpawnPanelEvent_WrongPanelType_Ignored` — event for different `PanelTypeName` → no spawn call
4. `SamplesViewerViewModel_Initialize_WritesTopicNameToComponentState` — after `Initialize`, `dict["TopicName"]` equals the meta's `TopicName`
5. `SamplesViewerViewModel_FilterText_ValidExpression_CallsSetFilter` — set `FilterText` to valid expression → `StubSampleView.SetFilterCalled == true`
6. `SamplesViewerViewModel_FilterText_InvalidExpression_SetsFilterError` — invalid expression → `FilterError != null`, `SetFilter` NOT called
7. `SamplesViewerViewModel_FilterText_Empty_ClearsFilter` — set `FilterText` to `""` → `SetFilter(null)` called (or filter cleared)
8. `SamplesViewerViewModel_OnViewRebuilt_UpdatesFilteredCountOnUiThread` — fire `OnViewRebuilt` from background thread → after `RunJobs()`, `FilteredCount` equals `StubSampleView.CurrentFilteredCount`
9. `SamplesViewerViewModel_Dispose_DisposesView` — after `Dispose()`, `StubSampleView.Disposed == true`
10. `SamplesViewerViewModel_Dispose_UnsubscribesOnViewRebuilt` — after `Dispose()`, firing `OnViewRebuilt` does not update `FilteredCount`

### DetailInspectorViewModel tests (minimum 10 tests):

1. `DetailInspectorPlugin_Initialize_RegistersContextMenuForSampleData` — context menu has "Open Inspector" item for `SampleData`
2. `DetailInspectorViewModel_Initialize_ReadsIsLinkedFromState` — state `{"IsLinked": false}` → `IsLinked == false`
3. `DetailInspectorViewModel_Initialize_ReadsSourcePanelIdFromState` — state `{"SourcePanelId": "SV_1"}` → `SourcePanelId == "SV_1"`
4. `DetailInspectorViewModel_IsLinked_True_SubscribesToSampleSelectedEvent` — after `Initialize` with `IsLinked=true`, `TrackingEventBroker.ActiveSubscriptionCount > 0`
5. `DetailInspectorViewModel_IsLinked_False_DoesNotSubscribe` — `Initialize` with `IsLinked=false` → no event subscription
6. `DetailInspectorViewModel_OnSampleReceived_UpdatesCurrentSample` — publish `SampleSelectedEvent` for matching panelId → `CurrentSample` updated
7. `DetailInspectorViewModel_OnSampleReceived_WrongPanel_Ignored` — publish event for different panelId → `CurrentSample` not updated
8. `DetailInspectorViewModel_RebuildFieldTree_NullPayload_EmptyList` — sample with null payload → `FieldTree.Count == 0`, no exception
9. `DetailInspectorViewModel_Unlink_DisposesSubscription` — toggle `IsLinked = false` → `TrackingEventBroker.ActiveSubscriptionCount == 0`
10. `DetailInspectorViewModel_Dispose_DisposesSubscriptions` — `vm.Dispose()` → all subscriptions released

### Stub additions needed in `Stubs.cs`:

**`StubSampleView`** (implements `ISampleView`):
```csharp
internal sealed class StubSampleView : ISampleView
{
    public int CurrentFilteredCount { get; set; } = 0;
    public bool Disposed { get; private set; }
    public bool SetFilterCalled { get; private set; }
    public Func<SampleData, bool>? LastFilter { get; private set; }
    public event Action? OnViewRebuilt;

    public void TriggerViewRebuilt() => OnViewRebuilt?.Invoke();

    public void SetFilter(Func<SampleData, bool>? predicate)
    {
        SetFilterCalled = true;
        LastFilter = predicate;
    }

    public void SetSortSpec(FieldMetadata? field, SortDirection direction) { }

    public ReadOnlyMemory<SampleData> GetVirtualView(int startIndex, int count)
        => ReadOnlyMemory<SampleData>.Empty;

    public void Dispose() => Disposed = true;
}
```

**`StubFilterCompiler`** (implements `IFilterCompiler`):
```csharp
internal sealed class StubFilterCompiler : IFilterCompiler
{
    public bool NextResultIsValid { get; set; } = true;
    public string? NextErrorMessage { get; set; }
    public FilterResult LastResult { get; private set; }

    public FilterResult Compile(string expression, TopicMetadata? topicMeta)
    {
        var result = NextResultIsValid
            ? new FilterResult(true, _ => true, null)
            : new FilterResult(false, null, NextErrorMessage ?? "Compile error");
        LastResult = result;
        return result;
    }

    public FilterResult Compile(string expression, TopicMetadata? topicMeta, IReadOnlyList<object?>? paramValues)
        => Compile(expression, topicMeta);
}
```

---

## Success Criteria

All suites must pass before writing the report:

```
dotnet test tests/DdsMonitor.Engine.Tests/
dotnet test tests/DdsMonitor.Blazor.Tests/
dotnet test tests/DdsMonitor.Avalonia.Core.Tests/
dotnet test tests/DdsMonitor.Avalonia.Tests/
dotnet test tests/DdsMonitor.Avalonia.StandardPlugin.Tests/
```

Expected minimum test counts (delta from BATCH-02):
- StandardPlugin.Tests: 30 → **≥ 52** (+1 hidden sample test from DT-003, +10 SamplesViewer, +10 DetailInspector, +1 rename coverage if needed)

---

## Report

Write `.dev-workstream/avalonia-mvp/reports/BATCH-03-REPORT.md` following the standard 6-question insight format:
1. What issues did you encounter and how were you resolved?
2. What surprises did the codebase have?
3. Design decisions beyond the instructions (and why)?
4. What tech debt did this batch create (if any)?
5. Are there any remaining open questions?
6. Final test counts per suite.
