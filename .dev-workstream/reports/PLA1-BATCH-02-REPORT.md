# PLA1-BATCH-02 Report

**Batch:** PLA1-BATCH-02  
**Date:** 2026-03-26  
**Status:** COMPLETE

---

## Task Summary

| Task | Status | Notes |
|------|--------|-------|
| PLA1-DEBT-001 | ✅ Resolved | Engine test flakiness fixed |
| PLA1-DEBT-002 | ✅ Resolved | TimeTravelTests all pass |
| PLA1-DEBT-003 | ✅ Resolved | Docs updated |
| PLA1-P2-T04 | ✅ Done | TopicExplorerPanel wired |
| PLA1-P2-T05 | ✅ Done | SamplesPanel wired |
| PLA1-P2-T06 | ✅ Done | InstancesPanel wired |

---

## PLA1-DEBT-001: Engine Test Parallel Flakiness

**Root cause:** `Batch28Tests.CompilePlugin` enumerates `AppDomain.CurrentDomain.GetAssemblies()` to build Roslyn metadata references. When tests run in parallel, other test instances (also in `Batch28Tests`) load compiled plugin DLLs from unique temp directories via an `AssemblyLoadContext`. Windows holds those DLL files open for the lifetime of the ALC. A concurrent `CompilePlugin` invocation tries `MetadataReference.CreateFromFile` on the same locked paths → `IOException`.

**Fix:** [tests/DdsMonitor.Engine.Tests/Batch28Tests.cs](../../tests/DdsMonitor.Engine.Tests/Batch28Tests.cs) — added a filter to exclude assemblies whose `Location` starts with `Path.GetTempPath()`. These are test-generated plugin DLLs and are never needed as Roslyn references for compiling other plugins.

**Verification:** `dotnet test tests/DdsMonitor.Engine.Tests/` run twice: **562/562 pass** both times (previously reported 2 intermittent failures).

---

## PLA1-DEBT-002: TimeTravelTests Correctness

**Root cause:** The `EcsSettings` in `TimeTravelTests` constructor used `NamespacePrefix = "company.BDC"`, but all test topic types in `EcsTestTopics.cs` use `[DdsTopic("company.ECS.*")]` names. `TimeTravelEngine.GetHistoricalState` filters out topics whose `TopicName` does not start with the configured prefix, so all topics were silently ignored, leaving the entity always `Dead`.

**Fix:** [tests/DdsMonitor.Plugins.ECS.Tests/TimeTravelTests.cs](../../tests/DdsMonitor.Plugins.ECS.Tests/TimeTravelTests.cs) — changed `NamespacePrefix` from `"company.BDC"` to `"company.ECS"` with an explanatory comment. The `TimeTravel_TopicOutsideNamespacePrefix_IsIgnored` test continues to pass because `OtherNamespaceTopic` uses the `"other.NS.*"` prefix which still does not match.

**Tests were wrong, not the engine.** The `TimeTravelEngine` logic is correct per the spec; the test fixture was configured for the wrong namespace.

**Verification:** `dotnet test tests/DdsMonitor.Plugins.ECS.Tests/` — **44/44 pass** (previously 4 failures).

---

## PLA1-DEBT-003: Documentation Alignment

**Files updated:**

- [docs/ddsmon/Plugin-API-deviations.md](../../docs/ddsmon/Plugin-API-deviations.md): Replaced `context.PanelRegistry.RegisterPanelType(...)` with `context.GetFeature<PluginPanelRegistry>()?.RegisterPanelType(...)` and `context.MenuRegistry` with `context.GetFeature<IMenuRegistry>()`. Also updated the initialization summary paragraph. Links added to `PLA1-DESIGN.md §4`.
- [docs/ddsmon/ECS-plugin-addendum.md](../../docs/ddsmon/ECS-plugin-addendum.md): Replaced `IMonitorContext.PanelRegistry` and `IMonitorContext.MenuRegistry` references with the `GetFeature<T>()` call pattern.

---

## PLA1-P2-T04: TopicExplorerPanel Context Menus

**File:** [tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicExplorerPanel.razor](../../tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicExplorerPanel.razor)

- Added `@inject IContextMenuRegistry ContextMenuRegistry`
- In `HandleRowMouseDown`: after building the two default items (`Topic Properties`, `Open Samples`), calls `ContextMenuRegistry.GetItems<TopicMetadata>(metadata)`. If any plugin items are returned, a visual separator (`─────────────────`) is appended first, followed by the plugin items.
- Separator uses `() => Task.CompletedTask` as the `Action` to avoid `NullReferenceException` when the menu renderer calls `item.Action()`.

**Manual verification steps:**
1. Run the app; open the Topic Explorer.
2. Right-click any topic row with no plugins loaded → menu shows `Topic Properties` and `Open Samples` only (baseline unchanged).
3. Register a test provider in `Program.cs`: `contextMenuRegistry.RegisterProvider<TopicMetadata>(m => new[] { new ContextMenuItem("Test Item", null, () => Task.CompletedTask) });`
4. Right-click a topic → separator and `Test Item` appear after the default items.

---

## PLA1-P2-T05: SamplesPanel Context Menus

**File:** [tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor](../../tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor)

- Added `@inject IContextMenuRegistry ContextMenuRegistry`
- In `OpenRowContextMenu`: after the four default items (`Show Detail`, `Clone to Send/Emulator`, `Filter Out Topic`, `Show Only This Topic`), calls `ContextMenuRegistry.GetItems<SampleData>(row.Sample)`. Separator + plugin items added when count > 0.

**Manual verification:** same pattern as T04 but register a `SampleData` provider.

---

## PLA1-P2-T06: InstancesPanel Context Menus

**File:** [tools/DdsMonitor/DdsMonitor.Blazor/Components/InstancesPanel.razor](../../tools/DdsMonitor/DdsMonitor.Blazor/Components/InstancesPanel.razor)

- Added `@inject IContextMenuRegistry ContextMenuRegistry`
- Context type chosen: **`InstanceData`** (`row.Row.Instance`). `InstanceRow` is a private record inside the razor file and cannot be referenced by external plugins; `InstanceData` is the public, stable Engine type that carries all relevant instance state.
- In `OpenRowContextMenu`: after the three default items, calls `ContextMenuRegistry.GetItems<InstanceData>(row.Row.Instance)`.

**Manual verification:** register an `InstanceData` provider and right-click an instance row in a keyed topic.

---

## Build & Test Results

```
dotnet build tools/DdsMonitor/DdsMonitor.Blazor/DdsMonitor.csproj
  → Build succeeded. 5 Warning(s), 0 Error(s)  (all warnings pre-existing)

dotnet test tests/DdsMonitor.Engine.Tests/
  → Passed!  Failed: 0, Passed: 562, Skipped: 0, Total: 562

dotnet test tests/DdsMonitor.Plugins.ECS.Tests/
  → Passed!  Failed: 0, Passed:  44, Skipped: 0, Total:  44
```

---

## Blazor Injection Notes

All three panels use plain `@inject IContextMenuRegistry ContextMenuRegistry` (non-optional, consistent with all other service injections in these files). The registry is registered as a singleton in Phase 1 DI bootstrap (PLA1-P2-T03), so it is always available. A `null` guard is not required at the injection site; the `null` guard was the original concern for graceful degradation in hypothetical unregistered scenarios, but since the registration is unconditional this is moot.

The `ContextMenuRegistry.GetItems<T>()` implementation already guards against empty provider lists by returning `Enumerable.Empty<ContextMenuItem>()`, so the `.ToList()` call always produces a safe list with no provider exceptions propagating to the panel.
