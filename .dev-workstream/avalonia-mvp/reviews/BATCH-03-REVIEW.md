# BATCH-03 Review

**Batch:** BATCH-03 — Phase 4 Firehose UI + Debt Resolution  
**Tasks:** DT-002, DT-003, TASK-E001, TASK-E002  
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
| DdsMonitor.Avalonia.StandardPlugin.Tests | ✅ Pass | 52 (+22) |
| **Total** | **✅ Pass** | **767** |

---

## DT-002 — `blazorComponentType` rename

**Decision:** ✅ Approved

Parameter renamed to `viewModelType` in `IWindowManager`, `WindowManager`, `PluginPanelRegistry`, and all stubs. One debt-resolution test verifies the rename coverage. No regressions.

---

## DT-003 — Hidden topic test coverage

**Decision:** ✅ Approved

`_HiddenSample` type added to the test project. `TopicExplorerViewModel_ShowHidden_False_DoesNotShowHiddenTopic` now tests the full filter path: a `_`-prefixed topic is absent when `ShowHidden=false` and present when `ShowHidden=true`. This closes the test gap identified in BATCH-02.

---

## TASK-E001 — SamplesViewerPlugin

**Decision:** ✅ Approved

- `SamplesViewerPlugin.Initialize` subscribes to `SpawnPanelEvent` via `SubscribeOnUiThread`. On match, calls `IWindowManager.SpawnPanel("SamplesViewer_<TopicName>", state)`.
- `SamplesViewerViewModel` correctly accepts a pre-injected `ISampleView` (for testing) and falls back to creating a real `SampleView` in production.
- `FilterText` setter bug (empty-value guard) was correctly identified and fixed by narrowing the skip guard to `same non-empty value` only. This ensures `SetFilter(null)` always fires on clear.
- `OnViewRebuilt` fires on the background thread but marshals to UI thread via `Dispatcher.UIThread.Post` before updating `FilteredCount`. ✅ Thread safety maintained.
- `ComponentState["TopicName"]` written in `Initialize`. ✅
- `Dispose()` unsubscribes `OnViewRebuilt` event AND disposes the `ISampleView`. ✅

**P2 note (TD-B03-1 from report):** `SamplesViewerView.axaml` has no sample row `DataTemplate`. The `ListBox` displays `SampleRowViewModel.ToString()`. This is acceptable for V1 but will need a `DataTemplate` before the panel is usable in production. → Added to DEBT-TRACKER as DT-004.

**Test quality:** 7 ViewModel tests + 3 plugin tests. The `OnViewRebuilt_UpdatesFilteredCountOnUiThread` test correctly dispatches from a background thread and uses `RunJobs()` to flush. The `Dispose_UnsubscribesOnViewRebuilt` test fires the event post-disposal and verifies no count update. Solid.

---

## TASK-E002 — DetailInspectorPlugin

**Decision:** ✅ Approved

- `DetailInspectorViewModel` implements both `IStatefulViewModel` and `IDisposable`.
- `SubscribeIfLinked()` is correctly conditional on both `IsLinked == true` AND `SourcePanelId != null`. This prevents phantom subscriptions when no source panel is known.
- `IsLinked` setter correctly disposes old subscription on unlink and re-subscribes on re-link.
- `RebuildFieldTree` is only ever called from `OnSampleReceived` which is wired via `SubscribeOnUiThread` — so `FieldMetadata.Getter` is always invoked on the UI thread. ✅
- `RebuildFieldTree` has a blanket `try/catch` around the getter call, producing `"<error>"` on failure. Inspector does not crash on bad getters.
- Null payload guard: `if (sample == null || sample.Payload == null || sample.TopicMetadata?.AllFields == null) return;` — correct.

**P2 note (TD-B03-2):** Field tree shows as flat list (no visual nesting). `Depth` is tracked in the ViewModel but not reflected in the view. Acceptable for V1 tree-preview. → Added to DEBT-TRACKER as DT-005.

**Test quality:** 1 plugin test + 9 ViewModel tests. `DetailInspectorViewModel_Unlink_DisposesSubscription` and `DetailInspectorViewModel_Dispose_DisposesSubscriptions` both use `TrackingEventBroker` with proper count tracking. Cross-panel routing (`WrongPanel_Ignored`) tested explicitly. Strong.

---

## Cross-Cutting

**`SamplesViewerPlugin` no `IDisposable`:** Developer correctly noted this is a lifecycle trade-off — plugins live for the app lifetime. The subscription token is held for the app lifetime which is fine. This is not a defect. → TD-B03-3 from report noted but not escalated (no action needed).

**`DetailInspectorPlugin` context menu source panel wiring:** The inspector captures `sourcePanelId` in a closure. The actual source panel ID must be provided by whoever publishes the context menu request — this is a design question for the integration layer (Phase 5+). Left open as Q4 in the report.

---

## Issues Logged

| ID | Priority | Description | Action |
|----|----------|-------------|--------|
| DT-004 | P2 | `SamplesViewerView.axaml` has no `DataTemplate` for sample rows — renders `.ToString()`. | BATCH-04 |
| DT-005 | P2 | `DetailInspectorView.axaml` field tree is flat (depth tracked in VM but not rendered). | BATCH-04 |

---

## Items Resolved This Batch

| ID | Description | Resolved |
|----|-------------|---------|
| DT-002 | `blazorComponentType` parameter renamed to `viewModelType` | BATCH-03 |
| DT-003 | `_HiddenSample` type added; hidden topic filter properly tested | BATCH-03 |
