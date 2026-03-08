# BATCH-25: Replay UX, File Dialogs & Export Polish

**Batch Number:** MON-BATCH-25  
**Tasks:** Export Format Fixes, File Browser Dialog, Replay Panel Redesign, Replay Filtering  
**Phase:** Phase 4 Finish  
**Estimated Effort:** 6-8 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-24

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome back! The user has tested the Export/Import and Replay features from Batch 24 and provided significant feedback. Before moving into Phase 5 (Plugins), we must polish Phase 4.

The feedback centers around three main areas:
1. **Export Format:** Tweaking exactly what is serialized.
2. **File Selection:** Replacing basic text inputs with a fully-fledged custom File Browser dialog.
3. **Replay UX:** Massively upgrading the Replay Panel with scrubbing features, time/frame modes, stepping, and filtering via the Samples Panel.

### Source Code Location
- **Primary Work Area:** `tools/DdsMonitor/` and `tools/DdsMonitor.Engine/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-25-REPORT.md`

---

## 🎯 Batch Objectives

### Task 1: Export Format Adjustments
- **Fix `ExportService.cs`:** 
  - Do NOT serialize the `SizeBytes` field.
  - The `TopicTypeName` must be serialized using the type's full namespace (e.g. `Type.FullName`) rather than `AssemblyQualifiedName`. Adjust `ImportService` to fallback or parse this accordingly.

### Task 2: Custom File Browser Dialog
- **Implement:** A new Blazor component (`FileDialog.razor` or similar) to replace the primitive `prompt` and text-input methods for file selection.
- **Requirement:** This dialog must query the backend's filesystem (since this is a Blazor Server app, the backend has access to local drives). 
- **Requirement:** Allow jumping into folders, traversing up/down the directory tree, and selecting files.
- **Requirement:** Include an editable text box where the user can manually type a folder or full file path.
- **Requirement:** Integrate this dialog into BOTH the `SamplesPanel` (for export) and `ReplayPanel` (for load).

### Task 3: Replay Panel Redesign & Scrubbing
- **Redesign:** Clean up `ReplayPanel.razor` with a modern, spacious layout. Avoid tightly condensed elements.
- **Controls & Stepping:** Implement a "Step" button to move to the exact next sample when playback is paused.
- **Display Metrics:** 
  - Show the total duration of the recording and the absolute time range (from initial timestamp to final timestamp).
  - Show the current Replay Clock timestamp and the relative time from the beginning of the recording.
  - If paused, show the timestamp of the *next* sample to be replayed.
- **Scrubber Slider:** Implement a progress slider that supports scrubbing. Moving the slider while paused must update the read position; unpausing or stepping will play from that position.
- **Mode Toggle (Frames vs Time):** The scrubber and positional tracking must support switching between:
  - **Frames Mode:** Index-based (Sample 0 is the first in recording; note: NOT the original saved `Ordinal`).
  - **Time Mode:** Relative or absolute time-based tracking.
- **Jump-To Input:** Provide a textbox to enter a target position (frame index or time) to jump the replay head to immediately. Support both absolute and relative time parsing if in Time mode.

### Task 4: Replay Filtering via Samples Panel
- **Implement Integration:** Add a button in the `ReplayPanel` to open a new `SamplesPanel` pre-loaded with the currently ingested Replay samples rather than the live DDS store.
- **Requirement:** The Replay Engine must synchronize with this specific `SamplesPanel`'s filter state. If the user applies a filter in that window, the Replay Engine should only playback the *filtered* subset of samples.

---

## 📊 Report Requirements

**✅ What to Answer in Your Report (`MON-BATCH-25-REPORT.md`):**

1. Explain how the Replay Engine manages its playback pointer when switching between Frame mode and Time mode.
2. Describe how the Replay Engine dynamically respects the active filter applied by the connected Samples Panel.

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] Export format is corrected (`SizeBytes` removed, `FullName` used).
- [ ] Backend-driven File Browser dialog is implemented and wired up.
- [ ] Replay Panel is overhauled with modern UX, stepping, and detailed time metrics.
- [ ] Replay scrubber works correctly under both Frame and Time modes.
- [ ] Replay playback can be filtered directly via an attached Samples Panel.
- [ ] Ensure all code adheres to previous structural layout rules!
