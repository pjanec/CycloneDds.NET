# BATCH-02: ViewModel Disposal Fix + Schema Discovery + Topic Explorer + Dummy Generator

**Batch Number:** BATCH-02  
**Tasks:** CORRECTIVE-0 (ViewModel disposal), TASK-C001, TASK-C002, TASK-D001  
**Phase:** Corrective + Phase 2 (Schema & Topic Discovery) + Phase 3 (Backend Prover)  
**Estimated Effort:** 16–20 hours  
**Priority:** HIGH  
**Dependencies:** BATCH-01 complete

---

## 📋 Onboarding & Workflow

### Developer Instructions

This batch has four goals:

1. **CORRECTIVE-0 (P1 fix, mandatory first):** `AvaloniaWindowManager` must call `IDisposable.Dispose()` on ViewModels when panels close. This was missed in BATCH-01 and is architecturally critical.
2. **TASK-C001:** Implement `WorkspaceManagerPlugin` with Schema Sources panel in `DdsMonitor.Avalonia.StandardPlugin`.
3. **TASK-C002:** Implement `TopicExplorerPlugin` in the same `StandardPlugin` assembly.
4. **TASK-D001:** Implement `DummyDataGeneratorPlugin` in the same `StandardPlugin` assembly.

### Required Reading (IN ORDER)

1. **BATCH-01 Review:** `.dev-workstream/avalonia-mvp/reviews/BATCH-01-REVIEW.md` — understand the P1 defect and debt items.
2. **Onboarding:** `.dev-workstream/avalonia-mvp/ONBOARDING.md` — folder layout and key files.
3. **Design §6 and §7:** `.dev-workstream/avalonia-mvp/DESIGN.md` — Phase 2 (Schema & Topic Discovery) and Phase 3 (Backend Prover).
4. **Task Details:** `.dev-workstream/avalonia-mvp/TASK-DETAIL.md` — TASK-C001, TASK-C002, TASK-D001 sections.
5. **Developer Skill:** `.github/skills/developer/SKILL.md` — batch workflow guide.

### Source Code Locations

- **Shell (fix location):** `tools/DdsMonitor/DdsMonitor.Avalonia/AvaloniaWindowManager.cs`
- **New plugin assembly to create:** `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/`
- **Engine Plugin contracts:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/` — `IMonitorPlugin.cs`, `IMenuRegistry.cs`, `IContextMenuRegistry.cs`, `ISampleViewRegistry.cs`
- **Engine services:** `tools/DdsMonitor/DdsMonitor.Engine/` — `IAssemblySourceService.cs`, `ITopicRegistry.cs`, `IDdsBridge.cs`, `IEventBroker.cs`, `EventBrokerEvents.cs`, `PanelState.cs`
- **Existing tests:** `tests/DdsMonitor.Avalonia.Tests/`, `tests/DdsMonitor.Engine.Tests/`

### Report Submission

**When done, submit your report to:**  
`.dev-workstream/avalonia-mvp/reports/BATCH-02-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/avalonia-mvp/questions/BATCH-02-QUESTIONS.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **CORRECTIVE-0:** Fix ViewModel disposal → Write/extend tests → **ALL tests pass** ✅
2. **TASK-C001:** WorkspaceManagerPlugin → Write tests → **ALL tests pass** ✅
3. **TASK-C002:** TopicExplorerPlugin → Write tests → **ALL tests pass** ✅
4. **TASK-D001:** DummyDataGeneratorPlugin → Write tests → **ALL tests pass** ✅

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written
- ✅ **ALL tests passing** (including all previous tests)

**No permission needed. Fix failures at the root cause. Work autonomously until done, then write the report.**

---

## 📌 CORRECTIVE-0 — Fix ViewModel IDisposable Disposal in AvaloniaWindowManager

**Source:** BATCH-01-REVIEW.md P1 defect  
**File:** `tools/DdsMonitor/DdsMonitor.Avalonia/AvaloniaWindowManager.cs`

### Problem

In `OpenPanelWindow`, the ViewModel (`vm`) is created via `ActivatorUtilities.CreateInstance` and then given to `_viewRegistry.BuildView(vm)`. The Window is tracked in `_openWindows[panelId]`. However, the ViewModel object itself is not tracked, so `OnWindowClosed` cannot call `Dispose()` on it.

