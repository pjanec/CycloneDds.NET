# BATCH-35: IDL Importer - Rigorous Verification

**Batch Number:** BATCH-35
**Tasks:** IDLIMP-014, IDLIMP-015
**Phase:** Phase 6: Testing Infrastructure
**Estimated Effort:** 12-16 hours
**Priority:** CRITICAL
**Dependencies:** BATCH-34 (Completed)

---

## 📋 Onboarding & Workflow

### Developer Instructions
This is the final quality gate. The Importer generates code, but we must prove it generates **correct** code that complies with the OMG DDS-XTypes specification and the FastCycloneDDS binding requirements.

**⚠️ STRICT TDD WORKFLOW:**
You are required to **write the test cases FIRST** for all scenarios listed below.
1. Create `RoundtripTests.cs`.
2. Implement the test method stubs with `Assert.Fail("Not Implemented")`.
3. Implement the IDL-to-Assembly compilation harness.
4. Implement the Reflection Validator.
5. **Run tests** (All Fail).
6. **Implement validation logic** one by one until All Pass.

### Validation Strategy
We cannot easily link against C libraries in this C# test runner, so we will use **Reflection & Attribute Validation**.
- **Structural Integrity:** Verify that generated types have the correct properties, types, and nesting.
- **Attribute Validation:** Verify every single IDL annotation maps to the correct `[Dds*]` attribute.
- **Compilation:** The fact that it compiles is the first line of defense.

### Source Code Location
-   `tools/CycloneDDS.IdlImporter.Tests/RoundtripTests.cs`

### Report Submission
When done, submit your report to: `.dev-workstream/reports/BATCH-35-REPORT.md`

---

## ✅ Tasks

### Task 1: The Validation Harness

**Goal:** Create a reusable test harness that takes IDL string -> compiles C# -> returns `Type`.

**Requirements:**
1.  Input: `string idlContent`, `string mainTypeName`.
2.  Action:
    -   Run Importer to generate C#.
    -   Compile C# with Roslyn (referencing CycloneDDS.Schema, System.Runtime, etc.).
    -   Load Assembly.
    -   Return `Type`.
3.  **Fail loudly** if compilation fails (dump errors to console).

---

### Task 2: Mandatory Test Scenarios (IDLIMP-015)

You must implement a test for **EACH** of these scenarios.

#### 1. Basic Structs (Fixed Types)
-   **IDL:** struct with `char`, `long`, `char`, `double`.
-   **Verify:** All fields exist with correct C# types (byte, int, byte, double).
-   *Note:* C layout matching is NOT required as serialization handles it. Just ensure fields are present.

#### 2. Key Definition
-   **IDL:** `@key` on various fields (start, middle, end).
-   **Verify:** `[DdsKey]` present on exact fields.

#### 3. Complex Nesting
-   **IDL:** Module A -> Struct B -> Module C -> Struct D.
-   **Verify:** `A.B.C.D` type exists.

#### 4. Unions
-   **IDL:** Union with multiple cases, default case, boolean discriminator.
-   **Verify:** `[DdsUnion]`, `[DdsDiscriminator]`, `[DdsCase]`.
-   **Context:** The FastCycloneDDS binding uses a custom serializer that handles Unions by internally switching on the discriminator. Therefore, **Explicit Layout and FieldOffsets are NOT required**. The C# struct can be sequential. The test should verify attributes are present so the serializer knows how to behave.

#### 5. Arrays & Sequences
-   **IDL:** `long x[3][4]`, `sequence<long, 10>`.
-   **Verify:** `[ArrayLength]`, `[MaxLength]`, `[DdsManaged]`.

#### 6. Inheritance (If supported)
-   If `idlc` flattens inheritance, verify all fields are present.

#### 7. Optional Members
-   **IDL:** `optional long`, `optional string`.
-   **Verify:** Property type is Nullable (`long?`), `[DdsOptional]`.

---

### Task 3: Unit Test Coverage (IDLIMP-014)

**Goal:** 90% Coverage.

**Requirements:**
-   Edge cases: Empty IDL, Invalid Syntax (should throw), File Not Found.
-   CLI Tests: Verify `--help` works, verify logic for defaults.

---

## 🎯 Success Criteria

This batch is DONE when:
1.  ✅ `RoundtripTests.cs` exists and covers all 7 scenarios above.
2.  ✅ All tests pass.

---

## ⚠️ Note on Unions
The FastCycloneDDS binding creates serialization code that manually checks the discriminator and writes only the active field. Therefore, generated C# Unions do **not** need to use `[StructLayout(LayoutKind.Explicit)]` or `[FieldOffset]`. Standard sequential structs are valid. **Do not enforce explicit layout in your tests.**
