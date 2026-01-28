# BATCH-32: IDL Importer - Core Engine & Basic Generation

**Batch Number:** BATCH-32
**Tasks:** IDLIMP-004, IDLIMP-005, **IDLIMP-006**
**Phase:** Phase 2 (Core Logic) + Start of Phase 3 (Generation)
**Estimated Effort:** 10-12 hours
**Priority:** HIGH
**Dependencies:** BATCH-31 (Completed)

---

## 📋 Onboarding & Workflow

### Developer Instructions
**⚠️ NOTE: This is an expanded batch.**
You have demonstrated strong velocity. This batch combines the **Core Recursive Engine** (Phase 2) with the **Basic Code Generation** (Phase 3).
By the end of this batch, the tool should not only crawl IDL files but also **generate valid C# code** for basic structs and enums.

### Required Reading (IN ORDER)
1.  **Task Details:** `tools/CycloneDDS.IdlImporter/IDLImport-TASK-DETAILS.md` (Read Phase 2 AND Phase 3/IDLIMP-006)
2.  **Design Doc:** `docs/IdlImport-design.md`

### Source Code Location
-   **Importer:** `tools/CycloneDDS.IdlImporter/Importer.cs`
-   **Emitter:** `tools/CycloneDDS.IdlImporter/CSharpEmitter.cs`

### Report Submission
When done, submit your report to: `.dev-workstream/reports/BATCH-32-REPORT.md`

---

## Context

We are effectively building the "Compiler" part of the tool.
1.  **Frontend (Importer):** Finds files, runs `idlc`, parses metadata.
2.  **Backend (Emitter):** Takes the parsed types and writes C# structs/enums using the `TypeMapper` you built in BATCH-31.

**Related Tasks:**
-   [IDLIMP-005 (Metadata)](../tools/CycloneDDS.IdlImporter/IDLImport-TASK-DETAILS.md#idlimp-005-json-parsing-and-file-metadata-extraction)
-   [IDLIMP-004 (Recursion)](../tools/CycloneDDS.IdlImporter/IDLImport-TASK-DETAILS.md#idlimp-004-importer-core---file-queue-and-recursion)
-   [IDLIMP-006 (Emitter)](../tools/CycloneDDS.IdlImporter/IDLImport-TASK-DETAILS.md#idlimp-006-csharpemitter---struct-and-enum-generation)

---

## 🎯 Batch Objectives

1.  **Crawler:** Implement recursive discovery of IDL files (Importer).
2.  **Parser:** Extract dependencies and type definitions from `idlc` JSON.
3.  **Generator:** Implement `CSharpEmitter` to output `struct` and `enum` definitions.
4.  **End-to-End:** Verify that running the Importer on a basic IDL produces a compiled C# file.

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1.  **Task 1 (Parser):** Extract dependencies → Tests Pass ✅
2.  **Task 2 (Crawler):** Implement recursion loop → Tests Pass ✅
3.  **Task 3 (Emitter):** Implement Struct/Enum generation → Tests Pass ✅

---

## ✅ Tasks

### Task 1: JSON Parsing & Metadata Extraction (IDLIMP-005)

**Goal:** Extract file lists, dependencies, and type definitions from JSON.

**File:** `tools/CycloneDDS.IdlImporter/Importer.cs` (Helpers) & `Compiler.Common` usage.

**Requirements:**
1.  Utilize `IdlcRunner` and `IdlJsonParser` (from BATCH-31).
2.  Implement logic to match `JsonFileMeta` entries to the processed file (handling relative/absolute path differences).
3.  Extract **Dependencies** (includes) to feed the crawler.
4.  Extract **Defined Types** (structs, modules, enums) to feed the Emitter.

**Tests:**
-   Verify correct dependency extraction from sample JSON.
-   Verify correct type filtration (only types defined in *this* file, not included ones).

---

### Task 2: Importer Core & Recursion (IDLIMP-004)

**Goal:** The recursive crawling loop.

**File:** `tools/CycloneDDS.IdlImporter/Importer.cs`

**Requirements:**
1.  Implement `Import(masterIdl)`.
2.  **Algorithm:**
    -   Queue = [MasterFile]
    -   While Queue > 0:
        -   File = Dequeue()
        -   Run `idlc -l json`
        -   Parse JSON
        -   **NEW:** Collect types from this file -> Store for Task 3.
        -   Find Dependencies -> Enqueue if new.
        -   Mirror Directory Structure (SourceRoot -> OutputRoot).
3.  Handle **Circular Includes** (A->B->A) by tracking processed canonical paths.

**Tests:**
-   Integration test using temp files: Verify a tree of IDLs is fully traversed.
-   Verify output directories are created.

---

### Task 3: Basic C# Emitter (IDLIMP-006)

**Goal:** Generate code for Structs and Enums.

**File:** `tools/CycloneDDS.IdlImporter/CSharpEmitter.cs`

**Requirements:**
1.  Implement `GenerateCSharp(List<JsonTypeDefinition> types, string filename)`.
2.  **Namespace Handling:** Map IDL modules (`Foo::Bar`) to Namespaces (`Foo.Bar`).
3.  **Structs:**
    -   `[DdsTopic]` (if it has keys) or `[DdsStruct]`.
    -   `public partial struct Name`.
    -   Members with attributes (using `TypeMapper` from BATCH-31).
4.  **Enums:**
    -   `public enum Name : int`.
    -   Members with values.
5.  **Attributes:**
    -   `[DdsKey]`, `[DdsExtensibility]`.
    -   **Ignore** `[DdsManaged]` for now (handled in next batch with Collections), unless simple Strings require it.

**Note:** **Do not implment** Unions or complex Sequences yet (that's BATCH-33). Focus on clean Struct/Enum generation.

**Tests:**
-   `CSharpEmitter_GeneratesSimpleStruct`: Verify syntax, namespace, and fields.
-   `CSharpEmitter_GeneratesEnum`: Verify enum values.
-   `CSharpEmitter_HandlesModuleNesting`: Verify `namespace A { namespace B { ... } }` or `namespace A.B`.

---

## 🧪 Testing Requirements

-   **Importer Tests:** Must verify recursion logic.
-   **Emitter Tests:** Must verify the **generated string content** contains valid C# syntax.
-   **End-to-End Test (Optional but recommended):**
    -   Take a simple `Point.idl`.
    -   Run Importer.
    -   Verify `Point.cs` exists and contains `struct Point`.

---

## 🎯 Success Criteria

This batch is DONE when:
1.  ✅ Importer correctly crawls dependencies.
2.  ✅ `CSharpEmitter` generates valid code for Structs and Enums.
3.  ✅ Running the tool on an IDL produces mirroring `.cs` files with actual content.

---

## ⚠️ Common Pitfalls
-   **Output Paths:** Ensure `C:/Src/Msg/A.idl` -> `C:/Out/Msg/A.cs`.
-   **Type Duplication:** Ensure you only generate code for types *defined* in the current file, not types imported from others (idlc JSON file metadata helps here).
-   **Namespace Collisions:** Validate that module names don't conflict with type names (C# limitation, though IDL allows it sometimes).
