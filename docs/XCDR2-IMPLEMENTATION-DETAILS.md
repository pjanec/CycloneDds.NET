# XCDR2 Implementation Details - Task Updates

**Date:** 2026-01-16  
**Source:** design-talk.md §3333-3836

## Summary

Extended FCDC-S010, FCDC-S011, FCDC-S012, and FCDC-S013 with critical byte-level XCDR2 implementation details that transform them from high-level descriptions into actionable, precise specifications.

---

## The Problem

The original task descriptions were high-level: "Generate Serialize method" or "Handle variable types". This **does not** specify the byte-level logic required to prevent stream corruption.

As noted in design-talk.md:
> "If you implemented the tasks as currently written, you would likely implement 'Classic CDR' (CDR1) by accident, which would fail to interop with modern Cyclone DDS."

---

## The "Devils" of XCDR2

Four specific devils that turn simple code into a nightmare:

### Devil #1: Absolute vs. Relative Alignment ("Shifting Struct")
- **Problem:** Alignment calculated from stream byte 0, not struct start
- **Impact:** Same struct at different offsets → different sizes
- **Solution:** `GetSerializedSize(int currentOffset)` propagates alignment state

### Devil #2: Nested DHEADER ("Russian Doll")
- **Problem:** Every appendable struct has DHEADER, nested headers depend on parent alignment
- **Impact:** Must perfectly simulate serialization to calculate DHEADER
- **Solution:** Two-pass architecture (CdrSizer then CdrWriter)

### Devil #3: Appendable Evolution Paradox
- **Problem:** Reader must skip unknown fields after reading known fields
- **Impact:** Without `Seek(endPos)`, stream desynchronizes by skipped bytes
- **Solution:** Generate `reader.Seek(endPos)` for all appendable types

### Devil #4: String Encoding Ambiguity
- **Problem:** XCDR1 vs XCDR2 string encoding differs, Cyclone behavior configuration-dependent
- **Impact:** Wrong NUL handling → byte mismatches
- **Solution:** Golden Rig (FCDC-S005) validates actual Cyclone behavior

---

## Changes Made to Tasks

### FCDC-S010: Serializer - Fixed Types (Extended from 4-5 days → 5-6 days)

**Added:**
1. **Alignment Formula** - Exact bitwise formula required
2. **Alignment Requirements Table** - 1/2/4/8 byte alignments by type
3. **DHEADER Logic** - ObjectSize - 4
4. **Two-Pass Architecture** - Introduced `CdrSizer` ref struct
5. **Symmetric Code Generation** - Single emit function for both passes
6. **AlignmentMath Helper** - Single source of truth for alignment
7. **Debug Safety Net** - Size mismatch assertion in DEBUG builds

**New Deliverables:**
- `Src/CycloneDDS.Core/AlignmentMath.cs`
- `Src/CycloneDDS.Core/CdrSizer.cs`
- Unit tests: `AlignmentMathTests.cs`

**Key Pattern:**
```csharp
// Pass 1: Simulate
var sizer = new CdrSizer(currentOffset);
sizer.WriteUInt32(0); // DHEADER placeholder
sizer.WriteInt32(this.Id);
return sizer.GetSizeDelta(currentOffset);

// Pass 2: Execute  
writer.WriteUInt32(totalSize - 4); // DHEADER
writer.WriteInt32(Id); // MUST match sizer calls
```

---

### FCDC-S011: Serializer - Variable Types (Extended from 5-6 days → 6-7 days)

**Added:**
1. **"Shifting Struct" Problem** - Explained absolute alignment propagation
2. **Example Calculation** - Same struct at offset 0 vs offset 5 → different sizes
3. **CdrSizer String Extension** - WriteString with UTF-8 counting
4. **Sequence Handling** - Header + element iteration with alignment
5. **Nested Struct Recursion** - Pass currentOffset through call chain
6. **String Encoding Details** - XCDR1 vs XCDR2 differences, Golden Rig validation

**New Deliverables:**
- Extended `CdrSizer.cs` (WriteString, WriteSequence)
- Unit tests: `SequenceSerializerTests.cs`
- Unit tests: `AlignmentPropagationTests.cs`

