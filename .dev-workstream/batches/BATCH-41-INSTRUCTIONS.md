# BATCH-31: Implement CsharpToC.Symmetry Test Suite

**Batch Number:** BATCH-41
**Tasks:** TASK-SYM-01, TASK-SYM-02, TASK-SYM-03, TASK-SYM-04
**Phase:** Testing Infrastructure
**Estimated Effort:** 8-12 hours
**Priority:** HIGH
**Prerequisites:** Existing `CsharpToC.Roundtrip` project must be built.

---

## 📋 Onboarding & Workflow

### Developer Instructions
You are tasked with populating the new **CsharpToC.Symmetry** test framework with test cases. The framework infrastructure is in place; your job is to port the test definitions (Topic Names + Seeds) from the existing Roundtrip tests into this new high-velocity framework.

### Required Reading (IN ORDER)
1.  **Onboarding Guide:** `tests/CsharpToC.Symmetry/ONBOARDING.md` (Read this first!)
2.  **Overview:** `tests/CsharpToC.Symmetry/README.md`
3.  **Roundtrip Reference:** `tests/CsharpToC.Roundtrip.Tests/README.md` (or source code)

### Source Code Location
-   **Work Area:** `tests/CsharpToC.Symmetry/Tests/`
-   **Reference Area:** `tests/CsharpToC.Roundtrip.Tests/`

