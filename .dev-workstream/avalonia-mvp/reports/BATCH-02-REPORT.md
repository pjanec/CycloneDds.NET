# BATCH-02 Report

**Batch:** BATCH-02 — ViewModel Disposal Fix + Schema Discovery + Topic Explorer + Dummy Generator  
**Tasks:** CORRECTIVE-0, TASK-C001, TASK-C002, TASK-D001  
**Status:** ✅ COMPLETE — All tests pass

---

## Test Results Summary

| Suite | Result | Count |
|---|---|---|
| DdsMonitor.Engine.Tests | ✅ Pass | 643 |
| DdsMonitor.Blazor.Tests | ✅ Pass | 16 |
| DdsMonitor.Avalonia.Core.Tests | ✅ Pass | 24 |
| DdsMonitor.Avalonia.Tests | ✅ Pass | 32 (incl. 3 new disposal tests) |
| DdsMonitor.Avalonia.StandardPlugin.Tests | ✅ Pass | 30 (new) |
| **Total** | **✅ Pass** | **745** |

---

## Implementation Summary

### CORRECTIVE-0 — ViewModel Disposal

**Files changed:**
- `tools/DdsMonitor/DdsMonitor.Avalonia/AvaloniaWindowManager.cs`
- `tests/DdsMonitor.Avalonia.Tests/ShellTests.cs`

Added a `Dictionary<string, object> _viewModels` tracking field alongside `_openWindows`. When a panel window opens, the created ViewModel is stored in `_viewModels[panelState.PanelId]` under the lock. When the window closes (`OnWindowClosed`), the VM is retrieved, removed from the dictionary, and `IDisposable.Dispose()` is called if implemented.

Three new tests added: `DisposeCalledOnWindowClose`, `NotDisposedWhileOpen`, `DoubleClose_NoException_NoDoubleDispose`.

### TASK-C001 — WorkspaceManagerPlugin

**New files in `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/`:**
- `WorkspaceManagerPlugin.cs` — `IMonitorPlugin` that registers "Tools/Schema Sources…" menu item
- `SchemaSourcesViewModel.cs` — ViewModel implementing `IStatefulViewModel`; subscribes to `IAssemblySourceService.Changed`
- `SchemaSourcesView.axaml` + `.axaml.cs` — Avalonia UserControl with ListBox + Add/Remove buttons

### TASK-C002 — TopicExplorerPlugin

**New files in `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/`:**
- `TopicExplorerPlugin.cs` — `IMonitorPlugin`; spawns panel at startup; registers View/Topic Explorer menu + toolbar button
- `TopicExplorerViewModel.cs` — ViewModel implementing `IStatefulViewModel` + `IDisposable`; subscribes to `ITopicRegistry.Changed` and `IEventBroker` for `SpawnPanelEvent`
- `TopicExplorerView.axaml` + `.axaml.cs` — Avalonia UserControl with ShowHidden checkbox + topic ListBox

### TASK-D001 — DummyDataGeneratorPlugin

**New files in `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/`:**
- `DummyDataGeneratorPlugin.cs` — `IMonitorPlugin`; registers `DummyGeneratorService` as singleton + `IHostedService`
- `DummyGeneratorService.cs` — `IHostedService` that publishes `HeartbeatSample` in a loop; supports `TogglePublishing()` thread-safely
- `HeartbeatSample.cs` — Synthetic struct DDS topic with `[DdsTopic]`/`[DdsKey]` attributes

**New project created:**
- `tools/DdsMonitor/DdsMonitor.Avalonia.StandardPlugin/DdsMonitor.Avalonia.StandardPlugin.csproj`
- `tests/DdsMonitor.Avalonia.StandardPlugin.Tests/DdsMonitor.Avalonia.StandardPlugin.Tests.csproj`

---

## Developer Insights

**Q1: What issues did you encounter? How did you resolve them?**

