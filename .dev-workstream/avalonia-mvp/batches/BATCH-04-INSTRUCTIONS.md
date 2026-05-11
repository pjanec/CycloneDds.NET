# BATCH-04 Instructions — Phase 5: Data Authoring & Network Configuration

**Branch:** `ddsmon-avalonia`  
**Developer model:** Claude Sonnet 4.6  
**Design references:** DESIGN.md §9, TASK-DETAIL.md TASK-F001 and TASK-F002  
**Predecessor:** BATCH-03 committed at `657d919`

---

## Context

767 tests pass across all suites. StandardPlugin.Tests: 52.

BATCH-04 delivers Phase 5 — Data Authoring & Network Configuration:
- **TASK-F001**: `SendSamplePlugin` — two-way expression-tree binding for DDS payload authoring
- **TASK-F002**: `WorkspaceManagerPlugin: Network Configurator` — dynamic DDS participant management

It also addresses two debt items from BATCH-03:
- **DT-004**: `SamplesViewerView.axaml` needs a `DataTemplate` for sample rows
- **DT-005**: `DetailInspectorView.axaml` field tree needs depth-based indentation

---

## Pre-Task: Debt Resolution

### DT-004 — SamplesViewerView DataTemplate

**File:** `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/SamplesViewerView.axaml`

Add a `DataTemplate` for `SampleRowViewModel` items in the `ListBox`. The template should show at minimum:
- `ReceptionTimestamp` on the left (bold or slightly muted)
- `TopicShortName` on the right (or below)

Also verify `SampleRowViewModel.cs` exists in `DdsMonitor.Avalonia.StandardPlugin` with at least:
```csharp
public sealed class SampleRowViewModel
{
    public string ReceptionTimestamp { get; init; } = "";
    public string TopicShortName { get; init; } = "";
}
```

If it does not exist, create it. No new tests needed for this — visual only.

### DT-005 — DetailInspectorView Field Tree Indentation

**File:** `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/DetailInspectorView.axaml`

Add `Margin` binding to `Depth` for each field row. Use a simple multiplier of 12px per depth level:

Option A (inline converter in AXAML — simplest): Use a `{Binding Depth, Converter={StaticResource DepthToMarginConverter}}` with a local `IValueConverter` that returns `new Thickness(depth * 12, 0, 0, 0)`.

Option B (code-behind): Set margin in the code-behind when `FieldTree` items are added.

Option B is acceptable if Option A causes AXAML build issues. Either way, depth 0 fields should have no extra margin, depth 1 fields 12px left margin, etc. No new test for this — visual only.

---

## Task 1 — TASK-F001: SendSamplePlugin

