# PLA1-BATCH-08 Report

**Batch:** PLA1-BATCH-08  
**Date:** 2026-03-27  
**Status:** Complete

---

## Summary

Completed **PLA1-DEBT-016**, **PLA1-DEBT-017**, **PLA1-P7-T01**, **PLA1-P7-T02**, and **PLA1-P7-T03**.

- Engine tests: **616/616** pass (unchanged).
- Blazor.Tests: **16/16** pass (was 11; +5 new `TooltipPortalTests`).
- `DdsMonitor.Engine` builds: **0 errors**.
- `DdsMonitor.Blazor` builds: **0 errors**.
- `DdsMonitor.Plugins.FeatureDemo` builds: **0 errors**, plugin staged to `plugins/`.

---

## PLA1-DEBT-016 — PluginConfigService production logger

**Problem:** `ServiceCollectionExtensions.cs` called `new PluginConfigService()` (parameterless ctor, `_logger = null`) so corrupt `enabled-plugins.json` silently swallowed the warning at runtime.

**Fix:**

1. **`PluginConfigService.cs`** — Changed the public constructor to `public PluginConfigService(ILoggerFactory? loggerFactory = null)` which delegates to the internal test-seam constructor via `loggerFactory?.CreateLogger<PluginConfigService>()`. The default `null` keeps all headless/test call sites unchanged.

2. **`ServiceCollectionExtensions.cs`** — Added optional `ILoggerFactory? loggerFactory = null` parameter to `AddDdsMonitorServices`; passes it to `new PluginConfigService(loggerFactory)`.

3. **`Program.cs`** — Added a bootstrap `LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning).AddConsole())` and passes it to `AddDdsMonitorServices`. The factory is disposed (via `using`) after the call returns because `PluginConfigService` only reads the config file during its constructor — no logging calls occur after that startup phase.

**Regression check:** All calls to `AddDdsMonitorServices` without the optional parameter (`HostWiringTests`, headless hosts) continue to work with `loggerFactory = null` (no-op logger, no change in behaviour).

---

## PLA1-DEBT-017 — TooltipService.Show with context + bUnit coverage

**Problem:** `TooltipPortal.razor` correctly consulted `ITooltipProviderRegistry` (P6-T07), but every `TooltipService.Show` call site passed `new TooltipState(html, x, y)` without `ContextType`/`ContextValue`, meaning the registry never ran in production. Additionally, the portal wrapped even plugin-supplied free HTML in `<pre class="tooltip-content">`, breaking rich markup.

**Fix:**

