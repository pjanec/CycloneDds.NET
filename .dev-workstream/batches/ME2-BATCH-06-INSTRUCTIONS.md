# ME2-BATCH-06: Topic Properties & Extensibility Debt

**Batch Number:** ME2-BATCH-06  
**Tasks:** ME2-T25-A (Tech), ME2-T25-B (Tech), ME2-T12, ME2-T13-A, ME2-T13-B, ME2-T26  
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
- **Global Theme & Hashing:** Inject UI CSS configurations binding uniquely hashed (deterministic) or custom selected colors against topic grids globally.

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

### Task 5: Global Colorized Topic Names (ME2-T26)
pls extend the batch by the following task: colorized topic names
if using all samples panel there are many different topic samples mixed. Assigning a unique color to each of the topic names will greatly improve the user's orientation. The colors must be selected with respect to dark or light theme to stay readable. same color should be used by the Topics panel for the topic name. In the topic properties panel we should have a color selector so we can re-color the topic (user color) and button for resetting to default ('auto'). Colors should be selected automatically until the topic color was selected in the topic properties panel - those topic colors should be saved to app settings.
 
Based on the current architecture of the application, here is how your feature could be integrated:

**1. Theming and Color Selection**
The application currently manages light and dark modes using a `[data-theme="dark"]` HTML attribute toggled via JavaScript in `app.js` and CSS variables defined in `app.css`. 
To ensure colors remain readable in both modes, you could define a palette of CSS variables (e.g., `--topic-color-1`, `--topic-color-2`) in both the `:root` (light) and `[data-theme="dark"]` selectors. The "auto" color assignment could be implemented by hashing the topic's `ShortName` to deterministically pick an index from this CSS palette.

**2. Applying Colors to the Panels**
*   **All Samples Panel:** The global "All Samples" panel is initialized as a standard `SamplesPanel` with the ID `"SamplesPanel.0"` and no specific topic type. The topic name is exposed to the grid as a synthetic wrapper field named `"Topic"`. You would update the `RenderCellValue` method in `SamplesPanel.razor` to inject the assigned color style when rendering the `"Topic"` column.
*   **Topics Panel:** In `TopicExplorerPanel.razor`, the topic name is currently rendered inside a `<div class="topic-explorer__name">@topic.Metadata.ShortName</div>` element. This class could be updated to apply the assigned topic color inline.

**3. Managing and Saving User Colors**
To support custom user colors and an "auto" reset button, a new UI component (like the "Topic properties panel" you suggested) would need to be created, as the current codebase primarily uses `DetailPanel` for sample-specific data and `TopicSourcesPanel` for DLL assembly management. 
Because these topic colors need to be globally consistent across all panels, the color mappings should be saved globally rather than in a specific panel's local state. You could persist this dictionary by extending the `DdsSettings` class, which is already bound to `appsettings.json`, or by adding it to the `WorkspaceState` manager so it saves alongside the user's desktop layout.

**4. Other Places to Apply the Coloring**
In addition to the Topics and All Samples panels, this coloring could be applied to:
*   **Send Sample Panel:** The dropdown combo box where users search and select a topic (`<span class="send-sample-panel__combo-option-name">`).
*   **Detail Panel:** The window title bar which displays `Detail [TopicName]`.
*   **Replay Panel:** The "Next Sample" preview block, which currently renders the topic name using `<span class="replay-panel__next-type">@pending.TopicMetadata.ShortName</span>`.
 

**Files:** `SamplesPanel.razor`, `TopicExplorerPanel.razor`, `SendSamplePanel.razor`, `DetailPanel.razor` (Window Title), `ReplayPanel.razor` (Next Sample), `app.css`, `DdsSettings.cs` (or `WorkspaceState`).
**Requirements:**
- **Theming and Colors:** Define CSS palette variables (e.g., `--topic-color-1`, `--topic-color-2`...) directly onto `:root` and `[data-theme="dark"]` to keep colors readable globally. Construct a deterministic hash targeting topic `ShortName` strings to auto-assign colors without overlap.
- **Applying Colors to the Panels:**
  - Override `<div class="topic-explorer__name">` elements mapping dynamically bound `style` inline CSS using the matching palette color. 
  - On the All Samples Grid (`SamplesPanel.0`), locate `RenderCellValue` where wrapper `"Topic"` fields route, and enforce the same color inline.
  - Push coloration matching into `SendSamplePanel` dropdown choices, `DetailPanel` header tags (`Detail [TopicName]`), and the Replay string identifier `<span class="replay-panel__next-type">`.
- **Saving Overrides:** Track explicit user-defined color overrides natively within a mapping element (either updating `DdsSettings` globally persisting in appsettings or the static Workspace). 
- **Topic Properties Element:** Use the new Topic Properties Panel window to embed a user-facing visual Color Selector matching standard CSS, with an `Auto` fallback enabling your deterministic hash natively.


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
- [ ] Task ME2-T26 deterministic and explicit Theme coloration persists through application resets securely against Topic metadata labels.
- [ ] Playwright tests accurately test user behaviors globally.
- [ ] 100% test coverage passed clean.
