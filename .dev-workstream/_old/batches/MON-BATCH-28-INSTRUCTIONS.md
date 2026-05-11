# BATCH-28: Plugin Infrastructure

**Batch Number:** MON-BATCH-28  
**Tasks:** DMON-041, DMON-042, DMON-043  
**Phase:** Phase 5  
**Estimated Effort:** 8 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-27

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome to Phase 5: Plugin Architecture! The goal of this batch is to implement the foundational infrastructure for loading external Razor Class Library plugins into the DDS Monitor. 

**IMPORTANT NOTE:** We are *only* building the infrastructure. Do NOT implement any concrete plugins (like BDC or TKB) in this batch. The BDC plugin will use a project-specific data model located outside this repository, so our focus here is strictly on the interfaces and the host's loading mechanism. Also note that `DMON-044` (Custom Value Formatters) has already been solved via attributes on DDS data structures, so you do not need to implement `IFormatterRegistry`.

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/DEV-GUIDE.md` - How to work with batches
2. **Task Definitions:** `docs\ddsmon\TASK-DETAIL.md ` - See DMON-041, DMON-042, DMON-043 details
3. **Design Document:** `docs\ddsmon\DESIGN.md` - Section 12 (Plugin Architecture)
4. **Previous Review:** `.dev-workstream/reviews/MON-BATCH-27-REVIEW.md` - Note how `AssemblyLoadContext` was used recently for `TopicDiscoveryService`.

### Source Code Location
- **Primary Work Area:** `tools/DdsMonitor/` and `tools/DdsMonitor.Engine/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-28-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-28-QUESTIONS.md`

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

---

## Context

The DDS Monitor must support extending its capabilities without compiling domain-specific logic into the core application. We will use a plugin architecture where external DLLs can provide new UI panels, menu items, and interaction capabilities. In this batch, you will build the abstractions (`IMonitorPlugin`, `IMonitorContext`, `IMenuRegistry`) and the host loading logic.

**Related Tasks:**
- [DMON-041](../TASK-DETAIL.md#dmon-041--plugin-loading-infrastructure) - Plugin loading infrastructure
- [DMON-042](../TASK-DETAIL.md#dmon-042--plugin-panel-registration) - Plugin panel registration
- [DMON-043](../TASK-DETAIL.md#dmon-043--plugin-menu-registration) - Plugin menu registration

---

## 🎯 Batch Objectives

### Task 1: Plugin Contract & Loading Infrastructure (DMON-041)

**Task Definition:** See [DMON-041](../TASK-DETAIL.md#dmon-041--plugin-loading-infrastructure)

**Description:** Define the core plugin abstractions and the host service to load them.
**Requirements:**
1. Define `IMonitorPlugin` and `IMonitorContext` interfaces in a shared/core project accessible to plugins.
2. Implement a `PluginLoader` that scans the configured `PluginDirectories` (from `appsettings.json`) for DLLs.
3. Similar to how `TopicDiscoveryService` isolates types in `MON-BATCH-27`, use a collectible `AssemblyLoadContext` to load each plugin. Make sure to share the global host `CycloneDDS.Runtime` context correctly to avoid Type mismatch exceptions.
4. Discover and instantiate types implementing `IMonitorPlugin`.
5. Provide a way for the host to call `ConfigureServices(IServiceCollection)` before the container is built, and `Initialize(IMonitorContext)` after it runs.
6. Handle bad DLLs gracefully without crashing the host.

**Tests Required:**
- ✅ Unit tests mocking `PluginLoader` directory scanning, ensuring valid plugins are loaded and bad DLLs are skipped.
- ✅ Unit test verifying `ConfigureServices` and `Initialize` are properly invoked on the plugin instances.


### Task 2: Plugin Panel Registration (DMON-042)

**Task Definition:** See [DMON-042](../TASK-DETAIL.md#dmon-042--plugin-panel-registration)

**Description:** Provide the mechanism for plugins to register their own UI panels.
**Requirements:**
1. Expand the existing `IWindowManager` interface so plugins can call `RegisterPanelType(string name, Type blazorComponentType)`.
2. Add a dynamic "Plugin Panels" menu in the desktop shell (top bar) that lists these registered panel types.
3. Spawning these panels should work identical to built-in panels, routing through `IWindowManager` and resolving the component via DI if necessary.

**Tests Required:**
- ✅ Unit test validating `RegisterPanelType` adds to the available registry.
- ✅ Unit test validating `SpawnRegisteredPlugin` correctly creates a new panel state instance.


### Task 3: Plugin Menu Registration (DMON-043)

**Task Definition:** See [DMON-043](../TASK-DETAIL.md#dmon-043--plugin-menu-registration)

**Description:** Allow plugins to inject custom actions into the application's top menu bar.
**Requirements:**
1. Create an `IMenuRegistry` interface:
   ```csharp
   public interface IMenuRegistry
   {
       void AddMenuItem(string menuPath, string label, Action onClick);
       void AddMenuItem(string menuPath, string label, Func<Task> onClickAsync);
   }
   ```
2. Implement the registry and expose it through `IMonitorContext`.
3. Support nested menu structures via `menuPath` (e.g., `"Plugins/BDC/Show Entities"`).
4. Update the Blazor shell's top navigation to dynamically render these menu nodes.

**Tests Required:**
- ✅ Unit test ensuring adding items creates the correct hierarchical structure.
- ✅ Unit test ensuring the callbacks are stored and invoked properly.

---

## 🧪 Testing Requirements
- **Quality over Quantity:** Tests must verify actual runtime behaviors, state mutations, and error handling, rather than shallow "object exists" checks.
- You must create a minimum of 6 robust unit/integration tests covering the various plugin life-cycle hooks, assembly loading isolation paths, and registry mutation logic.
- Ensure that testing the `AssemblyLoadContext` explicitly verifies isolation and type equivalence for shared framework types.

---

## 📊 Report Requirements

**Focus on Developer Insights, Not Understanding Checks**

**✅ What to Ask:**
- **Issues Encountered:** Did you encounter any assembly resolution or `Type` loading issues while testing `AssemblyLoadContext`? How did you solve them?
- **Design Decisions Made:** How did you handle the integration of the dynamic menu tree rendering in the top bar UI?
- **Edge Cases Discovered:** Did you find any edge cases regarding plugin initialization ordering or lifecycle?

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] Plugin Contract (`IMonitorPlugin`, `IMonitorContext`) is defined.
- [ ] `PluginLoader` correctly loads external assemblies via isolated contexts without crashing on invalid DLLs.
- [ ] Custom panels can be registered by plugins and launched by users.
- [ ] Custom menu items can be injected into the main menu hierarchy and clicked.
- [ ] All tests passing.
- [ ] The `MON-BATCH-28-REPORT.md` is submitted with full insight answers.

---

## ⚠️ Common Pitfalls to Avoid
- **Type Mismatches:** Loading plugins in a separate AssemblyLoadContext can cause identical types (like `IMonitorPlugin` or types belonging to CycloneDDS) to resolve as "different types" if the shared DLLs aren't properly delegated to the host ALC. 
- **Premature Concretization:** Remember, do NOT implement the BDC or TKB plugins. Only the infrastructure.

---

## 📚 Reference Materials
- **Task Defs:** [TASK-DETAIL.md](../docs/ddsmon/TASK-DETAIL.md) - DMON-041, DMON-042, DMON-043
- **Design:** `docs/ddsmon/DESIGN.md` - Section 12
