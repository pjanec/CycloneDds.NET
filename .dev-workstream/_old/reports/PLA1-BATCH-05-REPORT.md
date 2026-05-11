# Batch Report — PLA1-BATCH-05

**Batch Number:** PLA1-BATCH-05  
**Date Submitted:** 2026-03-26  
**Time Spent:** ~2 hours

---

## ✅ Completion Status

### Tasks Completed
- [x] PLA1-DEBT-009: ECS plugin documentation accuracy
- [x] PLA1-P5-T01: `DiscoveredPlugin` DTO + `PluginConfigService`
- [x] PLA1-P5-T02: `PluginLoader` two-phase behaviour

**Overall Status:** COMPLETE

---

## 🧪 Test Results

### Unit Tests
```
Passed! - Failed: 0, Passed: 592, Skipped: 0, Total: 592, Duration: 3 s
```

New PLA1-BATCH-05 tests (7/7 passing):
- `PluginConfigServiceTests`: 3 tests
- `PluginLoaderTests`: 4 tests

---

## 📝 Implementation Summary

### Files Added
```
- tools/DdsMonitor/DdsMonitor.Engine/Plugins/DiscoveredPlugin.cs
    Sealed class holding IMonitorPlugin instance, AssemblyPath, and mutable IsEnabled flag.

- tools/DdsMonitor/DdsMonitor.Engine/Plugins/PluginConfigService.cs
    Persists HashSet<string> of enabled plugin names to %AppData%\DdsMonitor\enabled-plugins.json.
    Load() is called from ctor; missing/corrupt file returns empty set. Save() writes atomically
    via temp-file-then-rename.

- tests/DdsMonitor.Engine.Tests/Plugins/PluginConfigServiceTests.cs
    3 tests: Load_WhenFileAbsent, Load_WhenFileCorrupt, SaveAndLoad_RoundTrips.

- tests/DdsMonitor.Engine.Tests/Plugins/PluginLoaderTests.cs
    4 tests: LoadPlugins_PopulatesDiscoveredPlugins, DisabledPlugin_DoesNotCallConfigureServices,
    EnabledPlugin_CallsConfigureServices, MalformedDll_IsSkipped. Uses Roslyn to compile
    minimal plugin assemblies at test time (same pattern as Batch28Tests).
```

### Files Modified
```
- tools/DdsMonitor/DdsMonitor.Plugins.ECS/EcsPlugin.cs
    PLA1-DEBT-009: Replaced stale comment on EcsSettingsPersistenceService registration.
    Now accurately describes WorkspaceSavingEvent/WorkspaceLoadedEvent as primary persistence
    and ecs-settings.json as legacy migration only.

- tools/DdsMonitor/DdsMonitor.Engine/Plugins/PluginLoader.cs
    Added optional PluginConfigService? parameter to constructor (default null = all enabled).
    Added _discovered List<DiscoveredPlugin> field and IReadOnlyList<DiscoveredPlugin>
    DiscoveredPlugins property. LoadPluginFromFileCore now always creates DiscoveredPlugin 
    entries for all found types; ConfigureServices is only called for enabled plugins.
    _plugins (backing LoadedPlugins) continues to contain only enabled plugins.

- tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs
    Eagerly instantiates PluginConfigService, passes it to PluginLoader, and registers it
    as a singleton in the DI container.

- docs/plugin-api/PLA1-DEBT-TRACKER.md
    Marked PLA1-DEBT-009 as Resolved (PLA1-BATCH-05).
```

---

## 🎯 Implementation Details

### PLA1-DEBT-009 — ECS plugin documentation

The old comment read "EcsSettingsPersistenceService persists EcsSettings to ecs-settings.json", which was inaccurate since at least PLA1-BATCH-02 (when workspace event persistence was introduced). Updated to describe WorkspaceSavingEvent/WorkspaceLoadedEvent as primary storage and ecs-settings.json as legacy-migration-only, consistent with the actual `EcsSettingsPersistenceService` implementation.

### PLA1-P5-T01 — DiscoveredPlugin + PluginConfigService

`DiscoveredPlugin` is a simple sealed class (not a record, to keep `IsEnabled` as a mutable property while keeping the design explicit). `PluginConfigService` uses an internal constructor overload accepting an explicit file path as the test seam, keeping the public API clean.

### PLA1-P5-T02 — PluginLoader two-phase loading

**Key decisions:**

- `PluginConfigService` is optional (`null` = all enabled) to maintain full backward compatibility with the 7 existing `Batch28Tests` calls to `new PluginLoader(settings)`.
- `_discovered` accumulates all found `IMonitorPlugin` instances regardless of enabled state. `_plugins` (backing `LoadedPlugins`) remains "enabled and service-configured plugins only", preserving the contract for `InitializePlugins` and existing tests.
- `PluginConfigService` is created eagerly in `ServiceCollectionExtensions` (before `LoadPlugins`) because `LoadPlugins` runs against a still-open `IServiceCollection` — the DI container is not built yet and cannot be used for injection at that point.

---

## 🚩 Deferred

- PLA1-P5-T03 (`PluginManagerPanel.razor`) and PLA1-P5-T04 (menu wiring) deferred to PLA1-BATCH-06 per instructions.
- PLA1-DEBT-008 (bUnit panel test) deferred to PLA1-BATCH-06 or PLA1-P8, unchanged.
