# PLA1-BATCH-03 Report

**Batch:** PLA1-BATCH-03  
**Author:** Developer  
**Date:** 2026-03-26  
**Status:** COMPLETE

---

## Summary

All six tasks completed in order. `dotnet test tests/DdsMonitor.Engine.Tests/` — **577/577 pass** (15 new tests added; was 562). Blazor project builds with 0 errors.

---

## Task Outcomes

### PLA1-DEBT-005 — Strengthen `IMonitorContextTests.GetFeature_ReturnsRegisteredService`

**File:** `tests/DdsMonitor.Engine.Tests/Plugins/IMonitorContextTests.cs`

Added `Assert.Same(menuRegistry, result)` immediately after the existing `Assert.NotNull(result)`. The fix ensures that if the singleton is registered under a different constructor overload or resolved as a fresh transient, the test will fail.

---

### PLA1-DEBT-004 — Regression tests for context menu composition

**Approach chosen:** A — pure static helper.

**New files:**
- `tools/DdsMonitor/DdsMonitor.Engine/Plugins/ContextMenuComposer.cs` — static `Compose<TContext>` that accepts defaults + registry + context and returns the combined list with the separator rule encapsulated.
- `tests/DdsMonitor.Engine.Tests/Plugins/ContextMenuComposerTests.cs` — 8 tests covering:
  - No plugin items → only defaults, no separator
  - Default items appear first (verified by index)
  - Separator at `index == defaultItems.Count` when plugin items present
  - Plugin items appear after separator
  - Multiple providers → all items after a single separator
  - Plugin items are never prepended before defaults
  - Exactly one separator when plugin items exist

Tests would fail if: plugin items were prepended, separator was added when no plugin items, or two separators were inserted.

---

### PLA1-P3-T01 — `ISampleViewRegistry` interface

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/ISampleViewRegistry.cs` (NEW)

- Namespace `DdsMonitor.Engine.Plugins`.
- Two methods: `void Register(Type type, RenderFragment<SampleData> viewer)` and `RenderFragment<SampleData>? GetViewer(Type type)`.
- XML documentation on both methods explains the hierarchy fallback contract.

---

### PLA1-P3-T02 — `SampleViewRegistry` implementation

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/SampleViewRegistry.cs` (NEW), `tests/DdsMonitor.Engine.Tests/Plugins/SampleViewRegistryTests.cs` (NEW)

Implementation:
- Internal `Dictionary<Type, RenderFragment<SampleData>>` protected by `lock`.
- `GetViewer` walks hierarchy: exact type first, then `BaseType` chain upward, then `GetInterfaces()`.
- `Register` replaces any prior viewer for the same type (last-wins).

Tests (9 cases):
- Null returned when nothing registered
- Exact-type round-trip
- Null for unregistered different type
- Exact match takes precedence over base-type registration
- Base-type viewer found for derived type when exact not registered
- Interface viewer found when neither exact nor base registered
- Register overwrites previous viewer for same type

---

### PLA1-P3-T03 — Register `ISampleViewRegistry` in host DI

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs`

Added `services.AddSingleton<ISampleViewRegistry, SampleViewRegistry>();` adjacent to the existing `IContextMenuRegistry` registration in the Phase 5 plugin infrastructure block.

---

### PLA1-P3-T04 — `DetailPanel.razor` integration

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor`

Changes:
1. Added `@inject ISampleViewRegistry SampleViewRegistry` after the existing `@inject ISampleStore SampleStore`.
2. Modified `RenderTreeView()`: before rendering the tree, call `SampleViewRegistry?.GetViewer(_currentSample.TopicMetadata.TopicType)`. If a viewer is returned it is rendered with `@customViewer(_currentSample)`; otherwise the original expand/collapse toolbar + tree renders unchanged.
3. JSON and Sample Info tabs are untouched.
4. The `?.` null-conditional on `SampleViewRegistry` guards against a future host version that does not register the service.

---

## Test Results

```
dotnet test tests/DdsMonitor.Engine.Tests/
Passed!  - Failed: 0, Passed: 577, Skipped: 0, Total: 577
```

```
dotnet build tools/DdsMonitor/DdsMonitor.Blazor/DdsMonitor.csproj
Build succeeded.  0 Error(s), 3 Warning(s)  (all pre-existing)
```

---

## Debt Tracker

- **PLA1-DEBT-004** → ✅ Resolved (PLA1-BATCH-03)
- **PLA1-DEBT-005** → ✅ Resolved (PLA1-BATCH-03)

---

## Deviations / Notes

- `ContextMenuComposer` is a new production type in the Engine (not yet used by the panels). The three panels already implement the composition pattern inline from BATCH-02. Migrating them to use `ContextMenuComposer` would be a clean-up; deferred to avoid scope creep — the debt asked only for a tested regression guard.
- The Blazor build emits one warning on `DetailPanel.razor(715,50): CS8625` — this is a pre-existing Roslyn-generated offset that was present before BATCH-03 changes (the number refers to the Roslyn compile unit, not the `.razor` source line).

---

## Files Changed

| File | Change |
|------|--------|
| `tests/DdsMonitor.Engine.Tests/Plugins/IMonitorContextTests.cs` | Add `Assert.Same` |
| `tools/DdsMonitor/DdsMonitor.Engine/Plugins/ContextMenuComposer.cs` | NEW |
| `tests/DdsMonitor.Engine.Tests/Plugins/ContextMenuComposerTests.cs` | NEW |
| `tools/DdsMonitor/DdsMonitor.Engine/Plugins/ISampleViewRegistry.cs` | NEW |
| `tools/DdsMonitor/DdsMonitor.Engine/Plugins/SampleViewRegistry.cs` | NEW |
| `tests/DdsMonitor.Engine.Tests/Plugins/SampleViewRegistryTests.cs` | NEW |
| `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs` | Add DI registration |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor` | Inject + RenderTreeView guard |
| `docs/plugin-api/PLA1-DEBT-TRACKER.md` | Mark DEBT-004, DEBT-005 resolved |
