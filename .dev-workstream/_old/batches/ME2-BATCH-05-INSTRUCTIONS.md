# ME2-BATCH-05: Dynamic Form Array & Union Struct Fixes

**Batch Number:** ME2-BATCH-05  
**Tasks:** ME2-T23, ME2-T24  
**Phase:** Phase 11 (Send Sample Dynamic Form Array & Union Struct Fixes)
**Estimated Effort:** 4-6 hours  
**Priority:** CRITICAL (Fixing broken dynamic UI workflows)
**Dependencies:** ME2-BATCH-04  

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome back! A well-intentioned proactive logic addition from BATCH-04 introduced a pair of rendering and logic traps for deeply nested union arrays. You're tackling exact UI logic fixes reported verbatim by QA to resolve structural expanders and Invalid Cast failures blocking `+Add` functions.

> **IMPORTANT ANNOUNCEMENT (AI Coding Agent Note):** 
> As an AI coding agent, you have access to the Playwright MCP server to control the browser natively. **You MUST NOT ask the user for manual UI testing.** Open the browser using your tools, run the web application, interact with the UI, and verify your changes directly. You must finish the whole batch autonomously until all functionality is perfectly working.

**Important Rule:** Finish the batch without stopping. Do not ask for permission to do obvious things like running tests or fixing root causes until everything works. Laziness is not allowed. 

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/guides/DEV-LEAD-GUIDE.md`
2. **Task Tracker:** `docs/mon-ext-2/ME2-TASK-TRACKER.md` (See Phase 11 Phase goals).

### Source Code Location
- **Main Toolset Application:** `tools/DdsMonitor/` (Blazor UI component `DynamicForm.razor`)

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/ME2-BATCH-05-REPORT.md`

---

## 🎯 Batch Objectives
- **Secure Array List Allocation:** `System.Single[]` arrays are natively failing casts against `List<System.Single>` during standard dynamic add events inside Blazor views. You must securely identify underlying metadata types to synthesize exact sequence mappings. 
- **Expand Active Union Structures:** Recursively nested structs acting as Union values mask their hierarchical properties behind plain strings unless specifically expanded. Fix the UI logic failing to process struct nodes as active arms nested inside lists.

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

### Task 1: Union List Item Structure Expansion Fix (ME2-T23)
**Files:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/DynamicForm.razor`
**Requirements:**
**Problem:** "In the 'send sample' panel when editing the union list item and selecting union arm whose data is a structure (Vec3), the structure is NOT expaned to show its fields, instead the structure is shown just a its full data type name (with namespace). So it is not possible to edit such a structure."
**Fix Details:**
- Track down where complex structs that act as union arms inside array loops fallback to `.ToString()` format rendering without showing expanders.
- Ensure that the nested DynamicForm recursive `<div class="dynamic-form__complex-body">` correctly attaches its context schema and processes active nested struct arm instantiations exactly like standard structured fields.

### Task 2: `AddArrayElement` InvalidCastException Fix (ME2-T24)
**Files:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/DynamicForm.razor`
**Requirements:**
**Problem:** "When I select SetTestPose topic, the list field 'Sample' shows +Add button but it does not do anything... Unable to cast object of type 'System.Single[]' to type 'System.Collections.Generic.List`1[System.Single]' at ... DynamicForm.AddArrayElement"
**Fix Details:**
- Track how `raw is IList list && !list.IsFixedSize` branching in `AddArrayElement` maps arrays compared to properly declared `.ValueType`.
- Ensure new items appended to existing containers respect the target field setter's expected Type (e.g. constructing `List<T>` instead of `Array.CreateInstance` if the `FieldMetadata` setter expects `IList<T>` or `List<T>`). CycloneDDS translates IDL sequences into `T[]` or `List<T>` dynamically depending on generator parameters; `DynamicForm` must flexibly adapt instead of forcefully returning array cast violations.

---

## 🧪 Testing Requirements
- The browser MCP should interact with a complex structured Union element.
- Assert nested structs expand accurately visually without namespace string obfuscations.
- `+Add` operation must function against sequences strictly declared as generic lists natively triggering no runtime exceptions.

## 📊 Report Requirements

Provide professional developer insights matching Q1-Q5 guidelines outlining the casting boundaries bypassed inside `T[]` mappings and structure layout recursion paths. 

---

## 🎯 Success Criteria
- [ ] Task ME2-T23 displays expanded structural fields mapped seamlessly inside complex struct loops. 
- [ ] Task ME2-T24 accurately assigns `IList` object representations avoiding any cast crashes into metadata sets.
- [ ] Playwright web driver succeeds clicking `+Add` button without crash.
- [ ] 100% test coverage passed clean.
