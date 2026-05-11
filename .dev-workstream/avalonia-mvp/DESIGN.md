# DdsMonitor.Avalonia — V1 MVP Design

> Port of `DdsMonitor.Blazor` to a strictly local, multiplatform Avalonia desktop application,
> rebuilt from the ground up as a VS Code–style plugin shell on top of the existing
> `DdsMonitor.Engine`.

---

## 1. Goals & Non-Goals

### Goals

- **Multiplatform desktop**, not browser-hosted. Avalonia for the UI, .NET 8.
- **Strict plugin architecture**: the shell knows nothing about DDS. Every feature is delivered
  by a plugin loaded at startup from a `./plugins` folder. A single plugin assembly may contain
  multiple `IMonitorPlugin` implementations (multi-plugin-per-assembly).
- **Reuse `DdsMonitor.Engine` as-is** wherever possible. The Engine already provides:
  expression-tree-compiled `TopicMetadata`/`FieldMetadata`, a UI-agnostic broker/registry layer,
  `IDdsBridge` with dynamic participant add/remove, collectible `AssemblyLoadContext` for schema
  DLLs via `TopicDiscoveryService`, dictionary-backed `PanelState.ComponentState` persistence,
  a background-threaded filtering pipeline (`SampleView`/`ISampleView`), `IFilterCompiler`,
  `IMenuRegistry`, `IContextMenuRegistry`, `PluginLoader` (with proper shared-assembly delegation),
  and `HeadlessRunnerService`.
- **Headless dual-boot**: the same executable can run as either an interactive desktop app or a
  non-interactive CLI tool (Record/Replay), driven by `--DdsSettings:HeadlessMode`.
- **Compatibility with all existing CLI arguments** (`--AppSettings:*`, `--DdsSettings:*`).
  No script changes for existing deployments.
- **Prove the architecture, not the feature set.** V1 ships a thin, deliberate slice that
  validates every high-risk architectural vector before porting the remaining Blazor features.

### Non-Goals for V1

- Full feature parity with `DdsMonitor.Blazor` (hex viewer, replay UI, filter builder, sparklines,
  statistics overlay, devel-mode self-send UI, custom export formats, etc.) — deferred.
- A docking framework. V1 uses **floating Avalonia `Window` instances only**. `Dock.Avalonia`
  is a V2 drop-in once the `IWindowManager` Avalonia implementation is stable.
- Plugin sandboxing/signing/marketplace. Plugins are trusted code in V1.
- Hot-reload of plugin assemblies. Schema DLLs hot-load; plugins load once at startup.

---

## 2. High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                   DdsMonitor.Avalonia  (shell exe)                  │
│  Generic Host • DI bootstrap • dual-boot decision                   │
│  Avalonia WindowManager • ShellWindow (Menu + Toolbar)              │
│  IUserSettings store (settings.json in AppData)                     │
│   — no DDS code, no panel-specific logic —                          │
└──────────────────┬──────────────────────────────────┬───────────────┘
                   │ resolves via DI / IMonitorContext │ loads at startup
                   ▼                                  ▼
