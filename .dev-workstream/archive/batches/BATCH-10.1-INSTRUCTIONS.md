# BATCH-10.1: Fix EMHEADER Format (CRITICAL)

**Batch Number:** BATCH-10.1 (Corrective - CRITICAL)  
**Parent:** BATCH-10  
**Tasks:** Fix EMHEADER bit layout to comply with XCDR2 specification  
**Phase:** Stage 2 - Code Generation (Critical Bug Fix)  
**Estimated Effort:** 1-2 hours  
**Priority:** 🔴 **CRITICAL BLOCKING**  
**Dependencies:** BATCH-10 (incorrect implementation)

---

## ⚠️⚠️⚠️ CRITICAL BUG FIX ⚠️⚠️⚠️

**ISSUE:** EMHEADER format violates XCDR2 specification - wrong bit layout causes interop failures.

**IMPACT:** Current implementation will NOT work with Cyclone DDS C code or any XCDR2-compliant system.

**FIX COMPLEXITY:** Simple (2 characters changed: `16` → `3`)

**VERIFICATION:** All 117+ tests must pass after fix.

---

## 📋 What Went Wrong

### Current Implementation (INCORRECT):

**File:** `tools/CycloneDDS.CodeGen/SerializerEmitter.cs`, **Line 310**

```csharp
uint emHeader = ((uint)emBodyLen << 16) | (uint)fieldId;
//                                   ^^
//                                   WRONG! Should be 3, not 16
```

**Result:** EMHEADER = `0x00040001` for 4-byte int with ID=1

**Bit layout (current - WRONG):**
```
Bits:  31-16           15-0
      [LENGTH]    [MEMBER_ID]
      0x0004      0x0001
```

### XCDR2 Specification Requires:

**EMHEADER format (32 bits, little-endian):**
```
Bits:  31   30-3                    2-0
      [M] [LENGTH (28 bits)]  [MEMBER_ID (3 bits)]
```

**M** = Must Understand flag (always 0 for `@appendable`)  
**LENGTH** = Size of member value in bytes  
**MEMBER_ID** = 0-7 (only 3 bits, for appendable usually 0)

**Correct calculation:**
```csharp
uint emHeader = ((uint)emBodyLen << 3) | (uint)(fieldId & 0x7);
//                                   ^              mask to 3 bits
//                                   Shift LEFT by 3 to position length in bits 30-3
```

**Result:** EMHEADER = `0x00000021` for 4-byte int with ID=1

**Bit layout (correct):**
```
Bits:  31  30-3        2-0
      [M] [LENGTH]    [ID]
      0   0x20 (32)    1

Binary: 0000 0000 0000 0000 0000 0000 0010 0001
        ^M  [      32 (4<<3)        ]  ^ID=1
```

---

## ✅ Task 1: Fix EMHEADER Calculation

### Step 1.1: Open SerializerEmitter.cs

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\SerializerEmitter.cs`

**Navigate to line 310** (in `EmitOptionalSerializer` method)

### Step 1.2: Change ONE number

**Find this line (line 310):**
```csharp
uint emHeader = ((uint)emBodyLen << 16) | (uint)fieldId;
```

**Change to:**
```csharp
uint emHeader = ((uint)emBodyLen << 3) | (uint)(fieldId & 0x7);
```

**Changes:**
1. `16` → `3` (shift length to bits 30-3, not 31-16)
2. `fieldId` → `(fieldId & 0x7)` (ensure only 3 bits used for ID)

**That's it for the fix!** Only 2 characters changed.

### Step 1.3: Add clarifying comment

**Above line 310, add:**
```csharp
// XCDR2 EMHEADER format: [M:1bit][Length:28bits][ID:3bits]
// M=0 for appendable, Length in bits 30-3, ID in bits 2-0
uint emHeader = ((uint)emBodyLen << 3) | (uint)(fieldId & 0x7);
```

**✅ CHECKPOINT:** SerializerEmitter.cs fixed.

---

## ✅ Task 2: Fix Test Expectations

### Step 2.1: Open OptionalTests.cs

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.CodeGen.Tests\OptionalTests.cs`

