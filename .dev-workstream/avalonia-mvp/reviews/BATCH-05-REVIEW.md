# BATCH-05 Review

**Batch:** BATCH-05 — Phase 6: Workspace Persistence + Debt Resolution  
**Tasks:** DT-007, DT-009, TASK-G001  
**Reviewer:** Dev Lead  
**Decision:** ✅ APPROVED — V1 COMPLETE

---

## Test Run Summary

| Suite | Result | Count |
|---|---|---|
| DdsMonitor.Engine.Tests | ✅ Pass | 643 |
| DdsMonitor.Blazor.Tests | ✅ Pass | 16 |
| DdsMonitor.Avalonia.Core.Tests | ✅ Pass | 24 |
| DdsMonitor.Avalonia.Tests | ✅ Pass | 42 (+10) |
| DdsMonitor.Avalonia.StandardPlugin.Tests | ✅ Pass | 74 (+3) |
| **Total** | **✅ Pass** | **799** |

---

## DT-009 — StandardDrawerRegistrar internal

**Decision:** ✅ Approved.

`InternalsVisibleTo` added to `StandardPlugin.csproj`. `StandardDrawerRegistrar` correctly changed to `internal`. All 3 existing drawer tests pass unchanged. Clean.

---

## DT-007 — NetworkConfigViewModel.Apply() no-op guard

**Decision:** ✅ Approved.

`Apply()` now compares UI `Participants` vs `_ddsBridge.ParticipantConfigs` using `Zip` before calling any bridge methods. If identical (same count, same DomainId+PartitionName in order), it returns early. The test `NetworkConfigViewModel_Apply_NoChanges_SkipsBridgeCalls` confirms zero `AddParticipant` calls on identical state. Correct fix within V1 scope.

---

## TASK-G001 — IStatefulViewModel Persistence Round-Trip

**Decision:** ✅ Approved.

**AvaloniaWorkspacePersistenceService:** Constructor-subscription pattern (not ExecuteAsync) is intentional and testable. Debounce at 1.5 s is correct per spec. `ApplicationStopping` registration for final sync save is correct. Subscription token properly disposed on `Dispose()`. The 5 persistence-service tests cover subscription, flush, empty-path no-crash, debounce cancel, and subscription release — this is tight coverage for a debounced background service.

**AvaloniaWindowManager — JsonElement geometry handling:** The `ToDouble(object)` helper is clean. Dual-path handling for native `Dictionary<string, double>` vs `Dictionary<string, object>` with `JsonElement` values covers both runtime cases (fresh vs. deserialized workspace). The `RestoresGeometryFromJsonElement` test directly exercises the JSON path. ✅

**`AvaloniaWindowManager.SpawnPanel` calls `Initialize` before Show:** Verified by `WindowManager_SpawnPanel_CallsInitializeOnStatefulViewModel`. The pattern is correct per the design spec: ViewModel reads from dict, stores reference, then window is shown. ✅

**`SamplesViewerViewModel.Initialize` restoration:** FilterText is read from `ComponentState` (handling `JsonElement`), stored, and then `ApplyFilter` is called to actually push the filter into `ISampleView` — so a restored filter is active immediately, not just set as a property. This is the correct behaviour. The `SamplesViewerViewModel_Initialize_RestoresFilterText` test verifies both `vm.FilterText` and `view.LastFilter != null`. ✅

**`SamplesViewerViewModel.FilterText` → `WorkspaceSaveRequestedEvent`:** `SamplesViewerViewModel_FilterTextChange_PublishesSaveEvent` confirms the event fires. ✅

**Workspace load in `App.axaml.cs`:** Loading after `pluginLoader.InitializePlugins` is the correct order — all view factories are registered before `SpawnPanel` is called for restoration. The headless mode guard (early return before `BuildAvaloniaApp`) prevents UI calls in headless mode. ✅

---

## Debt Items Noted in Report

| ID | Priority | Description | Action |
|----|----------|-------------|--------|
| DT-010 | P3 | `AvaloniaWindowManager.SaveWorkspaceToJson` uses bare `List<PanelState>` format, not `WorkspaceDocument`. Blazor/Avalonia workspace files are format-incompatible. | Phase 7 / deferred |
| DT-011 | P3 | `SamplesViewerViewModel.FilterText = ""` does not publish `WorkspaceSaveRequestedEvent` — clearing filter does not mark workspace dirty. Intentional but undocumented. | Acceptable V1 |
| DT-012 | P3 | `LoadWorkspace` in `App.axaml.cs` lacks a headless-mode guard at the call site (relies on caller to not reach this code path). | Low risk — current boot path safe |

---

## V1 Milestone Check

All V1 tasks in TASK-TRACKER.md Phases 0–6 are now **complete**:

| Phase | Tasks | Status |
|-------|-------|--------|
| 0 — Engine Purification | TASK-A001 | ✅ |
| 1 — Empty Shell | TASK-B001, B002, B003 | ✅ |
| 2 — Schema & Topic Discovery | TASK-C001, C002 | ✅ |
| 3 — Backend Prover | TASK-D001 | ✅ |
| 4 — Firehose UI | TASK-E001, E002 | ✅ |
| 5 — Data Authoring & Network Config | TASK-F001, F002 | ✅ |
| 6 — Workspace Polish | TASK-G001 | ✅ |

Phase 7 (TASK-H001–H005) is explicitly post-V1.

**DdsMonitor.Avalonia V1 MVP is complete at 799 automated tests with 0 failures.**
