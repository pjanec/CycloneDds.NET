# DdsMonitor Plugin API — Enhanced Design

**Project:** DdsMonitor Plugin API Enhancements  
**Prefix:** PLA1  
**Last Updated:** 2026-03-26

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Current State Analysis](#2-current-state-analysis)
3. [Design Goals & Principles](#3-design-goals--principles)
4. [Phase 1 — Capability-Querying Context (Future-Proof Foundation)](#4-phase-1--capability-querying-context-future-proof-foundation)
5. [Phase 2 — Context Menu Registry](#5-phase-2--context-menu-registry)
6. [Phase 3 — Detail View Hijacking (ISampleViewRegistry)](#6-phase-3--detail-view-hijacking-isampleviewregistry)
7. [Phase 4 — Workspace Settings Integration](#7-phase-4--workspace-settings-integration)
8. [Phase 5 — Plugin Manager UI](#8-phase-5--plugin-manager-ui)
9. [Phase 6 — Advanced Extension Points](#9-phase-6--advanced-extension-points)
10. [Phase 7 — Kitchen Sink Demo Plugin](#10-phase-7--kitchen-sink-demo-plugin)
11. [Phase 8 — Autonomous CI Testing](#11-phase-8--autonomous-ci-testing)
12. [ECS Plugin Migration](#12-ecs-plugin-migration)

---

## 1. Executive Summary

The current DdsMonitor plugin API is a basic hosting bridge: it allows a plugin to add top-bar menu items and register floating Blazor windows. It has no concept of deep UI integration, unified settings persistence, or runtime compatibility negotiation.

This design upgrades the plugin API to a fully-featured extensibility platform with these properties:

- **Future-proof:** plugins gracefully degrade on older hosts; older plugins work unchanged on newer hosts.
- **Deeply integrated:** plugins can inject context menus into every core panel, replace detail views, format values, and more.
- **Settings-unified:** plugin settings are stored alongside DdsMonitor's own workspace file with zero custom I/O code required from the plugin.
- **User-controlled:** plugins are not loaded automatically; a Plugin Manager panel gives users explicit enable/disable control.
- **Demonstrable & testable:** a "Kitchen Sink" demo plugin exercises every extension point; all points are covered by CI-runnable unit and Blazor component tests.

---

## 2. Current State Analysis

### 2.1 Plugin API Interfaces

| Interface / Type | Location | Role |
|---|---|---|
| `IMonitorPlugin` | `DdsMonitor.Engine/Plugins/IMonitorPlugin.cs` | Plugin contract (Name, Version, ConfigureServices, Initialize) |
| `IMonitorContext` | `DdsMonitor.Engine/Plugins/IMonitorContext.cs` | Passed to `Initialize`; exposes `MenuRegistry` and `PanelRegistry` |
| `IMenuRegistry` | `DdsMonitor.Engine/Plugins/IMenuRegistry.cs` | Slash-path menu tree |
| `PluginPanelRegistry` | `DdsMonitor.Engine/Plugins/PluginPanelRegistry.cs` | Maps display names to Blazor component types |
| `PluginLoader` | `DdsMonitor.Engine/Plugins/PluginLoader.cs` | Scans directories, loads all DLLs unconditionally |

### 2.2 Existing Registries (Internal, Not Exposed to Plugins)

| Service | Location | Purpose |
|---|---|---|
| `IValueFormatterRegistry` / `IValueFormatter` | `DdsMonitor.Engine/Ui/` | Inline token rendering for specific CLR types |
| `ITypeDrawerRegistry` | `DdsMonitor.Engine/Ui/ITypeDrawerRegistry.cs` | Custom Blazor input controls for `DynamicForm` |
| `IEventBroker` | `DdsMonitor.Engine/IEventBroker.cs` | Application-wide pub/sub event bus |
| `TopicColorService` | `DdsMonitor.Engine/TopicColorService.cs` | Topic color management |
| `IExportService` | `DdsMonitor.Engine/Export/IExportService.cs` | JSON sample export |
| `IFilterCompiler` | `DdsMonitor.Engine/IFilterCompiler.cs` | Dynamic LINQ filter compilation |
| `TopicMetadata` / `FieldMetadata` | `DdsMonitor.Engine/Metadata/` | Schema and compiled getter delegates |

### 2.3 Pain Points

1. **No deep UI integration** — context menus in `SamplesPanel`, `InstancesPanel`, `TopicExplorerPanel` are hardcoded; plugins cannot contribute actions.
2. **No unified settings** — the ECS plugin reinvents file I/O with its own `ecs-settings.json` instead of participating in the workspace persistence lifecycle.
3. **No version negotiation** — adding any new property to `IMonitorContext` breaks binary compatibility with existing plugins.
4. **Automatic loading** — all DLLs in the plugins directory are loaded unconditionally; users cannot disable a plugin without removing the file.
5. **No custom detail views** — `DetailPanel.razor` always renders the generic tree; plugins cannot replace it for specific payload types.

---

## 3. Design Goals & Principles

### 3.1 Core Design Principles

**Principle A — Capability Querying (Feature Provider Pattern)**  
`IMonitorContext` exposes a single `GetFeature<TFeature>()` method. Plugins ask for capabilities at runtime. If the host does not provide a feature, the method returns `null`; the plugin degrades gracefully without crashing.

**Principle B — Event-Driven Decoupling**  
The `IEventBroker` is the backbone for cross-cutting concerns (settings persistence, selection events). Plugins subscribe to events instead of implementing host-required interfaces.

**Principle C — Dictionary-Based State Passing**  
Plugin settings handed to the host are `Dictionary<string, object>` — the same pattern used by `IWindowManager.SpawnPanel`. The host serializes this without needing plugin-specific knowledge.

**Principle D — String-Path Registries**  
Extension points that relate to UI menus or hooks use slash-delimited string paths (e.g., `"ContextMenu/TopicPanel"`), consistent with the existing `IMenuRegistry` convention.

**Principle E — Two-Phase Plugin Loading**  
Discovery (reading Name/Version) is separated from activation (calling ConfigureServices/Initialize). Only user-enabled plugins are activated.

### 3.2 Compatibility Matrix

| Scenario | Outcome |
|---|---|
| New plugin on old host | `GetFeature<IContextMenuRegistry>()` returns `null` → plugin skips context menus, still registers panels |
| Old plugin on new host | Old plugin never calls new `GetFeature` paths → works unchanged |
| Plugin without `Version` property override (future) | Host reads via string reflection; falls back to `"unknown"` |

---

## 4. Phase 1 — Capability-Querying Context (Future-Proof Foundation)

### 4.1 Redesign `IMonitorContext`

Replace the hardcoded property bag with a single `GetFeature<TFeature>()` method:

```csharp
public interface IMonitorContext
{
    /// <summary>
    /// Returns the requested host feature, or null when the host does not support it.
    /// Plugins MUST perform a null check after calling this method to ensure
    /// graceful degradation on older host versions.
    /// </summary>
    TFeature? GetFeature<TFeature>() where TFeature : class;
}
```

The concrete `MonitorContext` implementation holds an internal `IServiceProvider` and resolves features by type:

```csharp
internal sealed class MonitorContext : IMonitorContext
{
    private readonly IServiceProvider _services;
    public MonitorContext(IServiceProvider services) => _services = services;
    public TFeature? GetFeature<TFeature>() where TFeature : class
        => _services.GetService<TFeature>();
}
```

All previously hardcoded registries (`MenuRegistry`, `PanelRegistry`) are registered in the host DI container so they are still retrievable via `GetFeature<IMenuRegistry>()` and `GetFeature<PluginPanelRegistry>()`.

### 4.2 Backward Compatibility Shim

The old `IMonitorContext.MenuRegistry` and `PanelRegistry` properties are removed. Because the ECS plugin is in this same repository (not a third-party binary), it will be migrated to the new API in [Phase — ECS Migration](#12-ecs-plugin-migration).

---

## 5. Phase 2 — Context Menu Registry

### 5.1 Interface

```csharp
public interface IContextMenuRegistry
{
    /// <summary>
    /// Registers a function that yields extra context menu items for every
    /// right-click event that carries a context payload of type TContext.
    /// </summary>
    void RegisterProvider<TContext>(Func<TContext, IEnumerable<ContextMenuItem>> provider);

    /// <summary>
    /// Called by UI panels to collect all plugin-injected menu items for the given context.
    /// </summary>
    IEnumerable<ContextMenuItem> GetItems<TContext>(TContext context);
}
```

### 5.2 Context Types per Panel

| Panel | Context type passed |
|---|---|
| `TopicExplorerPanel` | `TopicMetadata` |
| `SamplesPanel` | `SampleData` |
| `InstancesPanel` | `InstanceData` |
| `DetailPanel` (field node) | `FieldContextArgs` (type + value + field path) |

### 5.3 Panel Integration

Each panel already has a right-click handler (e.g., `OpenRowContextMenu`). The pattern is:

1. Build default `List<ContextMenuItem>`.
2. Query `IContextMenuRegistry.GetItems(context)`.
3. If any plugin items exist, append a separator and the plugin items.
4. Pass combined list to `ContextMenuService.Show(...)`.

### 5.4 `ContextMenuItem` Record

The existing `ContextMenuItem` record (used throughout the app) is used unchanged. Plugin authors receive the same type.

---

## 6. Phase 3 — Detail View Hijacking (`ISampleViewRegistry`)

### 6.1 Interface

```csharp
public interface ISampleViewRegistry
{
    /// <summary>
    /// Registers a Blazor RenderFragment that fully replaces the default tree view
    /// in the Detail Panel for samples whose payload type matches <paramref name="type"/>.
    /// </summary>
    void Register(Type type, RenderFragment<SampleData> viewer);

    /// <summary>
    /// Returns the custom viewer for the given payload type, or null to fall back to
    /// the default tree.
    /// </summary>
    RenderFragment<SampleData>? GetViewer(Type type);
}
```

### 6.2 `DetailPanel.razor` Modification

The `RenderTreeView()` method is updated to check the registry before rendering the default tree:

```razor
private RenderFragment RenderTreeView() => @<text>
    @if (_currentSample != null)
    {
        var viewer = SampleViewRegistry?.GetViewer(_currentSample.TopicMetadata.TopicType);
        if (viewer != null)
        {
            @viewer(_currentSample)
        }
        else
        {
            <div class="detail-tree">
                @RenderNode(_currentSample.Payload, ...)
            </div>
        }
    }
</text>;
```

The `SampleViewRegistry` is injected via `[Inject]`. When the feature is absent (older host), `SampleViewRegistry` will be null — the fallback path executes.

---

## 7. Phase 4 — Workspace Settings Integration

### 7.1 Event Records

Two new event records are added to `EventBrokerEvents.cs`:

```csharp
/// <summary>
/// Published by the host just before workspace.json is serialized.
/// Plugins add their settings to PluginSettings before returning.
/// </summary>
public sealed record WorkspaceSavingEvent(Dictionary<string, object> PluginSettings);

/// <summary>
/// Published by the host immediately after workspace.json is deserialized.
/// PluginSettings contains the raw plugin sections from the file.
/// </summary>
public sealed record WorkspaceLoadedEvent(IReadOnlyDictionary<string, object> PluginSettings);
```

### 7.2 `WorkspaceDocument` Extension

The `WorkspaceDocument` class is extended with a `PluginSettings` dictionary:

```csharp
public sealed class WorkspaceDocument
{
    public List<PanelState> Panels { get; set; } = new();
    public List<string>? ExcludedTopics { get; set; }

    /// <summary>
    /// Free-form settings contributed by plugins via WorkspaceSavingEvent.
    /// Keyed by plugin name (arbitrary strings).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? PluginSettings { get; set; }
}
```

### 7.3 `WindowManager` Save/Load Integration

- `SaveWorkspaceToJson()`: After building the `WorkspaceDocument`, create a `Dictionary<string, object>`, publish `WorkspaceSavingEvent`, then assign the result to `document.PluginSettings`.
- `LoadWorkspaceFromJson()`: After deserializing, populate an `IReadOnlyDictionary<string, object>` from `document.PluginSettings`, then publish `WorkspaceLoadedEvent`.

### 7.4 Plugin Usage Pattern

```csharp
// In plugin's ConfigureServices or Initialize:
broker.Subscribe<WorkspaceSavingEvent>(evt =>
{
    evt.PluginSettings["MyPlugin"] = new Dictionary<string, object>
    {
        ["Setting1"] = value1,
        ["Setting2"] = value2
    };
});

broker.Subscribe<WorkspaceLoadedEvent>(evt =>
{
    if (evt.PluginSettings.TryGetValue("MyPlugin", out var raw) &&
        raw is JsonElement je)   // System.Text.Json deserializes to JsonElement
    {
        // unpack settings
    }
});
```

---

## 8. Phase 5 — Plugin Manager UI

### 8.1 Two-Phase Loading

The `PluginLoader` is updated to separate **discovery** from **activation**:

1. **Discovery:** instantiate every `IMonitorPlugin` found (to read `Name` and `Version` only). Record the result in a `DiscoveredPlugin` DTO.
2. **Activation:** call `ConfigureServices` + later `Initialize` only for plugins whose `Name` appears in the persisted `enabled-plugins.json`.

```csharp
public sealed class DiscoveredPlugin
{
    public IMonitorPlugin Instance   { get; init; } = null!;
    public string          AssemblyPath { get; init; } = string.Empty;
    public bool            IsEnabled    { get; set;  }
}
```

### 8.2 `PluginConfigService`

Persists the enabled-plugin name set to `%AppData%\DdsMonitor\enabled-plugins.json`.

```csharp
public sealed class PluginConfigService
{
    public HashSet<string> EnabledPlugins { get; private set; }
    public void Save(HashSet<string> enabledPlugins);
    private void Load();
}
```

### 8.3 `PluginManagerPanel.razor`

A new Blazor panel (styled after `TopicSourcesPanel`) with:
- A table listing all `DiscoveredPlugin` entries (Name, Version, Path, Enable checkbox).
- A "Restart Required" badge that appears after the user changes any checkbox.
- Saves the new enabled-plugin set to `PluginConfigService` on every toggle.
- Accessible via **Application menu → Tools → Plugin Manager…**

---

## 9. Phase 6 — Advanced Extension Points

### 9.1 `IValueFormatterRegistry` Exposed

`IValueFormatterRegistry` is already registered in the host DI container. Plugins can retrieve it via `GetFeature<IValueFormatterRegistry>()` and call `Register()` without any code changes to the host.

**Action:** Document this as a supported extension point; add it to `MonitorContext`'s service provider registration confirmation.

### 9.2 `ITypeDrawerRegistry` Exposed

Same approach as `IValueFormatterRegistry`. Already in DI; exposed via `GetFeature<ITypeDrawerRegistry>()`.

### 9.3 Programmatic Topic Coloring

`TopicColorService` gains a plugin extension method:

```csharp
// New in TopicColorService:
private readonly List<Func<string, string?>> _programmaticRules = new();

public void RegisterColorRule(Func<string, string?> rule);

// GetEffectiveColor now checks programmatic rules after user overrides,
// before falling back to the auto-palette.
```

Plugins retrieve `TopicColorService` via `GetFeature<TopicColorService>()` and call `RegisterColorRule`.

### 9.4 Custom Export Formats

`IExportService` remains read-only for plugins (streaming). A new `IExportFormatRegistry` is introduced:

```csharp
public interface IExportFormatRegistry
{
    void RegisterFormat(string label, Func<IReadOnlyList<SampleData>, string, CancellationToken, Task> exportFunc);
    IReadOnlyList<ExportFormatEntry> GetFormats();
}
```

The Samples Panel `Export` button expands to a dropdown listing all registered formats.

### 9.5 Rich Tooltip Providers

```csharp
public interface ITooltipProviderRegistry
{
    void RegisterProvider(Func<Type, object?, string?> htmlProvider);
    string? GetTooltipHtml(Type type, object? value);
}
```

`TooltipPortal.razor` consults the registry before falling back to the default JSON tooltip.

### 9.6 Custom Filter Macros

```csharp
public interface IFilterMacroRegistry
{
    void RegisterMacro(string name, Func<object?[], object?> implementation);
    IReadOnlyDictionary<string, Func<object?[], object?>> GetMacros();
}
```

`FilterCompiler` consults the registry when resolving unknown method names in the LINQ expression.

---

## 10. Phase 7 — Kitchen Sink Demo Plugin

### 10.1 Project Location

`tools/DdsMonitor/DdsMonitor.Plugins.FeatureDemo/`

### 10.2 Scope

The demo plugin exercises every extension point added in this design:

| Extension Point | Demo Action |
|---|---|
| `IMenuRegistry` | Adds "Plugins/Demo" → "Show Dashboard" |
| `PluginPanelRegistry` | Registers `DemoDashboardPanel` |
| `IContextMenuRegistry` | Injects "Log to Console" action on `SampleData` |
| `ISampleViewRegistry` | Replaces tree view for `DemoPayload` type with a custom HTML panel |
| `IValueFormatterRegistry` | Registers `DemoGeoFormatter` for `GeoCoord` type |
| `ITypeDrawerRegistry` | Registers a custom slider input for `int` demo type |
| `TopicColorService` | Colors any topic containing "DEMO" in red |
| `IExportFormatRegistry` | Adds "Export as CSV (Demo)" entry |
| `ITooltipProviderRegistry` | Shows a mock "sensor gauge" tooltip for `DemoPayload` |
| `WorkspaceSavingEvent` | Saves `{"DemoMode": true}` to workspace |
| `WorkspaceLoadedEvent` | Restores demo mode from workspace |

### 10.3 Graceful Degradation

Every `GetFeature<T>()` call is followed by a null check. When the demo plugin is loaded into a hypothetical older host that only provides `IMenuRegistry` and `PluginPanelRegistry`, it registers those two things and skips everything else without exception.

---

## 11. Phase 8 — Autonomous CI Testing

### 11.1 Unit Tests (xUnit)

Project: `tests/DdsMonitor.Plugins.FeatureDemo.Tests/`

- **Plugin registration tests:** instantiate `FeatureDemoPlugin`, mock `IMonitorContext`, call `Initialize`, assert registrations.
- **Event broker tests:** publish `WorkspaceSavingEvent`, assert plugin fills in its settings key.
- **Graceful degradation tests:** pass an `IMonitorContext` that returns `null` for every `GetFeature` call; assert no exception is thrown.
- **Context menu tests:** call `IContextMenuRegistry.GetItems<SampleData>(mockSample)`, assert correct label is returned.

### 11.2 Blazor Component Tests (bUnit)

- Render `PluginManagerPanel` with a mock `PluginLoader` that exposes two discovered plugins (one enabled, one disabled) and assert checkbox state.
- Render the `DemoDashboardPanel` and assert it displays expected demo data.

### 11.3 Headless Integration Test

`DdsMonitor.Engine` already supports `HeadlessMode`. A CI test launches the engine in `HeadlessMode = Record`, loads the demo plugin from the CI plugins directory, generates sample traffic using `FeatureDemo` autonomous runner, and verifies that the demo plugin's background processor received and processed at least one sample.

---

## 12. ECS Plugin Migration

The ECS plugin (`DdsMonitor.Plugins.ECS`) is updated to use the new API in tandem with Phase 1–5.

### 12.1 Changes Required

| Before | After |
|---|---|
| `context.MenuRegistry.AddMenuItem(...)` | `context.GetFeature<IMenuRegistry>()?.AddMenuItem(...)` |
| `context.PanelRegistry.RegisterPanelType(...)` | `context.GetFeature<PluginPanelRegistry>()?.RegisterPanelType(...)` |
| `EcsSettingsPersistenceService` (custom file I/O) | Subscribe to `WorkspaceSavingEvent` / `WorkspaceLoadedEvent` |
| Plugin always loads on startup | User enables it via Plugin Manager |

### 12.2 Settings Migration

The ECS workspace settings section key is `"ECS"`. The first time the new host loads with ECS enabled:
1. No `"ECS"` key in workspace → `WorkspaceLoadedEvent.PluginSettings` has no ECS entry.
2. Plugin falls back to reading the legacy `ecs-settings.json` (one-time migration path).
3. On the next save, settings are written to `workspace.json["PluginSettings"]["ECS"]`.
4. Legacy `ecs-settings.json` is deleted by the plugin after successful migration.