### Step 2.2: Fix Test 1 - Line 106

**Find:**
```csharp
Assert.Equal(0x00040001, (int)BitConverter.ToUInt32(bytes, 8));
```

**Change to:**
```csharp
// EMHEADER for 4-byte int with ID=1: (4 << 3) | 1 = 0x21
Assert.Equal(0x00000021, (int)BitConverter.ToUInt32(bytes, 8));
```

### Step 2.3: Fix Test 2 - Line 183

**Find:**
```csharp
Assert.Equal(0x000A0003, (int)BitConverter.ToUInt32(bytes, 8));
```

**This test is for optional string "Hello" (6 bytes: 4-byte length + 5 chars + 1 NUL = 10 bytes total)**

**Wait - need to calculate correct EMHEADER:**
- String "Hello" serialized = 4 (length) + 5 (chars) + 1 (NUL) = 10 bytes
- But EMHEADER.length should be body only (after EMHEADER itself)
- So length = 10 bytes
- ID = 3 (third optional field: Id=required, OptInt=1, OptDouble=2, OptString=3)
- EMHEADER = `(10 << 3) | 3 = 0x53`

**Change to:**
```csharp
// EMHEADER for 10-byte string with ID=3: (10 << 3) | 3 = 0x53
Assert.Equal(0x00000053, (int)BitConverter.ToUInt32(bytes, 8));
```

**✅ CHECKPOINT:** Test expectations fixed.

---

## ✅ Task 3: Add EMHEADER Bit Layout Test

### Step 3.1: Add new test to OptionalTests.cs

**At the end of the class (before closing brace), add:**

```csharp
[Fact]
public void EMHEADER_BitLayout_FollowsXCDR2Spec()
{
    var type = CreateOptionalType();
    var serializerCode = new SerializerEmitter().EmitSerializer(type);
    var deserializerCode = new DeserializerEmitter().EmitDeserializer(type);
    var harnessCode = @"
using System;
using System.Buffers;
using CycloneDDS.Core;
using OptionalTests;

public class Harness
{
    public static byte[] Serialize(int id, int? optInt, double? optDouble, string optString)
    {
        var data = new OptionalData();
        data.Id = id;
        data.OptInt = optInt;
        data.OptDouble = optDouble;
        data.OptString = optString;

        var writer = new ArrayBufferWriter<byte>();
        var cdr = new CdrWriter(writer);
        data.Serialize(ref cdr);
        cdr.Complete();
        return writer.WrittenSpan.ToArray();
    }
}";
    var assembly = CompileToAssembly(serializerCode, deserializerCode, GetStructDef(), harnessCode);
    var harness = assembly.GetType("Harness");
    
    byte[] bytes = (byte[])harness.GetMethod("Serialize").Invoke(null, new object[] { 100, (int?)42, (double?)null, (string?)null });
    
    // EMHEADER at offset 8 (after DHEADER + Id)
    uint emheader = BitConverter.ToUInt32(bytes, 8);
    
    // XCDR2 EMHEADER bit layout: [M:1bit][Length:28bits][ID:3bits]
    uint mustUnderstand = (emheader >> 31) & 0x1;      // Bit 31
    uint length = (emheader >> 3) & 0x0FFFFFFF;        // Bits 30-3
    uint memberId = emheader & 0x7;                    // Bits 2-0
    
    // Verify bit fields
    Assert.Equal(0u, mustUnderstand);  // Appendable types have M=0
    Assert.Equal(4u, length);          // int is 4 bytes
    Assert.Equal(1u, memberId);        // First optional field gets ID=1
    
    // Verify complete EMHEADER value
    Assert.Equal(0x00000021u, emheader); // (4 << 3) | 1
}
```

