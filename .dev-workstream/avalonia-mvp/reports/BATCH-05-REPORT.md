# BATCH-05 Report — Phase 6: Workspace Persistence + Debt Resolution

**Date:** 2026-05-11  
**Branch:** `ddsmon-avalonia`  
**Total tests:** 799 (was 786)

---

## 1. What Was Implemented

### DT-009 — Make StandardDrawerRegistrar internal

**Files modified:**
- `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/DdsMonitor.Avalonia.StandardPlugin.csproj`  
  Added `InternalsVisibleTo(DdsMonitor.Avalonia.StandardPlugin.Tests)` via csproj `AssemblyAttribute` item group.
- `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/StandardDrawerRegistrar.cs`  
  Changed `public static class` → `internal static class`.

No test changes needed; existing 3 DrawerRegistrar tests still pass.

---

### DT-007 — NetworkConfigViewModel.Apply() accumulation guard

**File modified:** `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/NetworkConfigViewModel.cs`

Added a no-op guard at the top of `Apply()`: compares the current `Participants` collection (count + per-entry DomainId + PartitionName) against `_ddsBridge.ParticipantConfigs`. If they match exactly, the method sets `ApplyError = null` and returns early, skipping all `AddParticipant` calls. This prevents silent accumulation of duplicate participants on repeated Apply clicks without modifying the Engine interface.

---

### TASK-G001 — IStatefulViewModel Persistence Round-Trip

#### 1a — AvaloniaWorkspacePersistenceService (new file)

**File created:** `tools/DdsMonitor/DdsMonitor.Avalonia/AvaloniaWorkspacePersistenceService.cs`

`internal sealed class AvaloniaWorkspacePersistenceService : BackgroundService`

- Subscribes to `WorkspaceSaveRequestedEvent` **in the constructor** (not in `ExecuteAsync`) for testability without running the generic host.
- Registers `FlushSync` on `IHostApplicationLifetime.ApplicationStopping` for final save.
- `RequestSave()`: cancels any pending `CancellationTokenSource`, creates a new one, schedules a 1.5 s debounce via `Task.Delay + ContinueWith(NotOnCanceled)`.
- `FlushSync()`: calls `_windowManager.SaveWorkspace(path)` — best-effort, swallows exceptions.
- `ExecuteAsync`: waits on `Task.Delay(Timeout.Infinite, stoppingToken)` — effectively idle, service lifecycle is driven by events.
- `Dispose()`: releases the event subscription and cancels any in-flight debounce.

**Deviation from instructions:** Constructor-subscription instead of ExecuteAsync-subscription for cleaner testability without host infrastructure.

#### 1b — AvaloniaWindowManager changes

**File modified:** `tools/DdsMonitor/DdsMonitor.Avalonia/AvaloniaWindowManager.cs`

- `SpawnPanel`: Added `JsonElement` handling for `__window` geometry. When the value is a `JsonElement` (JSON-deserialized), it is deserialized to `Dictionary<string, object>` and re-stored in `ComponentState` for consistency. Added private `ToDouble(object)` helper that calls `JsonElement.GetDouble()` for JSON values instead of `Convert.ToDouble` (which throws for `JsonElement`).
- `IStatefulViewModel.Initialize`: Already called before `Show()` from BATCH-03; no changes needed.
- `ActivePanels` property: Already existed from BATCH-03; no changes needed.
- Geometry on-close and position tracking: Already implemented from BATCH-03; no changes needed.

#### 1c — SamplesViewerViewModel fixes

**File modified:** `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/SamplesViewerViewModel.cs`

1. Added `IEventBroker? eventBroker = null` optional constructor parameter.
2. `Initialize(dict)`:
   - Now handles `JsonElement` values for `FilterText` key (deserialized workspace state).
   - After wiring the view, calls `ApplyFilter(_filterText)` if FilterText is non-empty — so the restored filter is actually applied to the `ISampleView`, not just stored in the field.
3. `ApplyFilter(expression)`:
   - After writing `_state["FilterText"] = expression`, now publishes `WorkspaceSaveRequestedEvent` via `_eventBroker` (if injected).

#### 1d — Registration in Program.cs + App.axaml.cs

