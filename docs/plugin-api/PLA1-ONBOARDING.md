# DdsMonitor Plugin API — Developer Onboarding

**Project:** DdsMonitor Plugin API Enhancements  
**Prefix:** PLA1

---

## What Are We Building?

The DdsMonitor application currently loads plugins but only gives them two levers: add a menu item, or open a floating window.  
This workstream upgrades the plugin API into a full extensibility platform.

### Headline features being added:

| Feature | What it enables |
|---|---|
| Capability-Querying context | Plugins ask "does this host support X?" at runtime and degrade gracefully if not |
| Context Menu Registry | Plugins inject custom right-click actions into Topic Explorer, Samples Panel, Instances Panel |
| Detail View Hijacking | A plugin can replace the entire tree-view tab in the Detail Panel with domain-specific UI for a specific payload type |
| Workspace Settings Integration | Plugin settings are saved into `workspace.json` via an event-driven, dictionary-based pattern — no custom file I/O |
| Plugin Manager UI | Users explicitly enable/disable plugins from a UI panel; DLLs are no longer loaded unconditionally |
| Advanced extension points | Value formatters, type drawers, topic coloring rules, custom export formats, rich tooltip providers, filter macros |
| Kitchen Sink Demo Plugin | A reference plugin that demonstrates every extension point |
| Autonomous CI Testing | Full test suite: unit, bUnit Blazor component, headless integration |

The ECS plugin is migrated to these new patterns as part of the work.

---

## Key Documents

| Document | Purpose |
|---|---|
| [PLA1-DESIGN.md](PLA1-DESIGN.md) | Complete architecture — read this first |
| [PLA1-TASK-DETAIL.md](PLA1-TASK-DETAIL.md) | Detailed specification for every task (success criteria, test tables) |
| [PLA1-TASK-TRACKER.md](PLA1-TASK-TRACKER.md) | Task list with completion status — update this as you work |

---

## Required Reading

Before writing any code, read the developer workflow guide:

```
.dev-workstream/guides/DEV-GUIDE.md
```

It explains the batch system, how to report your work, and how to ask questions.

---

## Codebase Orientation

### Solution File

```
CycloneDDS.NET.sln          ← full solution
CycloneDDS.NET.Core.slnf    ← core-only solution filter (faster)
```

### Components Involved in This Workstream

```
tools/DdsMonitor/
├── DdsMonitor.Engine/              ← Core engine; plugin API interfaces live here
│   ├── Plugins/                    ← IMonitorPlugin, IMonitorContext, PluginLoader  ← PRIMARY TARGETS
│   ├── Ui/                         ← IValueFormatterRegistry, ITypeDrawerRegistry  ← EXPOSE TO PLUGINS
│   ├── Export/                     ← IExportService                               ← ADD IExportFormatRegistry
│   ├── EventBrokerEvents.cs        ← ADD WorkspaceSavingEvent, WorkspaceLoadedEvent
│   ├── WorkspaceDocument.cs        ← ADD PluginSettings property
│   ├── WindowManager.cs            ← INTEGRATE save/load events
│   └── TopicColorService.cs        ← ADD RegisterColorRule
│
├── DdsMonitor.Blazor/              ← Blazor host application
│   ├── Components/                 ← Razor panels                                 ← MODIFY several panels
│   │   ├── TopicExplorerPanel.razor←  add context menu extension point
│   │   ├── SamplesPanel.razor      ←  add context menu + custom export
│   │   ├── InstancesPanel.razor    ←  add context menu extension point
│   │   ├── DetailPanel.razor       ←  add detail view hijacking
│   │   └── TooltipPortal.razor     ←  add tooltip provider
│   ├── Program.cs                  ← Register new services in DI
│   └── (Components/) NEW: PluginManagerPanel.razor
│
├── DdsMonitor.Plugins.ECS/         ← Existing ECS plugin; migrate to new API
│
└── DdsMonitor.Plugins.FeatureDemo/ ← NEW: Kitchen Sink demo plugin (create from scratch)

tests/
├── DdsMonitor.Engine.Tests/        ← Engine unit tests; ADD tests for new registries
├── DdsMonitor.Plugins.ECS.Tests/   ← Update ECS plugin tests
└── DdsMonitor.Plugins.FeatureDemo.Tests/ ← NEW: Demo plugin tests (create from scratch)
```

