# PLA1-BATCH-02 Review

**Batch:** PLA1-BATCH-02  
**Reviewer:** Development Lead  
**Date:** 2026-03-26  
**Status:** APPROVED

**Report:** `.dev-workstream/reports/PLA1-BATCH-02-REPORT.md`

---

## Summary

Debt items **PLA1-DEBT-001–003** and feature tasks **PLA1-P2-T04–T06** are implemented as described. `Batch28Tests.CompilePlugin` now skips metadata references for assemblies under `%TEMP%`, which removes parallel `IOException`s without affecting compile needs for test plugins. `TimeTravelTests` fixture uses `NamespacePrefix` aligned with `EcsTestTopics` — a legitimate test fix, consistent with `TimeTravelEngine` behavior. All three panels call `IContextMenuRegistry.GetItems<T>()` with the correct context types, default host items first, and a separator only when plugin items exist.

Verified locally: **`dotnet test tests/DdsMonitor.Engine.Tests/`** — 562/562 pass; **`dotnet test tests/DdsMonitor.Plugins.ECS.Tests/`** — 44/44 pass.

---

## Issues Found

No blocking issues.

### Minor / follow-up (not rejecting)

1. **Automated tests for panel wiring:** P2-T04–T06 rely on manual verification in the report. Regression risk if someone refactors `OpenRowContextMenu` / `HandleRowMouseDown`. Logged as **PLA1-DEBT-004** for PLA1-BATCH-03 (see debt tracker).

2. **Optional injection vs batch text:** Instructions mentioned optional `IContextMenuRegistry`; implementation uses required `@inject` because DI always registers the singleton. Acceptable; aligns with current host.

3. **Separator row:** Implemented as a `ContextMenuItem` with a dashed label and no-op action. Works with existing `ContextMenuService`; if UX later needs a non-clickable separator component, that can be a separate UI debt.

---

## Test / design alignment

- **DEBT-001:** Fix targets the real failure mode (`CreateFromFile` on locked temp DLLs). Slightly broad filter (exclude all assemblies under temp) is acceptable for this test harness; production assemblies are not loaded from `%TEMP%` as compilation references.
- **DEBT-002:** Root cause analysis matches code path (namespace prefix vs topic names).
- **P2 panels:** Match **PLA1-DESIGN.md** §5.3 (defaults → `GetItems` → separator + plugin items). `TopicMetadata` / `SampleData` / `InstanceData` match the design table; `InstanceData` for Instances panel is the correct stable type for plugins.

---

## Verdict

**Status:** APPROVED

---

## Commit Message

```
fix(test): stabilize Batch28 Roslyn refs; align TimeTravel ECS fixture; feat(ddsmon): context menus in panels (PLA1-BATCH-02)

Completes PLA1-DEBT-001, PLA1-DEBT-002, PLA1-DEBT-003, PLA1-P2-T04, PLA1-P2-T05, PLA1-P2-T06

- Batch28Tests: skip temp-dir assemblies when building Roslyn MetadataReferences (parallel-safe)
- TimeTravelTests: EcsSettings.NamespacePrefix matches ECS test topics (company.ECS)
- Docs: Plugin-API-deviations.md, ECS-plugin-addendum.md — GetFeature<T>() panel/menu pattern
- TopicExplorerPanel, SamplesPanel, InstancesPanel: IContextMenuRegistry.GetItems after defaults;
  separator only when plugin items present

Tests: Engine.Tests 562/562; Plugins.ECS.Tests 44/44

Related: docs/plugin-api/PLA1-TASK-DETAIL.md (P2-T04–T06), PLA1-DESIGN.md §5
```

---

**Next batch:** `.dev-workstream/batches/PLA1-BATCH-03-INSTRUCTIONS.md`
