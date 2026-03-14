# ME1-BATCH-01: CodeGen & Schema Core Extensions

**Batch Number:** ME1-BATCH-01  
**Tasks:** ME1-T01, ME1-T02, ME1-T03
**Phase:** Phase 1 — CodeGen & Schema Core  
**Estimated Effort:** 8 hours  
**Priority:** HIGH  
**Dependencies:** None

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome to Monitoring Extensions 1 (ME1) workstream! Your goal in this first batch is to extend the code generator and schema layer with three new capabilities:
1. Typed Enum `@bit_bound` support
2. C# 12 `[InlineArray]` reflection and UI support
3. Default topic name inference from namespace

**CRITICAL:** Be completely autonomous. If you encounter errors, test failures, or missing prerequisites, do **not** stop and ask for permission. Fix the root cause, write necessary tests, ensure all tests pass, and proceed. Provide a full account of your actions in the final report. No laziness allowed.

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/guides/DEV-GUIDE.md` - How to work with batches
2. **Onboarding:** `docs/mon-ext-1/ME1-ONBOARDING.md` - General project info and build commands
3. **Design Document:** `docs/mon-ext-1/ME1-DESIGN.md` - *Specifically Phase 1 (1.1, 1.2, 1.3)*
4. **Task Definitions:** `docs/mon-ext-1/ME1-TASK-DETAILS.md` - Look for ME1-T01, ME1-T02, ME1-T03

### Source Code Location
- **Primary Work Area:**
  - `src/CycloneDDS.Schema/`
  - `tools/CycloneDDS.CodeGen/`
  - `tools/DdsMonitor/DdsMonitor.Engine/`
  - `tools/DdsMonitor/DdsMonitor.Blazor/`
- **Test Projects:**
  - `tests/CycloneDDS.CodeGen.Tests/`
  - `tests/DdsMonitor.Engine.Tests/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/ME1-BATCH-01-REPORT.md`

**If you have questions (that block implementation entirely), create:**  
`.dev-workstream/questions/ME1-BATCH-01-QUESTIONS.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1 (ME1-T01):** Implement → Write tests → **ALL tests pass** ✅
2. **Task 2 (ME1-T02):** Implement → Write tests → **ALL tests pass** ✅  
3. **Task 3 (ME1-T03):** Implement → Write tests → **ALL tests pass** ✅

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written
- ✅ **ALL tests passing** (including existing tests)

**Why:** Ensures each component is solid before building on top of it. Prevents cascading failures.

---

## Context

This batch focuses on extending the foundational type generation and schema layer. It sits at the absolute core of our system. By correctly reflecting C# underlying types (enums) and modern features like `[InlineArray]`, the DDS Monitor can deeply inspect real game assemblies without imposing unsafe memory constructs on game developers.

