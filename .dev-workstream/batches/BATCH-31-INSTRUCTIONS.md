# BATCH-31: IDL Importer - Foundation

**Batch Number:** BATCH-31
**Tasks:** IDLIMP-001, IDLIMP-002, IDLIMP-003
**Phase:** Phase 1: Foundation
**Estimated Effort:** 6-8 hours
**Priority:** HIGH
**Dependencies:** None

---

## 📋 Onboarding & Workflow

### Developer Instructions
This batch initiates the development of the **IDL Importer Tool**, a new utility to convert IDL files into C# DSL.
Your focus is on **Shared Infrastructure** and **Basic Type Mapping**. You will refactor existing code from `CycloneDDS.CodeGen` to share with the new tool.

### Required Reading (IN ORDER)
1.  **Workflow Guide:** `.dev-workstream/README.md`
2.  **Task Details:** `tools/CycloneDDS.IdlImporter/IDLImport-TASK-DETAILS.md` (Read Phase 1 sections)
3.  **Task Tracker:** `tools/CycloneDDS.IdlImporter/IDLImport-TASK-TRACKER.md`

### Source Code Location
-   **Tools Directory:** `tools/`
-   **New Tool:** `tools/CycloneDDS.IdlImporter/`
-   **Existing Tool:** `tools/CycloneDDS.CodeGen/`

### Report Submission
When done, submit your report to: `.dev-workstream/reports/BATCH-31-REPORT.md`

---

## Context

We are building a tool using `System.CommandLine` that wraps `idlc -l json` to import legacy IDL definitions into our C# DSL format.
The project skeleton exists, but the "Engine" is missing. We first need to share the IDL compilation and parsing logic that already exists in the CodeGen tool.

