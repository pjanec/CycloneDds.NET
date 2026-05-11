# ME2-BATCH-03 Report

**Batch:** ME2-BATCH-03  
**Tasks:** ME2-T20 (Tech Debt), ME2-T16, ME2-T17, ME2-T18, ME2-T19  
**Status:** ✅ COMPLETE  
**Test Results:** 387 passed, 0 failed (11 new tests added; all 376 prior tests retained)

---

## Q1 — Implementation Summary

All five tasks completed:

### ME2-T20 (Tech Debt) — `ApplySortToViewCache` Determinism for Replay Mode

`ApplySortToViewCache` previously assumed `_viewCache` was always in ascending ordinal order before calling `List.Reverse()` for descending sort. This invariant holds for the live `ISampleStore` (samples are appended monotonically) but is not guaranteed for `FixedSamples` (Replay mode), where the caller-provided list may have any ordinal ordering.

**Fix:** Added an `IsCacheAscending(FieldMetadata)` guard method that performs a single O(N) validation pass. The O(N) fast path (Reverse) is only taken when `FixedSamples == null` (live mode) or when `IsCacheAscending` confirms the cache is already ascending. If it is not, the code falls through to the general O(N log N) `List.Sort`. The live-mode path is unchanged — the `FixedSamples == null` early-exit bypasses the validation entirely, preserving zero overhead for the common case.

### ME2-T16 — Null String Representation in SamplesPanel

When a string field value is `null` in the payload (set via the `DynamicForm` nullable checkbox), `GetFieldValue` correctly returns `null`. However, `RenderValue` in `SamplesPanel` previously fell through to `FormatValue(null)` which returns `string.Empty`, rendering the cell as blank — identical to an empty string `""`.

**Fix:** Added an explicit `null` branch to `SamplesPanel.RenderValue` that renders a styled `<span class="samples-panel__value is-null">null</span>`, matching the DetailPanel pattern from ME2-T05. Added `.samples-panel__value.is-null { color: #f28ba8; font-style: italic; }` to `app.css`. Null and `""` are now visually distinct in the column grid.

**Note on DDS wire-level semantics:** CDR serialization does not carry a distinct null vs. empty-string encoding. A C# null string becomes `IntPtr.Zero` in the native struct; CycloneDDS serializes this as a zero-length CDR string `""`. The received sample therefore contains `""` not `null`. The fix addresses the in-process display layer (before round-trip and for any null value reaching the field getter), which is where the task's "retain the null intent" requirement applies.

### ME2-T17 — Filter Box `[x]` Clear Button

Added a conditionally-rendered `<button class="samples-panel__filter-clear">` immediately to the right of the filter `<input>`. The button shows only when `_filterText` is non-empty, displays a 12×12 SVG cross icon in red, and invokes the new `ClearFilter()` method which resets `_filterText`, `_filterError`, calls `ApplyFilter()`, and persists state. CSS class `.samples-panel__filter-clear` provides the circular hover indicator.

### ME2-T18 — Column Configuration Persistence and Reset

Two sub-tasks:

1. **Default columns reduced to `Topic` + `Timestamp`:** `AddDefaultSelectedColumns()` now only seeds `Topic` and `Timestamp` (the incoming reception timestamp from `SampleData.Timestamp`, not the source telemetry stamp). `Size [B]` and `Delay [ms]` are removed from defaults. The existing workspace persistence layer (`SavePanelState`/`LoadPanelState` via `PanelState.ComponentState`) was already correctly wired to push/pull `SelectedColumnsStateKey` — no structural changes were needed there.

2. **"Reset" button in ColumnPickerDialog:** Added an `OnReset` callback parameter to `ColumnPickerDialog.razor` and a "Reset" button in the dialog footer (left-of-Cancel). In `SamplesPanel`, the new `ResetColumns()` method handles this: clears `_selectedColumns`, calls `AddDefaultSelectedColumns()`, rebuilds layout columns, saves state, and closes the picker. This gives users a one-click path back to the `Topic`+`Timestamp` baseline without needing to manually remove all columns and re-add defaults.

### ME2-T19 — Delay Column Timing Arithmetic

**Root cause:** The `delayGetter` lambda in `TopicMetadata.AppendSyntheticFields` called `new DateTime(sample.SampleInfo.SourceTimestamp, DateTimeKind.Utc)`. This constructor interprets the argument as .NET ticks (100-nanosecond intervals since 0001-01-01). However, `DdsSampleInfo.SourceTimestamp` is nanoseconds-since-Unix-epoch (identical unit used by `DetailPanel.FormatSourceTimestamp`). A real DDS SourceTimestamp of ~1.77 × 10⁹ nanoseconds (March 2026) passed as .NET ticks represents approximately year 5621, placing it ~3600 years ahead of the receive time. The subtraction `sample.Timestamp − sourceTimestamp` thereby yields a large negative value.

