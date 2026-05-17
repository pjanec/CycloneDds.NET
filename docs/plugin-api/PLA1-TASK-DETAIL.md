# DdsMonitor Plugin API — Task Details

**Project:** DdsMonitor Plugin API Enhancements  
**Prefix:** PLA1  
**Last Updated:** 2026-03-26

**Design Reference:** [PLA1-DESIGN.md](PLA1-DESIGN.md)  
**Task Tracker:** [PLA1-TASK-TRACKER.md](PLA1-TASK-TRACKER.md)

---

## Overview

This document provides a detailed specification for every implementation task.  
Each task contains:

- **Unique ID** for tracking
- **Scope** — exactly which files to create or modify
- **Dependencies** on other tasks
- **Success Criteria** — verifiable deliverables
- **Unit Test Specifications** — test names and assertions where applicable

---

## Phase 1 — Capability-Querying Context

**Design Reference:** [§4 Phase 1](PLA1-DESIGN.md#4-phase-1--capability-querying-context-future-proof-foundation)

---

### PLA1-P1-T01: Redesign `IMonitorContext` to Capability-Querying

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/IMonitorContext.cs` (MODIFY)

**Dependencies:** None

**Scope:**
- Replace the two hardcoded properties (`MenuRegistry`, `PanelRegistry`) with a single generic method `GetFeature<TFeature>()`.
- Add XML documentation explaining graceful-degradation contract.

**Success Criteria:**
1. `IMonitorContext` contains exactly one method: `TFeature? GetFeature<TFeature>() where TFeature : class`.
2. No other members remain on the interface.
3. The file compiles successfully.

**Unit Test Specifications:**

**Test File:** `tests/DdsMonitor.Engine.Tests/Plugins/IMonitorContextTests.cs`

| Test Name | Assertion | Expected Result |
|---|---|---|
| `GetFeature_ReturnsRegisteredService` | Call `GetFeature<IMenuRegistry>()` on a `MonitorContext` whose `IServiceProvider` has `IMenuRegistry` registered | Non-null `IMenuRegistry` instance returned |
| `GetFeature_ReturnsNull_WhenServiceNotRegistered` | Call `GetFeature<IContextMenuRegistry>()` on a `MonitorContext` whose `IServiceProvider` has no such registration | `null` |
| `GetFeature_WhenCalledTwice_ReturnsSameInstance` | Call `GetFeature<IMenuRegistry>()` twice | Same reference returned (singleton) |

---

### PLA1-P1-T02: Update `MonitorContext` Concrete Implementation

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/MonitorContext.cs` (MODIFY)

**Dependencies:** PLA1-P1-T01

**Scope:**
- Remove `MenuRegistry` and `PanelRegistry` properties.
- Store `IServiceProvider _services` injected via constructor.
- Implement `GetFeature<TFeature>()` by calling `_services.GetService<TFeature>()`.

**Success Criteria:**
1. `MonitorContext` implements the updated `IMonitorContext`.
2. Class compiles with no warnings.
3. `MenuRegistry` and `PanelRegistry` are no longer accessible directly.

**Unit Test Specifications:**

_Covered by PLA1-P1-T01 tests (same test file)._

---

### PLA1-P1-T03: Register Core Registries in Host DI Container

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Program.cs` or the host service registration file (MODIFY)

**Dependencies:** PLA1-P1-T02

**Scope:**
- Ensure `IMenuRegistry` (singleton), `PluginPanelRegistry` (singleton), and `MonitorContext` are registered in the DI container so `GetFeature<T>()` can resolve them.

**Success Criteria:**
1. `IMenuRegistry` is resolvable from `IServiceProvider`.
2. `PluginPanelRegistry` is resolvable from `IServiceProvider`.
3. `IMonitorContext` resolves to `MonitorContext`.
4. Application boots without exception.

---

### PLA1-P1-T04: Migrate ECS Plugin to New API

**File:** `tools/DdsMonitor/DdsMonitor.Plugins.ECS/EcsPlugin.cs` (MODIFY)

**Dependencies:** PLA1-P1-T02

**Scope:**
- Replace `context.MenuRegistry.AddMenuItem(...)` with `context.GetFeature<IMenuRegistry>()?.AddMenuItem(...)`.
- Replace `context.PanelRegistry.RegisterPanelType(...)` with `context.GetFeature<PluginPanelRegistry>()?.RegisterPanelType(...)`.

**Success Criteria:**
1. `EcsPlugin.cs` compiles against the new `IMonitorContext`.
2. ECS plugin registers its panels and menu items on application startup.
3. No `NullReferenceException` thrown at runtime.

**Unit Test Specifications:**

**Test File:** `tests/DdsMonitor.Plugins.ECS.Tests/EcsPluginTests.cs` (MODIFY: update to new API)

| Test Name | Assertion | Expected Result |
|---|---|---|
| `Initialize_RegistersPanelsViaGetFeature` | Call `Initialize` with a context that has `PluginPanelRegistry` registered; check registry contains "ECS Entity Grid", "ECS Entity Detail", "ECS Settings" | All three found |
| `Initialize_RegistersMenuItemsViaGetFeature` | Call `Initialize` with a context that has `IMenuRegistry` registered; check registry tree | "Plugins/ECS" node present |
| `Initialize_DoesNotThrow_WhenContextHasNoFeatures` | Pass a context that returns `null` for all features | No exception thrown |

---

## Phase 2 — Context Menu Registry

**Design Reference:** [§5 Phase 2](PLA1-DESIGN.md#5-phase-2--context-menu-registry)

---

### PLA1-P2-T01: Create `IContextMenuRegistry` Interface

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/IContextMenuRegistry.cs` (NEW)

**Dependencies:** None

**Scope:**
- Define `IContextMenuRegistry` with `RegisterProvider<TContext>` and `GetItems<TContext>`.
- Add XML documentation.

**Success Criteria:**
1. File created and compiles.
2. Interface has exactly two methods.
3. Namespace is `DdsMonitor.Engine.Plugins`.

---

### PLA1-P2-T02: Implement `ContextMenuRegistry`

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/ContextMenuRegistry.cs` (NEW)

**Dependencies:** PLA1-P2-T01

**Scope:**
- Thread-safe internal dictionary keyed by `Type`, mapping to a list of `Delegate` providers.
- `RegisterProvider<TContext>` casts and stores the provider.
- `GetItems<TContext>` invokes all registered providers for `TContext`, collecting results.
- Exceptions from individual providers are caught and logged; they must not crash the panel.

**Success Criteria:**
1. `RegisterProvider<SampleData>` and `GetItems<SampleData>` work end-to-end.
2. Multiple providers can be registered for the same context type; all are invoked.
3. A provider that throws does not prevent other providers from running.

**Unit Test Specifications:**

**Test File:** `tests/DdsMonitor.Engine.Tests/Plugins/ContextMenuRegistryTests.cs`

| Test Name | Assertion | Expected Result |
|---|---|---|
| `GetItems_WhenNoProviders_ReturnsEmpty` | Call `GetItems<SampleData>` with no providers registered | Empty `IEnumerable<ContextMenuItem>` |
| `GetItems_ReturnsSingleProviderItems` | Register one provider yielding two items; call `GetItems` | Exactly two items |
| `GetItems_ReturnsCombinedItemsFromMultipleProviders` | Register two providers yielding 2 and 3 items each; call `GetItems` | Five items total |
| `GetItems_WhenProviderThrows_OtherProvidersStillRun` | Register throwing provider and normal provider; call `GetItems` | Normal provider items returned; no exception propagated |
| `RegisterProvider_IsThreadSafe` | Register providers from multiple threads simultaneously; call `GetItems` | No exception; all registered items present |

---

### PLA1-P2-T03: Register `IContextMenuRegistry` in Host DI

**File:** Host service registration (MODIFY)

**Dependencies:** PLA1-P2-T02

**Scope:**
- Register `ContextMenuRegistry` as singleton implementing `IContextMenuRegistry`.

**Success Criteria:**
1. `GetFeature<IContextMenuRegistry>()` returns a non-null instance.

---

### PLA1-P2-T04: Integrate Context Menus into `TopicExplorerPanel.razor`

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicExplorerPanel.razor` (MODIFY)

**Dependencies:** PLA1-P2-T03

**Scope:**
- Inject `IContextMenuRegistry` (allow null via `[Inject(Optional = true)]` or conditional resolution).
- In the row right-click handler, call `GetItems<TopicMetadata>(topicMeta)`.
- If items returned, append a separator and the items to the context menu list before calling `ContextMenuService.Show`.

**Success Criteria:**
1. Topic Explorer right-click menu shows plugin items when a provider has been registered for `TopicMetadata`.
2. Menu is unchanged when no provider is registered.
3. Separator is only added when there is at least one plugin item.

---

### PLA1-P2-T05: Integrate Context Menus into `SamplesPanel.razor`

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor` (MODIFY)

**Dependencies:** PLA1-P2-T03

**Scope:**
- Same integration pattern as T04, using `SampleData` as the context type.

**Success Criteria:**
1. Samples Panel right-click shows plugin items when a `SampleData` provider is registered.
2. Default items ("Show Detail", "Clone to Send", "Filter Out Topic") are always present first.

---

### PLA1-P2-T06: Integrate Context Menus into `InstancesPanel.razor`

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/InstancesPanel.razor` (MODIFY)

**Dependencies:** PLA1-P2-T03

**Scope:**
- Use `InstanceData` or the appropriate row model as the context type.

**Success Criteria:**
1. Instances Panel right-click shows plugin items when an `InstanceData` provider is registered.

---

## Phase 3 — Detail View Hijacking

**Design Reference:** [§6 Phase 3](PLA1-DESIGN.md#6-phase-3--detail-view-hijacking-isampleviewregistry)

---

### PLA1-P3-T01: Create `ISampleViewRegistry` Interface

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/ISampleViewRegistry.cs` (NEW)

**Dependencies:** None

**Scope:**
- Define `ISampleViewRegistry` with `Register(Type type, RenderFragment<SampleData> viewer)` and `RenderFragment<SampleData>? GetViewer(Type type)`.

**Success Criteria:**
1. File compiles.
2. Correct namespace: `DdsMonitor.Engine.Plugins`.
3. Uses `Microsoft.AspNetCore.Components.RenderFragment<T>` generics correctly.

---

### PLA1-P3-T02: Implement `SampleViewRegistry`

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/SampleViewRegistry.cs` (NEW)

**Dependencies:** PLA1-P3-T01

**Scope:**
- Dictionary mapping `Type` → `RenderFragment<SampleData>`.
- Thread-safe with `lock`.
- `GetViewer` walks the type hierarchy (checks exact type, then base types, then interfaces) to support polymorphic matching.

**Success Criteria:**
1. `Register` and `GetViewer` round-trip correctly for exact type match.
2. `GetViewer` returns `null` for a type that has not been registered.

**Unit Test Specifications:**

**Test File:** `tests/DdsMonitor.Engine.Tests/Plugins/SampleViewRegistryTests.cs`

| Test Name | Assertion | Expected Result |
|---|---|---|
| `GetViewer_ReturnsNull_WhenNothingRegistered` | Call `GetViewer(typeof(object))` | `null` |
| `GetViewer_ReturnsViewer_ForExactType` | Register viewer for `typeof(MyPayload)`; call `GetViewer(typeof(MyPayload))` | Non-null `RenderFragment<SampleData>` |
| `GetViewer_ReturnsNull_ForDifferentType` | Register viewer for `typeof(MyPayload)`; call `GetViewer(typeof(OtherPayload))` | `null` |

---

### PLA1-P3-T03: Register `ISampleViewRegistry` in Host DI

**File:** Host service registration (MODIFY)

**Dependencies:** PLA1-P3-T02

**Scope:**
- Register `SampleViewRegistry` as singleton implementing `ISampleViewRegistry`.

**Success Criteria:**
1. `GetFeature<ISampleViewRegistry>()` returns a non-null instance.

---

### PLA1-P3-T04: Modify `DetailPanel.razor` to Consult Registry

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor` (MODIFY)

**Dependencies:** PLA1-P3-T03

**Scope:**
- Inject `ISampleViewRegistry` (optional injection pattern).
- Modify the first tab's render method to check `GetViewer(currentSample.TopicMetadata.TopicType)` before rendering the default tree.
- If a viewer is returned, render it instead of the tree.

**Success Criteria:**
1. When a viewer is registered for the current sample's type, that viewer's output is rendered in place of the tree view.
2. When no viewer is registered, the default tree renders unchanged.
3. The JSON tab and Sample Info tab are not affected by this change.

---

## Phase 4 — Workspace Settings Integration

**Design Reference:** [§7 Phase 4](PLA1-DESIGN.md#7-phase-4--workspace-settings-integration)

---

### PLA1-P4-T01: Add `WorkspaceSavingEvent` and `WorkspaceLoadedEvent`

**File:** `tools/DdsMonitor/DdsMonitor.Engine/EventBrokerEvents.cs` (MODIFY)

**Dependencies:** None

**Scope:**
- Add `public sealed record WorkspaceSavingEvent(Dictionary<string, object> PluginSettings)`.
- Add `public sealed record WorkspaceLoadedEvent(IReadOnlyDictionary<string, object> PluginSettings)`.

**Success Criteria:**
1. Both records are defined in `DdsMonitor.Engine` namespace.
2. `WorkspaceSavingEvent.PluginSettings` is mutable so subscribers can add entries.
3. `WorkspaceLoadedEvent.PluginSettings` is read-only.

---

### PLA1-P4-T02: Extend `WorkspaceDocument` with `PluginSettings`

**File:** `tools/DdsMonitor/DdsMonitor.Engine/WorkspaceDocument.cs` (MODIFY)

**Dependencies:** None

**Scope:**
- Add `Dictionary<string, object>? PluginSettings` property with `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`.

**Success Criteria:**
1. `WorkspaceDocument` serializes with a `"PluginSettings"` key when any plugin has added data.
2. `WorkspaceDocument` serializes without a `"PluginSettings"` key when the dictionary is null.
3. Existing workspace files without `"PluginSettings"` deserialize correctly with `PluginSettings == null`.

**Unit Test Specifications:**

**Test File:** `tests/DdsMonitor.Engine.Tests/WorkspaceDocumentTests.cs`

| Test Name | Assertion | Expected Result |
|---|---|---|
| `Serialize_OmitsPluginSettings_WhenNull` | Serialize `WorkspaceDocument` with `PluginSettings = null` | JSON does not contain `"PluginSettings"` key |
| `Serialize_IncludesPluginSettings_WhenPopulated` | Set `PluginSettings = new() {["ECS"] = ...}`; serialize | JSON contains `"PluginSettings":{"ECS":...}` |
| `Deserialize_OldFormat_DoesNotThrow` | Deserialize a JSON string without `"PluginSettings"` key | No exception; `PluginSettings == null` |

---

### PLA1-P4-T03: Integrate Save/Load Events into `WindowManager`

**File:** `tools/DdsMonitor/DdsMonitor.Engine/WindowManager.cs` (MODIFY)

**Dependencies:** PLA1-P4-T01, PLA1-P4-T02

**Scope:**
- In the save path: create `Dictionary<string, object> pluginBag`, publish `new WorkspaceSavingEvent(pluginBag)`, assign to `document.PluginSettings` if non-empty.
- In the load path: after deserialization, build `IReadOnlyDictionary<string, object>` from `document.PluginSettings ?? empty`, publish `new WorkspaceLoadedEvent(...)`.

**Success Criteria:**
1. After calling save, a subscriber that added to `WorkspaceSavingEvent.PluginSettings` sees its data persisted in the JSON output.
2. After calling load, a subscriber to `WorkspaceLoadedEvent` receives the plugin settings previously written.

**Unit Test Specifications:**

**Test File:** `tests/DdsMonitor.Engine.Tests/WindowManagerPersistenceTests.cs`

| Test Name | Assertion | Expected Result |
|---|---|---|
| `Save_PublishesWorkspaceSavingEvent` | Subscribe to `WorkspaceSavingEvent`; call save | Event received with mutable `PluginSettings` |
| `Save_IncludesPluginDataInJson` | Subscribe and add `["Test"] = 42`; save | Resulting JSON file contains `"PluginSettings":{"Test":42}` |
| `Load_PublishesWorkspaceLoadedEvent` | Pre-populate JSON with `"PluginSettings":{"Key":"Val"}`; call load | `WorkspaceLoadedEvent` received with `"Key"="Val"` |
| `Load_WithNoPluginSettings_PublishesEmptyDictionary` | Load a JSON file without `"PluginSettings"` | Event received with empty dictionary |

---

### PLA1-P4-T04: Migrate ECS Plugin to Workspace Settings

**File:** `tools/DdsMonitor/DdsMonitor.Plugins.ECS/EcsSettingsPersistenceService.cs` (MODIFY or REMOVE),  
`tools/DdsMonitor/DdsMonitor.Plugins.ECS/EcsPlugin.cs` (MODIFY)

**Dependencies:** PLA1-P4-T03, PLA1-P1-T04

**Scope:**
- Replace `EcsSettingsPersistenceService` file I/O with event subscriptions.
- Subscribe to `WorkspaceSavingEvent` to write ECS settings dict under key `"ECS"`.
- Subscribe to `WorkspaceLoadedEvent` to restore settings.
- Add one-time migration fallback: if workspace has no `"ECS"` section, check for legacy `ecs-settings.json`.
- Delete legacy `ecs-settings.json` after successful migration.

**Success Criteria:**
1. ECS settings survive a save/reload cycle via `workspace.json`.
2. Legacy `ecs-settings.json` is read once and then cleaned up.
3. `EcsSettingsPersistenceService` is removed or reduced to migration-helper only.

---

## Phase 5 — Plugin Manager UI

**Design Reference:** [§8 Phase 5](PLA1-DESIGN.md#8-phase-5--plugin-manager-ui)

---

### PLA1-P5-T01: Create `DiscoveredPlugin` DTO and `PluginConfigService`

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/DiscoveredPlugin.cs` (NEW)  
**File:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/PluginConfigService.cs` (NEW)

**Dependencies:** None

**Scope:**
- `DiscoveredPlugin` record: `Instance`, `AssemblyPath`, `IsEnabled` (mutable bool).
- `PluginConfigService`:
  - Persists `HashSet<string>` of plugin names to `%AppData%\DdsMonitor\enabled-plugins.json`.
  - `Load()` called from constructor; returns empty set if file absent or malformed.
  - `Save(HashSet<string>)` writes atomically (write to temp file, then rename).

**Success Criteria:**
1. `PluginConfigService` reads and writes correctly.
2. Missing or corrupted file causes `Load()` to return empty set, not throw.

**Unit Test Specifications:**

**Test File:** `tests/DdsMonitor.Engine.Tests/Plugins/PluginConfigServiceTests.cs`

| Test Name | Assertion | Expected Result |
|---|---|---|
| `Load_WhenFileAbsent_ReturnsEmptySet` | Point to a non-existent file; call Load | Returns empty `HashSet<string>` |
| `Load_WhenFileCorrupt_ReturnsEmptySet` | Write invalid JSON; call Load | Returns empty `HashSet<string>` (no exception) |
| `SaveAndLoad_RoundTrips` | Save `{"PluginA", "PluginB"}`; load in a new instance | Returns set containing exactly "PluginA" and "PluginB" |

---

### PLA1-P5-T02: Update `PluginLoader` for Two-Phase Loading

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/PluginLoader.cs` (MODIFY)

**Dependencies:** PLA1-P5-T01

**Scope:**
- Add `List<DiscoveredPlugin> _discovered` field; expose as `IReadOnlyList<DiscoveredPlugin> DiscoveredPlugins`.
- In `LoadPlugins`: instantiate every found `IMonitorPlugin`, create a `DiscoveredPlugin`, set `IsEnabled` based on `PluginConfigService.EnabledPlugins`.
- Call `ConfigureServices` only for enabled plugins.
- Update `InitializePlugins` to only iterate enabled (activated) plugins.

**Success Criteria:**
1. All DLLs are scanned; all `IMonitorPlugin` implementations appear in `DiscoveredPlugins`.
2. `ConfigureServices` is called only for enabled plugins.
3. `Initialize` is called only for enabled plugins.

**Unit Test Specifications:**

**Test File:** `tests/DdsMonitor.Engine.Tests/Plugins/PluginLoaderTests.cs`

| Test Name | Assertion | Expected Result |
|---|---|---|
| `LoadPlugins_PopulatesDiscoveredPlugins` | Point at a directory containing a single plugin DLL | `DiscoveredPlugins.Count == 1` |
| `LoadPlugins_DisabledPlugin_DoesNotCallConfigureServices` | Disable the plugin in config; call `LoadPlugins` | Plugin's `ConfigureServices` mock not invoked |
| `LoadPlugins_EnabledPlugin_CallsConfigureServices` | Enable the plugin in config; call `LoadPlugins` | Plugin's `ConfigureServices` mock invoked once |
| `LoadPlugins_MalformedDll_IsSkipped` | Place a non-plugin DLL in the directory | No exception; `DiscoveredPlugins` does not include malformed entry |

---

### PLA1-P5-T03: Create `PluginManagerPanel.razor`

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/PluginManagerPanel.razor` (NEW)

**Dependencies:** PLA1-P5-T02

**Scope:**
- Table listing all `DiscoveredPlugin` entries (Name, Version, Path, Enable checkbox).
- "Restart Required" badge appears after any toggle.
- On checkbox toggle: update `DiscoveredPlugin.IsEnabled`, call `PluginConfigService.Save(...)`.
- Styled consistently with `TopicSourcesPanel` (same CSS BEM classes).

**Success Criteria:**
1. Panel renders with correct plugin list.
2. Toggling a checkbox triggers `PluginConfigService.Save`.
3. "Restart Required" badge appears after toggle and is absent on first load.

**Unit Test Specifications (bUnit):**

**Test File:** `tests/DdsMonitor.Engine.Tests/Components/PluginManagerPanelTests.cs`

| Test Name | Assertion | Expected Result |
|---|---|---|
| `Renders_PluginTable_WithCorrectRowCount` | Mount with 2 discovered plugins | 2 rows rendered |
| `Checkbox_Checked_ForEnabledPlugin` | EnabledPlugin is `IsEnabled = true` | Corresponding checkbox is checked |
| `Checkbox_Unchecked_ForDisabledPlugin` | `IsEnabled = false` | Corresponding checkbox is unchecked |
| `Toggle_ShowsRestartBadge` | Check/uncheck a checkbox | "Restart Required" badge becomes visible |
| `Toggle_SavesConfig` | Uncheck a plugin; verify `PluginConfigService.Save` called | Save called with updated set |

---

### PLA1-P5-T04: Wire Plugin Manager into Application Menu

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/Desktop.razor` or `Layout/MainLayout.razor` (MODIFY)

**Dependencies:** PLA1-P5-T03

**Scope:**
- Add "Plugin Manager…" button in the Tools (or application) menu alongside "Topic Sources…".
- Button click spawns `PluginManagerPanel` via `IWindowManager.SpawnPanel`.

**Success Criteria:**
1. "Plugin Manager…" button is visible in the menu.
2. Clicking opens a floating panel of type `PluginManagerPanel`.
3. Only one instance is spawned at a time (reuse existing if already open).

---

## Phase 6 — Advanced Extension Points

**Design Reference:** [§9 Phase 6](PLA1-DESIGN.md#9-phase-6--advanced-extension-points)

---

### PLA1-P6-T01: Expose `IValueFormatterRegistry` via `GetFeature`

**File:** Host service registration (MODIFY)

**Dependencies:** PLA1-P1-T03

**Scope:**
- Confirm `IValueFormatterRegistry` is registered in the DI container.
- Add integration test confirming `GetFeature<IValueFormatterRegistry>()` resolves.

**Success Criteria:**
1. `context.GetFeature<IValueFormatterRegistry>()` returns a non-null instance on a live host.

---

### PLA1-P6-T02: Expose `ITypeDrawerRegistry` via `GetFeature`

Same approach as PLA1-P6-T01 for `ITypeDrawerRegistry`.

**Success Criteria:**
1. `context.GetFeature<ITypeDrawerRegistry>()` returns a non-null instance.

---

### PLA1-P6-T03: Add `RegisterColorRule` to `TopicColorService`

**File:** `tools/DdsMonitor/DdsMonitor.Engine/TopicColorService.cs` (MODIFY)

**Dependencies:** None

**Scope:**
- Add `private List<Func<string, string?>> _programmaticRules = new()`.
- Add `public void RegisterColorRule(Func<string, string?> rule)` (thread-safe: lock on add).
- In `GetEffectiveColor(string shortName)`, after checking user overrides, iterate `_programmaticRules`; return the first non-null result before falling back to the auto palette.

**Success Criteria:**
1. A registered rule returning `"#FF0000"` for topics containing "Error" is applied in `GetEffectiveColor`.
2. A registered rule returning `null` is skipped (next rule or auto-palette is used).

**Unit Test Specifications:**

**Test File:** `tests/DdsMonitor.Engine.Tests/TopicColorServiceTests.cs`

| Test Name | Assertion | Expected Result |
|---|---|---|
| `RegisterColorRule_OverridesAutoColor` | Register rule returning `"#FF0000"` for "ErrorTopic"; call `GetEffectiveColor("ErrorTopic")` | Returns `"#FF0000"` |
| `RegisterColorRule_ReturningNull_FallsBackToAutoPalette` | Register rule returning `null`; call `GetEffectiveColor("NormalTopic")` | Returns auto-palette CSS var |
| `UserOverride_TakesPrecedenceOverRule` | Set user override to `"#00FF00"` AND register rule returning `"#FF0000"`; call `GetEffectiveColor` | Returns `"#00FF00"` (user override wins) |

---

### PLA1-P6-T04: Create `IExportFormatRegistry`

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Export/IExportFormatRegistry.cs` (NEW)  
**File:** `tools/DdsMonitor/DdsMonitor.Engine/Export/ExportFormatRegistry.cs` (NEW)

**Dependencies:** None

**Scope:**
- `IExportFormatRegistry`: `RegisterFormat(string label, Func<IReadOnlyList<SampleData>, string, CancellationToken, Task>)` and `IReadOnlyList<ExportFormatEntry> GetFormats()`.
- `ExportFormatEntry` record: `Label`, `ExportFunc`.
- `ExportFormatRegistry` implementation: thread-safe list.

**Success Criteria:**
1. Registered format appears in `GetFormats()`.
2. Multiple formats accumulate correctly.

---

### PLA1-P6-T05: Expose Export Formats in Samples Panel Export Button

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor` (MODIFY)

**Dependencies:** PLA1-P6-T04

**Scope:**
- Change the export button into a split-button or small dropdown: first item is always "Export as JSON" (current behavior); subsequent items come from `IExportFormatRegistry.GetFormats()`.
- When a custom format is selected, invoke its `ExportFunc` with the current filtered samples.

**Success Criteria:**
1. Default JSON export works unchanged.
2. When a format plugin registers a custom format, it appears in the dropdown.
3. Clicking a custom format invokes the registered function.

---

### PLA1-P6-T06: Create `ITooltipProviderRegistry`

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Ui/ITooltipProviderRegistry.cs` (NEW)  
**File:** `tools/DdsMonitor/DdsMonitor.Engine/Ui/TooltipProviderRegistry.cs` (NEW)

**Dependencies:** None

**Scope:**
- `ITooltipProviderRegistry`: `RegisterProvider(Func<Type, object?, string?> htmlProvider)` and `string? GetTooltipHtml(Type type, object? value)`.
- Implementation iterates providers; returns first non-null HTML string.

---

### PLA1-P6-T07: Consult `ITooltipProviderRegistry` in `TooltipPortal.razor`

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/TooltipPortal.razor` (MODIFY)

**Dependencies:** PLA1-P6-T06

**Scope:**
- Inject `ITooltipProviderRegistry` (optional).
- Before falling back to the default JSON tooltip, call `GetTooltipHtml(type, value)`.
- If a non-null HTML string is returned, render it as `MarkupString`.

**Success Criteria:**
1. A registered provider that returns HTML for its type is rendered in the tooltip.
2. When no provider matches, the default JSON tooltip renders unchanged.

---

### PLA1-P6-T08: Create `IFilterMacroRegistry`

**File:** `tools/DdsMonitor/DdsMonitor.Engine/IFilterMacroRegistry.cs` (NEW)  
**File:** `tools/DdsMonitor/DdsMonitor.Engine/FilterMacroRegistry.cs` (NEW)

**Dependencies:** None

**Scope:**
- `IFilterMacroRegistry`: `RegisterMacro(string name, Func<object?[], object?> impl)` and `IReadOnlyDictionary<string, Func<object?[], object?>> GetMacros()`.

---

### PLA1-P6-T09: Integrate Filter Macros into `FilterCompiler`

**File:** `tools/DdsMonitor/DdsMonitor.Engine/FilterCompiler.cs` (MODIFY)

**Dependencies:** PLA1-P6-T08

**Scope:**
- Inject `IFilterMacroRegistry`.
- When Dynamic LINQ encounters an unknown method name, look it up in `GetMacros()` and bind the registered `Func`.

**Success Criteria:**
1. A filter expression using a macro registered as `"DistanceTo"` compiles and executes correctly.
2. Unknown method names that are not registered still produce a compile error.

---

## Phase 7 — Kitchen Sink Demo Plugin

**Design Reference:** [§10 Phase 7](PLA1-DESIGN.md#10-phase-7--kitchen-sink-demo-plugin)

---

### PLA1-P7-T01: Create Demo Plugin Project

**File:** `tools/DdsMonitor/DdsMonitor.Plugins.FeatureDemo/DdsMonitor.Plugins.FeatureDemo.csproj` (NEW)

**Dependencies:** PLA1-P1-T01 through PLA1-P6-T09 (all extension points must exist)

**Scope:**
- Create `net8.0` Blazor class library project.
- Reference `DdsMonitor.Engine`.
- No references to `DdsMonitor.Blazor` (must use only the public plugin API).

**Success Criteria:**
1. Project builds without error.
2. Output DLL lands in a CI-accessible path.

---

### PLA1-P7-T02: Implement `FeatureDemoPlugin`

**File:** `tools/DdsMonitor/DdsMonitor.Plugins.FeatureDemo/FeatureDemoPlugin.cs` (NEW)

**Dependencies:** PLA1-P7-T01

**Scope:**
- Implement `IMonitorPlugin`.
- In `Initialize`, use `GetFeature<T>()` to discover and register against every extension point listed in Design §10.2.
- Every `GetFeature` call is guarded with a null check (graceful degradation).
- Register `DemoBackgroundProcessor` in `ConfigureServices`.

**Success Criteria:**
1. Plugin registers successfully against all available extension points.
2. When loaded into a mock host providing zero features, `Initialize` completes without exception.
3. When loaded into a full host, all extension points receive registrations.

---

### PLA1-P7-T03: Create `DemoDashboardPanel.razor`

**File:** `tools/DdsMonitor/DdsMonitor.Plugins.FeatureDemo/Components/DemoDashboardPanel.razor` (NEW)

**Scope:**
- A Blazor component that displays live metrics from `DemoBackgroundProcessor`.
- Suitable for spawning via `PluginPanelRegistry`.

**Success Criteria:**
1. Panel renders when spawned from the Plugin Panels menu.
2. Displays a counter or list that updates as `DemoBackgroundProcessor` processes samples.

---

## Phase 8 — Autonomous CI Testing

**Design Reference:** [§11 Phase 8](PLA1-DESIGN.md#11-phase-8--autonomous-ci-testing)

---

### PLA1-P8-T01: Create `DdsMonitor.Plugins.FeatureDemo.Tests` Project

**File:** `tests/DdsMonitor.Plugins.FeatureDemo.Tests/DdsMonitor.Plugins.FeatureDemo.Tests.csproj` (NEW)

**Dependencies:** PLA1-P7-T02

**Scope:**
- xUnit + Moq + bUnit test project.
- References `DdsMonitor.Engine`, `DdsMonitor.Plugins.FeatureDemo`.

---

### PLA1-P8-T02: Plugin Registration Unit Tests

**File:** `tests/DdsMonitor.Plugins.FeatureDemo.Tests/FeatureDemoPluginTests.cs` (NEW)

**Dependencies:** PLA1-P8-T01

**Success Criteria:**

| Test Name | Assertion |
|---|---|
| `Initialize_WhenAllFeaturesAvailable_RegistersAllExtensionPoints` | All registries have entries after `Initialize` |
| `Initialize_WhenNoFeaturesAvailable_DoesNotThrow` | Full `Initialize` with null-returning context completes without exception |
| `Initialize_RegistersContextMenuProvider_ForSampleData` | `IContextMenuRegistry.GetItems<SampleData>` returns at least one item |
| `Initialize_RegistersDetailViewer_ForDemoPayloadType` | `ISampleViewRegistry.GetViewer(typeof(DemoPayload))` returns non-null |
| `WorkspaceSaving_PopulatesPluginSettingsKey` | Publish `WorkspaceSavingEvent`; assert `"FeatureDemo"` key exists |
| `WorkspaceLoaded_RestoresDemoModeFromSettings` | Publish `WorkspaceLoadedEvent` with demo settings; assert demo mode applied |

---

### PLA1-P8-T03: bUnit Blazor Component Tests

**File:** `tests/DdsMonitor.Plugins.FeatureDemo.Tests/Components/DemoDashboardPanelTests.cs` (NEW)

**Success Criteria:**

| Test Name | Assertion |
|---|---|
| `DemoDashboardPanel_Renders_WithoutError` | `RenderComponent<DemoDashboardPanel>()` does not throw |
| `DemoDashboardPanel_ShowsProcessedCount` | After injecting a processor with count=5, rendered HTML contains "5" |

---

### PLA1-P8-T04: `PluginManagerPanel` bUnit Tests

**File:** `tests/DdsMonitor.Engine.Tests/Components/PluginManagerPanelTests.cs` (NEW)

_Specified in PLA1-P5-T03 above._

---

### PLA1-P8-T05: Headless Integration Test

**File:** `tests/DdsMonitor.Plugins.FeatureDemo.Tests/HeadlessPluginIntegrationTest.cs` (NEW)

**Dependencies:** PLA1-P7-T02, PLA1-P8-T01

**Scope (narrowed — PLA1-DEBT-020):**
- Boot a minimal DI container (no Blazor renderer, no `AddDdsMonitorServices` full stack to avoid native DDS
  dependencies in CI).
- Demonstrate the `PluginConfigService` / `PluginLoader` enablement check in-process: create a
  `PluginConfigService`, add `"Feature Demo"` to `EnabledPlugins`, replicate the exact `PluginLoader`
  enabled-check logic, then call `FeatureDemoPlugin.ConfigureServices` as `PluginLoader` would.
- Provide a `FakeSampleStore` whose `TotalCount` is 10 and `AllSamples` returns 10 `SampleData`
  instances with `DemoPayload` payloads.
- Assert that `DemoBackgroundProcessor` processed at least 1 sample.

> **Narrowing rationale (PLA1-DEBT-020):** `AddDdsMonitorServices` wires `IDdsBridge` (native
> CycloneDDS) and `DdsIngestionService`, making headless CI impractical without a full native build.
> The chosen test covers the `PluginConfigService` enabled-path and the ISampleStore → processor
> data-flow — the two behaviorally significant aspects of the spec.

**Success Criteria:**
1. Test completes in under 5 seconds.
2. `DemoBackgroundProcessor.ProcessedCount >= 1`.
3. No unhandled exceptions during plugin `Initialize` or sample processing.
