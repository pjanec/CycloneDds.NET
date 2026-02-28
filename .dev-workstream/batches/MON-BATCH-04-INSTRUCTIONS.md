# MON-BATCH-04: Dynamic reader/writer abstractions (DMON-006/007/008)

**Batch Number:** MON-BATCH-04  
**Tasks:** DMON-006, DMON-007, DMON-008  
**Phase:** 1 — Foundation  
**Estimated Effort:** 12-16 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-02, MON-BATCH-03

---

## ?? Onboarding & Workflow

### Developer Instructions
Implement the dynamic DDS reader/writer abstractions and their concrete implementations. Use xUnit only for new tests (no MSTest). Complete the full batch end-to-end without pausing for approvals (build, test, fix root causes).

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Developer Guide:** `.dev-workstream/guides/DEV-GUIDE.md`
3. **Code Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`
4. **DDS Monitor Onboarding:** `docs/ddsmon/ONBOARDING.md`
5. **DDS Monitor Design:** `docs/ddsmon/DESIGN.md` (§4.3, §4.4)
6. **Task Details:** `docs/ddsmon/TASK-DETAIL.md` (DMON-006/007/008)
7. **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
8. **Previous Review:** `.dev-workstream/reviews/MON-BATCH-03-REVIEW.md`

### Source Code Location
- **Engine Library:** `tools/DdsMonitor/DdsMonitor.Engine/`
- **Test Project:** `tests/DdsMonitor.Engine.Tests/`
- **Solution File:** `CycloneDDS.NET.sln`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-04-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-04-QUESTIONS.md`

---

## ?? MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1:** Implement ? Write tests ? **ALL tests pass** ?
2. **Task 2:** Implement ? Write tests ? **ALL tests pass** ?  
3. **Task 3:** Implement ? Write tests ? **ALL tests pass** ?

**DO NOT** move to the next task until:
- ? Current task implementation complete
- ? Current task tests written
- ? **ALL tests passing** (including previous batch tests)

**Why:** Ensures each component is solid before building on top of it. Prevents cascading failures.

---

## Context

This batch adds the dynamic reader/writer interfaces and their concrete implementations used by the DDS bridge.

**Related Tasks:**
- [DMON-006](../../docs/ddsmon/TASK-DETAIL.md#dmon-006--idynamicreader--idynamicwriter-interfaces)
- [DMON-007](../../docs/ddsmon/TASK-DETAIL.md#dmon-007--dynamicreadert-implementation)
- [DMON-008](../../docs/ddsmon/TASK-DETAIL.md#dmon-008--dynamicwritert-implementation)

---

## ?? Batch Objectives

- Define `IDynamicReader`/`IDynamicWriter` interfaces.
- Implement `DynamicReader<T>` and `DynamicWriter<T>` wrappers.
- Deliver xUnit tests validating reflection instantiation, event delivery, and reader/writer integration.

---

## ? Tasks

### Task 1: IDynamicReader / IDynamicWriter interfaces (DMON-006)

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/` (NEW)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-006--idynamicreader--idynamicwriter-interfaces)

**Requirements:**
- Define interfaces exactly as specified in task detail.
- Include `OnSampleReceived` event on `IDynamicReader`.

**Tests Required (xUnit):**
- `MockDynamicReader_FiresOnSampleReceived`

---

### Task 2: DynamicReader<T> implementation (DMON-007)

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/` (NEW)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-007--dynamicreadert-implementation)

**Requirements:**
- Wrap `DdsReader<T>` and stream `SampleData` via `OnSampleReceived`.
- Start/Stop behavior with cancellation.
- Instantiate via `MakeGenericType` + `Activator.CreateInstance`.

**Tests Required (xUnit):**
- `DynamicReader_CanBeConstructedViaReflection`
- `DynamicReader_ReceivesSample_FromDynamicWriter` (integration test)

---

### Task 3: DynamicWriter<T> implementation (DMON-008)

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/` (NEW)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-008--dynamicwritert-implementation)

**Requirements:**
- Wrap `DdsWriter<T>`.
- `Write(object payload)` and `DisposeInstance(object payload)` unbox and forward.

**Tests Required (xUnit):**
- `DynamicWriter_Write_DoesNotThrow`
- `DynamicWriter_DisposeInstance_DoesNotThrow`

---

## ?? Testing Requirements

- Run `dotnet build CycloneDDS.NET.sln`.
- Run `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj`.
- Fix any build or test failures without asking for permission. Complete the batch end-to-end.

---

## ?? Quality Standards

**? TEST QUALITY EXPECTATIONS**
- Use xUnit only. MSTest is not allowed for new tests.
- Tests must validate actual behavior and event delivery.
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
- [ ] DMON-006 completed per task definition
- [ ] DMON-007 completed per task definition
- [ ] DMON-008 completed per task definition
- [ ] `dotnet build CycloneDDS.NET.sln` passes
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj` passes
- [ ] Report submitted to `.dev-workstream/reports/MON-BATCH-04-REPORT.md`

---

## ?? Common Pitfalls to Avoid

- Missing cancellation/stop handling in `DynamicReader<T>`.
- Forgetting to fire `OnSampleReceived` for each sample.
- Swallowing DDS exceptions without surfacing in tests.
- Using MSTest or shallow tests.

---

## ?? Reference Materials

- **Task Defs:** `docs/ddsmon/TASK-DETAIL.md` (DMON-006/007/008)
- **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
- **Design:** `docs/ddsmon/DESIGN.md` (§4.3, §4.4)
- **Onboarding:** `docs/ddsmon/ONBOARDING.md`
