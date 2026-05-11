# PLA1-BATCH-09 Report

**Batch:** PLA1-BATCH-09  
**Date:** 2026-03-27  
**Status:** Complete

---

## Summary

All seven items (PLA1-DEBT-018, PLA1-DEBT-019, PLA1-P8-T01 through T05) are implemented.
All CI-relevant test suites pass:

| Suite | Before | After |
|---|---|---|
| `DdsMonitor.Engine.Tests` | 616 | 616 |
| `DdsMonitor.Blazor.Tests` | 16 | 16 |
| `DdsMonitor.Plugins.FeatureDemo.Tests` | (new) | 10 |

---

## PLA1-DEBT-018 — TopicColorService singleton

**Problem:** `TopicColorService` was registered as `AddScoped<>` in `Program.cs` (Blazor host).
Because `IMonitorContext` runs in the root service provider scope,
`GetFeature<TopicColorService>()` returned `null` for all root-scope callers (including
`FeatureDemoPlugin.Initialize`), so the §10.2 "DEMO topics red" rule was never registered.

**Fix:**
- `ServiceCollectionExtensions.cs`: added `services.AddSingleton<TopicColorService>(sp => new TopicColorService(new WorkspaceState(sp.GetService<AppSettings>())))`. This uses the same `WorkspaceState` factory approach already used by the `IWorkspaceState` scoped registration; no captive dependency.
- `Program.cs`: removed `builder.Services.AddScoped<DdsMonitor.Engine.TopicColorService>()` — now superseded by the singleton from the engine layer. Components that `@inject TopicColorService` continue to work because singletons are freely injectable into Blazor components.
- `FeatureDemoPlugin.Initialize`: added `context.GetFeature<TopicColorService>()?.RegisterColorRule(name => name.Contains("DEMO", StringComparison.OrdinalIgnoreCase) ? "#FF0000" : null)`.

`PLA1-DESIGN.md` §10.2 was **not** modified — the singleton lifetime aligns perfectly with the documented "singleton `TopicColorService`" resolution path from DEBT-018.

---

## PLA1-DEBT-019 — DetailPanel tooltip context

**Fix:** Updated `ShowJsonTooltip` in `DetailPanel.razor` to pass `ContextType` and `ContextValue` to `TooltipState`, using `_currentSample?.TopicMetadata?.TopicType` and `_currentSample?.Payload` respectively. This mirrors the `ShowDetailTooltip` pattern from `SamplesPanel` and `InstancesPanel`. When no sample is selected, both values are `null` and `TooltipPortal` falls back to default JSON rendering unchanged.

---

## PLA1-P8-T01 — New test project

Project: `tests/DdsMonitor.Plugins.FeatureDemo.Tests/`  
SDK: `Microsoft.NET.Sdk.Razor` (required for bUnit Razor component tests)  
Packages: `bunit 1.35.3`, `xunit 2.9.2`, `xunit.runner.visualstudio 2.8.2`, `Microsoft.NET.Test.Sdk 17.12.0`, `coverlet.collector 6.0.2`  
References: `DdsMonitor.Engine`, `DdsMonitor.Plugins.FeatureDemo`  
Added to `CycloneDDS.NET.sln`.

---

## PLA1-P8-T02 — FeatureDemoPluginTests

File: `tests/DdsMonitor.Plugins.FeatureDemo.Tests/FeatureDemoPluginTests.cs`  
6 tests, all passing.

| Test | Result |
|---|---|
| `Initialize_WhenAllFeaturesAvailable_RegistersAllExtensionPoints` | ✅ |
| `Initialize_WhenNoFeaturesAvailable_DoesNotThrow` | ✅ |
| `Initialize_RegistersContextMenuProvider_ForSampleData` | ✅ |
| `Initialize_RegistersDetailViewer_ForDemoPayloadType` | ✅ |
| `WorkspaceSaving_PopulatesPluginSettingsKey` | ✅ |
| `WorkspaceLoaded_RestoresDemoModeFromSettings` | ✅ |

Stubs used: `NullMonitorContext` (returns null for all `GetFeature<T>()` calls); `FakeWorkspaceState` (provides a temp-dir path for `TopicColorService`). No Moq dependency — stub types inline, consistent with project conventions.

---

## PLA1-P8-T03 — DemoDashboardPanel bUnit tests

File: `tests/DdsMonitor.Plugins.FeatureDemo.Tests/Components/DemoDashboardPanelTests.cs`  
2 tests, all passing.

| Test | Result |
|---|---|
| `DemoDashboardPanel_Renders_WithoutError` | ✅ |
| `DemoDashboardPanel_ShowsProcessedCount` | ✅ |

The `ShowsProcessedCount` test starts `DemoBackgroundProcessor` with a `FakeSampleStore(totalCount: 5)`, waits 150 ms for the initial timer tick (dueTime=0), then mounts the component and asserts the markup contains "5".

---

## PLA1-P8-T04 — PluginManagerPanel bUnit tests

**Decision:** Tests already exist and pass in `tests/DdsMonitor.Blazor.Tests/Components/PluginManagerPanelTests.cs` (5 tests, part of the 16-test Blazor.Tests suite). The spec path `tests/DdsMonitor.Engine.Tests/Components/PluginManagerPanelTests.cs` was not created to avoid duplicating stub code. Rationale:

- The existing `TestablePluginManager.razor` stub lives in `DdsMonitor.Blazor.Tests` and exercises identical enable/disable/save logic.
- Adding a second copy in `DdsMonitor.Engine.Tests` would require duplicating both the stub component and the `FakeMonitorPlugin` helper with no coverage gain.
- Canonical location: `tests/DdsMonitor.Blazor.Tests/Components/PluginManagerPanelTests.cs`.

---

## PLA1-P8-T05 — Headless DI integration test

**Decision:** File placed at `tests/DdsMonitor.Plugins.FeatureDemo.Tests/HeadlessPluginIntegrationTest.cs` (not `DdsMonitor.Engine.Tests`).  
Rationale: the test requires `DemoBackgroundProcessor` and `FeatureDemoPlugin` from `DdsMonitor.Plugins.FeatureDemo`; adding that project reference to `DdsMonitor.Engine.Tests` would create an upward dependency (engine tests → plugin). Placing it in the plugin's own test project is architecturally cleaner.

2 tests, all passing.

| Test | Result |
|---|---|
| `DemoBackgroundProcessor_ProcessesAtLeastOneSample_WithinTimeout` | ✅ (completes in < 250 ms) |
| `FeatureDemoPlugin_Initialize_DoesNotThrow_InHeadlessContainer` | ✅ |

---

## Files changed

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs` | `TopicColorService` singleton registration |
| `tools/DdsMonitor/DdsMonitor.Blazor/Program.cs` | Remove scoped `TopicColorService`; add comment |
| `tools/DdsMonitor/DdsMonitor.Plugins.FeatureDemo/FeatureDemoPlugin.cs` | `RegisterColorRule` call for DEMO topics |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor` | `ShowJsonTooltip` passes `ContextType`/`ContextValue` |
| `tests/DdsMonitor.Plugins.FeatureDemo.Tests/` | New test project (4 files) |
| `docs/plugin-api/PLA1-DEBT-TRACKER.md` | DEBT-018, 019 → ✅ Resolved |
| `docs/plugin-api/PLA1-TASK-TRACKER.md` | P8-T01–T05 → ✅ |
| `CycloneDDS.NET.sln` | Added `DdsMonitor.Plugins.FeatureDemo.Tests` |

---

## Questions

None.
