# PLA1-BATCH-01 Review

**Batch:** PLA1-BATCH-01  
**Reviewer:** Development Lead  
**Date:** 2026-03-26  
**Status:** APPROVED

**Instruction file:** `.dev-workstream/batches/PLA1-BATCH-01-INSTRUCTIONS.md`  
**Report:** `.dev-workstream/reports/PLA1-BATCH-01-REPORT.md`

---

## Summary

All seven tasks (PLA1-P1-T01–T04, PLA1-P2-T01–T03) are implemented in line with `PLA1-TASK-DETAIL.md` and Phase 1–2 intent in `PLA1-DESIGN.md`. `ContextMenuItem` was moved into `DdsMonitor.Engine.Plugins` so Engine can define `IContextMenuRegistry` without a Blazor reference—a reasonable resolution of an underspecified dependency.

`dotnet test tests/DdsMonitor.Engine.Tests/` still reports **2 intermittent failures** under full parallel execution; `dotnet test tests/DdsMonitor.Plugins.ECS.Tests/` reports **4 failures** in `TimeTravelTests`. Verified locally: PLA1-related tests pass; engine failures trace to `IOException` during `MetadataReference.CreateFromFile` while enumerating loaded assemblies (another test’s DLL path under `%Temp%`); ECS failures are `Alive` vs `Dead` assertion mismatches. These are logged as **PLA1-DEBT-001** and **PLA1-DEBT-002** for the next batch—**not** attributed to incorrect PLA1 implementation.

---

## Issues Found

No issues in the **PLA1 deliverables** (interface, `MonitorContext`, DI registration, ECS migration, registry implementation).

### Test quality (minor)

**File:** `tests/DdsMonitor.Engine.Tests/Plugins/IMonitorContextTests.cs`  
**Gap:** `GetFeature_ReturnsRegisteredService` only asserts `NotNull`. Stronger check: `Assert.Same(menuRegistry, result)` so a wrong registration would fail.

**File:** `tests/DdsMonitor.Engine.Tests/Plugins/ContextMenuRegistryTests.cs`  
**Assessment:** Covers counts, multi-provider merge, exception isolation, and thread-safety with a barrier. Acceptable. Optional: assert logger received an error when a provider throws (if a test logger is cheap to wire).

**File:** `tests/DdsMonitor.Plugins.ECS.Tests/EcsPluginTests.cs`  
**Assessment:** Panel registration and “no throw without features” are solid. Menu test confirms `Plugins` → `ECS` exists but does not assert child menu labels (“Entity Grid”, “Settings”); acceptable for this batch.

---

## Verdict

**Status:** APPROVED

PLA1-BATCH-01 feature scope is complete. Full-suite green is explicitly owned by **PLA1-BATCH-02** (debt items first).

---

## Commit Message

```
feat(ddsmon): capability context GetFeature + context menu registry core (PLA1-BATCH-01)

Completes PLA1-P1-T01, PLA1-P1-T02, PLA1-P1-T03, PLA1-P1-T04, PLA1-P2-T01,
PLA1-P2-T02, PLA1-P2-T03

- IMonitorContext: single GetFeature<T>() with graceful-degradation XML docs
- MonitorContext: resolves features from IServiceProvider
- ServiceCollectionExtensions: IMonitorContext factory; IContextMenuRegistry singleton
- ECS plugin: GetFeature<IMenuRegistry>() / GetFeature<PluginPanelRegistry>() with null-conditional calls
- IContextMenuRegistry + thread-safe ContextMenuRegistry (snapshot providers, invoke outside lock)
- ContextMenuItem moved to Engine.Plugins; Blazor imports updated
- Tests: IMonitorContextTests, ContextMenuRegistryTests, EcsPluginTests; Batch28Tests updated for new API

Tests: dotnet test tests/DdsMonitor.Engine.Tests/ (2 intermittent failures under parallel run,
tracked as PLA1-DEBT-001); dotnet test tests/DdsMonitor.Plugins.ECS.Tests/ (TimeTravelTests
failures tracked as PLA1-DEBT-002)

Related: docs/plugin-api/PLA1-TASK-DETAIL.md, docs/plugin-api/PLA1-DESIGN.md §4–§5
```

---

**Next batch:** `.dev-workstream/batches/PLA1-BATCH-02-INSTRUCTIONS.md`
