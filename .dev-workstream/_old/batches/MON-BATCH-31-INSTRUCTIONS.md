# BATCH-31: TKB Entity Folder Tree panel

**Batch Number:** MON-BATCH-31  
**Tasks:** DMON-047  
**Phase:** Phase 6 (Domain Entity Plugins)  
**Estimated Effort:** 4-6 hours  
**Priority:** MEDIUM  
**Dependencies:** MON-BATCH-30

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome back. We have completed the primary Business Domain Component (BDC) plugin functionality. The final puzzle piece for Phase 6 is the `TKB Entity Folder Tree panel` specified in DMON-047. The Tactical Knowledge Base (TKB) usually presents entities geographically or via a hierarchical order of battle grouping, unlike the flat datagrid built in the BDC grid. 

In this batch, we will extract that nested/tree logic to build an alternative view of the domain entities. 

### Source Code Location
- Continue working within `tools/DdsMonitor/DdsMonitor.Plugins.Bdc/` and `tests/DdsMonitor.Plugins.Bdc.Tests/`.
- Although this task says "TKB", it fundamentally relies on the generic `EntityStore` aggregation you just built. Include it inside the BdcPlugin project for now to leverage the infrastructure.

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-31-REPORT.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

1. **Task 1:** Implement → Write tests → **ALL tests pass** ✅

**DO NOT** move to the next task until the current one is fully implemented, strictly tested, and passing.

---

## ✅ Tasks

### Task 1: TKB Entity Folder Tree panel (DMON-047)

**Task Definition:** [DMON-047](../docs/ddsmon/TASK-DETAIL.md#dmon-047--tkb-entity-folder-tree-panel)

**Description:** Build a navigational Blazor component using a Tree hierarchy for displaying domain entities.
**Requirements:**
1. Create `TkbEntityTreePanel.razor`.
2. Register this panel via `IMonitorContext.PanelRegistry.RegisterPanelType` under the `Plugins/BDC/Entity Tree` menu.
3. Hook deeply into the `EntityStore`, reacting to `EntityStore.Changed`. 
4. Aggregate entities dynamically into a folder-based tree structure. Grouping logic could be based off reading a `Team`, `Side` or `Category` string field out of the payload dynamically (e.g. iterate through the master topic and peek for a `string Category` field).
5. The leaf nodes of this tree are the Entities themselves. Clicking a leaf node should spawn the `EntityDetailPanel` you built in Batch 30 using the `IWindowManager`.

**Tests Required:**
- ✅ Unit tests proving the generic folder sorting correctly segments elements that have the `Category` string field vs elements that lack it.

---

## 🧪 Testing Requirements
Ensure your tests do not instantiate heavy UI components. Validate the projection (Tree Model generation) separated from the Blazor layout.

---

## 🎯 Success Criteria
- [ ] Tree view component built and successfully sorts aggregated entities into nested hierarchies.
- [ ] Nodes correctly pivot down to leaf-level entity click events.
- [ ] Missing category fields fallback to an Uncategorized folder correctly without throwing errors.
- [ ] The `MON-BATCH-31-REPORT.md` is submitted!
