# BATCH-05 Instructions — Phase 6: Workspace Persistence + Debt Resolution

**Branch:** `ddsmon-avalonia`  
**Developer model:** Claude Sonnet 4.6  
**Design references:** DESIGN.md §10, TASK-DETAIL.md TASK-G001  
**Predecessor:** BATCH-04 committed at `f352f72`

---

## Context

786 tests pass. StandardPlugin.Tests: 71.

BATCH-05 delivers Phase 6 — Workspace State Persistence Round-Trip (TASK-G001) — the final
V1 milestone. It also resolves two P2/P3 debt items from previous batches.

---

## Pre-Task: Debt Resolution (minimal, non-breaking)

### DT-009 — Make StandardDrawerRegistrar internal again

**File:** `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/DdsMonitor.Avalonia.StandardPlugin.csproj`

Add:
```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
    <_Parameter1>DdsMonitor.Avalonia.StandardPlugin.Tests</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

Then change `StandardDrawerRegistrar.cs` from `public static class` to `internal static class`.

No new tests needed — existing DrawerRegistrar tests must still pass.

### DT-007 — NetworkConfigViewModel.Apply() accumulation guard

**File:** `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/NetworkConfigViewModel.cs`

First check the actual `IDdsBridge` interface in `tools/DdsMonitor/DdsMonitor.Engine/` for the exact
`AddParticipant` / `RemoveParticipant` method signatures and whether there is a `SetParticipants`
or `ClearParticipants` method. Read `IDdsBridge.cs` and `StubDdsBridge.cs` before modifying.

**If the Engine has no clear/remove method:** Add a call-count check — before calling `Apply()`,
compare the current `Participants` collection against `_ddsBridge.ParticipantConfigs`. If they
match (same domain IDs and partition names in the same order), skip all bridge calls and set
`ApplyError = null` (no-op Apply). This prevents silent accumulation on repeated Apply clicks
without changing the Engine's interface.

**If the Engine has `RemoveParticipant(index)` or `ClearParticipants()`:** Use it to diff-and-apply:
remove participants no longer in the list, add new ones.

Add or update these tests in `StandardPluginSuite.cs`:
- `NetworkConfigViewModel_Apply_NoChanges_SkipsBridgeCalls` — same participants as initial state →
  `AddParticipantCallCount == 0` after Apply

---

## Task 1 — TASK-G001: IStatefulViewModel Persistence Round-Trip

**Design Reference:** [DESIGN.md §10](../DESIGN.md#10-phase-6--workspace-polish)  
**Task Detail Reference:** [TASK-DETAIL.md TASK-G001](../TASK-DETAIL.md#task-g001--istatefulviewmodel-persistence-round-trip)

This task involves three coordinated changes:
1. **Shell**: `AvaloniaWorkspacePersistenceService` + `AvaloniaWindowManager` Initialize wiring + geometry persistence
2. **Plugin**: verify `SamplesViewerViewModel` fully satisfies the round-trip contract
3. **Tests**: unit tests for the shell-side persistence plumbing

---

### 1a — `AvaloniaWorkspacePersistenceService`

**New file:** `tools/DdsMonitor/DdsMonitor.Avalonia/AvaloniaWorkspacePersistenceService.cs`

```csharp
internal sealed class AvaloniaWorkspacePersistenceService : BackgroundService
{
    private readonly IEventBroker _broker;
    private readonly IWindowManager _windowManager;
    private readonly IWorkspaceState _workspaceState;
    private readonly IHostApplicationLifetime _lifetime;
    private CancellationTokenSource? _debounce;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Subscribe to WorkspaceSaveRequestedEvent — debounce 1.5 s then flush
        // Subscribe to ApplicationStopping for final save
    }

    private async Task FlushAsync()
    {
        // Collect all active PanelState entries from _windowManager.ActivePanels
        // Serialize to workspace.json via _workspaceState.WorkspaceFilePath
        // Use System.Text.Json — PanelState already serializable
        // Use WorkspaceDocument (existing Engine type) to wrap panel states for compatibility
    }
}
```

**Key contract:**
- Subscribes to `WorkspaceSaveRequestedEvent` via `IEventBroker`.
- Debounce: cancel any pending flush and restart a 1.5 s timer.
- On timer expiry: call `FlushAsync()`.
- On `IHostApplicationLifetime.ApplicationStopping`: call `FlushAsync()` synchronously (best-effort).
- `_windowManager.ActivePanels` must return the currently open `PanelState` list — verify the
  `AvaloniaWindowManager` has this property (it was not explicitly required before this task).

**Check whether `AvaloniaWindowManager` has an `ActivePanels` property.** If not, add:
```csharp
public IReadOnlyCollection<PanelState> ActivePanels => _panels.Values.ToList();
```
where `_panels` is the existing `Dictionary<string, (Window Window, PanelState State)>` or
similar structure already in `AvaloniaWindowManager`.

**Workspace file format:** Use the same format as the Blazor shell's `IWorkspaceState`.
Read the existing `IWorkspaceState` implementation (in the Engine or shell) to understand
the serialization contract before implementing. The format is `WorkspaceDocument` containing
a list of `PanelState`. Geometry lives inside `PanelState.ComponentState["__window"]` as a
`Dictionary<string, double>` with keys `"X"`, `"Y"`, `"Width"`, `"Height"`.

---

### 1b — `AvaloniaWindowManager` changes

**File:** `tools/DdsMonitor/DdsMonitor.Avalonia/AvaloniaWindowManager.cs`

Add the following (read the existing file first to understand the current structure):

**1. Call `IStatefulViewModel.Initialize` before `Show()`:**
```csharp
// After ViewModel creation:
if (vm is IStatefulViewModel stateful)
    stateful.Initialize(panelState.ComponentState);
