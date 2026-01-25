# BATCH-29: Nested Structures & Cleanup
**Batch Number:** BATCH-29
**Tasks:** RT-EXT-02 (Global Audit), RT-A02 (Cleanup), RT-A03 (Cleanup), RT-N01, RT-N02, RT-N03, RT-N04
**Phase:** Phase 4 (Nested Structures)
**Estimated Effort:** 8-12 hours
**Priority:** HIGH
**Dependencies:** BATCH-28 (Completed)

---

## 📋 Onboarding (New Developer)

**Welcome to the FastCycloneDdsCsharpBindings project!**
You are working on the **C# to C Roundtrip Test Suite**, a critical component for verifying the interoperability and correctness of our C# DDS bindings against the native C implementation.

### 1. Project Context
*   **Goal:** Ensure that data serialized by our C# bindings is byte-for-byte identical to data serialized by the native C library, and vice-versa.
*   **Mechanism:** We use a "Roundtrip" approach:
    1.  **Native -> C#:** Native C app generates data (seeded), C# receives it, validates values, and re-serializes it to check byte equality.
    2.  **C# -> Native:** C# generates data (seeded), sends it to Native C, which validates it.

### 2. Environment Setup & Building
You will need to build both the Native C library and the C# Test Runner.

**A. Build Native Library (`CsharpToC_Roundtrip_Native.dll`)**
```powershell
cd tests/CsharpToC.Roundtrip.Tests/Native
mkdir build
cd build
cmake ..
cmake --build . --config Release
# Ensure the DLL is in the C# output directory or PATH.
# Usually, the C# project copies it automatically if set up correctly, 
# but if you see DllNotFoundException, check this.
```

**B. Build C# Test Runner**
```powershell
cd tests/CsharpToC.Roundtrip.Tests
dotnet build
```

**C. Run Tests**
```powershell
# Run from the project root or bin folder
dotnet run --project tests/CsharpToC.Roundtrip.Tests/CsharpToC.Roundtrip.Tests.csproj
```

### 3. Key Files & Locations
*   **IDL Definitions:** `tests/CsharpToC.Roundtrip.Tests/idl/atomic_tests.idl`
    *   *Source of Truth.* Defines the DDS topics and types.
*   **C# Types:** `tests/CsharpToC.Roundtrip.Tests/AtomicTestsTypes.cs`
    *   *Generated/Manual Types.* Must match IDL exactly. Pay attention to attributes like `[DdsExtensibility]`.
*   **Native Handlers:** `tests/CsharpToC.Roundtrip.Tests/Native/atomic_tests_native.c`
    *   *C Implementation.* Contains `generate_X` and `validate_X` functions.
*   **Test Runner:** `tests/CsharpToC.Roundtrip.Tests/Program.cs`
    *   *Main Entry.* Orchestrates the tests.
*   **Task Tracker:** `tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-TASK-TRACKER.md`
    *   *Status.* Check this to see what is done and what is pending.

### 4. Workflow Resources
1.  **Workflow Guide:** `.dev-workstream/DEV-LEAD-GUIDE.md` - **READ THIS FIRST** to understand our "Agentic" workflow.
2.  **Implementation Guide:** `tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-IMPLEMENTATION-GUIDE.md` - Technical details on mapping C# types to C.

---

## 🎯 Batch Objectives

1.  **Cleanup BATCH-28:** Enable and verify the remaining Appendable Array tests that were commented out.
2.  **Implement Phase 4:** Add full support for Nested Structures (Structs containing Structs).

---

## 🔄 MANDATORY WORKFLOW

**You MUST complete Task 0 before proceeding.**

### Task 0: Global Extensibility Audit (CRITICAL)
**Goal:** Ensure **EVERY** topic and struct in the test suite has an explicit extensibility definition. The previous batch missed Sections 9, 10, and 12.

