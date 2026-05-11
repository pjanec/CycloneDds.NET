# BATCH-19: Heavy UI Bugfixes and Code Deduplication (Rework BATCH-18)

**Batch Number:** MON-BATCH-19  
**Tasks:** Core UI/UX Rework, Code Deduplication  
**Phase:** Phase 3 Corrections  
**Estimated Effort:** 4-6 hours  
**Priority:** CRITICAL  
**Dependencies:** MON-BATCH-18

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome to BATCH-19. The previous developer completely failed to follow precise instructions and violated the DRY principle, introducing a host of bugs while attempting to resolve previous debt. Your job is to clean up this mess immediately.

**Work Continuously:** Finish the batch without stopping. Do not ignore ANY of the bugs listed below. Read each line carefully. Failure to fix these exact UI details is unacceptable.

### Source Code Location
- **Primary Work Area:** `tools/DdsMonitor/` and `tools/DdsMonitor.Engine/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-19-REPORT.md`

---

## 🎯 Batch Objectives: CRITICAL BUG FIXES & REWORK

**CRITICAL: You MUST complete these tasks and verify them manually or structurally before marking them done.**

### Task 1: Refactor `AllSamplesPanel` into `SamplesPanel` (DRY Violation)
- **Issue:** The previous developer created a separate `AllSamplesPanel.razor`, which is missing the standard toolbar buttons and the "D" inspect action column.
- **Fix:** DELETE `AllSamplesPanel.razor`. Refactor `SamplesPanel.razor` so that it can operate in an "All Topics" (unfiltered) mode while sharing 100% of the UI shell (toolbars, data grid features, action column). The "All Samples" instance must have all standard features of a topic-specific window.
- **Requirement:** Assign this universal "All Samples" window an index of `0` so it correctly saves and restores its location and size in the Workspace manager.
- **Success Criteria:** `AllSamplesPanel.razor` is deleted. A single `SamplesPanel.razor` operates flawlessly for both generic and topic-specific queries. The universal generic mode clearly inherits the standard toolbars and "D" inspect action columns.

### Task 2: Fix Samples Window Location/Size Memory (P1)
- **Issue:** Opening a specific Topic's samples window via double-click still opens at a default location, discarding layout memory.
- **Fix:** Implement robust layout memory using the exact same principles used for Detail Panels: assign each samples window an index, and persist/restore its specific `X`, `Y`, `Width`, and `Height` using that index.
- **Success Criteria:** Samples windows (both topic-specific and indexed 0) consistently remember and restore their X, Y, Width, and Height attributes across closures and initializations.

### Task 3: Fix Context Menu Glitch
- **Issue:** The context menu opens on `mousedown` (right click) but instantly closes on the first `mouseup` if the mouse cursor isn't physically over the instantiated menu div (which usually spawns below the cursor). 
- **Fix:** Update the global click/mouseup interceptor to completely ignore the very first `mouseup` event that immediately follows the `contextmenu` activation.
- **Success Criteria:** Upon right clicking to invoke the context menu, releasing the right mouse button does not close the menu under any circumstance (even if the mouse pointer is not hovering over the newly rendered menu).

### Task 4: Filter Window State Memory
- **Issue:** Filter dialogs always spawn at default coordinates.
- **Fix:** Filter windows must remember their specific location/size based on the identity of their parent Samples window. Use the parent's identifier or index to form a unique persistence key for the filter panel.
- **Success Criteria:** Closing and re-opening a specific Filter panel correctly spawns it at its last known customized size and screen coordinate.

### Task 5: Pre-filled `"id"` Bug in Filter Builder
- **Issue:** Adding a new condition pre-fills the field drop-down text with `"id"`, visually preventing users from selecting genuine fields from the topic metadata payload.
- **Fix:** Ensure new conditions start blank, showing a proper placeholder, and forcing the user to select from the available combo-box list without hardcoded interference.
- **Success Criteria:** Adding a condition rule results in an empty field placeholder; the hardcoded `"id"` text is entirely absent.

### Task 6: Topics Panel Filter Bar Redesign
- **Issue:** The filter bar in the Topics window is currently bulky, takes up too much height, uses too large of a font, and looks exactly like normal buttons.
- **Fix:** Condense it. Use a much smaller font. Change its CSS styling so it looks like a sleek, compact tagging/filtering strip, visually distinct from standard UI action buttons.
- **Success Criteria:** The Topics window filter bar is radically reduced in vertical footprint and is styled differently (e.g. as a distinct tag/pill strip) from standard heavy application buttons.

### Task 7: Replace Text Toolbars with Icons
- **Issue:** The main toolbars across panels (Samples, Details, Topics) use cheap-looking text buttons.
- **Fix:** Replace all top-level panel toolbar buttons with high-quality SVG/Font graphical icons. They must include descriptive `title` (tooltip) text for accessibility. 
- **Success Criteria:** Text labels are gone from the toolbars on Samples, Details, and Topics panels—replaced entirely by clean iconography featuring HTML `title` tooltips. 

---

## 📊 Report Requirements

**✅ What to Answer in Your Report (`MON-BATCH-19-REPORT.md`):**

1. Explain how you achieved the unified `SamplesPanel` architecture without breaking specific-topic bindings.
2. Outline the exact event sequence used to fix the context menu `mouseup` glitch.
3. Detail how index-based dimensions are now assigned and recovered for generic and specific Samples Panels.

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] `AllSamplesPanel.razor` is deleted and `SamplesPanel` handles all traffic.
- [ ] Double-clicked topic samples windows restore their absolute screen bounds correctly.
- [ ] The global "All Samples" window restores to index 0.
- [ ] Context menus stay open after right-clicking.
- [ ] Filter dialogs remember their bounds.
- [ ] The `"id"` pre-fill bug is removed from the filter builder.
- [ ] Toolbars use colorful icons with tooltips instead of text blocks.
- [ ] The Topic Filter bar is distinctly sleeker and smaller.
