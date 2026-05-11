# BATCH-04 Review

**Batch:** BATCH-04 — Phase 5: Data Authoring & Network Configuration  
**Tasks:** DT-004, DT-005, TASK-F001, TASK-F002  
**Reviewer:** Dev Lead  
**Decision:** ✅ APPROVED

---

## Test Run Summary

| Suite | Result | Count |
|---|---|---|
| DdsMonitor.Engine.Tests | ✅ Pass | 643 |
| DdsMonitor.Blazor.Tests | ✅ Pass | 16 |
| DdsMonitor.Avalonia.Core.Tests | ✅ Pass | 24 |
| DdsMonitor.Avalonia.Tests | ✅ Pass | 32 |
| DdsMonitor.Avalonia.StandardPlugin.Tests | ✅ Pass | 71 (+19) |
| **Total** | **✅ Pass** | **786** |

---

## DT-004 — SamplesViewerView DataTemplate

**Decision:** ✅ Already resolved in BATCH-03.

The sub-agent correctly identified that BATCH-03 had already added a `DataTemplate` for `SampleRowViewModel`. No changes needed. DT-004 closed.

---

## DT-005 — DetailInspectorView Field Tree Indentation

**Decision:** ✅ Approved.

`FieldInspectorItemViewModel.IndentMargin` returns `new Thickness(Depth * 12, 0, 0, 0)`, bound in `DetailInspectorView.axaml` via `StackPanel.Margin="{Binding IndentMargin}"`. This avoids an AXAML converter dependency while keeping all visual logic in the ViewModel. Correct approach.

---

## TASK-F001 — SendSamplePlugin

**Decision:** ✅ Approved with one P3 debt note.

**StandardDrawerRegistrar:** All 12 primitive drawers registered correctly. The `NumericUpDown.FormatString` adaptation for Avalonia 11.2.3 (no `DecimalPlaces` property) is the right fix. Callback-per-field pattern (`ctx.OnChange(typed_value)`) is correct — no `Convert.ChangeType` at the Send callsite.

**SendSampleViewModel:** Constructor correctly accepts `initialPayload` for cloning. `BuildControls` silently skips fields with no drawer (avoids crashing on exotic types). `Send()` catches DDS exceptions, surfaces via `SendError`, does not rethrow. `SendEnabled` tied to `_validationErrorCount`. Does NOT implement `IStatefulViewModel` per spec.

**P3 note (DT-009):** `StandardDrawerRegistrar` is `public` rather than `internal`. Technically correct for V1 given no `InternalsVisibleTo` attribute on the plugin project, but exposes internal tooling. → DT-009 logged.

**Test quality:** 10 tests (2 over minimum). The extra coverage of `RegistersToolsMenuSendSample` and `ToolsMenuSpawnsSendSampleBlankPanel` is worth having. `SendSampleViewModel_Build_CreatesControlsForAllNonSyntheticFields` is correctly `[AvaloniaFact]`. The `StubAvaloniaTypeDrawerRegistry` throwing instead of returning a `TextBlock` (to stay off-UI-thread in non-AvaloniaFact tests) is the right approach — the stub's contract is proven and the VM's skip-on-error behavior is covered.

---

## TASK-F002 — NetworkConfigPlugin

**Decision:** ✅ Approved with P2 debt note.

**NetworkConfigViewModel:** Loads existing participants on construction. `AddRow`/`RemoveRow` modify the collection. `Apply()` iterates and calls `AddParticipant` per row, publishes `ParticipantsChangedEvent`, catches exceptions into `ApplyError`. Does NOT implement `IStatefulViewModel` (deferred per spec).

**P2 debt (DT-007):** `Apply()` calls `AddParticipant` for all rows without clearing existing participants first. If a user clicks Apply multiple times, DDS participant count accumulates. This is a design gap in the Engine's `IDdsBridge` interface — correct fix is a `SetParticipants` or diff-and-add/remove pattern. Logged as DT-007 (P2, target BATCH-05 if IDdsBridge supports it, otherwise defer).

**P3 note (DT-006):** `NetworkConfigView` uses `TextBox` bindings for `DomainId` rather than a `NumericUpDown`. Non-numeric input will silently fail to update the integer property. Acceptable for V1. Logged as DT-006.

**Test quality:** 6 tests, all `[Fact]`. `NetworkConfigViewModel_Apply_CallsAddParticipant` and `Apply_ExceptionSetsApplyError` both use the extended `StubDdsBridge`. Coverage is correct for V1.

---

## Cross-Cutting

**UI thread correctness:** All Avalonia-control-instantiating tests correctly use `[AvaloniaFact]`. Non-control tests use `[Fact]`. The split is clean.

**Drawer build error isolation:** `SendSampleViewModel.BuildControls` skips fields that throw during `Build`. This is the right V1 behavior — a missing drawer does not crash the panel. DT-001 (TypeDrawerRegistry null sentinel) is still open as P2 but this skip-on-error behavior covers the send panel surface.

---

## Issues Logged

| ID | Priority | Description | Target |
|----|----------|-------------|--------|
| DT-006 | P3 | `NetworkConfigView` DomainId uses `TextBox` string binding — non-numeric input silently fails. | BATCH-05 or defer |
| DT-007 | P2 | `NetworkConfigViewModel.Apply()` accumulates DDS participants on repeated clicks (no clear/diff). | BATCH-05 |
| DT-008 | P3 | `SendSampleViewModel` does not implement `IStatefulViewModel` — payload authoring state lost on panel close. | Deferred (spec says V1 only) |
| DT-009 | P3 | `StandardDrawerRegistrar` is `public` instead of `internal`. Add `InternalsVisibleTo` to plugin project to revert. | BATCH-05 |

---

## Items Resolved This Batch

| ID | Description | Resolved |
|----|-------------|---------|
| DT-004 | `SamplesViewerView.axaml` DataTemplate | Already complete from BATCH-03 |
| DT-005 | `DetailInspectorView.axaml` field tree indentation | BATCH-04 |
