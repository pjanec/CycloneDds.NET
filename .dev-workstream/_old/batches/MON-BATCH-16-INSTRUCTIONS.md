# MON-BATCH-16: Manual verification + UX regression fixes + UI test harness (DMON-017/024/025/026)

**Batch Number:** MON-BATCH-16  
**Tasks:** DMON-017 (manual), DMON-024 (manual), DMON-025 (verify/fix), DMON-026 (verify/fix)  
**Phase:** 2 — Blazor Shell & Core UI  
**Estimated Effort:** 22-30 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-15

---

## ?? Onboarding & Workflow

### Developer Instructions
This batch completes the outstanding manual verification and confirms the recent UX fixes. If any issues persist, fix them and add regression coverage. Use xUnit only for new tests (no MSTest). Complete the full batch end-to-end without pausing for approvals (build, test, fix root causes).

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Developer Guide:** `.dev-workstream/guides/DEV-GUIDE.md`
3. **Code Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`
4. **DDS Monitor Onboarding:** `docs/ddsmon/ONBOARDING.md`
5. **DDS Monitor Design:** `docs/ddsmon/DESIGN.md` (§8, §10.4–§10.6)
6. **Task Details:** `docs/ddsmon/TASK-DETAIL.md` (DMON-017/024/025/026)
7. **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
8. **Previous Review:** `.dev-workstream/reviews/MON-BATCH-15-REVIEW.md`

### Source Code Location
- **Blazor Host:** `tools/DdsMonitor/`
- **Engine Library:** `tools/DdsMonitor/DdsMonitor.Engine/`
- **Test Project:** `tests/DdsMonitor.Engine.Tests/`
- **Solution File:** `CycloneDDS.NET.sln`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-16-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-16-QUESTIONS.md`

---

## ?? MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 0:** Manual verification ? **ALL tests pass** ?
2. **Task 1:** Verify/fix ? Write tests ? **ALL tests pass** ?
3. **Task 2:** Verify/fix ? Write tests ? **ALL tests pass** ?
4. **Task 3:** Verify/fix ? Write tests ? **ALL tests pass** ?
5. **Task 4:** UI test harness improvements ? **ALL tests pass** ?

---

## Context

Manual verification is still pending. The previous batch introduced fixes for keyboard navigation scroll sync, context menu handling, detail panel latency, and a Topics launcher. These must be verified and fixed if issues remain. Automation gaps were also noted.

---

## ?? Batch Objectives

- Complete manual checks for DMON-017/024/025/026.
- Verify keyboard navigation scroll sync after PageUp/PageDown; fix if needed.
- Verify context menu renders and closes correctly; fix if needed.
- Verify detail panel open latency and adjust if needed.
- Improve UI test automation reliability where possible.

---

## ? Tasks

### Task 0: Manual verification (DMON-017/024/025/026)

**Files:** `.dev-workstream/reports/MON-BATCH-16-REPORT.md` (UPDATE)  
**Description:** Run manual checks and document results. For each step record: Environment (OS, browser and version, DPI), exact steps executed, observed result, pass/fail, any logs/screenshots.

**Detailed Manual Test Checklist (step-by-step)**

A. Environment & startup
- A1. Prepare environment
  - Ensure .NET 8 SDK is installed.
  - Build the solution: `dotnet build CycloneDDS.NET.sln` (record build output/warnings).
  - Start the Blazor app (preferably with explicit URLs to avoid port conflicts):
    `dotnet run --project tools/DdsMonitor/DdsMonitor.csproj --urls "http://127.0.0.1:58181;https://127.0.0.1:58180"`
  - Note OS, Browser (Chrome/Edge/Firefox) and versions, display resolution and scaling.
- A2. Open browser and navigate to the HTTP or HTTPS URL used above.

B. Desktop shell chrome (DMON-017)
- B1. Drag panels by title bar
  - Action: Click and drag the title bar of a panel across the desktop.
  - Expected: Panel follows cursor smoothly; no visual artifacts; other panels unaffected.
