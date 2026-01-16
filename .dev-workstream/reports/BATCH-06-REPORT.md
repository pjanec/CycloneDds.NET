# BATCH-06 Report: Serializer Code Emitter - Fixed Types

**Date:** January 16, 2026
**Author:** GitHub Copilot

## 1. Implementation Summary

I have successfully implemented the Serializer Code Emitter for fixed-size types, fulfilling all requirements of FCDC-S010.

**Components Created/Modified:**
- **`tools/CycloneDDS.CodeGen/SerializerEmitter.cs`**: New class generating `Serialize` and `GetSerializedSize`.
- **`tools/CycloneDDS.CodeGen/TypeMapper.cs`**: New helper mapping C# types to CdrWriter method names.
- **`src/CycloneDDS.Core/CdrWriter.cs`**: Updated to support full set of primitives, auto-alignment logic, and `PatchUInt32`.
- **`src/CycloneDDS.Core/CdrSizer.cs`**: Updated to mirror `CdrWriter`, added `Skip` method for nested sizing.
- **`tools/CycloneDDS.CodeGen/CodeGenerator.cs`**: Integrated `SerializerEmitter` into the generation pipeline.
- **`tests/CycloneDDS.CodeGen.Tests/SerializerEmitterTests.cs`**: New test suite validating generation, compilation, and execution.

## 2. Generated Code Example

The generator produces symmetric code for `Serialize` and `GetSerializedSize`:

```csharp
public partial struct SimplePrimitive
{
    public int GetSerializedSize(int currentOffset)
    {
        var sizer = new CdrSizer(currentOffset);

        // DHEADER (required for @appendable)
        sizer.WriteUInt32(0);

        // Struct body
        sizer.WriteInt32(0); // Id
        sizer.WriteDouble(0); // Value

        return sizer.GetSizeDelta(currentOffset);
    }

    public void Serialize(ref CdrWriter writer)
    {
        // DHEADER
        int dheaderPos = writer.Position;
        writer.WriteUInt32(0);

        int bodyStart = writer.Position;

        // Struct body
        writer.WriteInt32(this.Id); // Id
        writer.WriteDouble(this.Value); // Value

        // Patch DHEADER
        int bodySize = writer.Position - bodyStart;
        writer.PatchUInt32(dheaderPos, (uint)bodySize);
    }
}
```

## 3. Key Design Decisions

### Reference Passing for CdrWriter
The `CdrWriter` is a `ref struct` with mutable state (`_buffered`). To ensure the `Serialize` method propagates buffer updates back to the caller (e.g., for `Complete()` call), I updated the signature to `void Serialize(ref CdrWriter writer)`. This deviates slightly from the initial instruction text but is required for correctness with mutable value types.

### Nested Structure Sizing
Since `GetSerializedSize` returns a size (int) rather than taking a sizer by reference (to modify its state), I implemented a `Skip(int bytes)` method on `CdrSizer`. The generated code uses this to advance the sizer's cursor based on the size returned by nested structs:
```csharp
sizer.Skip(default(NestedType).GetSerializedSize(sizer.Position));
```

### CdrWriter Patching
Implemented `PatchUInt32` in `CdrWriter`. It safely patches the buffer if the position is within the currently buffered span. This works reliably for DDS scenarios where messages are typically serialized into a contiguous buffer before flushing.

## 4. Validation Results

**Total Tests:** 1 passed (in new suite), 38 total in project.
**Golden Rig Validation:** PASSED.

The execution test `GeneratedCode_Serializes_MatchesGoldenRig` confirmed:
- **DHEADER Generation:** Correctly emitted (0x0C size for 16-byte struct minus 4 header bytes).
- **Little Endian Output:** Confirmed correct byte order for Int32 and Double.
- **Alignment:** Verification of primitives mapping to aligned offsets.
- **Symmentry:** `GetSerializedSize` returns 16, and `Serialize` produces 16 bytes.

```text
Expected: 0C 00 00 00 15 CD 5B 07 77 BE 9F 1A 2F DD 5E 40
Actual:   0C 00 00 00 15 CD 5B 07 77 BE 9F 1A 2F DD 5E 40
```
