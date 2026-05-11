# DdsMonitor.Avalonia — V1 MVP Design

> Port of `DdsMonitor.Blazor` to a strictly local, multiplatform Avalonia desktop application, rebuilt from the ground up as a VS Code–style plugin shell on top of the existing `DdsMonitor.Engine`.

---

## 1. Goals & Non-Goals

### Goals
- **Multiplatform desktop**, not browser-hosted. Avalonia for the UI, .NET 8.
- **Strict plugin architecture**: the shell knows nothing about DDS. Every feature is delivered by a plugin loaded from a `/plugins` folder. A plugin assembly may contain multiple plugins.
- **Reuse `DdsMonitor.Engine` as-is** wherever possible. The Engine already implements expression-tree-compiled type metadata, a UI-agnostic broker/registry layer, dynamic participant management, collectible assembly contexts for schema DLLs, dictionary-backed panel state persistence, and a background-threaded filtering pipeline. The Avalonia work is mostly UI binding.
- **Headless dual-boot**: the same executable can run as either an interactive desktop app or a non-interactive CLI tool (record/replay), driven entirely by CLI arguments.
- **Compatibility with existing CLI arguments** (`--DdsSettings:DomainId=…`, `--AppSettings:TopicSources:0=…`, `--DdsSettings:HeadlessMode=Record`, etc.). No script changes for existing deployments.
- **Prove the architecture, not the feature set**. V1 deliberately ships a thin slice of features; the goal is to validate every high-risk vector (performance, decoupling, headless, persistence) before porting the rest.

### Non-Goals for V1
- Full feature parity with `DdsMonitor.Blazor`. Replay/record UI, hex viewer, filter builder, sparklines, statistics overlay, devel mode self-send UI, custom export formats, etc. are deferred.
- A docking framework. V1 uses **floating windows only**. `Dock.Avalonia` is a V2 swap-in once the panel manager contract is stable.
- Plugin sandboxing / signing / marketplace. Plugins are trusted code in V1.
- Hot-reload of plugin assemblies. Schema DLLs hot-load; plugins load once at startup.

---

## 2. High-Level Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                     DdsMonitor.Avalonia (shell)                   │
│  Generic Host • DI • WindowManager • Plugin Loader • Main Menu   │
│  Toolbar • IUserSettings • workspace.json persistence            │
│   — does NOT reference DDS or Engine domain types directly —      │
└────────────┬──────────────────────────────────────┬──────────────┘
             │ resolves via DI                      │ loads at startup
             ▼                                      ▼