**✅ CHECKPOINT:** Bit layout test added.

---

## ✅ Task 4: Verify Fix

### Step 4.1: Run all tests

**Command:**
```cmd
cd d:\Work\FastCycloneDdsCsharpBindings
dotnet test
```

**EXPECTED OUTPUT:**
```
Test summary: total: 118; failed: 0; succeeded: 118; skipped: 0;
```

**If tests fail:**
1. Check that all test expectations were updated correctly
2. Verify EMHEADER calculation uses `<< 3` not `<< 16`
3. Copy full error output to report

**✅ CHECKPOINT:** All 118 tests passing.

---

## ✅ Task 5: Verify EMHEADER Examples

**Create a simple manual verification:**

**Calculate EMHEADER values for common cases:**

| Field Type | Bytes | ID | Calculation | EMHEADER (hex) |
|------------|-------|----|----|----------------|
| `int?` | 4 | 0 | `(4 << 3) \| 0` | `0x00000020` |
| `int?` | 4 | 1 | `(4 << 3) \| 1` | `0x00000021` |
| `double?` | 8 | 0 | `(8 << 3) \| 0` | `0x00000040` |
| `double?` | 8 | 2 | `(8 << 3) \| 2` | `0x00000042` |
| `string "Hi"` | 7 | 0 | `(7 << 3) \| 0` | `0x00000038` |

**Add these to your report as verification.**

---

## 📊 Report Requirements

**Submit to:** `.dev-workstream/reports/BATCH-10.1-REPORT.md`

**Required Sections:**

1. **Fix Applied**
   - Screenshot or code snippet showing line 310 change
   - Before: `<< 16`
   - After: `<< 3`

2. **Test Updates**
   - List of test lines updated with new expectations
   - EMHEADER values table (from Task 5)

3. **Test Results**
   - **MUST INCLUDE:** Full `dotnet test` output
   - **MUST SHOW:** 118 tests passing

4. **EMHEADER Calculation Examples**
   - Verify at least 3 examples manually
   - Show calculation and result

---

## 🎯 Success Criteria

This batch is DONE when:

- ✅ Line 310 fixed: `<< 3` instead of `<< 16`
- ✅ Test expectations updated (lines 106, 183)
- ✅ Bit layout test added
- ✅ **ALL 118 tests passing**
- ✅ Report submitted with full test output

**BLOCKING:** Any test failure blocks completion.

---

## ⚠️ Common Pitfalls

1. **Forgetting `& 0x7` mask:**
   - Member ID is only 3 bits (0-7)
   - Must mask to prevent overflow

2. **Not updating ALL test expectations:**
   - Must update EVERY assertion that checks EMHEADER bytes
   - Search for "0x0004" in OptionalTests.cs

3. **Calculating string EMHEADER wrong:**
   - String length = 4 (len header) + strlen + 1 (NUL)
   - "Hello" = 4 + 5 + 1 = 10 bytes total

---

## 📚 Reference

**XCDR2 Specification:** OMG XTypes 1.3, Section 7.4.3.4.3

**EMHEADER Bit Layout:**
```
 31  30                           3   2   1   0
[M] [         LENGTH (28 bits)        ] [ID(3)]
```

**Example Calculation (4-byte int with ID=1):**
```
Length = 4 bytes
ID = 1

Step 1: Shift length left by 3:  4 << 3 = 32 = 0x00000020
Step 2: OR with ID:              32 | 1 = 33 = 0x00000021

Binary: 0000 0000 0000 0000 0000 0000 0010 0001
        ^M=0 [        32          ]   ^ID=1
        bit31 [     bits 30-3     ]  bits 2-0
```

---

**Estimated Time:** 1-2 hours (mostly testing and verification)

**This is a CRITICAL fix.** Without it, C# cannot communicate with Cyclone DDS C nodes!
