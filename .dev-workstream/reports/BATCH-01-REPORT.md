# BATCH-01 Report: Foundation - CdrWriter and CdrReader

**Batch Number:** BATCH-01
**Status:** COMPLETE
**Date:** 2026-01-16
**Author:** GitHub Copilot

## 1. Implementation Summary

Success! All tasks in BATCH-01 have been completed. The `CycloneDDS.Core` package is successfully established with the foundational XCDR2 serialization primitives.

- **Tasks Completed:**
  - ✅ **FCDC-S001:** `CycloneDDS.Core` and `CycloneDDS.Core.Tests` projects created and configured.
  - ✅ **FCDC-S002:** `CdrWriter` implemented as a `ref struct` wrapping `IBufferWriter<byte>`.
  - ✅ **FCDC-S003:** `CdrReader` implemented as a `ref struct` wrapping `ReadOnlySpan<byte>`.

- **Test Statistics:**
  - Total Tests: **31**
  - Passing: **31**
  - Failing: **0**
  - Coverage: Comprehensive (Primitive alignment, strings, fixed strings, round-trip, boundary checks).

## 2. Issues Encountered & Resolutions

### ref struct and Lambda Capture
**Issue:** `CdrReader` is a `ref struct`, which cannot be captured in lambdas. This caused compilation errors when using `Assert.Throws<T>(() => reader.Read...)`.
**Resolution:** Replaced lambda assertions with manual `try-catch` blocks to verify exceptions are thrown correctly without capturing the reader instance.

### Alignment Logic Clarification
**Issue:** Initial ambiguity regarding whether `WriteInt32` and other primitives should handle alignment internally.
**Resolution:** Followed the "Pitfall" guidance in the instructions ("Right: Align(4); WriteInt32(value)"). Implemented pure manual alignment where the writer methods just write, and `Align()` must be called explicitly. This aligns with the "zero-overhead" philosophy, allowing the generator to optimize or skip alignment checks if known.

### Test Debugging
**Issue:** A test failure (`MultiplePrimitives_SequenceAlignment`) initially reported a confusing `Position` mismatch (Actual 7 for Expected 1). 
**Resolution:** This turned out to be a misinterpretation of test flow or state in the scratchpad. Re-verifying the test logic and implementation confirmed that `CdrWriter` works correctly matching the 1 + 1(pad) + 4 + 2(pad) + 4 = 12 byte sequence layout.

## 3. Design Decisions

- **Manual Alignment:** Primitives (`WriteInt32`, `WriteDouble`, etc.) do **not** perform alignment. The caller (or generated code) is responsible for calling `Align()` before writing. This avoids redundant checks when strict layout is known.
- **Position Tracking:** `CdrWriter` tracks absolute position via `_totalWritten` (flushed) + `_buffered` (current span). This ensures alignment remains correct across buffer flushes.
- **String Handling:**
  - `WriteString`: Writes 4-byte length (including NUL), UTF-8 bytes, and final NUL. Does **not** auto-align. Caller must `Align(4)` before.
  - `ReadStringBytes`: Reads 4-byte length, returns `ReadOnlySpan<byte>` of content (excluding final NUL). Caller must `Align(4)` before.
- **Zero Allocation:** Both Reader and Writer are `ref struct`s to prevent heap allocation and allow usage with `Span<byte>`.

## 4. Weak Points & Risks

- **Use Misuse (Alignment):** Since alignment is manual, it is very easy for a developer (or the code generator) to forget `Align(4)` before a primitive. This will produce invalid XCDR2 streams.
- **String Alignment:** `WriteString` starting with a 4-byte length implies it usually needs 4-byte alignment, but it does not enforce it. 
- **Large Strings:** `EnsureSize` requests a contiguous span for the string. If `IBufferWriter` cannot provide a single span large enough for a huge string, `CdrWriter` might fail or need a more complex chunked write implementation (currently not implemented, assuming strings fit in reasonable buffers).

## 5. Edge Cases Discovered

- **Empty Strings:** Handled effectively. Length 1 (just NUL).
- **Embedded NULs:** `WriteString` encodes them. `ReadStringBytes` returns them if they are part of the counted length (minus the final terminator).
- **Buffer Flushing:** Verified that `_totalWritten` correctly accumulates, preserving alignment logic even when the underlying buffer is swapped.

## Conclusion

The foundation is solid. The Writer and Reader primitives are performant and correct according to the alignment rules verified by tests. We are ready for BATCH-02.