### Fix Required

1. **Track ViewModels alongside Windows.** Add a dictionary `Dictionary<string, object> _viewModels` (keyed by panelId) alongside `_openWindows`.
2. In `OpenPanelWindow`, after creating `vm`, store it: `_viewModels[panelState.PanelId] = vm;`.
3. In `OnWindowClosed`, retrieve the ViewModel from `_viewModels` and call `Dispose()` if it implements `IDisposable`. Remove it from `_viewModels`.
4. In `ClosePanel`, similarly clean up the `_viewModels` entry.

```csharp
// In OnWindowClosed:
object? vm;
lock (_lock)
{
    _viewModels.TryGetValue(panelState.PanelId, out vm);
    _viewModels.Remove(panelState.PanelId);
    // ... rest of cleanup
}
if (vm is IDisposable disposable)
    disposable.Dispose();
```

### Tests Required

Extend `tests/DdsMonitor.Avalonia.Tests/ShellTests.cs` with:
- **Dispose called on close:** Register a ViewModel that implements `IDisposable` (disposable flag set in `Dispose`). Spawn panel, close it → ViewModel's `Dispose()` was called.
- **Dispose NOT called when panel still open:** Spawn panel, don't close → `Dispose()` not called.
- **Double close:** Close an already-closed panel → no exception, no double-dispose.

---

## 📌 TASK-C001 — WorkspaceManagerPlugin: Schema Sources Panel

**Design Reference:** `.dev-workstream/avalonia-mvp/DESIGN.md` §6 — Phase 2  
**Task Detail Reference:** `.dev-workstream/avalonia-mvp/TASK-DETAIL.md` TASK-C001 section  

### Project to Create

Create `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/DdsMonitor.Avalonia.StandardPlugin.csproj`:
- Target `net8.0` class library.
- Reference `Avalonia`, `DdsMonitor.Engine`, `DdsMonitor.Avalonia.Core`.
- Do **NOT** reference `DdsMonitor.Avalonia` (the shell executable) — plugins must not reference the shell.

This one assembly will house all five V1 plugins (multi-plugin-per-assembly pattern).

### What To Implement

See full scope in TASK-DETAIL.md#TASK-C001. Summary:

- **`WorkspaceManagerPlugin`** (implements `IMonitorPlugin`):
  - `Name`: `"WorkspaceManager"`, `Version`: `"1.0"`
  - `Initialize(IMonitorContext)`: registers `"Tools/Schema Sources…"` menu item → calls `IWindowManager.SpawnPanel("SchemaSources", null)`.
  - Does NOT register any DI services in `ConfigureServices` (the ViewModels are instantiated by `AvaloniaWindowManager` via `ActivatorUtilities`).

- **`SchemaSourcesViewModel`** (implements `IStatefulViewModel` — does NOT need `IDisposable` since it subscribes to service events via weak patterns, not broker):
  - Constructor: `(IAssemblySourceService assemblyService, ITopicRegistry topicRegistry, IWindowManager windowManager)`
  - `Entries`: observable list from `IAssemblySourceService.AssemblyPaths`.
  - Refreshes on `IAssemblySourceService.Changed`.
  - `AddAssembly(string path)`: calls `IAssemblySourceService.Add(path)`.
  - `RemoveAssembly(int index)`: calls `IAssemblySourceService.Remove(index)` if it exists.
  - `IsCliOverride`: mirrors `IAssemblySourceService.IsCliOverride`.

- **`SchemaSourcesView.axaml`**: Avalonia `UserControl` with a `ListBox` showing loaded DLL file names + topics, "Add…" and "Remove" buttons.

- Register `SchemaSourcesViewModel` factory in `WorkspaceManagerPlugin.Initialize` via `IAvaloniaViewRegistry` (get from `IMonitorContext.GetFeature<IAvaloniaViewRegistry>()`):
  ```csharp
  context.GetFeature<IAvaloniaViewRegistry>()!
      .Register<SchemaSourcesViewModel>(vm => new SchemaSourcesView { DataContext = vm });
  ```

