# BATCH-01.1: Foundation Schema Package - Corrections

**Batch Number:** BATCH-01.1 (Corrective)  
**Parent Batch:** BATCH-01  
**Tasks:** Fixes for FCDC-002 (FixedString UTF-8 boundary handling)  
**Estimated Effort:** 1-2 hours  
**Priority:** HIGH (Corrective)  
**Dependencies:** BATCH-01

---

## 📋 Onboarding & Workflow

### Background

This is a **corrective batch** addressing issues found in BATCH-01 review.

**Original Batch:** `.dev-workstream/batches/BATCH-01-INSTRUCTIONS.md`  
**Review with Issues:** `.dev-workstream/reviews/BATCH-01-REVIEW.md`

Read both before starting.

### Report Submission

**When done, update:**  
`.dev-workstream/reports/BATCH-01-REPORT.md` (add "Corrections" section)

OR create new:  
`.dev-workstream/reports/BATCH-01.1-REPORT.md`

---

## 🎯 Objectives

Fix 4 issues from BATCH-01 review:

1. **Issue 1:** UTF-8 multi-byte character truncation bug (FixedString)
2. **Issue 2:** Missing test coverage for multi-byte UTF-8 boundary
3. **Issue 3:** Undocumented struct copy semantics risk (BoundedSeq)
4. **Issue 4:** Missing capacity constant tests

---

## ✅ Tasks

### Task 1: Fix UTF-8 Truncation Bug

**Files to Modify:**
- `src/CycloneDDS.Schema/WrapperTypes/FixedString32.cs`
- `src/CycloneDDS.Schema/WrapperTypes/FixedString64.cs`
- `src/CycloneDDS.Schema/WrapperTypes/FixedString128.cs`

**Problem:**
Current `Length` property counts bytes until NUL but doesn't ensure valid UTF-8 boundary. Could return partially-truncated multi-byte character.

**Solution Options:**

**Option A (Recommended):** Store length in field
```csharp
private fixed byte _buffer[32];
private int _length; // Actual stored byte count

public int Length => _length;

// Update TryFrom to set _length after GetBytes
// Update AsUtf8Span to use _length instead of scanning
```

**Option B:** Validate on construction
- Check buffer[length-1] isn't UTF-8 continuation byte
- More complex, doesn't fully solve issue

**Choose Option A.** Simpler and more robust.

**Changes Required:**
1. Add `private int _length;` field to all FixedStringN types
2. Update `TryFrom` to set `_length = byteCount` after successful write
3. Update constructor to set `_length` via TryFrom
4. Update `Length` property to `get => _length;`
5. Update `AsUtf8Span()` to return `new ReadOnlySpan<byte>(ptr, _length);`

**Tests Must Still Pass:** All existing FixedString tests

---

### Task 2: Add Multi-Byte UTF-8 Boundary Test

**File to Modify:**
- `tests/CycloneDDS.Schema.Tests/FixedStringTests.cs`

**Add Test:**
```csharp
[Fact]
public void FixedString32_MultiByteAtBoundary_Rejects()
{
    // 30 ASCII chars (30 bytes) + ü (2 bytes) = 32 bytes total (fits)
    string validAt32 = new string('a', 30) + "ü";
    Assert.True(FixedString32.TryFrom(validAt32, out var fs));
    Assert.Equal(32, fs.Length);
    
    // 30 ASCII chars (30 bytes) + € (3 bytes) = 33 bytes total (exceeds)
    string invalidAt33 = new string('a', 30) + "€";
    Assert.False(FixedString32.TryFrom(invalidAt33, out _));
    
    // 29 ASCII + 3-byte emoji = 32 bytes exactly
    string emojiAt32 = new string('a', 29) + "😀";
    Assert.True(FixedString32.TryFrom(emojiAt32, out var fs2));
    Assert.Equal(32, fs2.Length);
    Assert.Equal(emojiAt32, fs2.ToStringAllocated());
}
```

**Why This Matters:** Verifies byte-counting logic correctly rejects strings that exceed capacity due to multi-byte encoding.

---

### Task 3: Add BoundedSeq Copy Semantics Warning

**File to Modify:**
- `src/CycloneDDS.Schema/WrapperTypes/BoundedSeq.cs`

**Update XML doc:**
```csharp
/// <summary>
/// A bounded sequence of items with a fixed maximum capacity.
/// <para><b>WARNING:</b> This is a struct wrapping a reference type. 
/// Copying the struct creates a shallow copy that shares the underlying storage.
/// Mutations to the copied struct will affect the original.</para>
/// </summary>
/// <typeparam name="T">The type of elements in the sequence.</typeparam>
public struct BoundedSeq<T> : IEnumerable<T>
```

**No functional change,** just documentation.

---

### Task 4: Add Capacity Constant Tests

**File to Modify:**
- `tests/CycloneDDS.Schema.Tests/FixedStringTests.cs`

**Add Test:**
```csharp
[Fact]
public void FixedString_CapacityConstants_Correct()
{
    Assert.Equal(32, FixedString32.Capacity);
    Assert.Equal(64, FixedString64.Capacity);
    Assert.Equal(128, FixedString128.Capacity);
}
```

---

## 🧪 Testing Requirements

**All Previous Tests Must Pass:** 55 tests from BATCH-01  
**New Tests:** +2 (multi-byte boundary test + capacity constants test)  
**Total Expected:** 57+ tests passing

**Run:**
```bash
dotnet test tests/CycloneDDS.Schema.Tests/CycloneDDS.Schema.Tests.csproj
```

Verify zero failures.

---

## 📊 Report Requirements

**Update:** `.dev-workstream/reports/BATCH-01-REPORT.md`

**Add Section:**
```markdown
## 7. BATCH-01.1 Corrections

### Issue 1: UTF-8 Truncation Fix
**Solution Chosen:** Option A - Store length in field
**Rationale:** [Your explanation]
**Changes:** [Files modified]
**Test Impact:** [Describe any test changes]

### Issue 2: Multi-Byte Boundary Test
**Added:** [Describe test]
**Verified:** [What it confirms]

### Issue 3: BoundedSeq Documentation
**Added:** XML warning about struct copy semantics

### Issue 4: Capacity Constants Test
**Added:** Test verifying Capacity properties

### Final Test Count
**Total:** 57 tests (55 original + 2 new)
**Passing:** 57 (100%)
```

---

## 🎯 Success Criteria

- [ ] All 4 issues from review addressed
- [ ] FixedString types now store length in field (not computed)
- [ ] Multi-byte boundary test added and passing
- [ ] BoundedSeq XML doc includes copy semantics warning
- [ ] Capacity constants test added and passing
- [ ] All 57+ tests passing
- [ ] Zero compiler warnings
- [ ] Report updated with corrections section

---

## ⚠️ Critical Note

**Struct Layout Change:** Adding `_length` field changes FixedString layout. This is ACCEPTABLE because:
- Phase 1 is schema-only (user-facing types)
- Native blittable layout is generated in Phase 2
- No interop at this layer

**Ensure:** All three FixedString types get identical treatment.

---

**Report to:** Update existing BATCH-01-REPORT.md with corrections section (or create BATCH-01.1-REPORT.md)

**This should be quick (~1-2 hours). Focus on correctness, not perfection.**
