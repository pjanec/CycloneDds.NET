# ME2-BATCH-06: Topic Properties & Extensibility Debt

**Batch Number:** ME2-BATCH-06  
**Tasks:** ME2-T25-A (Tech), ME2-T25-B (Tech), ME2-T12, ME2-T13-A, ME2-T13-B  
**Phase:** Phase 6 (Topic Properties) and Phase 12 (Extensibility)
**Estimated Effort:** 10-12 hours  
**Priority:** HIGH  
**Dependencies:** ME2-BATCH-05  

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome back! In BATCH-06, we return our focus explicitly to new product feature requests, starting with the Topic Properties panels. However, as is the rule, you MUST first clear out the technical debt logged historically from BATCH-01.

> **IMPORTANT ANNOUNCEMENT (AI Coding Agent Note):** 
> As an AI coding agent, you have access to the Playwright MCP server to control the browser natively. **You MUST NOT ask the user for manual UI testing.** Open the browser using your tools, run the web application, interact with the UI, and verify your changes directly. You must finish the whole batch autonomously until all functionality is perfectly working.

**Important Rule:** Finish the batch without stopping. Do not ask for permission to do obvious things like running tests or fixing root causes until everything works. Laziness is not allowed. 

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/guides/DEV-LEAD-GUIDE.md`
2. **Task Tracker & Details:** `docs/mon-ext-2/ME2-TASK-TRACKER.md` & `docs/mon-ext-2/ME2-TASK-DETAILS.md`
3. **ME2-BATCH-01 Report:** `.dev-workstream/reports/ME2-BATCH-01-REPORT.md` - Please read the exact technical debt reports corresponding to `MON-DEBT-015` and `MON-DEBT-016` (Workspace AQNs and Desktop.razor plugins).

### Source Code Location
- **Main Toolset Application:** `tools/DdsMonitor/` (Blazor UI and Engine)

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/ME2-BATCH-06-REPORT.md`

---

## 🎯 Batch Objectives
- **Clear Workspace Vulnerabilities:** Address `Type.GetType`'s executing-assembly limitations which block decoupled plugin expansions, and correct `GetPanelBaseName` ID bloats.
- **Implement Topic Properties Panel:** Construct a non-modal detail widget allowing topic analysis mapped seamlessly to contextual right-click directives globally.

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

### Task 1: (Tech Debt) `GetPanelBaseName` Name Sanitization Fix (ME2-T25-A)
**Files:** `tools/DdsMonitor/DdsMonitor.Engine/WindowManager.cs`
**Requirements:**
- Fully sanitize long AQNs generated previously, making generated IDs explicitly concise matching conventional bounds. Truncate strictly safely. 

### Task 2: (Tech Debt) `Type.GetType` Extensibility Fix (ME2-T25-B)
**Files:** `Desktop.razor` (and components handling type construction loops)
**Requirements:**
- Reconfigure UI panel type resolution to loop safely over domain assembly loaders natively, allowing explicitly tracked exterior assemblies to act as valid panel providers. Do not rely exclusively on the strictly compiling context. 

### Task 3: Build Topic Properties Component (ME2-T12)
**Files:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicPropertiesPanel.razor`, `WindowManager` registration
**Requirements:**
- Implement the baseline component following your design guidelines (`ME2-TASK-DETAILS.md#me2-t12--new-topicpropertiespanel-component`).
- It must be inherently non-modal, exposing `TopicMetadata.KeyFields` mappings gracefully.
- Follow existing patterns for Workspace persistence via ID keys. 

### Task 4: Connect Contextual Handlers (ME2-T13-A & ME2-T13-B)
**Files:** `TopicExplorerPanel.razor`, `TopicSourcesPanel.razor`
**Requirements:**
- Embed `Topic Properties` explicitly into the Topic Explorer right-click layout mapping dynamically to OpenPanel.
- Enhance the Target Topic Sources interactions similarly. Guarantee standard UI UX rules apply natively. 

---

## 🧪 Testing Requirements
- Confirm that right-clicking elements populates the expected TopicProperty views cleanly without replacing distinct previous states.
- Assembly load testing must confirm your `Type.GetType` improvements don't arbitrarily drop current layouts unexpectedly.  

## 📊 Report Requirements

Provide professional developer insights matching Q1-Q5 guidelines outlining exactly how `Desktop.razor` was refactored and exactly how IDL types render securely natively across your new Topic parameter widget. 

---

## 🎯 Success Criteria
- [ ] Task ME2-T25-A Panel ID lengths reliably truncating efficiently.
- [ ] Task ME2-T25-B External plugin loading bounds securely lifted.
- [ ] Task ME2-T12 New property UI created and functioning visually.
- [ ] Task ME2-T13-A Explorer Context Menu accurately opening Properties tabs.
- [ ] Task ME2-T13-B Source panel mapped dynamically alongside logic handlers.
- [ ] Playwright tests accurately test user behaviors globally.
- [ ] 100% test coverage passed clean.