**Important constraint from TASK-DETAIL.md:** Must use `IAssemblySourceService.IsCliOverride` to disable Add/Remove buttons and show a `"CLI override active"` notice.

### Tests Required (new `tests/DdsMonitor.Avalonia.StandardPlugin.Tests/`)

Create a new xUnit test project. At minimum:
- `WorkspaceManagerPlugin_Initialize_RegistersSchemaSourcesMenuItem`: plugin calls `Initialize`, then `IMenuRegistry.GetTopLevelMenus()` contains a path that includes "Schema Sources".
- `SchemaSourcesViewModel_AddAssembly_CallsAssemblySourceService`: mock `IAssemblySourceService`; call `AddAssembly` → service `Add` invoked once with the path.
- `SchemaSourcesViewModel_RemoveAssembly_CallsAssemblySourceService`: mock; call `RemoveAssembly(0)` → service `Remove(0)` invoked.
- `SchemaSourcesViewModel_ChangedEvent_RefreshesEntries`: trigger `IAssemblySourceService.Changed` → `Entries` collection updated to reflect new count.
- `SchemaSourcesViewModel_IsCliOverride_True_ReflectedInViewModel`: `IAssemblySourceService.IsCliOverride = true` → ViewModel exposes true.

---

## 📌 TASK-C002 — TopicExplorerPlugin

**Design Reference:** `.dev-workstream/avalonia-mvp/DESIGN.md` §6 — Phase 2  
**Task Detail Reference:** `.dev-workstream/avalonia-mvp/TASK-DETAIL.md` TASK-C002 section  

### What To Implement

See full scope in TASK-DETAIL.md#TASK-C002. Summary:

- **`TopicExplorerPlugin`** (implements `IMonitorPlugin`):
  - `Initialize(IMonitorContext)`:
    - Registers `"View/Topic Explorer"` menu item → toggles `TopicExplorer` window.
    - Registers toolbar button via `IToolbarRegistry.Register("TopicExplorer", ToggleWindow, tooltip: "Topic Explorer")`.
    - Calls `IWindowManager.SpawnPanel("TopicExplorer", null)` to auto-open at startup.

- **`TopicExplorerViewModel`** (implements `IStatefulViewModel`, `IDisposable`):
  - Constructor: `(ITopicRegistry topicRegistry, IContextMenuRegistry contextMenuRegistry, IEventBroker eventBroker, IUserSettings userSettings)`
  - `Topics`: `ObservableCollection<TopicMetadata>` reflecting `ITopicRegistry.AllTopics` (filtered by `ShowHidden` setting).
  - `ShowHidden`: bool property, persisted via `IUserSettings.Set("TopicExplorer", "ShowHidden", value)` on change; loaded in `Initialize(dict)` from `IUserSettings.Get("TopicExplorer", "ShowHidden", false)`.
  - Subscribes to `ITopicRegistry.Changed` on the UI thread via `SubscribeOnUiThread` — refreshes `Topics`.
  - `GetContextMenu(TopicMetadata meta)`: calls `IContextMenuRegistry.GetItems<TopicMetadata>(meta)` and returns items.
  - `OpenSamplesViewer(TopicMetadata meta)`: publishes `SpawnPanelEvent("SamplesViewer", new Dictionary<string,object>{["TopicName"]=meta.TopicName})` via `IEventBroker`.
  - **MUST implement `IDisposable`**: dispose all subscription tokens returned by `SubscribeOnUiThread`.

- **`TopicExplorerView.axaml`**: `ListBox` of topics. Right-click → context menu from `GetContextMenu`. Double-click → `OpenSamplesViewer`.

- Register `TopicExplorerViewModel` factory in `TopicExplorerPlugin.Initialize` via `IAvaloniaViewRegistry`.

**"Show Hidden Topics" filter rule (from TASK-DETAIL.md):** Filter topics whose `ShortName` starts with `"_"` or whose `Namespace` contains `"Internal"`.

### Tests Required (extend `tests/DdsMonitor.Avalonia.StandardPlugin.Tests/`)