1.  **Update IDL (`atomic_tests.idl`):**
    *   Add `@final` to **ALL** remaining structs in:
        *   **Section 9 (Composite Keys):** `TwoKeyInt32Topic`, `TwoKeyStringTopic`, `ThreeKeyTopic`, `FourKeyTopic`.
        *   **Section 10 (Nested Keys):** `Location`, `NestedKeyTopic`, `Coordinates`, `NestedKeyGeoTopic`, `TripleKey`, `NestedTripleKeyTopic`.
        *   **Section 12 (Edge Cases):** `EmptySequenceTopic`, `LargeSequenceTopic`, `LongStringTopic`, `UnboundedStringTopic`, `AllPrimitivesAtomicTopic`.

2.  **Update C# (`AtomicTestsTypes.cs`):**
    *   Add `[DdsExtensibility(DdsExtensibilityKind.Final)]` to the following structs (which are currently missing it):
        *   `Location`
        *   `Coordinates`
        *   `TripleKey`
    *   *Verify:* Check that all topics in Sections 9, 10, and 12 also have the attribute (they should, but verify).

### Task 1: Cleanup BATCH-28 (Arrays)
**Goal:** Ensure all Array tests from the previous batch are active and passing.

1.  **Uncomment Tests:** In `Program.cs`, uncomment:
    *   `await TestArrayFloat64Appendable();`
    *   `await TestArrayStringAppendable();`
2.  **Verify:** Run the tests.
    *   *Note:* Native handlers for these already exist in `atomic_tests_native.c`.
    *   If they fail, fix them immediately. **Do not proceed until they pass.**

### Task 2: Implement Phase 4 (Nested Structures)
**Goal:** Implement Roundtrip tests for Nested Structures.

#### Step 2.1: Explicit Extensibility (Structs)
To ensure consistency and avoid "Appendable vs Final" mismatches (like the one seen with `Point2D` in Batch 28), you must explicitly mark the nested structs as Final.

1.  **Update IDL (`atomic_tests.idl`):**
    *   Add `@final` to the following **struct definitions** (not just the topics):
        *   `Point3D`
        *   `Box`
        *   `Container`
    *   *(Note: `Point2D` might already be handled, check it).*

2.  **Update C# (`AtomicTestsTypes.cs`):**
    *   Add `[DdsExtensibility(DdsExtensibilityKind.Final)]` to:
        *   `Point3D`
        *   `Box`
        *   `Container`

#### Step 2.2: Native Implementation
1.  **Modify `atomic_tests_native.c`:**
    *   Implement `generate_` and `validate_` functions for:
        *   `NestedStructTopic`
        *   `Nested3DTopic`
        *   `DoublyNestedTopic`
        *   `ComplexNestedTopic`
    *   *Hint:* Reuse logic. For `DoublyNestedTopic`, generating a `Box` implies generating two `Point2D`s.

2.  **Register Handlers:**
    *   Update `test_registry.c` to register the 4 new topics.

#### Step 2.3: C# Implementation
1.  **Modify `Program.cs`:**
    *   Implement test methods:
        *   `TestNestedStruct()`
        *   `TestNested3D()`
        *   `TestDoublyNested()`
        *   `TestComplexNested()`
    *   Add them to `Main()`.

#### Step 2.4: Verification
1.  **Run Tests:** Ensure all new tests PASS (Native->C#, CDR Verify, C#->Native).

---

## 🧪 Testing Requirements

**Total Expected Passing Tests:**
*   All Primitives (Phase 1)
*   All Arrays (Phase 3) - Including the 2 restored Appendable tests.
*   All Nested Structs (Phase 4) - 4 new tests.

---

## 📊 Report Requirements
In `BATCH-29-REPORT.md`:
1.  Confirm `TestArrayFloat64Appendable` and `TestArrayStringAppendable` passed.
2.  List the new Nested Struct tests implemented.
3.  Confirm that explicit `@final` / `[DdsExtensibility]` attributes were applied to the nested structs.
