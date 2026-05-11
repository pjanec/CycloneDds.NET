# ME2-BATCH-07: DI Startup Blockers & Folder Assemblies

**Batch Number:** ME2-BATCH-07  
**Tasks:** ME2-T27 (Tech Debt), ME2-T14  
**Phase:** Phase 13 (Startup Tech Debt) and Phase 7 (Folder-Based Assembly Scanning)
**Estimated Effort:** 4-6 hours  
**Priority:** CRITICAL (App Crashing on Startup)
**Dependencies:** ME2-BATCH-06  

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome back! A critical architectural violation was introduced in the previous batch that crashes the Blazor application immediately inside its boot sequence. We must solve this Dependency Injection failure first before touching any other systems.

> **IMPORTANT ANNOUNCEMENT (AI Coding Agent Note):** 
> As an AI coding agent, you have access to the Playwright MCP server to control the browser natively. **You MUST NOT ask the user for manual UI testing.** Open the browser using your tools, run the web application, interact with the UI, and verify your changes directly. You must finish the whole batch autonomously until all functionality is perfectly working.

**Important Rule:** Finish the batch without stopping. Do not ask for permission to do obvious things like running tests or fixing root causes until everything works. Laziness is not allowed. 

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/guides/DEV-LEAD-GUIDE.md`
2. **Task Tracker:** `docs/mon-ext-2/ME2-TASK-TRACKER.md` (See Phase 13 and Phase 7 goals).

### Source Code Location
- **Main Toolset Application:** `tools/DdsMonitor/` (Blazor UI component `Program.cs`, `TopicColorService.cs`)

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/ME2-BATCH-07-REPORT.md`

---

## 🎯 Batch Objectives
- **DI Frame Stabilization:** The `IWorkspaceState` container relies on Blazor circuits tracking localized scope per user. Embedding it into a globally enforced Singleton destroys the dependency injection model. Fix this.
- **Dynamic Extensibility Paths:** Empower users to target entire directories when picking dependencies dynamically instead of clicking identical DLLs iteratively.

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

### Task 1: (Tech Debt) `TopicColorService` Scoped Dependency Injection Fix (ME2-T27)
**Files:** `tools/DdsMonitor/DdsMonitor.Blazor/Program.cs`, `tools/DdsMonitor/DdsMonitor.Engine/TopicColorService.cs`
**Requirements:**
**Problem:** "System.AggregateException: Some services are not able to be constructed (Error while validating the service descriptor 'ServiceType: DdsMonitor.Engine.TopicColorService Lifetime: Singleton ImplementationType: DdsMonitor.Engine.TopicColorService': Cannot consume scoped service 'DdsMonitor.Engine.IWorkspaceState' from singleton 'DdsMonitor.Engine.TopicColorService'.)"
**Fix Details:**
- Correct the `IServiceCollection` mappings dynamically declaring `TopicColorService` as `Scoped` safely matching `IWorkspaceState`, or gracefully decouple `IWorkspaceState` if `TopicColorService` absolutely requires static Singletons globally.

### Task 2: Folder-Based Assembly Scanning (ME2-T14)
**Files:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicSourcesPanel.razor`, `AssemblySourceService`
**Requirements:**
- Implement the "Add Folder" button logic as explicitly written in the design spec (`ME2-TASK-DETAILS.md#me2-t14--folder-based-assembly-scanning`). 
- When the user selects a directory, automatically recursively scan and extract every `.dll` module explicitly loading them structurally through your previously expanded plugin scopes.

---

## 🧪 Testing Requirements
- Confirm via Playwright that the application launches successfully without throwing the AggregateException during DI construction.
- Prove folder tracking imports dependencies successfully alongside previous components.

## 📊 Report Requirements

Provide professional developer insights matching Q1-Q5 guidelines outlining the DI scopes and memory considerations.

---

## 🎯 Success Criteria
- [ ] Task ME2-T27 successfully clears exception crashes upon loading the landing web interface.
- [ ] Task ME2-T14 accurately maps `IO` routines traversing directory paths recursively locating all relevant runtime dependencies.
- [ ] Playwright web driver succeeds clicking UI without exceptions dynamically.
- [ ] 100% test coverage passed clean.
