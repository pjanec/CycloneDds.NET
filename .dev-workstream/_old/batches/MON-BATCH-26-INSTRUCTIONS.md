# BATCH-26: Replay Polish II & Universal Toolbar Harmony

**Batch Number:** MON-BATCH-26  
**Tasks:** Replay Slider Interactions, SampleState Export, Icon Tracking, Visual Indicators  
**Phase:** Phase 7 (UX Polish)  
**Estimated Effort:** 4-6 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-25

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome back! You nailed the file dialogs and the structure of the Replay Engine. But the user has tested it extensively, and found gaps in the UI response timing, serialized data depth, and general layout aesthetics.

We are taking a quick detour into Phase 7 UX Polish (DMON-058) to shore up these features before we open the Plugin pipeline.

### Source Code Location
- **Primary Work Area:** `tools/DdsMonitor/Components/` and `tools/DdsMonitor.Engine/Export/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-26-REPORT.md`

---

## 🎯 Batch Objectives

### Task 1: Replay UX Interactivity
- **Target Selection Config:** Remove the "Target" option (Local GUI / DDS) from the Replay Panel. Hardcode the Replay operation to target the DDS Network (live playback).
- **Track-bar Scrubbing:** During standard playback, the trackbar should automatically advance and should not be interactively draggable (lock it). When paused, grabbing and sliding the bar must *interactively* update the `CurrentTimestamp` and `CurrentRelativeTime` elements in real-time, regardless of mode (Time / Frames). Do not wait for drop/mouseup to update the UI readouts.
- **Recording File Persistence:** Ensure `_filePath` is retained and visibly displayed within the text box of the File layout after the import concludes successfully.

### Task 2: Advanced "Next Sample" Previewing
- **Enhance the "Step" feature:** Introduce a "Next Sample" readout active exclusively when paused.
- **Requirements:** 
  - Print the sample ordinal and struct Type.
  - Expose a "D" icon action button (mirroring `SamplesPanel`).
  - Hovering the "D" icon previews the JSON of the pending sample. Clicking it spans a `DetailPanel` specifically hooked to that sample.

### Task 3: Missing State Serialization (Alive/Disposed)
- **Problem:** Recorded playbacks render entirely as DISPOSED in the `SamplesPanel`.
- **Fix:** Update `ExportService.cs` and `ImportService.cs` so that the `SampleInfo`'s `InstanceState` (Alive vs NotAliveDisposed vs NotAliveNoWriters) is securely written to JSON and reconstructed.

### Task 4: SampleState Visual Indicators
- **DetailPanel Upgrade:** The `DetailPanel` currently does not surface if the payload is Alive or Disposed! Add a distinct indicator icon beside the layout tree. Apply strict muted color-coding (e.g., dark green for Alive, dark red for Disposed), with its textual representation as a tooltip.
- **SamplesPanel Alignment:** Ensure the `SamplesPanel` leverages this exact same color palette for its status circles, and add the textual version into an `onmouseenter` tooltip so users learn what the dot colors mean.

### Task 5: Universal Toolbar Consolidation & Colorization
- **Browse Action:** in the replay panel, Inject a new icon representing the "Browse & filter replay samples" button in (a new) primary icon toolbar on the Replay Panel. And remove the big button from the bottom of the replay panel.

- **Color Aesthetics:** Standardize the SVG icons across all Component panels by assigning *muted foreground colors*. Instead of pure whites or pure grays, tint icons based on function (e.g. muted cyan for navigation, muted green for actions/adds, muted yellow for UI layouts, muted red for deletion/reset).

---

## 📊 Report Requirements

**✅ What to Answer in Your Report (`MON-BATCH-26-REPORT.md`):**

1. Clarify how Blazor binds the trackbar's physical dragging to DOM update cycles `oninput` vs `onchange` when updating real-time readouts.
2. Outline how you injected `InstanceState` serialization backwards compatible with the Batch 24 JSON structure.

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] The "Local Store" target configuration block is successfully excised.
- [ ] Scrubbing the slider instantly reflects tracking times without waiting for a mouse-drop.
- [ ] "Next Sample" details are previewable/accessible via the paused UI.
- [ ] `InstanceState` properties survive the Export/Import JSON conversion cycle, repairing Disposed states.
- [ ] `SampleState` is visibly color-coded (text/icon) in `DetailPanel` and `SamplesPanel`.
- [ ] Filter text box shifted seamlessly to the top toolbar; all SVG toolbar icons sport harmonious muted tints.
