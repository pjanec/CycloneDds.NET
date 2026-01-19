# BATCH-09.1: Union Wire Format Verification

## Objective
Verify the wire format of XCDR2 Unions, specifically the presence of DHEADER, by running native C code using Cyclone DDS.

## Methodology
1. Created `UnionTest.idl` with a simple union:
   ```idl
   @appendable
   union TestUnion switch(long) {
       case 1: long valueA;
       case 2: double valueB;
   };
   ```
2. Generated C code using `idlc`.
3. Wrote `test_union_basic.c` to serialize `TestUnion` with `_d=1` and `valueA=0xDEADBEEF` using `dds_stream_write_sample` (native CDR stream API).
4. Compiled and executed against `ddsc.dll` (v0.11.0).

## Results

**Output:**
```
HEX DUMP (12 bytes):
08 00 00 00 01 00 00 00 EF BE AD DE
DHEADER (Raw): 0x00000008
```

### Analysis:
- **Total Size:** 12 bytes.
- **Bytes 0-3:** `08 00 00 00` (Little Endian 0x08 = 8). This is the **DHEADER**.
- **Bytes 4-7:** `01 00 00 00` (Discriminator = 1).
- **Bytes 8-11:** `EF BE AD DE` (Payload `valueA` = 0xDEADBEEF).

### Conclusion:
- **DHEADER IS PRESENT** for `TestUnion`.
- The DHEADER value (8) includes the size of the Discriminator (4) + Payload (4).
- Structure: `[DHEADER] [DISCRIMINATOR] [PAYLOAD]`.

## Implication for C# Bindings
The `SerializerEmitter` must ensure that for `[DdsUnion]`, a DHEADER is emitted wrapping the discriminator and the selected member.
The `DeserializerEmitter` must read and validate this DHEADER (or skip it if just jumping over).

## Next Steps
- Verify `SerializerEmitter.cs` implements this DHEADER logic.
- Verify `DeserializerEmitter.cs` handles this DHEADER logic.
- Run C# tests to ensure they match this format.
