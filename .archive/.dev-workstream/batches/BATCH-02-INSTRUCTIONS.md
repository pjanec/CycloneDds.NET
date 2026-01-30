# BATCH-02: Foundation - Size Calculator and Golden Rig Validation

**Batch Number:** BATCH-02  
**Tasks:** FCDC-S004, FCDC-S005  
**Phase:** Stage 1 - Foundation (Part 2)  
**Estimated Effort:** 6-8 hours  
**Priority:** CRITICAL (Validation Gate)  
**Dependencies:** BATCH-01 (CdrWriter and CdrReader must exist)

---

## 📋 Onboarding & Workflow

### Developer Instructions

This batch completes Stage 1 Foundation by adding size calculation utilities and **most critically** - proving your CDR implementation is byte-perfect through the Golden Rig validation test.

**Critical Context:** The Golden Rig is a **blocking validation gate**. We create a C program that uses Cyclone's native serialization, then compare its output byte-for-byte against your C# implementation. **100% match = proven correctness.**

**Your Mission:** Implement size calculation helpers, then prove your CDR implementation produces **byte-identical output** to Cyclone's native code.

### Required Reading (IN ORDER)

1. **Workflow Guide:** `.dev-workstream/README.md` - How to work with batches
2. **BATCH-01 Review:** `.dev-workstream/reviews/BATCH-01-REVIEW.md` - Learn from feedback
3. **Task Master:** `docs/SERDATA-TASK-MASTER.md` - See tasks FCDC-S004, FCDC-S005
4. **Design Document:** `docs/SERDATA-DESIGN.md` - Sections 6, 12.1 (CDR Core, Golden Rig)
5. **Design Context:** `docs/design-talk.md` - Lines 2890-2916 (Golden Rig validation)

### Source Code Location

- **Package:** `Src/CycloneDDS.Core/` (from BATCH-01)
- **Test Project:** `tests/CycloneDDS.Core.Tests/` (from BATCH-01)
- **Golden Rig:** `tests/GoldenRig/` (you will create this)

### Report Submission

**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-02-REPORT.md`

**Use template:**  
`.dev-workstream/templates/BATCH-REPORT-TEMPLATE.md`

**If you have questions, create:**  
`.dev-workstream/questions/BATCH-02-QUESTIONS.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **FCDC-S004 (SizeCalculator):** Implement → Write tests → **ALL tests pass** ✅
2. **FCDC-S005 (Golden Rig):** Implement C program → Implement C# tests → **100% byte-perfect match (8/8 cases)** ✅

**DO NOT** move to report until:
- ✅ Golden Rig shows 100% byte match on ALL 8 test cases
- ✅ If ANY case fails, debug until byte-perfect

**Why:** Stage 2 (Code Generation) is **blocked** until Golden Rig passes. If your CDR is wrong by 1 byte, all generated code will be wrong.

---

## Context

This batch completes **Stage 1 Foundation** and opens the gate to Stage 2 (Code Generation).

**Why Golden Rig Matters:** You cannot trust your own implementation without external validation. By comparing against Cyclone's battle-tested native code, we prove correctness at the byte level.