**Files modified:**
- `tools/DdsMonitor/DdsMonitor.Avalonia/Program.cs`: Added `builder.Services.AddHostedService<AvaloniaWorkspacePersistenceService>();`
- `tools/DdsMonitor/DdsMonitor.Avalonia/App.axaml.cs`: After creating the main window in `OnFrameworkInitializationCompleted`, added `windowManager.LoadWorkspace(workspaceState.WorkspaceFilePath)`. This is the correct moment — the UI thread is running and plugins are already initialized (done in Program.cs before `BuildAvaloniaApp`).
- `tools/DdsMonitor/DdsMonitor.Avalonia/DdsMonitor.Avalonia.csproj`: Added `InternalsVisibleTo(DdsMonitor.Avalonia.Tests)` so tests can access `AvaloniaWorkspacePersistenceService.RequestSave()` and `FlushSync()` (both `internal`).

---

## 2. Tests Added

### DdsMonitor.Avalonia.Tests — new file: `PersistenceTests.cs`

**New helper types added (test-only):**
- `StatefulTestPanelViewModel` — implements `IStatefulViewModel`; records Initialize call and writes `"TopicName"` to state.
- `AnotherTestPanelViewModel` — second panel type for multi-panel tests.
- `TrackingEventBroker` — counts active subscriptions (thread-safe).
- `StubWorkspaceState` — returns a configurable `WorkspaceFilePath`.
- `StubHostApplicationLifetime` — provides cancellation token for `ApplicationStopping`.
- `CountingWindowManager` — counts `SaveWorkspace` calls; implements full `IWindowManager`.

**New test class: `PersistenceServiceTests` (5 tests, no Avalonia UI):**
1. `AvaloniaWorkspacePersistenceService_SubscribesToSaveEvent` — verifies broker subscription count > 0 after construction.
2. `AvaloniaWorkspacePersistenceService_FlushSync_CallsSaveWorkspace` — calls `FlushSync()` directly; asserts `SaveCallCount == 1`.
3. `AvaloniaWorkspacePersistenceService_FlushSync_EmptyPath_DoesNotThrow` — empty file path → no exception, `SaveCallCount == 0`.
4. `AvaloniaWorkspacePersistenceService_Dispose_CancelsDebounce` (async) — 60 s debounce scheduled, then dispose; asserts save not fired.
5. `AvaloniaWorkspacePersistenceService_Dispose_ReleasesSubscription` — after dispose, subscription count returns to 0.

**New test class: `WindowManagerPersistenceTests` (5 tests, `[AvaloniaFact]`):**
6. `AvaloniaWindowManager_SpawnPanel_CallsInitializeOnStatefulViewModel` — `StatefulTestPanelViewModel.WasInitialized` is true after spawn.
7. `AvaloniaWindowManager_SpawnPanel_WritesTopicNameToComponentState` — `ComponentState["TopicName"] == "TestTopic"` written by stub Initialize.
8. `AvaloniaWindowManager_SpawnPanel_RestoresGeometryFromComponentState` — native `Dictionary<string, object>` geo → `PanelState.Width/Height` set correctly.
9. `AvaloniaWindowManager_SpawnPanel_RestoresGeometryFromJsonElement` — JSON-deserialized state with `JsonElement` `__window` → geometry restored correctly.
10. `AvaloniaWindowManager_ActivePanels_ReturnsOpenPanels` — 2 panels open → count 2; close one → count 1, correct panel remains.

### DdsMonitor.Avalonia.StandardPlugin.Tests — additions to `StandardPluginSuite.cs`

11. `NetworkConfigViewModel_Apply_NoChanges_SkipsBridgeCalls` — bridge pre-populated with 1 participant; VM loads it in ctor; Apply() → `AddParticipantCallCount == 0`.
12. `SamplesViewerViewModel_Initialize_RestoresFilterText` — dict with `FilterText="seq>5"` → `vm.FilterText == "seq>5"` and `view.LastFilter != null` (filter applied to view).
13. `SamplesViewerViewModel_FilterTextChange_PublishesSaveEvent` — after Initialize, set FilterText → `WorkspaceSaveRequestedEvent` fired via real `EventBroker`.

---

## 3. Final Test Counts

