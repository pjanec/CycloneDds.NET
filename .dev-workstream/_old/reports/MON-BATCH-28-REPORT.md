# Batch Report — MON-BATCH-28

**Batch Number:** MON-BATCH-28  
**Date Submitted:** 2026-03-21  
**Time Spent:** ~4 hours

---

## ✅ Completion Status

### Tasks Completed
- [x] DMON-041: Plugin contract (`IMonitorPlugin`, `IMonitorContext`) + `PluginLoader` with collectible `AssemblyLoadContext`
- [x] DMON-042: Plugin panel registration — `IWindowManager.RegisteredPanelTypes`, `PluginPanelRegistry`, Plugin Panels menu
- [x] DMON-043: Plugin menu registration — `IMenuRegistry`, `MenuRegistry`, dynamic Plugins menu in top nav

**Overall Status:** COMPLETE

---

## 🧪 Test Results

### Unit Tests — DdsMonitor.Engine.Tests

```
Passed!  - Failed:     0, Passed:   503, Skipped:     0, Total:   503, Duration: 3 s
```

New tests added in `Batch28Tests.cs`: **18 tests** (all passing)

| Test | Area | Status |
|------|------|--------|
| `MenuRegistry_AddItem_Sync_AppearsAtTopLevel` | DMON-043 | ✅ |
| `MenuRegistry_AddItem_Async_AppearsAtTopLevel` | DMON-043 | ✅ |
| `MenuRegistry_AddItem_NestedPath_CreatesCorrectHierarchy` | DMON-043 | ✅ |
| `MenuRegistry_AddItems_SamePath_SharedBranchNode` | DMON-043 | ✅ |
| `MenuRegistry_SyncCallback_IsInvokedWhenCalled` | DMON-043 | ✅ |
| `MenuRegistry_AsyncCallback_IsInvokedWhenCalled` | DMON-043 | ✅ |
| `MenuRegistry_Changed_FiredOnAdd` | DMON-043 | ✅ |
| `WindowManager_RegisterPanelType_AppearsInRegisteredPanelTypes` | DMON-042 | ✅ |
| `WindowManager_RegisterMultiplePanelTypes_AllAppearInRegisteredPanelTypes` | DMON-042 | ✅ |
| `WindowManager_SpawnRegisteredPluginPanel_CreatesPanel` | DMON-042 | ✅ |
| `WindowManager_RegisteredPanelTypes_IsSnapshot_NotLiveView` | DMON-042 | ✅ |
| `PluginPanelRegistry_RegisterPanelType_AppearsInRegisteredTypes` | DMON-041 | ✅ |
| `PluginLoader_BadDll_SkipsGracefully_NoException` | DMON-041 | ✅ |
| `PluginLoader_EmptyDirectory_LoadsNothingGracefully` | DMON-041 | ✅ |
| `PluginLoader_MissingDirectory_SkipsGracefully` | DMON-041 | ✅ |
| `PluginLoader_ValidPlugin_ConfigureServicesCalled` | DMON-041 | ✅ |
| `PluginLoader_ValidPlugin_InitializeReceivesContext` | DMON-041 | ✅ |
| `PluginLoader_AssemblyLoadContext_TypeIdentityPreserved` | DMON-041 | ✅ |

### Regression Check — Core Solution Filter

```
CycloneDDS.Schema.Tests:       12/12 passing
CycloneDDS.IdlImporter.Tests:  28/28 passing
CycloneDDS.Runtime.Tests:     146/147 (1 pre-existing skip)
CycloneDDS.CodeGen.Tests:     197/197 passing
FeatureDemo.Tests:             20/20 passing (individually)
```

Note: `FeatureDemo.Tests` shows 1 flaky DDS-network-interference failure when run in parallel with other test assemblies using live CycloneDDS. This failure is pre-existing and not related to this batch — running the suite in isolation produces 20/20.

---

## 📝 Implementation Summary

### Files Added
```
tools/DdsMonitor/DdsMonitor.Engine/Plugins/IMonitorPlugin.cs     - Plugin contract interface
tools/DdsMonitor/DdsMonitor.Engine/Plugins/IMonitorContext.cs    - Context interface for Initialize()
tools/DdsMonitor/DdsMonitor.Engine/Plugins/IMenuRegistry.cs      - Menu registration interface
tools/DdsMonitor/DdsMonitor.Engine/Plugins/MenuNode.cs           - Hierarchical tree node model
tools/DdsMonitor/DdsMonitor.Engine/Plugins/MenuRegistry.cs       - Thread-safe IMenuRegistry impl
tools/DdsMonitor/DdsMonitor.Engine/Plugins/MonitorContext.cs     - IMonitorContext default impl
tools/DdsMonitor/DdsMonitor.Engine/Plugins/PluginLoader.cs       - ALC-based plugin loader
tools/DdsMonitor/DdsMonitor.Engine/Plugins/PluginPanelRegistry.cs - Singleton panel type registry
tests/DdsMonitor.Engine.Tests/Batch28Tests.cs                    - 18 unit/integration tests
```

