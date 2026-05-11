# PLA1-BATCH-04 Report

**Batch Number:** PLA1-BATCH-04  
**Date Submitted:** 2026-03-26

---

## ✅ Completion Status

### Tasks Completed
- [x] PLA1-DEBT-006: `ContextMenuComposer.Compose` adopted in all three panels
- [x] PLA1-DEBT-007: Deterministic interface ordering in `SampleViewRegistry` + unit test
- [x] PLA1-P4-T01: `WorkspaceSavingEvent` / `WorkspaceLoadedEvent` records
- [x] PLA1-P4-T02: `WorkspaceDocument.PluginSettings` + `WorkspaceDocumentTests`
- [x] PLA1-P4-T03: `WindowManager` save/load event integration + `WindowManagerPersistenceTests`
- [x] PLA1-P4-T04: ECS workspace migration + `EcsSettingsPersistenceServiceTests`

**Overall Status:** COMPLETE

---

## 🧪 Test Results

### DdsMonitor.Engine.Tests
```
Passed!  - Failed: 0, Passed: 585, Skipped: 0, Total: 585, Duration: 4 s
(Previous: 577 — 8 new tests added)
```

### DdsMonitor.Plugins.ECS.Tests
```
Passed!  - Failed: 0, Passed: 48, Skipped: 0, Total: 48, Duration: 77 ms
(Previous: 44 — 4 new tests added)
```

---

## 📝 Implementation Summary

### Files Modified
```
tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicExplorerPanel.razor
  - Replaced inline context menu build + GetItems with ContextMenuComposer.Compose

tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor
  - Replaced inline context menu build + GetItems with ContextMenuComposer.Compose

tools/DdsMonitor/DdsMonitor.Blazor/Components/InstancesPanel.razor
  - Replaced inline context menu build + GetItems with ContextMenuComposer.Compose

tools/DdsMonitor/DdsMonitor.Engine/Plugins/SampleViewRegistry.cs
  - Added System.Linq import
  - Updated class XML doc noting interface resolution rule
  - Changed GetInterfaces() loop to OrderBy(FullName) for deterministic ordering

tools/DdsMonitor/DdsMonitor.Engine/EventBrokerEvents.cs
  - Added WorkspaceSavingEvent(Dictionary<string, object> PluginSettings) record
  - Added WorkspaceLoadedEvent(IReadOnlyDictionary<string, object> PluginSettings) record

tools/DdsMonitor/DdsMonitor.Engine/WorkspaceDocument.cs
  - Added Dictionary<string, object>? PluginSettings with [JsonIgnore(WhenWritingNull)]

tools/DdsMonitor/DdsMonitor.Engine/WindowManager.cs
  - Added optional IEventBroker? constructor parameter (backward compatible)
  - SaveWorkspaceToJson: publishes WorkspaceSavingEvent; writes non-empty bag to doc.PluginSettings
  - LoadWorkspaceFromJson: captures workspaceDoc.PluginSettings; publishes WorkspaceLoadedEvent

tools/DdsMonitor/DdsMonitor.Plugins.ECS/EcsSettingsPersistenceService.cs
  - Replaced file-based persistence with IEventBroker event subscriptions
  - Subscribes to WorkspaceSavingEvent → writes EcsSettingsDto under key "ECS"
  - Subscribes to WorkspaceLoadedEvent → restores from "ECS" or migrates from ecs-settings.json
  - Legacy ecs-settings.json deleted after successful migration
  - Removed DebouncedAction, bdc-settings.json file path, SettingsChanged hook

docs/plugin-api/PLA1-DEBT-TRACKER.md
  - Marked PLA1-DEBT-006 and PLA1-DEBT-007 as Resolved (PLA1-BATCH-04)
```

### Files Added
```
tests/DdsMonitor.Engine.Tests/WorkspaceDocumentTests.cs
  - Serialize_OmitsPluginSettings_WhenNull
  - Serialize_IncludesPluginSettings_WhenPopulated
  - Deserialize_OldFormat_DoesNotThrow

tests/DdsMonitor.Engine.Tests/WindowManagerPersistenceTests.cs
  - Save_PublishesWorkspaceSavingEvent
  - Save_IncludesPluginDataInJson
  - Load_PublishesWorkspaceLoadedEvent
  - Load_WithNoPluginSettings_PublishesEmptyDictionary

tests/DdsMonitor.Engine.Tests/Plugins/SampleViewRegistryTests.cs (new test added)
  - GetViewer_WithMultipleMatchingInterfaces_ReturnsAlphabeticallyFirst

tests/DdsMonitor.Plugins.ECS.Tests/EcsSettingsPersistenceServiceTests.cs
  - OnWorkspaceSaving_WritesEcsSectionToPluginBag
  - OnWorkspaceLoaded_RestoresSettingsFromPluginBag
  - OnWorkspaceLoaded_WithEmptyBag_DoesNotThrow
  - SaveLoadRoundTrip_ViaEventBroker
```

