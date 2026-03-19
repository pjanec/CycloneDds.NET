# ME2-BATCH-02: Advanced Filtering and Dynamic Payload Control

**Batch Number:** ME2-BATCH-02  
**Tasks:** ME2-T08, ME2-T09, ME2-T10, ME2-T11, ME2-T15  
**Phase:** Phase 4 (Filter & Column System), Phase 5 (Samples Panel Track Mode), Phase 8 (Send Sample Form Control)  
**Estimated Effort:** 10-12 hours  
**Priority:** HIGH  
**Dependencies:** ME2-BATCH-01  

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome back to ME2! In this batch, we dive into powerful data manipulation frameworks. You will empower users to filter non-payload metadata fields, introduce tracking UI modes, and allow literal null values to be omitted inside dynamic form payloads.

> **IMPORTANT ANNOUNCEMENT (AI Coding Agent Note):** 
> As an AI coding agent, you have access to the Playwright MCP server to control the browser natively. **You MUST NOT ask the user for manual UI testing.** Open the browser using your tools, run the web application, interact with the UI, and verify your changes directly. You must finish the whole batch autonomously until all functionality is perfectly working.

**Important Rule:** Finish the batch without stopping. Do not ask for permission to do obvious things like running tests or fixing root causes until everything works. Laziness is not allowed. 

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/guides/DEV-LEAD-GUIDE.md`
2. **Task Tracker & Details:** `docs/mon-ext-2/ME2-TASK-TRACKER.md` and `docs/mon-ext-2/ME2-TASK-DETAILS.md`
3. **Design Document:** `docs/mon-ext-2/ME2-DESIGN.md` -> Phase 4, Phase 5.
4. **ME2-BATCH-01 Review:** `.dev-workstream/reviews/ME2-BATCH-01-REVIEW.md` - Please read technical debts concerning LINQ/Reflection caching to keep them in mind if touching similar patterns.

### Source Code Location
- **Main Toolset Application:** `tools/DdsMonitor/` (Blazor UI and Engine)

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/ME2-BATCH-02-REPORT.md`

---

## 🎯 Batch Objectives
- **Data Filtering Expansion:** Allow non-payload metadata properties like `Topic` and `InstanceState` to be dynamically filtered and queried natively without compiler exceptions.
- **Performant Tracking:** Auto-scroll "track mode" logic safely handling descending/ascending streaming telemetry.
- **Dynamic Field Nullability (T15):** Expose UI controls enabling the explicit setting of `null` over dynamically built DDS payload strings and collections.

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

### Task 1: Expose Non-Payload Fields (ME2-T08)
**Files:** `TopicMetadata.cs`, `FilterCompiler.cs`, `FilterBuilderPanel.razor`, `FieldPicker.razor`
**Requirements:**
- Add `Topic` and `InstanceState` struct wrappers to `TopicMetadata.AppendSyntheticFields`.
- Adjust the Regex parser to detect `Sample.xxx` properties.
- Fix UI prefixing conventions dependent on whether `IsWrapperField` resolves to `true` ("Sample.") or `false` ("Payload.").

### Task 2: "Filter Out Topic" Context Menu (ME2-T09)
**Files:** `SamplesPanel.razor`, `InstancesPanel.razor`
**Requirements:**
- Context menu injects: `AND Sample.Topic != "TopicName"` onto the existing evaluator filter.
- Implement the extraction helper `ExcludeTopicFromFilter(string topicName)`.

### Task 3: Decouple Hardcoded Columns (ME2-T10)
**Files:** `SamplesPanel.razor`
**Requirements:**
- The standard user columns (Timestamp, Size, Delay, Topic) must be user selectable instead of hardcoded into `RebuildLayoutColumns`.
- Maintain hardcoded defaults only for: `Ordinal`, `Status`, `Act`.
- The `ColumnKind` enum compresses to 4 variants (`Ordinal`, `Status`, `Field`, `Actions`).

### Task 4: Samples Panel Track Mode (ME2-T11)
**Files:** `SamplesPanel.razor`
**Requirements:**
- Detect `_trackMode`.
- During active telemetry inserts (ascending ordinal), bypass full expensive array sorts `O(N log N)`. Calculate delta offsets intelligently using Reverse logic when required.
- Force `EnsureSelectionVisibleAsync` upon selection and streaming deltas.

### Task 5: Nullable/Optional Field Support in Send Sample Panel (ME2-T15)
**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/DynamicForm.razor`
**Requirements:**
- Allow intentionally sending explicit `null` data structures (e.g. `string`, `int?`, `T[]`) inside the payload injector logic.
- Locate the `<div class="dynamic-form__row">` render loop.
- **C# Helper Code:**
  - Create `CanBeNull` logic determining if `ValueType` evaluates natively as nullable. 
  - Create `ToggleNull(FieldMetadata field, object? isCheckedObj)` to safely handle safe empty defaults (`string.Empty` or `Array.CreateInstance`) and explicit null injections `field.Setter(Payload, null)`.
- **UI Logic:** 
  - Render an `<input type="checkbox">` visually positioned beside the form label logic whenever `canBeNull`.
  - Disable the standard editor when forced `null` happens via the checkbox (`style="opacity: 0.4; pointer-events: none;"`).

---

## 🧪 Testing Requirements
- Backend parsing tests must execute safely avoiding compiler/Regex breaks.
- Use Playwright MCP to visualize null strings getting passed through the UI payload structure. Ensure checkboxes gray out accurately without DOM exceptions.

## 📊 Report Requirements

Provide professional developer insights matching Q1-Q5 rules from the guide, including details over any performance regressions discovered while testing `_trackMode` arrays over large sets of memory boundaries.

---

## 🎯 Success Criteria
- [ ] Task ME2-T08, T09, T10 implementation complete.
- [ ] Task ME2-T11 sorting logic effectively manages ascending caches.
- [ ] Task ME2-T15 Checkbox toggle appropriately hides/nullifies dynamic inputs on the `SendSamplePanel`.
- [ ] All 360 unit tests pass correctly.
- [ ] Report submitted.