**Issue 1 — Root cause of CORRECTIVE-0 was deeper than the review described.** The BATCH-01 review identified that `OnWindowClosed` never calls `Dispose()`. The obvious fix adds disposal there. However, the tests revealed a second, subtler problem: `ActivatorUtilities.CreateInstance` bypasses DI-registered factory lambdas. When tests registered VMs via `AddTransient<T>(_ => factory)` (a standard pattern for injecting test-controlled instances), `ActivatorUtilities.CreateInstance` ignored the factory entirely and created a new instance via reflection. This meant the "created" variable in the test was never set.

**Resolution:** Changed `OpenPanelWindow` from:
```csharp
var vm = ActivatorUtilities.CreateInstance(_services, vmType);
```
to:
```csharp
var vm = _services.GetService(vmType)
    ?? ActivatorUtilities.CreateInstance(_services, vmType);
```
This is also the correct production behavior: a plugin that registers its VM via DI (for scoping, lifecycle control, or decoration) now has that respected.

**Issue 2 — `DummyGeneratorService` was `sealed`, blocking test subclassing.** The plugin init tests originally planned to use a `StubDummyGeneratorService : DummyGeneratorService` subclass. Removed `sealed` to allow this pattern in the future (the stub class was ultimately not needed, as the tests directly exercised `DummyDataGeneratorPlugin` against a real `StubMonitorContext`).

**Issue 3 — `Microsoft.Extensions.Configuration.Memory` not available in the local NuGet cache.** The test project initially declared this explicit package reference. Removed it; `AddInMemoryCollection` is already in `Microsoft.Extensions.Configuration` 8.0.0 which is pulled transitively from `Microsoft.Extensions.Hosting`.

**Issue 4 — `Avalonia.Controls.Control` ambiguity in test stubs.** Without an explicit `using Avalonia.Controls;`, the compiler resolved `Avalonia.Controls.Control` as `DdsMonitor.Avalonia.Controls.Control` (non-existent) because it attempted to match `Avalonia` as a sub-namespace of the `DdsMonitor.Avalonia` package. Fixed by adding `using Avalonia.Controls;` to `Stubs.cs`.

---

**Q2: Did the existing codebase have any surprises?**

- **`IUserSettings` API**: Turned out to be an async-first store: `Get<T>(section, key, default)` is synchronous but `SaveAsync()` is not. The ViewModel must call `SaveAsync()` (fire-and-forget with `_ = ...`) after `Set()`. The spec implied this but did not show it explicitly.

- **`IAvaloniaViewRegistry.Register<TViewModel>(Func<TViewModel, Control>)`**: The generic factory signature requires the return type to be `Avalonia.Controls.Control`. Test stubs referencing this signature needed `using Avalonia.Controls;` even in a project that doesn't reference the shell executable. The interface itself is in `DdsMonitor.Avalonia.Core`, so the reference is valid — just the using was missing.

- **`ITopicRegistry.Changed`**: Is a plain C# `event Action` (not async, not EventArgs). This means UI-thread dispatch must be done explicitly in the subscriber — the ViewModel calls `Dispatcher.UIThread.Post(...)`. The test used `[AvaloniaFact]` + `Dispatcher.UIThread.RunJobs()` to flush the dispatch queue synchronously.

- **`ContextMenuRegistry`**: A concrete class, not an interface implementation. Wrapping it in `StubContextMenuRegistry` for tests was straightforward because `IContextMenuRegistry.Register` is the only method tested.

---

**Q3: Design decisions beyond the instructions**

- **`TopicExplorerViewModel` uses `Dispatcher.UIThread.Post`** (fire-and-post) rather than `Dispatcher.UIThread.InvokeAsync` in `OnTopicRegistryChanged`. This matches the existing pattern in `ShellViewModel` (non-awaited post), avoids deadlocking if the registry fires on the UI thread, and makes the test deterministic via `RunJobs()`.