| Suite                              | Before | After | Delta |
|------------------------------------|--------|-------|-------|
| DdsMonitor.Engine.Tests            | 643    | 643   | +0    |
| DdsMonitor.Blazor.Tests            | 16     | 16    | +0    |
| DdsMonitor.Avalonia.Core.Tests     | 24     | 24    | +0    |
| DdsMonitor.Avalonia.Tests          | 32     | 42    | +10   |
| DdsMonitor.Avalonia.StandardPlugin.Tests | 71 | 74  | +3    |
| **Total**                          | **786**| **799**| **+13** |

All suites: **0 failures**.

---

## 4. Design Decisions

1. **Constructor-subscription in BackgroundService**: The subscription to `WorkspaceSaveRequestedEvent` happens in the constructor rather than in `ExecuteAsync`. This makes the service immediately testable without spinning up a full generic host — tests just instantiate the class and check subscription count. The trade-off is a tiny lifecycle asymmetry (subscription alive during DI setup before host starts), but this is harmless and consistent with how Blazor's `WorkspacePersistenceService` works.

2. **`ToDouble` helper for JsonElement**: Rather than duplicating the `JsonElement` branch four times (X, Y, Width, Height), a private static helper `ToDouble(object)` centralizes the conversion logic. This also makes future additions (e.g., opacity) trivial.

3. **Workspace load in `OnFrameworkInitializationCompleted`**: Loading panels in `App.axaml.cs` rather than Program.cs ensures the UI thread is running before `SpawnPanel` posts work to it. Loading before plugin initialization would fail because view factories haven't been registered yet.

4. **SamplesViewerViewModel IEventBroker as optional**: Added as `IEventBroker? eventBroker = null` to maintain backward compatibility. DI will inject it when available; unit tests that don't need save-event tracking can omit it.

5. **No-op Apply guard uses exact ordering**: The guard compares bridge configs against UI Participants in sequence order (using `Zip`). This is intentional — the VM always loads configs in order from the bridge, so the comparison is reliable.

---

## 5. Tech Debt Identified

**P3: `SamplesViewerViewModel.FilterText` setter skips write-back on empty clear**  
When `FilterText = ""`, `ApplyFilter` sets filter to null but does NOT write `""` back to `_state["FilterText"]` or publish a save event. This is intentional (clearing filter shouldn't mark workspace dirty), but should be documented.

**P3: `AvaloniaWindowManager.SaveWorkspaceToJson` uses `List<PanelState>` format**  
The Blazor shell uses `WorkspaceDocument` (with `ExcludedTopics` etc.) while Avalonia uses a bare `List<PanelState>`. The two formats are incompatible. If workspace portability between shells becomes a requirement, a migration/unification task will be needed.

**P3: `Program.cs` workspace load happens after `BuildAvaloniaApp` is called but inside `OnFrameworkInitializationCompleted`**  
The load call is inside App.axaml.cs which is correct, but there's no guard for headless/record mode. If `LoadWorkspace` is called in headless mode, `SpawnPanel` posts to `Dispatcher.UIThread` which may not exist. Currently the headless mode returns early before `BuildAvaloniaApp` so this path is not reached, but future changes should be careful.

---

## 6. Issues Encountered and Resolutions

**Issue 1: `Convert.ToDouble(JsonElement)` throws `InvalidCastException`**  
When `workspace.json` is deserialized via `JsonSerializer.Deserialize<Dictionary<string, object>>()`, all nested values are `JsonElement`. The original code used `Convert.ToDouble(x)` which calls `IConvertible.ToDouble()` — `JsonElement` doesn't implement `IConvertible`.  
**Resolution:** Extracted `private static double ToDouble(object value)` helper that dispatches on `JsonElement` via `je.GetDouble()`.

**Issue 2: `DeserializeGeoDict` also has `JsonElement` inner values**  
After deserializing `__window` from `JsonElement` to `Dictionary<string, object>`, the resulting dict's values are themselves `JsonElement` numbers (not native `double`). The same `ToDouble` fix resolves this.

**Issue 3: Headless Avalonia tests and `[ThreadStatic]` for `LastCreated`**  
`StatefulTestPanelViewModel.LastCreated` needed to be thread-static to avoid cross-test pollution when tests run in parallel. Used `[ThreadStatic]` attribute on the backing field.
