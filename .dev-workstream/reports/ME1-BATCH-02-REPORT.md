# Batch Report: ME1-BATCH-02

**Batch Number:** ME1-BATCH-02  
**Developer:** GitHub Copilot (Claude Sonnet 4.6)  
**Date Submitted:** 2025-07-10  
**Time Spent:** ~4 hours (multi-session, resumed from prior context)

---

## ✅ Completion Status

### Tasks Completed
- [x] Task 0 (Bug Fix): `IdlEmitter.cs` — 8-bit/16-bit enum union discriminator names
- [x] Task 0 (Bug Fix): `SerializerEmitter.cs` — discriminator marshal cast width
- [x] Task 0 (Bug Fix): `ViewEmitter.cs` — FixedString/FixedSizeBuffer/FixedArray union arm view generation
- [x] ME1-T04: StartsWith / EndsWith / Contains filter operators (parameterized `@N` notation)
- [x] ME1-T05: CLI-safe operator aliases (`ge`, `le`, `gt`, `lt`, `eq`, `ne`)
- [x] ME1-T06: Multi-participant DDS reception
- [x] ME1-T07: Global sample ordinal + participant stamping

**Overall Status:** COMPLETE

---

## 🧪 Test Results

### DdsMonitor.Engine.Tests
```
Passed!  - Failed: 0, Passed: 258, Skipped: 0, Total: 258
Duration: ~11s
```

### CycloneDDS.CodeGen.Tests
```
Passed!  - Failed: 0, Passed: 161, Skipped: 0, Total: 161
Duration: ~10s
```

**All 419 tests passing across both suites.**

---

## 📝 Implementation Summary

### Files Added
```
tools/DdsMonitor/DdsMonitor.Engine/OrdinalCounter.cs
    - Thread-safe monotonic counter (Interlocked-based)

tests/DdsMonitor.Engine.Tests/ME1Batch02Tests.cs
    - Comprehensive tests for T04, T05, T06, T07 (20 test cases)
```

### Files Modified
```
tools/CycloneDDS.CodeGen/IdlEmitter.cs
    - Task 0: Convert.ToInt32(val) instead of val is int iVal for enum discriminator labels
    - Restored ME1-T03 @topic(name=...) annotation (accidentally reverted; corrected)

tools/CycloneDDS.CodeGen/SerializerEmitter.cs
    - Task 0: Discriminator marshal cast uses EnumBitBound switch (byte/ushort/int)

tools/CycloneDDS.CodeGen/Emitters/ViewEmitter.cs
    - Task 0: EmitUnionProperty — added IsFixedString, IsFixedSizeBuffer, IsFixedArray branches
      before complex-type fallback (which was emitting invalid FixedString32_Native* types)

tools/DdsMonitor/DdsMonitor.Engine/Filtering/FilterNodes.cs
    - ME1-T04: BuildLinq(IList<object?> paramValues) signature; @N parameter notation for
      StartsWith/EndsWith/Contains; backward-compat ToDynamicLinqString() overload

tools/DdsMonitor/DdsMonitor.Engine/IFilterCompiler.cs
    - ME1-T04: New Compile(string, TopicMetadata?, IReadOnlyList<object?>?) overload

tools/DdsMonitor/DdsMonitor.Engine/FilterCompiler.cs
    - ME1-T04: New Compile overload with paramValues forwarded to ParseLambda
    - ME1-T05: NormalizeCliOperators() with word-boundary Regex replacements

tools/DdsMonitor/DdsMonitor.Engine/DdsSettings.cs
    - ME1-T06: ParticipantConfig class, Participants list, FilterExpression property

tools/DdsMonitor/DdsMonitor.Engine/IDdsBridge.cs
    - ME1-T06: Participants, IsPaused, AddParticipant, RemoveParticipant, ResetAll

tools/DdsMonitor/DdsMonitor.Engine/DdsBridge.cs
    - ME1-T06: Full multi-participant rewrite with IsPaused gating and ResetAll

tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs
    - ME1-T06: DI factory for DdsBridge, OrdinalCounter singleton, backward-compat DomainId

tools/DdsMonitor/DdsMonitor.Engine/Models/SampleData.cs
    - ME1-T07: Added DomainId, PartitionName, ParticipantIndex

tools/DdsMonitor/DdsMonitor.Engine/Import/SampleExportRecord.cs
    - ME1-T07: Added DomainId, PartitionName

tools/DdsMonitor/DdsMonitor.Engine/Export/ExportService.cs
    - ME1-T07: Serializes DomainId and PartitionName

tools/DdsMonitor/DdsMonitor.Engine/Import/ImportService.cs
    - ME1-T07: Deserializes DomainId and PartitionName

tools/DdsMonitor/DdsMonitor.Engine/Dynamic/DynamicReader.cs
    - ME1-T07: DynamicReaderConfig class; filter-first ordinal allocation; participant stamping

tools/DdsMonitor/DdsMonitor.Blazor/Components/FilterBuilderPanel.razor
    - ME1-T04: ApplyFilter collects paramValues; GetOperatorLabel shows friendly labels

tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor
    - ME1-T07: Sample Info tab shows Global Ordinal, Incoming Timestamp, Domain ID, Partition

tests/DdsMonitor.Engine.Tests/Batch24Tests.cs
    - Added missing IDdsBridge members (Participants, IsPaused, AddParticipant,
      RemoveParticipant, ResetAll) to FakeDdsBridge test stub
```

