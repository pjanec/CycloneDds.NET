# BATCH-17: Theme, Persistence & Visual Filter Builder

**Batch Number:** MON-BATCH-17  
**Tasks:** DMON-027 (Dark/light theme toggle), DMON-028 (Workspace persistence), DMON-029 (Visual Filter Builder)  
**Phase:** Phase 2 (Blazor Shell & Core UI) & Phase 3 (Advanced UI Features)  
**Estimated Effort:** 7-9 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-16

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome to BATCH-17! This batch finalizes Phase 2 by completing the core user experience features of the workspace (Dark/Light mode, Workspace layout saving), and begins Phase 3 by adding the Visual Filter Builder. Your goal is to work through these feature tasks sequentially.

**Work Continuously:** Finish the batch without stopping and asking if it is ok to do obvious things like running the tests and fixing the root cause until all ok. No laziness allowed. You should push through until everything is functioning flawlessly and then write your report. No useless asking for permission allowed.

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/guides/DEV-GUIDE.md` - How to work with batches
2. **Task Definitions:** `docs/ddsmon/TASK-DETAIL.md ` - See DMON-027, DMON-028, and DMON-029 details
3. **Design Document:** `docs/ddsmon/DESIGN.md` - Technical specifications
4. **Previous Review:** `.dev-workstream/reviews/MON-BATCH-16-REVIEW.md` - Learn from feedback

### Source Code Location
- **Primary Work Area:** `tools/DdsMonitor/` and `tools/DdsMonitor.Engine/`
- **Test Project:** `tests/DdsMonitor.Engine.Tests/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-17-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-17-QUESTIONS.md`

---

## Context

This batch completes the desktop shell environment by allowing layout persistance and aesthetic control, making it a viable long-lived application. Following that, it introduces the core power-user feature for data exploration: a graphical AST-based filter builder.

**Related Tasks:**
- [DMON-027](../../docs/ddsmon/TASK-DETAIL.md#dmon-027--darklight-theme-toggle) - Dark/light theme toggle
- [DMON-028](../../docs/ddsmon/TASK-DETAIL.md#dmon-028--workspace-persistence-saveload-layout) - Workspace persistence (save/load layout)
- [DMON-029](../../docs/ddsmon/TASK-DETAIL.md#dmon-029--visual-filter-builder) - Visual Filter Builder

---

## 🎯 Batch Objectives
- Ensure user preferences like dark mode and window placement survive browser reloads.
- Provide a robust tool to visually construct boolean filters based on DDS sample structures.

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1:** Implement → Write tests → **ALL tests pass** ✅
2. **Task 2:** Implement → Write tests → **ALL tests pass** ✅  
3. **Task 3:** Implement → Write tests → **ALL tests pass** ✅

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written
- ✅ **ALL tests passing** (including previous batch tests)

**Why:** Ensures each component is solid before building on top of it. Prevents cascading failures.

---

## ✅ Tasks

### Task 1: Dark/light theme toggle (DMON-027)

**File:** `tools/DdsMonitor/` (NEW CSS / UPDATE BLAZOR COMPONENTS)  
**Task Definition:** See [DMON-027](../../docs/ddsmon/TASK-DETAIL.md#dmon-027--darklight-theme-toggle)

**Description:** Implement CSS-based theming defaulting to dark mode, controllable via a UI toggle.
**Requirements:**
- Define CSS custom properties for colors in `:root` and `[data-theme="dark"]`.
- The UI toggle should toggle the `data-theme` attribute on the `<html>` root.
- Use `localStorage` to persist the preference across reloads.

**Design Reference:** See DESIGN.md §15.6

**Tests Required:**
- ✅ Ensure dark theme is applied out of the box.
- ✅ Clicking toggle acts immediately across components.
- ✅ Validate reloading retrieves state from localStorage correctly.

### Task 2: Workspace persistence (save/load layout) (DMON-028)

**File:** `tools/DdsMonitor/.../WorkspaceState` (NEW FEATURE / UPDATE)  
**Task Definition:** See [DMON-028](../../docs/ddsmon/TASK-DETAIL.md#dmon-028--workspace-persistence-saveload-layout)

**Description:** Persist active panels, sizes, indices, and states. Save automatically and allow user to export/import.
**Requirements:**
- Automatically debounce-save panel positions, dimensions, and inner active states to `workspace.json` (or standard persistence local structure).
- Add UI to manually export and import `.workspace` files if appropriate.
- On browser reload, read layout configuration and perfectly restore the panels.

**Design Reference:** See DESIGN.md §8.5

**Tests Required:**
- ✅ Unit test `WorkspacePersistence_SerializeDeserialize_RoundTrips`: Create a list of `PanelState`, serialize to JSON, deserialize. Assert equality.
- ✅ Execute manual test restoring layout upon browser reload.

### Task 3: Visual Filter Builder (DMON-029)

**File:** `tools/DdsMonitor/` (NEW COMPONENT FilterBuilderPanel.razor)  
**Task Definition:** See [DMON-029](../../docs/ddsmon/TASK-DETAIL.md#dmon-029--visual-filter-builder)

**Description:** Tree-based AST filter designer that outputs valid Dynamic LINQ to pass into our FilterCompiler.
**Requirements:**
- Model AND/OR recursive group logic.
- Node elements containing Property Selector, Operator dropdown, and dynamic Value Editor.
- UI features for appending, omitting conditions, or negating groups.
- `Apply` invokes `.ToDynamicLinqString()` and registers with `FilterCompiler`.

**Design Reference:** See DESIGN.md §10.7

**Tests Required:**
- ✅ Unit test `FilterNode_ToDynamicLinq_SimpleCondition`: `Payload.Id == 42` AST generates `"Payload.Id == 42"`.
- ✅ Unit test `FilterNode_ToDynamicLinq_NestedAndOr`: Correct combination syntax generated.
- ✅ Unit test `FilterNode_ToDynamicLinq_NegatedCondition`: Check negation format (`NOT` or `!`).

---

## ⚠️ Quality Standards

**❗ TEST QUALITY EXPECTATIONS**
- **NOT ACCEPTABLE:** Tests that only verify "can I set this value" or assert "not null".
- **REQUIRED:** Tests that verify actual behavior and logic combinations.
- Visual components tests (if Blazor-level) should evaluate element behaviors, though we primarily test logic models here (the AST to LINQ string compiler).

**❗ REPORT QUALITY EXPECTATIONS**
- **REQUIRED:** Document issues encountered and how you resolved them.
- **REQUIRED:** Document design decisions YOU made beyond the spec.
- **REQUIRED:** Share insights on code quality and improvement opportunities.

---

## 📊 Report Requirements

**Focus on Developer Insights, Not Understanding Checks**

**✅ What to Answer in Your Report (`MON-BATCH-17-REPORT.md`):**

**Q1:** What CSS challenges did theming introduce, and did you restructure any previous styling paths?
**Q2:** Does serialization logic bloat, or are there further runtime optimization opportunities concerning `workspace.json`?
**Q3:** How did you structure the AST model behind the visual builder to ensure neat JSON serialization? Any changes?
**Q4:** What UI/UX improvement opportunities exist to tie the `FieldMetadata` selectors better with the nodes in the visual filter UI?
**Q5:** Did you uncover any edge cases for specific types in the operations dropdown?

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] DMON-027 completed (CSS and localstorage sync confirmed working)
- [ ] DMON-028 completed (Round-trip serialization works)
- [ ] DMON-029 completed (Dynamic LINQ compilation acts to spec)
- [ ] Required Unit test structures pass gracefully
- [ ] `MON-BATCH-17-REPORT.md` submitted addressing required developer insights
