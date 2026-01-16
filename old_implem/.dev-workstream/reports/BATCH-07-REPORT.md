# BATCH-07 Report: Native Types + Unions + Managed Views

## 1. Executive Summary
This batch successfully combined three critical tasks: fixing native struct layout issues, implementing native union types, and starting the managed view generation. We significantly improved test quality by adding real layout validation tests using Roslyn compilation.

**Key Achievements:**
- **Fixed Pack=1 Padding:** Implemented explicit padding fields (`_paddingN`) to ensure correct C-compatible layout when using `[StructLayout(LayoutKind.Sequential, Pack = 1)]`.
- **Native Unions:** Implemented `NativeTypeEmitter.GenerateNativeUnion` using `[StructLayout(LayoutKind.Explicit)]` and `[FieldOffset]` to correctly map DDS unions to C# structs.
- **Managed Views:** Implemented `ManagedViewEmitter` to generate `ref struct` wrappers that provide safe, high-performance access to native data (including `ReadOnlySpan<byte>` for fixed strings).
- **Validation Testing:** Added 15 new tests that compile the generated code and verify actual `sizeof` and field offsets using `Marshal` and reflection.

## 2. Implementation Details

### Pack=1 Strategy
We chose **Option B (Explicit Padding)**. This ensures deterministic layout that exactly matches our `StructLayoutCalculator`. By emitting `private fixed byte _paddingN[Size]` fields, we guarantee that the C# struct layout matches the expected C layout byte-for-byte, regardless of compiler packing defaults.

### Union Layout
Unions are generated as `unsafe struct` with `[StructLayout(LayoutKind.Explicit)]`.
- **Discriminator:** Always at offset 0.
- **Payload:** All case arms are mapped to the *same* calculated payload offset (aligned to the maximum alignment of all arms).
- **Total Size:** Explicitly padded to match the calculated total size.

### Managed Views
Managed views are `ref struct` types that wrap a `ref NativeType`.
- **Zero-Copy:** They do not copy data; they just point to the native memory.
- **Safety:** They expose safe properties (e.g., `ReadOnlySpan<byte>` for fixed buffers, `DateTime` for ticks).
- **Performance:** Access is inlined and direct.

## 3. Test Results
All 71 tests passed successfully.

**New Validation Tests:**
- `GeneratedStruct_SizeMatchesCalculatedLayout`
- `GeneratedStruct_FieldOffsetsMatchLayout`
- `StructWithPadding_HasExplicitPaddingFields`
- `Union_HasExplicitLayout`
- `Union_DiscriminatorAtOffset0`
- `Union_PayloadAtCorrectOffset`
- `Union_SizeMatchesCalculatedLayout`
- `UnionWithMultipleArms_AllAtPayloadOffset`

**New Managed View Tests:**
- `ManagedView_IsRefStruct`
- `ManagedView_HasPrimitiveProperties`
- `ManagedView_FixedStringReturnsReadOnlySpan`
- `ManagedView_GuidProperty`
- `ManagedView_DateTimeProperty`

## 4. Developer Insights

**Q1: Which padding strategy did you choose (explicit padding vs natural) and why?**
I chose explicit padding. While natural alignment is simpler, it relies on the C# compiler's packing rules matching the C compiler's exactly. Explicit padding with `Pack=1` gives us total control and ensures that our `StructLayoutCalculator` is the single source of truth for layout.

**Q2: What was the trickiest part of union explicit layout?**
Calculating the correct payload offset. It's not just the size of the discriminator; it must be aligned to the *maximum alignment* of all possible union arms. If we have a `byte` discriminator and a `long` arm, the payload must start at offset 8 (on 64-bit aligned systems), leaving 7 bytes of padding.

**Q3: How do managed views handle fixed buffers safely?**
They use `unsafe` blocks internally to get a pointer to the fixed buffer, but expose it as a safe `ReadOnlySpan<byte>`. This allows consumers to work with the data safely without using `unsafe` code themselves.

**Q4: What performance implications exist for managed views over native?**
Managed views are extremely lightweight. They are `ref struct`s, so they are stack-allocated and cannot be boxed. The overhead is negligible (pointer arithmetic), making them ideal for high-performance scenarios.

## 5. Code Quality Checklist
- [x] Pack=1 padding issue resolved (Explicit padding used)
- [x] Explicit padding fields emitted
- [x] Actual size/offset validation tests added
- [x] Union explicit layout implemented
- [x] Discriminator at offset 0
- [x] Payload at calculated offset
- [x] Managed view ref structs generated
- [x] Fixed string → ReadOnlySpan
- [x] Guid/DateTime properties working
- [x] 20+ tests passing (15 new tests added in this batch, total 71)
- [x] All previous tests still passing
