# MON-BATCH-15: Manual verification + UX fixes (DMON-017/024/025/026)

**Batch Number:** MON-BATCH-15  
**Tasks:** DMON-017 (manual), DMON-024 (manual), DMON-025 (fix), DMON-026 (fix)  
**Phase:** 2 — Blazor Shell & Core UI  
**Estimated Effort:** 22-30 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-14

---

## ?? Onboarding & Workflow

### Developer Instructions
This batch completes the outstanding manual verification and resolves the reported UX issues around keyboard navigation, context menus, detail panel latency, and Topics panel relaunch. Use xUnit only for new tests (no MSTest). Complete the full batch end-to-end without pausing for approvals (build, test, fix root causes).

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Developer Guide:** `.dev-workstream/guides/DEV-GUIDE.md`
3. **Code Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`
4. **DDS Monitor Onboarding:** `docs/ddsmon/ONBOARDING.md`
5. **DDS Monitor Design:** `docs/ddsmon/DESIGN.md` (§8, §10.4–§10.6)
6. **Task Details:** `docs/ddsmon/TASK-DETAIL.md` (DMON-017/024/025/026)
7. **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
8. **Previous Review:** `.dev-workstream/reviews/MON-BATCH-14-REVIEW.md`

### Source Code Location
- **Blazor Host:** `tools/DdsMonitor/`
- **Engine Library:** `tools/DdsMonitor/DdsMonitor.Engine/`
- **Test Project:** `tests/DdsMonitor.Engine.Tests/`
- **Solution File:** `CycloneDDS.NET.sln`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-15-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-15-QUESTIONS.md`

---

## ?? MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 0:** Manual verification ? **ALL tests pass** ?
2. **Task 1:** Implement ? Write tests ? **ALL tests pass** ?
3. **Task 2:** Implement ? Write tests ? **ALL tests pass** ?
4. **Task 3:** Implement ? Write tests ? **ALL tests pass** ?
5. **Task 4:** Implement ? Write tests ? **ALL tests pass** ?

**DO NOT** move to the next task until:
- ? Current task implementation complete
- ? Current task tests written
- ? **ALL tests passing** (including previous batch tests)

---

## Context

Manual verification remains pending and UX bugs were reported: keyboard navigation scroll sync, context menu showing browser menu, detail panel open latency, and inability to reopen Topics panel.

---

## ?? Batch Objectives

- Complete manual checks for DMON-017/024/025/026.
- Fix SamplesPanel keyboard navigation scroll sync after PageUp/PageDown.
- Ensure right-click context menus render and close correctly.
- Reduce detail panel open latency.
- Add a launcher to reopen the Topics panel.

---

## ? Tasks

### Task 0: Manual verification (DMON-017/024/025/026)

**Files:** `.dev-workstream/reports/MON-BATCH-15-REPORT.md` (UPDATE)  
**Description:** Run manual checks and document results. Use the checklist below. For each step record: Environment (OS, browser, DPI), steps executed, observed result, pass/fail, any logs/screenshots.

**Manual Test Checklist (step-by-step)**

A. Environment & startup
- A1. Prepare environment: ensure .NET 8 SDK installed, build solution, and run the Blazor host:
  - `dotnet build CycloneDDS.NET.sln`
  - `dotnet run --project tools/DdsMonitor/DdsMonitor.csproj --urls "http://127.0.0.1:58181;https://127.0.0.1:58180"`
- A2. Open a desktop browser (Chrome/Edge/Firefox) and navigate to the app URL. Note browser and version.

