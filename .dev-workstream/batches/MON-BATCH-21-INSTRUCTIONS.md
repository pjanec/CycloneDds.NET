# BATCH-21: Exhaustive UI Polish and Interaction Fixes

**Batch Number:** MON-BATCH-21  
**Tasks:** UI Adjustments, Default Sizings, Filter Compiler Fixes, Persistence Polish  
**Phase:** Phase 3 Corrections  
**Estimated Effort:** 3-5 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-20

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome to BATCH-21! Although the core functionality of Phase 3 is implemented, further manual verification found a litany of precise UI and UX interaction defects. Details matter heavily here.

**Work Continuously:** Push through the fixes without stopping. Ensure you test your changes thoroughly manually after writing the code, especially regarding sizes and persistence behavior.

### Source Code Location
- **Primary Work Area:** `tools/DdsMonitor/` and `tools/DdsMonitor.Engine/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-21-REPORT.md`

---

## 🎯 Batch Objectives: CORRECTIVE TASKS

**CRITICAL: You MUST complete these tasks and satisfy the exact success criteria before concluding the batch.**

### Task 1: Main Menu Interaction Fix
- **Issue:** Clicking an actionable item inside the new main menu pull-down popups executes the action but leaves the pull-down menu visually open.
- **Fix:** Hook the action links/buttons within the dropdowns so that they invoke the `CloseMenus()` or toggle clear logic simultaneously.
- **Success Criteria:** Clicking any item within the pull-down menu (e.g. "Topics", "All Samples") successfully launches the panel and immediately closes the dropdown menu overlay.

### Task 2: Persistence for Topics & Instances Panels
- **Issue:** The main Topics window and the topic-specific Instances windows do not recall their last position and size when opened.
- **Fix:** 
  1. For the Topics panel, bind it to the same strict workspace/hidden state reuse logic. If it is dismissed and re-launched, it must un-hide the exact previous panel state instead of creating a new one.
  2. For Instances panels, assign them a persistence key/index based on the Topic Type so each discrete Topic Instances panel remembers its specific dimensions.
- **Success Criteria:** Discarding and re-opening the Topics window or any Topic Instance window restores the exact X, Y, Width, and Height from before it was closed.

### Task 3: Global Default Dimension Scales
- **Issue:** Several panels spawn with default sizes that are uncomfortably narrow or small for viewing complex data.
- **Fix:** 
  - Topics Panel Default Width: **Increase by 2x (e.g., multiply the default Width constant by 2).**
  - Samples Panel Default Width (All & Topic-specific): **Increase by 2x.**
  - Instances Panel Default Width: **Increase by 2x.**
  - Filter Builder Windows Default Width: **Increase by 2x.**
  - Sample Details Window Default Width: **Increase by 1.3x (30%).**
- **Success Criteria:** Opening these panels for the *very first time* (before user resizing/memory takes over) spawns them with the substantially widened default dimensions according to the ratios above.

### Task 4: Card View Formatting (Expand All Mode)
- **Issue:** The header within the JSON payload expanded cards is lacking critical metadata. It currently shows: `#35  12:32:16.242  o` instead of the Topic Name and timestamp in brackets.
- **Fix:** Update the expanded card rendering string layout.
- **Success Criteria:** The card header string must exactly match this readable format: `#35  [12:32:16.242]  <TopicName>`

### Task 5: Topic "Subscribe" Checkbox Sync
- **Issue:** The subscribe toggle checkboxes rendered on the Topic Explorer table rows are not checked visually, even though those topics are actively monitored and receiving messages.
- **Fix:** Synchronize the check state of these toggles with the actual background subscription state driven by the Engine/EventBroker. 
- **Success Criteria:** Topics that are automatically receiving messages display checked boxes in the grid from the moment the panel opens.

### Task 6: Global Filter Builder Metadata Crash
- **Issue:** In the "All Samples" window, adding a Payload Field filter (e.g. `Id == 2`) and applying it results in a red UI error: `Topic metadata is required for payload field filters.` No filtering occurs.
- **Fix:** The `FilterCompiler` relies on `TopicMetadata` for strongly typing LINQ compilation. For the "All Samples" mode (where Metadata is null), you must gracefully allow the expression to compile dynamically across heterogenous samples. Either handle `null` metadata safely in `FilterCompiler` using C# dynamic invocation properties, or fallback to returning `false`/skipping samples safely if they don't possess the named property.
- **Success Criteria:** Applying `Id == 2` inside the All Samples Filter Builder filters down to *any* sample possessing an `Id` property equal to `2` across *all* active topics. It does not throw an error.

