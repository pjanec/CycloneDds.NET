# PLA1-BATCH-02: Test suite hardening + context menus in panels

**Batch Number:** PLA1-BATCH-02  
**Tasks:** PLA1-DEBT-001, PLA1-DEBT-002 (corrective), PLA1-P2-T04, PLA1-P2-T05, PLA1-P2-T06  
**Phase:** PLA1 Phase 2 (complete panel integration)  
**Estimated Effort:** 14–18 hours  
**Priority:** HIGH  
**Dependencies:** PLA1-BATCH-01 (approved)

---

## 📋 Onboarding & Workflow

### Developer Instructions

Start with **debt items** so `dotnet test` for Engine and ECS projects is reliable and green. Then wire `IContextMenuRegistry` into the three Blazor panels per `PLA1-TASK-DETAIL.md`.

### Required reading (in order)

1. **Workflow:** `.dev-workstream/guides/DEV-GUIDE.md`
2. **Onboarding:** `docs/plugin-api/PLA1-ONBOARDING.md`
3. **Previous review:** `.dev-workstream/reviews/PLA1-BATCH-01-REVIEW.md`
4. **Debt:** `docs/plugin-api/PLA1-DEBT-TRACKER.md` — PLA1-DEBT-001 through PLA1-DEBT-003
5. **Design:** `docs/plugin-api/PLA1-DESIGN.md` — [§5 Phase 2](../../docs/plugin-api/PLA1-DESIGN.md#5-phase-2--context-menu-registry)
6. **Task specs:** `docs/plugin-api/PLA1-TASK-DETAIL.md` — P2-T04, P2-T05, P2-T06 and debt rows above
7. **Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`

### Source code locations

| Work | Path |
|------|------|
| Engine tests / `Batch28Tests` | `tests/DdsMonitor.Engine.Tests/Batch28Tests.cs` (and related) |
| ECS time-travel tests | `tests/DdsMonitor.Plugins.ECS.Tests/TimeTravelTests.cs` + implementation under `tools/DdsMonitor/DdsMonitor.Plugins.ECS/` |
| Topic / Samples / Instances panels | `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicExplorerPanel.razor`, `SamplesPanel.razor`, `InstancesPanel.razor` |
| Context menu host | `tools/DdsMonitor/DdsMonitor.Blazor/Services/ContextMenuService.cs` |
| Outdated plugin API docs | `docs/ddsmon/Plugin-API-deviations.md`, `docs/ddsmon/ECS-plugin-addendum.md` |

### Builds and tests

```powershell
dotnet build tools/DdsMonitor/DdsMonitor.Blazor/DdsMonitor.csproj
dotnet test tests/DdsMonitor.Engine.Tests/
dotnet test tests/DdsMonitor.Plugins.ECS.Tests/
```

**Success for debt slice:** both test projects **0 failures** in a normal full run (parallel default).

### Report / questions

- **Report:** `.dev-workstream/reports/PLA1-BATCH-02-REPORT.md`
- **Questions:** `.dev-workstream/questions/PLA1-BATCH-02-QUESTIONS.md`

### Anti-laziness

Run the **full** affected test projects each time; fix root cause until green. Document any intentional test semantic change in the report.

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

1. **PLA1-DEBT-001** → `dotnet test tests/DdsMonitor.Engine.Tests/` all pass ✅  
2. **PLA1-DEBT-002** → `dotnet test tests/DdsMonitor.Plugins.ECS.Tests/` all pass ✅  
3. **PLA1-DEBT-003** (documentation) → docs updated ✅  
4. **PLA1-P2-T04** → build + targeted manual or automated check if applicable → **all tests still pass** ✅  
5. **PLA1-P2-T05** → same ✅  
6. **PLA1-P2-T06** → same ✅  

Do not start panel integration until debt **001** and **002** are fixed unless you unblock with an explicit, reviewed approach in the questions file (default: fix debt first).

---

## Context

Batch 01 delivered the registry and DI; hosts still need to **call** `GetItems` from real UI. Parallel test instability and broken `TimeTravelTests` undermine CI and must not accumulate.

---

## ✅ Tasks

### Task 0 — PLA1-DEBT-001: Engine test parallel / metadata reference failures

**Problem:** See `docs/plugin-api/PLA1-DEBT-TRACKER.md`.

**Goal:** `dotnet test tests/DdsMonitor.Engine.Tests/` succeeds reliably (including parallel execution).

**Hints (choose what fits; do not duplicate long rationale here):**

- xUnit `[Collection]` + `DisableParallelization` for Roslyn compile tests, **or**
- Avoid enumerating every loaded assembly with `CreateFromFile` without shared read / copy, **or**
- Unique temp output + ensure no cross-test file contention.

**Verify:** Run the full Engine.Tests project at least twice locally.

---

### Task 1 — PLA1-DEBT-002: `TimeTravelTests` correctness

**Problem:** Four tests expect `Alive` but observe `Dead`.

**Goal:** Align `TimeTravelEngine` (or test fixtures) with documented entity journal semantics so all four tests pass. If the tests are wrong, justify and update **tests** with a short comment citing the correct rule.

**Files:** Start from `tests/DdsMonitor.Plugins.ECS.Tests/TimeTravelTests.cs` stack traces; trace into `TimeTravelEngine` and entity journal types.

---

### Task 2 — PLA1-DEBT-003: Documentation alignment (P3)

**Goal:** Update `docs/ddsmon/Plugin-API-deviations.md` and `docs/ddsmon/ECS-plugin-addendum.md` so they describe `GetFeature<IMenuRegistry>()` / `GetFeature<PluginPanelRegistry>()`, not removed properties.

Keep edits minimal; link to `docs/plugin-api/PLA1-DESIGN.md` where useful.

---

### Task 3 — PLA1-P2-T04: `TopicExplorerPanel.razor`

**Task definition:** [PLA1-TASK-DETAIL.md](../../docs/plugin-api/PLA1-TASK-DETAIL.md#pla1-p2-t04-integrate-context-menus-into-topicexplorerpanelrazor)

Inject `IContextMenuRegistry` optional if consistent with host patterns. On row right-click, append `GetItems<TopicMetadata>(topicMeta)` with separator only when plugin items exist.

---

### Task 4 — PLA1-P2-T05: `SamplesPanel.razor`

**Task definition:** [PLA1-TASK-DETAIL.md](../../docs/plugin-api/PLA1-TASK-DETAIL.md#pla1-p2-t05-integrate-context-menus-into-samplespanelrazor)

Default items stay first; then plugin items for `SampleData`.

---

### Task 5 — PLA1-P2-T06: `InstancesPanel.razor`

**Task definition:** [PLA1-TASK-DETAIL.md](../../docs/plugin-api/PLA1-TASK-DETAIL.md#pla1-p2-t06-integrate-context-menus-into-instancespanelrazor)

Use the correct row context type (`InstanceData` or the type the panel actually uses—match real handler).

---

## 🧪 Panel / integration testing

- Where possible, add or extend tests so plugin-provided items are observable (e.g. engine-level registry tests already exist; bUnit may be out of scope until Phase 8—if no UI test, document **manual** verification steps in the report: register a test provider from a test-only path or describe a minimal dev scenario).

---

## 📊 Report requirements

Answer: what fixed the flakiness; root cause of TimeTravel mismatch; any Blazor injection pitfalls; panel behaviors verified.

---

## 🎯 Success criteria

- [ ] PLA1-DEBT-001 and PLA1-DEBT-002: **Resolved** in `docs/plugin-api/PLA1-DEBT-TRACKER.md`
- [ ] PLA1-DEBT-003: docs updated
- [ ] PLA1-P2-T04, T05, T06: success criteria in task detail met
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/` and `dotnet test tests/DdsMonitor.Plugins.ECS.Tests/` pass
- [ ] Report: `.dev-workstream/reports/PLA1-BATCH-02-REPORT.md`

---

## 📚 References

- `docs/plugin-api/PLA1-TASK-TRACKER.md`
- `.dev-workstream/reviews/PLA1-BATCH-01-REVIEW.md`