---

## 🎯 Implementation Details

### Task 0: Code Generator Bug Fixes

**Three distinct bugs were fixed:**

**Bug 1 — `IdlEmitter.cs` enum discriminator label resolution:**  
`val is int iVal` silently skipped `byte` and `short` backed enum discriminators (Roslyn returns narrow-typed values for `[EnumBitBound(8)]`/`[EnumBitBound(16)]` attributes). Changed to `Convert.ToInt32(val)` inside try/catch for `InvalidCastException` and `OverflowException`. This now correctly resolves member names like `case Ok:` instead of emitting `case 0:`.

**Bug 2 — `SerializerEmitter.cs` discriminator marshal cast width:**  
The generated marshal code for union discriminators always used `(int)` cast regardless of the backing type. For `byte`-backed enums the native `_d` field is `byte`, causing a narrowing conversion error (`int` to `byte`). Fixed by using `EnumBitBound switch { 8 => "(byte)", 16 => "(ushort)", _ => "(int)" }`.

**Bug 3 — `ViewEmitter.cs` union arm view generation for composite types:**  
`EmitUnionProperty` fell through to the complex struct handler for ALL non-primitive union arms, generating `FixedString32_Native*` (non-existent type) for `FixedString32` fields and `(float)...EightFloatsInline` (scalar cast) for `fixed float` buffers. Added dedicated branches in order:  
- `IsFixedString(caseType)` → `new FixedStringXxxView((FixedStringXxx*)&_ptr->...)`  
- `member.IsFixedSizeBuffer` → `new ReadOnlySpan<T>(_ptr->..., N)`  
- `IsFixedArray(member)` → span via element pointer  

Both `FixedStringXxxView` and `ReadOnlySpan<T>` are ref structs and cannot be `Nullable<T>`, so the on-mismatch pattern throws `InvalidOperationException` (consistent with the complex struct case).

**Tests added:** `EmitIdl_UnionWithByteEnumDiscriminator_UsesMemberNamesNotIntegers`, `EmitIdl_UnionWithShortEnumDiscriminator_UsesMemberNames`.

---

### ME1-T04: StartsWith / EndsWith / Contains Filter Operators

**Approach:** Parameterized `@N` notation via `BuildLinq(IList<object?> paramValues)`. For string method operators, `FilterConditionNode.FormatValue` appends the `ValueText` to the `paramValues` list and returns `@{index}`, so the generated LINQ string looks like `Name.StartsWith(@0)`. The `IFilterCompiler.Compile` overload accepts `IReadOnlyList<object?>? paramValues` and passes them to `DynamicExpressionParser.ParseLambda` as `extraArgs`.

**Key Decisions:**
- Kept `ToDynamicLinqString()` (zero-arg) as backward-compatible overload — callers that don't need parameterization are unaffected.
- Only string method operators use parameterization; all other operators still inline their values as before. This avoids unnecessary complexity for numeric/enum comparisons.
- A `FilterGroupNode` passes the same `paramValues` list to all children, so indices auto-coordinate across conditions — `@0`, `@1`, etc. are globally unique within a compiled expression.

---

### ME1-T05: CLI-Safe Operator Aliases