- B2. Resize panels from edges and corners
  - Action: Drag each side and corner resize handle.
  - Expected: Panel resizes fluidly; content reflows; no content overlap.
- B3. Minimize panel
  - Action: Click minimize button on a panel.
  - Expected: Panel collapses to a panel-strip (bottom); strip shows entry with panel title.
- B4. Restore from panel-strip
  - Action: Click the panel-strip entry.
  - Expected: Panel restores to previous position and size and gains focus (on top).
- B5. Close panel
  - Action: Click the close/X button.
  - Expected: Panel removed; any resources freed; panel-strip entry (if present) removed.
- B6. Z-order behavior
  - Action: Open two overlapping panels A and B. Click the background of panel A.
  - Expected: Panel A moves to front and receives focus; visually on top.
- B7. Panel spawn/launcher
  - Action: Close the Topics panel. Use the toolbar launcher (MainLayout toolbar) to reopen Topics.
  - Expected: Topics panel spawns and becomes focused; WindowManager.ActivePanels reflects the panel.

C. TopicExplorer and Subscribe flows
- C1. Bulk Subscribe (Subscribe All) with some invalid topics
  - Action: Click 'Subscribe All' when repository contains topics lacking descriptor ops.
  - Expected: Inline message appears listing skipped topics (capped length) and valid topics subscribed; no unhandled exception.
- C2. Individual subscribe for invalid topic
  - Action: Click subscribe checkbox for a topic without descriptor ops.
  - Expected: Friendly error message shown; UI remains stable; topic checkbox may be checked if placeholder reader created; no crash.

D. Samples list and selection behavior (DMON-025)
- D1. Large sample list verification
  - Action: Open Samples panel for a topic with >500 samples to ensure virtualization.
  - Expected: Scrollbar present; rows render on demand.
- D2. Arrow key navigation
  - Action: Focus the samples grid, press ArrowDown and ArrowUp repeatedly.
  - Expected: Selection moves one row, viewport scrolls only when selection exits visible area; no selection out-of-view.
- D3. PageUp/PageDown navigation
  - Action: Press PageDown then PageUp.
  - Expected: Selection moves by page; after PageDown/PageUp, subsequent Arrow keys keep selected row visible (selection and viewport remain in sync).
  - Record: before/after top-index and selected sample ordinal for one scenario.
- D4. Home/End keys
  - Action: Press Home then End.
  - Expected: Selection jumps to first/last row and remains visible.
- D5. Selection visibility regression test
  - Action: Sequence: PageDown, then ArrowDown several times.
  - Expected: Selection remains visible; if not, capture exact sequence and screenshots.
- D6. Track mode behavior
  - Action: Toggle Track mode and observe how newly incoming samples affect selection and scroll.
  - Expected: In Track mode, view follows newest items; when disabled, selection does not auto-scroll.

E. Detail panel open and latency (DMON-022)
- E1. Double-click to open detail
  - Action: Double-click a row 5 times (separate trials), measure time from double-click to detail panel visible.
  - Expected: Median latency under ~400 ms (adjustable threshold); no StackOverflow or exceptions.
  - Record timings for each trial.
- E2. Enter key to open detail
  - Action: Focus a row and press Enter 5 times, measure latency.
  - Expected: Reliable open; latency comparable to double-click.
- E3. Rapid selection bursts
  - Action: Rapidly change selection (10 changes within 200ms), then open detail.
  - Expected: Detail opens after debounce but not excessively delayed. If delayed, note debounce value and suggest reduction.

F. Context menu behavior (DMON-026)
- F1. Right-click on a JSON cell
  - Action: Right-click on a cell that contains JSON/text.
  - Expected: App context menu appears (not native browser menu), positioned near cursor, with expected actions (Copy, View JSON, Open Text View).
- F2. Click outside to close
  - Action: Click in an empty area outside the menu.
  - Expected: Context menu closes.
- F3. Escape closes menu
  - Action: With context menu open, press Escape.
  - Expected: Menu closes.
