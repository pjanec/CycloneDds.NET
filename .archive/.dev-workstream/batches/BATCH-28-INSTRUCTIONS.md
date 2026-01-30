# BATCH-28: Arrays Completion & Appendable Fixes

**Batch Number:** BATCH-28
**Tasks:** RT-EXT-01 (Explicit Final), RT-A01 (Fix), RT-A02, RT-A03 (Appendable), RT-A04, RT-A05, RT-A06, RT-P12 (Fix)
**Phase:** Phase 3 (Arrays) & Phase 8 (Extensibility Fixes)
**Estimated Effort:** 12-16 hours
**Priority:** HIGH
**Dependencies:** BATCH-27 (Failed)

---

## 📋 Onboarding & Workflow (New Developer)

Welcome! You are picking up a critical batch to complete the Array support and fix regression issues in our Roundtrip Test Suite.

### 1. Required Reading (IN ORDER)
1.  **Workflow Guide:** `.dev-workstream/DEV-LEAD-GUIDE.md` - **READ THIS FIRST** to understand our "Agentic" workflow.
2.  **Implementation Guide:** `tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-IMPLEMENTATION-GUIDE.md` - Technical details on mapping C# types to C.
3.  **Task Tracker:** `tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-TASK-TRACKER.md` - Current project status.

### 2. Source Code Location
-   **C# Tests:** `tests/CsharpToC.Roundtrip.Tests/Program.cs`
-   **Native Handlers:** `tests/CsharpToC.Roundtrip.Tests/Native/atomic_tests_native.c`
-   **IDL Definitions:** `tests/CsharpToC.Roundtrip.Tests/idl/atomic_tests.idl`
-   **Code Generator:** `tools/CycloneDDS.CodeGen/` (You may need to debug/modify this)

### 3. Reporting
-   **Create Questions:** `.dev-workstream/questions/BATCH-28-QUESTIONS.md` (if needed)
-   **Submit Report:** `.dev-workstream/reports/BATCH-28-REPORT.md`

---

## 🎯 Batch Objectives

The previous batch (BATCH-27) failed to complete the Array implementation and introduced a crash. Your goal is to stabilize the suite and finish the missing array types.

### 🛑 Critical Issues to Fix First
1.  **CRASH in `ArrayInt32TopicAppendable`:** The test runner crashes (Exit Code 1) when running this test.
2.  **CDR Mismatch in `StringBounded32TopicAppendable`:** The "CDR Verify" step fails with a header mismatch (`03` vs `00`).
3.  **Missing Tests:** `ArrayInt32Topic` and `ArrayFloat64Topic` (Final) are commented out in `Program.cs`.

---

## 🔄 MANDATORY WORKFLOW

**You MUST complete these tasks in order. Do NOT proceed if a step fails.**

### Task 0: Explicit Extensibility (New Requirement)
**Goal:** Eliminate ambiguity by explicitly marking all "default" topics as `@final` / `Final`.

1.  **Update IDL (`atomic_tests.idl`):**
    *   Add the `@final` annotation to **ALL** topics in Sections 1 through 7 (Primitives, Enums, Nested, Unions, Optional, Sequences, Arrays).
    *   *Example:*
        ```idl
        @final
        @topic
        struct BooleanTopic { ... };
        ```
    *   **Do NOT** change topics that are already `@appendable` or `@mutable` (Section 8 and Appendable Duplicates).

2.  **Update C# (`AtomicTestsTypes.cs`):**
    *   Add `[DdsExtensibility(DdsExtensibilityKind.Final)]` to all corresponding structs that match the IDL changes.
    *   *Example:*
        ```csharp
        [DdsTopic("BooleanTopic")]
        [DdsExtensibility(DdsExtensibilityKind.Final)]
        public partial struct BooleanTopic { ... }
        ```

3.  **Verify:**
    *   Re-run a simple test (e.g., `TestBoolean`) to ensure the explicit attribute doesn't break the existing serialization logic.

