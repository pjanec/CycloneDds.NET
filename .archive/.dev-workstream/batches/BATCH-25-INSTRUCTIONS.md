# BATCH-25: Phase 1 Completion - Basic Primitives

**Batch Number:** BATCH-25  
**Tasks:** RT-P03, RT-P04, RT-P05, RT-P06, RT-P07, RT-P08, RT-P09, RT-P10, RT-P11, RT-P13, RT-P14  
**Phase:** Phase 1: Basic Primitives  
**Estimated Effort:** 16-20 hours  
**Priority:** HIGH  
**Dependencies:** BATCH-24 (or previous setup)

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome! This batch focuses on completing the "Basic Primitives" phase of the C# to C Roundtrip Test Framework. You will be implementing support for various primitive types (integers, floats, chars) and string variants. This work is foundational for ensuring data integrity between C# and Native C.

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/README.md` - How to work with batches
2. **Task Tracker:** `tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-TASK-TRACKER.md` - See Phase 1 details
3. **Implementation Guide:** `tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-IMPLEMENTATION-GUIDE.md` - **CRITICAL:** Contains exact code patterns for every type.

### Source Code Location
- **Primary Work Area:** `tests/CsharpToC.Roundtrip.Tests/`
- **IDL Definitions:** `tests/CsharpToC.Roundtrip.Tests/idl/atomic_tests.idl`
- **Native Code:** `tests/CsharpToC.Roundtrip.Tests/Native/`
- **C# Types:** `tests/CsharpToC.Roundtrip.Tests/AtomicTestsTypes.cs`
- **Test Orchestrator:** `tests/CsharpToC.Roundtrip.Tests/Program.cs`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-25-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/BATCH-25-QUESTIONS.md`

---

## Context

We are building a comprehensive roundtrip testing framework to verify that the C# bindings produce byte-identical CDR serialization to the native Cyclone DDS C library. Phase 1 focuses on "Basic Primitives" - ensuring that every fundamental data type (char, octet, short, long, float, string, etc.) works correctly in isolation.

**Related Tasks:**
- All RT-Pxx tasks in [ROUNDTRIP-TASK-TRACKER.md](../tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-TASK-TRACKER.md)

---

## 🎯 Batch Objectives
Complete all remaining tasks in **Phase 1: Basic Primitives**. By the end of this batch, we should have 100% coverage of primitive types.

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1 (Integers):** Implement → Write tests → **ALL tests pass** ✅
2. **Task 2 (Floats):** Implement → Write tests → **ALL tests pass** ✅
3. **Task 3 (Strings):** Implement → Write tests → **ALL tests pass** ✅

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written
- ✅ **ALL tests passing** (including previous tasks)

---

## ✅ Tasks

### Task 1: Integer & Character Types (RT-P03 to RT-P09)

**Tasks Covered:**
- **RT-P03:** CharTopic (`char`)
- **RT-P04:** OctetTopic (`octet` / `byte`)
- **RT-P05:** Int16Topic (`short`)
- **RT-P06:** UInt16Topic (`unsigned short`)
- **RT-P07:** UInt32Topic (`unsigned long`)
- **RT-P08:** Int64Topic (`long long`)
- **RT-P09:** UInt64Topic (`unsigned long long`)

**Files to Modify:**
1. `idl/atomic_tests.idl`: Ensure types are defined (should already be there).
2. `AtomicTestsTypes.cs`: Define C# structs with `[DdsTopic]` and `[DdsExtensibility]`.
3. `Native/atomic_tests_native.c`: Implement `generate_*` and `validate_*` functions.
4. `Native/test_registry.c`: Register the new handlers.
5. `Program.cs`: Add `Test*` functions and call them in `Main`.

**Requirements:**
- Implement **BOTH** `Final` and `Appendable` variants for every topic.
- Use the **Seed-Based Algorithms** defined in `ROUNDTRIP-TASK-TRACKER.md` or derive simple deterministic ones (e.g., `value = seed * constant`).
- **Validation:** Must verify exact value match.

**Design Reference:**
- [Primitive Types Guide](../tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-IMPLEMENTATION-GUIDE.md#51-primitive-types)

---

### Task 2: Floating Point Types (RT-P10, RT-P11)

**Tasks Covered:**
- **RT-P10:** Float32Topic (`float`)
- **RT-P11:** Float64Topic (`double`)

**Files to Modify:** Same as Task 1.

**Requirements:**
- Implement **BOTH** `Final` and `Appendable` variants.
- **Validation:** MUST use epsilon comparison (e.g., `fabs(a - b) < 0.0001`).
- **Native:** Include `<math.h>` if needed.

**Design Reference:**
- [Example: Adding Float32Topic](../tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-IMPLEMENTATION-GUIDE.md#example-adding-float32topic)

---

### Task 3: String Types (RT-P13, RT-P14)

**Tasks Covered:**
- **RT-P13:** StringUnboundedTopic (`string`)
- **RT-P14:** StringBounded256Topic (`string<256>`)

**Files to Modify:** Same as Task 1.

**Requirements:**
- Implement **BOTH** `Final` and `Appendable` variants.
- **C# Attributes:** Must use `[DdsManaged]` for types containing strings.
- **Native:** Use `dds_string_dup` for allocation.
- **Validation:** String content must match exactly.

**Design Reference:**
- [Strings Guide](../tests/CsharpToC.Roundtrip.Tests/ROUNDTRIP-IMPLEMENTATION-GUIDE.md#52-strings)

---

## 🧪 Testing Requirements

**For EVERY Topic (e.g., CharTopic, Float32Topic...):**
1. **Native → C# Roundtrip:**
   - Native generates data from seed.
   - C# receives and validates against same seed logic.
2. **CDR Byte Verification:**
   - Capture CDR bytes from Native.
   - C# serializes same data.
   - Bytes must match **exactly**.
3. **C# → Native Roundtrip:**
   - C# generates data from seed.
   - Native receives and validates.

**Total New Tests:**
- 11 Topics * 2 Variants (Final/Appendable) = **22 New Test Pairs**
- Each pair runs 3 phases (Receive, Verify, Send).

---

## 📊 Report Requirements

**Focus on Developer Insights:**

1. **Issues Encountered:** Did any specific type cause issues? (e.g., unsigned types, 64-bit alignment).
2. **Pattern Observations:** Did you notice any redundancy that could be refactored in the future?
3. **Performance:** Any noticeable slowdown with the increased number of tests?
4. **Edge Cases:** Did you test max/min values for integers?

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] All 11 tasks (RT-P03 to RT-P14) are marked complete in tracker.
- [ ] All 22 new topic variants (Final + Appendable) are implemented.
- [ ] `tests/CsharpToC.Roundtrip.Tests` builds and runs successfully.
- [ ] All roundtrip phases pass for all new topics.
- [ ] Report submitted.

---

## ⚠️ Common Pitfalls to Avoid
- ** forgetting `[DdsManaged]`** on string topics (causes crash).
- **Mismatched Seeds:** Using the same seed for different topics can cause confusion if messages cross-talk (though topics separate them). Use unique base seeds as suggested in the guide.
- **Floating Point Equality:** Never use `==` for floats/doubles.
- **Unsigned Casting:** Be careful with `uint` <-> `int` casting in C# vs C.
