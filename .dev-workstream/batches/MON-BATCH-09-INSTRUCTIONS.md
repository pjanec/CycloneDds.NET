# MON-BATCH-09: Desktop verification + Topic Explorer + Topic Picker + Column Picker (DMON-017 fix/018/019/020)

**Batch Number:** MON-BATCH-09  
**Tasks:** DMON-017 (fix), DMON-018, DMON-019, DMON-020  
**Phase:** 2 — Blazor Shell & Core UI  
**Estimated Effort:** 20-28 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-08

---

## ?? Onboarding & Workflow

### Developer Instructions
This batch completes manual verification for the desktop shell and adds the Topic Explorer, Topic Picker, and Column Picker. Use xUnit only for new tests (no MSTest). Complete the full batch end-to-end without pausing for approvals (build, test, fix root causes).

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Developer Guide:** `.dev-workstream/guides/DEV-GUIDE.md`
3. **Code Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`
4. **DDS Monitor Onboarding:** `docs/ddsmon/ONBOARDING.md`
5. **DDS Monitor Design:** `docs/ddsmon/DESIGN.md` (§8, §10.1–§10.3)
6. **Task Details:** `docs/ddsmon/TASK-DETAIL.md` (DMON-017/018/019/020)
7. **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
8. **Previous Review:** `.dev-workstream/reviews/MON-BATCH-08-REVIEW.md`

### Source Code Location
- **Blazor Host:** `tools/DdsMonitor/`
- **Engine Library:** `tools/DdsMonitor/DdsMonitor.Engine/`
- **Test Project:** `tests/DdsMonitor.Engine.Tests/`
- **Solution File:** `CycloneDDS.NET.sln`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-09-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-09-QUESTIONS.md`

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

This batch finishes the desktop shell verification and adds core UI panels for topic discovery and column configuration.

**Related Tasks:**
- [DMON-017](../../docs/ddsmon/TASK-DETAIL.md#dmon-017--desktoprazor-shell--panel-chrome)
- [DMON-018](../../docs/ddsmon/TASK-DETAIL.md#dmon-018--topic-explorer-panel)
- [DMON-019](../../docs/ddsmon/TASK-DETAIL.md#dmon-019--topic-picker-reusable-component)
- [DMON-020](../../docs/ddsmon/TASK-DETAIL.md#dmon-020--column-picker-dialog)

---

## ?? Batch Objectives

- Execute and document the DMON-017 manual tests.
- Implement Topic Explorer panel with tri-state filters and live stats.
- Implement Topic Picker reusable component with keyboard navigation.
- Implement Column Picker dialog with dual-list and ordering.

---

## ? Tasks

### Task 0: Corrective — DMON-017 manual verification

**Files:** `.dev-workstream/reports/MON-BATCH-09-REPORT.md` (UPDATE)  
**Description:** Run the manual checks required by DMON-017 and document results in the report.

**Manual Test Steps:**
- Run `dotnet run --project tools/DdsMonitor/DdsMonitor.csproj`.
- Verify drag: panels move with title bar drag.
- Verify resize: edges/corners resize with handles.
- Verify z-order: clicking background panel brings it to front.
- Verify minimize: title-bar button collapses to bottom strip; click strip restores.
- Verify close: close button removes panel.

---

### Task 1: Topic Explorer panel (DMON-018)

**Files:** `tools/DdsMonitor/` (NEW/UPDATE)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-018--topic-explorer-panel)

**Tests Required (xUnit):**
- `TopicExplorerPanel_TriStateFilter_CyclesCorrectly`

---

### Task 2: Topic Picker (DMON-019)

**Files:** `tools/DdsMonitor/` (NEW/UPDATE)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-019--topic-picker-reusable-component)

**Tests Required (xUnit):**
- `TopicPicker_FiltersOnKeystroke`
- `TopicPicker_MatchesBothNameAndNamespace`

---

### Task 3: Column Picker dialog (DMON-020)

**Files:** `tools/DdsMonitor/` (NEW/UPDATE)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-020--column-picker-dialog)

**Tests Required (xUnit):**
- `ColumnPicker_AddField_MovesToSelected`
- `ColumnPicker_RemoveField_MovesToAvailable`
- `ColumnPicker_Apply_ReturnsSelectedOrder`

---

## ?? Testing Requirements

- Run `dotnet build CycloneDDS.NET.sln`.
- Run `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj`.
- Fix any build or test failures without asking for permission. Complete the batch end-to-end.

---

## ?? Quality Standards

**? TEST QUALITY EXPECTATIONS**
- Use xUnit only. MSTest is not allowed for new tests.
- Tests must validate actual behavior (filter state transitions, list movement, selection order).
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
- [ ] DMON-017 manual verification documented
- [ ] DMON-018 completed per task definition
- [ ] DMON-019 completed per task definition
- [ ] DMON-020 completed per task definition
- [ ] `dotnet build CycloneDDS.NET.sln` passes
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj` passes
- [ ] Report submitted to `.dev-workstream/reports/MON-BATCH-09-REPORT.md`

---

## ?? Common Pitfalls to Avoid

- Skipping manual verification for desktop shell.
- Failing to debounce input for Topic Picker filtering.
- Not preserving ordering in Column Picker Apply.
- Using MSTest or shallow tests.

---

## ?? Reference Materials

- **Task Defs:** `docs/ddsmon/TASK-DETAIL.md` (DMON-017/018/019/020)
- **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
- **Design:** `docs/ddsmon/DESIGN.md` (§8, §10.1–§10.3)
- **Onboarding:** `docs/ddsmon/ONBOARDING.md`
