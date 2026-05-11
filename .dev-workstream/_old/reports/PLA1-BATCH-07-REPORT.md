# PLA1-BATCH-07 Report

**Batch:** PLA1-BATCH-07  
**Developer:** AI Agent  
**Date:** 2026-03-27  
**Status:** COMPLETE

---

## Summary

All nine tasks completed (PLA1-DEBT-011 through PLA1-DEBT-015, PLA1-P6-T06 through PLA1-P6-T09).  
Phase 6 of the DdsMonitor Plugin API is now fully implemented.

**Test counts:**
- `dotnet test tests/DdsMonitor.Engine.Tests/` — **616 passed** (up from 602, +14 new tests)
- `dotnet test tests/DdsMonitor.Blazor.Tests/` — **11 passed** (unchanged count, but tests were updated)
- `dotnet test tests/DdsMonitor.Plugins.ECS.Tests/` — **48 passed** (no change, no regressions)

---

## Task-by-task

### PLA1-DEBT-011 — StubDetailTreeView type-key alignment ✅

**Files changed:**
- `tests/DdsMonitor.Blazor.Tests/BlazorTestTypes.cs` — NEW: `FooTopicType` and `BarTopicType` with `[DdsTopic]` attribute used as registry keys in tests
- `tests/DdsMonitor.Blazor.Tests/Components/StubDetailTreeView.razor` — Changed `GetViewer(Sample.Payload?.GetType() ?? typeof(object))` → `GetViewer(Sample.TopicMetadata.TopicType)` to match production `DetailPanel.RenderTreeView`
- `tests/DdsMonitor.Blazor.Tests/DetailPanelViewRegistryTests.cs` — Updated `MakeSample` to supply real `TopicMetadata` (no more `null!`); tests now register viewers against `typeof(FooTopicType)` / `typeof(BarTopicType)` (topic types, not payload types)

**Approach:** Added `[DdsTopic]`-decorated struct types to the Blazor.Tests project. `CycloneDDS.Schema` types are accessible via the transitive project reference through `DdsMonitor.Engine`. The `TopicMetadata` constructor works correctly with empty structs (produces empty AllFields/KeyFields).

---

### PLA1-DEBT-012 — app.css rules for plugin-manager and export group ✅

**Files changed:**
- `tools/DdsMonitor/DdsMonitor.Blazor/wwwroot/app.css` — Added rules for:
  - `.plugin-manager` (container, toolbar, title, restart-badge, empty state, table, thead, row hover, cell-path)
  - `.samples-panel__export-group` (split-button group, caret, dropdown, dropdown items)
  
Styles are consistent with `.topic-sources__*` spacing, border-radius, and color-variable patterns.

---

### PLA1-DEBT-013 — Single registration for IValueFormatterRegistry / ITypeDrawerRegistry ✅

**Files changed:**
- `tools/DdsMonitor/DdsMonitor.Blazor/Program.cs` — Removed the two duplicate `AddSingleton<IValueFormatterRegistry>` and `AddSingleton<ITypeDrawerRegistry>` lines from the non-headless path. `AddDdsMonitorServices` is now the single source of truth for these registrations.

**Risk assessment:** Low. The former duplicates used the same concrete types. Removing them makes DI resolution deterministic: the one instance registered in `AddDdsMonitorServices` is used everywhere.

---

### PLA1-DEBT-014 — Two-rule TopicColorService test ✅

**Files changed:**
- `tests/DdsMonitor.Engine.Tests/TopicColorServiceTests.cs` — Added `RegisterColorRule_TwoRules_FirstNull_SecondReturnsColor`: registers two rules (first returns `null`, second returns `#123456`); asserts second rule's result is used.

---

### PLA1-DEBT-015 — Warning log for corrupt enabled-plugins.json ✅

**Files changed:**
- `tools/DdsMonitor/DdsMonitor.Engine/Plugins/PluginConfigService.cs` — Added optional `ILogger<PluginConfigService>?` parameter to the internal constructor; `TryParseFile` now calls `_logger?.LogWarning(ex, ...)` when parsing fails
- `tests/DdsMonitor.Engine.Tests/Plugins/PluginConfigServiceTests.cs` — Added `Load_WhenFileCorrupt_LogsWarning` test; uses an inline `FakeLogger<T>` (no Moq required)

