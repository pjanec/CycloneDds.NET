# MON-BATCH-08: Host wiring + EventBroker + WindowManager + Desktop shell (DMON-014/015/016/017)

**Batch Number:** MON-BATCH-08  
**Tasks:** DMON-014, DMON-015, DMON-016, DMON-017  
**Phase:** 1–2 Transition (Foundation ? Blazor Shell)  
**Estimated Effort:** 20-28 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-07

---

## ?? Onboarding & Workflow

### Developer Instructions
This batch wires the app host and introduces the first UI infrastructure. Use xUnit only for new tests (no MSTest). Complete the full batch end-to-end without pausing for approvals (build, test, fix root causes).

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Developer Guide:** `.dev-workstream/guides/DEV-GUIDE.md`
3. **Code Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`
4. **DDS Monitor Onboarding:** `docs/ddsmon/ONBOARDING.md`
5. **DDS Monitor Design:** `docs/ddsmon/DESIGN.md` (§8, §9, §14)
6. **Task Details:** `docs/ddsmon/TASK-DETAIL.md` (DMON-014/015/016/017)
7. **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
8. **Previous Review:** `.dev-workstream/reviews/MON-BATCH-07-REVIEW.md`

### Source Code Location
- **Blazor Host:** `tools/DdsMonitor/`
- **Engine Library:** `tools/DdsMonitor/DdsMonitor.Engine/`
- **Test Project:** `tests/DdsMonitor.Engine.Tests/`
- **Solution File:** `CycloneDDS.NET.sln`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-08-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-08-QUESTIONS.md`

---

## ?? MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1:** Implement ? Write tests ? **ALL tests pass** ?
2. **Task 2:** Implement ? Write tests ? **ALL tests pass** ?  
3. **Task 3:** Implement ? Write tests ? **ALL tests pass** ?
4. **Task 4:** Implement ? Write tests ? **ALL tests pass** ?

**DO NOT** move to the next task until:
- ? Current task implementation complete
- ? Current task tests written
- ? **ALL tests passing** (including previous batch tests)

**Why:** Ensures each component is solid before building on top of it. Prevents cascading failures.

---

## Context

This batch brings the application to life: DI wiring, event broker, window management, and the initial desktop shell.

**Related Tasks:**
- [DMON-014](../../docs/ddsmon/TASK-DETAIL.md#dmon-014--application-host--di-wiring)
- [DMON-015](../../docs/ddsmon/TASK-DETAIL.md#dmon-015--eventbroker-pubsub)
- [DMON-016](../../docs/ddsmon/TASK-DETAIL.md#dmon-016--panelstate-model--iwindowmanager-interface)
- [DMON-017](../../docs/ddsmon/TASK-DETAIL.md#dmon-017--desktoprazor-shell--panel-chrome)

---

## ?? Batch Objectives

- Wire up the application host and DI configuration.
- Implement the EventBroker for panel-to-panel communication.
- Implement WindowManager and PanelState models with persistence support.
- Build the initial `Desktop.razor` shell with draggable/resizable panels.

---

## ? Tasks

### Task 1: Application host & DI wiring (DMON-014)

**Files:** `tools/DdsMonitor/` (UPDATE)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-014--application-host--di-wiring)

**Requirements:**
- Add DI registrations as specified (Singletons, Scoped services, Hosted service).
- Add `appsettings.json` with `DdsSettings` defaults.
- Verify Kestrel starts and Blazor loads.

**Tests Required (xUnit):**
- Add a DI resolution test that builds the service provider and resolves all required services without exceptions.

---

### Task 2: EventBroker (DMON-015)

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/` (NEW)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-015--eventbroker-pubsub)

**Tests Required (xUnit):**
- `EventBroker_PublishAndSubscribe_DeliversEvent`
- `EventBroker_Dispose_StopsDelivery`
- `EventBroker_MultipleSubscribers_AllReceive`
- `EventBroker_DifferentEventTypes_DoNotCrossTalk`

---

### Task 3: PanelState model & IWindowManager (DMON-016)

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/` (NEW)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-016--panelstate-model--iwindowmanager-interface)

**Tests Required (xUnit):**
- `WindowManager_SpawnPanel_AssignsUniqueId`
- `WindowManager_ClosePanel_RemovesFromList`
- `WindowManager_BringToFront_SetsHighestZIndex`
- `WindowManager_SaveAndLoad_RoundTrips`

---

### Task 4: Desktop.razor shell & panel chrome (DMON-017)

**Files:** `tools/DdsMonitor/` (NEW/UPDATE)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-017--desktoprazor-shell--panel-chrome)

**Requirements:**
- Implement draggable, resizable panels with title bar and close/minimize.
- Use `DynamicComponent` for panel body.
- Minimized panels collapse to bottom strip.

**Tests Required:**
- Manual tests per DMON-017 success conditions.

---

## ?? Testing Requirements

- Run `dotnet build CycloneDDS.NET.sln`.
- Run `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj`.
- Fix any build or test failures without asking for permission. Complete the batch end-to-end.

---

## ?? Quality Standards

**? TEST QUALITY EXPECTATIONS**
- Use xUnit only. MSTest is not allowed for new tests.
- Tests must validate actual behavior (event delivery, window state changes).
- Do not rely on string-presence checks for correctness.

**? REPORT QUALITY EXPECTATIONS**
- Document issues encountered and how you resolved them.
- Document any design decisions you made beyond the instructions.
- Note any edge cases or follow-up work needed.

---

## ?? Report Requirements

## Developer Insights

**Q1:** What issues did you encounter during implementation? How did you resolve them?

**Q2:** Did you spot any weak points in the existing codebase? What would you improve?

**Q3:** What design decisions did you make beyond the instructions? What alternatives did you consider?

**Q4:** What edge cases did you discover that weren't mentioned in the spec?

**Q5:** Are there any performance concerns or optimization opportunities you noticed?

---

## ?? Success Criteria

This batch is DONE when:
- [ ] DMON-014 completed per task definition
- [ ] DMON-015 completed per task definition
- [ ] DMON-016 completed per task definition
- [ ] DMON-017 completed per task definition
- [ ] `dotnet build CycloneDDS.NET.sln` passes
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj` passes
- [ ] Report submitted to `.dev-workstream/reports/MON-BATCH-08-REPORT.md`

---

## ?? Common Pitfalls to Avoid

- Registering WindowManager as Singleton instead of Scoped.
- Forgetting to register `Channel<SampleData>` or `DdsIngestionService`.
- Missing panel z-index updates on bring-to-front.
- Skipping manual tests for drag/resize/minimize.

---

## ?? Reference Materials

- **Task Defs:** `docs/ddsmon/TASK-DETAIL.md` (DMON-014/015/016/017)
- **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
- **Design:** `docs/ddsmon/DESIGN.md` (§8, §9, §14)
- **Onboarding:** `docs/ddsmon/ONBOARDING.md`