- F4. Execute action
  - Action: Choose 'Open in Text View' (or similar) from context menu.
  - Expected: Text View panel opens and displays the selected content.
- F5. Right-click on non-interactive area
  - Action: Right-click on empty panel background.
  - Expected: Either app menu or no action; no browser native menu should appear.

G. Text View panel checks (DMON-024)
- G1. JSON formatting
  - Action: From context menu or UI, open Text View for a JSON payload.
  - Expected: Pretty-printed JSON when JSON mode selected; highlight present; no parse errors.
- G2. Plain text view toggle
  - Action: Toggle to Plain mode.
  - Expected: Raw payload displayed verbatim; switching back restores formatted view.
- G3. Large payload performance
  - Action: Open a sample with payload >= 10KB; measure time to render and responsiveness when scrolling.
  - Expected: Load and render within ~1s; UI remains responsive.
- G4. Copy behavior
  - Action: Select all and copy contents; paste elsewhere to verify fidelity.
  - Expected: Copied content matches displayed text.

H. Accessibility and keyboard-only flow
- H1. Keyboard-only navigation
  - Action: Using only keyboard, open Topics, navigate to samples, open detail, open context menu via keyboard (if supported).
  - Expected: Primary flows should be possible without mouse; focus rings and ARIA attributes present where applicable.

I. Logging and failures
- I1. Monitor logs
  - Action: While testing, monitor server console logs and browser console for exceptions (StackOverflow, InvalidOperationException, JS errors).
  - Expected: No unhandled exceptions; if errors occur, capture stack trace and reproduction steps.
- I2. Evidence capture
  - Action: For any failure capture screenshot, browser console logs, and server logs; include timestamps.

J. Reporting results
- J1. For each checklist item record:
  - Step ID (e.g., D2)
  - Environment (OS, Browser + version, screen resolution)
  - Exact steps
  - Observed result
  - Expected result
  - Pass/Fail
  - Attached evidence (screenshot, logs)

**Notes & automation tips:**
- Playwright may require `page.dispatchEvent('contextmenu', ...)` or `page.mouse.click(x, y, { button: 'right' })` to reliably trigger app context menus; if automation fails, perform checks locally.
- If any server-side exceptions occur, stop further interactive checks and file a bug with full logs and reproduction steps.

---

### Task 1: Keyboard navigation regression check (DMON-025)

**Files:** `tools/DdsMonitor/Components/SamplesPanel.razor` (UPDATE)  
**Requirement:** Confirm scroll sync is correct after PageUp/PageDown. If not, fix and add a focused unit test for the scroll helper (if extractable).

---

### Task 2: Context menu regression check (DMON-026)

**Files:** `tools/DdsMonitor/Components/ContextMenu.razor`, `tools/DdsMonitor/Components/SamplesPanel.razor`, `tools/DdsMonitor/Components/DetailPanel.razor` (UPDATE)  
**Requirement:** Confirm right-click opens the app menu and closes on click-outside/Escape. Fix any remaining issues.

---

### Task 3: Detail panel open latency validation

**Files:** `tools/DdsMonitor/Components/DetailPanel.razor`, `tools/DdsMonitor/Components/SamplesPanel.razor` (UPDATE)  
**Requirement:** Verify latency improvements. If still slow, reduce debounce further or separate selection debounce from detail opening and document timings.

---

### Task 4: UI test harness improvements (optional but recommended)

**Files:** `tests/DdsMonitor.Engine.Tests/` or `tools/DdsMonitor/`  
**Requirement:** Improve automation reliability (Playwright adjustments or add a lightweight component test harness). Document any changes.

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
- [ ] DMON-025 verified (and fixed if needed)
- [ ] DMON-026 verified (and fixed if needed)
- [ ] Detail panel latency verified (and fixed if needed)
- [ ] UI automation reliability improved (if changed)
- [ ] `dotnet build CycloneDDS.NET.sln` passes
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj` passes
- [ ] Report submitted to `.dev-workstream/reports/MON-BATCH-16-REPORT.md`

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