**Note:** The public `PluginConfigService()` constructor (used in production via `new PluginConfigService()` in `ServiceCollectionExtensions.cs`) passes `null` for the logger. The warning will only appear in production if the caller provides a logger. Since `PluginConfigService` is constructed eagerly before the DI container is built, injecting a framework logger at that point is not straightforward. Documenting as a known limitation: the warning is testable but not surfaced in the default production startup. A future improvement could use a `LoggerFactory` created from the configuration.

---

### PLA1-P6-T06 — ITooltipProviderRegistry + TooltipProviderRegistry ✅

**Files created:**
- `tools/DdsMonitor/DdsMonitor.Engine/Ui/ITooltipProviderRegistry.cs` — Interface with `RegisterProvider(Func<Type, object?, string?> htmlProvider)` and `GetTooltipHtml(Type type, object? value)`
- `tools/DdsMonitor/DdsMonitor.Engine/Ui/TooltipProviderRegistry.cs` — Thread-safe implementation (snapshot copy under lock; iterates providers returning first non-null)

**Files changed:**
- `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs` — Added `services.AddSingleton<ITooltipProviderRegistry, TooltipProviderRegistry>()`
- `tests/DdsMonitor.Engine.Tests/Ui/TooltipProviderRegistryTests.cs` — NEW: 4 tests covering empty registry, matching provider, first-null-try-next, and no-match fallback
- `tests/DdsMonitor.Engine.Tests/HostWiringTests.cs` — Added `GetFeature_ITooltipProviderRegistry_ReturnsNonNull`

---

### PLA1-P6-T07 — TooltipPortal.razor integration ✅

**Files changed:**
- `tools/DdsMonitor/DdsMonitor.Blazor/Services/TooltipService.cs` — Extended `TooltipState` record with optional `Type? ContextType = null` and `object? ContextValue = null` parameters (backward compatible; existing `new TooltipState(html, x, y)` calls continue to compile)
- `tools/DdsMonitor/DdsMonitor.Blazor/Components/TooltipPortal.razor` — Injected `ITooltipProviderRegistry`; added `GetDisplayHtml(TooltipState)` method that calls registry when `ContextType != null`, falling back to `tooltip.Html`

**Design note:** `ITooltipProviderRegistry` is injected via `@inject` (not optional injection) since it's always registered in `AddDdsMonitorServices`. Callers that want plugin-overridable tooltips pass `ContextType` and `ContextValue` in `TooltipService.Show(new TooltipState(..., contextType, contextValue))`. Existing call sites that omit these args continue to use the default HTML path unchanged.

---

### PLA1-P6-T08 — IFilterMacroRegistry + FilterMacroRegistry ✅

**Files created:**
- `tools/DdsMonitor/DdsMonitor.Engine/IFilterMacroRegistry.cs` — Interface with `RegisterMacro(string name, Func<object?[], object?> impl)` and `IReadOnlyDictionary<string, Func<object?[], object?>> GetMacros()`
- `tools/DdsMonitor/DdsMonitor.Engine/FilterMacroRegistry.cs` — Thread-safe implementation (Dictionary under lock; `GetMacros()` returns a snapshot copy; duplicate names replace previous impl)

**Files changed:**
- `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs` — Added `services.AddSingleton<IFilterMacroRegistry, FilterMacroRegistry>()`
- `tests/DdsMonitor.Engine.Tests/FilterMacroRegistryTests.cs` — NEW: 4 tests
- `tests/DdsMonitor.Engine.Tests/HostWiringTests.cs` — Added `GetFeature_IFilterMacroRegistry_ReturnsNonNull`

---

### PLA1-P6-T09 — FilterCompiler macro integration ✅

