# BATCH-26: Phase 2 & 3 - Enums and Arrays

**Batch Number:** BATCH-26  
**Tasks:** RT-E01, RT-E02, RT-A01, RT-A02, RT-A03, RT-A04, RT-A05, RT-A06  
**Phase:** Phase 2 (Enums) & Phase 3 (Arrays)  
**Estimated Effort:** 12-16 hours  
**Priority:** HIGH  
**Dependencies:** BATCH-25

---

## 📋 Onboarding & Workflow

### Developer Instructions
This batch moves beyond primitives to structured types: Enumerations and Arrays. You will implement full roundtrip support for enums and various array configurations (1D, multi-dimensional, arrays of strings/structs).

### Required Reading (IN ORDER)
1.  **Workflow Guide:** `.dev-workstream/README.md`
2.  **Task Tracker:** `tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-TASK-TRACKER.md` - See Phase 2 & 3 details
3.  **Implementation Guide:** `tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-IMPLEMENTATION-GUIDE.md` - **CRITICAL:** See "Enumerations" and "Arrays" sections.
4.  **Previous Review:** `.dev-workstream/reviews/BATCH-25-REVIEW.md`

### Source Code Location
-   **Primary Work Area:** `tests/CsharpToC.Roundtrip.Tests/`
-   **IDL Definitions:** `tests/CsharpToC.Roundtrip.Tests/idl/atomic_tests.idl`
-   **Native Code:** `tests/CsharpToC.Roundtrip.Tests/Native/`
-   **C# Types:** `tests/CsharpToC.Roundtrip.Tests/AtomicTestsTypes.cs`
-   **Test Orchestrator:** `tests/CsharpToC.Roundtrip.Tests/Program.cs`

---

## 🎯 Batch Objectives
Complete all tasks in **Phase 2: Enumerations** and **Phase 3: Arrays**. Ensure robust handling of fixed-size arrays and multi-dimensional arrays in both C# and Native C.

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1.  **Task 1 (Enums):** Implement → Write tests → **ALL tests pass** ✅
2.  **Task 2 (Basic Arrays):** Implement → Write tests → **ALL tests pass** ✅
3.  **Task 3 (Complex Arrays):** Implement → Write tests → **ALL tests pass** ✅

---

## ✅ Tasks

### Task 1: Enumerations (RT-E01, RT-E02)

**Tasks Covered:**
-   **RT-E01:** EnumTopic (Simple enum)
-   **RT-E02:** ColorEnumTopic (Enum with explicit values if applicable, or just another enum variant)

**Requirements:**
-   Define C# `enum` types.
-   Map to Native `enum` types.
-   Implement **BOTH** `Final` and `Appendable` variants.
-   **Validation:** Verify integer value preservation.

**Design Reference:**
-   [Enumerations Guide](../tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-IMPLEMENTATION-GUIDE.md#55-enumerations)

---

### Task 2: Basic Arrays (RT-A01, RT-A02, RT-A03)

**Tasks Covered:**
-   **RT-A01:** ArrayInt32Topic **[FIX EXISTING]**
    -   *Note:* This topic is already partially defined but skipped in `Program.cs`. Uncomment and fix it.
-   **RT-A02:** ArrayFloat64Topic (`double[5]`)
-   **RT-A03:** ArrayStringTopic (`string[5]`)

**Requirements:**
-   **C#:** Use `[ArrayLength(N)]` attribute on array fields.
-   **Native:** Use fixed-size arrays in structs (e.g., `int32_t values[5]`).
-   **Strings:** Array of strings requires careful memory management in Native (array of pointers).
-   Implement **BOTH** `Final` and `Appendable` variants.

**Design Reference:**
-   [Arrays Guide](../tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-IMPLEMENTATION-GUIDE.md#54-arrays)

---

### Task 3: Complex Arrays (RT-A04, RT-A05, RT-A06)

**Tasks Covered:**
-   **RT-A04:** Array2DInt32Topic (`long[3][4]`)
-   **RT-A05:** Array3DInt32Topic (`long[2][3][4]`)
-   **RT-A06:** ArrayStructTopic (`Point2D[5]`)

**Requirements:**
-   **Multi-dimensional Arrays:**
    -   IDL: `long values[3][4];`
    -   C#: Flattened array `int[]` with `[ArrayLength(3, 4)]`? Or jagged arrays?
    -   *Check Implementation Guide:* If C# bindings flatten multi-dim arrays (common in DDS), map `[3][4]` to `int[]` of size 12.
    -   *Native:* C supports `values[3][4]` natively.
-   **Array of Structs:**
    -   Ensure `Point2D` struct is defined.
    -   Verify struct alignment within array.
-   Implement **BOTH** `Final` and `Appendable` variants.

**Design Reference:**
-   [Arrays Guide](../tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-IMPLEMENTATION-GUIDE.md#54-arrays)

---

## 🧪 Testing Requirements

**For EVERY Topic:**
1.  **Native → C# Roundtrip:** Verify data generated in C is correctly received in C#.
2.  **CDR Byte Verification:** Verify C# serialization matches Native CDR exactly.
3.  **C# → Native Roundtrip:** Verify data generated in C# is correctly received in C.

**Total New Tests:**
-   8 Topics * 2 Variants = **16 New Test Pairs**

---

## 📊 Report Requirements

**Focus on Developer Insights:**
1.  **Array Layout:** How did you handle multi-dimensional array flattening?
2.  **Memory Management:** Any issues with `ArrayStringTopic` memory allocation/freeing in Native?
3.  **Alignment:** Did you encounter alignment padding issues with `ArrayStructTopic`?

---

## 🎯 Success Criteria

This batch is DONE when:
-   [ ] All 8 tasks (RT-E01 to RT-A06) are marked complete.
-   [ ] `ArrayInt32Topic` is fixed and enabled.
-   [ ] All 16 new topic variants are implemented and passing.
-   [ ] Report submitted.

---

## ⚠️ Common Pitfalls to Avoid
-   **Multi-dim Array Indexing:** `[i][j]` vs `[i*cols + j]`. Ensure generator/validator logic matches.
-   **String Array Memory:** In C, `char* values[5]` needs 5 allocations + 1 array allocation? Or just 5 pointers in struct? (IDL `string values[5]` -> struct { char* values[5]; }).
-   **Struct Array Padding:** Watch out for padding between struct elements in an array.
