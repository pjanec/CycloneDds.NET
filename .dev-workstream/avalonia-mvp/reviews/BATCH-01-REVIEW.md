# BATCH-01 Review

**Batch:** BATCH-01  
**Tasks:** TASK-A001, TASK-B001, TASK-B002, TASK-B003  
**Reviewer:** Dev Lead  
**Verdict:** ⚠️ APPROVED WITH CORRECTIVE ACTION REQUIRED  

---

## Test Run Results (Verified by Dev Lead)

| Suite | Passed | Failed |
|---|---|---|
| `DdsMonitor.Engine.Tests` | 643 | 0 |
| `DdsMonitor.Blazor.Tests` | 16 | 0 |
| `DdsMonitor.Avalonia.Core.Tests` | 24 | 0 |
| `DdsMonitor.Avalonia.Tests` | 29 | 0 |
| **Total** | **712** | **0** |

All builds pass. All tests pass.

---

## Per-Task Review

### TASK-A001 — Engine Purification ✅

**Design alignment:** Excellent.
- `ITypeDrawerRegistry` correctly uses `Func<DrawerContext, object?>` — no Blazor imports anywhere in Engine.
- `DrawerContext` has `Receiver` properly removed. All 5-parameter construction is clean.
- `ISampleViewRegistry` / `SampleViewRegistry` updated consistently.
- `BlazorTypeDrawerAdapter` properly restores all primitive type registrations in `RegisterBuiltIns` — the Blazor app is not degraded.
- `MacroShim.Registry [ThreadStatic]` fix is a legitimate and valid improvement.

**Test quality:** Strong. 15 purification tests cover the key success conditions. Tests verify actual delegate identity, not just compilation.

**One P2 concern:** `TypeDrawerRegistry` built-in stubs return `null` — if GetDrawer is called for a built-in type outside the Blazor+Adapter stack, callers get null silently. This is by design but warrants documentation in the interface (see Debt Tracker).

---

### TASK-B001 — Avalonia.Core ✅

**Design alignment:** Good.
- `UserSettingsStore` debounce via CancellationTokenSource cancel-and-replace is correct and verified by test.
- `AvaloniaTypeDrawerRegistry.Build` throws `InvalidCastException` with the offending type name — matches spec.
- `SubscribeOnUiThread` uses `Dispatcher.UIThread.Post` with thread-check shortcut — correct pattern.
- `IEventBrokerExtensions` test properly validates UI-thread dispatch via `CheckAccess()`.

**Test quality:** 24 tests, all behaviorally meaningful. The `SubscribeOnUiThread` test properly asserts `Dispatcher.UIThread.CheckAccess()` was true inside the handler. The `UserSettingsStore_GetBeforeSet_ReturnsDefault` test correctly checks default semantics.

**One P2 concern:** `UserSettingsStore._data` is a plain `Dictionary` with a `lock` on writes but reads in `Get<T>` acquire the same lock — this is actually thread-safe. No action needed.

---

### TASK-B002 — Shell ⚠️ (P1 defect found)

**Design alignment:** Mostly good, but one critical violation.

**P1 — ViewModel Disposal Not Implemented:**  
DESIGN.md §5.1 states explicitly: *"AvaloniaWindowManager checks IDisposable on every ViewModel it instantiates and calls Dispose() when the panel closes. This is not optional."*  
TASK-DETAIL.md TASK-B001 Constraints: *"AvaloniaWindowManager calls Dispose() on any ViewModel implementing IDisposable when its panel closes."*

In `AvaloniaWindowManager.OnWindowClosed`, the geometry is saved and `WorkspaceSaveRequestedEvent` is published, but **the ViewModel is never retrieved and Dispose() is never called**. The ViewModel is instantiated in `OpenPanelWindow` and stored only inside the Window's Content control — `OnWindowClosed` has no reference to it.

