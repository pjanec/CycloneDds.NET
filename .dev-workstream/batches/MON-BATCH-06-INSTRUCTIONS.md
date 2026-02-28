# MON-BATCH-06: SampleStore (DMON-010)

**Batch Number:** MON-BATCH-06  
**Tasks:** DMON-010  
**Phase:** 1 — Foundation  
**Estimated Effort:** 12-16 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-02, MON-BATCH-03, MON-BATCH-04, MON-BATCH-05

---

## ?? Onboarding & Workflow

### Developer Instructions
Implement the `SampleStore` chronological ledger with filtering and sorting behavior. Use xUnit only for new tests (no MSTest). Complete the full batch end-to-end without pausing for approvals (build, test, fix root causes).

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Developer Guide:** `.dev-workstream/guides/DEV-GUIDE.md`
3. **Code Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`
4. **DDS Monitor Onboarding:** `docs/ddsmon/ONBOARDING.md`
5. **DDS Monitor Design:** `docs/ddsmon/DESIGN.md` (§6)
6. **Task Details:** `docs/ddsmon/TASK-DETAIL.md` (DMON-010)
7. **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
8. **Previous Review:** `.dev-workstream/reviews/MON-BATCH-05-REVIEW.md`

### Source Code Location
- **Engine Library:** `tools/DdsMonitor/DdsMonitor.Engine/`
- **Test Project:** `tests/DdsMonitor.Engine.Tests/`
- **Solution File:** `CycloneDDS.NET.sln`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-06-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-06-QUESTIONS.md`

---

## Context

This batch introduces the `SampleStore` ledger with filter/sort capabilities and a virtualized view API.

**Related Task:**
- [DMON-010](../../docs/ddsmon/TASK-DETAIL.md#dmon-010--samplestore-chronological-ledger)

---

## ?? Batch Objectives

- Implement `ISampleStore` and `SampleStore` per design.
- Support filtering, sorting, and virtualized view access.
- Deliver xUnit tests covering append, filter, sorting, and thread safety.

---

## ? Tasks

### Task 1: SampleStore (DMON-010)

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/` (NEW)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-010--samplestore-chronological-ledger)

**Requirements:**
- Implement `ISampleStore`, `SampleStore`, and `ITopicSamples` as specified.
- Maintain per-topic index and filtered view.
- Implement background sort/merge worker and `OnViewRebuilt` event.
- `GetVirtualView` returns a `ReadOnlyMemory<SampleData>` slice.

**Tests Required (xUnit):**
- `SampleStore_Append_IncrementsCount`
- `SampleStore_GetTopicSamples_ReturnsOnlyMatchingTopic`
- `SampleStore_SetFilter_ReducesFilteredCount`
- `SampleStore_SetSortSpec_SortsDescending`
- `SampleStore_Clear_ResetsEverything`
- `SampleStore_GetVirtualView_ReturnsCorrectSlice`
- `SampleStore_ConcurrentAppendAndRead_DoesNotThrow`

---

## ?? Testing Requirements

- Run `dotnet build CycloneDDS.NET.sln`.
- Run `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj`.
- Fix any build or test failures without asking for permission. Complete the batch end-to-end.

---

## ?? Quality Standards

**? TEST QUALITY EXPECTATIONS**
- Use xUnit only. MSTest is not allowed for new tests.
- Tests must validate actual data ordering and filter effects.
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
- [ ] DMON-010 completed per task definition
- [ ] `dotnet build CycloneDDS.NET.sln` passes
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj` passes
- [ ] Report submitted to `.dev-workstream/reports/MON-BATCH-06-REPORT.md`

---

## ?? Common Pitfalls to Avoid

- Returning stale views after sort/filter changes.
- Failing to raise `OnViewRebuilt` on resort.
- Thread-safety gaps between append and view reads.
- Using MSTest or shallow tests.

---

## ?? Reference Materials

- **Task Defs:** `docs/ddsmon/TASK-DETAIL.md` (DMON-010)
- **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
- **Design:** `docs/ddsmon/DESIGN.md` (§6)
- **Onboarding:** `docs/ddsmon/ONBOARDING.md`