### Report Submission
**When done, submit your report to:** `.dev-workstream/reports/BATCH-31-REPORT.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1.  **Setup:** Initialize IDL and DLL resources.
2.  **Part 1:** Implement → Write tests → **ALL tests pass** ✅
3.  **Part 2:** Implement → Write tests → **ALL tests pass** ✅
4.  **Part 3:** Implement → Write tests → **ALL tests pass** ✅
5.  **Part 4:** Implement → Write tests → **ALL tests pass** ✅

**DO NOT** move to the next task until the current one is fully passing.

---

## 🎯 Batch Objectives

The goal is to achieve **parity** with the `CsharpToC.Roundtrip` test suite. For every test in Roundtrip, there should be a corresponding "Symmetry" test that verifies the serialization byte-for-byte against golden data.

**Why?** Roundtrip tests take 60s+ to run. Symmetry tests take 2s. We need this speed for development.

---

## ✅ Tasks

### Task 0: Environment Setup (TASK-SYM-00)

**Description:** Ensure the Symmetry project has the necessary dependencies from the Roundtrip project.

**Actions:**
1.  **Copy IDL:** Copy `atomic_tests.idl` from `tests/CsharpToC.Roundtrip.Tests/idl/` to `tests/CsharpToC.Symmetry/`.
2.  **Copy DLL:** Copy `ddsc_test_lib.dll` (or .so on Linux) from `tests/CsharpToC.Roundtrip.Tests/` (or its build output `bin/Debug/net8.0/`) to `tests/CsharpToC.Symmetry/Native/`.
3.  **Verify Build:** Run `.\rebuild_and_test.ps1`. It should build successfully (tests might pass or fail depending on placeholders).

---

### Task 1: Primitive Tests (TASK-SYM-01)

**File:** `tests/CsharpToC.Symmetry/Tests/Part1_PrimitiveTests.cs`
**Reference:** `tests/CsharpToC.Roundtrip.Tests/BasicPrimitivesTests.cs` & `BasicPrimitivesAppendableTests.cs`

**Description:**
Implement symmetry tests for all "Primitive" topics.

**Requirements:**
1.  Open `BasicPrimitivesTests.cs` in the reference project.
2.  For **EVERY** `[Fact]` test method there:
    *   Identify the `TopicName` (e.g., "AtomicTests::CharTopic") and `Seed` (e.g., 1420).
    *   Create a corresponding test in `Part1_PrimitiveTests.cs`.
    *   Use the `VerifySymmetry<T>` pattern.
3.  **Pattern:**
    ```csharp
    [Fact]
    public void TestCharTopic()
    {
        VerifySymmetry<CharTopic>(
            "AtomicTests::CharTopic",
            seed: 1420,
            deserializer: reader => CharTopic.Deserialize(ref reader),
            serializer: (obj, writer) => obj.Serialize(ref writer));
    }
    ```
4.  Include both XCDR1 (Final) and XCDR2 (Appendable) variants if present in Roundtrip.

**Success Criteria:**
*   All primitive tests from Roundtrip are ported.
*   `.\run_tests_only.ps1 -Filter "Part1"` passes green for all tests.

---

### Task 2: Collection Tests (TASK-SYM-02)

**File:** `tests/CsharpToC.Symmetry/Tests/Part2_CollectionTests.cs`
**Reference:** `tests/CsharpToC.Roundtrip.Tests/ArrayTests.cs` & `SequenceTests.cs`

**Description:**
Port all Array and Sequence tests.

**Requirements:**
*   Cover Fixed Arrays (Primitives, Strings).
*   Cover Sequences (Bounded, Unbounded).
*   Cover Multidimensional Arrays.
*   Ensure `@appendable` variants are included if they exist in Roundtrip.

**Success Criteria:**
*   `.\run_tests_only.ps1 -Filter "Part2"` passes green.

---

### Task 3: Complex Type Tests (TASK-SYM-03)

**File:** `tests/CsharpToC.Symmetry/Tests/Part3_ComplexTests.cs` (Create if missing)
**Reference:** `tests/CsharpToC.Roundtrip.Tests/UnionTests.cs`, `NestedStructTests.cs`, `NestedKeyTests.cs`

**Description:**
Port tests for Unions, Nested Structures, and Keyed topics.

**Requirements:**
*   Implement tests for all Union discriminators.
*   Implement tests for deeply nested structures.
*   Implement tests for topics with `@key` fields.

**Success Criteria:**
*   `.\run_tests_only.ps1 -Filter "Part3"` passes green.

---

### Task 4: XTypes & Extensibility Tests (TASK-SYM-04)

**File:** `tests/CsharpToC.Symmetry/Tests/Part4_XTypesTests.cs` (Create if missing)
**Reference:** `tests/CsharpToC.Roundtrip.Tests/` (Look for Mutable/Appendable specific logic not covered in primitives)

**Description:**
Port tests specifically focused on XTypes features like `@mutable` (if any), inheritance, or mixed extensibility.

**Requirements:**
*   If Roundtrip has specific XTypes tests, port them.
*   If not, ensure `Part1` and `Part2` covered the `@appendable` cases sufficiently.
*   Add comments indicating if specific coverage is missing in Roundtrip.

**Success Criteria:**
*   `.\run_tests_only.ps1 -Filter "Part4"` passes green (or is empty if no tests exist).

---

## 🧪 Testing Requirements

*   **Total Test Count:** Expected 100+ tests (matching Roundtrip).
*   **Golden Data:** You must run `.\generate_golden_data.ps1` as needed to create the initial .txt files.
*   **Verification:**
    *   Run `.\rebuild_and_test.ps1` to verify the full suite.
    *   Ensure **NO** tests are skipped or commented out.

---

## 🎯 Success Criteria

This batch is DONE when:
*   [ ] `atomic_tests.idl` matches Roundtrip.
*   [ ] All 4 Test Parts are implemented.
*   [ ] `dotnet test` passes with 100% success rate.
*   [ ] Golden Data files exist for all topics.
*   [ ] Report answers the questions below.

---

## 📊 Report Requirements

**Include in your report:**

**Q1:** How many tests were ported in total?
**Q2:** Did you find any discrepancies between Roundtrip implementation and Symmetry implementation?
**Q3:** Were there any topics in Roundtrip that could not be ported? Why?
**Q4:** What is the execution time of the full Symmetry suite vs the Roundtrip suite (approx)?
