# BATCH-20: Final Polish, Main Menu Redesign & Detail Persistence

**Batch Number:** MON-BATCH-20  
**Tasks:** Menu Redesign, Layout Persistence, Dynamic Column Aggregation  
**Phase:** Phase 3 Corrections & Debt Clearance  
**Estimated Effort:** 3-5 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-19

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome to BATCH-20! You are tasked with finalizing the Phase 3 UI adjustments. The foundation of the layout is solid, but there are several exact, specific interaction bugs and architectural "loose ends" that must be resolved. We are heavily redesigning the main menu from a horizontal button row into a standard pull-down style menu, and fixing persistence on Detail Panels.

**Work Continuously:** Finish the batch without stopping. Do not ignore ANY of the bugs listed below. Read each line carefully. 

### Source Code Location
- **Primary Work Area:** `tools/DdsMonitor/` and `tools/DdsMonitor.Engine/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-20-REPORT.md`

---

## 🎯 Batch Objectives

**CRITICAL: You MUST complete these tasks and verify them manually before marking them done.**

### Task 1: Sample Detail Window Persistence
- **Issue:** Sample Detail windows are not recalling their location or size (spawning at defaults).
- **Fix:** Detail windows must remember their specific `X`, `Y`, `Width`, and `Height` based strictly on their assigned window index. Connect the Detail panel spawn logic to the Workspace layout tracking so it securely reloads its bounds upon opening.
- **Success Criteria:** Double clicking a row to open an indexed Detail window repeatedly spawns it at its last-moved coordinate and size.

### Task 2: Main Menu "Filter Builder" Removal
- **Issue:** The main launcher menu still contains a raw "Filter Builder" button.
- **Fix:** Remove this button completely. Filter builders are now strictly scoped to their originating Samples panel toolbar.
- **Success Criteria:** The "Filter Builder" standalone button no longer exists in the top-level main menu.

### Task 3: Main Menu "All Samples" Launcher
- **Issue:** There is no way for a user to explicitly launch the unfiltered generic "All Samples" view if they close it.
- **Fix:** Add a dedicated item to the main menu specifically for launching the "All Samples" window (assigning it index 0).
- **Success Criteria:** Clicking the "All Samples" menu element reliably brings index 0's universal sample grid into focus (or creates it if hidden).

### Task 4: Unified Column Picker for "All Samples"
- **Issue:** The `ColumnPicker` for the `SamplesPanel` operating in "All Topics" mode (`TopicMetadata == null`) is currently showing 0 selectable columns.
- **Fix:** When `TopicMetadata` is null, dynamically combine/aggregate all unique fields from EVERY registered topic in the `TopicRegistry`. The column picker must show this aggregate list so universal columns (matching by string path) can be toggled. The data grid must gracefully render an empty/null cell if a specific sample does not contain that field.
- **Success Criteria:** Opening the columns modal inside the "All Samples" panel displays a deduplicated master list of all application data fields.

### Task 5: Main Menu Pull-Down Redesign (DEBT-012)
- **Issue:** The main top-level menu simply looks like a row of horizontal buttons. This is cheap and non-standard.
- **Fix:** Redesign the top launcher menu into a standard OS-style "Pull Down" menu (e.g. A "File", "View", "Windows" structured bar) where nested elements drop downwards vertically instead of sitting side-by-side in a single strip. Move all existing launch buttons (Topics, Workspace, All Samples, Settings) into logical drop-down categories.
- **Success Criteria:** The top bar is a sleek pull-down structure utilizing nested lists or standardized dropdown overlays.

---

## 📊 Report Requirements

**✅ What to Answer in Your Report (`MON-BATCH-20-REPORT.md`):**

1. Explain the mechanism used to aggregate the distinct column names across all topics in the runtime without blocking the UI thread.
2. Outline how you successfully attached persistence ID logic to the Sample Detail panels over their lifecycle indices.
3. Detail the CSS / Blazor strategy used to construct the new Pull-Down Main Menu.

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] Detail panels remember and load exact dimensions per index.
- [ ] The "Filter Builder" button is fully removed from the Desktop/Main Menu.
- [ ] The Main Menu cleanly implements a vertical Pull-Down dropdown structure.
- [ ] An "All Samples" launcher is available in the Main Menu.
- [ ] The Column picker for index 0 (All Samples) populates every topic field dynamically.
