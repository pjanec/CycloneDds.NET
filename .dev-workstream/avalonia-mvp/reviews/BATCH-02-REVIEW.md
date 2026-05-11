# BATCH-02 Review

**Batch:** BATCH-02 — ViewModel Disposal Fix + Schema Discovery + Topic Explorer + Dummy Generator  
**Tasks:** CORRECTIVE-0, TASK-C001, TASK-C002, TASK-D001  
**Reviewer:** Dev Lead  
**Decision:** ✅ APPROVED

---

## Test Run Summary

| Suite | Result | Count |
|---|---|---|
| DdsMonitor.Engine.Tests | ✅ Pass | 643 |
| DdsMonitor.Blazor.Tests | ✅ Pass | 16 |
| DdsMonitor.Avalonia.Core.Tests | ✅ Pass | 24 |
| DdsMonitor.Avalonia.Tests | ✅ Pass | 32 (+3 disposal) |
| DdsMonitor.Avalonia.StandardPlugin.Tests | ✅ Pass | 30 (new) |
| **Total** | **✅ Pass** | **745** |

---

## CORRECTIVE-0 — ViewModel Disposal

**Decision:** ✅ Approved — root cause correctly identified and resolved.

`AvaloniaWindowManager` now maintains `_viewModels Dictionary<string, object>` alongside `_openWindows`. On `OpenPanelWindow` the created ViewModel is stored under `panelState.PanelId`. On `OnWindowClosed` the ViewModel is retrieved, removed from the dictionary, and `IDisposable.Dispose()` is called if implemented.

**Bonus fix discovered:** The sub-agent identified that `ActivatorUtilities.CreateInstance` bypasses DI-registered factory lambdas, which breaks tests that register controlled VM instances via `AddTransient<T>(_ => factory)`. The fix (`GetService` first, fall-back to `CreateInstance`) is the correct production behavior and also makes the injection architecture testable. Good catch.

**Three new disposal tests are solid:**
- `DisposeCalledOnWindowClose` — verifies IDisposable.Dispose() is called
- `NotDisposedWhileOpen` — verifies Dispose is NOT called prematurely
- `DoubleClose_NoException_NoDoubleDispose` — guards against double-dispose

P1 defect **DT-P1-01** (BATCH-01 review) is resolved.

---

## TASK-C001 — WorkspaceManagerPlugin

**Decision:** ✅ Approved

- Plugin registers "Tools/Schema Sources…" menu item — verified by test.
- Menu item action spawns `SchemaSourcesViewModel` panel — verified by test.
- `SchemaSourcesViewModel` subscribes to `IAssemblySourceService.Changed` and refreshes entries — verified by test with in-event mutation.
- Add/Remove/OutOfRange covered; `IsCliOverride` reflected correctly.
- `ConfigureServices` correctly registers nothing (schema service owned by Engine DI).

---

## TASK-C002 — TopicExplorerPlugin

**Decision:** ✅ Approved

- Spawns panel at startup, registers menu item + toolbar button — all covered.
- `TopicExplorerViewModel` implements both `IStatefulViewModel` and `IDisposable`.
- Disposal test uses `TrackingEventBroker` — a proper proxy that counts active subscriptions via `Interlocked` and returns `TrackedToken` disposables. `ActiveSubscriptionCount` reaches 0 after `vm.Dispose()`. Excellent stub design.
- `ShowHidden` setting persisted to `UserSettingsStore` — verified with real temp-file store (not a mock). This proves the full round-trip.
- `OpenSamplesViewer` publishes `SpawnPanelEvent` with `"TopicName"` in state — verified.

**P2 issue noted:** `TopicExplorerViewModel_ShowHidden_False_FiltersHiddenTopics` does not cover an actual hidden topic (one with `ShortName` starting with `_`). The `IsHidden` predicate (`StartsWith('_') || Namespace.Contains("Internal")`) is implemented correctly but cannot be tested without a CLR type whose name starts with `_`. Adding a `_HiddenSample` sentinel type to the test project would close this gap. → Added to DEBT-TRACKER as DT-003.

---

## TASK-D001 — DummyDataGeneratorPlugin

**Decision:** ✅ Approved

- `DummyGeneratorService` is a proper `IHostedService` + `IDisposable` with `CancellationTokenSource` lifecycle.
- `TogglePublishing` is thread-safe (lock on `_toggleLock`).
- Tests verify actual `WriteCount > 0` after a timed delay — not just that `StartAsync` was called. Real integration-style verification.
- Toggle test verifies count stops growing after toggle-off, then resumes after toggle-on.
- Context menu injection test verifies the provider is wired correctly.
- `ConfigureServices` test verifies both `DummyGeneratorService` and `IHostedService` registrations.

---

## Cross-Cutting Concerns

**Stub quality:** `Stubs.cs` is well-organized. `TrackingEventBroker`, `CapturingBroker`, `StubDdsBridge`, `StubAssemblySourceService` — all purpose-built for the scenarios they test.

**No shell reference:** Confirmed `DdsMonitor.Avalonia.StandardPlugin.csproj` has no reference to `DdsMonitor.Avalonia`. ✅

**`IDisposable` discipline:** Every ViewModel subscribing to `IEventBroker` implements `IDisposable` and disposes tokens. `TopicExplorerViewModel` also unsubscribes from `ITopicRegistry.Changed` (plain C# event) on disposal. ✅

---

## Issues Logged

| ID | Priority | Description | Action |
|----|----------|-------------|--------|
| DT-003 | P2 | `TopicExplorerViewModel_ShowHidden_False_FiltersHiddenTopics` doesn't test actual hidden topic filtering — no CLR type with `_` prefix available. Add `_HiddenSample` type to test project. | BATCH-03 |

---

## Items Resolved This Batch

| ID | Description | Resolved |
|----|-------------|---------|
| DT-P1-01 | ViewModel disposal: `IDisposable.Dispose()` never called on panel close | BATCH-02 (CORRECTIVE-0) |