### Files Modified
```
tools/DdsMonitor/DdsMonitor.Engine/IWindowManager.cs             - Added RegisteredPanelTypes property
tools/DdsMonitor/DdsMonitor.Engine/WindowManager.cs              - Implemented RegisteredPanelTypes (thread-safe snapshot)
tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs - Register MenuRegistry, PluginPanelRegistry, PluginLoader, IMonitorContext
tools/DdsMonitor/DdsMonitor.Blazor/Program.cs                    - Call InitializePlugins() after StartAsync()
tools/DdsMonitor/DdsMonitor.Blazor/Components/Layout/MainLayout.razor - Added Plugins menu with panel spawning + dynamic menu nodes
tools/DdsMonitor/DdsMonitor.Blazor/wwwroot/app.css               - Added .app-menu__submenu-item / .app-menu__submenu CSS for nested menus
```

---

## 🎯 Implementation Details

### DMON-041: Plugin Contract & Loading Infrastructure

**Approach:**

Followed the exact same `AssemblyLoadContext` isolation pattern used in `TopicDiscoveryService` (Batch 27). Each plugin DLL is loaded into its own collectible `PluginAssemblyLoadContext`. A static `SharedAssemblyNames` set in `PluginLoader` delegates critical assemblies (`DdsMonitor.Engine`, `CycloneDDS.*`, `Microsoft.Extensions.DependencyInjection.*`) to the Default context. Without this delegation, `typeof(IMonitorPlugin).IsAssignableFrom(pluginType)` would return `false` because two different instances of `DdsMonitor.Engine.dll` would be loaded.

**Lifecycle integration:**
- `PluginLoader` is constructed eagerly in `ServiceCollectionExtensions` (before `BuildServiceProvider()`) so `LoadPlugins(services)` can call each plugin's `ConfigureServices` while the DI registration window is still open.
- `InitializePlugins(context)` is called in `Program.cs` after `app.StartAsync()` — giving plugins access to fully-running services.

**`IMonitorContext` scope decision:**  
The design's `IMonitorContext` includes `IWindowManager`, which is Scoped (per Blazor circuit). Calling it during singleton-level initialization would require a scope factory, adding complexity. Instead, `IMonitorContext` was simplified to expose only the two registries plugins actually need at init time: `IMenuRegistry` (Singleton) and `PluginPanelRegistry` (Singleton). Panel types registered via `PluginPanelRegistry` are read by the Scoped `MainLayout` each render cycle without coupling lifetimes. See Deviations section.

**Key Decisions:**
- `PluginLoader` has no DI logger dependency (`ILogger<PluginLoader>? = null`) — keeps test instantiation simple and avoids requiring a full DI container in tests.
- `GetExportedTypesSafe` wraps `Assembly.ExportedTypes` in a try/catch to handle assemblies with partially-loadable types.
- All plugin lifecycle exceptions are caught individually; one bad plugin never prevents others from initialising.

**AssemblyLoadContext type identity test:**  
`PluginLoader_AssemblyLoadContext_TypeIdentityPreserved` compiles a plugin via Roslyn, loads it through `PluginLoader`, and asserts the instance is `IsAssignableFrom<IMonitorPlugin>`. This only succeeds if `DdsMonitor.Engine` is correctly shared from the Default context.

---

### DMON-042: Plugin Panel Registration

**Approach:**

`IWindowManager` already had `RegisterPanelType(string, Type)` implemented in `WindowManager`. The only additions:
1. Added `IReadOnlyDictionary<string, Type> RegisteredPanelTypes { get; }` to the interface.
2. Implemented it in `WindowManager` as a thread-safe snapshot of `_panelTypes`.

For the UI, a Singleton `PluginPanelRegistry` stores plugin-registered panel types independently of the Scoped `IWindowManager`. `MainLayout.razor` injects `PluginPanelRegistry` and renders a Plugin Panels section in the Plugins dropdown. When the user clicks a plugin panel entry, `SpawnPluginPanel` first calls `WindowManager.RegisterPanelType(name, type)` on the current scoped `IWindowManager` (bridging the Singleton registry into the current session), then calls `SpawnPanel`.

This pattern means every `WindowManager` session that spawns a plugin panel has the type registered in its own `_panelTypes` dict, which the existing `SpawnPanel` lookup (`ResolveComponentTypeName`) uses.

**Why `WindowManager.RegisteredPanelTypes` is still useful:**  
Unit tests (`WindowManager_RegisterPanelType_AddsToAvailable`, `WindowManager_SpawnRegisteredPluginPanel_CreatesPanel`) test the self-contained window manager instance, which already fully works through `_panelTypes`.

---

### DMON-043: Plugin Menu Registration

**Approach:**

`MenuRegistry` builds a hierarchical tree of `MenuNode` objects from slash-delimited `menuPath` strings. All tree mutations are protected by a `lock (_sync)`.

