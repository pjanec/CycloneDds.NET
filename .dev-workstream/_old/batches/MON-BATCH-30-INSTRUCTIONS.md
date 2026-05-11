# BATCH-30: BDC Detail Inspector & Time-Travel History

**Batch Number:** MON-BATCH-30  
**Tasks:** Corrective Task 0 (Debt), DMON-048, DMON-049  
**Phase:** Phase 6 (BDC Domain Plugin)  
**Estimated Effort:** 8-10 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-29

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome back! In Batch 29, the `EntityStore` dynamically aggregated `Instances` into generic `Entities`. Now we extend visibility into these entities. We're skipping the TKB folder view (`DMON-047`) for now, focusing instead on inspecting deep properties of individual entities and unlocking the Historical State (time-travel) engine.

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/DEV-GUIDE.md`
2. **API Deviations:** `docs/ddsmon/Plugin-API-deviations.md` (Specifically around using `@inject IWindowManager` in UI components)
3. **Task Definitions:** `docs/ddsmon/TASK-DETAIL.md` (See DMON-048, DMON-049)

### Source Code Location
- Continue operating within `tools/DdsMonitor/DdsMonitor.Plugins.Bdc/` and `tests/DdsMonitor.Plugins.Bdc.Tests/`.

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-30-REPORT.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 0:** Implement fix → **tests pass** ✅
2. **Task 1:** Implement → Write tests → **ALL tests pass** ✅
...

**DO NOT** move to the next task until the current one is fully implemented, strictly tested, and passing.

---

## ✅ Tasks

### Task 0: Corrective Issue (Tech Debt) - MON-DEBT-024

Address this technical debt item left over from Batch 29:
- **Problem:** `BdcSettings` are only held in memory and do not persist across server reboots.
- **Fix:** Integrate `BdcSettings` with the `WorkspacePersistenceService`. Ensure the plugin registers itself to load and save its configuration (Regex patterns, namespace prefix) into `workspace.json` automatically.

---

### Task 1: Entity Detail Inspector (DMON-048)

**Task Definition:** [DMON-048](../docs/ddsmon/TASK-DETAIL.md#dmon-048--entity-detail-inspector)

**Description:** Build the `EntityDetailPanel.razor` that inspects an individual entity's data fields and link the Grid.
**Requirements:**
1. Update `BdcEntityGridPanel.razor`: The "Detail" button must now successfully spawn an `EntityDetailPanel` using `IWindowManager` scoped to the current Blazor tab.
2. Build `EntityDetailPanel.razor` within the `Bdc` project. It must accept an `EntityId` as a parameter.
3. The panel displays the Master descriptor (topic) and all subordinate Part descriptors side-by-side or stacked vertically.
4. Render raw fields dynamically. You may reuse the engine's `SampleDetailView.razor` or build a simplified property grid that traverses the generic `DynamicData` / values.

**Tests Required:**
- ✅ Verify the UI binds to the Entity correctly and reflects changes if the instance store updates while the panel is open.

---

### Task 2: Historical State (Time-Travel) Engine (DMON-049)

**Task Definition:** [DMON-049](../docs/ddsmon/TASK-DETAIL.md#dmon-049--historical-state-time-travel)

**Description:** Allow users to request an Entity's absolute state at a prior Timestamp `T`.
**Requirements:**
1. In `EntityDetailPanel`, add a "View History At..." datetime picker.
2. Implement the `GetHistoricalState(int entityId, DateTime targetTime)` logic.
3. Use the `Entity.Journal` to determine if the entity was Alive/Zombie/Dead at that exact timestamp.
4. Query the globally injected `ISampleStore` to find the exact historical value of the Master topic and Part topics for that `EntityId` at the `targetTime`. Do a binary search on `ITopicSamples` chronologically.
5. Provide a readonly UI state rendering of the fields locked at that timeframe.

**Tests Required:**
- ✅ Unit test the binary-search historical matching algorithm using an isolated set of mock chronological samples. Provide exactly correct previous-state outcomes.

---

## 🧪 Testing Requirements
- The time-travel boundary testing should be your focus. Make a test where 5 rapidly changing states happen, and target a timestamp exactly between event #3 and #4. Ensure it returns the correct payload!

---

## 📊 Report Requirements

**✅ What to Answer in Your Report (`MON-BATCH-30-REPORT.md`):**
- **Time Travel Extrapolation:** How did you efficiently cross-reference the `EntityStore`'s `Journal` structure against the `ISampleStore` historical timeline without blowing up memory?
- **Persistence Handling:** Detail how you successfully threaded the `BdcSettings` into the existing workspace storage model.

---

## 🎯 Success Criteria
- [ ] Task 0 (Debt) eliminated completely.
- [ ] Clicking "Detail" in the BDC Entity Grid seamlessly spawns a rich object inspector panel.
- [ ] Historic Time-Travel successfully reconstructs past states perfectly using proper binary-search chronological lookups.
- [ ] The `MON-BATCH-30-REPORT.md` is submitted!
