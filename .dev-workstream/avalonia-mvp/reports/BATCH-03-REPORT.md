# BATCH-03 Report — Phase 4 Firehose UI + Debt Resolution

## 1. Issues Encountered and How Resolved

### A. `FilterText` setter early-return bug (1 failing test after initial run)

**Problem:** `SamplesViewerViewModel` initialized `_filterText = ""` (default string value). The setter guard `if (_filterText == value) return;` caused `vm.FilterText = ""` to silently skip `ApplyFilter` on a fresh VM, so the test expecting `SetFilter(null)` to be called failed.

**Resolution:** Changed the guard to `if (_filterText == value && !string.IsNullOrEmpty(value)) return;`. This ensures clearing the filter always propagates to the view even when the field was already empty.

### B. `DetailInspectorPlugin` test used 4-element deconstruction on a 3-tuple

**Problem:** `BuildContext()` returned a 3-tuple but the test tried to deconstruct into 4 variables → CS8132 build error.

**Resolution:** Changed `var (ctx, contextMenu, _, _)` to `var (ctx, contextMenu, _)`.

### C. `SampleData` nullability — record vs. struct

**Problem:** First draft of `DetailInspectorViewModel` checks used `.HasValue` (nullable struct pattern) but `SampleData` is a reference type.

**Resolution:** Changed to null-reference checks (`_currentSample != null`).

### D. `DetailInspectorView.axaml` — missing `DepthToMarginConverter` resource

**Problem:** The first draft AXAML used `{StaticResource DepthToMarginConverter}` which isn't registered anywhere.

**Resolution:** Removed the converter reference and used a flat `Margin="4,0"` for field rows.

### E. `StubWindowManager.RegisteredPanelTypes` was a dead-end stub

**Problem:** The property returned a fresh empty dictionary regardless of `RegisterPanelType` calls, making the DT-002 rename coverage test assert fail.

**Resolution:** Added a backing `_registeredPanelTypes` dictionary and wired `RegisterPanelType` to populate it.

---

## 2. Codebase Surprises

- `SampleData.SampleInfo` exposes `SourceTimestamp` as a raw `long` (not a `DateTime` or `DateTimeOffset`), and `GenerationRank` as `uint`. The `DetailInspectorViewModel` surfaces these as `.ToString()` strings, which is consistent but may need formatting work later.
- `IWindowManager.RegisteredPanelTypes` was never part of the interface — it is only on `StubWindowManager`. The production `WindowManager` does not expose this property (it is internal state). The stub was added to support the DT-002 rename test.
- `TopicMetadata.AllFields` with `FieldMetadata.Getter` already exists and works well for reflective field trees. No boxing surprises for value types found during testing.

---

## 3. Design Decisions Beyond Instructions

### SamplesViewerPlugin — no IDisposable

The instructions said "store subscription token in `_spawnToken`" but did not mandate `IDisposable`. Since `IMonitorPlugin.Initialize` is called once at app startup and the plugin lives for the app lifetime, `IDisposable` was omitted. If plugins ever need graceful shutdown this will need revisiting.

### FilterText setter semantics

The rule "skip if same value" was narrowed to "skip only if same non-empty value" to make the "clear filter" path always consistent. This means clearing the filter twice fires `SetFilter(null)` twice, which is idempotent in the engine.

### FieldInspectorItemViewModel is `public sealed`, not `internal`

The instructions did not specify access modifier. `public sealed` was chosen to align with other ViewModels in the project that are public by default, allowing view binding from AXAML without reflection.

---

## 4. Technical Debt Created

| ID | Item | Risk |
|----|------|------|
| TD-B03-1 | `SamplesViewerView.axaml` has no sample row template — the `ListBox` bound to `SampleRows` will display raw `SampleRowViewModel.ToString()` output until a `DataTemplate` is added. | Low (functional but ugly) |
| TD-B03-2 | `DetailInspectorView.axaml` shows field depth via flat margin only; nested objects are not visually grouped. | Low |
| TD-B03-3 | `SamplesViewerPlugin` stores `_spawnToken` but never disposes it (plugin has no `IDisposable`). If plugins are ever torn down this leaks a subscription. | Low (app lifetime) |
| TD-B03-4 | `SamplesViewerViewModel.Initialize` ignores the `"TopicName"` key if both `_view` and `_meta` are null (unit-test-only path). If a VM is instantiated without DI injection the topic name will be blank. | Low (DI always provides these) |
| TD-B03-5 | `FieldMetadata.Getter` may return boxed value types; no special handling for enums or nested objects — they display as `.ToString()`. | Low (future work) |

---

## 5. Remaining Open Questions

1. **SamplesViewerView row template:** What fields from `SampleRowViewModel` should appear in each row? Timestamp? Key fields only? All fields? Needs UX decision before a follow-up batch.
2. **SourceTimestamp formatting:** `SampleInfo.SourceTimestamp` is a raw `long`. Is it nanoseconds since DDS epoch, POSIX milliseconds, or ticks? This affects the `WriteTimestamp` display in `DetailInspectorViewModel`.
3. **SamplesViewerViewModel state persistence:** The `GetState()` method returns the topic name but not the current filter. Should the filter expression survive panel re-open? Currently it does not.
4. **DetailInspectorPlugin context menu source panel ID:** The `sourcePanelId` captured in the lambda comes from the panel that owns the context menu. Is that always the `SamplesViewer` panel, or can other panels also raise this menu?

---

## 6. Test Counts Per Suite

| Suite | Before BATCH-03 | After BATCH-03 | Delta |
|-------|----------------|----------------|-------|
| DdsMonitor.Engine.Tests | 643 | 643 | 0 |
| DdsMonitor.Blazor.Tests | 16 | 16 | 0 |
| DdsMonitor.Avalonia.Core.Tests | 24 | 24 | 0 |
| DdsMonitor.Avalonia.Tests | 32 | 32 | 0 |
| DdsMonitor.Avalonia.StandardPlugin.Tests | 30 | **52** | **+22** |
| **Total** | **745** | **767** | **+22** |

All tests pass. No regressions.

### New tests breakdown (StandardPlugin, +22):

| Category | Count |
|----------|-------|
| DT-002 rename coverage (`DebtResolutionTests`) | 1 |
| DT-003 hidden sample filter (`TopicExplorerViewModelTests`) | 1 |
| `SamplesViewerPluginTests` | 3 |
| `SamplesViewerViewModelTests` | 7 |
| `DetailInspectorPluginTests` | 1 |
| `DetailInspectorViewModelTests` | 9 |
| **Total new** | **22** |