**Files changed:**
- `tools/DdsMonitor/DdsMonitor.Engine/FilterCompiler.cs`:
  - Added `MacroShim` internal static class decorated with `[DynamicLinqType]` (discoverable by the default `DynamicLinqCustomTypeProvider`). Has `Invoke(string name, …)` overloads for 0–5 arguments; routes through `MacroShim.Registry` at evaluation time
  - Added `FilterCompiler(IFilterMacroRegistry? macroRegistry)` constructor; sets `MacroShim.Registry` at construction time so predicates can call macros during evaluation
  - Added `ExpandMacros(string expression, IReadOnlyDictionary<string, Func<…>> macros)` pre-processor: uses regex with negative look-behind `(?<![.\w])` to replace free-standing `MacroName(` with `MacroShim.Invoke("MacroName", ` before passing to Dynamic LINQ
  - In `Compile()`: calls `ExpandMacros` after `NormalizeCliOperators` and before `PrepareExpression`
- `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs` — Changed `services.AddSingleton<IFilterCompiler, FilterCompiler>()` to a factory `services.AddSingleton<IFilterCompiler>(sp => new FilterCompiler(sp.GetService<IFilterMacroRegistry>()))` so the macro registry is injected when present
- `tests/DdsMonitor.Engine.Tests/FilterCompilerTests.cs` — Added `FilterCompiler_WithRegisteredMacro_ExecutesCorrectly` and `FilterCompiler_WithUnknownMethodName_ReturnsError`

**Implementation note on type coercion:** `MacroShim.Invoke` returns `object?`. Dynamic LINQ's `ConvertObjectToSupportComparison = true` converts the boxed result for comparison. Macro implementations should return the same numeric type as the comparison literal (e.g., return `int` when comparing `> 100`). The test verifies this: `Squared` returns `(object?)(int)(x*x)`.

**Unknown method names:** expressions containing `UnknownFunc(...)` are not expanded by the pre-processor (only registered macro names are); Dynamic LINQ then fails to parse the method, returning `FilterResult(IsValid = false, ...)`. ✅

---

## Files created (new)

| File | Purpose |
|------|---------|
| `tests/DdsMonitor.Blazor.Tests/BlazorTestTypes.cs` | DDS topic types for Blazor test use |
| `tools/DdsMonitor/DdsMonitor.Engine/Ui/ITooltipProviderRegistry.cs` | P6-T06 interface |
| `tools/DdsMonitor/DdsMonitor.Engine/Ui/TooltipProviderRegistry.cs` | P6-T06 implementation |
| `tools/DdsMonitor/DdsMonitor.Engine/IFilterMacroRegistry.cs` | P6-T08 interface |
| `tools/DdsMonitor/DdsMonitor.Engine/FilterMacroRegistry.cs` | P6-T08 implementation |
| `tests/DdsMonitor.Engine.Tests/Ui/TooltipProviderRegistryTests.cs` | P6-T06 tests |
| `tests/DdsMonitor.Engine.Tests/FilterMacroRegistryTests.cs` | P6-T08 tests |

---

## Questions / Notes

1. **DEBT-015 production logging gap:** `PluginConfigService` is constructed before the DI container is built, so no `ILogger` can be injected from the framework at construction time. The warning is tested via `FakeLogger` in the test suite. Future improvement: accept an `ILoggerFactory` from the host settings, or restructure construction timing.

2. **DEBT-013 DI resolution order:** With the duplicate registrations removed from `Program.cs`, the DI container now has exactly one registration for `IValueFormatterRegistry` and `ITypeDrawerRegistry` (both from `AddDdsMonitorServices`). The `Program.cs` non-headless path no longer adds competing registrations.

3. **P6-T09 macro arg count limit:** `MacroShim.Invoke` overloads support 0–5 arguments. If consumers need more, additional overloads can be added without changing the interface.

---

## Trackers updated

- `docs/plugin-api/PLA1-DEBT-TRACKER.md` — PLA1-DEBT-011 through PLA1-DEBT-015 marked ✅ Resolved (PLA1-BATCH-07)
- `docs/plugin-api/PLA1-TASK-TRACKER.md` — PLA1-P6-T06 through PLA1-P6-T09 marked ✅; Phase 6 marked complete
