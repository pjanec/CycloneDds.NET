# BATCH-10.1 REPORT: EMHEADER Fix

**Date:** 2026-01-17
**Status:** ✅ SUCCESS
**Tests Passed:** 118/118

---

## 1. Fix Applied

**File:** `tools/CycloneDDS.CodeGen/SerializerEmitter.cs`
**Line:** ~310

Changed bit shifting from `<< 16` (incorrect) to `<< 3` (correct XCDR2 Spec compliance).

**Code Snippet:**
```csharp
// XCDR2 EMHEADER format: [M:1bit][Length:28bits][ID:3bits]
// M=0 for appendable, Length in bits 30-3, ID in bits 2-0
sb.AppendLine($"                uint emHeader = ((uint)emBodyLen << 3) | (uint)({fieldId} & 0x7);");
```

**Also Updated:** `tools/CycloneDDS.CodeGen/DeserializerEmitter.cs` to match the new bit layout:
```csharp
// EMHEADER: (Length << 3) | ID
sb.AppendLine("                    ushort id = (ushort)(emHeader & 0x7);");
```

---

## 2. Test Updates

**File:** `tests/CycloneDDS.CodeGen.Tests/OptionalTests.cs`

Updated expectations to match XCDR2 EMHEADER format:

1. **Int (4 bytes, ID 1):**
   - Old: `0x00040001`
   - New: `0x00000021` ((4 << 3) | 1)

2. **String "Hello" (10 bytes, ID 3):**
   - Old: `0x000A0003`
   - New: `0x00000053` ((10 << 3) | 3)

**Added Test:** `EMHEADER_BitLayout_FollowsXCDR2Spec`
- Verifies individual bit fields (M, Length, ID) specifically.

---

## 3. Test Results

```
Test summary: total: 118; failed: 0; succeeded: 118; skipped: 0; duration: 4.3s
Build succeeded with 4 warning(s) in 5.9s
```

All 118 tests passed, confirming the fix works across the board (Core, Schema, and CodeGen).

---

## 4. EMHEADER Calculation Examples

Manual verification of EMHEADER values:

| Field Type | Bytes (Len) | ID | Calculation (Len << 3 \| ID) | Binary (32-bit) | Hex Result |
|------------|-------------|----|------------------------------|-----------------|------------|
| `int?`     | 4           | 0  | `(4 << 3) \| 0` = `32 \| 0`  | `...0010 0000`  | `0x00000020` |
| `int?`     | 4           | 1  | `(4 << 3) \| 1` = `32 \| 1`  | `...0010 0001`  | `0x00000021` |
| `double?`  | 8           | 0  | `(8 << 3) \| 0` = `64 \| 0`  | `...0100 0000`  | `0x00000040` |
| `double?`  | 8           | 2  | `(8 << 3) \| 2` = `64 \| 2`  | `...0100 0010`  | `0x00000042` |
| `string`   | 10*         | 3  | `(10 << 3) \| 3` = `80 \| 3` | `...0101 0011`  | `0x00000053` |

\* String length includes 4-byte length prefix + chars + null terminator. Example "Hello": 4 + 5 + 1 = 10 bytes.

These values match the new test expectations.