**Related Tasks:**
-   [IDLIMP-001](../tools/CycloneDDS.IdlImporter/IDLImport-TASK-DETAILS.md#idlimp-001-project-setup-and-shared-infrastructure) - Shared Infra
-   [IDLIMP-002](../tools/CycloneDDS.IdlImporter/IDLImport-TASK-DETAILS.md#idlimp-002-idlcrunner-enhancement-for-include-paths) - IdlcRunner
-   [IDLIMP-003](../tools/CycloneDDS.IdlImporter/IDLImport-TASK-DETAILS.md#idlimp-003-type-mapper-implementation) - Type Mapper

---

## 🎯 Batch Objectives

1.  Extract reusable logic (`IdlcRunner`, `IdlJsonParser`) from `CodeGen` into a new `Common` library.
2.  Enhance `IdlcRunner` to support include paths (`-I`).
3.  Implement the core `TypeMapper` logic to translate IDL types to C# types.

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1.  **Task 1:** Implement → Write tests → **ALL tests pass** ✅
2.  **Task 2:** Implement → Write tests → **ALL tests pass** ✅
3.  **Task 3:** Implement → Write tests → **ALL tests pass** ✅

**DO NOT** move to the next task until:
-   ✅ Current task implementation complete
-   ✅ Current task tests written
-   ✅ **ALL tests passing**

---

## ✅ Tasks

### Task 1: Shared Infrastructure (IDLIMP-001)

**Goal:** Create `CycloneDDS.Compiler.Common` and refactor CodeGen.

**Steps:**
1.  Create new Class Library project: `tools/CycloneDDS.Compiler.Common/CycloneDDS.Compiler.Common.csproj` (.NET 8).
2.  **Move** the following files from `tools/CycloneDDS.CodeGen/` to `tools/CycloneDDS.Compiler.Common/`:
    -   `IdlcRunner.cs`
    -   `IdlcResult.cs`
    -   `IdlJsonParser.cs`
    -   `IdlJson/` (folder and contents)
3.  Update namespaces in moved files to `CycloneDDS.Compiler.Common`.
4.  Adding `CycloneDDS.Compiler.Common` reference to:
    -   `tools/CycloneDDS.CodeGen/CycloneDDS.CodeGen.csproj`
    -   `tools/CycloneDDS.IdlImporter/CycloneDDS.IdlImporter.csproj`
5.  Fix compilation errors in `CodeGen` (update usings).
6.  Verify `IdlImporter` builds.

**Tests Required:**
-   Create `tools/CycloneDDS.Compiler.Common.Tests/` (xUnit).
-   Moves existing tests for `IdlcRunner`/`Parser` if any, or create basic instantiation tests.
-   **Run:** `dotnet test tools/CycloneDDS.Compiler.Common.Tests`

---

### Task 2: IdlcRunner Enhancement (IDLIMP-002)

**Goal:** Support `-I` include path in `IdlcRunner`.

**File:** `tools/CycloneDDS.Compiler.Common/IdlcRunner.cs`

**Requirements:**
1.  Update `RunIdlc` signature:
    ```csharp
    public IdlcResult RunIdlc(string idlFilePath, string outputDir, string? includePath = null)
    ```
2.  Append `-I "{includePath}"` to arguments if provided.
3.  Handle paths with spaces (quotes).
4.  Ensure backward compatibility with `CodeGen`.

**Reference:** [IDLIMP-002 Details](../tools/CycloneDDS.IdlImporter/IDLImport-TASK-DETAILS.md#idlimp-002-idlcrunner-enhancement-for-include-paths)

**Tests Required:**
-   `IdlcRunner_SupportsIncludePath`: Verify command string contains `-I`.
-   `IdlcRunner_HandlesPathsWithSpaces`: Verify quoting.

---

### Task 3: Type Mapper Implementation (IDLIMP-003)

**Goal:** Implement IDL → C# type mapping logic.

**File:** `tools/CycloneDDS.IdlImporter/TypeMapper.cs`

**Requirements:**
1.  Implement `MapPrimitive(string idlType)`.
    -   Map `long` → `int`, `unsigned long` → `uint`, `boolean` → `bool`, etc.
    -   See Design or Task Details for full table.
2.  Implement `MapMember`.
    -   Input: `JsonMember` (use `dynamic` or the `IdlJson` models from Common).
    -   Output: C# type, IsManaged flag, ArrayLen, Bound.
    -   Handle: Sequences (`List<T>`), Arrays (`T[]`), Bounded Strings.
3.  Implement `RequiresManagedAttribute`.

**Reference:** [IDLIMP-003 Details](../tools/CycloneDDS.IdlImporter/IDLImport-TASK-DETAILS.md#idlimp-003-type-mapper-implementation)

**Tests Required:**
-   Create `tools/CycloneDDS.IdlImporter.Tests/TypeMapperTests.cs`.
-   `MapPrimitive_CorrectlyMapsBasicTypes` (Table driven).
-   `MapMember_UnboundedSequence` (`List<T>`, managed).
-   `MapMember_FixedArray` (`T[]`, managed, len).

---

## 🧪 Testing Requirements

-   **New Projects:**
    -   `tools/CycloneDDS.Compiler.Common.Tests`
    -   `tools/CycloneDDS.IdlImporter.Tests`
-   **Coverage:**
    -   `TypeMapper`: 100% path coverage (all primitive cases).
    -   `IdlcRunner`: Verify command construction (mock process execution if possible, or just public API check).
-   **Quality:**
    -   Tests must verify the **mapping results**, not just that methods don't throw.

---

## 🎯 Success Criteria

This batch is DONE when:
1.  ✅ `CycloneDDS.Compiler.Common` exists and contains shared logic.
2.  ✅ `CycloneDDS.CodeGen` compiles and runs (regression check).
3.  ✅ `IdlcRunner` supports include paths.
4.  ✅ `TypeMapper` correctly maps all IDL primitives and collection types.
5.  ✅ All new tests pass.

---

## ⚠️ Common Pitfalls
-   **Namespace Issues:** Be careful when moving files. Ensure `CodeGen` usings are updated.
-   **Path Quoting:** Windows paths with spaces behave tricky in command arguments. Test thoroughly.
-   **Json Models:** Ensure strict typing is used if available (vs `dynamic`), but don't over-engineer if models involve extensive changes. Use the moved `IdlJson` models.
