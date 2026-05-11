# BATCH-29: BDC Plugin Foundation & Aggregation Engine

**Batch Number:** MON-BATCH-29  
**Tasks:** Corrective Task 0 (Debt), DMON-045, DMON-061, DMON-062, DMON-060, DMON-046  
**Phase:** Phase 6 (BDC Domain Plugin)  
**Estimated Effort:** 8-10 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-28

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome to Phase 6! We are building the real-world Business Domain Component (BDC) plugin. 

**IMPORTANT ARCHITECTURAL NOTE:**
- We are implementing the BDC plugin **in this repository / folder** (e.g. `tools/DdsMonitor/DdsMonitor.Plugins.Bdc/DdsMonitor.Plugins.Bdc.csproj` — a Razor Class Library), not an external outer folder.
- **Do NOT reference the project-specific `bdc-data-model` from this codebase.** That folder (`docs/ddsmon/bdc-data-model`) is provided strictly as a mental reference of what topics the plugin will ingest. The BDC plugin must remain completely decoupled from specific data models; it aggregates entities based on dynamic keys and namespace prefixes.
- **API Deviations:** Review `docs/ddsmon/Plugin-API-deviations.md`. The original design specs (DMON-045+) assumed certain APIs would be accessible via `IMonitorContext`. Due to the Blazor scoped DI architecture implemented in Batch 28, you must use Dependency Injection (e.g., constructor injection in backend services, `@inject` in UI components) to access Singletons (`ISampleStore`, `IInstanceStore`), and `IMonitorContext.PanelRegistry` must be used for panel registration.

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/DEV-GUIDE.md`
2. **API Deviations:** `docs/ddsmon/Plugin-API-deviations.md`
3. **BDC Addendum:** `docs/ddsmon/BDC-plugin-addendum.md`
4. **Task Definitions:** `docs/ddsmon/TASK-DETAIL.md` (See DMON-045, DMON-046)

### Source Code Location
- **Primary Work Area:** Create `tools/DdsMonitor/DdsMonitor.Plugins.Bdc/` (RCL project) and include it in the solution.
- **Test Work Area:** Create `tests/DdsMonitor.Plugins.Bdc.Tests/` (xUnit test project).

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-29-REPORT.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 0:** Implement fix → **tests pass** ✅
2. **Task 1:** Implement → Write tests → **ALL tests pass** ✅
...

**DO NOT** move to the next task until the current one is fully implemented, strictly tested, and passing.

---

## ✅ Tasks

### Task 0: Corrective Issue (Tech Debt)

Before starting feature work, address this technical debt item from the previous batch:
- **File:** `tests/DdsMonitor.Engine.Tests/Batch28Tests.cs` (Line 62)
- **Fix:** Remove the unused variable warning (`CS0219`: `invoked is assigned but its value is never used`) or assert against it. Ensure zero compiler warnings across the entire solution.

---

### Task 1: EntityStore Core (DMON-045) & Regex Validation (DMON-061, DMON-062)

**Task Definitions:** [DMON-045](../docs/ddsmon/TASK-DETAIL.md#dmon-045--entitystore-core-aggregation-engine) + `BDC-plugin-addendum.md`

**Description:** Implement the background service that aggregates raw DDS samples into domain entities.
**Requirements:**
1. Create `EntityStore` (as a singleton service injected via `ConfigureServices`). It will subscribe to `IInstanceStore.OnInstanceChanged`.
2. Extract the `EntityId` and `PartId` purely using RegEx patterns against `FieldMetadata.StructuredName` instead of assuming hardcoded indexing (DMON-061).
3. Validate that matched key fields are strictly standard 32-bit or 64-bit integers. If they aren't, reject/ignore the topic entirely (DMON-062).
4. Maintain `Dictionary<int, Entity>`. An `Entity` aggregates all related descriptors.
5. Apply state transitioning logic (Alive, Zombie, Dead) based on presence of a Master descriptor.

**Tests Required:**
- ✅ Unit test verifying missing/invalid numeric types are gracefully skipped (DMON-062).
- ✅ Unit test verifying generic regex extraction retrieves the ids from correct field paths (DMON-061).
- ✅ Unit tests validating the Alive/Zombie/Dead state machine transitions from DMON-045.

---

### Task 2: Settings Panel & Configuration Persistence (DMON-060)

**Task Definition:** `BDC-plugin-addendum.md`

**Description:** Provide UI for modifying the plugin's namespace and regex definitions.
**Requirements:**
1. Create `BdcSettingsPanel.razor` within the plugin RCL.
2. Bind input fields for configuring: Namespace Prefix filter, EntityId Regex, PartId Regex.
3. Save configuration states efficiently to the global workspace via `WorkspacePersistenceService` if applicable, or plugin-specific persistent store so selections survive reboots.
4. Add a menu button under `Plugins/BDC/Settings` in `IMonitorContext.MenuRegistry` during plugin `Initialize()` to spawn this panel.

**Tests Required:**
- ✅ Unit/Integration test proving that changing the regex dynamically resets/re-aggregates the `EntityStore` accordingly.

---

### Task 3: BDC Entity Grid Panel (DMON-046)

**Task Definition:** [DMON-046](../docs/ddsmon/TASK-DETAIL.md#dmon-046--bdc-entity-grid-panel)

**Description:** Build the Live Grid summarizing all constructed aggregations.
**Requirements:**
1. Create `BdcEntityGridPanel.razor`.
2. Register this panel via `IMonitorContext.PanelRegistry.RegisterPanelType`. Add a menu item `Plugins/BDC/Entity Grid` to spawn it.
3. Inject `IWindowManager`, `ISampleStore` directly into the panel via DI (see API Deviations module).
4. Show rows corresponding to elements in the active `EntityStore`. Format properly with Data Grid virtual wrappers. Include column sorting.
5. Include a Live / History toggle (Live showing active entities, history showing `EntityJournal` snapshots).

**Tests Required:**
- ✅ Unit tests checking if virtual grids correctly bind against `EntityStore` collections.
- ✅ Unit tests observing correct UI state changes on toggle triggers.

---

## 🧪 Testing Requirements
- **Quality over Quantity:** Tests must verify actual layout aggregations. Does `EntityStore` properly categorize unrelated topics if regexes overlap? Does the grid survive rapid insertions?
- Avoid shallow string presence assertions. If compiling/loading Blazor UI components is difficult, isolate the core domain aggregation engines completely so logic can be validated decoupled from the HTML.

---

## 📊 Report Requirements

**✅ What to Answer in Your Report (`MON-BATCH-29-REPORT.md`):**
- **State Initialization:** How did you initialize `EntityStore`'s required dependencies and DI subscriptions within `PluginLoader.ConfigureServices`?
- **Regex Edge Cases:** Were there edge cases in mapping generic C# numeric types to `DdsKey` attributes? What issues did you face with the regex extraction mechanism?
- **Performance:** What approaches did you take to guarantee `EntityStore` could scale when iterating hundreds of overlapping matching topics?

---

## 🎯 Success Criteria
- [ ] Task 0 (Debt) eliminated completely.
- [ ] New `DdsMonitor.Plugins.Bdc` class library is established and loads cleanly through `PluginLoader`.
- [ ] `EntityStore` successfully aggregates `InstanceTransitionEvent` entries entirely driven by RegEx heuristics without referencing external data objects.
- [ ] `BdcSettingsPanel` drives those exact RegEx configurations.
- [ ] `BdcEntityGridPanel` displays summarized entity health.
- [ ] The `MON-BATCH-29-REPORT.md` is submitted!
