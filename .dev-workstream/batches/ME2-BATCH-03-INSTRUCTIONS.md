# ME2-BATCH-03: Replay Stability, Null Serialization, and UX Adjustments

**Batch Number:** ME2-BATCH-03  
**Tasks:** ME2-T20 (Tech Debt), ME2-T16, ME2-T17, ME2-T18, ME2-T19  
**Phase:** Phase 9 (Replay Stability, Null Serialization, and UX Adjustments)
**Estimated Effort:** 8-10 hours  
**Priority:** HIGH  
**Dependencies:** ME2-BATCH-02  

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome back! This batch zeroes in on resolving technical debt discovered in the previous tracker mode improvements, along with critical UX feedback received for null serialization and sampling controls. 

> **IMPORTANT ANNOUNCEMENT (AI Coding Agent Note):** 
> As an AI coding agent, you have access to the Playwright MCP server to control the browser natively. **You MUST NOT ask the user for manual UI testing.** Open the browser using your tools, run the web application, interact with the UI, and verify your changes directly. You must finish the whole batch autonomously until all functionality is perfectly working.

**Important Rule:** Finish the batch without stopping. Do not ask for permission to do obvious things like running tests or fixing root causes until everything works. Laziness is not allowed. 

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/guides/DEV-LEAD-GUIDE.md`
2. **Task Tracker & Details:** `docs/mon-ext-2/ME2-TASK-TRACKER.md` (See Phase 9 Phase goals).
3. **ME2-BATCH-02 Report:** `.dev-workstream/reports/ME2-BATCH-02-REPORT.md` - Please read Q2 regarding the track mode array boundaries (`ApplySortToViewCache`) to address `ME2-T20`.

### Source Code Location
- **Main Toolset Application:** `tools/DdsMonitor/` (Blazor UI and Engine)

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/ME2-BATCH-03-REPORT.md`

---

## 🎯 Batch Objectives
- **Fix Data Integrity Rules:** Secure Replay Mode sorting against out-of-order telemetry processing determinism logic.
- **Fix Formatting/Math Engines:** Resolve the `Delay` calculations yielding inverted metrics, and verify exactly how null strings bypass strict serialization rendering.
- **Elevate UX Filtering:** Add standard filter-clearing icons. Implement smart persistence logic over grid configurations.

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1:** Implement → Write tests → **ALL tests pass** ✅
2. **Task 2:** Implement → Write tests → **ALL tests pass** ✅  
3. ...

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written
- ✅ **ALL tests passing** (including previous batch tests)

---

## ✅ Tasks

### Task 1: (Tech Debt) Fix `ApplySortToViewCache` Determinism Hazard (ME2-T20)
**Files:** `SamplesPanel.razor.cs`
**Requirements:**
- Resolve the O(N) sort vulnerability outlined in the BATCH-02 report. Specifically, if `FixedSamples` contains out-of-order ordinals during Replay Mode, the `ApplySortToViewCache` `Reverse()` fast-path algorithm is factually incorrect.
- Enforce standard `Sort` overhead for scenarios where data might not be strictly monotonic, or implement a safe validation pass before reverting to O(N).

### Task 2: Fix Dynamic String Serialization Mapping (ME2-T16)
**Files:** `DynamicString` serialization mappings / `DynamicForm.razor` / `DetailPanel.razor` / JSON logic 
**Description:** Unchecking the "non-null" form checkbox passes `null` to the payload setter, but its output representation continues to display/export as `""` (empty string) instead of properly retaining the `null` intent.
**Requirements:**
- Investigate why DDS string parameters fail to retain nullable value intents across the framework payload logic.
- Prevent dynamic string marshalling logic from defaulting to empty strings during serialization drops.

### Task 3: Samples Panel Filter Box Clear Action (ME2-T17)
**Files:** `SamplesPanel.razor`
**Requirements:**
- Embed a standard `[x]` clear icon (red cross format) directly to the right of the existing query textbox to easily wipe the sample filter field.

### Task 4: Samples Panel Column Configuration Persistence (ME2-T18)
**Files:** `SamplesPanel.razor`, `SamplesPanel.razor.cs`
**Requirements:**
- The list of columns currently selected must cleanly push/pull from the workspace panel persistence layer. 
- Introduce a clear UX "reset columns" action inside/near the column picker component returning back to standard defaults.
- Default selection configuration MUST be limited specifically to: `Topic` and `Timestamp` (specifically the incoming system reception time, not the telemetry source stamp).

### Task 5: Delay Column Timing Arithmetic Correction (ME2-T19)
**Files:** `SamplesPanel.razor.cs` / `FormatValue` logic
**Description:** "The Delay column is always showing a very high negative number."
**Requirements:**
- Trace how Delay calculations extract timestamp fields. A common mistake here subtracts reception from send (or fails to account for millisecond/tick/nanosecond arithmetic mismatch between DDS epochs and local dot-net environments).
- Fix the logic to result in positive millisecond delay counts for localized loopbacks.

---

## 🧪 Testing Requirements
- Unit tests must prove that the fast-sorting bypass prevents corruption internally.
- Use Playwright MCP to manipulate grid filtering constraints and layout caching. Reload the workspace page to assert the `[x]` clearing works, and the selection persistence remains fully retained over `Topic` / `Timestamp` baseline mappings. 

## 📊 Report Requirements

Provide professional developer insights matching Q1-Q5 guidelines. Expose exact calculations discovered around the high-negative DateTime arithmetic logic. Detail how the JSON logic for string fields was addressed.

---

## 🎯 Success Criteria
- [ ] Task ME2-T20 sorted payload ordering logic functions correctly for unordered items.
- [ ] Task ME2-T16 null strings propagate cleanly through dynamic serializers without transforming into `""`.
- [ ] Task ME2-T17 red-cross clear button removes query artifacts explicitly.
- [ ] Task ME2-T18 Workspace caching retains user's specifically picked grid fields.
- [ ] Task ME2-T19 The delay metric mathematically evaluates as `# >= 0`.
- [ ] Successful Playwright test confirming column persistence rules.
- [ ] All unit tests pass cleanly.
