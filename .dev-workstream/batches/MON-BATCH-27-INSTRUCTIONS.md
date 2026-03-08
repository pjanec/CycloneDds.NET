# BATCH-27: Dynamic Topic Assembly Loading

**Batch Number:** MON-BATCH-27  
**Tasks:** Deactivate self-sending, Dynamic DLL inspection, Topic Sources UI, Configuration Persistence  
**Phase:** Phase 7+ (Real World Detour)  
**Estimated Effort:** 4-6 hours  
**Priority:** CRITICAL  
**Dependencies:** MON-BATCH-26

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome back! Because the tool is about to be used on *real networks*, deploying it with hardcoded test-topics and mocking systems active is unacceptable. We need a fundamental ability to load external DLLs and extract DDS models dynamically from them.

Phase 5 (Plugin Architecture) will continue to wait. We need you to build a robust "**Assembly Scanner**" that persists what external DDS assemblies the user wants to monitor.

### Source Code Location
- **Engine Logic Area:** `tools/DdsMonitor.Engine/Modelling/` or `tools/DdsMonitor.Engine/AssemblyScanner.cs`
- **UI Area:** `tools/DdsMonitor/Components/` and Main Layout Menus.

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-27-REPORT.md`

---

## 🎯 Batch Objectives

### Task 1: Silence Self-Sending (Mocking)
- **Problem:** Currently, DDS monitor sends its own fake test packets into the domain (e.g. `SelfSendTopics.cs` or similar mocking loops).
- **Fix:** Disable this entirely by default. 
- **UI Addition:** Add a `Devel` dropdown menu horizontally on the main screen (alongside `File`, `View`, etc.). Put a toggle in there called "Enable Self-Sending". Only dispatch mock samples when this is checked.

### Task 2: Persistence & Auto-Scan Configuration
- **Objective:** DdsMonitor must remember an arbitrary list of external DLL paths between launches.
- **Fix:** Read and write this string list from a physical configuration file (e.g. the application setting config in the user's workspace/appdata folder).
- **Auto-scan:** When the backend engine boots up, immediately attempt to load each of the configured DLL assemblies and recursively reflect out all valid struct configurations (`DdsTopic` or structs with public static `GetDescriptorOps`). Integrate these models into the engine's known Topic catalogue.

### Task 3: "Topic Sources" Editor Panel
- **New UI Component:** Build a `TopicSourcesPanel.razor` that opens from the `File` menu.
- **Top Section (Assemblies):**
  - Render an interactive, selectable grid/list of the DLLs loaded.
  - Expose "Add", "Remove", and "Move Up/Down" buttons (reordering).
  - The "Add" button must spawn a `FileDialog` bridging the local OS browser (filtering `*.dll`).
  - Next to each assembly listing, print out **how many topics** were successfully extracted from it.
- **Bottom Section (Models):**
  - Provide a detail grid displaying the list of all Topic Types/Models discovered strictly within the assembly selected in the top half.

### Task 4: Topics Panel & Missing Warnings Integration
- **Topics Panel Enhancements:** Right next to the `Subscribe All` button in the `TopicsPanel`, print out the total number of known topics, and append a settings/gear icon. Clicking this icon must immediately spawn the new `TopicSourcesPanel`.
- **Zero Topics Warning:** If the application has `0` payload structures loaded in its global Topic manifest, clearly display a warning layer giving instructions on how to add topics (e.g. "No topic models have been imported! Open File -> Topic Sources to link CycloneDDS Dll assemblies.")

---

## 📊 Report Requirements

**✅ What to Answer in Your Report (`MON-BATCH-27-REPORT.md`):**

1. Explain the mechanism and Context (`AssemblyLoadContext` vs `Assembly.LoadFrom`) used to pull in these foreign `.dll` files safely.
2. Outline how the persistent config state translates into the backend manifest.

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] Fake network sending is dormant by default and tucked behind a `Devel` debug menu.
- [ ] User can browse and attach arbitrary `.dll` files via `FileDialog`.
- [ ] System automatically persists those paths and resolves them upon restart.
- [ ] `TopicSourcesPanel` presents a master-detail split (Assemblies -> Topics).
- [ ] `TopicsPanel` exhibits the total topic count and quick-link to the Sources Panel.
- [ ] A stark user-warning is cast if the registry has zero configurations loaded.