**Approach:** `NormalizeCliOperators()` is called at the start of `Compile()` before any other processing. It applies 6 static `Regex` replacements using `\b{op}\b` word boundaries with `RegexOptions.IgnoreCase`.

**Key Decisions:**
- Word-boundary regex (`\bge\b`) prevents corrupting field names that contain the operator substring. For example, a field named `message` contains `ge` — `String.Replace("ge", ">=")` would corrupt it to `m>>=sage`. The regex anchor prevents this entirely.
- Case-insensitive matching allows `GE`, `gE`, `Ge` etc. to all resolve correctly.
- Replacements are applied in a fixed order; priority conflicts (e.g., `ge` vs `gle`) don't exist for the 6 aliases chosen.

---

### ME1-T06: Multi-Participant DDS Reception

**Approach:** `DdsSettings.ParticipantConfig` defines `(uint DomainId, string PartitionName)` pairs. `DdsBridge` creates one `DdsParticipant` per config entry and creates readers on each participant in `Subscribe()`.

**Key Decisions:**
- `IsPaused` gating is implemented at the `ChannelWriter.TryWrite` call site in the reader callback — samples are received but silently dropped when paused, avoiding reader teardown/rebuild cost.
- `ResetAll()` clears the sample store, instance store, and ordinal counter atomically from the UI's perspective (though individual operations are not transactional).
- `AddParticipant`/`RemoveParticipant` are provided as interface members for future dynamic configuration; the current implementation adds to the internal list but does not hot-wire new readers (by design — callers should restart subscription).
- Legacy `DomainId` in `DdsSettings` is migrated into `Participants[0]` in `ServiceCollectionExtensions` for backward compatibility with existing configuration files.

---

### ME1-T07: Global Sample Ordinal + Participant Stamping

**Approach:** `OrdinalCounter` is a singleton DI service wrapping a `long` incremented via `Interlocked.Increment`. `DynamicReaderConfig` carries `OrdinalCounter`, `Filter`, `DomainId`, `PartitionName`, and `ParticipantIndex`. In `DynamicReader.EmitSample`:
1. A temporary `SampleData` (ordinal=0) is built with participant metadata.
2. The user filter is tested — if it returns false, the sample is discarded **without** consuming an ordinal number.
3. Only on acceptance is `OrdinalCounter.Increment()` called, ensuring ordinals are dense for the filtered result set.

**Key Decisions:**
- "Filter before ordinal allocation" was a deliberate UX decision: the ordinal sequence seen in the UI becomes the sequence of accepted samples, with no gaps from discarded ones.
- `ParticipantIndex` in `SampleData` is an `int` (not `uint`) for alignment with list indexing patterns in the host code.
- `DomainId` and `PartitionName` are included in `SampleExportRecord` so that exported capture files record the source fully — enabling multi-source replay fidelity.

---

## 🚀 Deviations & Improvements

### Deviations from Specification

**Deviation 1 — FixedString/FixedSizeBuffer union arms throw instead of returning null:**
- **What:** For ref-struct union arm accessors (`FixedString32View`, `ReadOnlySpan<T>`), on discriminator mismatch the spec implicitly assumed nullable return. Ref structs cannot be `Nullable<T>`.
- **Why:** C# language constraint — `Nullable<T>` requires T to be a non-ref-struct value type or reference type.
- **Benefit:** Consistent with the complex struct arm pattern (which already throws). Callers check the discriminator accessor first before calling the typed accessor.
- **Risk:** None — callers must guard via `XxxKind == …` before calling `XxxAsYyy`.
- **Recommendation:** Keep.

### Improvements Made

**Improvement 1 — `DynamicReaderConfig` value object:**
- **What:** Grouped reader configuration parameters into a `DynamicReaderConfig` record instead of adding individual constructor parameters to `DynamicReader<T>`.
- **Benefit:** Clean extension point; future parameters (e.g., QoS overrides, per-reader sample limits) can be added without breaking callsites.
- **Complexity:** Low.

**Improvement 2 — `OrdinalCounter` as standalone injectable class:**
- **What:** Encapsulated the ordinal counter in its own class rather than using a shared `long[]` or `StrongBox<long>`.
- **Benefit:** Testable in isolation (`OrdinalCounter_SharedAcrossReaders_IsMonotonic` test), clearly named, injectable via DI.
- **Complexity:** Low.