**Key Insight:**
```csharp
// Field: Mode (1 byte)
sizer.WriteByte(this.Mode);

// Field: Name (string, variable length)
sizer.WriteString(this.Name); // Advances cursor by length + NUL

// Field: Speed (8-byte aligned)
// Alignment depends on Name's actual length at runtime!
sizer.WriteDouble(this.Speed);
```

---

### FCDC-S012: Deserializer (Needs Update - Not Done Yet)

**Should Add:**
1. **DHEADER-based bounds checking** - `endPos = pos + DHEADER`
2. **Fast Path vs Robust Path** - Version match optimization
3. **Unknown field skipping** - `reader.Seek(endPos)` for evolution
4. **View struct zero-copy** - String views as `ReadOnlySpan<byte>`

---

### FCDC-S013: Unions (Needs Update - Not Done Yet)

**Should Add:**
1. **Discriminator-first serialization** - Always write discriminator
2. **Switch-based branch logic** - Only active arm serialized
3. **Alignment per arm** - Different arms → different sizes
4. **Union non-appendable** - Unions usually FINAL, not APPENDABLE

---

## Implementation Architecture

### Core Primitive: AlignmentMath (Single Source of Truth)

```csharp
public static class AlignmentMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Align(int currentPosition, int alignment)
    {
        int mask = alignment - 1;
        int padding = (alignment - (currentPosition & mask)) & mask;
        return currentPosition + padding;
    }
}
```

### Two-Pass Pattern (Ensures Coherency)

**CdrWriter** - Tracks `_totalWritten` (absolute stream position):
```csharp
public void WriteInt32(int value)
{
    int alignedPos = AlignmentMath.Align(_totalWritten, 4);
    int padding = alignedPos - _totalWritten;
    // ... write padding ...
    // ... write value ...
    _totalWritten += padding + 4; // Advance absolute position
}
```

**CdrSizer** - Mirrors CdrWriter API, writes nothing:
```csharp
public void WriteInt32(int value)
{
    _cursor = AlignmentMath.Align(_cursor, 4);
    _cursor += 4;
}
```

**Generated Code** - Identical call sequence guarantees coherency:
```csharp
GetSerializedSize(int offset) {
    var sizer = new CdrSizer(offset);
    sizer.WriteInt32(Id);   // Call 1
    sizer.WriteDouble(Val); // Call 2
    return sizer.GetSizeDelta(offset);
}

Serialize(ref writer) {
    writer.WriteInt32(Id);   // Call 1 (mirrors sizer)
    writer.WriteDouble(Val); // Call 2 (mirrors sizer)
}
```

---

## Why This Guarantees Correctness

1. **Shared Math** - `CdrWriter` and `CdrSizer` use `AlignmentMath.Align()` → cannot drift
2. **Symmetric Generation** - Same emit function generates both passes → field changes propagate
3. **Recursive Correctness** - `currentOffset` parameter propagates alignment state through nesting
4. **Debug Safety Net** - Assertion catches drift immediately in DEBUG builds

---

## Impact on Effort Estimates

| Task | Original | Updated | Reason |
|------|----------|---------|--------|
| FCDC-S010 | 4-5 days | 5-6 days | Added AlignmentMath, CdrSizer, symmetric generation |
| FCDC-S011 | 5-6 days | 6-7 days | Added alignment propagation, sequence handling |

**Total Additional:** ~2-3 days for foundational helpers that enable all future tasks.

---

## Next Actions

1. ✅ Updated FCDC-S010 with XCDR2 details
2. ✅ Updated FCDC-S011 with alignment propagation
3. ⏳ Update FCDC-S012 with deserializer DHEADER logic (should do this)
4. ⏳ Update FCDC-S013 with union serialization details (should do this)

---

## Reference

**Source:** design-talk.md Lines 3333-3836

**Key Sections:**
- §3350-3410: Alignment formula and DHEADER logic
- §3450-3484: Union handling
- §3488-3517: Sequence handling
- §3549-3568: The "Shifting Struct" problem (absolute alignment)
- §3570-3590: Nested DHEADER "Russian Doll"
- §3592-3606: Appendable evolution skip logic
- §3608-3616: String encoding ambiguity
- §3638-3834: Perfect stream offset model architecture

All details are now encoded into actionable task descriptions that will prevent implementation bugs.