### Task 7: Visual Negation Indicator in Filter Builder
- **Issue:** Clicking the `!` negation button on a rule condition applies the condition logic, but gives zero visual feedback that the rule is inverted.
- **Fix:** Add a dynamic CSS class to the `!` button that changes its background color (e.g., active highlight color) when `IsNegated == true`.
- **Success Criteria:** The `!` toggle button visibly stays "pressed" or "highlighted" with a distinct background color while a rule condition is negated.

### Task 8: Filter Compiler String Methods Crash
- **Issue:** Adding a string condition like `Message contains "abc"` results in a red error: `Unknown payload field 'Message.Contains'.`
- **Fix:** The `FilterCompiler`'s AST-to-LINQ parser or the dynamic type resolution is misinterpreting standard .NET string method calls (like `.Contains()`, `.StartsWith()`, `.EndsWith()`) as nested payload field names. Ensure these method calls are properly executed dynamically against the string field values instead of being passed to the `_GetPropertyValue` logic.
- **Success Criteria:** String operators (`Contains`, `StartsWith`, `EndsWith`) in the Filter Builder compile correctly and filter target samples effectively without throwing "Unknown payload field" errors.
### Task 9: Timestamp Filter Builder Crash
- **Issue:** In the filter dialog, using the `Timestamp` field (or other synthetic wrapper fields) in a condition results in the red error `"Unknown payload field 'Timestamp'."`
- **Fix:** Synthetic properties like `Timestamp` belong to the `Sample` wrapper object, not the inner `Payload` dictionary. The `FilterCompiler` must be updated to correctly target the `Sample` object itself when fields resolving to known top-level wrapper properties (Timestamp, Ordinal, TopicId, Status, etc.) are selected, rather than blindly assuming every field lives inside the nested `Payload`.
- **Success Criteria:** Filtering by `Timestamp` succeeds logically without producing the `"Unknown payload field"` error.

### Task 10: "Subscribe All" Descriptor Ops Error
- **Issue:** Clicking "Subscribe all" in the topics window yields a red error: `"Skipped 2 topic(s) without descriptor ops: SelfTestSimple, SelfTestPose"`.
- **Fix:** The batch subscription logic must gracefully handle test topics or dynamically discovered topics that lack type descriptors. Instead of throwing a loud, disruptive exception/toast that interrupts the user and looks like a crash, gracefully catch this and either skip those specific topics silently or present a mild informational warning that doesn't trigger the red error UI.
- **Success Criteria:** "Subscribe All" handles complex/test workspaces smoothly and automatically ignores/handles topics without descriptor ops without showing disruptive red error toasts.

---

## 📊 Report Requirements

**✅ What to Answer in Your Report (`MON-BATCH-21-REPORT.md`):**

1. Explain the changes made to the `FilterCompiler` allowing it to compile rules without a strict `TopicMetadata` instance.
2. Detail how the Instances panels calculate a unique index for persistence.
3. List the numerical width values assigned as the new defaults for the requested panels.

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] Task 1: Main menu popups auto-close on selection.
- [ ] Task 2: Topics and Instances panels correctly persist their full geometries.
- [ ] Task 3: 200% width buffs applied to Topics, Samples, Instances, Filters & a 130% buff to Details.
- [ ] Task 4: Extended card headers show `#XX [TIME] TOPICNAME`.
- [ ] Task 5: Topic subscriptions cleanly sync their UI toggle visual bounds.
- [ ] Task 6: Global samples panel filtering succeeds on payload fields without crashing.
- [ ] Task 7: The negation toggle has an active visual state.
- [ ] Task 8: String operators (Contains, StartsWith) succeed without compiler crashing.
- [ ] Task 9: Synthetic wrapper fields like `Timestamp` compile and filter correctly.
- [ ] Task 10: "Subscribe All" smoothly handles topics lacking descriptor ops without errors.
- [ ] Required Unit tests pass, especially covering the updated dynamic `FilterCompiler` paths.
