# BATCH-34: IDL Importer - Full Feature Set & CLI

**Batch Number:** BATCH-34
**Tasks:** IDLIMP-011, IDLIMP-012, IDLIMP-013, IDLIMP-009, IDLIMP-010
**Phase:** Phase 4 (CLI) & Phase 5 (Advanced Features)
**Estimated Effort:** 10-14 hours
**Priority:** CAUTION (Large Batch)
**Dependencies:** BATCH-33 (Completed)

---

## 📋 Onboarding & Workflow

### Developer Instructions
**⚠️ IMPORTANT: This is a massive batch.**
We are combining Phase 5 (Advanced Features) with Phase 4 (CLI & Integration) to reach a "Feature Complete" state faster.
You must implement support for **Nested Types**, **Optional Members**, and **Member IDs** BEFORE building the CLI and running the final integration verification.

### Required Reading (IN ORDER)
1.  **Task Details:** `tools/CycloneDDS.IdlImporter/IDLImport-TASK-DETAILS.md`
    -   Read Phase 5: IDLIMP-011, IDLIMP-012, IDLIMP-013
    -   Read Phase 4: IDLIMP-009, IDLIMP-010
2.  **Design Doc:** `docs/IdlImport-design.md`

### Source Code Location
-   `tools/CycloneDDS.IdlImporter/CSharpEmitter.cs` (Update for new features)
-   `tools/CycloneDDS.IdlImporter/Program.cs` (Implement CLI)

### Report Submission
When done, submit your report to: `.dev-workstream/reports/BATCH-34-REPORT.md`

---

## 🎯 Batch Objectives

1.  **Advanced Types:** Support `MyStruct::NestedStruct`, `optional<T>`, and `@id` annotations.
2.  **CLI:** Implement the final command-line interface.
3.  **End-to-End:** Verify the tool against a complex, realistic IDL scenario (Compilable Code).

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

1.  **Task 1 (Advanced):** Implement Nested/Optional/IDs → Write tests → **ALL tests pass** ✅
2.  **Task 2 (CLI):** Implement CLI → Write tests → **ALL tests pass** ✅
3.  **Task 3 (Integration):** Run full integration suite → **ALL tests pass** ✅

---

## ✅ Tasks

### Task 1: Advanced Features (IDLIMP-011, 012, 013)

**Goal:** Complete the Type Support.

**File:** `tools/CycloneDDS.IdlImporter/CSharpEmitter.cs`

**Requirements:**
1.  **Nested Classes (IDLIMP-011):**
    -   If a struct is defined *inside* another module/struct context, ensure the C# namespace generation handles it.
    -   *Note:* IDL `struct A { struct B {}; };` -> C# `partial struct A` ??? No, C# structs can't nest recursively the same way. Usually `A` and `B` are just namespaces. Check `idlc` JSON output. If `B` is "A::B", map it to `namespace A { struct B }`.
2.  **Optional Members (IDLIMP-012):**
    -   Map `optional` fields to `T?` (nullable) in C# (for value types). Ref types (string) are already nullable.
    -   Attribute: `[DdsOptional]`.
3.  **Member IDs (IDLIMP-013):**
    -   Support `@id(N)` annotation from IDL.
    -   Attribute: `[DdsId(N)]`.
    -   Usually found in `mutable` structs.

**Tests:**
-   `CSharpEmitterTests.GeneratesOptionalMember` (`int?`, `[DdsOptional]`)
-   `CSharpEmitterTests.GeneratesMemberIds` (`[DdsId(42)]`)

---

### Task 2: CLI Implementation (IDLIMP-009)

**Goal:** Build the executable entry point.

**File:** `tools/CycloneDDS.IdlImporter/Program.cs`

**Requirements:**
1.  **Arguments:**
    -   `master-idl` (Required string)
    -   `--source-root` (Optional string, defaults to master-idl dir)
    -   `--output-root` (Optional string, defaults to current dir)
    -   `--idlc-path` (Optional string)
2.  **Logic:**
    -   Parse args with `System.CommandLine`.
    -   Call `Importer.Import`.
    -   Handle exceptions -> Exit Code 1.
    -   Success -> Exit Code 0.

---

### Task 3: End-to-End Integration (IDLIMP-010)

**Goal:** The Final Gate.

**Requirements:**
1.  Create `ImporterTests.Import_GeneratesCompilableCode`.
2.  **Input:** A complex structure with:
    -   Includes (nested folders)
    -   Circular dependency
    -   Unions
    -   Sequences/Arrays
    -   Optional members
    -   Keyed topics
3.  **Validation:**
    -   Run Importer.
    -   **Compile** the output using Roslyn (`Microsoft.CodeAnalysis.CSharp`).
    -   **Assert:** Compilation Success (0 Errors).

**Note:** You will need to add `CycloneDDS.Schema.dll` reference to the Roslyn compilation context in the test.

---

## 🧪 Testing Requirements

-   **Unit Tests:** Verify specific syntax for Optional/ID fields.
-   **Integration Test:** Must prove the generated code is valid C#.

---

## 🎯 Success Criteria

This batch is DONE when:
1.  ✅ All IDL features (Basic + Complex + Advanced) are supported.
2.  ✅ CLI is fully functional.
3.  ✅ Integration tests pass and prove valid C# generation.

---

## ⚠️ Common Pitfalls
-   **Nullable Value Types:** `optional long` must be `int?`. `optional string` is just `string`.
-   **Reference Assemblies:** In the Roslyn test, finding the path to `CycloneDDS.Schema.dll` can be tricky. Use `typeof(DdsStructAttribute).Assembly.Location`.
