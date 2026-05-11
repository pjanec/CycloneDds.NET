# MON-BATCH-14: UI verification + Detail panel recursion fix + Subscribe All hardening (DMON-017/024/025/026 fixes)

**Batch Number:** MON-BATCH-14  
**Tasks:** DMON-017 (fix), DMON-024 (fix), DMON-025 (fix), DMON-026 (fix) + corrective UI bugs  
**Phase:** 2 — Blazor Shell & Core UI  
**Estimated Effort:** 22-30 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-13

---

## ?? Onboarding & Workflow

### Developer Instructions
This batch completes pending manual verification and fixes two critical UI issues (Detail panel stack overflow and Subscribe All failures). Use xUnit only for new tests (no MSTest). Complete the full batch end-to-end without pausing for approvals (build, test, fix root causes).

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Developer Guide:** `.dev-workstream/guides/DEV-GUIDE.md`
3. **Code Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`
4. **DDS Monitor Onboarding:** `docs/ddsmon/ONBOARDING.md`
5. **DDS Monitor Design:** `docs/ddsmon/DESIGN.md` (§8, §10.4–§10.6)
6. **Task Details:** `docs/ddsmon/TASK-DETAIL.md` (DMON-017/024/025/026)
7. **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
8. **Previous Review:** `.dev-workstream/reviews/MON-BATCH-13-REVIEW.md`

### Source Code Location
- **Blazor Host:** `tools/DdsMonitor/`
- **Engine Library:** `tools/DdsMonitor/DdsMonitor.Engine/`
- **Test Project:** `tests/DdsMonitor.Engine.Tests/`
- **Solution File:** `CycloneDDS.NET.sln`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-14-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-14-QUESTIONS.md`

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

Manual verification remains incomplete for the desktop shell and core UI. Two critical issues were observed: Detail panel rendering recursion and Subscribe All failures for invalid topics.

**Related Tasks:**
- [DMON-017](../../docs/ddsmon/TASK-DETAIL.md#dmon-017--desktoprazor-shell--panel-chrome)
- [DMON-024](../../docs/ddsmon/TASK-DETAIL.md#dmon-024--text-view-panel)
- [DMON-025](../../docs/ddsmon/TASK-DETAIL.md#dmon-025--keyboard-navigation)
- [DMON-026](../../docs/ddsmon/TASK-DETAIL.md#dmon-026--context-menu-system)

---

## ?? Batch Objectives

- Complete manual verification for desktop, keyboard navigation, context menus, and text view panel.
- Fix Detail panel recursive rendering to prevent stack overflow.
- Harden Subscribe All against invalid topic types.

---

## ? Tasks

### Task 0: Manual verification (DMON-017/024/025/026)

**Files:** `.dev-workstream/reports/MON-BATCH-14-REPORT.md` (UPDATE)  
**Description:** Run manual checks and document results.

**Manual Test Steps:**
- Run `dotnet run --project tools/DdsMonitor/DdsMonitor.csproj`.
- Verify desktop z-order and close behavior.
- Verify keyboard navigation (arrow/page/home/end, Enter opens detail, Track mode debounce).
- Verify context menu: right-click shows menu, click-outside closes, Escape closes, actions invoke.
- Verify Text View panel JSON formatting, Plain/JSON toggle, and 10KB+ string rendering.

---

### Task 1: Fix Detail panel recursion

**Files:** `tools/DdsMonitor/Components/DetailPanel.razor` (UPDATE)  
**Requirements:**
- Add recursion guards or depth limits in `RenderNode` to avoid unbounded recursion.
- Ensure double-click in Samples panel opens Detail without stack overflow.

**Tests Required (xUnit):**
- `DetailPanel_RenderNode_DoesNotRecurseInfinitely` (construct a cyclic payload or depth-limited render and assert it terminates).

---

### Task 2: Harden Subscribe All against invalid topics

**Files:** `tools/DdsMonitor/Components/TopicExplorerPanel.razor`, `tools/DdsMonitor/DdsMonitor.Engine/DdsBridge.cs` (UPDATE)  
**Requirements:**
- Skip topics lacking descriptor ops and surface a message (toast or inline error).
- Ensure bulk subscribe continues with remaining valid topics.

**Tests Required (xUnit):**
- `DdsBridge_Subscribe_InvalidTopic_DoesNotThrow` (simulate missing descriptor ops and assert no exception).

---

### Task 3: Context menu and keyboard navigation manual validation

**Files:** `.dev-workstream/reports/MON-BATCH-14-REPORT.md` (UPDATE)  
**Requirements:**
- Document the manual verification results for DMON-025/026 explicitly (pass/fail with notes).

---

## ?? Testing Requirements

- Run `dotnet build CycloneDDS.NET.sln`.
- Run `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj`.
- Fix any build or test failures without asking for permission. Complete the batch end-to-end.

---

## ?? Quality Standards

**? TEST QUALITY EXPECTATIONS**
- Use xUnit only. MSTest is not allowed for new tests.
- Tests must validate actual behavior (stack overflow prevention, subscription guarding).
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
- [ ] DMON-024 manual verification documented
- [ ] DMON-025 manual verification documented
- [ ] DMON-026 manual verification documented
- [ ] DetailPanel recursion fix implemented and tested
- [ ] Subscribe All hardening implemented and tested
- [ ] `dotnet build CycloneDDS.NET.sln` passes
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj` passes
- [ ] Report submitted to `.dev-workstream/reports/MON-BATCH-14-REPORT.md`

---

## ?? Common Pitfalls to Avoid

- Ignoring cycles in object graphs when rendering Detail panel.
- Silently failing bulk subscribe without user feedback.
- Skipping manual verification steps.
- Using MSTest or shallow tests.

---

## ?? Reference Materials

- **Task Defs:** `docs/ddsmon/TASK-DETAIL.md` (DMON-017/024/025/026)
- **Design:** `docs/ddsmon/DESIGN.md` (§8, §10.4–§10.6)
- **Onboarding:** `docs/ddsmon/ONBOARDING.md`