---

## ⚡ Performance Observations

### Q4: Dynamic LINQ Performance

Dynamic LINQ expression compilation (`DynamicExpressionParser.ParseLambda`) is called once per user-submitted filter, not per sample. The compiled `Func<SampleData, bool>` delegate is then cached and applied per-tick with negligible overhead.

The parameterized `@N` path for `StartsWith`/`EndsWith`/`Contains` passes `object?[]` extra args to `ParseLambda` on each compilation, not on each tick evaluation. The compiled delegate captures the parameter values as constants at compile time within the expression tree, so tick-time cost remains identical to inline value compilation.

**Potential concern:** If a user changes the filter expression very frequently (e.g., live-typed search), `ParseLambda` is called on every change. This is throttle-worthy in the UI (debounce on keystroke), but is not an issue in the current push-button Apply model.

---

## 💬 Developer Insights

**Q1 — CLI operator replacement precision:**  
Word-boundary Regex (`\bge\b` with `RegexOptions.IgnoreCase`) was used. Simple `String.Replace("ge", ">=")` would corrupt any field name containing `ge` as a substring (e.g., `message` → `m>>=sage`, `geometry` → `>>=ometry`). The `\b` anchor matches only at word boundaries (letter/digit ↔ non-letter transitions), so isolated tokens `ge`, `GE`, etc. are replaced while embedded substrings are preserved. All 6 aliases use this pattern — the replacements are applied sequentially on each successive tokenized expression string.

**Q2 — UI operator visibility improvements observed:**  
In `FilterBuilderPanel.razor`, `GetOperatorLabel()` now returns readable labels for `StartsWith` / `EndsWith` / `Contains`. A natural follow-up observed but out of scope: the operator dropdown could be filtered to show only type-appropriate operators (e.g., hide string-methods for numeric fields, hide numeric comparisons for string-only fields). The `TopicMetadata.Fields` field type information is available at panel construction time and could drive the operator list per-field.

**Q3 — Design decisions beyond the spec:**  
- **`DynamicReaderConfig` grouping** (see Improvements section).  
- **Filter-before-ordinal semantics**: The spec stated ordinal should be maintained globally but didn't specify the ordering relative to filtering. Choosing filter-first produces dense ordinals for the visible result set, which is more useful in the UI (no ordinal gaps).  
- **`ResetAll` scope**: The spec listed clearing sample/instance stores. It was extended to also reset the `OrdinalCounter`, so ordinals restart from 1 on each reset — a natural UX expectation.  
- **`IsPaused` at write site vs. reader teardown**: Pausing by stopping `ChannelWriter.TryWrite` avoids expensive DDS reader lifecycle churn and allows instant resume.

**Q4 — Dynamic LINQ per-tick performance:** Addressed above (Performance Observations section). In summary: no per-tick compilation overhead. The compiled delegate is the hot path, and it is a simple typed lambda over a known-good expression tree — equivalent to a hand-written predicate in JIT output.

---

## ⚠️ Known Issues & Limitations

**Issue 1 — `@topic` annotation regression in IdlEmitter.cs:**  
- **Description:** During Task 0 integration, the ME1-T03 `@topic(name="...")` conditional was accidentally overwritten. Caught and corrected in this session by regenerating from git diff. All 3 affected DefaultTopicNameTests now pass.
- **Impact:** Was a build-time regression; fully resolved.
- **Recommendation:** Resolved.

**Limitation 1 — `AddParticipant` / `RemoveParticipant` do not hot-wire readers:**  
Dynamic participant management modifies the participants list but does not automatically create/destroy DDS readers. Callers must restart subscription explicitly. This is acceptable for the current use case (pre-configured at startup) and is documented via XML comments on the interface.

---

## 📋 Pre-Submission Checklist

- [x] All tasks completed as specified
- [x] All tests passing (DdsMonitor.Engine.Tests: 258/258, CycloneDDS.CodeGen.Tests: 161/161)
- [x] No compiler errors (2 CS8602 nullable dereference warnings in ViewEmitter.cs pre-exist)
- [x] Code follows existing patterns
- [x] Deviations documented and justified
- [x] Key public APIs documented with XML comments
- [x] Report filled out completely

---

**Ready for Review:** YES  
**Next Batch:** Can start immediately
