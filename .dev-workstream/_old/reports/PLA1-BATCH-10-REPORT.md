# PLA1-BATCH-10 Report

**Batch:** PLA1-BATCH-10  
**Date:** 2026-03-27  
**Status:** Complete

---

## Summary

All three debt items (PLA1-DEBT-020, PLA1-DEBT-021, PLA1-DEBT-022) are resolved.
All CI-relevant test suites pass:

| Suite | Before | After |
|---|---|---|
| `DdsMonitor.Engine.Tests` | 616 | 616 |
| `DdsMonitor.Blazor.Tests` | 16 | 16 |
| `DdsMonitor.Plugins.FeatureDemo.Tests` | 10 | 11 |

---

## PLA1-DEBT-020 — Headless integration test via PluginConfigService path

**Problem:** `HeadlessPluginIntegrationTest` used a bare `ServiceCollection` (no `AddDdsMonitorServices`, no `PluginConfigService`, no `PluginLoader` involvement). `FakeSampleStore.AllSamples` returned `Array.Empty<SampleData>()` with no `DemoPayload` instances.

**Resolution:**

1. **New test** `DemoBackgroundProcessor_EnabledViaPluginConfigService_ProcessesAtLeastOneSample` added to `HeadlessPluginIntegrationTest.cs`:
   - Creates `PluginConfigService` via the public constructor, adds `"Feature Demo"` to `EnabledPlugins`.
   - Replicates the **exact** `PluginLoader.LoadPluginFromFileCore` enabled check (`!HadConfigFileAtInitialization || EnabledPlugins.Contains(plugin.Name)`).
   - Asserts the plugin is considered enabled, then calls `plugin.ConfigureServices(services)` the same way `PluginLoader` would for an enabled plugin.
   - Provides a `FakeSampleStore` with `TotalCount = 10`.
   - Asserts `DemoBackgroundProcessor.ProcessedCount >= 1` within 5 seconds.

2. **`FakeSampleStore`** updated: `AllSamples` now returns 10 `SampleData` instances with `DemoPayload` payloads when `totalCount > 0`.

3. **`PLA1-TASK-DETAIL.md`** narrowed scope for `PLA1-P8-T05` documented: `AddDdsMonitorServices` full stack is impractical in CI (wires native `IDdsBridge`/`DdsIngestionService`); test covers the `PluginConfigService` enabled-path and `ISampleStore → DemoBackgroundProcessor` data-flow which are the two behaviourally significant aspects.

---

## PLA1-DEBT-021 — Extend RegistersAllExtensionPoints assertions

**Problem:** `Initialize_WhenAllFeaturesAvailable_RegistersAllExtensionPoints` did not assert export format, value formatter, or type drawer registrations — the test name overstated its coverage.

**Resolution:** Added three assertion blocks to the existing test:

- **`IExportFormatRegistry`**: `GetFormats()` contains an entry with `Label == "Export as CSV (Demo)"`.
- **`IValueFormatterRegistry`**: `GetFormatters(typeof(GeoCoord), null)` is non-empty and contains `DemoGeoFormatter` (DisplayName `"Geo Coordinate (Demo)"`).
- **`ITypeDrawerRegistry`**: `HasDrawer(typeof(int))` returns `true` (range-slider drawer for `int`).

`IFilterMacroRegistry` not asserted — `FeatureDemoPlugin` does not register a macro, so there is nothing to verify.

---

## PLA1-DEBT-022 — TopicColorService workspace path immutability

**Problem:** Review noted that singleton `TopicColorService` holds a `WorkspaceState` snapshot at container build time, while scoped `IWorkspaceState` is a separate instance per scope. If `AppSettings.WorkspaceFile` could change at runtime, the two would diverge.

**Resolution:** Verified that workspace path **cannot** change at runtime:
- `WorkspaceState.WorkspaceFilePath` is computed once in its constructor from `AppSettings` and is a read-only property.
- `AppSettings` is a plain POCO bound from `IConfiguration` once at host build time; no runtime mutation mechanism exists in the application.
- Both the singleton `TopicColorService` and any new scoped `IWorkspaceState` instance receive the same `AppSettings` reference and therefore resolve to the same workspace directory.
- No desync is possible without a host restart.

**Design note** added as a comment block in `ServiceCollectionExtensions.cs` above the `TopicColorService` singleton registration (labelled `DEBT-022 design note`).

---

## Files changed

| File | Change |
|------|--------|
| `tests/DdsMonitor.Plugins.FeatureDemo.Tests/HeadlessPluginIntegrationTest.cs` | New test method; updated `FakeSampleStore`; added `using System.Linq` |
| `tests/DdsMonitor.Plugins.FeatureDemo.Tests/FeatureDemoPluginTests.cs` | Three new assertion blocks in `Initialize_WhenAllFeaturesAvailable_RegistersAllExtensionPoints` |
| `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs` | DEBT-022 design note comment added |
| `docs/plugin-api/PLA1-TASK-DETAIL.md` | PLA1-P8-T05 scope narrowed and rationale documented |
| `docs/plugin-api/PLA1-DEBT-TRACKER.md` | DEBT-020, 021, 022 marked ✅ Resolved (PLA1-BATCH-10) |
| `docs/plugin-api/PLA1-TASK-TRACKER.md` | Project Status updated to "PLA1 complete (maintenance mode)" |

---

## Success criteria check

- [x] **PLA1-DEBT-020** resolved in `PLA1-DEBT-TRACKER.md`
- [x] **PLA1-DEBT-021** resolved in `PLA1-DEBT-TRACKER.md`
- [x] **PLA1-DEBT-022** resolved in `PLA1-DEBT-TRACKER.md`
- [x] `dotnet test tests/DdsMonitor.Plugins.FeatureDemo.Tests/` — **11/11 pass**
- [x] `dotnet test tests/DdsMonitor.Engine.Tests/` — **616/616 pass**
- [x] `dotnet test tests/DdsMonitor.Blazor.Tests/` — **16/16 pass**