### Task 1: Stabilize Existing Arrays (RT-A01, RT-A02)
1.  **Uncomment Tests:** In `Program.cs`, uncomment `await TestArrayInt32();` and `await TestArrayFloat64();`.
2.  **Verify Final Variants:** Run the tests. Ensure `ArrayInt32Topic` and `ArrayFloat64Topic` (Final) PASS.
3.  **Debug Crash:** Run `await TestArrayInt32Appendable();`. It will crash.
    *   *Investigation Hint:* Check `atomic_tests_native.c`. Ensure the `generate_` and `validate_` functions for `ArrayInt32TopicAppendable` are correct and safe.
    *   *Investigation Hint:* Check `SerializerEmitter.cs` for `[ArrayLength]` handling in Appendable mode. Does it write the correct XCDR2 headers?
4.  **Fix Crash:** Apply fix. Verify `ArrayInt32TopicAppendable` PASSES.
5.  **Verify Others:** Uncomment and verify `TestArrayFloat64Appendable` and `TestArrayStringAppendable`.

### Task 2: Fix String Appendable Verification (RT-P12)
1.  **Reproduce:** Run `await TestStringBounded32Appendable();`.
2.  **Analyze Failure:** `[CDR Verify] FAILED: Byte mismatch at index 3: Received 03, Serialized 00`.
    *   *Context:* Index 3 in XCDR2 usually relates to the encoding format or padding flags.
    *   *Hypothesis:* Native side might be emitting a specific flag for "Delimiter Header" or "Member Header" that C# is missing, or vice versa.
    *   *Action:* Adjust `SerializerHelper.cs` or `Program.cs` padding logic, OR update `SerializerEmitter.cs` to match Native behavior if C# is incorrect.
3.  **Fix:** Ensure the test PASSES with "Byte-for-Byte match".

### Task 3: Implement Complex Arrays (RT-A04, RT-A05, RT-A06)
**These were skipped in BATCH-27. You must implement them.**

1.  **IDL:** Verify `atomic_tests.idl` has `Array2DInt32Topic`, `Array3DInt32Topic`, `ArrayStructTopic` (and Appendable variants).
2.  **Native (`atomic_tests_native.c`):**
    *   Implement `generate_` and `validate_` functions for all 6 new topics (3 Final, 3 Appendable).
    *   *Note:* C multidimensional arrays (`long matrix[3][4]`) are contiguous.
3.  **Registry (`test_registry.c`):** Register the new handlers.
4.  **C# (`Program.cs`):**
    *   Implement test methods: `TestArray2DInt32`, `TestArray3DInt32`, `TestArrayStruct` (and Appendable).
    *   *Note:* C# `[ArrayLength]` maps to 1D array in C# (`int[]`) but flattened. You may need to map `int[3,4]` (multidimensional) or `int[]` (flattened) depending on what the CodeGen produces. **Check the generated code!**
5.  **Verify:** All new tests must PASS.

---

## 🧪 Testing Requirements

**Success Standard:**
1.  **Native -> C#:** Data matches.
2.  **CDR Verify:** Byte-for-byte match (CRITICAL).
3.  **C# -> Native:** Data matches.

**Total Expected Passing Tests:**
-   All Primitives (Phase 1)
-   All Arrays (Phase 3) - 1D, 2D, 3D, Struct (Final & Appendable)
-   All Sequences (Phase 6 - `SequenceInt32` only so far)
-   All Unions (Phase 5 - `UnionLongDisc` only so far)

---

## 📊 Report Requirements

In `BATCH-28-REPORT.md`, please explain:
1.  **Crash Root Cause:** Why did `ArrayInt32TopicAppendable` crash?
2.  **String Header Fix:** What did `03` vs `00` mean? How did you fix it?
3.  **Complex Array Mapping:** How does C# CodeGen handle `long matrix[3][4]`? Is it `int[,]` or `int[]`?

**Good luck!**