- `TopicExplorerPlugin_Initialize_SpawnsTopicExplorerPanel`: `Initialize` called → `IWindowManager.SpawnPanel("TopicExplorer", null)` was called.
- `TopicExplorerPlugin_Initialize_RegistersViewMenuItem`: menu contains "Topic Explorer" item.
- `TopicExplorerPlugin_Initialize_RegistersToolbarButton`: toolbar has entry with id "TopicExplorer".
- `TopicExplorerViewModel_TopicRegistryChanged_RefreshesTopics`: add topic to mock `ITopicRegistry`, fire `Changed` → `Topics` collection updated.
- `TopicExplorerViewModel_ShowHidden_False_FiltersHiddenTopics`: `ShowHidden=false`, add a topic with `ShortName="_Hidden"` → `Topics` does NOT contain it.
- `TopicExplorerViewModel_ShowHidden_True_ShowsAllTopics`: `ShowHidden=true` → hidden topic IS visible.
- `TopicExplorerViewModel_ShowHiddenPersistedToUserSettings`: set `ShowHidden=true` → `IUserSettings.Get("TopicExplorer", "ShowHidden", false)` returns `true`.
- `TopicExplorerViewModel_OpenSamplesViewer_PublishesSpawnPanelEvent`: call `OpenSamplesViewer(meta)` → `IEventBroker.Publish` called with `SpawnPanelEvent` whose `PanelTypeName == "SamplesViewer"` and state contains `"TopicName"`.
- `TopicExplorerViewModel_Dispose_DisposesSubscriptionTokens`: after `Dispose()`, broker subscriptions removed (verify by publishing an event and asserting handler NOT called).
- `TopicExplorerViewModel_GetContextMenu_CallsContextMenuRegistry`: calls `GetContextMenu(meta)` → `IContextMenuRegistry.GetItems<TopicMetadata>(meta)` was called.

---

## 📌 TASK-D001 — DummyDataGeneratorPlugin

**Design Reference:** `.dev-workstream/avalonia-mvp/DESIGN.md` §7 — Phase 3  
**Task Detail Reference:** `.dev-workstream/avalonia-mvp/TASK-DETAIL.md` TASK-D001 section  

### What To Implement

See full scope in TASK-DETAIL.md#TASK-D001. Summary:

- Define `HeartbeatSample` struct in `StandardPlugin`:
  ```csharp
  [DdsTopic]
  public struct HeartbeatSample
  {
      [DdsKey] public int Id;
      public long Timestamp;
      public int Sequence;
  }
  ```
  Adjust attribute names if they differ from the above — verify against the `CycloneDDS.Schema` project.

- **`DummyDataGeneratorPlugin`** (implements `IMonitorPlugin`):
  - `ConfigureServices(IServiceCollection)`: registers `DummyGeneratorService` as `IHostedService` and `DummyGeneratorService` itself as a singleton (so Initialize can toggle it).
  - `Initialize(IMonitorContext)`:
    - Registers `IContextMenuRegistry.RegisterProvider<TopicMetadata>` injecting `"Toggle Dummy Generator"` item.
    - Registers `"Tools/Dummy Generator"` menu item that calls `ToggleGenerator()`.

- **`DummyGeneratorService`** (implements `IHostedService`):
  - Reads `IConfiguration["GeneratorPlugin:Enabled"]` (bool, default `false`) and `IConfiguration["GeneratorPlugin:PublishRateMs"]` (int, default `100`).
  - If `Enabled`: registers `HeartbeatSample` with `ITopicRegistry` and acquires `IDdsBridge.GetWriter(meta)`.
  - Publishes on a `PeriodicTimer` or `Task.Delay` loop at `PublishRateMs`. Stress mode: `PublishRateMs <= 0` → publish without delay (as fast as possible).
  - Supports runtime toggle (`TogglePublishing()`) without restarting the service.
  - `StopAsync`: stops the loop cleanly.

### Tests Required (extend `tests/DdsMonitor.Avalonia.StandardPlugin.Tests/`)

