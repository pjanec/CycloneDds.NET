# MON-BATCH-05: DDS bridge service (DMON-009)

**Batch Number:** MON-BATCH-05  
**Tasks:** DMON-009  
**Phase:** 1 — Foundation  
**Estimated Effort:** 12-14 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-02, MON-BATCH-03, MON-BATCH-04

---

## ?? Onboarding & Workflow

### Developer Instructions
Implement the DDS bridge service that manages dynamic readers/writers and partition changes. Use xUnit only for new tests (no MSTest). Complete the full batch end-to-end without pausing for approvals (build, test, fix root causes).

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Developer Guide:** `.dev-workstream/guides/DEV-GUIDE.md`
3. **Code Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`
4. **DDS Monitor Onboarding:** `docs/ddsmon/ONBOARDING.md`
5. **DDS Monitor Design:** `docs/ddsmon/DESIGN.md` (§4.3, §4.4)
6. **Task Details:** `docs/ddsmon/TASK-DETAIL.md` (DMON-009)
7. **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
8. **Previous Review:** `.dev-workstream/reviews/MON-BATCH-04-REVIEW.md`

### Source Code Location
- **Engine Library:** `tools/DdsMonitor/DdsMonitor.Engine/`
- **Test Project:** `tests/DdsMonitor.Engine.Tests/`
- **Solution File:** `CycloneDDS.NET.sln`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-05-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-05-QUESTIONS.md`

---

## Context

This batch introduces `DdsBridge`, the runtime coordinator that manages `DdsParticipant`, dynamic readers, and partition changes.

**Related Task:**
- [DMON-009](../../docs/ddsmon/TASK-DETAIL.md#dmon-009--ddsbridge-service)

---

## ?? Batch Objectives

- Implement `IDdsBridge` and `DdsBridge` per design.
- Support subscribe/unsubscribe and writer creation.
- Implement partition change behavior that rebuilds readers.
- Deliver xUnit tests that validate subscription lifecycle and partition handling.

---

## ? Tasks

### Task 1: DdsBridge service (DMON-009)

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/` (NEW)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-009--ddsbridge-service)

**Requirements:**
- Implement `IDdsBridge` and `DdsBridge`.
- Maintain `ActiveReaders` mapping by topic type.
- `Subscribe`/`Unsubscribe` manage reader lifecycle.
- `GetWriter` returns `IDynamicWriter` for the topic.
- `ChangePartition` recreates all active readers with new partition while preserving subscribed topics.
- Keep the `DdsParticipant` stable across partition changes.

**Tests Required (xUnit):**
- `DdsBridge_Subscribe_CreatesReader`
- `DdsBridge_Unsubscribe_RemovesReader`
- `DdsBridge_ChangePartition_RecreatesReaders`

---

## ?? Testing Requirements

- Run `dotnet build CycloneDDS.NET.sln`.
- Run `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj`.
- Fix any build or test failures without asking for permission. Complete the batch end-to-end.

---

## ?? Quality Standards

**? TEST QUALITY EXPECTATIONS**
- Use xUnit only. MSTest is not allowed for new tests.
- Tests must validate actual lifecycle behavior (reader instances created/recreated).
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
- [ ] DMON-009 completed per task definition
- [ ] `dotnet build CycloneDDS.NET.sln` passes
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj` passes
- [ ] Report submitted to `.dev-workstream/reports/MON-BATCH-05-REPORT.md`

---

## ?? Common Pitfalls to Avoid

- Reusing disposed readers after partition change.
- Forgetting to update `ActiveReaders` when unsubscribing.
- Creating new `DdsParticipant` on partition change (must be stable).
- Using MSTest or shallow tests.

---

## ?? Reference Materials

- **Task Defs:** `docs/ddsmon/TASK-DETAIL.md` (DMON-009)
- **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
- **Design:** `docs/ddsmon/DESIGN.md` (§4.3, §4.4)
- **Onboarding:** `docs/ddsmon/ONBOARDING.md`