┌───────────────────────────────────┐  ┌──────────────────────────────────┐
│   DdsMonitor.Avalonia.Core        │  │     ./plugins/*.dll              │
│   (NEW — Avalonia-specific        │  │  DdsMonitor.Avalonia.StandardPlugin│
│    extensions to Engine API)      │  │  Each DLL may contain N plugins  │
│                                   │◄─┤  implementing IMonitorPlugin     │
│  IToolbarRegistry                 │  │  → ConfigureServices + Initialize│
│  IUserSettings                    │  └──────────────┬───────────────────┘
│  IAvaloniaTypeDrawerRegistry      │                 │ ctor-injection via DI
│  IAvaloniaViewRegistry            │                 ▼
│  AvaloniaDrawerContext            │  ┌──────────────────────────────────┐
│  IStatefulViewModel               │  │     DdsMonitor.Engine           │
└──────────────────┬────────────────┘  │                                  │
                   │ both reference    │  IDdsBridge, ITopicRegistry      │
                   └──────────────────►│  ISampleStore, SampleView        │
                                       │  IFilterCompiler, TopicMetadata  │
                                       │  IAssemblySourceService          │
                                       │  IEventBroker, IMenuRegistry     │
                                       │  IContextMenuRegistry            │
                                       │  IWindowManager (Engine base)    │
                                       │  PanelState, EventBrokerEvents   │
                                       │  IValueFormatterRegistry         │
                                       │  PluginLoader, IMonitorPlugin    │
                                       │  HeadlessRunnerService           │
                                       └──────────────────────────────────┘
```

### Layering Rules

| Layer | References | Rule |
|-------|-----------|------|
| `DdsMonitor.Engine` | none (lightly purified) | Must compile without `Microsoft.AspNetCore.*` |
| `DdsMonitor.Avalonia.Core` | Engine | Avalonia-specific extensions; no shell types |
| `DdsMonitor.Avalonia` (shell) | Engine + Core | Implements registries, Window manager, bootstrap |
| Plugin assemblies | Engine + Core | Must NOT reference the shell executable |

---

## 3. Solution Layout

```
tools/DdsMonitor/
├── DdsMonitor.Engine/                        (existing — Phase 0 light purification only)
├── DdsMonitor.Blazor/                        (existing — untouched, ships in parallel)
├── DdsMonitor.Avalonia.Core/                 NEW — Avalonia-specific shared contracts
├── DdsMonitor.Avalonia/                      NEW — shell executable (.NET 8, Avalonia)
└── DdsMonitor.Avalonia.StandardPlugin/       NEW — all V1 plugins in one assembly
```

All five `IMonitorPlugin` implementations in V1 live in `DdsMonitor.Avalonia.StandardPlugin`.
The `PluginLoader` (already in the Engine) iterates `assembly.ExportedTypes` and discovers all
of them from one DLL, proving multi-plugin-per-assembly loading from day one.

---

## 4. Phase 0 — Engine Purification

**Goal:** Remove the last `Microsoft.AspNetCore.Components` leaks so the Engine compiles and
tests pass with no Blazor reference. This is a mechanical, non-breaking change.

### What must change

| File | Change |
|------|--------|
| `Engine/Ui/ITypeDrawerRegistry.cs` | Replace `RenderFragment<DrawerContext>` with `Func<DrawerContext, object>` (UI-agnostic factory) |
| `Engine/Ui/DrawerContext.cs` | Remove `IHandleEvent? Receiver` property (Blazor-specific) |
| `Engine/Plugins/ISampleViewRegistry.cs` | Replace `RenderFragment<SampleData>` with `Func<SampleData, object>` |

The Blazor shell in `DdsMonitor.Blazor` provides thin adapter registrations that cast the
`object` factory return values back to `RenderFragment`. The Avalonia shell provides adapters
producing Avalonia `Control` instances. Neither the Engine nor plugin authors are affected.

### Why this must come first

Every other phase depends on Engine types. If the Engine references Blazor, the Avalonia
projects cannot reference the Engine without pulling in `Microsoft.AspNetCore.*`.

---

## 5. Phase 1 — Empty Shell

**Goal:** A running Avalonia desktop process that loads the Generic Host, detects headless mode,
and shows an empty `ShellWindow` with a working (but empty) menu and toolbar. No plugins yet.

### 5.1 `DdsMonitor.Avalonia.Core` — New Contracts

This is a `.NET 8` class library. Both the shell and all plugins reference it.

#### Toolbar Registry (new — Engine has `IMenuRegistry` but no toolbar equivalent)

```csharp
// Added to Avalonia.Core; not in Engine
public interface IToolbarRegistry
{
    void Register(string id, Action onClick, string? iconKey, string tooltip);
    IEnumerable<ToolbarEntry> Entries { get; }
    event Action? Changed;
}
```

#### User Settings Store (new — separate from per-panel `workspace.json`)

Global preferences (checkboxes, theme, etc.) that survive across workspaces.

```csharp
public interface IUserSettings
{
    T Get<T>(string pluginId, string key, T defaultValue = default!);
    void Set<T>(string pluginId, string key, T value);
    Task SaveAsync();
}
```

Saved in `%APPDATA%\DdsMonitor\settings.json` (OS-appropriate path via
`Environment.SpecialFolder.ApplicationData`).

#### Stateful Panel View-Models

```csharp
/// Marker interface. Implement this to receive the live ComponentState dictionary
/// injected on instantiation. The ViewModel reads initial state from the dict and
/// writes back to it directly when its properties change. There are no lifecycle
/// callbacks — mutations are immediate and the dict is the shared ground truth.
public interface IStatefulViewModel
{
    /// Called by AvaloniaWindowManager immediately after instantiation, before Show().
    /// Implementation should both read initial state AND store the dict reference
    /// for future direct mutation.
    void Initialize(IDictionary<string, object> componentState);
}
```

The `WindowManager` injects the live `PanelState.ComponentState` dict into the ViewModel
via `Initialize()`. The ViewModel mutates it directly whenever relevant state changes
(filter text, column widths, etc.) and publishes `WorkspaceSaveRequestedEvent` to trigger
a debounced flush to disk. No separate `CaptureState` callback is needed — the dict is
always current. Only JSON-safe primitives, arrays, and nested dictionaries survive the
`FilterPersistableState` pass (existing Engine sanitizer).

#### Subscription Lifecycle — `IDisposable` Requirement

ViewModels that hold `IEventBroker` subscriptions (including `SubscribeOnUiThread`) must
implement `IDisposable` and `Dispose()` each returned subscription token. Because `IEventBroker`
is a singleton that outlives any panel, undisposed subscriptions root the ViewModel in the
broker indefinitely — preventing GC and eventually exhausting resources.

`AvaloniaWindowManager` checks `IDisposable` on every ViewModel it instantiates and calls
`Dispose()` when the panel closes. This is not optional.

#### Avalonia View Registry (new — replaces Blazor `PluginPanelRegistry`)

```csharp
public interface IAvaloniaViewRegistry
{
    void Register<TViewModel>(Func<TViewModel, Control> viewFactory);
    Control BuildView(object viewModel);   // ViewLocator pattern; throws on unknown type
}
```

Plugins register in `ConfigureServices` or `Initialize`; the shell's View Locator delegates to
this registry for all ViewModel-to-Control resolution.

#### Avalonia Type Drawer Registry (purified port of `ITypeDrawerRegistry`)

```csharp
public interface IAvaloniaTypeDrawerRegistry
{
    void Register(Type type, Func<AvaloniaDrawerContext, Control> factory);
    Control Build(AvaloniaDrawerContext ctx);   // falls back to generic reflection walker
}

// AvaloniaDrawerContext is DrawerContext WITHOUT IHandleEvent (already removed in Phase 0)
// It just adds Avalonia-specific convenience (e.g. ObservableAsPropertyHelper binding helpers)
```

`IAvaloniaTypeDrawerRegistry.Build` explicitly casts the factory return value to `Control`.
If a plugin's registered factory returns anything other than an Avalonia `Control`,
`Build` throws `InvalidCastException` immediately, surfacing the misbehaving plugin at
registration time rather than propagating an invalid object to the view locator.

```csharp
```

### 5.2 `DdsMonitor.Avalonia` — Shell Executable

#### Generic Host Bootstrap

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDdsMonitorServices(builder.Configuration);   // existing Engine extension
builder.Services.AddSingleton<IToolbarRegistry, ToolbarRegistry>();
builder.Services.AddSingleton<IUserSettings, UserSettingsStore>();
builder.Services.AddSingleton<IAvaloniaViewRegistry, AvaloniaViewRegistry>();
builder.Services.AddSingleton<IAvaloniaTypeDrawerRegistry, AvaloniaTypeDrawerRegistry>();
// Engine's Avalonia WindowManager replaces the Engine's base IWindowManager
builder.Services.AddSingleton<IWindowManager, AvaloniaWindowManager>();
```

The existing `PluginLoader` (already in the Engine, injected by `AddDdsMonitorServices`) scans
`./plugins/*.dll` and calls `ConfigureServices` on each `IMonitorPlugin` during bootstrap.

#### Dual-Boot Decision

```csharp
var ddsSettings = host.Services.GetRequiredService<DdsSettings>();
if (ddsSettings.HeadlessMode != HeadlessMode.None)
{
    // CLI/headless: existing HeadlessRunnerService handles Record/Replay
    await host.RunAsync();
}
else
{
    _ = host.StartAsync();
    BuildAvaloniaApp(host.Services).StartWithClassicDesktopLifetime(args);
}
```

When `HeadlessMode != None`, Avalonia is never initialised. The existing `HeadlessRunnerService`
(already a `BackgroundService` in the Engine) handles all record/replay logic. Plugins' own
`IHostedService`s (like `DummyDataGeneratorPlugin`) run regardless of UI mode.

#### `Initialize` Phase — Calling Plugins

After `BuildAvaloniaApp` returns, before `ShellWindow` is shown, the shell calls:

```csharp
var pluginLoader = host.Services.GetRequiredService<PluginLoader>();
var context = host.Services.GetRequiredService<IMonitorContext>();
pluginLoader.InitializePlugins(context);   // calls IMonitorPlugin.Initialize(IMonitorContext)
```

#### `ShellWindow` Structure

An Avalonia `Window` (not `MainWindow.axaml` with panels inside — those come from plugins):

```
┌──────────────────────────────────────────────────────────────┐
│  [Menu Bar]  ← bound to IMenuRegistry                       │
│  [Toolbar]   ← bound to IToolbarRegistry                    │
│  [▶ Play] [⏸ Pause] [⏹ Reset]  ← global transport controls │
│  [Status Bar placeholder]                                   │
└──────────────────────────────────────────────────────────────┘
```

The **global transport controls** (Play, Pause, Reset) are shell-level, not plugin-provided.
They are always visible and call the global transport API on `IDdsBridge` — equivalent to
the controls in `DdsMonitor.Blazor`'s `MainLayout.razor`. Pausing halts sample ingestion
globally so users can inspect a frozen data snapshot without shutting down DDS participants.
Play and Reset restore/restart ingestion. The exact bridge API (method names) must be
verified against the Blazor implementation during TASK-B002.

No panel host region. All panels are independent floating `Window` objects managed by
`AvaloniaWindowManager`.

---

## 6. Phase 2 — Schema & Topic Discovery

**Goal:** Prove dynamic schema DLL hot-load and live topic list rendering.

### `WorkspaceManagerPlugin` (schema portion)

- Registers a menu item `"Tools/Schema Sources…"` via `IMenuRegistry`.
- On click, opens a `PanelId = "SchemaSources"` floating window (singleton).
- The Avalonia schema panel replicates `TopicSourcesPanel.razor`:
  - Uses Avalonia native file picker → `IAssemblySourceService.Add(dllPath)`.
  - `IAssemblySourceService.Changed` triggers a UI refresh of the loaded-DLL list.
  - `TopicDiscoveryService` (already in Engine) handles the collectible `AssemblyLoadContext`
    hot-load, expression-tree compilation, and `ITopicRegistry.Register()`.

### `TopicExplorerPlugin`

- Calls `IWindowManager.SpawnPanel("TopicExplorer", null)` in `Initialize` to open at startup.
- `TopicExplorerViewModel` subscribes to `ITopicRegistry.Changed` to refresh a `ListBox`.
- "Show Hidden/Internal Topics" checkbox state persisted via `IUserSettings`.
- Adds `"View/Topic Explorer"` menu item and a toolbar toggle button.
- Right-click on a topic item builds a context menu by calling
  `IContextMenuRegistry.GetItems<TopicMetadata>(topicMeta)` (already in Engine).
- Double-click publishes `SpawnPanelEvent("SamplesViewer", {"TopicName": topicName})` via
  `IEventBroker` (existing event record from `EventBrokerEvents.cs`).

---

## 7. Phase 3 — Backend Prover

**Goal:** Prove headless execution, CLI argument binding per-plugin, background services, and
cross-plugin context menu injection.

### `DummyDataGeneratorPlugin`

- Registers an `IHostedService` (`DummyGeneratorService`).
- Reads `--GeneratorPlugin:Enabled=true` and `--GeneratorPlugin:PublishRateMs=100` via
  `IConfiguration` injection (the Generic Host makes all CLI args available).
- If `Enabled=true`, acquires `IDdsBridge.GetWriter(syntheticHeartbeatMeta)` and publishes
  at the configured rate, reaching up to **5 kHz** for stress-testing Phase 4.
- Runs unchanged in headless mode — no UI dependency.
- Registers a context menu provider for `TopicMetadata` via `IContextMenuRegistry.RegisterProvider`:
  ```csharp
  registry.RegisterProvider<TopicMetadata>(meta =>
      [new ContextMenuItem("Toggle Dummy Generator", () => ToggleGenerator(meta.TopicName))]);
  ```
  `TopicExplorerPlugin` picks this up automatically, proving cross-plugin injection.
- Adds `"Tools/Dummy Generator"` menu item.

---

## 8. Phase 4 — Firehose UI

**Goal:** Prove that Avalonia can consume a 5 kHz DDS firehose without UI thread starvation,
using the Engine's existing background-threaded `SampleView` and zero-allocation slice API.

### `SamplesViewerPlugin`

- Subscribes to `SpawnPanelEvent` where `PanelTypeName == "SamplesViewer"`.
- Calls `IWindowManager.SpawnPanel("SamplesViewer", state)` (or focuses existing if open).
- `PanelId` convention: `"SamplesViewer_<TopicName>"` — unique per-topic, matches
  existing `PanelState.PanelId` string convention in the Engine.
- The ViewModel implements `IStatefulViewModel`: receives the live `ComponentState` dict on
  initialization and directly writes to it whenever filter text, sort direction, or column
  selection changes. Publishes `WorkspaceSaveRequestedEvent` after each relevant state change.

#### Filtering Pipeline (thread-safe, no UI-thread LINQ)

1. Construct `SampleView(ISampleStore store)` — starts its own background worker.
2. User filter string → `IFilterCompiler.Compile(expr, topicMeta)` → `FilterResult.Predicate`.
3. Push predicate: `_view.SetFilter(FilterResult.Predicate)` — evaluation is off UI thread.
4. `ISampleView.OnViewRebuilt` fires on the background thread → ViewModel marshals:
   ```csharp
   Dispatcher.UIThread.InvokeAsync(() => _gridSource.UpdateCount(_view.CurrentFilteredCount));
   ```
5. Avalonia `TreeDataGrid` pulls visible rows via `_view.GetVirtualView(start, count)` →
   `ReadOnlyMemory<SampleData>` zero-allocation slice. No per-sample event. No polling timer.

#### `SamplesViewerPlugin` — State Parameterisation

`SamplesViewerViewModel` must write `"TopicName"` into `ComponentState` during `Initialize`:

```csharp
void Initialize(IDictionary<string, object> componentState)
{
    // read
    _filterText = componentState.GetValueOrDefault("FilterText", "");
    // write back the discriminator key so WindowManager can restore parameterised panels
    componentState["TopicName"] = _meta.TopicName;
    _state = componentState;
}
```

`AvaloniaWindowManager`'s startup restoration loop extracts `"TopicName"` from
`ComponentState`, resolves the corresponding `TopicMetadata` from `ITopicRegistry`, and
passes it to the ViewModel factory before calling `Initialize`. Without this, parameterised
panels cannot be restored from `workspace.json` after an app restart.

### `DetailInspectorPlugin` — Independent Linked Inspector

The detail inspector is **not** embedded inside the `SamplesViewer` window. It is a fully
independent, addressable floating window, preserving the decoupled linking paradigm from the
Blazor codebase.

- PanelId: `"DetailInspector_<uuid>"` — each instance is independent; multiple inspectors
  may be open simultaneously for side-by-side payload comparison.
- State persisted via `IStatefulViewModel.Initialize(dict)`:
  - `"IsLinked"` (bool): when `true`, subscribes to `SampleSelectedEvent` filtered by
    `SourcePanelId`. When `false`, displays the last pinned sample (frozen).
  - `"SourcePanelId"` (string): `PanelId` of the linked `SamplesViewer` (e.g.
    `"SamplesViewer_Heartbeat_Topic"`).
- `SamplesViewerPlugin` publishes `SampleSelectedEvent(panelId, sample)` on row click.
- `DetailInspectorPlugin` registers a `"Open Inspector"` context menu item on `SampleData`
  rows via `IContextMenuRegistry`, spawning a new inspector linked to the source panel.
- A **Link / Unlink** toggle button in the inspector's toolbar switches `IsLinked` and
  immediately updates `ComponentState["IsLinked"]` (triggering a debounced save).

#### Inspector Content

1. **Fields** (default view): checks `ISampleViewRegistry.GetViewer(payload type)` first —
   if a custom viewer is registered, renders it. Otherwise iterates
   `TopicMetadata.AllFields` (pre-compiled `FieldMetadata.Getter` delegates), building a
   hierarchical `FieldInspectorItemViewModel` tree. Leaf values rendered via
   `IValueFormatterRegistry`.
2. **Sample Info**: DDS metadata extracted from `SampleData.Info` — write timestamp,
   reception timestamp, generation rank, and other QoS-observable fields.
3. **Sender**: `SampleData.SenderIdentity` — process ID and IP address of the writing
   participant, as extracted by the Engine's ingestion pipeline.

---

## 9. Phase 5 — Data Authoring & Network Configuration

**Goal:** Prove two-way data binding through compiled expression-tree setters, and prove dynamic
DDS participant management without host restart.

### `SendSamplePlugin`

- Registers a context menu provider for `SampleData` rows:
  ```csharp
  registry.RegisterProvider<SampleData>(sample =>
      [new ContextMenuItem("Clone to Send", () => OpenSendPanel(sample))]);
  ```
  This is cross-plugin injection into the `SamplesViewerPlugin` — `SendSamplePlugin` references
  only Engine + Core.
- Spawns `PanelId = "SendSample_<TopicName>"`.
- Instantiates an empty payload: `Activator.CreateInstance(meta.TopicType)`.
- For each `FieldMetadata` in `meta.AllFields`, constructs an `AvaloniaDrawerContext` and
  calls `IAvaloniaTypeDrawerRegistry.Build(ctx)` to get a strictly-typed Avalonia `Control`
  (`NumericUpDown` for numerics, `TextBox` for strings, `ComboBox` for enums, etc.).
- On "Send": `IDdsBridge.GetWriter(meta)` → `writer.Write(payload)`, wrapped in a
  `try/catch(Exception ex)`. Any caught DDS exception routes to the existing inline
  validation error UI (same component as field-level `OnValidationError` feedback) with
  the prefix `"DDS Publish Failed: {ex.Message}"`. The shell process must never crash on
  a DDS network fault from the Send panel.
- Type conversion handled at control level, not at submit time — no `Convert.ChangeType`.

### `WorkspaceManagerPlugin` (network portion)

- Adds `"Tools/Network Configuration…"` menu item.
- Opens `PanelId = "NetworkConfig"` (singleton).
- Avalonia port of `ParticipantEditorDialog`:
  - Reads `IDdsBridge.ParticipantConfigs` to populate a list.
  - "Add": calls `IDdsBridge.AddParticipant(domainId, partition)`.
  - "Remove": calls `IDdsBridge.RemoveParticipant(index)`.
  - Publishes `ParticipantsChangedEvent(bridge.ParticipantConfigs)` via `IEventBroker`
    (existing event record).
- CLI preload: `--DdsSettings:Participants:0:DomainId=0 --DdsSettings:Participants:0:PartitionName=…`
  is handled by `AddDdsMonitorServices` automatically (same as Blazor).

---

## 10. Phase 6 — Workspace Polish

**Goal:** Prove full per-panel state round-trip, including column layout, filter strings, and
window geometry. Confirm `workspace.json` format compatibility with the Blazor version where
panel kinds overlap.

### Panel State Persistence (dict-injection pattern)

- `AvaloniaWindowManager` calls `IStatefulViewModel.Initialize(panelState.ComponentState)`
  immediately after instantiating the ViewModel, before the window is shown. The ViewModel
  reads its initial state from the dict AND stores the dict reference for future direct writes.
- The ViewModel writes state changes (filter text updates, column resizes, etc.) back into
  the shared `ComponentState` dict directly — no callback, no polling.
- The ViewModel publishes `WorkspaceSaveRequestedEvent` when a change warrants a flush.
- Window geometry (`X`, `Y`, `Width`, `Height`, `IsMinimized`) is written into
  `panelState.ComponentState["__window"]` as a nested object by the `AvaloniaWindowManager`
  on close and on each `LocationChanged`/`SizeChanged` event (debounced), keeping the
  top-level `PanelState` schema unchanged and compatible with Blazor.
- `AvaloniaWorkspacePersistenceService` (registered as `IHostedService` singleton in the shell)
  subscribes to `WorkspaceSaveRequestedEvent`, debounces for 1–2 s, then serializes all active
  `PanelState` entries to `workspace.json` using `IWorkspaceState.WorkspaceFilePath`.
  It also subscribes to `WorkspaceSavingEvent` to flush `PluginSettings` from the Engine's
  `WorkspaceDocument` before writing. On graceful shutdown (`IHostApplicationLifetime.ApplicationStopping`)
  it performs a final synchronous save.

### Acceptance Criteria (Phase 6 Done)

- Open three `SamplesViewer` panels on different topics, resize, move, filter, sort, close.
- Reopen the app — all three panels reappear at exact positions, with columns and filter intact.

---

## 11. Architectural Patterns & Constraints

### PanelId Convention

The Engine's `PanelState.PanelId` is a plain `string`. The Avalonia shell follows the
convention already present in `SpawnPanelEvent`: `"{PanelKind}_{Discriminator}"`.

Examples:
- `"TopicExplorer"` — singleton panel
- `"SchemaSources"` — singleton panel
- `"SamplesViewer_RobotTelemetry_Topic"` — per-topic panel
- `"SendSample_RobotTelemetry_Topic"` — per-topic authoring panel

Panels with the same `PanelId` are focused (not duplicated) if already open.

### IEventBroker Threading

The Engine's `IEventBroker` delivers on the publisher's thread (may be background). The Avalonia
shell wraps the broker subscription to marshal UI-bound handlers to the Avalonia UI thread:

```csharp
// Avalonia.Core provides an extension method
eventBroker.SubscribeOnUiThread<TEvent>(handler, Dispatcher.UIThread);
```

This pattern must be applied consistently by all plugin ViewModels.

The `IDisposable` returned by `SubscribeOnUiThread` must be stored and disposed in the
ViewModel's `Dispose()` method. `AvaloniaWindowManager` calls `Dispose()` on any ViewModel
that implements `IDisposable` when its panel closes. Failing to dispose leaves the ViewModel
rooted in the singleton broker indefinitely. Plugin guidelines must document this rule
explicitly.

### High-Frequency Data — Never Through IEventBroker

`IEventBroker` is for app-state and user-intent events (e.g. `SpawnPanelEvent`,
`ParticipantsChangedEvent`, `SampleSelectedEvent`). DDS sample data flows exclusively through:
- `ISampleStore` → `SampleView.GetVirtualView()` (read path)
- `IDdsBridge.GetWriter().Write()` (write path)

No per-sample event is ever published to the broker.

### ISampleView Lifecycle

Each `SamplesViewerPlugin` window instance owns exactly one `SampleView` object. The ViewModel
creates it: `new SampleView(ISampleStore store)`. The ViewModel disposes it on window close.
`SampleView` starts its background worker on construction; `SetFilter` and `SetSortSpec` are
thread-safe and non-blocking.

### Engine Extension Point Adapters

For the two registries purified in Phase 0, the Blazor shell registers adapters in its
`Program.cs` that cast `object` factories back to `RenderFragment`:
```csharp
services.AddSingleton<ITypeDrawerRegistry>(sp => new BlazorDrawerAdapter(sp.GetRequiredService<IAvaloniaTypeDrawerRegistry>()));
```
This keeps the Blazor app working throughout the migration.

---

## 12. Critical Architectural Vectors Proved by V1

| Concern | Mechanism | Phase |
|---------|-----------|-------|
| Headless dual-boot | Generic Host branches on `HeadlessMode` before Avalonia init | 1 + 3 |
| Schema DLL hot-load | Collectible `AssemblyLoadContext` via existing `TopicDiscoveryService` | 2 |
| Dynamic DDS participants | `IDdsBridge.AddParticipant` / `RemoveParticipant` | 5 |
| Firehose UI (5 kHz) | `SampleView` bg-worker + `OnViewRebuilt` + `GetVirtualView` zero-alloc | 4 |
| Cross-plugin UI injection | `IContextMenuRegistry.RegisterProvider<T>` / `GetItems<T>` | 3→2, 5→4 |
| Addressed panel persistence | `PanelState.PanelId` string + `ComponentState` dictionary | 4 + 6 |
| Expression-tree payload read | `FieldMetadata.Getter` → `IValueFormatterRegistry` | 4 |
| Expression-tree payload write | `FieldMetadata.Setter` via `AvaloniaDrawerContext` | 5 |
| Per-instance state | `IStatefulViewModel.Initialize` + direct dict mutation + `WorkspaceSaveRequestedEvent` | 6 |
| Parameterised panel restore | `ComponentState["TopicName"]` key + `ITopicRegistry` lookup on startup | 6 |
| Subscription memory safety | `IDisposable` on ViewModels; `AvaloniaWindowManager` calls `Dispose()` on close | 1 |
| Global transport controls | Shell-level Play/Pause/Reset wired to `IDdsBridge` transport API | 1 |
| Linked/detachable inspector | `DetailInspectorPlugin` with `IsLinked` + `SourcePanelId` persisted in `ComponentState` | 4 |
| Plugin isolation | Engine's `PluginLoader` with `SharedAssemblyNames` delegation | 1 |
| Multi-plugin per assembly | `PluginLoader` iterates `ExportedTypes` | 1 |

---

## 13. V1 Acceptance Checklist

- [ ] Engine builds with **zero** `Microsoft.AspNetCore.*` references.
- [ ] `DdsMonitor.Avalonia.exe` launches an empty shell on Windows (Linux/macOS deferred).
- [ ] `DdsMonitor.Avalonia.exe --DdsSettings:HeadlessMode=Record --GeneratorPlugin:Enabled=true` runs headlessly, writes a recording file, no window appears.
- [ ] Five plugin classes load from `./plugins/DdsMonitor.Avalonia.StandardPlugin.dll` (one DLL, five `IMonitorPlugin` implementations).
- [ ] User adds a schema DLL at runtime via the schema panel; topics appear in the explorer without app restart.
- [ ] User adds a second DDS participant on a different domain at runtime; both participants' topics merge into the registry.
- [ ] Double-click on a topic opens a `SamplesViewer` for that topic. Double-click on a second topic opens a second `SamplesViewer`. Double-click on the first topic again focuses the existing window (no duplicate).
- [ ] `SamplesViewer` sustains 5 kHz from the dummy generator for 60 s with smooth scrolling and live filter response.
- [ ] Right-click on a topic in the explorer shows "Toggle Dummy Generator" injected by `DummyDataGeneratorPlugin`.
- [ ] Right-click on a sample row in the viewer shows "Clone to Send" injected by `SendSamplePlugin`.
- [ ] "Clone to Send" opens a `SendSample` panel pre-filled with the payload; submitting it round-trips back to the viewer.
- [ ] All existing `--AppSettings:*` and `--DdsSettings:*` CLI arguments continue to work without modification.
- [ ] Shell toolbar shows Play, Pause, Reset transport controls; Pause halts sample ingestion globally while DDS participants remain connected.
- [ ] Detail Inspector opens as an independent floating window linked to a `SamplesViewer`; the Link/Unlink toggle pins or follows the selection.
- [ ] Multiple independent Inspector windows can be open simultaneously, each linked to a different `SamplesViewer`.
- [ ] Inspector shows Sample Info (timestamps, generation rank) and Sender (process ID, IP) in addition to the field tree.
- [ ] Window geometry, column layout, filter strings, `IsLinked` state, and "Show Hidden Topics" preference survive an app restart.

---

## 14. Open Questions & Risks

1. **`TreeDataGrid` virtualization under 5 kHz.** Avalonia's `TreeDataGrid` handles tens of
   thousands of rows well in benchmarks; Phase 4 must confirm it sustains 5 kHz append + filter
   without lag. Fallback: a custom `Canvas`-based virtualized list.
2. **EventBroker threading.** The Engine broker delivers on the publisher's thread. The
   `SubscribeOnUiThread` extension method must be implemented and enforced in plugin guidelines.
3. **`workspace.json` schema compatibility.** Keeping byte-compatibility with Blazor is desirable
   for shared team setups. Avalonia writes window geometry into `ComponentState["__window"]` to
   avoid touching the top-level `PanelState` schema.
4. **Standard drawers location.** `int`/`double`/`string`/`bool`/`enum` drawers could live in
   the shell or in a separate `DefaultDrawersPlugin`. Putting them in a plugin enforces the
   dogfood rule. Recommendation: included in `StandardPlugin` for V1, extracted later.
5. **Blazor adapter for purified registries.** Phase 0 must not break the running Blazor app.
   Adapter classes in `DdsMonitor.Blazor` bridge the new `Func<DrawerContext, object>` API
   back to `RenderFragment`. These adapters are a `DdsMonitor.Blazor`-only concern.

---

## 15. Migration Path After V1

The Blazor app continues shipping during the migration. After V1, subsequent work becomes
incremental plugin writing against stable contracts:

### Phase 7 — DDS Diagnostics Depth & Tooling

These are the first post-V1 plugins. All reference only Engine + Core.

| Task | Plugin / Feature | Key APIs |
|------|-----------------|----------|
| TASK-H001 | `InstancesViewerPlugin` — DDS instance lifecycle tracking | `IInstanceStore` (Engine) — Alive/NotAlive/NoWriters states, keyed instance grouping |
| TASK-H002 | `ExportPlugin` — data export pipeline | `IExportService`, `IExportFormatRegistry` (Engine) — `ExportFilteredSamplesAsync`, JSON + plugin-injected formats |
| TASK-H003 | `WorkspaceManagerPlugin` — manual layout management | `IMenuRegistry`: "File/Export Layout…", "File/Import Layout…", "File/Reset Layout" → `IWorkspaceState` serialisation |
| TASK-H004 | `FilterBuilderPlugin` — visual LINQ expression builder | Broadcasts `ApplyFilterRequestEvent(TargetPanelId, FilterText)` via `IEventBroker`; `SamplesViewerViewModel` subscribes |
| TASK-H005 | Custom Column Picker — per-`SamplesViewer` column configuration | Dialog over `TopicMetadata.AllFields`; dynamic `TreeDataGrid` column rebuild; binds `FieldMetadata.Getter` directly (zero reflection overhead) |

### Phase 8 — V2 Shell Enhancements

- Integrate `Dock.Avalonia` by reimplementing `AvaloniaWindowManager`. Plugin code unchanged.
- Hex viewer plugin, sparklines overlay, statistics panel, replay UI panel.

