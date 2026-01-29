# BATCH-35: IDL Importer - Roundtrip Verification

**Batch Number:** BATCH-35
**Tasks:** IDLIMP-014, IDLIMP-015
**Phase:** Phase 6: Testing Infrastructure
**Estimated Effort:** 10-12 hours
**Priority:** HIGH
**Dependencies:** BATCH-34 (Completed)

---

## 📋 Onboarding & Workflow

### Developer Instructions
The Importer code is complete. Now we must prove it works correctly at **runtime** by performing "Roundtrip" tests.
You will create a test suite that:
1.  Uses `idlc` to compile IDL to C (Golden Standard).
2.  Uses `CycloneDDS.IdlImporter` to compile IDL to C# (Candidate).
3.  Serializes data in C# and deserializes in C (and vice-versa) to prove binary compatibility.

*Note: Since we might not have a full C environment set up easily in this test runner, we will focus on **Self-Managed Roundtrip** (C# -> Serialization -> Deserialization -> C#) and **structure validation** first. If possible, we will compare against pre-generated golden binaries if available, or rely on the fact that `CycloneDDS.Schema` attributes are already proven to work if applied correctly.*

**Refined Goal for this Environment:**
Since compiling C code on the fly might be complex, we will focus on **Property-Based Testing** using the generated C# classes:
1.  Generate C# classes from complex IDLs.
2.  Instantiate them, populate with random data.
3.  Serialize using `DdsWriter` (simulated or real).
4.  Deserialize using `DdsReader`.
5.  Assert `Input == Output`.

### Required Reading
1.  **Task Details:** `tools/CycloneDDS.IdlImporter/IDLImport-TASK-DETAILS.md` (Phase 6)

### Source Code Location
-   `tools/CycloneDDS.IdlImporter.Tests/RoundtripTests.cs` (New File)

### Report Submission
When done, submit your report to: `.dev-workstream/reports/BATCH-35-REPORT.md`

---

## 🎯 Batch Objectives

1.  **Coverage:** Ensure unit tests cover >90% of `IdlImporter`.
2.  **Runtime Validation:** Prove that the generated C# code actually works with the CycloneDDS serialization engine.

---

## 🔄 MANDATORY WORKFLOW

1.  **Task 1 (Coverage):** Add missing unit tests for `Importer.cs` edge cases (broken IDL, missing includes) → **All Pass**.
2.  **Task 2 (Roundtrip):** create `RoundtripTests.cs` → Generates code → Compiles it → Runs Serialization Roundtrip → **All Pass**.

---

## ✅ Tasks

### Task 1: Comprehensive Unit Tests (IDLIMP-014)

**Goal:** shore up any missing coverage.

**Requirements:**
1.  Test `Importer` with invalid IDL (syntax error). Should fail gracefully.
2.  Test `Importer` with specific `idlc` flags if any.
3.  Test `TypeMapper` with obscure IDL types (e.g., `long double` -> `double`, `octet` -> `byte`).

### Task 2: Roundtrip Validation (IDLIMP-015)

**Goal:** Verify Runtime Serialization.

**Requirements:**
1.  Create `RoundtripTests.cs`.
2.  **Test 1: Basic Roundtrip**
    -   Define simple IDL.
    -   Run Importer -> `Basic.cs`.
    -   Compile `Basic.cs` assembly.
    -   Reflection-load `Basic` type.
    -   Create instance, set fields.
    -   (Optional) If you can invoke `CycloneDDS.Serialization` directly, do so. If not, verifying the attributes (which we did in Batch 34) + Compilation (Batch 34) is extremely strong.
    
    *Correction:* Actually, validating that we can *serialize* it requires the full DDS stack. If that's too heavy, focus on **Reflection Validation**:
    -   Load generated assembly.
    -   Verify standard DDS attributes exist on correct properties.
    -   Verify `sizeof` struct (using `Marshal.SizeOf` or `Unsafe.SizeOf`) matches expectation for `final` types.

    **Let's stick to "Reflection Attribute Validator"**:
    -   The most common failure mode is missing an attribute (e.g. `[Key]`).
    -   Write a test that iterates over the generated Type properties and asserts:
        -   If IDL had `@key`, C# has `[DdsKey]`.
        -   If IDL had `sequence`, C# has `List<T>` + `[DdsManaged]`.

---

## 🧪 Testing Requirements

-   **High Coverage:** We want to be confident refactoring won't break basic logic.

---

## 🎯 Success Criteria

This batch is DONE when:
1.  ✅ Reviewer confirms >90% test coverage estimate.
2.  ✅ `RoundtripTests` verifies that generated types effectively match the Schema expectations (Attributes, Types).

---