B. Desktop shell chrome (DMON-017)
- B1. Drag panels by title bar: click-and-drag the panel title bar; expected: panel moves following pointer, content updates while dragging. Pass if smooth and no UI glitches.
- B2. Resize panels from edge and corner: drag each resize handle; expected: panel resizes and content reflows; no layout corruption.
- B3. Minimize panel: click minimize; expected: panel collapses to panel-strip at bottom; strip entry visible with panel title.
- B4. Restore from strip: click strip entry; expected: panel restores to previous size/position and comes to front.
- B5. Close panel: click close (X); expected: panel removed and resources freed; PanelStrip (if present) entry removed.
- B6. Z-order: open two overlapping panels; click the background panel; expected: clicked panel comes to front and receives focus.
- B7. Panel spawn/launcher: close the Topics panel then use the toolbar Topics launcher; expected: Topics panel reopens and is focused.

C. TopicExplorer / Subscribe flows
- C1. Subscribe All when some topics lack descriptor ops: click 'Subscribe All'; expected: inline message listing skipped topics and remaining valid topics subscribed. No unhandled error.
- C2. Individual subscribe for invalid topic: click subscribe checkbox on a topic lacking descriptor ops; expected: friendly error message shown, UI remains stable (panel spawn may be disabled or replaced with placeholder).

D. Samples list and selection behavior (DMON-025)
- D1. Open Samples panel for a topic with many samples (>500). Confirm virtualization visible (scrollbar present).
- D2. Keyboard navigation - Arrow keys: focus the samples grid, press ArrowDown/ArrowUp repeatedly; expected: selection moves one row and view scrolls only if selection exits viewport.
- D3. Page navigation: press PageDown/PageUp; expected: selection moves by page and resulting selected row is visible in viewport. Record before/after top-index and selected ordinal.
- D4. Home/End: press Home/End; expected: selection moves to first/last visible row and is visible.
- D5. Selection visibility regression: sequence: PageDown, then ArrowDown several times; expected: selection stays in view; record any mismatch.
- D6. Track mode debounce: enable/disable track mode and verify selection of incoming samples highlights and scrolls appropriately without excessive jumps. Verify UI updates at configured refresh rate.

E. Detail panel open (DMON-022 / latency)
- E1. Open detail by double-click: double-click a selected sample row; measure time from action to detail panel visible. Expected median < 400 ms (adjust target based on UX guidance). Record values for 5 trials.
- E2. Open detail by Enter key: focus a row and press Enter; measure times as above. Ensure Enter opens panel reliably; expected similar latency to double-click.
- E3. Rapid interactions: perform a burst of selection changes (10 in 100 ms), then open detail; expected: detail opens after debounce but not excessively delayed. If delayed, note debounce period in code and suggest reduction.

F. Context menu behavior (DMON-026)
- F1. Right-click on a sample cell containing JSON text: expected: app context menu appears (not browser menu), positioned near cursor, with actions (Copy, Show JSON, Open in Text View). Record whether `preventDefault` prevented native menu.
- F2. Click outside the context menu: expected: context menu closes.
- F3. Press Escape while context menu open: expected: context menu closes.
- F4. Invoke an action (e.g., Open in Text View): expected: corresponding panel opens and displays content.
- F5. Right-click on empty area or non-interactive UI: expected: either show app menu or no action; no browser menu.

G. Text View panel (DMON-024)
- G1. Open Text View with valid JSON: verify formatted/pretty-printed JSON appears when JSON mode selected; syntax highlighting present.
- G2. Switch to Plain Text mode: verify raw payload shown; no formatting applied.
- G3. Large payloads (10KB+): open a large string sample; expected: panel loads and renders within acceptable time (< 1s) and does not freeze UI. Test scrolling performance.
- G4. Copy/Select behavior: select text and copy to clipboard; verify copied content matches shown text.

H. Accessibility & focus
- H1. Keyboard-only flow: open Topics panel, navigate to samples grid with keyboard, open detail via Enter, open context menu via keyboard shortcut (if supported). Expected: can perform main flows without mouse.

I. Logging and failures
- I1. While performing above steps, monitor server logs for exceptions (unhandled exceptions, StackOverflow, InvalidOperationException). If any occur, capture stack trace and reproduction steps.
- I2. Capture screenshots or short video clips for any failing behavior.

