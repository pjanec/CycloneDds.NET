# MON-BATCH-03: Topic discovery service (DMON-003)

**Batch Number:** MON-BATCH-03  
**Tasks:** DMON-003  
**Phase:** 1 — Foundation  
**Estimated Effort:** 12-14 hours  
**Priority:** HIGH  
**Dependencies:** MON-BATCH-02

---

## ?? Onboarding & Workflow

### Developer Instructions
Implement the topic discovery service for DDS Monitor. Use xUnit only for new tests (no MSTest). Complete the full batch end-to-end without pausing for approvals (build, test, fix root causes).

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Developer Guide:** `.dev-workstream/guides/DEV-GUIDE.md`
3. **Code Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`
4. **DDS Monitor Onboarding:** `docs/ddsmon/ONBOARDING.md`
5. **DDS Monitor Design:** `docs/ddsmon/DESIGN.md` (§5.1)
6. **Task Details:** `docs/ddsmon/TASK-DETAIL.md` (DMON-003)
7. **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
8. **Previous Review:** `.dev-workstream/reviews/MON-BATCH-01-REVIEW.md`

### Source Code Location
- **Engine Library:** `tools/DdsMonitor/DdsMonitor.Engine/`
- **Test Project:** `tests/DdsMonitor.Engine.Tests/`
- **Solution File:** `CycloneDDS.NET.sln`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-03-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-03-QUESTIONS.md`

---

## Context

This batch introduces assembly scanning and topic discovery used to populate the DDS Monitor registry.

**Related Task:**
- [DMON-003](../../docs/ddsmon/TASK-DETAIL.md#dmon-003--topicdiscoveryservice-assembly-scanning)

---

## ?? Batch Objectives

- Implement `TopicDiscoveryService` and `ITopicRegistry`.
- Validate assembly isolation via collectible `AssemblyLoadContext`.
- Deliver xUnit tests that validate the discovery behavior, negative cases, and isolation.

---

## ? Tasks

### Task 1: TopicDiscoveryService (DMON-003)

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/` (NEW)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-003--topicdiscoveryservice-assembly-scanning)

**Requirements:**
- Scan one or more directories for `.dll` files.
- Load each DLL into a **collectible** `AssemblyLoadContext`.
- Discover types with `[DdsTopic]` and register `TopicMetadata` in `ITopicRegistry`.
- Ignore DLLs without topics.

**Tests Required (xUnit):**
- `TopicDiscoveryService_FindsTopicInAssembly`
- `TopicDiscoveryService_IgnoresDllsWithoutTopics`
- `TopicDiscoveryService_IsolatesAssemblyLoadContext`

---

## ?? Testing Requirements

- Run `dotnet build CycloneDDS.NET.sln`.
- Run `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj`.
- Fix any build or test failures without asking for permission. Complete the batch end-to-end.

---

## ?? Quality Standards

**? TEST QUALITY EXPECTATIONS**
- Use xUnit only. MSTest is not allowed for new tests.
- Tests must validate behavior (actual registry contents, actual isolation behavior).
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
- [ ] DMON-003 completed per task definition
- [ ] `dotnet build CycloneDDS.NET.sln` passes
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj` passes
- [ ] Report submitted to `.dev-workstream/reports/MON-BATCH-03-REPORT.md`

---

## ?? Common Pitfalls to Avoid

- Loading assemblies into the default context instead of a collectible `AssemblyLoadContext`.
- Registering topics without consistent `TopicMetadata` creation.
- Using MSTest or shallow tests.

---

## ?? Reference Materials

- **Task Defs:** `docs/ddsmon/TASK-DETAIL.md` (DMON-003)
- **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
- **Design:** `docs/ddsmon/DESIGN.md` (§5.1)
- **Onboarding:** `docs/ddsmon/ONBOARDING.md`