---

## 🎯 Implementation Details

### PLA1-DEBT-006: ContextMenuComposer adoption

All three panels (`TopicExplorerPanel`, `SamplesPanel`, `InstancesPanel`) now call
`ContextMenuComposer.Compose(defaults, ContextMenuRegistry, context)` instead of the
duplicated inline pattern. The separator and plugin-items merge logic is now centralised
in the helper, matching the contract tested in `ContextMenuComposerTests`.

### PLA1-DEBT-007: Deterministic interface ordering

`SampleViewRegistry.GetViewer` now calls `type.GetInterfaces().OrderBy(i => i.FullName, StringComparer.Ordinal)`
before iterating, ensuring a stable winner when multiple registered interfaces match.
Class XML doc was updated to document the rule. A new test
`GetViewer_WithMultipleMatchingInterfaces_ReturnsAlphabeticallyFirst` registers `IZzz`
before `IAaa` (opposite registration order) and verifies `IAaa` wins because its
`FullName` sorts first.

### PLA1-P4-T01: Event records

Two records added to `EventBrokerEvents.cs`:
- `WorkspaceSavingEvent(Dictionary<string, object> PluginSettings)` — mutable bag passed to subscribers before serialisation.
- `WorkspaceLoadedEvent(IReadOnlyDictionary<string, object> PluginSettings)` — read-only view passed after deserialisation.

### PLA1-P4-T02: WorkspaceDocument.PluginSettings

Added `Dictionary<string, object>? PluginSettings` with `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`, matching the existing `ExcludedTopics` pattern.

### PLA1-P4-T03: WindowManager integration

`WindowManager` accepts an optional `IEventBroker? eventBroker = null` constructor parameter so `new WindowManager()` in existing tests continues to compile.  When the broker is provided:
- **Save path:** creates an empty `pluginBag`, publishes `WorkspaceSavingEvent(pluginBag)`, assigns to `doc.PluginSettings` if non-empty.
- **Load path:** captures `workspaceDoc.PluginSettings` as `pluginSettingsRaw`; after panel state is restored, publishes `WorkspaceLoadedEvent` with the dict (or empty dict if none).

### PLA1-P4-T04: ECS workspace migration

`EcsSettingsPersistenceService` was rewritten:
- Constructor now takes `(EcsSettings, IEventBroker)` — DI injects both automatically since `IEventBroker` is a singleton in the host container.
- `StartAsync` subscribes to both events; `Dispose` releases subscriptions.
- `OnWorkspaceSaving` serialises current settings into `e.PluginSettings["ECS"]`.
- `OnWorkspaceLoaded` reads from `"ECS"` key; if absent, attempts to read and delete legacy `ecs-settings.json`.
- All file I/O, `DebouncedAction`, and `SettingsChanged` hookup removed.

---

## Commit Message

```
feat(ddsmon): workspace plugin settings, composer adoption, deterministic interface ordering (PLA1-BATCH-04)

Completes PLA1-DEBT-006, PLA1-DEBT-007, PLA1-P4-T01, PLA1-P4-T02, PLA1-P4-T03, PLA1-P4-T04

- ContextMenuComposer.Compose adopted in TopicExplorerPanel, SamplesPanel, InstancesPanel
- SampleViewRegistry: interfaces sorted by FullName (deterministic; test added)
- WorkspaceSavingEvent / WorkspaceLoadedEvent records in EventBrokerEvents
- WorkspaceDocument.PluginSettings (nullable, JsonIgnore when null)
- WindowManager: optional IEventBroker; publishes events on save/load
- EcsSettingsPersistenceService: event-driven (no more bdc-settings.json); migrates from ecs-settings.json on first load

Tests: DdsMonitor.Engine.Tests 585 passed (+8); DdsMonitor.Plugins.ECS.Tests 48 passed (+4)

Related: docs/plugin-api/PLA1-TASK-DETAIL.md Phase 4, PLA1-DESIGN.md §7
```