`MenuNode` exposes:
- `IReadOnlyList<MenuNode> Children` — public read-only view
- Internal `GetOrAddChild(label)` — find-or-create a child by label (used by `MenuRegistry`)
- Internal `AddChild(node)` — append a direct child (used by `MenuRegistry` for leaf nodes)

This avoids exposing a mutable list publicly while keeping `MenuRegistry`'s tree-walking code clean.

**UI rendering:** `MainLayout.razor` renders `IMenuRegistry.GetTopLevelMenus()` recursively via `RenderPluginMenuNode()`. Branch nodes get CSS class `app-menu__submenu-item` which shows a `.app-menu__submenu` flyout on hover (pure CSS, no JS). Leaf nodes render as standard `app-menu__dropdown-item` buttons.

**`Changed` event:** Both `MenuRegistry` and `PluginPanelRegistry` fire `Changed` on mutation. `MainLayout` subscribes and calls `InvokeAsync(StateHasChanged)` so the menu re-renders if items are added after first render.

---

## 🚀 Deviations & Improvements

### Deviation 1: `IMonitorContext` does not include `IWindowManager` or `ISampleStore`

**What:** The design spec shows `IMonitorContext` with `IWindowManager`, `ISampleStore`, and `IFormatterRegistry`. This implementation uses only `IMenuRegistry` and `PluginPanelRegistry`.

**Why:**
- `IWindowManager` is Scoped. Exposing it from a Singleton `IMonitorContext` would cause a captive dependency issue. The instructions explicitly note `IFormatterRegistry` is not needed. For panels, `PluginPanelRegistry` (Singleton) is a cleaner alternative — it decouples panel registration from the circuit lifecycle.
- `ISampleStore` is available in the DI container for plugins that register DI services during `ConfigureServices`; they can inject it themselves. Adding it to `IMonitorContext` in this batch adds API surface with no test exercising it.

**Benefit:** Clean lifecycle separation; no Singleton capturing Scoped services.  
**Risk:** If a plugin needs `ISampleStore` at `Initialize` time, `IMonitorContext` would need extending (low risk — plugins can use DI instead).  
**Recommendation:** Keep for now; expand when a concrete use case arises.

### Deviation 2: `PluginPanelRegistry` replaces plugin-side `IWindowManager.RegisterPanelType`

**What:** Instead of plugins calling `context.WindowManager.RegisterPanelType(…)`, they call `context.PanelRegistry.RegisterPanelType(name, type)`.

**Why:** Avoids the Scoped/Singleton lifecycle mismatch. The Singleton `PluginPanelRegistry` is read by `MainLayout.razor` on every render, and types are forwarded to the active `IWindowManager` at spawn time.

**Benefit:** Works correctly regardless of how many Blazor circuits are active.

---

## 🐛 Issues Encountered

### 1. Assembly Type Identity With `AssemblyLoadContext`

When first writing `PluginLoader_AssemblyLoadContext_TypeIdentityPreserved`, `typeof(IMonitorPlugin).IsAssignableFrom(pluginType)` returned `false` in an early prototype. Root cause: `DdsMonitor.Engine` was being resolved again into the plugin ALC instead of delegated to Default. Fixed by adding `"DdsMonitor.Engine"` to `SharedAssemblyNames` in `PluginAssemblyLoadContext.Load()`. This is the same pattern used in `TopicDiscoveryService` for `CycloneDDS.Schema/Runtime/Core` (Batch 27).

### 2. Roslyn Compilation — Missing `System.ComponentModel` Reference

The initial `CompilePlugin` helper enumerated only a few hand-picked `MetadataReference` targets. The first run failed with:
```
error CS0012: The type 'IServiceProvider' is defined in an assembly that is not referenced.
You must add a reference to assembly 'System.ComponentModel, Version=8.0.0.0, ...'
```
Fix: replaced hand-picked references with `AppDomain.CurrentDomain.GetAssemblies()` filtered to non-dynamic loaded assemblies. This mirrors the pattern used in similar Roslyn-based tests (`TopicDiscoveryServiceTests`).

---

## ⚠️ Known Issues / Limitations

1. **No plugin hot-reload** — plugins are loaded once at startup. A live-scan `IHostedService` could be added in a future batch.
2. **No plugin isolation boundary UI** — plugin crashes during `Initialize` are logged but there's no user-visible indication that a plugin failed to load.
3. **Sub-menus are CSS hover-only** — on touch-screen or keyboard nav, the sub-menus won't open. Sufficient for the desktop-targeted app where mouse interaction is the norm.

---

## 📊 Success Criteria Check

| Criterion | Status |
|-----------|--------|
| `IMonitorPlugin`, `IMonitorContext` defined | ✅ |
| `PluginLoader` loads external assemblies via isolated ALC | ✅ |
| Bad DLLs skipped without crashing the host | ✅ |
| `ConfigureServices` called before container builds | ✅ |
| `Initialize` called after host starts with valid context | ✅ |
| Custom panels registered by plugins & launchable by users | ✅ |
| Custom menu items injected into top menu hierarchy | ✅ |
| All 18 new tests passing | ✅ |
| No regressions (503/503 total tests pass) | ✅ |
