# BATCH-01 Review

**Batch:** BATCH-01  
**Reviewer:** Development Lead  
**Date:** 2026-01-14  
**Status:** ⚠️ NEEDS FIXES

---

## Summary

Foundation package implemented with all required attributes, wrapper types, and QoS/error types. 55 tests pass. However, FixedString implementation has multi-byte UTF-8 truncation bug and test coverage misses critical edge cases.

---

## Issues Found

### Issue 1: UTF-8 Multi-Byte Character Truncation Bug

**File:** `src/CycloneDDS.Schema/WrapperTypes/FixedString32.cs` (Line 30)  
**Problem:** `Length` calculation counts bytes until first NUL, but doesn't validate UTF-8 boundary. A truncated multi-byte character (e.g., 3-byte emoji stored as 2 bytes + NUL) will cause `ToStringAllocated()` to throw or return replacement chars.

**Example:**
```csharp
// If buffer contains: [0xE2, 0x82, 0x00, ...] (truncated € symbol)
// Length returns 2, but UTF8.GetString(span[0..2]) is invalid
```

**Fix:** Either:
1. Store actual byte length in a field (requires layout change), OR
2. Validate on construction that buffer[length-1] isn't mid-character (check if previous byte is continuation byte)

**Why It Matters:** Design Talk §2193-2201 explicitly warns about this. Current code makes it possible to create poisoned FixedString.

---

### Issue 2: Missing Test Coverage - Multi-Byte UTF-8 Boundary

**File:** `tests/CycloneDDS.Schema.Tests/FixedStringTests.cs`  
**Missing Test:** FixedString32 with multi-byte characters at boundary (e.g., 30-byte ASCII + 3-byte emoji = 33 bytes, should reject).

**Current tests only check:**
- ASCII strings exceeding capacity
- Lone surrogates (invalid UTF-8)

**Missing edge case:** 
```csharp
[Fact]
public void FixedString32_MultiByteAtBoundary_Rejects()
{
    // 30 chars + 2-byte char = 32 bytes (fits)
    string valid = new string('a', 30) + "ü"; // ü is 2 bytes
    Assert.True(FixedString32.TryFrom(valid, out _));
    
    // 30 chars + 3-byte emoji = 33 bytes (rejects)
    string invalid = new string('a', 30) + "€"; // € is 3 bytes
    Assert.False(FixedString32.TryFrom(invalid, out _));
}
```

**Required:** Add this test and verify it passes with current impl (should pass, but confirms byte-counting logic).

---

### Issue 3: BoundedSeq Struct Copy Semantics Undocumented Risk

**File:** `src/CycloneDDS.Schema/WrapperTypes/BoundedSeq.cs` (Line 16)  
**Problem:** Struct wraps reference type (`List<T>`) causing shared-state on copy. Test at line 73-83 verifies this behavior but doesn't document the risk.

**Example:**
```csharp
var seq1 = new BoundedSeq<int>(5); seq1.Add(1);
var seq2 = seq1; // COPIES STRUCT, NOT THE LIST
seq2.Add(2);     // MUTATES SHARED LIST
// seq1.Count == 2 (!!)
```

**Fix:** Add XML doc warning on struct:
```csharp
/// <summary>
/// A bounded sequence of items with a fixed maximum capacity.
/// <para><b>WARNING:</b> This is a struct wrapping a reference type. 
/// Copying the struct creates a shallow copy that shares the underlying storage.</para>
/// </summary>
```

**Why It Matters:** Users may assume struct = value semantics. Shared mutation is surprising.

---

### Issue 4: Missing Test - FixedString Capacity Constant

**File:** `tests/CycloneDDS.Schema.Tests/FixedStringTests.cs`  
**Missing Test:** Verify `FixedString32.Capacity == 32` (and same for 64, 128).

**Add:**
```csharp
[Fact]
public void FixedString_CapacityConstants_Correct()
{
    Assert.Equal(32, FixedString32.Capacity);
    Assert.Equal(64, FixedString64.Capacity);
    Assert.Equal(128, FixedString128.Capacity);
}
```

Minor issue, but spec requires it.

---

## Test Quality Assessment

**Overall:** Tests verify behavior, not just compilation. Good coverage of validation and edge cases.

**Strengths:**
- Lone surrogate invalid UTF-8 test (line 37-51 FixedStringTests)
- BoundedSeq shared storage test (line 73-83)
- AttributeUsage AllowMultiple test (line 159-164)

**Weaknesses:**
- Missing multi-byte UTF-8 boundary test (critical edge case)
- No capacity constant tests
- No test for FixedString with exactly 32 bytes of multi-byte chars

---

## Verdict

**Status:** ⚠️ NEEDS FIXES

**Required Actions:**
1. Fix Issue 1 (UTF-8 truncation bug) - choose solution and implement
2. Add Issue 2 test (multi-byte boundary)
3. Add Issue 3 doc warning (BoundedSeq copy semantics)
4. Add Issue 4 test (capacity constants)

**Once fixed, update report with:**
- Which UTF-8 truncation solution was chosen and why
- Confirmation all 4 issues addressed

---

## 📝 Commit Message

**DO NOT USE YET - batch needs fixes first**

Once approved, use this message:

```
feat: foundation schema package (BATCH-01)

Completes FCDC-001, FCDC-002, FCDC-003, FCDC-004

Establishes CycloneDDS.Schema foundation package with attribute system,
wrapper types for fixed-size data, and type registry infrastructure.

Schema Attributes (FCDC-001):
- 11 attribute classes: DdsTopic, DdsQos, DdsUnion, DdsTypeName
- Field-level: DdsKey, DdsBound, DdsId, DdsOptional
- Union-specific: DdsDiscriminator, DdsCase, DdsDefaultCase
- All sealed with validated constructors and AllowMultiple=false

Wrapper Types (FCDC-002):
- FixedString32/64/128 with inline UTF-8 storage and strict validation
- BoundedSeq<T> with List<T> backing for safe capacity enforcement
- AsUtf8Span() and AsSpan() for zero-copy access

Global Type Map (FCDC-003):
- Assembly-level DdsTypeMapAttribute for central type mappings
- DdsWire enum: Guid16, Int64TicksUtc, QuaternionF32x4, FixedUtf8BytesN

QoS and Errors (FCDC-004):
- DdsReliability, DdsDurability, DdsHistoryKind enums
- DdsException with encapsulated DdsReturnCode
- DdsSampleInfo stub (deferred to FCDC-015)

Testing:
- 55 tests covering validation, edge cases, and UTF-8 handling
- Reflection tests verify attribute retrieval correctness
- Boundary tests for capacity limits

Related: docs/FCDC-TASK-MASTER.md, tasks/FCDC-001.md
```

---

**Next Steps:** Developer should address 4 issues above and update report. Quick re-review expected (<30 min).
