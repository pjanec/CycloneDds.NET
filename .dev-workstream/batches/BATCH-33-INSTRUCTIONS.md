# BATCH-33: IDL Importer - Complexes (Collections & Unions)

**Batch Number:** BATCH-33
**Tasks:** IDLIMP-007, IDLIMP-008
**Phase:** Phase 3: C# Code Generation (Continued)
**Estimated Effort:** 8-10 hours
**Priority:** HIGH
**Dependencies:** BATCH-32 (Completed)

---

## 📋 Onboarding & Workflow

### Developer Instructions
Now that we have a working crawler and basic struct emitter, we must tackle the **complex types**: Collections (Sequences/Arrays) and Unions.
This involves significant updates to the `CSharpEmitter` and `TypeMapper`.

### Required Reading (IN ORDER)
1.  **Task Details:** `tools/CycloneDDS.IdlImporter/IDLImport-TASK-DETAILS.md` (Read IDLIMP-007 and IDLIMP-008)
2.  **Existing Code:** Review `CSharpEmitter.cs` to see where to plug in the new logic.

### Source Code Location
-   `tools/CycloneDDS.IdlImporter/CSharpEmitter.cs`
-   `tools/CycloneDDS.IdlImporter/TypeMapper.cs`

### Report Submission
When done, submit your report to: `.dev-workstream/reports/BATCH-33-REPORT.md`

---

## Context

-   **Sequences/Arrays:** Need to map to `List<T>` / `T[]` and require `[DdsManaged]` attributes, plus `[MaxLength]` or `[ArrayLength]`.
-   **Unions:** Are mapped to structs but with a `_d` discriminator field and specific `[DdsUnion]`, `[DdsDiscriminator]`, `[DdsCase]` attributes.

**Related Tasks:**
-   [IDLIMP-007 (Collections)](../tools/CycloneDDS.IdlImporter/IDLImport-TASK-DETAILS.md#idlimp-007-csharpemitter---collection-type-support)
-   [IDLIMP-008 (Unions)](../tools/CycloneDDS.IdlImporter/IDLImport-TASK-DETAILS.md#idlimp-008-csharpemitter---union-type-support)

---

## 🎯 Batch Objectives

1.  **Map Collections:** Update logic to handle sequences, arrays, and bounded strings correctly.
2.  **Emit Collections:** Generate `List<T>` / `T[]` fields with correct attributes.
3.  **Emit Unions:** Generate C# structs for IDL Unions with discriminator logic.

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

1.  **Task 1 (Collections):** Implement → Write tests → **ALL tests pass** ✅
2.  **Task 2 (Unions):** Implement → Write tests → **ALL tests pass** ✅

---

## ✅ Tasks

### Task 1: Collection Type Support (IDLIMP-007)

**Goal:** Handle sequences, arrays, bounded strings.

**File:** `tools/CycloneDDS.IdlImporter/CSharpEmitter.cs`

**Requirements:**
1.  **Update EmitStructMember:**
    -   Use `TypeMapper` (already partially supports collections from BATCH-31, verify it).
    -   **Attributes to Emit:**
        -   `[DdsManaged]`: For all Lists, Arrays, and Strings.
        -   `[MaxLength(N)]`: For Bounded Sequences and Bounded Strings.
        -   `[ArrayLength(N)]`: For Fixed Arrays.
        -   `[DdsKey]`: If member is a key.
2.  **Nested Types:** Ensure `sequence<MyModule::MyType>` generates `List<MyModule.MyType>`.

**Tests Required:**
-   `CSharpEmitterTests`:
    -   `GeneratesUnboundedSequence` (`List<int>`)
    -   `GeneratesBoundedSequence` (`[MaxLength(10)] List<int>`)
    -   `GeneratesFixedArray` (`[ArrayLength(5)] int[]`)
    -   `GeneratesBoundedString` (`[DdsString(32)] string`) - note: existing code used `DdsString`, instructions say `MaxLength`. **Use `DdsString` for strings** and `MaxLength` for sequences to match existing bindings conventions if that's what `CycloneDDS.Schema` expects. *Correction:* Use `DdsString` for strings, `MaxLength` for Sequences.

---

### Task 2: Union Type Support (IDLIMP-008)

**Goal:** Handle unions.

**File:** `tools/CycloneDDS.IdlImporter/CSharpEmitter.cs`

**Requirements:**
1.  **Implement EmitUnion:**
    -   If `type.Kind == "union"`.
    -   Attribute: `[DdsUnion]`.
    -   Structure: `public partial struct Name`.
2.  **Discriminator:**
    -   First member is always discriminator (`_d`).
    -   Attribute: `[DdsDiscriminator]`.
    -   Type: Mapped from `type.Discriminator`.
3.  **Case Members:**
    -   Attribute: `[DdsCase(value)]`.
    -   Handle `default` label: `[DdsDefaultCase]`.
    -   Handle multiple labels for one field (multiple attributes).

**Tests Required:**
-   `CSharpEmitterTests`:
    -   `GeneratesUnion`: Verify `[DdsUnion]`, `[DdsDiscriminator]`, `[DdsCase]`.
    -   `GeneratesUnionWithDefault`: Verify `[DdsDefaultCase]`.

---

## 🧪 Testing Requirements

-   **Existing Tests must pass.**
-   **New Tests:** Add specific test cases to `CSharpEmitterTests.cs`.
-   **Code Quality:** Ensure generated code compiles (visually check syntax in tests).

---

## 🎯 Success Criteria

This batch is DONE when:
1.  ✅ `CSharpEmitter` handles all collection types defined in IDLIMP-007.
2.  ✅ `CSharpEmitter` handles unions defined in IDLIMP-008.
3.  ✅ Tests verify the correct attributes are emitted.

---

## ⚠️ Common Pitfalls
-   **Attribute Order:** Access modifiers (`public`) must come before type. Attributes must be above the field.
-   **Managed Flag:** Don't forget `[DdsManaged]` on Lists/Arrays/Strings!
-   **PascalCase:** Apply `ToPascalCase` to Union members too.
