# DdsMonitor.Avalonia — Task Detail

**Reference:** See [DESIGN.md](./DESIGN.md) for architecture, rationale, and phase descriptions.

---

## Phase 0: Engine Purification

---

### TASK-A001 — Remove Blazor Types from Engine Registries

**Design Reference:** [DESIGN.md §4 — Phase 0: Engine Purification](./DESIGN.md#4-phase-0--engine-purification)

**Scope**

_In scope:_
- Change `ITypeDrawerRegistry.Register` and `GetDrawer` signatures to use `Func<DrawerContext, object>` instead of `RenderFragment<DrawerContext>`.
- Update `TypeDrawerRegistry` (the concrete class) and all its internal builder methods accordingly.
- Remove `IHandleEvent? Receiver` property from `DrawerContext` and its constructor.
- Change `ISampleViewRegistry.Register` and `GetViewer` signatures to use `Func<SampleData, object>` instead of `RenderFragment<SampleData>`.
- Update `SampleViewRegistry` (the concrete class) accordingly.
- Add Blazor adapter classes in `DdsMonitor.Blazor` that bridge the new UI-agnostic signatures back to `RenderFragment` so existing Blazor components continue to work.

_Out of scope:_
- Changing any other Engine files.
- Changing any logic in `TypeDrawerRegistry` built-in drawer implementations (they are adapted not rewritten).
- Touching `DdsMonitor.Plugins.*` plugin assemblies (they already use `IMonitorContext.GetFeature<ITypeDrawerRegistry>()` and will call `Build`; the adapter handles the rest).

**Constraints**
- The Engine must compile with no `using Microsoft.AspNetCore.Components;` anywhere after this task.
- The Blazor app must still build and pass all existing tests after the adapters are in place.
- The Blazor adapter must be the only location where `RenderFragment` is cast/produced; it must not leak into the Engine.

**Dependencies:** None — this is the first task.

**Success Conditions**
1. `dotnet build tools/DdsMonitor/DdsMonitor.Engine` succeeds with zero errors and zero warnings mentioning `AspNetCore`.
2. `dotnet build tools/DdsMonitor/DdsMonitor.Blazor` succeeds with zero errors.
3. All existing Engine unit tests pass without modification.
4. A test: instantiate `TypeDrawerRegistry`, register a `Func<DrawerContext, object>` for `int`, call `GetDrawer(typeof(int))`, assert non-null and that the returned factory is the same delegate.
5. A test: instantiate `SampleViewRegistry`, register a `Func<SampleData, object>` for a dummy type, call `GetViewer(dummyType)`, assert non-null.
6. Negative: `DrawerContext` constructed without the removed `Receiver` parameter compiles fine.

---

## Phase 1: Empty Shell

---

### TASK-B001 — Create `DdsMonitor.Avalonia.Core` Project

**Design Reference:** [DESIGN.md §5.1 — Avalonia.Core Contracts](./DESIGN.md#51-ddsmonitoravaloniacore--new-contracts)

**Scope**

_In scope:_
- Create `.NET 8` class library `tools/DdsMonitor/DdsMonitor.Avalonia.Core/DdsMonitor.Avalonia.Core.csproj`.
- Add `PackageReference` to `Avalonia` (same version as will be used by the shell).
- Add `ProjectReference` to `DdsMonitor.Engine`.
- Define and implement:
  - `IToolbarRegistry` interface + `ToolbarRegistry` singleton implementation.
  - `ToolbarEntry` record (`Id`, `Action`, `IconKey?`, `Tooltip`).
  - `IUserSettings` interface + `UserSettingsStore` implementation (saves `%APPDATA%\DdsMonitor\settings.json`).
  - `IStatefulViewModel` interface: single method `void Initialize(IDictionary<string, object> componentState)`. Implementations read initial state from the dict and store the reference for subsequent direct mutations.
  - `IAvaloniaViewRegistry` interface + `AvaloniaViewRegistry` implementation.
  - `IAvaloniaTypeDrawerRegistry` interface + `AvaloniaTypeDrawerRegistry` implementation with generic reflection-walker fallback.
  - `AvaloniaDrawerContext` class (mirrors `DrawerContext` but without Blazor dependency).
  - `IEventBrokerExtensions` static class with `SubscribeOnUiThread<TEvent>` extension method.

_Out of scope:_
- Avalonia UI controls (those live in plugin projects or the shell).
- Any DDS-specific types (those stay in the Engine).

**Constraints**
- Must not reference `DdsMonitor.Blazor`.
- `UserSettingsStore` must use `System.Text.Json` for serialization.
- `UserSettingsStore.SaveAsync()` must be debounced (fire-and-forget + replace if pending) to survive rapid property changes.
- `IAvaloniaViewRegistry.BuildView` throws `InvalidOperationException` if no factory is registered for the ViewModel type; it must never silently return `null`.
- `IAvaloniaTypeDrawerRegistry.Build` must explicitly cast the factory result to `Control` and throw `InvalidCastException` immediately if the result is not a `Control`. The error must surface the misbehaving plugin at registration/build time.
- `SubscribeOnUiThread<TEvent>` must marshal the handler via `Avalonia.Threading.Dispatcher.UIThread.InvokeAsync`.
- ViewModels that call `SubscribeOnUiThread` must implement `IDisposable` and store each returned `IDisposable` subscription token. `AvaloniaWindowManager` calls `Dispose()` on any ViewModel implementing `IDisposable` when its panel closes. This is the prescribed pattern for all broker-subscribing ViewModels.

**Dependencies:** TASK-A001 (Engine must be Blazor-free before referencing it from a non-Blazor project).

**Success Conditions**
1. Project builds targeting `net8.0` with no warnings.
2. `UserSettingsStore`: set key "TopicExplorer/ShowHidden" = true, call `SaveAsync`, read the JSON file from disk, assert the key is present with correct value.
3. `UserSettingsStore`: call `Get<bool>("TopicExplorer", "ShowHidden", false)` without prior `Set`; assert result is `false`.
4. `ToolbarRegistry`: register two entries, assert `Entries` returns both in registration order; assert `Changed` event fired once per registration.
5. `AvaloniaTypeDrawerRegistry`: register a factory for `string`, call `Build` with a `AvaloniaDrawerContext` for `string`, assert returned `Control` is non-null.
6. `AvaloniaTypeDrawerRegistry.Build` for an unregistered type with string-like properties falls back to a generic `StackPanel` walker (non-null control returned).
7. `SubscribeOnUiThread` test: publish event on a background thread, assert handler is invoked on Avalonia UI thread (check `Dispatcher.UIThread.CheckAccess()`).
8. `IAvaloniaTypeDrawerRegistry.Build`: register a factory that returns a plain `object` (not a `Control`) → assert `InvalidCastException` is thrown with a message naming the offending registered type.

---

### TASK-B002 — Create `DdsMonitor.Avalonia` Shell Project

**Design Reference:** [DESIGN.md §5.2 — Shell Executable](./DESIGN.md#52-ddsmonitoravalonia--shell-executable)

**Scope**

_In scope:_
- Create `tools/DdsMonitor/DdsMonitor.Avalonia/DdsMonitor.Avalonia.csproj` as an Avalonia desktop app (`OutputType=WinExe`).
- Add `PackageReference`s: `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`, `Microsoft.Extensions.Hosting`.
- Add `ProjectReference`s: `DdsMonitor.Engine`, `DdsMonitor.Avalonia.Core`.
- Implement `App.axaml` / `App.axaml.cs` wiring `IServiceProvider` from the Generic Host.
- Implement `ShellWindow.axaml` with:
  - Top `Menu` control bound to `IMenuRegistry` observable menu tree.
  - Horizontal `StackPanel` toolbar bound to `IToolbarRegistry.Entries`.
  - Global transport control row: **Play**, **Pause**, and **Reset** `Button` controls wired to the corresponding `IDdsBridge` transport API (verify exact method names from `DdsMonitor.Blazor/MainLayout.razor` during implementation). These are shell-built-in, not plugin-provided.
  - Empty client area (no panel region — panels are independent `Window` objects).
  - Status bar placeholder (`TextBlock` with static "Ready").
- Implement `AvaloniaViewRegistry`-based `ViewLocator` that `App` uses for all content resolution.
- Implement `AvaloniaWindowManager` (Avalonia implementation of Engine's `IWindowManager`):
  - `SpawnPanel(componentTypeName, initialState?)`: looks up a registered ViewModel factory by `componentTypeName`, instantiates the ViewModel, resolves the Avalonia `Control` via `IAvaloniaViewRegistry`, creates an Avalonia `Window`, restores geometry from `initialState` (or `PanelState.ComponentState["__window"]`), shows the window.
  - `ClosePanel(panelId)`: closes and removes the window.
  - `BringToFront(panelId)`, `ShowPanel(panelId)`: focus the window.
  - On window close: persists geometry to `ComponentState["__window"]`, publishes `WorkspaceSaveRequestedEvent`.
- Implement `Program.cs` with full bootstrap sequence (see DESIGN.md §5.2).
- Implement the dual-boot decision based on `DdsSettings.HeadlessMode`.

_Out of scope:_
- Plugin code (that goes in TASK-B003+).
- Concrete panel implementations.

**Constraints**
- Shell must not reference `DdsMonitor.Blazor`.
- The shell must register `IWindowManager` as a singleton that replaces the Engine's default implementation.
- `AvaloniaWindowManager.SpawnPanel` must not throw if the panel is already open — it must focus it instead.
- The `Menu` binding to `IMenuRegistry` must react dynamically to `IMenuRegistry.Changed` so that plugins registering menu items after `Initialize` still appear.
- The toolbar similarly must observe `IToolbarRegistry.Changed`.
- The `PluginLoader` instance created inside `AddDdsMonitorServices` is reused; the shell does not create a second one.
- `pluginLoader.InitializePlugins(monitorContext)` must be called after the host starts and before `ShellWindow` is shown.

**Dependencies:** TASK-B001 (Core contracts), TASK-A001 (Engine must be Blazor-free).

**Success Conditions**
1. `dotnet run --project DdsMonitor.Avalonia` launches without exceptions; `ShellWindow` appears with an empty menu and toolbar.
2. Running with `--DdsSettings:HeadlessMode=Record` does not open a window; process stays alive and exits cleanly on Ctrl+C.
3. Running with no plugins in `./plugins` folder: app starts and closes without errors.
4. `ShellWindow` `Menu` shows a top-level `"File"` entry added by the shell itself (exit command) — proves menu binding works before any plugin loads.
5. `AvaloniaWindowManager.SpawnPanel("Test", null)` (called with a test ViewModel registered): opens a window. Second call with same id: focuses existing window (window count stays 1).
6. Window close: geometry saved to `ComponentState["__window"]`; re-spawning restores position.
7. Shell toolbar shows Play, Pause, and Reset buttons; clicking Pause calls the `IDdsBridge` pause API and disables Play; clicking Play re-enables ingestion.

---

### TASK-B003 — Integrate PluginLoader + InitializePlugins into Shell

**Design Reference:** [DESIGN.md §5.2 — Generic Host Bootstrap](./DESIGN.md#52-ddsmonitoravalonia--shell-executable)

**Scope**

_In scope:_
- Wire the existing `PluginLoader` (already instantiated inside `AddDdsMonitorServices`) into the shell startup.
- Add `DdsMonitor.Avalonia.Core` to the `PluginLoader.SharedAssemblyNames` set (so plugins that reference Core get the host's singleton instances).
- Verify that `IMonitorContext.GetFeature<IToolbarRegistry>()` returns the registered singleton.
- Verify that `IMonitorContext.GetFeature<IAvaloniaTypeDrawerRegistry>()` returns the registered singleton.
- Verify that `IMonitorContext.GetFeature<IUserSettings>()` returns the registered singleton.

_Out of scope:_
- Writing any specific plugin.
- The test that proves multi-plugin-per-assembly (that is validated when `StandardPlugin` is written).

**Constraints**
- `DdsMonitor.Avalonia.Core` must appear in `SharedAssemblyNames` to guarantee type identity.
- If `./plugins` folder does not exist, `PluginLoader` must silently do nothing (already the Engine's behavior).
- `Initialize` is called only after the DI container is fully built and `host.StartAsync()` has run.

**Dependencies:** TASK-B002.

**Success Conditions**
1. A minimal test plugin (one DLL with two `IMonitorPlugin` classes) placed in `./plugins` is discovered; both `ConfigureServices` and both `Initialize` are called.
2. `IMonitorContext.GetFeature<IToolbarRegistry>()` returns non-null from within a plugin's `Initialize`.
3. `IMonitorContext.GetFeature<IAvaloniaTypeDrawerRegistry>()` returns non-null.
4. `IMonitorContext.GetFeature<IUserSettings>()` returns non-null.
5. A plugin with a bad DLL (corrupt bytes) in `./plugins` does not crash the app — it is skipped and an error is logged.

---

## Phase 2: Schema & Topic Discovery

---

### TASK-C001 — WorkspaceManagerPlugin: Schema Sources Panel

**Design Reference:** [DESIGN.md §6 — Phase 2](./DESIGN.md#6-phase-2--schema--topic-discovery)

**Scope**

_In scope:_
- Add `WorkspaceManagerPlugin` class (one of the five `IMonitorPlugin` classes in `StandardPlugin`).
- In `Initialize`: register `"Tools/Schema Sources…"` menu item via `IMenuRegistry.AddMenuItem`.
- Implement `SchemaSourcesViewModel` and `SchemaSourcesView.axaml`:
  - List of `IAssemblySourceService.Entries`.
  - "Add…" button → Avalonia `OpenFileDialog` filtered to `*.dll` → calls `IAssemblySourceService.Add(path)`.
  - "Remove" button → calls `IAssemblySourceService.Remove(selectedIndex)`.
  - Subscribes to `IAssemblySourceService.Changed` to refresh the list.
  - For each entry, shows the DLL file name and a sub-list of discovered `TopicMetadata.ShortName` values from `ITopicRegistry.AllTopics` filtered by `AssemblyPath`.
- On menu item click: `IWindowManager.SpawnPanel("SchemaSources", null)`.
- Register the ViewModel factory in `ConfigureServices` so `AvaloniaWindowManager` can resolve it.

_Out of scope:_
- IDL import (not in V1).
- Reorder functionality (deferred).

**Constraints**
- Must use `IAssemblySourceService.IsCliOverride` to disable Add/Remove buttons and show a
  `"CLI override active"` notice when the sources were set via `--AppSettings:TopicSources`.
- The file dialog must be modal relative to `ShellWindow`.
- Must not call `ITopicRegistry.Register` directly — `IAssemblySourceService.Add` triggers the scan.

**Dependencies:** TASK-B003.

**Success Conditions**
1. Click "Tools → Schema Sources…" → `SchemaSources` window opens (singleton; second click focuses).
2. Click "Add…", select a valid DDS schema DLL → new entry appears in the list; topics from that DLL appear in the explorer (next task) without app restart.
3. Select an entry and click "Remove" → entry disappears; topics from that DLL are no longer listed.
4. Start with `--AppSettings:TopicSources:0=somefile.dll` → Add/Remove buttons are disabled; notice text visible.
5. `IAssemblySourceService.Changed` fires → the view refreshes within one Avalonia dispatch cycle.

---

### TASK-C002 — TopicExplorerPlugin

**Design Reference:** [DESIGN.md §6 — Phase 2](./DESIGN.md#6-phase-2--schema--topic-discovery)

**Scope**

_In scope:_
- Add `TopicExplorerPlugin` class in `StandardPlugin`.
- `TopicExplorerViewModel`: `ObservableCollection<TopicMetadata>` refreshed on `ITopicRegistry.Changed`.
- `TopicExplorerView.axaml`: `ListBox` of topics showing `ShortName` + namespace.
- In `Initialize`:
  - Register `"View/Topic Explorer"` menu item (toggles window open/closed).
  - Register toolbar button ("Topic Explorer" icon, toggles).
  - Call `IWindowManager.SpawnPanel("TopicExplorer", null)` to auto-open at startup.
- "Show Hidden/Internal Topics" checkbox: state loaded from `IUserSettings` on open; saved via `IUserSettings.Set` on change.
- Right-click on a topic item: build context menu via `IContextMenuRegistry.GetItems<TopicMetadata>(topicMeta)`.
- Double-click on a topic: publish `SpawnPanelEvent("SamplesViewer", new Dictionary { ["TopicName"] = topicName })` via `IEventBroker`.
- Subscribe to `IEventBroker` events on the UI thread (use `SubscribeOnUiThread<T>`).
- Implement `IDisposable`: dispose all subscription tokens returned by `SubscribeOnUiThread` in `Dispose()`. `AvaloniaWindowManager` will call `Dispose()` on panel close.

_Out of scope:_
- Subscription toggle (subscribe/unsubscribe from topic) — deferred.
- Sparkline metrics in the topic list — deferred.

**Constraints**
- The ViewModel must not hold a direct reference to `SamplesViewerPlugin` or any other plugin type.
- `TopicExplorerViewModel` must implement `IDisposable` and dispose all `IEventBroker` subscription tokens on disposal.
- "Show Hidden Topics" filters topics whose `ShortName` starts with `"_"` or whose `Namespace` contains `"Internal"` — this is the convention; deferred full implementation; V1 just proves the `IUserSettings` round-trip.
- Context menu must call `IContextMenuRegistry.GetItems<TopicMetadata>` — the registry call must happen at open time of the menu, not at topic list render time.
- Double-click must not open a duplicate window; `IWindowManager.SpawnPanel` handles idempotency.

**Dependencies:** TASK-C001 (WorkspaceManagerPlugin must exist first; shares the same `StandardPlugin` assembly).

**Success Conditions**
1. App launches → `TopicExplorer` window opens automatically (PanelId `"TopicExplorer"`).
2. Load a schema DLL via the Schema Sources panel → topics appear in the explorer `ListBox` within one Avalonia dispatch cycle.
3. "Show Hidden Topics" checkbox state survives app restart (read from `settings.json`).
4. Right-click on a topic → context menu appears (may be empty if no providers registered yet; must not throw).
5. Double-click `"Heartbeat_Topic"` → `SpawnPanelEvent` is published; IWindowManager receives it (SamplesViewer not yet implemented — event just needs to be published).
6. Double-click same topic again → second event published; IWindowManager still shows only one window of that kind (idempotency tested after SamplesViewer is wired in Phase 4).
7. `"View/Topic Explorer"` menu item and toolbar button both trigger the window toggle.

---

## Phase 3: Backend Prover

---

### TASK-D001 — DummyDataGeneratorPlugin

**Design Reference:** [DESIGN.md §7 — Phase 3](./DESIGN.md#7-phase-3--backend-prover)

**Scope**

_In scope:_
- Add `DummyDataGeneratorPlugin` class in `StandardPlugin`.
- Define a synthetic `[DdsTopic]`-annotated `HeartbeatSample` struct in `StandardPlugin` (or in a
  shared location if needed).
- In `ConfigureServices`: register `DummyGeneratorService` as `IHostedService`.
- `DummyGeneratorService`: reads `IConfiguration["GeneratorPlugin:Enabled"]` (bool, default false)
  and `IConfiguration["GeneratorPlugin:PublishRateMs"]` (int, default 100).
  - If `Enabled`, registers the `HeartbeatSample` type with `ITopicRegistry` and acquires
    `IDdsBridge.GetWriter(meta)`.
  - Publishes on a timer loop at `PublishRateMs` interval.
  - Supports stress mode: if `PublishRateMs <= 0`, publish as fast as possible (for Phase 4 5 kHz test).
- In `Initialize`:
  - Register `IContextMenuRegistry.RegisterProvider<TopicMetadata>` injecting "Toggle Dummy Generator".
  - Register `"Tools/Dummy Generator"` menu item (toggles the service on/off at runtime).

_Out of scope:_
- Any UI panel for the generator (headless-only feature in V1).
- Replay-mode integration.

**Constraints**
- `DummyGeneratorService` must run and publish even when `DdsSettings.HeadlessMode == HeadlessMode.Record` — the headless recorder should capture its output.
- Must not hold `IDdsBridge.GetWriter` if `Enabled=false` at startup.
- The `HeartbeatSample` type must have at least one non-key field (`Timestamp: long`, `Sequence: int`) so `TopicMetadata` compiles a non-trivial expression tree.
- Toggle at runtime: the "Toggle Dummy Generator" context menu item and menu item must start/stop the publish loop without restarting the `IHostedService`.

**Dependencies:** TASK-B003 (shell + plugin loader wired up).

**Success Conditions**
1. `--DdsSettings:HeadlessMode=Record --DdsSettings:HeadlessFilePath=out.json --GeneratorPlugin:Enabled=true`: process runs, Ctrl+C → `out.json` exists and contains sample records.
2. Interactive mode, `--GeneratorPlugin:Enabled=true`: topics appear in the explorer (HeartbeatSample topic visible).
3. Right-click on `HeartbeatSample` topic in the explorer → "Toggle Dummy Generator" item visible (injected by `DummyDataGeneratorPlugin`).
4. Click "Toggle Dummy Generator" → service pauses (no more samples appear in viewer); click again → resumes.
5. `--GeneratorPlugin:PublishRateMs=0 --GeneratorPlugin:Enabled=true`: stress mode; viewer (in Phase 4) must handle this without crash.
6. With `Enabled=false` (default): no topic, no writer, no CPU usage from the service.

---

## Phase 4: Firehose UI

---

### TASK-E001 — SamplesViewerPlugin: Grid & Filtering

**Design Reference:** [DESIGN.md §8 — Phase 4](./DESIGN.md#8-phase-4--firehose-ui)

**Scope**

_In scope:_
- Add `SamplesViewerPlugin` class in `StandardPlugin`.
- In `Initialize`: subscribe to `SpawnPanelEvent` (on UI thread) where `PanelTypeName == "SamplesViewer"`.
- On event: call `IWindowManager.SpawnPanel("SamplesViewer_<TopicName>", state)`.
- `SamplesViewerViewModel`:
  - Constructor: `ISampleStore store, IFilterCompiler filterCompiler, TopicMetadata? meta`.
  - Creates `SampleView(store)` on construction.
  - `FilterText` property → on change, compiles via `IFilterCompiler.Compile(text, meta)` → calls `_view.SetFilter(result.Predicate)`.
  - Subscribes to `_view.OnViewRebuilt` (background thread) → marshals to UI thread via `SubscribeOnUiThread`.
  - Exposes `VirtualizedItemsProvider` that fulfils range requests via `_view.GetVirtualView(start, count)`.
  - Disposes `_view` on ViewModel dispose.
  - Implements `IStatefulViewModel`: in `Initialize(dict)` reads initial `FilterText`, `SortFieldPath`, `SortDirection`, `SelectedColumnPaths` from the dict **and writes `ComponentState["TopicName"] = _meta.TopicName`** so the window manager can restore parameterised panels on app restart; thereafter writes back to the dict directly whenever those properties change, and publishes `WorkspaceSaveRequestedEvent`.
  - Implements `IDisposable`: disposes `_view` and all `IEventBroker` subscription tokens.
- `SamplesViewerView.axaml`: `TreeDataGrid` bound to `VirtualizedItemsProvider`; filter `TextBox`; sort by column header.
- Registers context menu providers for `SampleData` rows via `IContextMenuRegistry.RegisterProvider<SampleData>`.

_Out of scope:_
- Detail inspector (TASK-E002).
- Sparklines, bandwidth stats.
- Custom column picker beyond the default set.

**Constraints**
- Filter compilation errors (`FilterResult.IsValid == false`) must show a non-throwing inline error label (not a dialog or exception).
- The `TreeDataGrid` must use Avalonia's virtualization — no `ItemsControl` with full materialization.
- `OnViewRebuilt` handler must never touch UI controls directly on the background thread.
- `SamplesViewerPlugin` must not import or reference `TopicExplorerPlugin` types.
- `SamplesViewerViewModel` must implement `IDisposable` (disposes `SampleView` and all broker subscriptions). `AvaloniaWindowManager` calls `Dispose()` on panel close.
- On app startup restoration, `AvaloniaWindowManager` must extract `ComponentState["TopicName"]`, resolve the `TopicMetadata` from `ITopicRegistry`, and pass it to the `SamplesViewerViewModel` factory. If the topic is not yet in the registry (schema DLL not yet loaded), restoration of that panel must be deferred until the topic appears.

**Dependencies:** TASK-D001 (dummy generator produces data for the stress test), TASK-C002 (TopicExplorer publishes `SpawnPanelEvent`).

**Success Conditions**
1. Double-click `"Heartbeat_Topic"` in explorer → `SamplesViewer_Heartbeat_Topic` window opens with a `TreeDataGrid`.
2. Double-click same topic → existing window focused, not duplicated.
3. Double-click `"AnotherTopic"` → second, separate `SamplesViewer` window opens.
4. With dummy generator at 5 000 samples/s for 60 s: UI remains responsive (no freeze, grid scrolls smoothly), RAM does not grow unboundedly (store is bounded by Engine's max-store limit).
5. Enter filter text `"Payload.Sequence > 100"` → grid updates within 200 ms to show only matching rows.
6. Enter invalid filter `"not a valid expression"` → inline error label shows `FilterResult.ErrorMessage`; grid shows unfiltered data.
7. Close the `SamplesViewer` window → `SampleView` is disposed (no background worker leak; verified by no threads remaining for that view after close).
8. Restart app → `SamplesViewer_Heartbeat_Topic` reopens with restored filter text and sort direction (IStatefulViewModel round-trip, after Phase 6 polish).

---

### TASK-E002 — DetailInspectorPlugin: Linked Inspector Panel

**Design Reference:** [DESIGN.md §8 — Firehose UI — DetailInspectorPlugin](./DESIGN.md#8-phase-4--firehose-ui)

**Scope**

_In scope:_
- Add `DetailInspectorPlugin` class in `StandardPlugin` (separate from `SamplesViewerPlugin`).
- In `Initialize`:
  - Register a context menu provider for `SampleData` rows via `IContextMenuRegistry`:
    `"Open Inspector"` → spawns a new `DetailInspector_<uuid>` window pre-linked to the source panel.
  - Subscribe to `SampleSelectedEvent` on UI thread to route samples to the correct open inspector.
- `DetailInspectorViewModel`:
  - Constructor: receives `string sourcePanel` and initial `SampleData?`.
  - Implements `IStatefulViewModel`: in `Initialize(dict)` reads `IsLinked` (bool, default `true`) and
    `SourcePanelId` (string) from the dict; writes them back on change; stores dict reference.
  - Implements `IDisposable`: disposes all `IEventBroker` subscription tokens.
  - When `IsLinked == true`: subscribes to `SampleSelectedEvent` filtered to `SourcePanelId`,
    updates the displayed sample on each event.
  - When `IsLinked == false`: freezes the current sample (no further updates).
  - Exposes `IsLinked` as a bindable property with a toggle command (Link/Unlink button).
  - On sample update: first checks `ISampleViewRegistry.GetViewer(payload.GetType())` — if a
    custom Avalonia viewer is registered, builds it. Otherwise iterates
    `TopicMetadata.AllFields` (pre-compiled `FieldMetadata.Getter`) to build a hierarchical
    `FieldInspectorItemViewModel` tree; leaf values rendered via `IValueFormatterRegistry`.
  - Exposes `SampleInfoViewModel` (read-only properties from `SampleData.Info`): write
    timestamp, reception timestamp, generation rank, and other QoS-observable fields.
  - Exposes `SenderViewModel` (from `SampleData.SenderIdentity`): process ID, IP address.
- `DetailInspectorView.axaml`: tabbed or split layout with:
  - **Fields** tab: `TreeView` (or flat `ItemsControl`) bound to field tree.
  - **Sample Info** tab: read-only property grid from `SampleInfoViewModel`.
  - **Sender** tab: read-only grid from `SenderViewModel`.
  - **Link/Unlink** toggle button in the window's toolbar row.
- PanelId convention: `"DetailInspector_<uuid>"` generated per-spawn (each inspector is independent).

_Out of scope:_
- Two-way editing in the inspector (that is `SendSamplePlugin`).
- JSON raw view, hex view — deferred to Phase 8.
- `SampleData.SenderIdentity` property mapping: exact property names must be verified from
  the Engine's `SampleData` class during implementation.

**Constraints**
- `FieldMetadata.Getter` must only be invoked on the UI thread (post-marshal), never on the
  `OnViewRebuilt` background thread.
- Inspector must not crash if `sample.Payload` is null or `TopicMetadata` has no fields.
- `ISampleViewRegistry` lookup follows the Engine convention: exact type → base types → interfaces.
- `IsLinked` and `SourcePanelId` must survive app restart (persisted in `ComponentState`).
- `DetailInspectorPlugin` must not reference `SamplesViewerPlugin` types; cross-plugin
  communication is exclusively via `IEventBroker` and `IContextMenuRegistry`.

**Dependencies:** TASK-E001 (`SamplesViewerPlugin` publishes `SampleSelectedEvent`).

**Success Conditions**
1. Right-click on a sample row → "Open Inspector" menu item visible.
2. Click "Open Inspector" → `DetailInspector_<uuid>` window opens, linked to the source panel, showing the selected sample's payload fields.
3. Select a different row in the linked `SamplesViewer` → the inspector updates within one Avalonia dispatch cycle.
4. Click "Unlink" → inspector freezes current sample; selecting different rows in the `SamplesViewer` no longer updates it.
5. Open two inspectors from two different `SamplesViewer` windows → each follows its own source independently.
6. Scalar fields (int, double, string, bool) show formatted values via `IValueFormatterRegistry`.
7. Nested struct fields show as expandable nodes; array fields show count and are expandable.
8. A test plugin registers a custom `ISampleViewRegistry` viewer for `HeartbeatSample` → the inspector shows the custom control instead of the generic tree.
9. **Sample Info** tab shows write timestamp, reception timestamp, and generation rank (non-null values from `SampleData.Info`).
10. **Sender** tab shows process ID and IP address from `SampleData.SenderIdentity` (or `"Unknown"` if not available).
11. Inspector does not crash when `Payload` is null.
12. `IsLinked` state and `SourcePanelId` persist across app restart (restored from `ComponentState`).

---

## Phase 5: Data Authoring & Network Configuration

---

### TASK-F001 — SendSamplePlugin

**Design Reference:** [DESIGN.md §9 — Phase 5](./DESIGN.md#9-phase-5--data-authoring--network-configuration)

**Scope**

_In scope:_
- Add `SendSamplePlugin` class in `StandardPlugin`.
- In `Initialize`: register a context menu provider for `SampleData` via `IContextMenuRegistry`:
  `"Clone to Send"` → opens the send panel pre-filled with the cloned payload.
- `SendSampleViewModel`:
  - Accepts `TopicMetadata meta` and an optional existing payload to clone.
  - Instantiates payload: `Activator.CreateInstance(meta.TopicType)` if no clone.
  - Iterates `meta.AllFields` (excluding synthetic/wrapper fields), builds `AvaloniaDrawerContext` per field, calls `IAvaloniaTypeDrawerRegistry.Build(ctx)` per field.
  - "Send" button: acquires `IDdsBridge.GetWriter(meta)`, calls `writer.Write(payload)` inside a
    `try { } catch (Exception ex)` block. Any caught exception is routed to the existing inline
    validation error area with the message `"DDS Publish Failed: {ex.Message}"`. The shell process
    must not crash on DDS network faults from the Send panel.
  - Shows inline validation errors from `AvaloniaDrawerContext.OnValidationError` callbacks.
- `SendSampleView.axaml`: `ScrollViewer` containing dynamically built controls.
- Panel ID: `"SendSample_<TopicName>"`.

_Out of scope:_
- Array element add/remove UI (deferred).
- Union discriminator UI (deferred).
- Saving a draft payload (deferred).

**Constraints**
- Type conversion is performed at the control level via the registered drawer factories.  
  No `Convert.ChangeType` fallback is permitted on the "Send" button handler.
- `IAvaloniaTypeDrawerRegistry` must have standard drawers for: `int`, `uint`, `long`, `ulong`, `float`, `double`, `bool`, `string`, `char`, all `enum` types. These standard drawers are registered by `SendSamplePlugin.Initialize` (or by a shared drawer-registration helper called from `StandardPlugin`).
- `IStatefulViewModel` is NOT required for `SendSampleViewModel` in V1 (payload authoring state is not persisted).

**Dependencies:** TASK-E001 (SamplesViewer provides the context menu target), TASK-B001 (`IAvaloniaTypeDrawerRegistry` exists).

**Success Conditions**
1. Right-click on a sample row → "Clone to Send" menu item visible.
2. Click "Clone to Send" → `SendSample_<TopicName>` window opens; fields pre-filled with cloned values.
3. Edit a numeric field → `AvaloniaDrawerContext.OnChange` fires; value in the payload object is updated.
4. Click "Send" → `IDdsBridge.GetWriter(meta).Write(payload)` is called; the sent sample appears in the `SamplesViewer` grid (round-trip proof).
5. Enter an invalid value in a numeric field (e.g. `"abc"` in an `int` field) → inline error shown, "Send" button disabled or no-op.
6. Simulate a DDS write failure (e.g. mock `IDdsBridge.GetWriter` to throw) → inline error shows `"DDS Publish Failed: …"` message; application remains running.
7. Standard drawers: `int`, `double`, `string`, `bool`, `enum` — each renders the appropriate Avalonia control (`NumericUpDown`, `TextBox`, `ToggleSwitch`, `ComboBox`).
7. Open two `SendSample` panels for different topics → independent, both functional.

---

### TASK-F002 — WorkspaceManagerPlugin: Network Configurator Panel

**Design Reference:** [DESIGN.md §9 — Phase 5 — WorkspaceManagerPlugin (network portion)](./DESIGN.md#9-phase-5--data-authoring--network-configuration)

**Scope**

_In scope:_
- Add `"Tools/Network Configuration…"` menu item in `WorkspaceManagerPlugin.Initialize`.
- Implement `NetworkConfigViewModel` and `NetworkConfigView.axaml`:
  - Shows current `IDdsBridge.ParticipantConfigs` in an editable list (domain ID + partition name per row).
  - "Add Row" → adds a new `ParticipantConfig` with defaults.
  - "Remove" → removes the selected row.
  - "Apply" → computes diff vs. current `IDdsBridge.Participants`:
    - For each removed config, calls `IDdsBridge.RemoveParticipant(index)`.
    - For each added config, calls `IDdsBridge.AddParticipant(domainId, partitionName)`.
    - Publishes `ParticipantsChangedEvent(bridge.ParticipantConfigs)` via `IEventBroker`.
- Panel ID: `"NetworkConfig"` (singleton).

_Out of scope:_
- DDS QoS profiles (deferred).
- DDS XML configuration import (deferred).

**Constraints**
- "Apply" must be transactional with respect to the diff: remove stale participants first, then add new ones.
- CLI preloaded participants (`--DdsSettings:Participants:0:DomainId=0`) must appear in the view on open.
- If `IDdsBridge.AddParticipant` throws (e.g. invalid domain), the error must be shown inline, not swallowed.

**Dependencies:** TASK-C001 (`WorkspaceManagerPlugin` class already exists).

**Success Conditions**
1. Click "Tools → Network Configuration…" → `NetworkConfig` window opens.
2. On open, current participants are listed (at least one from CLI/defaults).
3. Add domain `1` + empty partition → click Apply → `IDdsBridge.AddParticipant(1, "")` called; `ParticipantsChangedEvent` published.
4. Topics from domain 1 eventually appear in the explorer (proves hot-wiring).
5. Remove a participant → `IDdsBridge.RemoveParticipant(index)` called; its topics stop arriving.
6. Apply with no changes → no `AddParticipant`/`RemoveParticipant` calls made.
7. Attempt to add a negative domain ID → inline error shown without crashing.

---

## Phase 6: Workspace Polish

---

### TASK-G001 — IStatefulViewModel Persistence Round-Trip

**Design Reference:** [DESIGN.md §10 — Phase 6](./DESIGN.md#10-phase-6--workspace-polish)

**Scope**

_In scope:_
- Implement `AvaloniaWorkspacePersistenceService` as a singleton background service that:
  - Subscribes to `WorkspaceSaveRequestedEvent` via `IEventBroker`.
  - Debounces saves (1–2 s after last request).
  - Serializes `IWindowManager.ActivePanels` to `workspace.json` using `IWorkspaceState.WorkspaceFilePath` (existing Engine path resolver that honours `--AppSettings:ConfigFolder` and `--AppSettings:WorkspaceFile`).
  - Subscribes to `WorkspaceSavingEvent` to collect `PluginSettings` from the Engine's `WorkspaceDocument`.
- Wire `AvaloniaWindowManager` to call `IStatefulViewModel.Initialize(panelState.ComponentState)` immediately after instantiating the ViewModel, before `Show()`. The ViewModel then owns the dict reference and mutates it directly.
- Wire `AvaloniaWindowManager` to write window geometry back into `panelState.ComponentState["__window"]` on `LocationChanged`/`SizeChanged` (debounced).
- Implement `IStatefulViewModel` on `SamplesViewerViewModel`: on `Initialize`, read `FilterText`, `SortFieldPath`, `SortDirection`, column selection from the dict; thereafter write each value back to the same dict whenever the property changes.
- Write window geometry into `panelState.ComponentState["__window"]` as a `Dictionary<string, double>` with keys `X`, `Y`, `Width`, `Height`.
- On app startup, load `workspace.json` and call `IWindowManager.SpawnPanel` for each stored `PanelState` (restoring the last session's open panels).
- Verify that `FilterPersistableState` (existing Engine sanitizer called by the workspace persistence pipeline) does not discard any values written by `IStatefulViewModel.Initialize`-injected ViewModels (only primitives, arrays, and nested dictionaries are kept).

_Out of scope:_
- `IStatefulViewModel` for `SendSamplePlugin` (V1 deferred).
- `IStatefulViewModel` for `NetworkConfigViewModel` (V1 deferred).

**Constraints**
- `AvaloniaWindowManager` must call `IStatefulViewModel.Initialize(panelState.ComponentState)` before `Show()`; the ViewModel owns the dict and mutates it directly.
- The `workspace.json` format must be identical in meaning to Blazor's format for overlapping panel kinds (e.g. `SamplesViewer`). Geometry keys inside `ComponentState["__window"]` are new and Blazor ignores unknown keys.

**Dependencies:** TASK-E001 (`SamplesViewerViewModel` exists).

**Success Conditions**
1. Open `SamplesViewer_Heartbeat_Topic`, set filter `"Payload.Sequence > 10"`, resize window to 900×500, move to position (200, 150). Close app. Reopen.
2. `SamplesViewer_Heartbeat_Topic` reopens at position 200,150 with size 900×500 and filter `"Payload.Sequence > 10"` pre-filled (≤ 2 dispatch cycles after open).
3. Open three different `SamplesViewer` windows with different filters and positions; close app; reopen — all three restore correctly and independently.
4. `workspace.json` on disk contains a `"ComponentState"` entry with `"__window"` (geometry) and `"FilterText"` keys; the file is written within 2 s of publishing `WorkspaceSaveRequestedEvent`.
5. `AvaloniaWorkspacePersistenceService` also writes on app exit (graceful shutdown via `IHostApplicationLifetime.ApplicationStopping`).
6. Blazor app reads the same `workspace.json` without crash (unknown keys are ignored).
7. `FilterPersistableState` does not strip any value written by the `IStatefulViewModel.Initialize`-injected ViewModels (verified by comparing the captured dict before and after the sanitizer pass).

---

## Phase 7: DDS Diagnostics Depth & Tooling

> Phase 7 tasks are post-V1. All plugins in this phase reference only Engine + Core and do not
> require changes to the shell executable.

---

### TASK-H001 — InstancesViewerPlugin

**Design Reference:** [DESIGN.md §15 — Phase 7](./DESIGN.md#phase-7--dds-diagnostics-depth--tooling)

**Scope**

_In scope:_
- Add `InstancesViewerPlugin` class in a new `DdsMonitor.Avalonia.DiagnosticsPlugin` assembly
  (or extend `StandardPlugin` if the assembly is not too large).
- In `Initialize`: register `"View/Instances…"` menu item that opens `PanelId = "InstancesViewer_<TopicName>"`.
- `InstancesViewerViewModel`:
  - Consumes `IInstanceStore` (existing Engine) for the specified topic.
  - Exposes two observable collections: `LiveInstances` and `HistoryInstances`.
  - Each instance row shows: instance key field values, lifecycle state (Alive / NotAliveDisposed /
    NotAliveNoWriters), and last update timestamp.
  - Implements `IStatefulViewModel`: persists selected tab (Live/History), filter, and sort direction.
  - Implements `IDisposable`: disposes `IInstanceStore` listeners and broker subscriptions.
- `InstancesViewerView.axaml`: tabbed `DataGrid` or `TreeDataGrid` for Live and History views.
- Registers a `"View Instances"` context menu item on `TopicMetadata` rows (via `IContextMenuRegistry`),
  allowing launch from `TopicExplorerPlugin`.

_Out of scope:_
- Writing/modifying instance keys (that is `SendSamplePlugin`).
- Instance aggregation statistics panel (deferred).

**Constraints**
- Must verify that `IInstanceStore` exists in the Engine before implementing; if not, the task
  begins with adding `IInstanceStore` to the Engine (similar scope to Phase 0 purification).
- Lifecycle state transitions must be rendered in the UI within 100 ms of the Engine update.

**Dependencies:** TASK-B003, TASK-C002.

**Success Conditions**
1. Click `"View/Instances…"` or context menu on a keyed topic → `InstancesViewer_<TopicName>` window opens.
2. Live tab shows currently alive instances grouped by instance key.
3. History tab shows disposed/no-writer instances.
4. Lifecycle state column updates in real time as instances transition states.
5. Filter text box filters the instance list without blocking the UI thread.
6. State (selected tab, filter, sort) survives app restart.

---

### TASK-H002 — ExportPlugin: Data Export Pipeline

**Design Reference:** [DESIGN.md §15 — Phase 7](./DESIGN.md#phase-7--dds-diagnostics-depth--tooling)

**Scope**

_In scope:_
- Add `ExportPlugin` class.
- In `Initialize`:
  - Register `"Tools/Export Samples…"` menu item.
  - Register a context menu item `"Export Filtered Samples…"` on `SamplesViewer` panels (via
    `IContextMenuRegistry` bound to a `PanelId` context type or an `ISampleView` context type).
- Implement `ExportViewModel`:
  - Reads available formats from `IExportFormatRegistry` (existing Engine) to populate a format
    picker.
  - On confirm: calls `IExportService.ExportFilteredSamplesAsync(view, format, filePath)`.
  - Shows progress (indeterminate `ProgressBar`) while export runs.
  - Shows inline success/error message on completion.
- Register a default Avalonia-side JSON format entry in `IExportFormatRegistry` if the Engine
  does not already provide one.

_Out of scope:_
- CSV and binary formats (deferred, register via `IExportFormatRegistry` as separate tasks).
- Scheduled/automated export.

**Constraints**
- Export must run on a background thread; must not block the UI.
- `IExportService` and `IExportFormatRegistry` must exist in the Engine; verify during implementation.

**Dependencies:** TASK-E001 (`ISampleView` provided by `SamplesViewerViewModel`).

**Success Conditions**
1. Right-click on `SamplesViewer` → "Export Filtered Samples…" dialog opens.
2. Select JSON format, choose file path, click Export → file written to disk with all currently
   filtered samples; progress indicator visible during write.
3. Exported JSON is valid and round-trippable (can be loaded back via replay mode).
4. Export does not freeze the UI during a 100k-sample export.
5. Error during export (e.g. path not writable) → inline error message, no crash.

---

### TASK-H003 — WorkspaceManagerPlugin: Manual Layout Import/Export

**Design Reference:** [DESIGN.md §15 — Phase 7](./DESIGN.md#phase-7--dds-diagnostics-depth--tooling)

**Scope**

_In scope:_
- Extend `WorkspaceManagerPlugin` with three new menu items registered in `Initialize`:
  - `"File/Export Layout…"` → opens a save-file dialog filtered to `*.workspace.json`; copies
    the current in-memory workspace state to the chosen path via `IWorkspaceState`.
  - `"File/Import Layout…"` → opens an open-file dialog; loads the selected `workspace.json`,
    closes all currently open panels, reopens panels from the loaded state. Must show a
    confirmation prompt if there are unsaved changes.
  - `"File/Reset Layout"` → closes all open panels; publishes `WorkspaceSaveRequestedEvent`
    to persist the empty layout.
- All three commands are registered on `IMenuRegistry` under the `"File"` top-level menu.

_Out of scope:_
- Layout versioning or migration (deferred).
- Cloud/shared layout storage (deferred).

**Constraints**
- Import must not crash if the imported file contains panel kinds not currently registered
  (unknown panel types are skipped with a warning logged).
- Export must flush the current in-memory state before writing, so the exported file always
  reflects the live layout.

**Dependencies:** TASK-G001 (workspace persistence service exists).

**Success Conditions**
1. Configure three `SamplesViewer` windows at specific positions and filters.
2. `"File/Export Layout…"` → choose a path → file written; inspect JSON: three panel entries
   present with correct geometry and filter strings.
3. Close two of the three windows.
4. `"File/Import Layout…"` → select the exported file → all three windows reopen at the
   exported positions and filters.
5. `"File/Reset Layout"` → all panels close; `workspace.json` reflects empty layout.
6. Import of a file with an unrecognised `ComponentTypeName` → skips that entry, logs a warning,
   opens the remaining valid panels without crash.

---

### TASK-H004 — FilterBuilderPlugin: Visual Filter Builder

**Design Reference:** [DESIGN.md §15 — Phase 7](./DESIGN.md#phase-7--dds-diagnostics-depth--tooling)

**Scope**

_In scope:_
- Add `FilterBuilderPlugin` class in its own assembly or in `StandardPlugin`.
- In `Initialize`: register a `"Open Filter Builder"` context menu item on `SamplesViewer`
  panels (via `IContextMenuRegistry` keyed on a `SamplesViewerContext` record containing the
  target `PanelId`).
- `FilterBuilderViewModel`:
  - Shows a visual node tree for constructing filter conditions: field picker (populated from
    `TopicMetadata.AllFields`), operator picker, value input.
  - "Apply" button: compiles the visual tree into a Dynamic LINQ filter string and publishes
    `ApplyFilterRequestEvent(TargetPanelId: string, FilterText: string)` via `IEventBroker`
    (new event record added to `EventBrokerEvents.cs` in the Engine or to `Avalonia.Core`).
  - `SamplesViewerViewModel` subscribes to `ApplyFilterRequestEvent`, checks if `TargetPanelId`
    matches its own `PanelId`, and applies the filter string via `IFilterCompiler`.
- PanelId: `"FilterBuilder_<SourcePanelId>"` (one builder per viewer).
- `FilterBuilderPlugin` must not reference `SamplesViewerPlugin` types.

_Out of scope:_
- Saving/loading named filter presets (deferred).
- Complex compound expressions beyond two clauses (deferred for V2).

**Constraints**
- The compiled filter string must pass through `IFilterCompiler` — `FilterBuilderPlugin` does
  not call `SampleView.SetFilter` directly.
- If the built filter string fails `IFilterCompiler.Compile`, the "Apply" button must show the
  `FilterResult.ErrorMessage` inline.

**Dependencies:** TASK-E001 (`SamplesViewerViewModel` must subscribe to `ApplyFilterRequestEvent`
— requires a small addition to E001's ViewModel in this task).

**Success Conditions**
1. Right-click on a `SamplesViewer` → "Open Filter Builder" → `FilterBuilder_<PanelId>` window opens.
2. Select field `"Payload.Sequence"`, operator `">"`, value `"100"` → click Apply →
   `SamplesViewerPlugin`'s grid filters to `Payload.Sequence > 100`.
3. The filter string box in the `SamplesViewer` window updates to show the compiled expression.
4. `FilterBuilderPlugin` and `SamplesViewerPlugin` never directly reference each other's types.
5. Invalid combination (e.g. string field with `">"` operator) → error shown inline in builder;
   no publish to `IEventBroker`.

---

### TASK-H005 — Custom Column Picker

**Design Reference:** [DESIGN.md §15 — Phase 7](./DESIGN.md#phase-7--dds-diagnostics-depth--tooling)

**Scope**

_In scope:_
- Extend `SamplesViewerPlugin` with a "Columns…" toolbar button or context menu item.
- `ColumnPickerViewModel`:
  - Queries `TopicMetadata.AllFields` to present available schema fields as a checklist.
  - The current `SelectedColumnPaths` (already in `ComponentState` from TASK-E001) determines
    the initial checked state.
  - "Apply" dynamically rebuilds the Avalonia `TreeDataGrid` column definitions.
- Dynamic columns bind directly to the pre-compiled `FieldMetadata.Getter` delegate —
  zero reflection overhead during high-frequency renders.
- On apply: `SamplesViewerViewModel` updates `ComponentState["SelectedColumnPaths"]` directly
  and publishes `WorkspaceSaveRequestedEvent` (persists new column layout to disk).

_Out of scope:_
- Column reordering via drag-and-drop (deferred).
- Column width presets (deferred).

**Constraints**
- Column rebuild must not recreate the `TreeDataGrid` from scratch — update the
  `TreeDataGrid.Columns` collection in place to preserve scroll position.
- `FieldMetadata.Getter` delegates must be used directly; no `Reflection.GetProperty` calls
  in the grid cell renderer.

**Dependencies:** TASK-E001 (`SelectedColumnPaths` already persisted in `ComponentState`).

**Success Conditions**
1. Click "Columns…" → column picker dialog shows all `TopicMetadata.AllFields` as a checklist
   with the current selection pre-checked.
2. Uncheck `"Payload.Sequence"`, click Apply → that column disappears from the grid.
3. Re-check it, click Apply → column reappears; scroll position is preserved.
4. New column layout is written to `ComponentState["SelectedColumnPaths"]` and persists across
   app restart.
5. Under 5 kHz load: adding/removing a column does not cause a noticeable frame drop.