window.Content = /* view from IAvaloniaViewRegistry */;
window.Show();
```

**2. Write window geometry to ComponentState:**
```csharp
// After window is created:
window.PositionChanged += (_, _) => WriteGeometry(panelState, window);
window.GetObservable(Window.ClientSizeProperty).Subscribe(_ => WriteGeometry(panelState, window));
// also on close (already handled)

private static void WriteGeometry(PanelState state, Window w)
{
    if (!state.ComponentState.TryGetValue("__window", out var raw) ||
        raw is not Dictionary<string, double> geo)
    {
        geo = new Dictionary<string, double>();
        state.ComponentState["__window"] = geo;
    }
    geo["X"] = w.Position.X;
    geo["Y"] = w.Position.Y;
    geo["Width"] = w.ClientSize.Width;
    geo["Height"] = w.ClientSize.Height;
}
```

**3. Restore geometry on `SpawnPanel`:**
```csharp
if (panelState.ComponentState.TryGetValue("__window", out var raw) &&
    raw is Dictionary<string, object> geo)
{
    window.Position = new PixelPoint(
        (int)Convert.ToDouble(geo.GetValueOrDefault("X", 0d)),
        (int)Convert.ToDouble(geo.GetValueOrDefault("Y", 0d)));
    window.Width = Convert.ToDouble(geo.GetValueOrDefault("Width", 800d));
    window.Height = Convert.ToDouble(geo.GetValueOrDefault("Height", 600d));
}
```

Note: `ComponentState` values coming from JSON deserialization may be `JsonElement` not
`Dictionary<string, double>`. Handle both:
```csharp
if (raw is JsonElement je && je.ValueKind == JsonValueKind.Object)
{
    geo = je.Deserialize<Dictionary<string, double>>() ?? new();
    // rewrite as native dict for future use
    panelState.ComponentState["__window"] = geo;
}
```

**4. Startup restoration:** On app startup, if `workspace.json` exists, load it and call
`SpawnPanel` for each `PanelState` found. Register this as a startup action in `Program.cs`
or `App.axaml.cs` (after `pluginLoader.InitializePlugins(context)` is called, so all panel
factories are registered).

---

### 1c — Verify `SamplesViewerViewModel` round-trip completeness

**File:** `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/SamplesViewerViewModel.cs`

Read the current implementation. Verify that `Initialize(dict)`:
1. Writes `componentState["TopicName"] = _meta.TopicName` ✓ (done in BATCH-03)
2. Reads and applies `FilterText` from the dict if present
3. Stores the dict reference as `_state` for future direct writes

Verify that the `FilterText` setter writes back to `_state["FilterText"] = value` and publishes
`WorkspaceSaveRequestedEvent`. If any of these writes are missing, add them.

Also add to the tests an integration test:
- `SamplesViewerViewModel_Initialize_RestoresFilterText` — populate dict with `FilterText="test"`, 
  call `Initialize`, assert `vm.FilterText == "test"` and `_view.LastFilter != null`

---

### 1d — Registration in the Shell

**File:** `tools/DdsMonitor/DdsMonitor.Avalonia/Program.cs` (or `App.axaml.cs`)

Register the persistence service:
```csharp
builder.Services.AddHostedService<AvaloniaWorkspacePersistenceService>();
```

---

## Task 2 — Tests

### New test project: `tests/DdsMonitor.Avalonia.Tests/`

This project already exists (32 tests). Add to its test file (find the file with the 32 tests).

Add a new partial class or region: `WorkspacePersistenceTests`

**Minimum 8 tests:**

1. `AvaloniaWorkspacePersistenceService_SubscribesToSaveEvent` — verify service subscribes to 
   `WorkspaceSaveRequestedEvent` via the broker on startup (use `TrackingEventBroker`-style stub)

2. `AvaloniaWindowManager_SpawnPanel_CallsInitializeOnStatefulViewModel` — stub ViewModel implements
   `IStatefulViewModel`; after `SpawnPanel`, `Initialize` is called with a non-null dict

3. `AvaloniaWindowManager_SpawnPanel_WritesTopicNameToComponentState` — spawn a panel with a 
   `SamplesViewerViewModel`-like stub that writes `"TopicName"` → dict contains `"TopicName"` after spawn

4. `AvaloniaWindowManager_SpawnPanel_RestoresGeometryFromComponentState` — provide `ComponentState` with
   `"__window"` containing `Width=900, Height=600` → window's `Width == 900` after spawn

5. `AvaloniaWindowManager_ActivePanels_ReturnsOpenPanels` — spawn two panels → `ActivePanels.Count == 2`;
   close one → `ActivePanels.Count == 1`

6. `SamplesViewerViewModel_Initialize_RestoresFilterText` — dict with `FilterText="seq>5"` → 
   `vm.FilterText == "seq>5"` after Initialize

7. `SamplesViewerViewModel_FilterTextChange_WritesToComponentState` — after Initialize, change FilterText →
   `dict["FilterText"] == new value`

8. `SamplesViewerViewModel_FilterTextChange_PublishesSaveEvent` — after Initialize, change FilterText →
   `WorkspaceSaveRequestedEvent` published via broker

For the above tests, use the existing `[AvaloniaFact]` / headless Avalonia test infrastructure
that the 32 existing tests use. Read the existing test file before adding tests.

---

## Success Criteria

Run all suites before writing the report:

```
dotnet test tests/DdsMonitor.Engine.Tests/ -c Release
dotnet test tests/DdsMonitor.Blazor.Tests/ -c Release
dotnet test tests/DdsMonitor.Avalonia.Core.Tests/ -c Release
dotnet test tests/DdsMonitor.Avalonia.Tests/ -c Release
dotnet test tests/DdsMonitor.Avalonia.StandardPlugin.Tests/ -c Release
```

Expected minimum test counts:
- DdsMonitor.Avalonia.Tests: 32 → **≥ 40** (+8 persistence tests)
- DdsMonitor.Avalonia.StandardPlugin.Tests: 71 → **≥ 73** (+1 no-op Apply test, +1 FilterText restore)

---

## Implementation Guidance

### Existing code to read before implementing:

1. `tools/DdsMonitor/DdsMonitor.Avalonia/AvaloniaWindowManager.cs` — full file
2. `tools/DdsMonitor/DdsMonitor.Avalonia/Program.cs` — bootstrap sequence
3. `tools/DdsMonitor/DdsMonitor.Engine/Plugins/IWorkspaceState.cs` — workspace file path resolution
4. `tools/DdsMonitor/DdsMonitor.Engine/Workspace/WorkspaceDocument.cs` — serialization schema
5. `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/SamplesViewerViewModel.cs` — current Initialize
6. `tests/DdsMonitor.Avalonia.Tests/` — existing headless test patterns

### Potential pitfall — JSON deserialization type mismatch

When `workspace.json` is loaded, `ComponentState` values deserialized from JSON will be
`JsonElement` instances, not native C# types. All code that reads from `ComponentState`
must handle `JsonElement`:
```csharp
string? filterText = state.ComponentState.TryGetValue("FilterText", out var v)
    ? (v is JsonElement je ? je.GetString() : v as string)
    : null;
```

The `AvaloniaWindowManager.WriteGeometry` method and the `SamplesViewerViewModel.Initialize`
method must both be robust to `JsonElement` values.

---

## Report

Write `.dev-workstream/avalonia-mvp/reports/BATCH-05-REPORT.md` following the standard 6-question format.
