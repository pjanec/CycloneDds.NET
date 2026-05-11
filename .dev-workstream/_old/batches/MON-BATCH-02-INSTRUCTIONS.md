# MON-BATCH-02: Topic metadata + synthetic fields + SampleData (DMON-002/004/005)

**Batch Number:** MON-BATCH-02  
**Tasks:** DMON-002, DMON-004, DMON-005  
**Phase:** 1 — Foundation  
**Estimated Effort:** 12-16 hours  
**Priority:** HIGH  
**Dependencies:** None

---

## 📋 Onboarding & Workflow

### Developer Instructions
This batch implements core Engine data models and synthetic fields. Use xUnit only for new tests (no MSTest). Complete the full batch end-to-end without pausing for approvals (build, test, fix root causes).

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Developer Guide:** `.dev-workstream/guides/DEV-GUIDE.md`
3. **Code Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`
4. **DDS Monitor Onboarding:** `docs/ddsmon/ONBOARDING.md`
5. **DDS Monitor Design:** `docs/ddsmon/DESIGN.md` (§4.2, §5.2–§5.4)
6. **Task Details:** `docs/ddsmon/TASK-DETAIL.md` (DMON-002, DMON-004, DMON-005)
7. **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
8. **Previous Review:** `.dev-workstream/reviews/MON-BATCH-01-REVIEW.md`

### Source Code Location
- **Engine Library:** `tools/DdsMonitor/DdsMonitor.Engine/`
- **Test Project:** `tests/DdsMonitor.Engine.Tests/`
- **Solution File:** `CycloneDDS.NET.sln`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-02-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/MON-BATCH-02-QUESTIONS.md`

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

**Why:** Ensures each component is solid before building on top of it. Prevents cascading failures.

---

## Context

This batch builds the core metadata and sample data model used throughout the DDS Monitor engine. It also adds synthetic fields as specified in the design.

**Related Tasks:**
- [DMON-002](../../docs/ddsmon/TASK-DETAIL.md#dmon-002--topicmetadata--fieldmetadata-types)
- [DMON-004](../../docs/ddsmon/TASK-DETAIL.md#dmon-004--synthetic-computed-fields)
- [DMON-005](../../docs/ddsmon/TASK-DETAIL.md#dmon-005--sampledata-record)

---

## 🎯 Batch Objectives

- Implement `TopicMetadata`/`FieldMetadata` with flattened field discovery.
- Add synthetic fields per design.
- Introduce the `SampleData`/`SenderIdentity` records.
- Deliver xUnit tests that validate actual behavior and edge cases in the task details.

---

## ✅ Tasks

### Task 1: TopicMetadata & FieldMetadata types (DMON-002)

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/` (NEW)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-002--topicmetadata--fieldmetadata-types)

**Requirements:**
- Implement `TopicMetadata` and `FieldMetadata` in the Engine project.
- Flatten nested public properties into dot-separated `StructuredName` paths.
- Use compiled accessors (Fasterflect or equivalent per design).

**Tests Required (xUnit):**
- `TopicMetadata_FlattensNestedProperties`
- `TopicMetadata_IdentifiesKeyFields`
- `FieldMetadata_Getter_ReturnsCorrectValue`
- `FieldMetadata_Setter_SetsCorrectValue`

---

### Task 2: Synthetic (computed) fields (DMON-004)

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/` (UPDATE)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-004--synthetic-computed-fields)

**Requirements:**
- Add synthetic fields `Delay [ms]` and `Size [B]` at the end of `AllFields`.
- Synthetic getters take `SampleData` as input.
- Mark synthetic fields with `IsSynthetic = true`.

**Tests Required (xUnit):**
- `SyntheticFields_AppearInAllFields`
- `SyntheticField_DelayGetter_ComputesCorrectly`

---

### Task 3: SampleData record (DMON-005)

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/` (NEW)  
**Task Definition:** See [TASK-DETAIL.md](../../docs/ddsmon/TASK-DETAIL.md#dmon-005--sampledata-record)

**Requirements:**
- Add `SampleData` and `SenderIdentity` records as specified.

**Tests Required (xUnit):**
- `SampleData_WithInitSyntax_SetsAllProperties`
- `SampleData_RecordEquality_WorksByValue`

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] DMON-002 completed per task definition
- [ ] DMON-004 completed per task definition
- [ ] DMON-005 completed per task definition
- [ ] `dotnet build CycloneDDS.NET.sln` passes
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj` passes
- [ ] Report submitted to `.dev-workstream/reports/MON-BATCH-02-REPORT.md`

---

## ⚠️ Common Pitfalls to Avoid

- Missing synthetic fields or placing them in the wrong order.
- Using reflection in hot paths instead of compiled getters/setters.
- Forgetting `[DdsKey]` handling for key field extraction in metadata.
- Using MSTest or shallow tests.

---

## 📚 Reference Materials

- **Task Defs:** `docs/ddsmon/TASK-DETAIL.md` (DMON-002/004/005)
- **Task Tracker:** `docs/ddsmon/TASK-TRACKER.md`
- **Design:** `docs/ddsmon/DESIGN.md` (§4.2, §5.2–§5.4)
- **Onboarding:** `docs/ddsmon/ONBOARDING.md`