**Design Reference:** [DESIGN.md §9](../DESIGN.md#9-phase-5--data-authoring--network-configuration)  
**Task Detail Reference:** [TASK-DETAIL.md TASK-F001](../TASK-DETAIL.md#task-f001--sendsampleplugin)

### Key constraint: Standard drawers must be registered first

Before `IAvaloniaTypeDrawerRegistry.Build` can be called per field, standard Avalonia controls must be registered for primitive types. `SendSamplePlugin.Initialize` must call a helper that registers:
- `int`, `uint`, `long`, `ulong`, `short`, `ushort`, `byte`, `sbyte` → `NumericUpDown` control
- `float`, `double`, `decimal` → `NumericUpDown` with decimal places
- `bool` → `ToggleSwitch` (or `CheckBox`)
- `string` → `TextBox`
- `char` → single-character `TextBox` (max length 1)
- All `enum` types: handled via reflection in the registry's generic fallback — or register via `typeof(Enum)` base

The helper can be a `static class StandardDrawerRegistrar` with a `Register(IAvaloniaTypeDrawerRegistry registry)` method. Place it in `DdsMonitor.Avalonia.StandardPlugin`.

### New files in `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/`:

**`SendSamplePlugin.cs`**
- `IMonitorPlugin`; in `Initialize`:
  - Register standard drawers via `StandardDrawerRegistrar.Register(avaloniaTypeDrawerRegistry)`.
  - Register context menu provider for `SampleData`:
    ```csharp
    contextMenuRegistry.RegisterProvider<SampleData>(sample =>
        [new ContextMenuItem("Clone to Send", () => windowManager.SpawnPanel(
            $"SendSample_{sample.TopicMetadata.TopicName}",
            new Dictionary<string, object> { ["TopicName"] = sample.TopicMetadata.TopicName }))]);
    ```
  - Register `"Tools/Send Sample"` menu item → spawns `SendSample_Blank` panel with no pre-filled payload.
- In `ConfigureServices`: no new DI registrations.

**`StandardDrawerRegistrar.cs`**
```csharp
internal static class StandardDrawerRegistrar
{
    public static void Register(IAvaloniaTypeDrawerRegistry registry)
    {
        // int
        registry.Register(typeof(int), ctx =>
        {
            var upDown = new NumericUpDown { Value = Convert.ToDecimal(ctx.ValueGetter() ?? 0), ... };
            upDown.ValueChanged += (_, _) => ctx.OnChange(Convert.ToInt32(upDown.Value ?? 0));
            return upDown;
        });
        // ... etc for all primitive types
    }
}
```

**`SendSampleViewModel.cs`**
- Constructor: `TopicMetadata meta, IAvaloniaTypeDrawerRegistry drawerRegistry, IDdsBridge ddsBridge, object? initialPayload = null`
- On construction:
  - `_payload = initialPayload ?? Activator.CreateInstance(meta.TopicType)!`
  - For each field in `meta.AllFields` (non-synthetic), create `AvaloniaDrawerContext(...)` + call `drawerRegistry.Build(ctx)`.
  - Store controls in `List<Control> BuiltControls` for the view to use.
  - Collect validation errors per field via `OnValidationError` callback.
- `Send()` method:
  ```csharp
  public string? SendError { get; private set; }
  public void Send()
  {
      try
      {
          SendError = null;
          var writer = _ddsBridge.GetWriter(new TopicMetadata(_meta.TopicType));
          writer.Write(_payload);
      }
      catch (Exception ex)
      {
          SendError = $"DDS Publish Failed: {ex.Message}";
      }
  }
  ```
- `SendEnabled` property: returns `true` when there are no pending validation errors.
- Does NOT implement `IStatefulViewModel` (payload authoring state is not persisted in V1).
- Does NOT implement `IDisposable` (no subscriptions).

**`SendSampleView.axaml` + `.axaml.cs`**
- `ScrollViewer` containing an `ItemsControl` bound to `BuiltControls` (the dynamically built Avalonia controls).
- A "Send" `Button` bound to `Send()` command; disabled when `!SendEnabled`.
- A `TextBlock` showing `SendError`, visible only when non-null.

### Key constraints:
- `Convert.ChangeType` is NOT permitted in the Send handler — type conversion happens at the control level.
- DDS write exceptions must be caught, surfaced via `SendError`, and must NOT crash the app.
- The Send panel is independent of `SamplesViewerPlugin` types — use only Engine + Core contracts.

---

## Task 2 — TASK-F002: NetworkConfigPlugin (WorkspaceManagerPlugin extension)

**Design Reference:** [DESIGN.md §9](../DESIGN.md#9-phase-5--data-authoring--network-configuration)  
**Task Detail Reference:** [TASK-DETAIL.md TASK-F002](../TASK-DETAIL.md#task-f002--workspacemanagerplugin-network-configurator-panel)

### Update `WorkspaceManagerPlugin.cs`:
Add `"Tools/Network Configuration…"` menu item in `Initialize`. On click: `windowManager.SpawnPanel("NetworkConfig", null)`.

Register `NetworkConfigViewModel` view factory in `IAvaloniaViewRegistry`.

### New files in `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/`:

**`NetworkConfigViewModel.cs`**
- Constructor: `IDdsBridge ddsBridge, IEventBroker eventBroker`
- Properties:
  - `ObservableCollection<ParticipantConfigViewModel> Participants` — loaded from `ddsBridge.ParticipantConfigs` in constructor.
  - `string? ApplyError` — inline error for the Apply button.
- `AddRow()`: add a new `ParticipantConfigViewModel { DomainId = 0, PartitionName = "" }` to `Participants`.
- `RemoveRow(int index)`: remove participant at index.
- `Apply()`:
  ```csharp
  public void Apply()
  {
      try
      {
          ApplyError = null;
          foreach (var p in Participants)
              _ddsBridge.AddParticipant(p.DomainId, p.PartitionName);
          _eventBroker.Publish(new ParticipantsChangedEvent(_ddsBridge.ParticipantConfigs));
      }
      catch (Exception ex)
      {
          ApplyError = $"Failed to apply: {ex.Message}";
      }
  }
  ```
- Does NOT implement `IStatefulViewModel` in V1 (deferred per TASK-DETAIL constraint).
- Does NOT implement `IDisposable` (no subscriptions).

**`ParticipantConfigViewModel.cs`** (simple editable row):
```csharp
public sealed class ParticipantConfigViewModel
{
    public int DomainId { get; set; }
    public string PartitionName { get; set; } = "";
}
```

**`NetworkConfigView.axaml` + `.axaml.cs`**
- `DataGrid` or `ListBox` bound to `Participants`, with editable `DomainId` (integer) and `PartitionName` (string) columns.
- "Add Row" button → `AddRow()`.
- "Remove Selected" button → `RemoveRow(selectedIndex)`.
- "Apply" button → `Apply()`.
- `TextBlock` showing `ApplyError`, visible only when non-null.

### Key constraint:
- `IDdsBridge.AddParticipant` throwing must be caught and surfaced via `ApplyError` — never crash.
- Read the Engine's `IDdsBridge` for exact method signatures (`AddParticipant`, `ParticipantConfigs`).
- `ParticipantsChangedEvent` must exist in `EventBrokerEvents.cs` — if it doesn't, add it as `public sealed record ParticipantsChangedEvent(IReadOnlyList<object> Participants);` (adjust based on actual `ParticipantConfig` type).

---

## Task 3 — Tests

Add to `tests/DdsMonitor.Avalonia.StandardPlugin.Tests/StandardPluginSuite.cs`.

### SendSamplePlugin tests (minimum 8 tests):

1. `SendSamplePlugin_Initialize_RegistersContextMenuForSampleData` — `IContextMenuRegistry` has "Clone to Send" item
2. `SendSamplePlugin_Initialize_RegistersStandardDrawers` — `IAvaloniaTypeDrawerRegistry` returns non-null for `typeof(int)` after init
3. `SendSampleViewModel_Build_CreatesControlsForAllFields` — `BuiltControls.Count` equals `meta.AllFields.Count` after construction
4. `SendSampleViewModel_Send_CallsWriter` — `StubDdsBridge.Writer.WriteCount > 0` after `Send()`
5. `SendSampleViewModel_Send_ExceptionSetsError` — `StubDdsBridge` throws → `SendError` contains "DDS Publish Failed"
6. `SendSampleViewModel_Send_ExceptionDoesNotThrow` — DDS write failure must not throw from ViewModel
7. `SendSampleViewModel_Send_ClearsPreviousError` — call send with stub that throws, then stub that succeeds → `SendError == null`
8. `SendSampleViewModel_InitialPayload_Cloned` — construct with a pre-built `HeartbeatSample` payload → `_payload` reference is the injected object

### NetworkConfigViewModel tests (minimum 6 tests):

1. `WorkspaceManagerPlugin_Initialize_RegistersNetworkConfigMenuItem` — menu has "Network Configuration" entry (or "Network Config")
2. `NetworkConfigViewModel_Constructor_LoadsExistingParticipants` — `StubDdsBridge.ParticipantConfigs` has 2 entries → `Participants.Count == 2`
3. `NetworkConfigViewModel_AddRow_IncreasesCount` — `AddRow()` → `Participants.Count` increases by 1
4. `NetworkConfigViewModel_RemoveRow_DecreasesCount` — `AddRow()` then `RemoveRow(0)` → `Participants.Count` back to original
5. `NetworkConfigViewModel_Apply_CallsAddParticipant` — after `AddRow()` and `Apply()` → `StubDdsBridge.AddParticipantCallCount > 0`
6. `NetworkConfigViewModel_Apply_ExceptionSetsApplyError` — `StubDdsBridge.AddParticipant` throws → `ApplyError` non-null, `null` before

### StandardDrawerRegistrar tests (minimum 3 tests in a new `DrawerRegistrarTests` class):

1. `StandardDrawerRegistrar_Register_IntDrawer_ReturnsNumericUpDown` — after `Register(registry)`, `Build(ctx for int)` returns a `NumericUpDown`
2. `StandardDrawerRegistrar_Register_BoolDrawer_ReturnsCheckBoxOrToggle` — `Build(ctx for bool)` returns a `ToggleSwitch` or `CheckBox`
3. `StandardDrawerRegistrar_Register_StringDrawer_ReturnsTextBox` — `Build(ctx for string)` returns a `TextBox`

These 3 tests require `[AvaloniaFact]` because they instantiate Avalonia controls.

### Stub additions needed in `Stubs.cs`:

**`StubAvaloniaTypeDrawerRegistry`** (if not already present):
```csharp
internal sealed class StubAvaloniaTypeDrawerRegistry : IAvaloniaTypeDrawerRegistry
{
    private readonly Dictionary<Type, Func<AvaloniaDrawerContext, Control>> _factories = new();

    public void Register(Type type, Func<AvaloniaDrawerContext, Control> factory)
        => _factories[type] = factory;

    public Control Build(AvaloniaDrawerContext ctx)
    {
        if (_factories.TryGetValue(ctx.FieldType, out var factory))
            return factory(ctx);
        return new TextBlock { Text = $"<no drawer for {ctx.FieldType.Name}>" };
    }

    public bool HasDrawer(Type type) => _factories.ContainsKey(type);
}
```

**Update `StubDdsBridge`** to support:
- `int AddParticipantCallCount { get; private set; }`
- `bool AddParticipantShouldThrow { get; set; }`
- `List<object> SimulatedParticipantConfigs { get; }` as backing for `ParticipantConfigs`

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

Expected minimum test counts:
- StandardPlugin.Tests: 52 → **≥ 69** (+8 SendSample, +6 NetworkConfig, +3 DrawerRegistrar)

---

## Report

Write `.dev-workstream/avalonia-mvp/reports/BATCH-04-REPORT.md` following the standard 6-question format.
