# BATCH-18: Advanced UI Enhancements (Grid Settings, Sparklines & Quick-Add)

**Batch Number:** MON-BATCH-18  
**Tasks:** DMON-030 (Expand All mode), DMON-031 (Grid settings export/import), DMON-032 (Sparkline charts in Topic Explorer), DMON-033 (Quick-Add column from Inspector)  
**Phase:** Phase 3 (Advanced UI Features)  
**Estimated Effort:** 5-7 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-17

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome to BATCH-18! This batch wraps up Phase 3 by introducing several advanced quality-of-life UI features to the Workspace: Expand All for JSON arrays, Grid configurations, Sparkline visualizers for Topic frequency, and Quick Add column actions from the tree Inspector. 

**Work Continuously:** Finish the batch without stopping and asking if it is ok to do obvious things like running the tests and fixing the root cause until all ok. No laziness allowed. You should push through until everything is functioning flawlessly and then write your report. No useless asking for permission allowed.

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/guides/DEV-GUIDE.md` - How to work with batches
2. **Task Definitions:** `docs/ddsmon/TASK-DETAIL.md ` - See DMON-030, DMON-031, DMON-032, DMON-033
3. **Design Document:** `docs/ddsmon/DESIGN.md` - Technical specifications
4. **Previous Review:** `.dev-workstream/reviews/MON-BATCH-17-REVIEW.md` - Learn from feedback

### Source Code Location
- **Primary Work Area:** `tools/DdsMonitor/` and `tools/DdsMonitor.Engine/`
- **Test Project:** `tests/DdsMonitor.Engine.Tests/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-18-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-18-QUESTIONS.md`

---

## Context

This batch focuses on UX density and user productivity. Introducing a sparkline gives real-time visual feedback on topic activity. Exporting Grid Settings ensures that long-running tasks retain customized data views. Providing an "Expand All" mode drastically improves analyzing nested payloads.

**Related Tasks:**
- [DMON-030](../../docs/ddsmon/TASK-DETAIL.md#dmon-030--expand-all-mode-json-tree-per-row) - Expand All mode (JSON tree per row)
- [DMON-031](../../docs/ddsmon/TASK-DETAIL.md#dmon-031--grid-settings-exportimport) - Grid settings export/import
- [DMON-032](../../docs/ddsmon/TASK-DETAIL.md#dmon-032--sparkline-charts-in-topic-explorer) - Sparkline charts in Topic Explorer
- [DMON-033](../../docs/ddsmon/TASK-DETAIL.md#dmon-033--quick-add-column-from-inspector) - Quick-Add column from Inspector

---

## 🎯 Batch Objectives
- Enhance real-time DDS feedback directly inside the Topic Explorer using SVG/CSS sparklines.
- Expand data grid tooling for power users, allowing settings permanence and quick layout alterations.

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1:** Implement → Write tests → **ALL tests pass** ✅
2. **Task 2:** Implement → Write tests → **ALL tests pass** ✅  
3. **Task 3:** Implement → Write tests → **ALL tests pass** ✅
4. **Task 4:** Implement → Write tests → **ALL tests pass** ✅

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written
- ✅ **ALL tests passing** (including previous batch tests)

---

## ✅ Tasks

### Corrective Tasks: Filter Builder UX & Dark/Light Theme Bugs

**Description:** Manual verification of BATCH-17 exposed three critical UX and UI bugs:
1. **Scope/UX Issue in Filter Builder:** The Filter Builder was implemented as a standalone panel that tries to query multiple target grinds. It must be refactored: Expand the Sample Panel's toolbar to include a "Filter Builder" button. Clicking this button should spawn a scoped version of the Filter Builder dedicated strictly to that single source panel, pre-loaded with its Topic Type and automatically applying results to it.
2. **Missing Enum Type Registration:** Applying an enum filter condition (e.g., `Level == "Ok"` on `Level`) results in `Unknown identifier 'DdsMonitor'`. The generic Dynamic LINQ `FilterCompiler` needs the enum type registered or needs full type-paths properly formatted without string parsing collisions.
3. **Incomplete Theme Coverage:** The Light Theme currently fails to affect inner elements—buttons, combo boxes, text inputs, and the main data grid backgrounds. Ensure these CSS classes correctly implement the `--panel-text`, `--panel-bg`, `--input-bg`, etc., fallback patterns within `:root` and `[data-theme="dark"]`.
4. **Unfiltered Generic Samples Panel & Startup Auto-subscription:** A generic "Samples" panel showing all incoming samples (unfiltered initially) must be available from the main menu. Critically, this panel must open automatically at startup alongside the Topics panel. Furthermore, all existing topics should automatically be monitored (auto-subscribed) at launch so their samples populate this unified view immediately.
5. **Context Menu Glitch:** The context menu closes immediately upon releasing the right mouse button if the cursor isn't directly over it. Modify the mouse event handling so the first click-release (the one opening the menu) doesn't instantly close it.
6. **Minimize/Restore State Loss:** When minimizing a panel (like Topics) and restoring it from the bottom bar, it resets to default size instead of its previous dimensions. Ensure restoring a minimized panel correctly reapplies its pre-minimized `Width` and `Height`.
7. **Samples Panel Workspace Memory (P1):** Opening a Samples panel (e.g., via double click) always uses default coordinates. Update it to use an index-based memory system (similar to DetailPanel) to remember location and size per index.
8. **Indexed Unfiltered Samples Panel:** The new generic Unfiltered Samples Panel should also be assigned an index (e.g., 0) so that it also restores its layout and size consistently.

**Tests Required:**
- ✅ Manual check that buttons/inputs correctly reflect the applied light theme dynamically.
- ✅ Unit test ensuring the `FilterCompiler` or `FilterNode` builder correctly structures Enum condition strings avoiding `Unknown identifier` runtime exceptions.

---

### Task 1: Expand All mode (JSON tree per row) (DMON-030)

**File:** `tools/DdsMonitor/` (UPDATE SamplesPanel)  
**Task Definition:** See [DMON-030](../../docs/ddsmon/TASK-DETAIL.md#dmon-030--expand-all-mode-json-tree-per-row)

**Description:** Add a toggle in `SamplesPanel` toggling between table view and fully expanded tree views.
**Requirements:**
- A UI toolbar toggle.
- For each virtualized sample, output an expanded colored recursive JSON tree utilizing the existing tree renderer.
- Do not break the `<Virtualize>` container's scrolling mechanisms.

**Tests Required:**
- ✅ Manual: verify toggling renders the full row tree, preserving virtualization.

### Task 2: Grid settings export/import (DMON-031)

**File:** `tools/DdsMonitor/` (UPDATE SamplesPanel)  
**Task Definition:** See [DMON-031](../../docs/ddsmon/TASK-DETAIL.md#dmon-031--grid-settings-exportimport)

**Description:** Exporting `.samplepanelsettings` and loading them back.
**Requirements:**
- Toolbar actions to `Save Settings` and `Load Settings`.
- State to handle: Active filter text, column list and order, sorted field and its direction.

**Tests Required:**
- ✅ Unit test `GridSettings_SerializeDeserialize_RoundTrips`
- ✅ Manual verification on restoring states onto a fresh panel.

### Task 3: Sparkline charts in Topic Explorer (DMON-032)

**File:** `tools/DdsMonitor/` (UPDATE TopicExplorerPanel / NEW Sparkline Component)  
**Task Definition:** See [DMON-032](../../docs/ddsmon/TASK-DETAIL.md#dmon-032--sparkline-charts-in-topic-explorer)

**Description:** Small 10-second history moving polyline showing topic samples/sec.
**Requirements:**
- A `RingBuffer<int>` tracking history in the engine or UI layer.
- Update metrics and shift the buffer once a second.
- Implement an SVGs `<polyline>` or CSS bar layout, rendered cleanly inside the topic table row.

**Tests Required:**
- ✅ Unit test `RingBuffer_RecordsCorrectFrequency` (records the right frequency count every flush).

### Task 4: Quick-Add column from Inspector (DMON-033)

**File:** `tools/DdsMonitor/` (UPDATE DetailPanel)  
**Task Definition:** See [DMON-033](../../docs/ddsmon/TASK-DETAIL.md#dmon-033--quick-add-column-from-inspector)

**Description:** Ability to pin a field from the detail payload tree to become a grid column on the spot.
**Requirements:**
- Add a pin icon to properties rendered in the Detail tree.
- Clicking the pin broadcasts an `AddColumnRequestEvent(targetPanelId, fieldPath)` via EventBroker.
- The corresponding source SamplesPanel subscribes and adds it.

**Tests Required:**
- ✅ Manual verification that clicking the pin appends it correctly dynamically to the datagrid.

---

## ⚠️ Quality Standards

**❗ TEST QUALITY EXPECTATIONS**
- **REQUIRED:** Tests that verify actual runtime properties or struct/buffer logic. Ensure `RingBuffer` boundaries act flawlessly.

**❗ REPORT QUALITY EXPECTATIONS**
- **REQUIRED:** Document issues encountered during Sparkline polling mechanisms implementations.
- **REQUIRED:** Document design decisions YOU made beyond the spec.

---

## 📊 Report Requirements

**✅ What to Answer in Your Report (`MON-BATCH-18-REPORT.md`):**

**Q1:** How did you implement the buffer flush for the Sparkline polling without lagging the Blazor renderer?
**Q2:** Does the `Expand All` structural change disrupt the `Virtualize` row sizing? How did you approach smooth scrolling?
**Q3:** During grid setting restoration, how are mismatched column layouts resolved if the topic type structures shifted underneath? 
**Q4:** Did you note any visual noise added to the Tree View with the new Quick-Add icons? How did you mitigate layout shifts?
**Q5:** What issues did you encounter and how did you resolve them?

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] DMON-030 completed (Extensive JSON tree rendering handles virtualization)
- [ ] DMON-031 completed (Grid setting payloads exported and imported securely)
- [ ] DMON-032 completed (Robust performance 10-sec topic history charts)
- [ ] DMON-033 completed (Quick-Add event cleanly pushes to SamplesPanel without reloads)
- [ ] Corrective tasks completed (Filter Builder UX, Enum fix, Theme coverage, Startup Unfiltered Samples)
- [ ] Required Unit tests run green 
- [ ] `MON-BATCH-18-REPORT.md` submitted addressing required developer insights
