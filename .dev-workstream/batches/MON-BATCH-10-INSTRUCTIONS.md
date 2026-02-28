# MON-BATCH-10: Desktop verification + Samples Panel + Detail Panel + Hover Tooltip + Text View (DMON-017 fix/021/022/023/024)

**Batch Number:** MON-BATCH-10  
**Tasks:** DMON-017 (fix), DMON-021, DMON-022, DMON-023, DMON-024  
**Phase:** 2 — Blazor Shell & Core UI  
**Estimated Effort:** 20-30 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-09

---

## ?? Onboarding & Workflow

### Developer Instructions
This batch finishes the desktop shell verification and implements the Samples Panel, Detail Panel, Hover JSON tooltip, and Text View panel. Use xUnit only for new tests (no MSTest). Complete the full batch end-to-end without pausing for approvals (build, test, fix root causes).

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Developer Guide:** `.dev-workstream/guides/DEV-GUIDE.md`
3. **Code Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`
4. **DDS Monitor Onboarding:** `docs/ddsmon/ONBOARDING.md`
5. **DDS Monitor Design:** `docs/ddsmon/DESIGN.md` (§8, §10.4–§10.6)
6. **Task Details:** `docs/ddsmon/TASK-DETAIL.md` (DMON-017/021/022/023/024)
7. **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
8. **Previous Review:** `.dev-workstream/reviews/MON-BATCH-09-REVIEW.md`

### Source Code Location
- **Blazor Host:** `tools/DdsMonitor/`
- **Engine Library:** `tools/DdsMonitor/DdsMonitor.Engine/`
- **Test Project:** `tests/DdsMonitor.Engine.Tests/`
- **Solution File:** `CycloneDDS.NET.sln`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-10-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-10-QUESTIONS.md`

---

## ?? MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 0:** Fix ? Write tests ? **ALL tests pass** ?
2. **Task 1:** Implement ? Write tests ? **ALL tests pass** ?
3. **Task 2:** Implement ? Write tests ? **ALL tests pass** ?
4. **Task 3:** Implement ? Write tests ? **ALL tests pass** ?
5. **Task 4:** Implement ? Write tests ? **ALL tests pass** ?

**DO NOT** move to the next task until:
- ? Current task implementation complete
- ? Current task tests written
- ? **ALL tests passing** (including previous batch tests)

**Why:** Ensures each component is solid before building on top of it. Prevents cascading failures.

---

## Context

This batch completes the desktop shell verification and brings the core Samples/Detail UI to life.

**Related Tasks:**
- [DMON-017](../../docs/ddsmon/TASK-DETAIL.md#dmon-017--desktoprazor-shell--panel-chrome)
- [DMON-021](../../docs/ddsmon/TASK-DETAIL.md#dmon-021--samples-panel-virtualized-data-grid)
- [DMON-022](../../docs/ddsmon/TASK-DETAIL.md#dmon-022--sample-detail-panel-inspector)
- [DMON-023](../../docs/ddsmon/TASK-DETAIL.md#dmon-023--hover-json-tooltip)
- [DMON-024](../../docs/ddsmon/TASK-DETAIL.md#dmon-024--text-view-panel)

---

## ?? Batch Objectives

- Execute and document the remaining DMON-017 manual checks.
- Implement the Samples Panel with virtualization and filtering.
- Implement the Detail Panel with linked/detached modes and debounce.
- Implement hover JSON tooltip and Text View panel.

---

## ? Tasks

### Task 0: Corrective — DMON-017 manual verification

**Files:** `.dev-workstream/reports/MON-BATCH-10-REPORT.md` (UPDATE)  
**Description:** Run the remaining manual checks for z-order and close behavior and document results.

**Manual Test Steps:**
- Run `dotnet run --project tools/DdsMonitor/DdsMonitor.csproj`.
- Verify z-order: clicking a background panel brings it to front.
- Verify close: close button removes panel.

---

### Task 1: Samples Panel (DMON-021)

**Files:** `tools/DdsMonitor/` (NEW/UPDATE)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-021--samples-panel-virtualized-data-grid)

**Tests Required (xUnit):**
- `SamplesPanel_VirtualizeCallback_RequestsCorrectRange`

---

### Task 2: Sample Detail Panel (DMON-022)

**Files:** `tools/DdsMonitor/` (NEW/UPDATE)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-022--sample-detail-panel-inspector)

**Tests Required (xUnit):**
- `DetailPanel_Debounce_WaitsBeforeRender`

---

### Task 3: Hover JSON Tooltip (DMON-023)

**Files:** `tools/DdsMonitor/` (NEW/UPDATE)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-023--hover-json-tooltip)

**Tests Required (xUnit):**
- `HoverTooltip_ValidJson_ParsesWithoutError`
- `HoverTooltip_InvalidJson_ReturnsFalse`

---

### Task 4: Text View Panel (DMON-024)

**Files:** `tools/DdsMonitor/` (NEW/UPDATE)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-024--text-view-panel)

**Tests Required:**
- Manual tests per DMON-024 success conditions.

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
- [ ] DMON-021 completed per task definition
- [ ] DMON-022 completed per task definition
- [ ] DMON-023 completed per task definition
- [ ] DMON-024 completed per task definition
- [ ] `dotnet build CycloneDDS.NET.sln` passes
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj` passes
- [ ] Report submitted to `.dev-workstream/reports/MON-BATCH-10-REPORT.md`

---

## ?? Common Pitfalls to Avoid

- Skipping remaining manual verification for desktop shell.
- Debounce logic that still renders too often.
- Tooltip logic that parses invalid JSON.
- Using MSTest or shallow tests.

---

## ?? Reference Materials

- **Task Defs:** `docs/ddsmon/TASK-DETAIL.md` (DMON-017/021/022/023/024)
- **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
- **Design:** `docs/ddsmon/DESIGN.md` (§8, §10.4–§10.6)
- **Onboarding:** `docs/ddsmon/ONBOARDING.md`