**Impact:** Any ViewModel implementing `IDisposable` (which will include all future plugin ViewModels that subscribe to `IEventBroker`) will remain rooted in the singleton broker indefinitely, preventing GC and eventually exhausting resources. This MUST be fixed before Phase 2 ViewModels are implemented.

**Fix required:** Track the ViewModel object separately alongside the `Window` reference. On `OnWindowClosed`, retrieve the tracked ViewModel and call `Dispose()` if it implements `IDisposable`.

**Other observations (non-blocking):**
- `ShellWindow` transport buttons correctly wire to `IDdsBridge.IsPaused` and `ResetAll()` — confirmed from `MainLayout.razor` equivalents.
- Menu dynamic rebuild on `Changed` event works correctly, confirmed by test.
- `SpawnPanel` focus-if-open behavior is correct and tested.

---

### TASK-B003 — PluginLoader ✅

**Design alignment:** Good.
- `"DdsMonitor.Avalonia.Core"` added to `SharedAssemblyNames` — verified in test via `GetFeature<IToolbarRegistry>()` returning non-null.
- `InitializePlugins` called after `host.StartAsync()` and before window show — correct sequence.
- Corrupt DLL graceful skip — verified by test.

---

## Debt Tracker Updates

### P1 (Goes directly to Corrective Task 0 in next batch — not tracked here)
- **ViewModel disposal in `AvaloniaWindowManager`**: `OnWindowClosed` must call `Dispose()` on the ViewModel if it implements `IDisposable`. Must track ViewModels separately from Windows.

### P2 Items (Added to DEBT-TRACKER.md)
- **DT-001:** `TypeDrawerRegistry` primitive stubs return `null` silently — callers outside the Blazor/Avalonia adapter stacks may get null without error. Document this in the interface or add a sentinel guard.
- **DT-002:** `IWindowManager.RegisterPanelType` parameter named `blazorComponentType` — Blazor-specific name leaks into the general Engine interface. Rename to `viewModelType` or `componentType`.

---

## Approved Commit Message

```
feat(avalonia): BATCH-01 — engine purification + avalonia shell foundation

TASK-A001: Remove Blazor types from engine registries
- ITypeDrawerRegistry: Func<DrawerContext, object?> instead of RenderFragment
- ISampleViewRegistry: Func<SampleData, object?> instead of RenderFragment
- DrawerContext: remove IHandleEvent Receiver parameter
- Add BlazorTypeDrawerAdapter and BlazorSampleViewAdapter in DdsMonitor.Blazor
- Fix [ThreadStatic] race on MacroShim.Registry in FilterCompiler

TASK-B001: Create DdsMonitor.Avalonia.Core
- IToolbarRegistry, IUserSettings, IAvaloniaViewRegistry, IAvaloniaTypeDrawerRegistry
- AvaloniaDrawerContext, IStatefulViewModel, IEventBrokerExtensions
- 24 tests in DdsMonitor.Avalonia.Core.Tests (Avalonia.Headless.XUnit 11.2.3)

TASK-B002: Create DdsMonitor.Avalonia shell (WinExe)
- Program.cs: Generic Host + dual-boot (HeadlessMode/interactive)
- ShellWindow: Menu (dynamic), Toolbar (dynamic), Transport row, Status bar
- AvaloniaWindowManager: IWindowManager with geometry persistence
- ViewLocator: delegates to IAvaloniaViewRegistry
- 21 tests in DdsMonitor.Avalonia.Tests

TASK-B003: Wire PluginLoader into shell
- Add DdsMonitor.Avalonia.Core to PluginLoader.SharedAssemblyNames
- InitializePlugins called before ShellWindow shown
- 8 plugin loader tests (corrupt DLL graceful, GetFeature non-null, etc.)

All 712 tests pass (643 Engine + 16 Blazor + 24 Core + 29 Shell).
NOTE: P1 defect found — ViewModel IDisposable.Dispose() not called on panel close.
Corrective Task 0 in BATCH-02.
```
