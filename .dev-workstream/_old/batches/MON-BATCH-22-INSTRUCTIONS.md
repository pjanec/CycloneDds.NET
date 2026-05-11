# BATCH-22: Final Bug Prioritization (Dropped Tasks & UI Desync)

**Batch Number:** MON-BATCH-22  
**Tasks:** Core UI State Rendering, Filter string/synthetic operator compilation  
**Phase:** Phase 3 Corrections  
**Estimated Effort:** 2 hours  
**Priority:** CRITICAL  
**Dependencies:** MON-BATCH-21

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome to BATCH-22. The previous developer pushed an incomplete payload and completely missed tasks 8, 9, and 10 from the instructions, whilst failing to truly solve the UI checkbox desync (Task 5). 

**Work Continuously:** Finish the batch without stopping. Do not ignore ANY of these bugs! 

### Source Code Location
- **Primary Work Area:** `tools/DdsMonitor/` and `tools/DdsMonitor.Engine/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-22-REPORT.md`

---

## 🎯 Batch Objectives: REWORK

**CRITICAL: You MUST complete these tasks and verify them logically.**

### Task 1: Checkbox UI Desync & Startup Auto-Subscription
- **Issue:** Topics are receiving messages, but their checkboxes in the `TopicExplorerPanel` are not checked visually. Furthermore, "Subscribe All" acts inconsistently.
- **Fix Constraints:** 
   1. Ensure that if topics are being monitored, `topic.IsSubscribed` is genuinely resolving to `true` and the DOM checkbox correctly binds (ensure `@bind-value` isn't needed over `@onchange` + `checked`).
   2. Ensure that at Application/Desktop startup, **ALL existing topics are fundamentally auto-subscribed** so the "All Samples" panel actually populates automatically. If they are auto-subscribed, they MUST show as checked in the topics explorer.
- **Success Criteria:** All available topics are automatically subscribed upon application launch, and their corresponding toggles in the Topics UI reflect a checked, synchronized state without manual intervention.

### Task 2: Filter Compiler String Methods Crash (Formerly Task 8)
- **Issue:** Adding a string condition like `Message contains "abc"` results in a red error: `Unknown payload field 'Message.Contains'.`
- **Fix:** The `FilterCompiler`'s AST-to-LINQ parser or the dynamic expression compiler is misinterpreting standard .NET string method calls (like `.Contains()`, `.StartsWith()`, `.EndsWith()`) as nested payload field paths. Ensure these operator paths intercept string method execution and convert them to valid `MethodCallExpression` instances dynamically instead of pushing them into `_GetPropertyValue()`.
- **Success Criteria:** String operators (`Contains`, `StartsWith`, `EndsWith`) in the Filter Builder apply successfully without compiling into "Unknown payload field" exception traps.

### Task 3: Timestamp Filter Builder Crash (Formerly Task 9)
- **Issue:** In the filter dialog, using the `Timestamp` field (or other synthetic wrapper fields) in a condition results in the red error `"Unknown payload field 'Timestamp'."`
- **Fix:** Synthetic properties like `Timestamp`, `Ordinal`, `TopicId`, or `Status` belong to the `Sample` wrapper object, not the inner `Payload` dynamic dictionary. `FilterCompiler` must intercept queries targeting these known wrapper fields and redirect the reflection/property-getter to the `Sample` base object instead of blindly looking inside `Payload`.
- **Success Criteria:** Filtering by `Timestamp` applies flawlessly without throwing layout errors.

### Task 4: "Subscribe All" Graceful Error Handling (Formerly Task 10)
- **Issue:** Clicking "Subscribe all" in the topics window yields a red error: `"Skipped 2 topic(s) without descriptor ops: SelfTestSimple, SelfTestPose"`.
- **Fix:** Update the `ToggleAllSubscriptions` batch subscription logic in `TopicExplorerPanel.razor`. It must gracefully handle test topics or dynamically discovered topics that lack type descriptors. Catch these "descriptor ops" errors implicitly and skip filtering them into the `_subscriptionError` variable unless explicitly required. Hide these expected noisy exceptions from the UI.
- **Success Criteria:** Pressing "Subscribe All" handles structurally naked topics gracefully without flashing severe red error messages on the screen.

---

## 📊 Report Requirements

**✅ What to Answer in Your Report (`MON-BATCH-22-REPORT.md`):**

1. Explain how you anchored the auto-subscription to application startup so the checkboxes and topic readers sync globally.
2. Outline the expression replacement used to correctly evaluate `string.Contains` in the dynamic AST builder.

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] Task 1: Checkboxes map 1:1 with auto-subscribed startup readers.
- [ ] Task 2: Contains/StartsWith operator string bug is fixed.
- [ ] Task 3: The `Timestamp` alias points to the top-level Sample object in the expression tree.
- [ ] Task 4: Subscribe All skips testing topics without panicking the UI state.
- [ ] 100% test passage.
