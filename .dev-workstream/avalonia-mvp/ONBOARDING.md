# Onboarding — DdsMonitor.Avalonia V1

Welcome to the `DdsMonitor.Avalonia` workstream. This document gets you from zero to
building and running the V1 Proof of Concept.

---

## 1. What Is Being Built

`DdsMonitor.Avalonia` is a port of the existing `DdsMonitor.Blazor` tool to a strictly local,
multiplatform desktop application using the **Avalonia UI** framework. It uses the same
`DdsMonitor.Engine` backend but replaces the Blazor rendering layer with a VS Code–style
plugin shell: the shell executable knows nothing about DDS; all features are delivered by
plugins loaded at startup from a `./plugins` folder.

The V1 goal is not full feature parity — it is to prove every high-risk architectural vector
(plugin loading, 5 kHz data virtualization, headless CLI mode, cross-plugin UI injection,
per-panel state persistence) before porting the complete Blazor feature set.

---

## 2. Planning Artifacts

| Document | Purpose |
|----------|---------|
| [DESIGN.md](./DESIGN.md) | Architecture, layer rules, phased plan, open questions |
| [TASK-DETAIL.md](./TASK-DETAIL.md) | Per-task specs: scope, constraints, success conditions |
| [TASK-TRACKER.md](./TASK-TRACKER.md) | Progress checklist with links into TASK-DETAIL |
| [DEBT-TRACKER.md](./DEBT-TRACKER.md) | Known technical debt items |

---

## 3. Folder Layout

```
tools/DdsMonitor/
│
├── DdsMonitor.Engine/                ← Shared domain layer (existing).
│   ├── Plugins/IMonitorPlugin.cs     ← Plugin contract (ConfigureServices + Initialize)
│   ├── Plugins/PluginLoader.cs       ← Scans ./plugins/*.dll with AssemblyLoadContext
│   ├── Plugins/IMenuRegistry.cs      ← Already in Engine; reused by Avalonia shell
│   ├── Plugins/IContextMenuRegistry.cs ← Already in Engine; reused by plugins
│   ├── Plugins/ISampleViewRegistry.cs  ← Purified in Phase 0
│   ├── Ui/ITypeDrawerRegistry.cs     ← Purified in Phase 0
│   ├── Ui/DrawerContext.cs           ← Purified in Phase 0 (Blazor Receiver removed)
│   ├── ISampleStore.cs, ISampleView.cs ← High-perf data pipeline
│   ├── IDdsBridge.cs                 ← DDS participant + reader/writer management
│   ├── IEventBroker.cs, EventBrokerEvents.cs ← Pub/sub + standard event records
│   ├── PanelState.cs                 ← Panel geometry + ComponentState dict
│   ├── HeadlessRunnerService.cs      ← Headless Record/Replay logic
│   └── AppSettings.cs, DdsSettings.cs ← CLI-bindable configuration
│
├── DdsMonitor.Blazor/                ← Existing Blazor app (untouched, ships in parallel)
│
├── DdsMonitor.Avalonia.Core/         ← NEW: Avalonia-specific shared contracts (Phase 1)
│   ├── IToolbarRegistry.cs           ← Toolbar plugin extension point
│   ├── IUserSettings.cs              ← Global per-user preferences (settings.json)
│   ├── IStatefulViewModel.cs         ← Per-panel state save/restore contract
│   ├── IAvaloniaViewRegistry.cs      ← ViewModel → Avalonia Control resolution
│   ├── IAvaloniaTypeDrawerRegistry.cs ← Two-way data authoring controls
│   ├── AvaloniaDrawerContext.cs      ← Purified DrawerContext for Avalonia
│   └── IEventBrokerExtensions.cs    ← SubscribeOnUiThread<T> helper
│
├── DdsMonitor.Avalonia/              ← NEW: Shell executable (Phase 1)
│   ├── Program.cs                    ← Generic Host setup + dual-boot decision
│   ├── App.axaml / App.axaml.cs     ← Avalonia App wiring IServiceProvider
│   ├── ShellWindow.axaml             ← Top menu + toolbar; no panel region
│   ├── AvaloniaWindowManager.cs     ← IWindowManager → floating Avalonia Window
│   └── ViewLocator.cs               ← Delegates to IAvaloniaViewRegistry
│
└── DdsMonitor.Avalonia.StandardPlugin/ ← NEW: All V1 plugins (one DLL, 5 plugins)
    ├── WorkspaceManagerPlugin.cs     ← Schema Sources panel + Network Config panel
    ├── TopicExplorerPlugin.cs        ← Topic list window
    ├── DummyDataGeneratorPlugin.cs   ← Background DDS publisher (headless proof)
    ├── SamplesViewerPlugin.cs        ← Firehose grid + detail inspector
    └── SendSamplePlugin.cs           ← Payload authoring + Clone-to-Send
```