- **`DummyGeneratorService.TogglePublishing()` uses a lock + `CancellationTokenSource` swap** rather than a boolean flag. A boolean flag with `volatile` could race between the check and the start of a new task. The lock approach ensures atomicity of the stop/start transition.

- **`HeartbeatSample` is a `struct`** rather than a class. DDS value types are naturally struct-shaped, and the `DdsTopic`/`DdsKey` attribute scan in the engine works on any type — struct vs class is irrelevant to schema registration.

- **`StubDummyGeneratorService` planned but not used.** The plugin init tests (`ConfigureServices_RegistersDummyGeneratorService`, `Initialize_RegistersToolsMenu`, `Initialize_RegistersContextMenuProvider`) build a real `ServiceCollection`, add stubs for all other services, and check DI registrations directly. This was simpler and more thorough than subclassing.

---

**Q4: Edge cases discovered**

- **`RemoveAssembly` with out-of-range index:** The spec did not mention this. `IAssemblySourceService.Remove(index)` may throw if the index is invalid. `SchemaSourcesViewModel.RemoveAssembly` guards with a bounds check before calling the service, preventing crashes from stale UI state.

- **`DummyGeneratorService` — `GetWriter` may throw.** The spec said "acquires a writer via `IDdsBridge.GetWriter`." In practice `IDdsBridge.GetWriter` can throw if the DDS domain is not up (native library missing, etc.). The service wraps this in a try/catch, logs the failure, and disables itself gracefully rather than crashing the hosted service pipeline.

- **`PublishRateMs <= 0` (stress mode):** The spec noted this should use `await Task.Yield()` for cooperative multitasking. Without the yield, the loop would starve the thread pool. Added this guard.

- **Double-close of a window:** The disposal test `DoubleClose_NoException_NoDoubleDispose` confirms that removing the VM from `_viewModels` on first close means the second close finds no entry and silently skips. This is the correct behavior — `DisposeCount` stays at 1.

---

**Q5: Performance and threading concerns with `DummyGeneratorService`**

- **Backpressure:** The service does not back off if the DDS writer queue is full. At `PublishRateMs = 0` (stress mode), the publish loop will produce as fast as the writer allows. If the native writer blocks, the loop task blocks and the cancellation token check is deferred. This is acceptable for a diagnostic/test tool but would need a timeout or non-blocking write for production.

- **`_toggleLock`:** The lock is brief (creating/cancelling a `CancellationTokenSource`), so it won't block the UI thread meaningfully. The publish loop itself runs on a ThreadPool thread, never the UI thread.

- **`StopAsync` awaits the loop task.** This means `IHost.StopAsync` will wait for the current in-flight publish to complete before returning. At `PublishRateMs = 0` this could theoretically delay shutdown by one write round-trip. Acceptable; could be bounded with a `Task.WhenAny(loopTask, Task.Delay(500))` if needed.

---

**Q6: Suggested git commit message**

```
feat(ddsmon): ViewModel disposal + StandardPlugin (schema, topics, dummy)

CORRECTIVE-0: AvaloniaWindowManager now stores and disposes IDisposable
ViewModels when their panel windows are closed. OpenPanelWindow prefers
IServiceProvider.GetService over ActivatorUtilities.CreateInstance so
DI-registered factory delegates are respected.

TASK-C001: WorkspaceManagerPlugin with SchemaSourcesViewModel/View —
displays IAssemblySourceService entries, supports Add/Remove assembly.

TASK-C002: TopicExplorerPlugin with TopicExplorerViewModel/View —
subscribes to ITopicRegistry, filters hidden topics, persists ShowHidden
to IUserSettings, publishes SpawnPanelEvent on double-click.

TASK-D001: DummyDataGeneratorPlugin with DummyGeneratorService —
IHostedService that publishes HeartbeatSample on a configurable interval;
supports TogglePublishing(); HeartbeatSample is the pipeline proof type.

New test project: DdsMonitor.Avalonia.StandardPlugin.Tests (30 tests).
Total: 745 tests passing (0 failures).
```