**Related Tasks:**
- [FCDC-S004](../docs/SERDATA-TASK-MASTER.md#fcdc-s004-cdrsizecalculator-utilities) - Size calculation
- [FCDC-S005](../docs/SERDATA-TASK-MASTER.md#fcdc-s005-golden-rig-integration-test-validation-gate) - **CRITICAL** Golden Rig validation

---

## 🎯 Batch Objectives

**Primary Goal:** Prove CDR implementation correctness through byte-perfect validation against Cyclone native.

**Success Metric:** Golden Rig test passes with **100% byte match on all 8 test cases**.

---

## ✅ Tasks

### Task 1: AlignmentMath and CdrSizer Utilities (FCDC-S004)

**Files:**  
- `Src/CycloneDDS.Core/AlignmentMath.cs` (NEW)
- `Src/CycloneDDS.Core/CdrSizer.cs` (NEW)

**Task Definition:** See [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md#fcdc-s004-cdrsizecalculator-utilities)

**Description:**  
Implement the core alignment helper and the "shadow writer" for size calculation. These are **critical for the two-pass XCDR2 serialization** in Stage 2.

**Design Reference:** [XCDR2-IMPLEMENTATION-DETAILS.md](../docs/XCDR2-IMPLEMENTATION-DETAILS.md), design-talk.md §3638-3741

**Part A: AlignmentMath (Single Source of Truth)**

Create `Src/CycloneDDS.Core/AlignmentMath.cs`:

```csharp
using System.Runtime.CompilerServices;

namespace CycloneDDS.Core
{
    /// <summary>
    /// Single source of truth for XCDR2 alignment calculations.
    /// </summary>
    public static class AlignmentMath
    {
        /// <summary>
        /// Calculate next aligned position for given alignment.
        /// </summary>
        /// <param name="currentPosition">Current absolute position in stream</param>
        /// <param name="alignment">Required alignment (must be power of 2: 1, 2, 4, 8)</param>
        /// <returns>Next position aligned to boundary</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Align(int currentPosition, int alignment)
        {
            int mask = alignment - 1;
            int padding = (alignment - (currentPosition & mask)) & mask;
            return currentPosition + padding;
        }
    }
}
```

**Part B: CdrSizer (Shadow Writer for Sizing)**

Create `Src/CycloneDDS.Core/CdrSizer.cs`:

```csharp
using System;
using System.Text;

namespace CycloneDDS.Core
{
    /// <summary>
    /// Shadow writer that calculates sizes without writing bytes.
    /// MUST mirror CdrWriter API exactly for symmetric code generation.
    /// </summary>
    public ref struct CdrSizer
    {
        private int _cursor;
        
        public CdrSizer(int initialOffset)
        {
            _cursor = initialOffset;
        }
        
        public int Position => _cursor;
        
        // Primitives (mirrors CdrWriter)
        public void WriteByte(byte value)
        {
            _cursor += 1;
        }
        
        public void WriteInt32(int value)
        {
            _cursor = AlignmentMath.Align(_cursor, 4);
            _cursor += 4;
        }
        
        public void WriteUInt32(uint value)
        {
            _cursor = AlignmentMath.Align(_cursor, 4);
            _cursor += 4;
        }
        
        public void WriteInt64(long value)
        {
            _cursor = AlignmentMath.Align(_cursor, 8);
            _cursor += 8;
        }
        
        public void WriteUInt64(ulong value)
        {
            _cursor = AlignmentMath.Align(_cursor, 8);
            _cursor += 8;
        }
        
        public void WriteFloat(float value)
        {
            _cursor = AlignmentMath.Align(_cursor, 4);
            _cursor += 4;
        }
        
        public void WriteDouble(double value)
        {
            _cursor = AlignmentMath.Align(_cursor, 8);
            _cursor += 8;
        }
        
        public void WriteString(ReadOnlySpan<char> value)
        {
            _cursor = AlignmentMath.Align(_cursor, 4); // Length header
            _cursor += 4; // Length (Int32)
            _cursor += Encoding.UTF8.GetByteCount(value);
            _cursor += 1; // NUL terminator
        }
        
        public void WriteFixedString(ReadOnlySpan<byte> utf8Bytes, int fixedSize)
        {
            _cursor += fixedSize;
        }
        
        /// <summary>
        /// Returns size delta from initial offset.
        /// </summary>
        public int GetSizeDelta(int startOffset) => _cursor - startOffset;
    }
}
```

**Implementation Notes:**
- `AlignmentMath.Align`: MUST use exact formula `(alignment - (pos & mask)) & mask`
- `CdrSizer`: MUST mirror every `CdrWriter` method signature
- All alignment uses `AlignmentMath.Align` - single source of truth
- Returns delta size, not absolute offset

**Tests Required:** (Create `AlignmentMathTests.cs` and `CdrSizerTests.cs`)

**AlignmentMathTests - Minimum 8 tests:**
1. ✅ Align(0, 4) → 0 (already aligned)
2. ✅ Align(1, 4) → 4 (pad 3 bytes)
3. ✅ Align(2, 4) → 4 (pad 2 bytes)
4. ✅ Align(3, 4) → 4 (pad 1 byte)
5. ✅ Align(5, 8) → 8 (8-byte boundary)
6. ✅ Align(7, 2) → 8 (2-byte boundary)
7. ✅ Align(100, 1) → 100 (1-byte aligned = no change)
8. ✅ Edge: Align(0, 8) → 0

**CdrSizerTests - Minimum 10 tests:**
1. ✅ WriteByte advances by 1
2. ✅ WriteInt32 from offset 0 → size 4
3. ✅ WriteInt32 from offset 1 → align to 4, then +4 = size 7 total
4. ✅ WriteDouble from offset 0 → size 8
5. ✅ WriteDouble from offset 5 → align to 8, then +8 = size 11 total
6. ✅ WriteString("Hello") from offset 0 → 4 (len) + 5 (bytes) + 1 (NUL) = 10
7. ✅ WriteString("") from offset 0 → 4 + 0 + 1 = 5
8. ✅ Multiple writes: Byte + Int32 + Double (verify cumulative size)
9. ✅ GetSizeDelta returns correct delta, not absolute
10. ✅ **CRITICAL:** CdrSizer size matches actual CdrWriter output

**Quality Check:**
For test #10, serialize same data with both `CdrSizer` and `CdrWriter`, verify sizes match:
```csharp
var sizer = new CdrSizer(0);
sizer.WriteInt32(42);
sizer.WriteString("Test");
int expectedSize = sizer.GetSizeDelta(0);

var writer = new ArrayBufferWriter<byte>();
var cdr = new CdrWriter(writer);
cdr.WriteInt32(42);
cdr.WriteString("Test");
cdr.Complete();

Assert.Equal(expectedSize, writer.WrittenCount);
```

**Estimated Time:** 2-3 hours

---

### Task 2: Golden Rig Integration Test (FCDC-S005) 🚨 CRITICAL

**Files:**
- `tests/GoldenRig/golden_data_generator.c` (NEW - C program)
- `tests/GoldenRig/CMakeLists.txt` (NEW - build C program)
- `tests/CycloneDDS.Core.Tests/GoldenConsistencyTests.cs` (NEW)

**Task Definition:** See [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md#fcdc-s005-golden-rig-integration-test-validation-gate)

**Description:**  
**DO NOT PROCEED TO BATCH-03 (Stage 2) WITHOUT 100% PASS RATE ON THIS TEST.**

This is the **validation gate** that proves your CDR implementation is byte-identical to Cyclone's native serialization.

**Design Reference:** [SERDATA-DESIGN.md §12.1](../docs/SERDATA-DESIGN.md#121-stage-1-golden-rig-foundation), design-talk.md lines 2890-2916

**Test Structure:**

**Part 1: C Golden Data Generator**

Create `tests/GoldenRig/golden_data_generator.c`:

```c
#include <stdio.h>
#include <string.h>
#include <dds/dds.h>
#include "dds/ddsc/dds_public_impl.h"

// Test Case 1: Simple Primitives
typedef struct SimplePrimitive {
    int32_t id;
    double value;
} SimplePrimitive;

// Test Case 2: Nested Struct (alignment trap)
typedef struct Nested {
    int32_t a;
    double b;
} Nested;

typedef struct NestedStruct {
    uint8_t byte_field;  // Forces alignment after
    Nested nested;
} NestedStruct;

// Test Case 3: Unbounded String
typedef struct StringMessage {
    int32_t id;
    char* message;
} StringMessage;

// Test Cases 4-8: Add more (see below)

void print_hex(const uint8_t* data, size_t len, const char* name) {
    printf("%s: ", name);
    for (size_t i = 0; i < len; i++) {
        printf("%02X", data[i]);
    }
    printf("\n");
}

int main() {
    // Initialize Cyclone DDS
    dds_entity_t participant = dds_create_participant(DDS_DOMAIN_DEFAULT, NULL, NULL);
    
    // Test Case 1: Simple Primitives
    SimplePrimitive sp = {.id = 42, .value = 3.14159};
    // Serialize using Cyclone's serdata APIs
    // Print hex: print_hex(buffer, size, "SimplePrimitive");
    
    // Repeat for all 8 test cases
    
    dds_delete(participant);
    return 0;
}
```

**Build with CMake:**
```cmake
cmake_minimum_required(VERSION 3.16)
project(GoldenRig C)

find_package(CycloneDDS REQUIRED)

add_executable(golden_gen golden_data_generator.c)
target_link_libraries(golden_gen CycloneDDS::ddsc)
```

**Part 2: C# Validation**

Create `tests/CycloneDDS.Core.Tests/GoldenConsistencyTests.cs`:

```csharp
using System;
using System.Buffers;
using Xunit;

public class GoldenConsistencyTests
{
    // Hex strings captured from C program output
    private const string SimplePrimitive_Expected = "2A000000182D4454FB210940";
    
    [Fact]
    public void GoldenRig_SimplePrimitive_BytePerfectMatch()
    {
        // Manually serialize same data as C program
        var writer = new ArrayBufferWriter<byte>();
        var cdr = new CdrWriter(writer);
        
        cdr.WriteInt32(42);       // id = 42
        cdr.WriteDouble(3.14159); // value = 3.14159
        cdr.Complete();
        
        // Convert to hex
        string actualHex = Convert.ToHexString(writer.WrittenSpan);
        
        // Assert byte-for-byte match
        Assert.Equal(SimplePrimitive_Expected, actualHex);
    }
    
    // Repeat for 7 more test cases
}
```

**Minimum 8 Test Cases Required:**

1. ✅ **SimplePrimitive:** int + double (basic alignment)
2. ✅ **NestedStruct:** byte + nested struct (alignment after byte field)
3. ✅ **FixedString:** 32-byte fixed UTF-8 string (NUL-padded)
4. ✅ **UnboundedString:** length header + "Hello World" + NUL
5. ✅ **PrimitiveSequence:** length + int[] {1, 2, 3, 4, 5}
6. ✅ **StringSequence:** length + string[] {"A", "B", "C"} (nested headers)
7. ✅ **MixedStruct:** byte + int + double + string (alignment traps)
8. ✅ **AppendableStruct:** DHEADER (4-byte size) + fields (tests delimiter header)

**Success Criteria:**
- ✅ 100% byte match on ALL 8 test cases
- ✅ Detailed diff printed on mismatch (show byte-by-byte difference)
- ✅ Tests automated (can run in CI)

**If ANY test case fails:** Debug until byte-perfect. Do not proceed to BATCH-03.

**Implementation Tips:**
1. Run C program: `./golden_gen` → captures hex strings
2. Copy hex strings into C# test constants
3. Manually write C# serialization matching C structs
4. Compare hex outputs
5. If mismatch, print both hex strings byte-by-byte to find difference

**Estimated Time:** 4-6 hours

---

## 🧪 Testing Requirements

**Minimum Total Tests:** 26 tests

**Test Distribution:**
- AlignmentMathTests: 8 tests
- CdrSizerTests: 10 tests
- GoldenConsistencyTests: 8 tests (MANDATORY - all must pass)

**Test Quality Standards:**

**✅ REQUIRED - Golden Rig:**
- 100% byte match on all 8 test cases
- No exceptions, no partial matches
- Hex strings must be exact

**❌ FAILURE CONDITIONS:**
- Any test case with byte mismatch
- Skipping test cases
- Accepting "close enough" results

**All tests must pass, especially Golden Rig, before submitting report.**

---

## 📊 Report Requirements

Use template: `.dev-workstream/templates/BATCH-REPORT-TEMPLATE.md`

**Focus on Developer Insights:**

**Required Sections:**

1. **Implementation Summary**
   - Tasks completed (FCDC-S004, FCDC-S005)
   - Test counts
   - **Golden Rig Status: MUST be 100% pass (8/8)**

2. **Issues Encountered**
   - Problems with C program compilation?
   - Cyclone DDS API challenges?
   - Byte mismatches discovered?
   - How did you debug mismatches?

3. **Golden Rig Debugging**
   - Which test cases failed initially?
   - Root causes of mismatches
   - How you achieved byte-perfect match

4. **Weak Points Spotted**
   - CdrWriter/Reader improvements needed?
   - Test coverage gaps?
   - Documentation clarity?

5. **Confidence Assessment**
   - How confident are you in CDR correctness now?
   - What would improve confidence further?

---

## 🎯 Success Criteria

This batch is DONE when:

- ✅ **FCDC-S004** Complete: AlignmentMath and CdrSizer implemented, 18 tests pass (8 + 10)
- ✅ **FCDC-S005** Complete: **Golden Rig 100% byte-perfect match (8/8 test cases)**
- ✅ All 26 tests passing
- ✅ No compiler warnings
- ✅ Report submitted to `.dev-workstream/reports/BATCH-02-REPORT.md`

**GATE:** Golden Rig test MUST pass (100% match) before Stage 2 begins.

---

## ⚠️ Common Pitfalls to Avoid

1. **Golden Rig Shortcuts:** Accepting partial matches or skipping test cases
   - **Must be:** 100% byte match on ALL 8 cases

2. **Endianness Confusion:** C program using wrong endianness
   - Cyclone uses little-endian, verify with hex output

3. **String Encoding:** Forgetting NUL in C strings affects length
   - C: `"Hello"` has implicit NUL, length header must reflect this

4. **Alignment in C:** C compiler may pad structs differently
   - Use `#pragma pack` or `__attribute__((packed))` if needed

5. **Size Calculator:** Returning absolute offset instead of delta size
   - Must return size increment, not final position

---

## 📚 Reference Materials

- **Task Master:** [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md) - FCDC-S004, S005
- **Design Doc:** [SERDATA-DESIGN.md](../docs/SERDATA-DESIGN.md) - Section 12.1
- **Design Context:** [design-talk.md](../docs/design-talk.md) - Lines 2890-3021
- **XCDR2 Spec:** `docs/dds-xtypes-1.3-xcdr2-1-single-file.htm`
- **Cyclone DDS Docs:** Serdata APIs for Golden Rig C program

---

**Next Batch:** BATCH-03 (Schema Package + Generator Infrastructure) - **Blocked until Golden Rig passes**

**Validation Gate:** This batch opens Stage 2. Do not proceed without 100% Golden Rig success.