1. **`SamplesPanel.razor`** — `ShowDetailTooltip` now passes `ContextType: sample.TopicMetadata.TopicType` and `ContextValue: sample.Payload`. Providers registered for a payload type (e.g., `FeatureDemoPlugin`'s `DemoPayload` provider) will override the default JSON tooltip.

2. **`InstancesPanel.razor`** — Same fix applied to its `ShowDetailTooltip` method (both tooltip call sites updated).

3. **`TooltipPortal.razor`** — Replaced the single `<pre>` branch with a two-branch render:
   - **Provider HTML path:** result from `GetOverrideHtml(tooltip)` rendered as free `MarkupString` (no `<pre>` wrapper) so plugins can contribute rich HTML.
   - **Default path:** the pre-computed JSON is still wrapped in `<pre class="tooltip-content">` for correct monospaced rendering.

4. **`StubTooltipPortal.razor`** (new, `tests/DdsMonitor.Blazor.Tests/Components/`) — A bUnit-testable stub that mirrors the portal's `GetOverrideHtml`/fallback split. Parameterised with `ContextType`, `ContextValue`, and `DefaultHtml`.

5. **`TooltipPortalTests.cs`** (new, `tests/DdsMonitor.Blazor.Tests/`) — Five bUnit tests:
   - `StubTooltipPortal_RendersProviderHtml_WhenRegistryReturnsMarkup` — provider HTML appears without `<pre>`.
   - `StubTooltipPortal_RendersDefaultJson_WhenProviderReturnsNull` — default in `<pre class="tooltip-content">`.
   - `StubTooltipPortal_RendersDefaultJson_WhenNoProviderRegistered` — same fallback with empty registry.
   - `StubTooltipPortal_RendersDefaultJson_WhenProviderDoesNotMatchType` — wrong-type provider skipped.
   - `StubTooltipPortal_RendersDefaultJson_WhenContextTypeIsNull` — null context bypasses registry entirely.

---

## PLA1-P7-T01 — Demo Plugin project

**New files:**
- `tools/DdsMonitor/DdsMonitor.Plugins.FeatureDemo/DdsMonitor.Plugins.FeatureDemo.csproj` — `net8.0` Razor class library; references only `DdsMonitor.Engine` and `Microsoft.AspNetCore.Components.Web`. No reference to `DdsMonitor.Blazor`. Includes `StagePlugin` MSBuild target that copies the output DLL to the host's `plugins/` folder on every build.

**Solution integration:**
- Project GUID `{3CEE700B-8E85-46B0-92FA-090D124E66F0}` added to `CycloneDDS.NET.sln` under the `DdsMonitor` solution folder (same parent as ECS plugin).
- Build configuration platforms (Debug/Release × Any CPU/x64/x86) added.

---

## PLA1-P7-T02 — FeatureDemoPlugin

**New files:**
- `DemoTypes.cs` — `DemoPayload` (sealed class) and `GeoCoord` (readonly struct) used as registry keys and demo data.
- `DemoGeoFormatter.cs` — `IValueFormatter` for `GeoCoord`: score 1.0 for exact type; `FormatText` → `"{lat}°N, {lon}°E"`; `FormatTokens` → Number + Punctuation tokens.
- `DemoBackgroundProcessor.cs` — `IHostedService` + `IDisposable`; uses a 1-second `Timer` to snapshot `ISampleStore.TotalCount` into `ProcessedCount`; raises `OnUpdated` event for panels.
- `FeatureDemoPlugin.cs` — `IMonitorPlugin` implementation; `ConfigureServices` registers `DemoBackgroundProcessor` as singleton + hosted service; `Initialize` registers against all §10.2 extension points:

  | Extension Point | Demo Action |
  |---|---|
  | `IMenuRegistry` | `"Plugins/Demo"` → `"Show Dashboard"` |
  | `PluginPanelRegistry` | `DemoDashboardPanel` as `"Demo Dashboard"` |
  | `IContextMenuRegistry` | `"Log to Console (Demo)"` item on `SampleData` |
  | `ISampleViewRegistry` | Custom RenderFragment for `DemoPayload` |
  | `IValueFormatterRegistry` | `DemoGeoFormatter` for `GeoCoord` |
  | `ITypeDrawerRegistry` | Range slider `<input type="range">` for `int` |
  | `IExportFormatRegistry` | `"Export as CSV (Demo)"` (ordinal + topic name CSV) |
  | `ITooltipProviderRegistry` | `<div class="demo-tooltip">` sensor-gauge HTML for `DemoPayload` |
  | `IEventBroker` | Subscribe to `WorkspaceSavingEvent` / `WorkspaceLoadedEvent` |

  Every `GetFeature<T>()` call is guarded with `?.` null-conditional; `IEventBroker` subscription is guarded with an explicit null check.

**Design §10.3 graceful degradation:** When loaded into a context that returns `null` for all features, `Initialize` completes without exception.

**Note on `TopicColorService`:** `TopicColorService` is registered as `AddScoped` in `Program.cs` (UI-mode only) while `IMonitorContext.GetFeature<T>()` resolves from the root singleton DI scope. Calling `GetService<TopicColorService>()` from the root scope would throw `InvalidOperationException` (scope validation). The demo plugin omits `TopicColorService` registration to avoid this; a future refactor to register `TopicColorService` as a singleton would enable it. Documented for PLA1-BATCH-09 if needed.

---

## PLA1-P7-T03 — DemoDashboardPanel

**New files:**
- `Components/_Imports.razor` — namespace imports for the Components folder.
- `Components/DemoDashboardPanel.razor` — Blazor component that injects `DemoBackgroundProcessor` and displays `ProcessedCount` (updated live via `OnUpdated` event subscription). Implements `IDisposable` to unsubscribe cleanly. Can be spawned by the host `WindowManager` via `PluginPanelRegistry`.

---

## Files changed

| File | Change |
|------|--------|
| `tools/DdsMonitor/DdsMonitor.Engine/Plugins/PluginConfigService.cs` | DEBT-016: public ctor now accepts `ILoggerFactory?` |
| `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs` | DEBT-016: added `ILoggerFactory?` param; pass to `PluginConfigService` |
| `tools/DdsMonitor/DdsMonitor.Blazor/Program.cs` | DEBT-016: bootstrap `LoggerFactory.Create` passed to `AddDdsMonitorServices` |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor` | DEBT-017: `ShowDetailTooltip` passes `ContextType`/`ContextValue` |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/InstancesPanel.razor` | DEBT-017: same as SamplesPanel |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/TooltipPortal.razor` | DEBT-017: split `<pre>` vs free HTML; `GetOverrideHtml` replaces `GetDisplayHtml` |
| `tests/DdsMonitor.Blazor.Tests/Components/StubTooltipPortal.razor` | NEW — bUnit stub for tooltip portal |
| `tests/DdsMonitor.Blazor.Tests/TooltipPortalTests.cs` | NEW — 5 bUnit tests |
| `tools/DdsMonitor/DdsMonitor.Plugins.FeatureDemo/DdsMonitor.Plugins.FeatureDemo.csproj` | NEW — demo plugin project |
| `tools/DdsMonitor/DdsMonitor.Plugins.FeatureDemo/DemoTypes.cs` | NEW — DemoPayload, GeoCoord |
| `tools/DdsMonitor/DdsMonitor.Plugins.FeatureDemo/DemoGeoFormatter.cs` | NEW — IValueFormatter |
| `tools/DdsMonitor/DdsMonitor.Plugins.FeatureDemo/DemoBackgroundProcessor.cs` | NEW — IHostedService |
| `tools/DdsMonitor/DdsMonitor.Plugins.FeatureDemo/FeatureDemoPlugin.cs` | NEW — IMonitorPlugin |
| `tools/DdsMonitor/DdsMonitor.Plugins.FeatureDemo/Components/_Imports.razor` | NEW |
| `tools/DdsMonitor/DdsMonitor.Plugins.FeatureDemo/Components/DemoDashboardPanel.razor` | NEW — P7-T03 |
| `CycloneDDS.NET.sln` | Added FeatureDemo project with GUID `{3CEE700B-8E85-46B0-92FA-090D124E66F0}` |
| `docs/plugin-api/PLA1-DEBT-TRACKER.md` | DEBT-016 and DEBT-017 marked ✅ Resolved |
| `docs/plugin-api/PLA1-TASK-TRACKER.md` | P7-T01–T03 marked ✅ |

---

## Suggested commit message

```
feat(dds-monitor): DEBT-016/017 + Phase 7 Kitchen Sink demo plugin (PLA1-BATCH-08)

DEBT-016: Wire ILoggerFactory to PluginConfigService so corrupt enabled-plugins.json
logs a Warning in production. AddDdsMonitorServices gains optional ILoggerFactory param;
Program.cs passes a bootstrap LoggerFactory.Create console logger.

DEBT-017: SamplesPanel + InstancesPanel ShowDetailTooltip now pass ContextType/ContextValue
so ITooltipProviderRegistry runs in the live UI. TooltipPortal renders provider HTML as
free MarkupString (no <pre> wrapper); default JSON path keeps <pre class="tooltip-content">.
Add StubTooltipPortal + 5 bUnit tests covering both render paths.

PLA1-P7-T01–T03: Add DdsMonitor.Plugins.FeatureDemo — a Kitchen Sink demo plugin that
registers against all §10.2 extension points (menu, panel, context menu, sample view,
value formatter, type drawer, export format, tooltip provider, workspace events).
DemoBackgroundProcessor polls ISampleStore.TotalCount every second; DemoDashboardPanel
displays the live count. Plugin staged to plugins/ on every build.
```
