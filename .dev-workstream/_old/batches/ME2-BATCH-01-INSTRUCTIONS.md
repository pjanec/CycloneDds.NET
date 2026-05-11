# ME2-BATCH-01: Foundational Framework and Bug Fixes

**Batch Number:** ME2-BATCH-01  
**Tasks:** ME2-T01, ME2-T02, ME2-T03, ME2-T04, ME2-T05, ME2-T06, ME2-T07
**Phase:** Phase 1 (Bug Fixes), Phase 2 (Detail Panel Value Rendering), Phase 3 (CodeGen Quick Fix)  
**Estimated Effort:** 10-12 hours  
**Priority:** HIGH  
**Dependencies:** None  

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome to the ME2 (Monitoring Extensions 2) project! You will be implementing foundational bug fixes and enhancements to the DDS Monitor and CycloneDDS CodeGen tools. 

> **IMPORTANT ANNOUNCEMENT (AI Coding Agent Note):** 
> As an AI coding agent, you have access to the Playwright MCP server to control the browser natively. **You MUST NOT ask the user for manual UI testing.** Open the browser using your tools, run the web application, interact with the UI, and verify your changes directly. You must finish the whole batch autonomously until all functionality is perfectly working.

**Important Rule:** Finish the batch without stopping. Do not ask for permission to do obvious things like running tests or fixing root causes until everything works. Laziness is not allowed. 

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/guides/DEV-LEAD-GUIDE.md` - How to work with batches
2. **Task Tracker & Details:** `docs/mon-ext-2/ME2-TASK-TRACKER.md` and `docs/mon-ext-2/ME2-TASK-DETAILS.md`
3. **Design Document:** `docs/mon-ext-2/ME2-DESIGN.md` - Technical specifications

### Source Code Location
- **Main Toolset Application:** `tools/DdsMonitor/` (Blazor UI and Engine)
- **Code Generator:** `tools/CycloneDDS.CodeGen/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/ME2-BATCH-01-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/ME2-BATCH-01-QUESTIONS.md`

---

## Context

This batch addresses several critical usability flaws in the DDS Monitor tool and CycloneDDS CodeGen. It establishes a resilient workspace state, fixes data tracking issues (reset handling, list sorting), and drastically improves rendering for complex data types (nulls, unions, dates). It also adds better logging to the MSBuild target for easier MSBuild diagnosis.

**Related Tasks:**
- **ME2-T01** Workspace ComponentTypeName Forward Compatibility
- **ME2-T02** Reset Does Not Lose Subscriptions
- **ME2-T03** Ordinal Sort Broken in All Samples
- **ME2-T04** Timestamp Display Formatting
- **ME2-T05** Null String Visibility + Value Type Syntax Highlighting
- **ME2-T06** Union Rendering Improvements
- **ME2-T07** Schema Compiler Project Name in Build Log

---

## 🎯 Batch Objectives
- **Robust Persistence:** Workspace files survive version upgrades.
- **Continuous Monitoring:** Reset logic now preserves DSS reader subscriptions, smoothly resuming capture.
- **Reliable Ordering:** Sort applies cleanly to the all-topics trace view.
- **Visual Clarity:** Clearer timestamps, correct highlighting for nulls, enums, numbers, and bools. Accurate union visualization.
- **Diagnostics:** Distinct codebase compilation identification through CodeGen log.

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1:** Implement → Write tests → **ALL tests pass** ✅
2. **Task 2:** Implement → Write tests → **ALL tests pass** ✅  
...and so on.

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written (where applicable)
- ✅ **ALL tests passing** (including previous batch tests)

---

## ✅ Tasks

### Task 1: ComponentTypeName Compatibility (ME2-T01)

**Files:** `WindowManager.cs`, `MainLayout.razor`, `Desktop.razor`, `TopicExplorerPanel.razor`, (and wherever `typeof(SomePanel).AssemblyQualifiedName!` is used)
**Task Definition:** See [ME2-TASK-DETAILS.md](../../docs/mon-ext-2/ME2-TASK-DETAILS.md#me2-t01--workspace-componenttypename-forward-compatibility)

**Description:** Panel type names saved to `ddsmon.workspace` must be mapped to `.FullName` rather than `.AssemblyQualifiedName`.
**Requirements:**
- Update `WindowManager.ResolveComponentTypeName` to prefer `FullName`.
- Change all caller sites that register panels to use `typeof(...).FullName!`.
- **Note:** Only alter panel component strings. Do not alter DDS external assembly identity references.
**Design Reference:** `ME2-DESIGN.md` -> Phase 1.1

### Task 2: Reset Does Not Lose Subscriptions (ME2-T02)

**File:** `DdsBridge.cs`
**Task Definition:** See [ME2-TASK-DETAILS.md](../../docs/mon-ext-2/ME2-TASK-DETAILS.md#me2-t02--reset-does-not-lose-subscriptions)

**Description:** The reset command should clear sample lists but MUST NOT dispose active DDS readers.
**Requirements:**
- Remove reader dispose loops in `ResetAll()`.
- Do not call `.Clear()` on `_activeReaders` and `_auxReadersPerParticipant`.
- Do not fire `ReadersChanged`.
- Only reset ordinal counters and empty data stores.
**Design Reference:** `ME2-DESIGN.md` -> Phase 1.2
**Tests Required:**
- ✅ Verify existing `DdsBridge` test suite allows `ResetAll` without invalidating existing subscriber readers.

### Task 3: Ordinal Sort Fixed in All Samples (ME2-T03)

**File:** `SamplesPanel.razor`, `SamplesPanel.razor.cs`
**Task Definition:** See [ME2-TASK-DETAILS.md](../../docs/mon-ext-2/ME2-TASK-DETAILS.md#me2-t03--ordinal-sort-broken-in-all-samples)

**Description:** Sorting by ordinal is visually broken in the 'All Samples' panel.
**Requirements:**
- Extract the sort functionality in `EnsureView()` into an `ApplySortToViewCache()` method.
- Ensure `ApplySortToViewCache()` is executed in BOTH branches of `EnsureView()` (topic-specific and all-topics).
**Design Reference:** `ME2-DESIGN.md` -> Phase 1.3

### Task 4: Timestamp Display Formatting (ME2-T04)

**Files:** `DetailPanel.razor`, `SamplesPanel.razor`, `InstancesPanel.razor`
**Task Definition:** See [ME2-TASK-DETAILS.md](../../docs/mon-ext-2/ME2-TASK-DETAILS.md#me2-t04--timestamp-display-formatting)

**Description:** Use local time representation formatting for timestamps.
**Requirements:**
- Alter DataTime variables to local time via `.ToLocalTime()`.
- Detail pane: `"yyyy-MM-dd HH:mm:ss.fffffff"`. Grid cells: `"HH:mm:ss.fff"`.
- Support converting nanosecond `SourceTimestamp` into correct DateTime string handling out-of-bounds metrics (e.g. `<= 0` or `long.MaxValue` renders "Unknown").
**Design Reference:** `ME2-DESIGN.md` -> Phase 1.4

### Task 5: Null Visibility & Value Highlighting (ME2-T05)

**File:** `DetailPanel.razor`
**Task Definition:** See [ME2-TASK-DETAILS.md](../../docs/mon-ext-2/ME2-TASK-DETAILS.md#me2-t05--null-string-visibility--value-type-syntax-highlighting)

**Description:** Enhance object readability by treating `null` explicitly, and leveraging the existing visual type classes (enum, number, bool).
**Requirements:**
- Intercept `null` and return `<span class="detail-tree__value is-null">null</span>`.
- Dynamically hook into `GetValueClass(value.GetType())` for the CSS base class assignment.
- Format `bool` values as lowercase.
**Design Reference:** `ME2-DESIGN.md` -> Phase 2.1

### Task 6: Union Rendering Improvements (ME2-T06)

**File:** `DetailPanel.razor`
**Task Definition:** See [ME2-TASK-DETAILS.md](../../docs/mon-ext-2/ME2-TASK-DETAILS.md#me2-t06--union-rendering-improvements)

**Description:** Extend the data tables to visualize union schemas comprehensively.
**Requirements:**
- Extract `GetUnionInfo(object unionObj)` helper using reflection for `[DdsDiscriminatorAttribute]` and `[DdsCaseAttribute]`.
- Enforce union discriminator active arm toggling for `RenderTableView` and inside struct/list expansions.
- Tree tab rendering should appropriately define displaying value as the discriminator.
**Design Reference:** `ME2-DESIGN.md` -> Phase 2.2

### Task 7: Schema Compiler Logging Context (ME2-T07)

**File:** `tools/CycloneDDS.CodeGen/CycloneDDS.targets`
**Task Definition:** See [ME2-TASK-DETAILS.md](../../docs/mon-ext-2/ME2-TASK-DETAILS.md#me2-t07--schema-compiler-project-name-in-build-log)

**Description:** Inject project context to make build logs coherent.
**Requirements:**
- Alter XML `Message` to include `$(MSBuildProjectName)` variable.
**Design Reference:** `ME2-DESIGN.md` -> Phase 3.1

---

## 🧪 Testing Requirements
- Every .NET project containing adjusted backend features must have its preexisting tests executed locally to prove stability. Provide test counts in your report.
- UI features: Use the supplied web agent capacities (Playwright MCP server) to visualize and self-test. Assert the panel type restoration properly works by refreshing the page and that no console warnings block UI elements.

**❗ TEST QUALITY EXPECTATIONS**
- **NOT ACCEPTABLE:** Tests that only verify syntax, or UI asserting unrendered markup. Tests that skip edge case definitions (e.g. not verifying the source-timestamp out-of-bounds rendering).
- **REQUIRED:** Tests that verify actual behavior and logic correctness.

---

## 📊 Report Requirements

**Focus on Developer Insights, Not Understanding Checks**

Please capture your valuable insights and experience from implementing these tasks. Address the following:

**Q1:** What issues did you encounter during implementation? How did you resolve them?  
**Q2:** Did you spot any weak points in the existing codebase? What would you improve?  
**Q3:** What design decisions did you make beyond the instructions? What alternatives did you consider?  
**Q4:** What edge cases did you discover that weren't mentioned in the spec?  
**Q5:** Are there any performance concerns or optimization opportunities you noticed (especially rendering)?

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] Task ME2-T01 completed and workspace panel state accurately reinstates.
- [ ] Task ME2-T02 completed and listeners handle Reset operations persistently.
- [ ] Task ME2-T03 completed and ordinal sorting in the All Samples cache properly reflects the correct arrow ordering behavior dynamically.
- [ ] Task ME2-T04 completed with correct formatting conversions applied.
- [ ] Task ME2-T05 completed and type rendering is visually distinct in both tables and trees.
- [ ] Task ME2-T06 completed and union attributes securely bind the table rendering pipeline.
- [ ] Task ME2-T07 completed with project resolution in target logging.
- [ ] Successful interactive browser-based check using tools confirms end-to-end functionality.
- [ ] All code compiles with zero new warnings.
- [ ] All pre-existing unit tests pass smoothly (plus any new testing scaffolding added).
- [ ] Report submitted to `.dev-workstream/reports/ME2-BATCH-01-REPORT.md`.

---

## ⚠️ Common Pitfalls to Avoid
- **Unintended Global Namespace:** Double check `typeof().FullName` outputs, making sure anonymous or dynamically produced instances don't break persistence.
- **Accidental Closure Capture:** Avoid LINQ or excessive memory polling inside rapid `EnsureView` loops on the UI rendering thread. 
- **Timezone Drift:** Do not manually subtract ticks in timestamps, trust the `.ToLocalTime()` UTC mechanisms as detailed in the design.
- **Null Reference Exceptions:** The `DdsBridge` state changes should be exceptionally resilient during partial loading states to avoid uninitialized dictionaries on reset.
- **Asking for User testing:** Do it yourself using Playwright.

---

## 📚 Reference Materials
- **Task Tracker:** `docs/mon-ext-2/ME2-TASK-TRACKER.md`
- **Design:** `docs/mon-ext-2/ME2-DESIGN.md` 