### Key existing files to read before touching anything

- `DdsMonitor.Engine/Plugins/IMonitorPlugin.cs` — plugin contract  
- `DdsMonitor.Engine/Plugins/IMonitorContext.cs` — `GetFeature<T>()` access point  
- `DdsMonitor.Engine/Plugins/PluginLoader.cs` — how plugins are discovered  
- `DdsMonitor.Engine/EventBrokerEvents.cs` — all standard event records  
- `DdsMonitor.Engine/PanelState.cs` — workspace persistence model  
- `DdsMonitor.Engine/ISampleView.cs` — high-perf view API  

---

## 4. Build & Run

### Prerequisites

- .NET 8 SDK
- CycloneDDS native library for Windows in `artifacts/native/win-x64/` (already committed)

### Build

```powershell
# Build everything (Engine + Core + Shell + StandardPlugin)
dotnet build tools/DdsMonitor/DdsMonitor.Avalonia/DdsMonitor.Avalonia.csproj -c Debug

# Or build only the Engine (Phase 0 verification)
dotnet build tools/DdsMonitor/DdsMonitor.Engine/DdsMonitor.Engine.csproj -c Debug
```

### Run (interactive)

```powershell
dotnet run --project tools/DdsMonitor/DdsMonitor.Avalonia/DdsMonitor.Avalonia.csproj
```

### Run (headless record)

```powershell
dotnet run --project tools/DdsMonitor/DdsMonitor.Avalonia/DdsMonitor.Avalonia.csproj -- `
    --DdsSettings:HeadlessMode=Record `
    --DdsSettings:HeadlessFilePath=recording.json `
    --GeneratorPlugin:Enabled=true
# Press Ctrl+C to stop
```

### Run (with a schema DLL preloaded)

```powershell
dotnet run --project tools/DdsMonitor/DdsMonitor.Avalonia/DdsMonitor.Avalonia.csproj -- `
    --AppSettings:TopicSources:0=path/to/MyTopics.dll
```

### Run tests

```powershell
dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj
```

### Stage the StandardPlugin into the shell's plugins folder

After a build, copy `DdsMonitor.Avalonia.StandardPlugin.dll` (and its dependencies) to
`tools/DdsMonitor/DdsMonitor.Avalonia/bin/Debug/net8.0/plugins/`.

A convenience script will be added as part of TASK-B003 similar to the existing
`stage bdc plugin` VS Code task.

---

## 5. Developer Workflow

Read `.dev-workstream/guides/DEV-GUIDE.md` to understand the batch-based development
workflow used in this project (batches, reports, reviews, corrective tasks).

### Quick orientation

1. Pick up the next unchecked item in [TASK-TRACKER.md](./TASK-TRACKER.md).
2. Read the full task spec in [TASK-DETAIL.md](./TASK-DETAIL.md) before writing any code.
3. Verify success conditions before marking the task done.
4. Never reference the shell executable from a plugin — plugins reference only Engine + Core.
5. Never publish per-sample DDS data through `IEventBroker` — use `ISampleStore`/`SampleView`.
6. Always subscribe to `IEventBroker` on the UI thread via `SubscribeOnUiThread<TEvent>` in plugin ViewModels.