**Related Tasks:**
- [ME1-T01](docs/mon-ext-1/ME1-TASK-DETAILS.md#me1-t01--typed-enum-bit_bound-support) - Read exact specs and success conditions
- [ME1-T02](docs/mon-ext-1/ME1-TASK-DETAILS.md#me1-t02--inlinearray-support) - Read exact specs and success conditions
- [ME1-T03](docs/mon-ext-1/ME1-TASK-DETAILS.md#me1-t03--default-topic-name-from-namespace) - Read exact specs and success conditions

---

## 🎯 Batch Objectives

- Introduce IDL `@bit_bound` declarations and narrowed integer I/O in our C# serializers for enums backing `byte` and `short`.
- Interpret C# 12 `[InlineArray]` uniformly with existing `unsafe fixed` arrays in schema, code generation, and UI.
- Fallback un-named `[DdsTopic]` structs to their full, underscored namespace.

---

## ✅ Tasks

### Task 1: Typed Enum `@bit_bound` Support (ME1-T01)

**Files:**
- `tools/CycloneDDS.CodeGen/TypeInfo.cs`
- `tools/CycloneDDS.CodeGen/SchemaDiscovery.cs`
- `tools/CycloneDDS.CodeGen/IdlEmitter.cs`
- `tools/CycloneDDS.CodeGen/SerializerEmitter.cs`

**Description:**
Read the C# enum underlying type (e.g., `byte`, `short`) using Roslyn and emit the proper DDS `@bit_bound(N)` annotation and C# serialization methods.

**Detailed Specs & Success Conditions:**
**DO NOT DUPLICATE DESIGN:** Rather than repeating, rely exclusively on `docs/mon-ext-1/ME1-TASK-DETAILS.md#me1-t01--typed-enum-bit_bound-support` for exact success criteria, required unit tests, and the list of specific file modifications.

**Tests Required:**
- ✅ Verify `@bit_bound(8)` generation for `byte`-backed enums.
- ✅ Verify no `@bit_bound` emitted for standard 32-bit enums.
- ✅ Validate narrowed binary writes in generated C# serializers (e.g., `WriteUInt16(ushort)` for `short`).

---

### Task 2: `[InlineArray]` Support (ME1-T02)

**Files:**
- `tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs`
- `tools/DdsMonitor/DdsMonitor.Engine/Json/FixedBufferJsonConverter.cs`
- `tools/CycloneDDS.CodeGen/SerializerEmitter.cs`
- `tools/DdsMonitor/DdsMonitor.Blazor/Components/DynamicForm.razor`

**Description:**
Handle `System.Runtime.CompilerServices.InlineArrayAttribute`. Reflection and serialization should treat inline arrays precisely like `unsafe fixed` arrays in layout and JSON representation, without actually requiring `unsafe` modifiers in C#.

**Detailed Specs & Success Conditions:**
Read `docs/mon-ext-1/ME1-TASK-DETAILS.md#me1-t02--inlinearray-support` for all exact unit tests required. Do not proceed until Blazor's UI and JSON serialization handle inline array elements cleanly.

**Tests Required:**
- ✅ TopicMetadata populates `isFixedSizeArray=true` and `fixedArrayLength=N`.
- ✅ FixedBufferJsonConverter accurately targets and loops through the created span.
- ✅ Existing `FixedBufferAttribute` logic tests remain unbroken.

---

### Task 3: Default Topic Name from Namespace (ME1-T03)

**Files:**
- `src/CycloneDDS.Schema/Attributes/TypeLevel/DdsTopicAttribute.cs`
- `tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs`
- `tools/CycloneDDS.CodeGen/SchemaDiscovery.cs`
- `tools/CycloneDDS.CodeGen/IdlEmitter.cs`/`SerializerEmitter.cs`

**Description:**
The `topicName` parameter to `[DdsTopic]` becomes optional. When not explicitly provided, type full namespace path replaces `.` with `_`.

**Detailed Specs & Success Conditions:**
See `docs/mon-ext-1/ME1-TASK-DETAILS.md#me1-t03--default-topic-name-from-namespace` for the success matrix and exactly what edge cases to test.

**Tests Required:**
- ✅ Omitted `topicName` parameter yields a `Namespace_Name_MyStruct` output.
- ✅ Hardcoded test names are respected continuously.
- ✅ Schema code generator test asserts default fallbacks are safe from null reference defects.

---

## 🧪 Testing Requirements

You must add new explicit unit testing corresponding to each "Success Condition" dictated inside `ME1-TASK-DETAILS.md`.

**❗ TEST QUALITY EXPECTATIONS**
- **NOT ACCEPTABLE:** Checking simple compiler non-failure. Or `Assert.Contains("string", out)` without actual structural proof.
- **REQUIRED:** You must inspect emitted code sizes, assert types precisely using reflection equivalents (whenever possible), and guarantee runtime span data integrity for the `[InlineArray]`. Behavior matters over string existence.

---

## 📊 Report Requirements

Your report should focus on valuable engineering context and insights that aren't visible through simply passing tests. Create `.dev-workstream/reports/ME1-BATCH-01-REPORT.md` and explicitly include:

## Developer Insights

**Q1:** What issues did you encounter implementing `[InlineArray]` spans in `DynamicForm.razor` and JSON converters? How did you solve them?

**Q2:** During the underlying type analysis in `SchemaDiscovery.cs`, were there any weird edge-case special types encountered from Roslyn?

**Q3:** Did you spot any weak points/code debt inside the existing IDL code generation logic that you would refactor if given time?

**Q4:** What design decisions did you have to make that weren't strictly defined in the `ME1-TASK-DETAILS.md` spec?

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] Task ME1-T01 completed (with robust bit selection logic)
- [ ] Task ME1-T02 completed (Inline Arrays parsed accurately)
- [ ] Task ME1-T03 completed (topic-names default safely)
- [ ] NO test regressions.
- [ ] All requested tests from `ME1-TASK-DETAILS.md` are passing locally.
- [ ] Developer Report submitted including responses to Developer Insights.

---

## ⚠️ Common Pitfalls to Avoid
- **Not fixing root causes**: Failing builds / CI during development shouldn't block you from creating an immediate workaround/fix and reporting it.
- **`[InlineArray]` Length Off-by-One**: Remember that Length on InlineArray corresponds to elements. It doesn't mean size in bytes. Be careful crossing `MemoryMarshal`.
- **String searching generated code**: We use compilation and structure verification, not simple `Assert.Contains` strings when thoroughly verifying generator outputs.

---

## 📚 Reference Materials
- [ME1-TASK-DETAILS.md](docs/mon-ext-1/ME1-TASK-DETAILS.md) - Exact requirements for Tasks 1-3.
- [ME1-DESIGN.md](docs/mon-ext-1/ME1-DESIGN.md) - High-level goals.
- `.dev-workstream/guides/CODE-STANDARDS.md` - (If available, general code expectations).
