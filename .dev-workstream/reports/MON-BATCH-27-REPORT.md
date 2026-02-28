# MON-BATCH-27 REPORT

**Batch:** MON-BATCH-27 – Dynamic Topic Assembly Loading  
**Status:** Complete  
**Build:** ✅ 0 errors, 194/194 tests passing

---

## 1. Assembly Loading Mechanism

### `AssemblyLoadContext` vs `Assembly.LoadFrom`

The existing `TopicDiscoveryService` already used a custom **`CollectiblePluginLoadContext`** (a subclass of `AssemblyLoadContext`) for plugin DLLs, and that pattern is retained and extended:

```csharp
private sealed class CollectiblePluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    // isCollectible: true → load context and its assemblies can be GC'd
    public CollectiblePluginLoadContext(string mainAssemblyPath)
        : base(isCollectible: true) { … }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Route CycloneDDS.Schema back to the host's default context so
        // attribute types are reference-equal and reflection checks work.
        if (assemblyName.Name == "CycloneDDS.Schema")
            return AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
        // Everything else: use the resolver to isolate the plugin
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path == null ? null : LoadFromAssemblyPath(path);
    }
}
```

**Why not `Assembly.LoadFrom`?**

| Concern | `Assembly.LoadFrom` | `AssemblyLoadContext` |
|---|---|---|
| Isolation | No – shares the default context | Yes – full isolation boundary |
| Conflict risk | Type-identity collisions if two DLLs define same type | Each context owns its types |
| Unloadability | Not possible | `isCollectible: true` allows GC collection |
| Dependency probing | Uses GAC / codebase | `AssemblyDependencyResolver` reads the DLL's `.deps.json` |

`CycloneDDS.Schema` is explicitly **re-routed to the default context** so that the `[DdsTopic]` attribute class is the same reference as the one the engine compiled against — without this, `type.GetCustomAttribute<DdsTopicAttribute>()` would return `null` even for valid topic types because the attribute type would be a different CLR object loaded in the plugin's isolated context.

A new public method `DiscoverFromFile(string dllPath) : int` was added to `TopicDiscoveryService`. Unlike the internal batch-discover path (which silently swallows exceptions), this method propagates exceptions so `AssemblySourceService` can surface the error message in the UI badge next to the DLL.

---

## 2. Persistent Config → Backend Manifest Pipeline

### Config file location

`%APPDATA%\DdsMonitor\assembly-sources.json` — a plain JSON array of absolute DLL paths:

```json
[
  "C:\\Users\\dev\\MyApp\\MyApp.Topics.dll",
  "D:\\robots\\DriveSystem\\DriveTopics.dll"
]
```

### Startup flow

```
app start
  └── ServiceCollectionExtensions.AddDdsMonitorServices()
        ├── new TopicDiscoveryService(registry)     (plugin dirs scanned)
        └── new AssemblySourceService(registry, discoveryService)
              ├── reads assembly-sources.json
              └── for each path:
                    before_count = registry.AllTopics.Count
                    discoveryService.DiscoverFromFile(path)   ← AssemblyLoadContext
                    after_count  = registry.AllTopics.Count
                    entry.TopicCount = after_count - before_count
                    _entryTopics[i] = registry.AllTopics[before … after]
```

The delta-snapshot trick (`before_count` / `after_count`) lets `AssemblySourceService` know exactly which `TopicMetadata` objects came from each DLL even though `TopicRegistry` is a global append-only store.

### Runtime mutation (Add / Remove)

When the user adds a new DLL via the `TopicSourcesPanel`:

1. `AssemblySourceService.Add(path)` is called.
2. The assembly is scanned immediately; newly registered topics are recorded for that entry.
3. The path list is persisted back to `assembly-sources.json` atomically (write-all-text).
4. `Changed` event is raised → `TopicSourcesPanel` re-renders.

**Remove**: removes the entry from the in-memory list and re-persists. Because `ITopicRegistry` is append-only (by design — readers might already hold references), removed DLL's topics remain accessible for the lifetime of the process but won't appear-in the source detail view and won't be reloaded on next start.

---

## 3. Changes Made

### New files

| File | Purpose |
|---|---|
| `DdsMonitor.Engine/DevelSettings.cs` | Runtime-mutable `SelfSendEnabled` bool + `Changed` event |
| `DdsMonitor.Engine/AssemblyScanner/AssemblySourceEntry.cs` | Data model: path + topic count + load error |
| `DdsMonitor.Engine/AssemblyScanner/IAssemblySourceService.cs` | Service interface |
| `DdsMonitor.Engine/AssemblyScanner/AssemblySourceService.cs` | Full implementation: persist/load/scan |
| `DdsMonitor/Components/TopicSourcesPanel.razor` | Master-detail panel for assembly management |

### Modified files

| File | Change |
|---|---|
| `SelfSendService.cs` | Replaced one-shot early-exit with polling loop driven by `DevelSettings.SelfSendEnabled`; writers created/disposed lazily on toggle transitions. Self-send topics registered on first enable (lazy). |
| `TopicDiscoveryService.cs` | Added public `DiscoverFromFile(string) : int`; split into `LoadAndScanAssembly` helper that returns count and propagates exceptions. |
| `Hosting/ServiceCollectionExtensions.cs` | Registers `DevelSettings`; makes `TopicDiscoveryService` a DI singleton; registers `IAssemblySourceService`; unconditionally registers `SelfSendService`. |
| `Components/_Imports.razor` | Added `@using DdsMonitor.Engine.AssemblyScanner` |
| `Components/Layout/MainLayout.razor` | File → "Topic Sources…" menu item; new **Devel** dropdown with "Enable Self-Sending" toggle (checkmark prefix when active). |
| `Components/TopicExplorerPanel.razor` | Topic count label + gear icon next to Subscribe All; zero-topics warning banner with deep-link to Topic Sources. |
| `wwwroot/app.css` | Styles for all new UI elements listed above. |

---

## 4. Success-Criteria Verification

- [x] **Fake sending dormant by default** – `DevelSettings.SelfSendEnabled` defaults to `false`; `SelfSendService` idles in a 500 ms polling loop until toggled on.
- [x] **Devel menu** – "Devel" item in the top menu bar; "Enable Self-Sending" toggle with live checkmark.
- [x] **File dialog for DLL pick** – `TopicSourcesPanel` opens the existing `FileDialog` component with `Filter="*.dll"`.
- [x] **Persist & restore** – `AssemblySourceService` reads/writes `assembly-sources.json` on every mutation; DLLs are re-scanned automatically on startup.
- [x] **TopicSourcesPanel master-detail** – Assembly list (top) with topic count badge; topic table (bottom) scoped to selected assembly.
- [x] **Add / Remove / Move Up / Move Down** – All four operations implemented and persisted.
- [x] **Topic count + gear icon in Topics panel** – Count displayed next to Subscribe All; gear opens TopicSourcesPanel.
- [x] **Zero-topics warning** – Pink warning banner shown in Topics panel when `TopicRegistry.AllTopics.Count == 0`, with "Open Topic Sources" shortcut button.