**Fix:** Changed the conversion to `DateTime.UnixEpoch.AddTicks(sourceTimestampNs / 100)`, matching the identical approach used in `DetailPanel.FormatSourceTimestamp`. Added guards for `sourceTimestampNs <= 0` and `sourceTimestampNs == long.MaxValue` (returns 0.0 for "not set" entries), consistent with the detail panel convention.

**Updated existing test:** `SyntheticField_DelayGetter_ComputesCorrectly` in `TopicMetadataTests.cs` was also using the wrong unit (passing `sourceTime.Ticks` when the field expects nanoseconds-since-epoch). Fixed to compute `sourceTimestampNs = (sourceTime.Ticks - DateTime.UnixEpoch.Ticks) * 100L` before constructing the test sample.

---

## Q2 — Delay Arithmetic Detail

Old code (wrong):
```csharp
var sourceTimestamp = new DateTime(sample.SampleInfo.SourceTimestamp, DateTimeKind.Utc);
// SourceTimestamp = 1,773,826,775,412,144,300 ns ≈ March 2026
// Treated as ticks → DateTime("17738267754121443 * 100ns") ≈ year 5621
// sample.Timestamp ≈ 2026
// Delay = 2026 - 5621 = -3595 years → -1.89 × 10¹² ms
```

New code (correct):
```csharp
var sourceTimestamp = DateTime.UnixEpoch.AddTicks(sourceTimestampNs / 100);
// 1,773,826,775,412,144,300 / 100 = 17,738,267,754,121,443 ticks since 1970
// + 621,355,968,000,000,000 ticks (unix epoch offset) ≈ 2026-03-19
// Delay = receiveTime - sourceTime = positive milliseconds
```

The division by 100 converts nanoseconds to 100-ns ticks. `DateTime.UnixEpoch` (= 1970-01-01 00:00:00 UTC) is the reference point. For a loopback send/receive pair on the same machine, the result is a small positive number of milliseconds (network + processing latency).

---

## Q3 — Sort Determinism Design Choices

The `IsCacheAscending` validation pass was placed inside `ApplySortToViewCache` rather than inside the caller (`EnsureView`) because:
- It is a localised invariant check for a specific sort optimisation — keeping it co-located with the fast path makes future readers immediately understand the precondition.
- `EnsureView` has three branches; modifying each caller would scatter the guard.
- The O(N) validation only runs in Replay mode (`FixedSamples != null`) and only for Ordinal/Timestamp sort fields. In practice it is nearly free.

For live mode, `FixedSamples == null` short-circuits to the existing no-validation path, so there is no runtime regression on the hot path.

---

## Q4 — Null String Serialization Rationale

The `null` display fix operates at the .NET metadata getter level, not at the DDS wire level. The reason for this separation:

- DDS CDR serialization has no `null` string: the minimum valid CDR string is one byte (the null terminator). CycloneDDS maps `IntPtr.Zero` (returned by `arena.CreateString(null)`) to an empty string in the CDR stream.
- Consequently, a round-tripped "null" string always arrives as `""` in the monitor. There is no reliable way to distinguish send-side null from a genuine `""` after the DDS transit.
- The correct fix is at the **display** layer: the in-process representation (before or outside DDS transit) correctly holds C# `null`, and `RenderValue` must display it distinctly. This mirrors the DetailPanel's approach established in ME2-T05.
- For JSON export, `System.Text.Json` with `IncludeFields = true` already serializes C# `null` as JSON `null` and C# `""` as JSON `""` — no converter change was needed.

---

## Q5 — Test Coverage

11 new tests in `ME2Batch03Tests.cs`:

- `DelayGetter_PositiveDelay_WhenReceiveAfterSend` — verifies ~42.5 ms delay computed correctly from nanoseconds epoch.
- `DelayGetter_Returns0_WhenSourceTimestampIsZero` — verifies zero-guard for absent timestamp.
- `DelayGetter_Returns0_WhenSourceTimestampIsMaxValue` — verifies sentinel-guard.
- `DelayGetter_OldBugWouldProduceNegativeValue_CorrectCodeProducesPositive` — regression test using a realistic 2026 nanosecond timestamp; confirms result is positive ~1 ms.
- `IsCacheAscending_ReturnsFalse_WhenSamplesOutOfOrder` — white-box validation of the guard path for out-of-order ordinals.
- `IsCacheAscending_ReturnsTrue_WhenSamplesAreAscending` — confirms fast-path is enabled for correctly ordered data.
- `FieldMetadata_Getter_ReturnsNull_WhenStringFieldIsNull` — null string field getter returns `null` (not `""`).
- `FieldMetadata_Getter_ReturnsEmpty_WhenStringFieldIsEmpty` — empty string getter returns `""` (not null).
- `TopicMetadata_TopicAndTimestamp_PresentAsSyntheticFields` — confirms the two ME2-T18 default column fields exist and are wrapper fields.
- `TopicSyntheticField_Getter_ReturnsShortName` — Topic getter returns the correct ShortName value.
- `TimestampSyntheticField_Getter_ReturnsReceptionTime` — Timestamp getter returns reception time (not source timestamp).

Total: 387 tests, 0 failures.
