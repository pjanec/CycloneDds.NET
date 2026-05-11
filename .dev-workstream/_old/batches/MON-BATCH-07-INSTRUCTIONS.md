# MON-BATCH-07: SampleStore fixes + InstanceStore + FilterCompiler + Ingestion (DMON-010/011/012/013)

**Batch Number:** MON-BATCH-07  
**Tasks:** DMON-010 (fix), DMON-011, DMON-012, DMON-013  
**Phase:** 1 — Foundation  
**Estimated Effort:** 20-26 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-06

---

## ?? Onboarding & Workflow

### Developer Instructions
This batch combines a corrective fix for `SampleStore` with three new foundation services. Use xUnit only for new tests (no MSTest). Complete the full batch end-to-end without pausing for approvals (build, test, fix root causes).

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Developer Guide:** `.dev-workstream/guides/DEV-GUIDE.md`
3. **Code Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`
4. **DDS Monitor Onboarding:** `docs/ddsmon/ONBOARDING.md`
5. **DDS Monitor Design:** `docs/ddsmon/DESIGN.md` (§6, §7, §6.3, §4.1)
6. **Task Details:** `docs/ddsmon/TASK-DETAIL.md` (DMON-010/011/012/013)
7. **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
8. **Previous Review:** `.dev-workstream/reviews/MON-BATCH-06-REVIEW.md`

### Source Code Location
- **Engine Library:** `tools/DdsMonitor/DdsMonitor.Engine/`
- **Test Project:** `tests/DdsMonitor.Engine.Tests/`
- **Solution File:** `CycloneDDS.NET.sln`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-07-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-07-QUESTIONS.md`

---

## ?? MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 0:** Fix ? Write tests ? **ALL tests pass** ?
2. **Task 1:** Implement ? Write tests ? **ALL tests pass** ?
3. **Task 2:** Implement ? Write tests ? **ALL tests pass** ?
4. **Task 3:** Implement ? Write tests ? **ALL tests pass** ?

**DO NOT** move to the next task until:
- ? Current task implementation complete
- ? Current task tests written
- ? **ALL tests passing** (including previous batch tests)

**Why:** Ensures each component is solid before building on top of it. Prevents cascading failures.

---

## Context

This batch fixes SampleStore design adherence and adds InstanceStore, FilterCompiler, and the ingestion background service.

**Related Tasks:**
- [DMON-010](../../docs/ddsmon/TASK-DETAIL.md#dmon-010--samplestore-chronological-ledger)
- [DMON-011](../../docs/ddsmon/TASK-DETAIL.md#dmon-011--instancestore-keyed-instance-tracking)
- [DMON-012](../../docs/ddsmon/TASK-DETAIL.md#dmon-012--filtercompiler-dynamic-linq)
- [DMON-013](../../docs/ddsmon/TASK-DETAIL.md#dmon-013--ddsingestionservice-background-worker)

---

## ?? Batch Objectives

- Replace SampleStore full snapshot sort with incremental merge-sort worker.
- Implement InstanceStore keyed lifecycle tracking and observable transitions.
- Implement FilterCompiler using Dynamic LINQ with payload field access.
- Implement DdsIngestionService background worker.

---

## ? Tasks

### Task 0: Corrective — SampleStore merge-sort worker (DMON-010 fix)

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/SampleStore.cs` (UPDATE)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-010--samplestore-chronological-ledger)

**Requirements:**
- Replace full snapshot sort with incremental merge-sort worker as described in `docs/ddsmon/DESIGN.md` §6.2.
- New arrivals should be sorted and merged into the existing sorted view.
- Preserve existing API surface and thread-safety guarantees.

**Tests Required (xUnit):**
- Add `SampleStore_MergeSort_MergesNewArrivals` (verify ordered merge when new samples arrive after initial sort)

---

### Task 1: InstanceStore (DMON-011)

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/` (NEW)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-011--instancestore-keyed-instance-tracking)

**Tests Required (xUnit):**
- `InstanceStore_NewKey_CreatesAliveInstance`
- `InstanceStore_DisposeKey_MarksAsDead`
- `InstanceStore_RebirthKey_ResetsCounters`
- `InstanceStore_FiresTransitionEvents`
- `InstanceStore_ExtractsCompositeKey`

---

### Task 2: FilterCompiler (DMON-012)

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/` (NEW)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-012--filtercompiler-dynamic-linq)

**Tests Required (xUnit):**
- `FilterCompiler_SimpleExpression_Compiles`
- `FilterCompiler_Predicate_FiltersCorrectly`
- `FilterCompiler_InvalidExpression_ReturnsError`
- `FilterCompiler_PayloadFieldAccess_Works`

---

### Task 3: DdsIngestionService (DMON-013)

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/` (NEW)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-013--ddsingestionservice-background-worker)

**Tests Required (xUnit):**
- `IngestionService_ProcessesSamplesFromChannel`
- `IngestionService_RoutesKeyedSamplesToInstanceStore`
- `IngestionService_StopsGracefullyOnCancellation`

---

## ?? Testing Requirements

- Run `dotnet build CycloneDDS.NET.sln`.
- Run `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj`.
- Fix any build or test failures without asking for permission. Complete the batch end-to-end.

---

## ?? Quality Standards

**? TEST QUALITY EXPECTATIONS**
- Use xUnit only. MSTest is not allowed for new tests.
- Tests must validate actual behavior (state transitions, predicate results, merge ordering).
- Do not rely on string-presence checks for correctness.

**? REPORT QUALITY EXPECTATIONS**
- Document issues encountered and how you resolved them.
- Document any design decisions you made beyond the instructions.
- Note any edge cases or follow-up work needed.

---

## ?? Report Requirements

## Developer Insights

**Q1:** What issues did you encounter during implementation? How did you resolve them?

**Q2:** Did you spot any weak points in the existing codebase? What would you improve?

**Q3:** What design decisions did you make beyond the instructions? What alternatives did you consider?

**Q4:** What edge cases did you discover that weren't mentioned in the spec?

**Q5:** Are there any performance concerns or optimization opportunities you noticed?

---

## ?? Success Criteria

This batch is DONE when:
- [ ] DMON-010 fix completed per task definition
- [ ] DMON-011 completed per task definition
- [ ] DMON-012 completed per task definition
- [ ] DMON-013 completed per task definition
- [ ] `dotnet build CycloneDDS.NET.sln` passes
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj` passes
- [ ] Report submitted to `.dev-workstream/reports/MON-BATCH-07-REPORT.md`

---

## ?? Common Pitfalls to Avoid

- Implementing another full snapshot sort instead of merge-sort.
- Missing transition events in InstanceStore.
- Filtering payload fields without using `FieldMetadata.Getter`.
- Blocking in `DdsIngestionService` hot path.

---

## ?? Reference Materials

- **Task Defs:** `docs/ddsmon/TASK-DETAIL.md` (DMON-010/011/012/013)
- **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
- **Design:** `docs/ddsmon/DESIGN.md` (§6, §7, §6.3, §4.1)
- **Onboarding:** `docs/ddsmon/ONBOARDING.md`
