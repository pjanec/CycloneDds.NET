# MON-BATCH-13: UI validation + core panel tests + keyboard nav + context menus (DMON-017 fix/021/022/023/024/025/026)

**Batch Number:** MON-BATCH-13  
**Tasks:** DMON-017 (fix), DMON-021 (tests), DMON-022 (tests), DMON-023 (tests), DMON-024 (manual), DMON-025, DMON-026  
**Phase:** 2 — Blazor Shell & Core UI  
**Estimated Effort:** 22-30 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-12

---

## ?? Onboarding & Workflow

### Developer Instructions
This batch completes pending manual verification, adds missing tests for core panels, and implements keyboard navigation + context menus. Use xUnit only for new tests (no MSTest). Complete the full batch end-to-end without pausing for approvals (build, test, fix root causes).

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Developer Guide:** `.dev-workstream/guides/DEV-GUIDE.md`
3. **Code Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`
4. **DDS Monitor Onboarding:** `docs/ddsmon/ONBOARDING.md`
5. **DDS Monitor Design:** `docs/ddsmon/DESIGN.md` (§8, §10.4–§10.6)
6. **Task Details:** `docs/ddsmon/TASK-DETAIL.md` (DMON-017/021/022/023/024/025/026)
7. **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
8. **Previous Review:** `.dev-workstream/reviews/MON-BATCH-12-REVIEW.md`

### Source Code Location
- **Blazor Host:** `tools/DdsMonitor/`
- **Engine Library:** `tools/DdsMonitor/DdsMonitor.Engine/`
- **Test Project:** `tests/DdsMonitor.Engine.Tests/`
- **Solution File:** `CycloneDDS.NET.sln`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-13-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-13-QUESTIONS.md`

---

## ?? MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 0:** Fix ? Write tests ? **ALL tests pass** ?
2. **Task 1:** Implement ? Write tests ? **ALL tests pass** ?
3. **Task 2:** Implement ? Write tests ? **ALL tests pass** ?
4. **Task 3:** Implement ? Write tests ? **ALL tests pass** ?
5. **Task 4:** Implement ? Write tests ? **ALL tests pass** ?
6. **Task 5:** Implement ? Write tests ? **ALL tests pass** ?

**DO NOT** move to the next task until:
- ? Current task implementation complete
- ? Current task tests written
- ? **ALL tests passing** (including previous batch tests)

**Why:** Ensures each component is solid before building on top of it. Prevents cascading failures.

---

## Context

This batch finishes manual verification, adds missing tests for core panels, and introduces keyboard navigation + context menus.

**Related Tasks:**
- [DMON-017](../../docs/ddsmon/TASK-DETAIL.md#dmon-017--desktoprazor-shell--panel-chrome)
- [DMON-021](../../docs/ddsmon/TASK-DETAIL.md#dmon-021--samples-panel-virtualized-data-grid)
- [DMON-022](../../docs/ddsmon/TASK-DETAIL.md#dmon-022--sample-detail-panel-inspector)
- [DMON-023](../../docs/ddsmon/TASK-DETAIL.md#dmon-023--hover-json-tooltip)
- [DMON-024](../../docs/ddsmon/TASK-DETAIL.md#dmon-024--text-view-panel)
- [DMON-025](../../docs/ddsmon/TASK-DETAIL.md#dmon-025--keyboard-navigation)
- [DMON-026](../../docs/ddsmon/TASK-DETAIL.md#dmon-026--context-menu-system)

---

## ?? Batch Objectives

- Complete pending manual checks for desktop shell and Text View panel.
- Add missing unit tests for Samples Panel, Detail Panel, and Hover Tooltip.
- Implement keyboard navigation in Samples Panel.
- Implement the context menu system with required actions.

---

## ? Tasks

### Task 0: Corrective — Manual verification (DMON-017/DMON-024)

**Files:** `.dev-workstream/reports/MON-BATCH-13-REPORT.md` (UPDATE)  
**Description:** Run manual checks and document results.

**Manual Test Steps:**
- Run `dotnet run --project tools/DdsMonitor/DdsMonitor.csproj`.
- Verify z-order and close behavior in Desktop.
- Verify Text View panel JSON formatting, Plain/JSON toggle, and 10KB+ string rendering.

---

### Task 1: Missing Samples Panel test (DMON-021)

**Files:** `tests/DdsMonitor.Engine.Tests/` (UPDATE)  
**Requirement:** Add `SamplesPanel_VirtualizeCallback_RequestsCorrectRange` and ensure it asserts actual `startIndex`/`count` behavior from the virtualizer.

---

### Task 2: Missing Detail Panel test (DMON-022)

**Files:** `tests/DdsMonitor.Engine.Tests/` (UPDATE)  
**Requirement:** Add `DetailPanel_Debounce_WaitsBeforeRender` that verifies the debounce behavior under rapid event firing.

---

### Task 3: Missing Hover Tooltip tests (DMON-023)

**Files:** `tests/DdsMonitor.Engine.Tests/` (UPDATE)  
**Requirements:**
- `HoverTooltip_ValidJson_ParsesWithoutError`
- `HoverTooltip_InvalidJson_ReturnsFalse`

---

### Task 4: Keyboard navigation (DMON-025)

**Files:** `tools/DdsMonitor/` (UPDATE)  
**Requirements:** Implement keyboard navigation for Samples Panel per DMON-025 success conditions.

**Tests Required:** Manual tests per DMON-025 (arrow, page up/down, home/end, track mode debounce).

---

### Task 5: Context menu system (DMON-026)

**Files:** `tools/DdsMonitor/` (NEW/UPDATE)  
**Requirements:** Implement `ContextMenu.razor` and wire it into the grid and inspector per DMON-026.

**Tests Required:** Manual tests per DMON-026 success conditions.

---

## ?? Testing Requirements

- Run `dotnet build CycloneDDS.NET.sln`.
- Run `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj`.
- Fix any build or test failures without asking for permission. Complete the batch end-to-end.

---

## ?? Quality Standards

**? TEST QUALITY EXPECTATIONS**
- Use xUnit only. MSTest is not allowed for new tests.
- Tests must validate actual behavior (virtualize ranges, debounce timing, JSON parsing).
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
- [ ] DMON-021 test added and passing
- [ ] DMON-022 test added and passing
- [ ] DMON-023 tests added and passing
- [ ] DMON-024 manual verification documented
- [ ] DMON-025 completed per task definition
- [ ] DMON-026 completed per task definition
- [ ] `dotnet build CycloneDDS.NET.sln` passes
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj` passes
- [ ] Report submitted to `.dev-workstream/reports/MON-BATCH-13-REPORT.md`

---

## ?? Common Pitfalls to Avoid

- Skipping manual verification steps.
- Debounce logic that still renders too often.
- Tooltip parsing that accepts invalid JSON.
- Context menu not closing on click-outside or Escape.
- Using MSTest or shallow tests.

---

## ?? Reference Materials

- **Task Defs:** `docs/ddsmon/TASK-DETAIL.md` (DMON-017/021/022/023/024/025/026)
- **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
- **Design:** `docs/ddsmon/DESIGN.md` (§8, §10.4–§10.6)
- **Onboarding:** `docs/ddsmon/ONBOARDING.md`