### Understanding the Existing Plugin API

Read these files before touching anything:

```
tools/DdsMonitor/DdsMonitor.Engine/Plugins/IMonitorPlugin.cs
tools/DdsMonitor/DdsMonitor.Engine/Plugins/IMonitorContext.cs
tools/DdsMonitor/DdsMonitor.Engine/Plugins/MonitorContext.cs
tools/DdsMonitor/DdsMonitor.Engine/Plugins/PluginLoader.cs
tools/DdsMonitor/DdsMonitor.Plugins.ECS/EcsPlugin.cs
```

### Understanding the existing internal services you will expose

```
tools/DdsMonitor/DdsMonitor.Engine/Ui/IValueFormatterRegistry.cs
tools/DdsMonitor/DdsMonitor.Engine/Ui/ITypeDrawerRegistry.cs
tools/DdsMonitor/DdsMonitor.Engine/IEventBroker.cs
tools/DdsMonitor/DdsMonitor.Engine/EventBrokerEvents.cs
tools/DdsMonitor/DdsMonitor.Engine/WorkspaceDocument.cs
tools/DdsMonitor/DdsMonitor.Engine/WindowManager.cs
tools/DdsMonitor/DdsMonitor.Engine/TopicColorService.cs
tools/DdsMonitor/DdsMonitor.Engine/Export/IExportService.cs
tools/DdsMonitor/DdsMonitor.Engine/IFilterCompiler.cs
```

---

## How to Build

### Prerequisites

- .NET 8 SDK
- Windows (native CycloneDDS binaries are win-x64)

### Build the engine and Blazor host

```powershell
dotnet build tools/DdsMonitor/DdsMonitor.Engine/DdsMonitor.Engine.csproj
dotnet build tools/DdsMonitor/DdsMonitor.Blazor/DdsMonitor.csproj
```

### Build the ECS plugin and stage it

The workspace task `stage bdc plugin` can be adapted; for ECS:

```powershell
dotnet build tools/DdsMonitor/DdsMonitor.Plugins.ECS/DdsMonitor.Plugins.ECS.csproj -c Debug
```

### Run tests

```powershell
dotnet test tests/DdsMonitor.Engine.Tests/
dotnet test tests/DdsMonitor.Plugins.ECS.Tests/
```

Once the Feature Demo plugin and its tests exist:

```powershell
dotnet test tests/DdsMonitor.Plugins.FeatureDemo.Tests/
```

### Run DdsMonitor

```powershell
.\run_ddsmon.bat
```

---

## Implementation Order

Follow the phase order in the task tracker. Phase 1 must be complete before any other phase because all subsequent phases depend on the `GetFeature<T>()` pattern:

1. **Phase 1** — Redesign `IMonitorContext` and migrate ECS plugin
2. **Phase 2** — Context Menu Registry
3. **Phase 3** — Detail View Hijacking
4. **Phase 4** — Workspace Settings Integration (including ECS migration from file I/O)
5. **Phase 5** — Plugin Manager UI
6. **Phase 6** — Advanced extension points (can be done in any sub-order)
7. **Phase 7** — Demo plugin (requires all extension points to be in place)
8. **Phase 8** — CI tests (can be developed in parallel with Phases 2–7)

---

## Key Design Decisions (Why We Did This)

**Why `GetFeature<T>()` instead of more properties on `IMonitorContext`?**  
Adding a new property to an interface breaks binary compatibility: an older plugin compiled against the old interface will throw `MissingMethodException` when loaded. `GetFeature<T>()` returns `null` for unknown features; both old and new plugins co-exist without recompilation.

**Why event-driven settings instead of a settings interface?**  
An `IHasSettings` interface on the plugin requires a shared, versioned DTO. The `IEventBroker` + `Dictionary<string, object>` approach has zero contract requirements and matches the same pattern already used for spawning windows.

**Why two-phase plugin loading?**  
Zero-trust loading posture: a DLL file in the plugins folder should not automatically gain access to the DI container. Discovery (reading name/version) is harmless; activation (calling `ConfigureServices`) is a privilege.