- `DummyDataGeneratorPlugin_ConfigureServices_RegistersDummyGeneratorService`: after `ConfigureServices`, `IServiceCollection` contains `DummyGeneratorService` as `IHostedService`.
- `DummyDataGeneratorPlugin_Initialize_RegistersToolsMenu`: `IMenuRegistry` receives "Dummy Generator" item under "Tools".
- `DummyDataGeneratorPlugin_Initialize_RegistersContextMenuProvider`: `IContextMenuRegistry.GetItems<TopicMetadata>(meta)` returns a list containing "Toggle Dummy Generator".
- `DummyGeneratorService_Enabled_True_StartsPublishing`: create service with `Enabled=true`; start it; after 300 ms → at least one sample published (via mock `IDdsBridge.GetWriter.Write` call count > 0).
- `DummyGeneratorService_Enabled_False_NoPublishing`: `Enabled=false` → `GetWriter` never called after start.
- `DummyGeneratorService_Toggle_StopsAndRestartsPublishing`: start with `Enabled=true`; call `TogglePublishing()` → publishing stops (call count frozen); call again → publishing resumes.
- `DummyGeneratorService_StopAsync_CancelsLoop`: call `StopAsync` → no further publishes within 200 ms.
- `HeartbeatSample_HasNonKeyField_Timestamp`: `typeof(HeartbeatSample)` has field `Timestamp` of type `long` (or `int`) and `Sequence` of type `int`.

---

## ⚠️ Quality Standards

### Test Quality

**NOT ACCEPTABLE:**
- Tests that only verify a method was called without checking preconditions or return values.
- Tests that mock so much they verify nothing real.
- Empty `[Fact]` methods.

**REQUIRED:**
- `TopicExplorerViewModel` subscription disposal test MUST verify the handler is NOT called after `Dispose()`, not just that the token was stored.
- `DummyGeneratorService_Enabled_True_StartsPublishing` must verify actual Write calls (mock count > 0), not just that the service started.
- All ViewModel tests that check `IUserSettings` persistence must round-trip through the real `UserSettingsStore` (use a temp file path), not just assert that `Set` was called on a mock.

### Code Quality

- All ViewModels that subscribe to `IEventBroker` MUST implement `IDisposable` and dispose all subscription tokens.
- `StandardPlugin` assembly must NOT reference `DdsMonitor.Avalonia` (the shell exe).
- All new types must live in `DdsMonitor.Avalonia.StandardPlugin` namespace.
- `DummyGeneratorService` must never crash if `IDdsBridge.GetWriter` throws — log and disable.

---

## 🎯 Success Criteria (Batch Done When)

- [ ] CORRECTIVE-0: `AvaloniaWindowManager` calls `Dispose()` on ViewModel when panel closes; 3 new tests pass.
- [ ] `dotnet build tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin` — zero errors.
- [ ] `dotnet test tests/DdsMonitor.Avalonia.StandardPlugin.Tests` — all pass.
- [ ] All previously passing test suites still pass (712 + new tests total).
- [ ] BATCH-02-REPORT.md submitted.

---

## 📚 Reference Materials

- **BATCH-01 Review:** `.dev-workstream/avalonia-mvp/reviews/BATCH-01-REVIEW.md`
- **Task Detail:** `.dev-workstream/avalonia-mvp/TASK-DETAIL.md` — TASK-C001, TASK-C002, TASK-D001
- **Design:** `.dev-workstream/avalonia-mvp/DESIGN.md` — §6 (Phase 2), §7 (Phase 3)
- **Engine contracts:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/` — `IMonitorPlugin.cs`, `PluginLoader.cs`, `IMenuRegistry.cs`, `IContextMenuRegistry.cs`
- **Engine services:** `tools/DdsMonitor/DdsMonitor.Engine/` — `IAssemblySourceService.cs`, `ITopicRegistry.cs`, `IEventBroker.cs`, `EventBrokerEvents.cs`
- **Shell (fix location):** `tools/DdsMonitor/DdsMonitor.Avalonia/AvaloniaWindowManager.cs`

---

## 💡 Developer Insights Section

When writing your report, answer these questions:

**Q1:** What issues did you encounter? How did you resolve them?

**Q2:** Did the existing codebase have any surprises (e.g., missing services, unexpected API shapes)?

**Q3:** What design decisions did you make beyond the instructions? What alternatives did you consider?

**Q4:** What edge cases did you discover that weren't in the spec?

**Q5:** Are there any performance or threading concerns with the `DummyGeneratorService`?

**Q6:** What is your suggested git commit message for this batch?
