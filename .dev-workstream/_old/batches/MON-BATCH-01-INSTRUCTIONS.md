# MON-BATCH-01: DDS Monitor scaffolding (DMON-001)

**Batch Number:** MON-BATCH-01  
**Tasks:** DMON-001  
**Phase:** 1 — Foundation  
**Estimated Effort:** 12-14 hours  
**Priority:** HIGH  
**Dependencies:** None

---

## ?? Onboarding & Workflow

### Developer Instructions
You are implementing the initial scaffolding for the DDS Monitor toolchain. Follow the existing project conventions and use xUnit for all new tests. Do not use MSTest.

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Developer Guide:** `.dev-workstream/guides/DEV-GUIDE.md`
3. **Code Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`
4. **DDS Monitor Onboarding:** `docs/ddsmon/ONBOARDING.md`
5. **DDS Monitor Design:** `docs/ddsmon/DESIGN.md` (focus on §2, §14)
6. **Task Details:** `docs/ddsmon/TASK-DETAIL.md` (DMON-001)
7. **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`

### Source Code Location
- **Primary Work Area:** `tools/DdsMonitor/`
- **Engine Library:** `tools/DdsMonitor/DdsMonitor.Engine/`
- **Test Project:** `tests/DdsMonitor.Engine.Tests/`
- **Solution File:** `CycloneDDS.NET.sln`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-01-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-01-QUESTIONS.md`

---

## Context

This batch sets up the DDS Monitor solution structure and test scaffolding. It enables all subsequent DMON tasks to build on a consistent project layout.

**Related Task:**
- [DMON-001](../../docs/ddsmon/TASK-DETAIL.md#dmon-001--create-solution--project-scaffolding)

---

## ?? Batch Objectives

- Create the initial DDS Monitor projects (Blazor host, Engine library, Engine tests).
- Add required package references.
- Wire the projects into `CycloneDDS.NET.sln`.
- Ensure the solution builds and the xUnit test scaffold passes.

---

## ? Tasks

### Task 1: Create solution & project scaffolding (DMON-001)

**Files:**
- `tools/DdsMonitor/DdsMonitor.csproj` (NEW)
- `tools/DdsMonitor/DdsMonitor.Engine/DdsMonitor.Engine.csproj` (NEW)
- `tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj` (NEW)
- `CycloneDDS.NET.sln` (UPDATE)

**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-001--create-solution--project-scaffolding)

**Requirements:**
- Target framework: `net8.0` for all new projects.
- `DdsMonitor.Engine` must have **no** `Microsoft.AspNetCore.*` or Blazor references.
- Use xUnit in `tests/DdsMonitor.Engine.Tests` (do not use MSTest).
- Add required packages:
  - `tools/DdsMonitor/DdsMonitor.Engine/DdsMonitor.Engine.csproj`: `Fasterflect.Netstandard`, `System.Linq.Dynamic.Core`
  - `tools/DdsMonitor/DdsMonitor.csproj`: `Microsoft.AspNetCore.Components`
- Reference existing runtime projects where needed:
  - `DdsMonitor.csproj` ? `src/CycloneDDS.Runtime/CycloneDDS.Runtime.csproj`
  - `DdsMonitor.Engine.csproj` ? `src/CycloneDDS.Runtime/CycloneDDS.Runtime.csproj`, `src/CycloneDDS.Core/CycloneDDS.Core.csproj`
- Add all three projects to `CycloneDDS.NET.sln`.
- Add a placeholder xUnit test:
  - `Scaffold_Builds()` ? `Assert.True(true)`

**Design Reference:** `docs/ddsmon/DESIGN.md` §2, §14

**Tests Required (xUnit only):**
- ? `Scaffold_Builds()` placeholder test in `tests/DdsMonitor.Engine.Tests`

---

## ?? Testing Requirements

- Run `dotnet build CycloneDDS.NET.sln`.
- Run `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj`.
- Fix any build or test failures without asking for permission. Complete the batch end-to-end.

---

## ?? Quality Standards

**? TEST QUALITY EXPECTATIONS**
- Use xUnit only. MSTest is not allowed for new tests.
- Tests must be meaningful, even if placeholder for scaffolding.

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
- [ ] DMON-001 completed per task definition
- [ ] `dotnet build CycloneDDS.NET.sln` passes
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj` passes
- [ ] Report submitted to `.dev-workstream/reports/MON-BATCH-01-REPORT.md`

---

## ?? Common Pitfalls to Avoid

- Forgetting to add new projects to `CycloneDDS.NET.sln`.
- Introducing Blazor dependencies into `DdsMonitor.Engine`.
- Using MSTest instead of xUnit.
- Skipping build/test runs.

---

## ?? Reference Materials

- **Task Defs:** `docs/ddsmon/TASK-DETAIL.md` (DMON-001)
- **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
- **Design:** `docs/ddsmon/DESIGN.md` §2, §14
- **Onboarding:** `docs/ddsmon/ONBOARDING.md`