┌──────────────────────────────┐   ┌─────────────────────────────────┐
│  DdsMonitor.Avalonia.Core    │   │     /plugins/*.dll              │
│  (shared contracts)          │   │   StandardPlugin, Generator…    │
│  IMonitorPlugin              │◄──┤   each impl. IMonitorPlugin     │
│  IWindowManager / PanelAddr  │   │   referencing Core + Engine     │
│  IMenuRegistry, IToolbarReg  │   └──────────────┬──────────────────┘
│  IContextMenuRegistry        │                  │ ctor-injects
│  ITypeDrawerRegistry (Avlnia)│                  ▼
│  IUserSettings               │   ┌─────────────────────────────────┐
└──────────────┬───────────────┘   │      DdsMonitor.Engine          │
               │                   │  IDdsBridge • ITopicRegistry    │
               │ both reference    │  ISampleStore • SampleView      │
               └──────────────────►│  IFilterCompiler • TopicMetadata│
                                   │  IAssemblySourceService         │
                                   │  IEventBroker • IValueFormatter │
                                   │  HeadlessRunnerService          │
                                   └─────────────────────────────────┘
```

### Layering rules
- `DdsMonitor.Engine` — unchanged domain layer. Must be **purified** of remaining Blazor types (`RenderFragment` in `ISampleViewRegistry` and `ITypeDrawerRegistry`). Those become Blazor-free factory delegates.
- `DdsMonitor.Avalonia.Core` — UI-agnostic plugin contracts plus Avalonia-flavoured registries (`ITypeDrawerRegistry` Avalonia variant returning `Control` / `DataTemplate`). References the Engine for shared domain types.
- `DdsMonitor.Avalonia` — the shell executable. References Engine + Core. Implements registries, window manager, plugin loader. No DDS code.
- `DdsMonitor.Avalonia.StandardPlugin` (and any other plugin) — references Engine + Core only. Does **not** reference the shell executable.

---

## 3. Solution Layout

```
tools/DdsMonitor/
├── DdsMonitor.Engine/                       (existing, lightly purified)
├── DdsMonitor.Blazor/                       (existing, untouched)
├── DdsMonitor.Avalonia.Core/                NEW — shared contracts
├── DdsMonitor.Avalonia/                     NEW — shell exe
└── DdsMonitor.Avalonia.StandardPlugin/      NEW — V1 plugins live here
```

A single plugin assembly may host multiple `IMonitorPlugin` implementations; the loader iterates `assembly.ExportedTypes` looking for the interface, identical to the existing Blazor pattern.

---

## 4. Shell Responsibilities (`DdsMonitor.Avalonia`)

The shell is a `.NET 8` Avalonia desktop project that does five things and no more:

1. **Bootstrap the Generic Host.** `Host.CreateApplicationBuilder(args)` binds all existing `--AppSettings:*` and `--DdsSettings:*` CLI arguments to `IOptions<AppSettings>` / `IOptions<DdsSettings>` automatically — no custom parser.
2. **Register Engine and Core services** in DI. Engine exposes an `AddDdsEngineCore()` extension; the shell calls it and adds its own registry implementations.
3. **Discover and load plugins.** Iterate `./plugins/*.dll` with a collectible-aware `AssemblyLoadContext`, locate every `IMonitorPlugin`, call `ConfigureServices(IServiceCollection)` on each before building the container, then call `Initialize(IServiceProvider)` after.
4. **Dual-boot decision.** Read `DdsSettings.HeadlessMode`:
   - `None` → start background services AND boot Avalonia (`StartWithClassicDesktopLifetime`).
   - `Record` / `Replay` → `await host.RunAsync()`. Avalonia is never initialized; the existing `HeadlessRunnerService` handles the work.
5. **Provide the empty UI scaffold.** `ShellWindow` containing:
   - A `Menu` bound to `IMenuRegistry`.
   - A `ToolBar` (or `StackPanel`) bound to `IToolbarRegistry`.
   - A status bar (placeholder for V1).
   - **No panel area.** All panels are floating `Window` instances managed by `IWindowManager`.

### Why floating windows in V1
A docking framework is a large dependency and constrains the design of `IWindowManager`. By starting with plain `Window`s addressed by `PanelAddress`, every persistence and routing concern is forced into the contract, not the host. V2 swaps the implementation for `Dock.Avalonia` without changing plugins.

---

## 5. Core Contracts (`DdsMonitor.Avalonia.Core`)

### 5.1 Plugin lifecycle

```csharp
public interface IMonitorPlugin
{
    void ConfigureServices(IServiceCollection services);
    void Initialize(IServiceProvider provider);
}
```

The shell calls `ConfigureServices` on **all** plugins before `Build()`, then `Initialize` on **all** plugins afterwards. Plugins register their view-models and `IHostedService`s in `ConfigureServices`; they push UI registrations (menu items, toolbar buttons, context commands) in `Initialize`.

### 5.2 Panel addressing & window manager

The cornerstone of inter-plugin UI routing. A `PanelAddress` is an immutable, hierarchical identifier independent of physical placement.

```csharp
public sealed record PanelAddress(string Kind, string? Discriminator = null)
{
    public string Key => Discriminator is null ? Kind : $"{Kind}/{Discriminator}";
}
// Examples:
//   new("TopicExplorer")                      → singleton window
//   new("SamplesViewer", "RobotTelemetry")    → per-topic window, opens new instance per topic
```

```csharp
public interface IWindowManager
{
    /// Spawns the window if not already open; otherwise focuses it.
    /// PanelState (position, size, ComponentState dictionary) is restored from workspace.json.
    void Open(PanelAddress address, Func<PanelState, object> viewModelFactory, string title);

    void Close(PanelAddress address);

    bool IsOpen(PanelAddress address);
}
```

`PanelState` is the **existing Engine type** (`Engine/Workspace/PanelState.cs`) and includes window geometry plus the `Dictionary<string, object> ComponentState` slot used by Blazor today. The Avalonia `WindowManager` reuses this verbatim and writes through the existing `WorkspacePersistenceService` so `workspace.json` stays format-compatible with the Blazor version.

### 5.3 Stateful view-models (optional)

```csharp
public interface IStatefulViewModel
{
    void RestoreState(IReadOnlyDictionary<string, object> componentState);
    void CaptureState(IDictionary<string, object> componentState);
}
```

Implemented by view-models that need to persist filter strings, column widths, custom column definitions, etc. The `WindowManager` calls `CaptureState` on close (and debounced periodically), and `RestoreState` on open. The shell's persistence layer runs `FilterPersistableState` (existing Engine sanitizer) so plugins can only push JSON-safe primitives, arrays, and nested dictionaries.

### 5.4 Menu, toolbar, context menu

```csharp
public interface IMenuRegistry      { void Register(string path, ICommand command, string? icon = null); }
public interface IToolbarRegistry   { void Register(string id, ICommand command, string? icon, string tooltip); }

public interface IContextMenuRegistry
{
    /// Plugins register actions bound to a CLR type. The owning panel queries by type at open-time.
    void Register<T>(string title, Func<T, ICommand> commandFactory);
    IEnumerable<(string title, ICommand cmd)> Resolve<T>(T target);
}
```

The `IContextMenuRegistry` is the mechanism that lets, e.g., `SendSamplePlugin` inject a "Clone to Send" action into the `SamplesViewerPlugin`'s grid rows without either plugin referencing the other. The Engine already exposes an equivalent contract; the Core version simply wraps it for Avalonia commands.

### 5.5 Type drawers and value formatters

Two distinct registries — never combine them:

- **`IValueFormatterRegistry`** (already in Engine, UI-agnostic) — read-only display. Maps `Type` to a token sequence + plain-text representation for grid cells and inspector trees. Reused as-is.
- **`ITypeDrawerRegistry` Avalonia variant** (in `Avalonia.Core`) — two-way data authoring. Maps `Type` to a factory that produces an Avalonia `Control` bound to a `DrawerContext` (the existing Engine binding contract: `ValueGetter` / `OnChange` over the pre-compiled expression-tree setters).

```csharp
public interface IAvaloniaTypeDrawerRegistry
{
    void Register(Type type, Func<DrawerContext, Control> factory);
    Control Build(DrawerContext ctx);  // falls back to a generic StackPanel walker
}
```

The standard drawers for `int`, `double`, `string`, `bool`, `enum` ship in the shell or in a `StandardDrawersPlugin`. Plugins can register custom drawers for domain types (e.g., a `GeoCoord` map picker).

### 5.6 User settings & events

- **`IUserSettings`** — wraps a single `settings.json` in the OS AppData folder, namespaced by plugin id: `Get<T>(pluginId, key, default)`, `Set(pluginId, key, value)`, debounced `SaveAsync()`. This is **separate** from `workspace.json` (which holds per-panel state). User settings are global preferences ("show hidden topics", "theme").
- **`IEventBroker`** — reuse the Engine's existing broker. Strictly for **app state and user intent** — not for high-frequency DDS data. Pre-defined records reused from Engine (`SpawnPanelEvent`, `ParticipantsChangedEvent`, `SampleSelectedEvent`, `WorkspaceSaveRequestedEvent`).

---

## 6. The V1 Plugin Set

All five live in `DdsMonitor.Avalonia.StandardPlugin` (one assembly, multiple `IMonitorPlugin` classes). This proves multi-plugin-per-assembly loading from day one.

### 6.1 `DummyDataGeneratorPlugin` — backend prover
**Validates:** background services, headless mode, CLI parameter binding, context-menu cross-plugin injection.

- Registers an `IHostedService` that, when `--GeneratorPlugin:Enabled=true`, opens a writer via `IDdsBridge.GetWriter(...)` for a synthetic `Heartbeat` topic and publishes at `--GeneratorPlugin:PublishRateMs` (default 100ms; target 5000 Hz under stress to prove the firehose pipeline).
- Registers a "Toggle Dummy Generator" command in `IContextMenuRegistry` bound to topic names — `TopicExplorerPlugin` picks it up automatically.
- Adds an "Tools → Dummy Generator" menu item.
- Runs identically in headless mode (no UI dependency).

### 6.2 `WorkspaceManagerPlugin` — schema + network configuration
**Validates:** dynamic schema DLL hot-load via collectible `AssemblyLoadContext`, dynamic participant lifecycle, CLI override compatibility.

- Schema panel: Avalonia port of `TopicSourcesPanel.razor`. Uses the OS-native file picker → `IAssemblySourceService.Add(dllPath)` → existing `TopicDiscoveryService` does the rest. Listens for `IAssemblySourceService.Changed` and `ITopicRegistry.Changed`.
- Network panel: Avalonia port of `ParticipantEditorDialog`. Binds to `IDdsBridge.Participants`, calls `AddParticipant(domainId, partition)` / `RemoveParticipant(index)`, publishes `ParticipantsChangedEvent`.
- Honours startup CLI overrides (`--AppSettings:TopicSources:0=…`, `--DdsSettings:Participants:0:DomainId=0`) — they preload the same services this UI mutates.

### 6.3 `TopicExplorerPlugin` — frontend prover
**Validates:** Avalonia rendering, panel addressing, menu/toolbar/context-menu integration, user settings persistence, intent routing via `IEventBroker`.

- Opens `new PanelAddress("TopicExplorer")` — singleton floating window, position restored from `workspace.json`.
- `ListBox` bound to `ITopicRegistry` topics, refreshed on `ITopicRegistry.Changed`.
- Adds "View → Topic Explorer" menu item and a toolbar toggle.
- "Show Hidden Topics" checkbox — state read from / written to `IUserSettings` (proves global preference persistence).
- Right-click on a topic builds a context menu by calling `IContextMenuRegistry.Resolve<TopicName>(...)` — automatically includes "Toggle Dummy Generator" injected by 6.1.
- Double-click publishes `SpawnPanelEvent(new PanelAddress("SamplesViewer", topicName))`. The shell (or `SamplesViewerPlugin`) handles routing.

### 6.4 `SamplesViewerPlugin` — high-performance UI prover
**Validates:** per-instance addressed windows, virtualized 5 kHz+ data binding, background filtering, expression-tree payload inspection, per-instance state persistence.

- Subscribes to `SpawnPanelEvent` for `Kind == "SamplesViewer"`. Asks `IWindowManager.Open(...)` with a per-topic `Discriminator`. Two topics → two windows; the same topic twice → focus existing.
- View-model implements `IStatefulViewModel`: stores selected columns, column widths, sort direction, filter text in `ComponentState`.
- **Filtering pipeline:**
  1. View-model constructs an Engine `SampleView`, injecting the singleton `ISampleStore`. `SampleView` spins its own background worker.
  2. User filter string → `IFilterCompiler.Compile(expr)` → `Func<SampleData,bool>` → `_view.SetFilter(predicate)`.
  3. `SampleView.OnViewRebuilt` fires from the worker → view-model marshals to UI via `Dispatcher.UIThread.InvokeAsync` and tells the grid that `_view.CurrentFilteredCount` changed.
  4. Avalonia `TreeDataGrid` pulls visible rows by calling `_view.GetVirtualView(start, count)` → zero-allocation `ReadOnlyMemory<SampleData>` slice. No UI-thread filtering, no per-sample event, no polling timer.
- **Detail inspector** (lower half of the same window, or a child panel): on row click, iterates `sample.TopicMetadata.AllFields` (the pre-compiled `FieldMetadata`s) and builds a tree of view-models. Each leaf renders via `IValueFormatterRegistry` for display.

### 6.5 `SendSamplePlugin` — authoring prover
**Validates:** two-way binding through compiled expression-tree setters, the `ITypeDrawerRegistry`/`DrawerContext` round-trip, writer acquisition through `IDdsBridge.GetWriter`.

- Registers a "Clone to Send" context-menu action on `SampleData` rows (cross-plugin injection into 6.4).
- Spawns `new PanelAddress("SendSample", topicName)`.
- Instantiates an empty payload via `Activator.CreateInstance(TopicMetadata.TopicType)`, wraps each `FieldMetadata` in a `DrawerContext`, and queries `IAvaloniaTypeDrawerRegistry.Build(ctx)` for each field. The returned `Control` is strictly typed (e.g., `NumericUpDown` for `int`) — no `Convert.ChangeType` on submit.
- On "Send", calls `IDdsBridge.GetWriter(TopicMetadata)` then `writer.Write(payload)`.

---

## 7. Critical Architectural Patterns

These five concepts are the ones that, if mis-designed, require a rewrite later. The V1 MVP intentionally exercises each one:

| Concern | Pattern | Where it's proved in V1 |
| --- | --- | --- |
| Headless dual-boot | Generic Host branches on `HeadlessMode` before Avalonia init | 6.1 + shell `Program.cs` |
| Schema DLL hot-load | Collectible `AssemblyLoadContext` via `TopicDiscoveryService` | 6.2 |
| Dynamic DDS participants | `IDdsBridge.AddParticipant` / `RemoveParticipant` + `ParticipantsChangedEvent` | 6.2 |
| Firehose UI strategy | `SampleView` background worker + `OnViewRebuilt` + `GetVirtualView` zero-alloc slice | 6.4 |
| Inter-plugin UI injection | `IContextMenuRegistry` keyed by CLR type | 6.1 → 6.4, 6.5 → 6.4 |
| Addressed panel persistence | `PanelAddress` + `PanelState.ComponentState` dictionary | 6.4 |
| Expression-tree payload IO | Engine's `TopicMetadata.AllFields` Getter/Setter | 6.4 (read), 6.5 (write) |

---

## 8. Migration Path From `DdsMonitor.Blazor`

The Blazor app stays running and shipping during the migration. Avalonia is built alongside it.

### Phase 0 — Engine purification (small, mechanical)
Goal: remove the last Blazor leak so the Engine compiles without `Microsoft.AspNetCore.Components`.

- `ISampleViewRegistry` and `ITypeDrawerRegistry` currently use `RenderFragment`. Replace with `Type` + a UI-agnostic factory delegate (e.g., `Func<DrawerContext, object>`), where the `object` is whatever the UI shell understands. The Blazor app provides a Blazor adapter that casts back to `RenderFragment`; the Avalonia app provides an adapter producing `Control`.
- Verify the Engine builds and tests pass with no `Microsoft.AspNetCore.*` reference.

### Phase 1 — Empty shell
- Create `DdsMonitor.Avalonia.Core` with the contracts in §5.
- Create `DdsMonitor.Avalonia` referencing Engine + Core. Generic Host, plugin loader (port from Blazor's `PluginLoader`), `WindowManager`, registry implementations, `ShellWindow.axaml` with menu + toolbar bound to the registries.
- Smoke test: app launches, empty menu, no plugins, exits cleanly. Headless path: `--DdsSettings:HeadlessMode=Record` does not open a window.

### Phase 2 — First plugin pair
- Implement `WorkspaceManagerPlugin` (schema panel only) and `TopicExplorerPlugin`. Both alone validate plugin loading, registry wiring, `ITopicRegistry` consumption, and dynamic DLL hot-load.
- Acceptance: load a schema DLL via the schema panel → topic list updates in the explorer.

### Phase 3 — Backend prover
- Implement `DummyDataGeneratorPlugin`. Add `IContextMenuRegistry` integration so the explorer shows the injected command.
- Acceptance: `--HeadlessMode=Record --GeneratorPlugin:Enabled=true` writes a recording file with no UI. Interactive mode shows the cross-plugin context-menu item.

### Phase 4 — Firehose UI
- Implement `SamplesViewerPlugin` with `TreeDataGrid` + `SampleView`. This is the highest-risk slice; it should hit 5 kHz sustained from the dummy generator with the grid scrolling smoothly.
- Acceptance: 5 000 samples/s for 60 s, no dropped frames, RAM stable, filter text changes apply within 200 ms.

### Phase 5 — Authoring + Participants
- Implement `SendSamplePlugin` and finish `WorkspaceManagerPlugin` participant editor.
- Acceptance: send a hand-authored sample on a user-loaded schema, observe it round-trip back to the viewer.

### Phase 6 — Workspace polish
- Per-panel `IStatefulViewModel` round-trip for the samples viewer. Confirm `workspace.json` is byte-compatible with the Blazor version where panel kinds overlap.
- Acceptance: open three samples viewers on different topics, resize, move across monitors, close app, reopen — exact restore.

After Phase 6, V1 is complete. V2 work (Dock.Avalonia, hex viewer, replay UI, filter builder, sparklines, statistics overlay, export plugins, themes) becomes incremental plugin work against a stable contract.

---

## 9. Open Questions / Risks

1. **`TreeDataGrid` virtualization limits.** Avalonia's grid handles tens of thousands of rows well; the prover at Phase 4 must confirm it sustains 5 kHz append + filter without lag. Fallback: a custom `Canvas`-based virtualized list.
2. **Collectible `AssemblyLoadContext` for plugins.** The Engine already uses collectible contexts for schema DLLs. Plugins in V1 are non-collectible (loaded once). If V2 wants plugin hot-reload, the plugin loader will need the collectible variant — but every static cache in plugins becomes a leak. Defer to V2.
3. **Threading model around `IEventBroker`.** The Engine's broker delivers on the publisher's thread. UI subscribers must marshal via `Dispatcher.UIThread.InvokeAsync`. Make this explicit in plugin guidelines; consider adding a `Subscribe(handler, dispatch: UiThread)` overload to the Core wrapper.
4. **`workspace.json` schema compatibility.** Keeping byte-compatibility with the Blazor version is desirable but optional. The Avalonia `WindowManager` may write Avalonia-specific keys (`X`, `Y` of the floating window) inside `PanelState.ComponentState["__window"]` to keep the top-level schema unchanged.
5. **Standard drawers location.** `int`/`double`/`string`/`bool`/`enum` drawers could ship in the shell or in a tiny `DefaultDrawersPlugin`. Putting them in a plugin enforces the dogfood rule (the shell knows nothing). Recommendation: separate plugin.

---

## 10. V1 Acceptance Checklist

When all of the following pass, V1 is done:

- [ ] Engine builds with zero `Microsoft.AspNetCore.*` references.
- [ ] `DdsMonitor.Avalonia.exe` launches an empty shell on Windows, Linux, and macOS.
- [ ] `DdsMonitor.Avalonia.exe --DdsSettings:HeadlessMode=Record --DdsSettings:HeadlessFilePath=out.json --GeneratorPlugin:Enabled=true` records a file with no UI.
- [ ] Five plugins load from `./plugins/DdsMonitor.Avalonia.StandardPlugin.dll`, all from one assembly.
- [ ] User loads a schema DLL at runtime via the schema panel; topics appear in the explorer without app restart.
- [ ] User adds a second DDS participant on a different domain at runtime; both participants' topics merge into the registry.
- [ ] Double-click on a topic opens a samples viewer addressed to that topic. Double-click on a second topic opens a second viewer. Double-click on the first topic again focuses the existing window.
- [ ] Samples viewer sustains 5 kHz from the dummy generator with smooth scrolling and live filter response.
- [ ] Right-click on a topic in the explorer shows the "Toggle Dummy Generator" command injected by a different plugin.
- [ ] Right-click on a sample row in the viewer shows "Clone to Send" injected by yet another plugin, which opens the send panel pre-filled with the sample's payload.
- [ ] User edits a payload field in the send panel; submitting it round-trips back to the viewer.
- [ ] All existing `--AppSettings:*` and `--DdsSettings:*` CLI arguments from the Blazor version continue to work without modification.
- [ ] Window positions, column layouts, filter strings, and the "Show Hidden Topics" preference all survive an app restart.



------------------------------------

Reviewing the V1 design document above against our architectural refinements reveals several gaps and outdated concepts that diverge from the established capabilities of `DdsMonitor.Engine`. 

Here are the flaws that need to be corrected in the blueprint:

**1. Outdated State Persistence Pattern**
Section 5.3 and Section 6.4 still define an `IStatefulViewModel` interface requiring the window manager to invoke `CaptureState` and `RestoreState` lifecycle callbacks. This contradicts our refined approach. The existing engine already provides a `PanelState` model containing a `Dictionary<string, object> ComponentState`. The Avalonia shell should inject this specific dictionary directly into the plugin's ViewModel upon instantiation. The ViewModel then mutates its dictionary keys continuously and relies on the `IEventBroker` to publish a `WorkspaceSaveRequestedEvent`, triggering the shell's debounced persistence pipeline.

**2. Omission of `ISampleViewRegistry` in the Detail Inspector**
The document correctly segregates `IValueFormatterRegistry` (for read-only text formatting) and the Avalonia variant of `ITypeDrawerRegistry` (for two-way data authoring). However, Section 6.4 mandates that the Detail Inspector must always iterate `TopicMetadata.AllFields` to construct a generic tree of view-models. This completely ignores the `ISampleViewRegistry` extension point. To support complex domain types, the Detail Inspector must first query `ISampleViewRegistry` to determine if a plugin has registered a custom Avalonia view to entirely replace the default hierarchical tree for a specific CLR payload type.

**3. Unresolved EventBroker Threading Model**
Section 9 flags the threading model for `IEventBroker` as an "Open Question", suggesting we rely on plugin guidelines for UI dispatching. In a strict MVVM desktop shell, this is an architectural hazard. Because the Engine's broker delivers messages on the publisher's background thread, leaving thread marshaling up to individual plugin developers guarantees inevitable cross-thread UI crashes. The `DdsMonitor.Avalonia.Core` implementation of the broker must explicitly enforce `Dispatcher.UIThread` marshaling for UI-bound subscriptions.

**4. `PanelAddress` vs `PanelId` Divergence**
The design proposes a new immutable, hierarchical `PanelAddress` type for routing UI intent. This introduces unnecessary friction with the Engine's existing `PanelState` implementation, which relies on a simple string `PanelId` to maintain seamless `workspace.json` compatibility. To reuse the engine's `WorkspacePersistenceService` without modification, the shell should adhere to the established string-based `PanelId` convention (e.g., concatenating the panel type and discriminator, like `"SamplesViewer_RobotTelemetry"`) rather than inventing a new domain type.