J. Final report entries
- For each check above record:
  - Step ID (e.g., D1)
  - Environment (OS, Browser + version, screen resolution)
  - Exact steps performed
  - Observed result
  - Expected result
  - Pass/Fail
  - Attached evidence (screenshot, console log, server log)


**Notes:**
- If Playwright automation is used, verify that synthetic input events map to real browser events (some right-click sequences require `page.dispatchEvent('contextmenu', ...)` or using `mouse.down({button: 'right'})`). If automation fails to reproduce, complete the checks manually in a local browser.
- If tests reveal regressions (e.g., stack overflows, unhandled exceptions), stop and file a bug entry in the debt tracker and attach logs.

---

### Task 1: Keyboard navigation scroll sync (DMON-025 fix)

**Files:** `tools/DdsMonitor/Components/SamplesPanel.razor` (UPDATE)  
**Requirements:**
- Ensure selection changes after PageUp/PageDown keep the selected row in view.
- If needed, call a virtualizer scroll-to-index API or re-request range based on selection.

**Tests Required:** Manual verification plus a unit test if a helper method is extracted.

---

### Task 2: Context menu rendering (DMON-026 fix)

**Files:** `tools/DdsMonitor/Components/ContextMenu.razor`, `tools/DdsMonitor/Components/SamplesPanel.razor`, `tools/DdsMonitor/Components/DetailPanel.razor` (UPDATE)  
**Requirements:**
- Ensure right-click uses `oncontextmenu:preventDefault` and opens the app menu.
- Ensure click-outside and Escape close the menu.

**Tests Required:** Manual verification per task definition.

---

### Task 3: Detail panel open latency

**Files:** `tools/DdsMonitor/Components/DetailPanel.razor`, `tools/DdsMonitor/Components/SamplesPanel.razor` (UPDATE)  
**Requirements:**
- Reduce debounce delay for opening detail (double-click/Enter).
- Ensure selection debounce does not block detail window creation.

**Tests Required:** Update or add a timing-focused unit test if possible; otherwise document manual timings.

---

### Task 4: Topics panel launcher

**Files:** `tools/DdsMonitor/Components/Layout/MainLayout.razor`, `tools/DdsMonitor/Components/Desktop.razor` (UPDATE)  
**Requirements:**
- Add a menu/toolbar action to spawn the Topics panel after it is closed.
- Use `IWindowManager.SpawnPanel` with the Topic Explorer component type.

**Tests Required:** Manual verification (open/close/reopen).

---

## ?? Testing Requirements

- Run `dotnet build CycloneDDS.NET.sln`.
- Run `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj`.
- Fix any build or test failures without asking for permission. Complete the batch end-to-end.

---

## ?? Quality Standards

**? TEST QUALITY EXPECTATIONS**
- Use xUnit only. MSTest is not allowed for new tests.
- Tests must validate actual behavior where possible.
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
- [ ] DMON-025 keyboard navigation scroll sync fixed and verified
- [ ] DMON-026 context menu renders and closes correctly
- [ ] Detail panel open latency addressed and verified
- [ ] Topics panel can be reopened from a launcher
- [ ] `dotnet build CycloneDDS.NET.sln` passes
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj` passes
- [ ] Report submitted to `.dev-workstream/reports/MON-BATCH-15-REPORT.md`

---

## ?? Common Pitfalls to Avoid

- Skipping manual verification steps.
- Introducing scroll jank when syncing selection.
- Context menu not closing on Escape/click-outside.
- Using MSTest or shallow tests.

---

## ?? Reference Materials

- **Task Defs:** `docs/ddsmon/TASK-DETAIL.md` (DMON-017/024/025/026)
- **Design:** `docs/ddsmon/DESIGN.md` (§8, §10.4–§10.6)
- **Onboarding:** `docs/ddsmon/ONBOARDING.md`
