# BATCH-27: Roundtrip Arrays (Completion)

**Batch Number:** BATCH-27
**Tasks:** RT-A03 (Fix), RT-A04, RT-A05, RT-A06
**Phase:** Phase 3 (Arrays - Completion)
**Estimated Effort:** 8-12 hours
**Priority:** HIGH
**Dependencies:** BATCH-26

---

## 📋 Onboarding & Workflow

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome to the team! You are picking up a high-priority task to complete the Array support for our C# <-> C Roundtrip tests. This work was started in BATCH-26 but needs completion and fixes.

**Goal:** Ensure our C# bindings can correctly serialize/deserialize arrays (1D, Multi-dim, Struct arrays) to match Native C layout exactly.

### Required Reading (IN ORDER)
1.  **Workflow Guide:** `.dev-workstream/README.md` - **READ THIS FIRST** to understand how we work.
2.  **Previous Review:** `.dev-workstream/reviews/BATCH-26-REVIEW.md` - Understand why the previous batch was stopped.
3.  **Task Tracker:** `tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-TASK-TRACKER.md` - See "Phase 3: Arrays".
4.  **Implementation Guide:** `tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-IMPLEMENTATION-GUIDE.md` - Technical details on mapping C# arrays to C.

### Source Code Location
-   **Primary Work Area:** `tests/CsharpToC.Roundtrip.Tests/`
-   **IDL Definitions:** `tests/CsharpToC.Roundtrip.Tests/idl/atomic_tests.idl`
-   **Native Code:** `tests/CsharpToC.Roundtrip.Tests/Native/` (You may need to check `handler_arrays.c`)

### Report Submission
**When done, submit your report to:**
`.dev-workstream/reports/BATCH-27-REPORT.md`

**If you have questions, create:**
`.dev-workstream/questions/BATCH-27-QUESTIONS.md`

---

## 🎯 Batch Objectives
1.  **Fix `ArrayStringTopic`:** Resolve the `normalize_string: bound check failed` error.
2.  **Implement Complex Arrays:** Complete the implementation of 2D, 3D, and Struct arrays (both Final and Appendable).

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1.  **Task 1 (Fix String Arrays):** Debug & Fix → **ALL tests pass** ✅
2.  **Task 2 (Update IDL):** Add missing Appendable types → Compile ✅
3.  **Task 3 (Complex Arrays):** Implement tests → **ALL tests pass** ✅

---

## ✅ Tasks

### Task 1: Fix ArrayStringTopic (RT-A03)

**Status:** Currently FAILING in BATCH-26.
**Error:** `[native] normalize_string: bound check failed`

**Requirements:**
1.  **Investigate Failure:** The native side seems to reject the serialized string array. Check if the C# serializer is writing the correct padding/alignment for an array of bounded strings.
    -   *Hint:* `string<16>` in an array might need to be treated as a fixed-size block of 17 bytes (16 chars + null) or similar, depending on mapping. Verify `SerializerEmitter.cs` logic for `[ArrayLength]` combined with strings.
2.  **Align IDL:** The instructions requested `string[5]`, but IDL has `names[3]`. Update IDL to `string<16> names[5]` to match the original spec, or update the test code to match the IDL. **Ensure consistency.**
3.  **Verify:** `TestArrayString` and `TestArrayStringAppendable` must PASS.

### Task 2: Update IDL for Complex Arrays

**Status:** Missing Appendable variants.

**Requirements:**
1.  **Edit `idl/atomic_tests.idl`:**
    -   Add `@appendable` variants for:
        -   `Array2DInt32Topic` -> `Array2DInt32TopicAppendable`
        -   `Array3DInt32Topic` -> `Array3DInt32TopicAppendable`
        -   `ArrayStructTopic` -> `ArrayStructTopicAppendable`
2.  **Rebuild:** Ensure `CycloneDDS.CodeGen` generates the correct C# types for these new topics.

### Task 3: Implement Complex Arrays (RT-A04, RT-A05, RT-A06)

**Status:** Skipped in BATCH-26.

**Tasks Covered:**
-   **RT-A04:** `Array2DInt32Topic` (and Appendable)
-   **RT-A05:** `Array3DInt32Topic` (and Appendable)
-   **RT-A06:** `ArrayStructTopic` (and Appendable)

**Requirements:**
1.  **Update `Program.cs`:**
    -   Implement `TestArray2DInt32`, `TestArray3DInt32`, `TestArrayStruct`.
    -   Implement `TestArray2DInt32Appendable`, `TestArray3DInt32Appendable`, `TestArrayStructAppendable`.
2.  **Native Handling:**
    -   Ensure the Native side (C code) correctly handles multi-dimensional arrays and struct arrays.
    -   *Note:* You might need to update `handler_arrays.c` (or similar) in the Native project if it doesn't support these types yet.
3.  **Validation:**
    -   Verify correct flattening (if applicable) or jagged array handling.
    -   Verify struct alignment within arrays.

---

## 🧪 Testing Requirements

**Total New Tests:**
-   3 Topics * 2 Variants = **6 New Test Pairs** (plus fixing the existing String Array tests).

**Success Standard:**
-   **Native -> C#:** Data matches.
-   **CDR Verify:** Byte-for-byte match.
-   **C# -> Native:** Data matches.

---

## 📊 Report Requirements

**Focus on:**
1.  **String Array Fix:** What was the root cause of the `normalize_string` error? How did you fix it?
2.  **Multi-dim Arrays:** How does C# `[ArrayLength(3,4)]` map to the generated code? Did you have to handle row-major vs column-major?
3.  **Struct Arrays:** Any padding surprises?

---

## 🎯 Success Criteria

This batch is DONE when:
-   [ ] `ArrayStringTopic` (Final & Appendable) PASSES.
-   [ ] IDL updated with Appendable Complex Arrays.
-   [ ] `Array2DInt32Topic` (Final & Appendable) PASSES.
-   [ ] `Array3DInt32Topic` (Final & Appendable) PASSES.
-   [ ] `ArrayStructTopic` (Final & Appendable) PASSES.
-   [ ] Report submitted.
