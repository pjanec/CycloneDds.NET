# BATCH-01: Engine Purification + Avalonia Shell Foundation

**Batch Number:** BATCH-01  
**Tasks:** TASK-A001, TASK-B001, TASK-B002, TASK-B003  
**Phase:** Phase 0 (Engine Purification) + Phase 1 (Empty Shell)  
**Estimated Effort:** 16–20 hours  
**Priority:** HIGH  
**Dependencies:** None (first batch)

---

## 📋 Onboarding & Workflow

### Developer Instructions

You are building the foundational infrastructure for the `DdsMonitor.Avalonia` desktop application —
a VS Code-style plugin shell that replaces the existing `DdsMonitor.Blazor` UI. This batch does four things:

1. **Purify the Engine** so it no longer depends on `Microsoft.AspNetCore.Components`.
2. **Create `DdsMonitor.Avalonia.Core`** — the Avalonia-specific shared contract library.
3. **Create `DdsMonitor.Avalonia`** — the shell executable.
4. **Wire the PluginLoader** into the shell to prove multi-plugin-per-assembly loading.

These tasks are sequential by nature. Complete each task and get its tests passing **before** moving on.

### Required Reading (IN ORDER)

1. **Onboarding guide:** `.dev-workstream/avalonia-mvp/ONBOARDING.md` — key existing files, codebase map.
2. **Design document:** `.dev-workstream/avalonia-mvp/DESIGN.md` — read all of §1–§5 and §11 carefully. §4 is Phase 0, §5 is Phase 1.
3. **Task Details:** `.dev-workstream/avalonia-mvp/TASK-DETAIL.md` — TASK-A001, TASK-B001, TASK-B002, TASK-B003 (Phase 0 and Phase 1 sections).
4. **Developer Skill:** `.github/skills/developer/SKILL.md` — how to work with the batch system.

### Source Code Locations

- **Engine (to be purified):** `tools/DdsMonitor/DdsMonitor.Engine/`
  - `Ui/ITypeDrawerRegistry.cs` — uses `RenderFragment<DrawerContext>` today
  - `Ui/TypeDrawerRegistry.cs` — concrete implementation with many builder methods
  - `Ui/DrawerContext.cs` — has `IHandleEvent? Receiver` to remove
  - `Plugins/ISampleViewRegistry.cs` — uses `RenderFragment<SampleData>` today
- **Blazor (needs adapters):** `tools/DdsMonitor/DdsMonitor.Blazor/`
- **New projects to create:** `tools/DdsMonitor/DdsMonitor.Avalonia.Core/`, `tools/DdsMonitor/DdsMonitor.Avalonia/`
- **Existing Engine tests:** `tests/DdsMonitor.Engine.Tests/`
- **Existing Blazor tests:** `tests/DdsMonitor.Blazor.Tests/`

### Report Submission

**When done, submit your report to:**  
`.dev-workstream/avalonia-mvp/reports/BATCH-01-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/avalonia-mvp/questions/BATCH-01-QUESTIONS.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **TASK-A001:** Purify Engine → Write tests → **ALL tests pass** ✅
2. **TASK-B001:** Create Avalonia.Core → Write tests → **ALL tests pass** ✅
3. **TASK-B002:** Create Shell → Write tests → **ALL tests pass** ✅
4. **TASK-B003:** Wire PluginLoader → Write tests → **ALL tests pass** ✅

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written
- ✅ **ALL tests passing** (including all previous batch tests)

**Why:** Ensures each component is solid before building on top of it. Prevents cascading failures.

**No permission needed to run tests, fix failures, or make obvious design calls. Work autonomously until every success criterion is met, then write the report.**

---

## 📌 TASK-A001 — Remove Blazor Types from Engine Registries

**Design Reference:** `.dev-workstream/avalonia-mvp/DESIGN.md` §4 — "Phase 0 — Engine Purification"  
**Task Detail Reference:** `.dev-workstream/avalonia-mvp/TASK-DETAIL.md` TASK-A001 section  

### What To Do

See the complete scope, constraints, and success conditions in TASK-DETAIL.md#TASK-A001. Summary:

1. **`ITypeDrawerRegistry.cs`** — Replace `RenderFragment<DrawerContext>` with `Func<DrawerContext, object>` in the interface.
2. **`TypeDrawerRegistry.cs`** — Update concrete class and all its private builder methods (BuildTextDrawer, BuildNumberDrawer, etc.) to produce `Func<DrawerContext, object>` instead of `RenderFragment<DrawerContext>`. The logic inside the builders is unchanged — just wrap the Blazor builder's return into a `Func<DrawerContext, object>` that returns the same delegate (or adapt the internal patterns).
3. **`DrawerContext.cs`** — Remove the `IHandleEvent? Receiver` constructor parameter and `Receiver` property.
4. **`ISampleViewRegistry.cs`** — Replace `RenderFragment<SampleData>` with `Func<SampleData, object>`.
5. **`SampleViewRegistry.cs`** — Update the concrete implementation to match.
6. **Blazor adapter** — Create an adapter class (e.g., `BlazorTypeDrawerAdapter`) in `DdsMonitor.Blazor` that bridges `Func<DrawerContext, object>` back to `RenderFragment<DrawerContext>` for all Blazor components that consume these registries.

### Tests To Write (in `tests/DdsMonitor.Engine.Tests/`)

Add a new test class `TypeDrawerRegistryPurificationTests.cs`:
- Register a `Func<DrawerContext, object>` for `int` → `GetDrawer(typeof(int))` returns non-null factory, same delegate.
- Register a factory for `string`, call `GetDrawer(typeof(string))`, assert non-null.
- `DrawerContext` constructed without `Receiver` parameter compiles and the property is absent.
- `SampleViewRegistry`: register a `Func<SampleData, object>` for a dummy type, `GetViewer(dummyType)` returns non-null.

### Success Gate

```
dotnet build tools/DdsMonitor/DdsMonitor.Engine
dotnet build tools/DdsMonitor/DdsMonitor.Blazor
dotnet test tests/DdsMonitor.Engine.Tests
dotnet test tests/DdsMonitor.Blazor.Tests
```
All must succeed with zero errors.

---

## 📌 TASK-B001 — Create `DdsMonitor.Avalonia.Core` Project

**Design Reference:** `.dev-workstream/avalonia-mvp/DESIGN.md` §5.1  
**Task Detail Reference:** `.dev-workstream/avalonia-mvp/TASK-DETAIL.md` TASK-B001 section  

### What To Do

See the complete scope, constraints, and success conditions in TASK-DETAIL.md#TASK-B001. Key points:

Create `tools/DdsMonitor/DdsMonitor.Avalonia.Core/DdsMonitor.Avalonia.Core.csproj` targeting `net8.0`, referencing `Avalonia` NuGet package (version **11.2.x** — match whatever the main solution uses or pick latest stable 11.x) and `DdsMonitor.Engine`.

Implement these types (all in the new project):

- **`IToolbarRegistry`** + **`ToolbarRegistry`**: `Register(id, onClick, iconKey?, tooltip)`, `Entries` property, `Changed` event.
- **`ToolbarEntry`** record: `Id`, `Action`, `IconKey?`, `Tooltip` fields.
- **`IUserSettings`** + **`UserSettingsStore`**: uses `System.Text.Json`, saves to `%APPDATA%\DdsMonitor\settings.json`. `SaveAsync()` must be debounced (replace pending save if another arrives within 500 ms).
- **`IStatefulViewModel`**: single method `void Initialize(IDictionary<string, object> componentState)`.
- **`IAvaloniaViewRegistry`** + **`AvaloniaViewRegistry`**: generic `Register<TViewModel>(Func<TViewModel, Control> viewFactory)`, `BuildView(object viewModel)` throws `InvalidOperationException` on unknown ViewModel type.
- **`IAvaloniaTypeDrawerRegistry`** + **`AvaloniaTypeDrawerRegistry`**: `Register(Type type, Func<AvaloniaDrawerContext, Control> factory)`, `Build(AvaloniaDrawerContext ctx)` — explicit cast to `Control`, throw `InvalidCastException` immediately if not a `Control`; generic reflection-walker fallback for unknown types.
- **`AvaloniaDrawerContext`**: mirrors Engine's `DrawerContext` without the Blazor `Receiver`. Holds `Type TargetType`, `object? Value`, `Action<object?> OnChange`, `Action<string> OnValidationError`.
- **`IEventBrokerExtensions`** static class with `SubscribeOnUiThread<TEvent>(this IEventBroker, Action<TEvent>, Dispatcher)` that marshals the handler via `Dispatcher.UIThread.InvokeAsync`.

### Tests To Write (create `tests/DdsMonitor.Avalonia.Core.Tests/`)

Write a new xUnit test project. Minimum 20 test methods covering:

- `UserSettingsStore`: `Set` + `SaveAsync` + read file from disk → key present with correct value.
- `UserSettingsStore`: `Get<bool>` before any `Set` → returns default `false`.
- `UserSettingsStore`: `Get<string>` round-trip → correct value.
- `UserSettingsStore`: `SaveAsync` called twice rapidly → only one file write (debounce).
- `ToolbarRegistry`: register two entries → `Entries` has both in order; `Changed` fires once per registration.
- `ToolbarRegistry`: register same id twice → second registration replaces first OR second entry appended (document your choice).
- `AvaloniaTypeDrawerRegistry`: register factory for `string` → `Build` returns non-null `Control`.
- `AvaloniaTypeDrawerRegistry`: register factory returning plain `object` (not a `Control`) → `Build` throws `InvalidCastException`.
- `AvaloniaTypeDrawerRegistry`: unknown type with string-like properties → fallback walker returns non-null `StackPanel` (or similar container).
- `AvaloniaViewRegistry`: register factory for TestViewModel → `BuildView` returns non-null `Control`.
- `AvaloniaViewRegistry`: call `BuildView` for unregistered type → `InvalidOperationException`.
- `SubscribeOnUiThread`: publish on background thread → handler fires (verify by checking result from handler call; Avalonia headless test fixture may be needed for UIThread checks).

**Note on Avalonia headless testing:** Use `Avalonia.Headless.XUnit` for tests that require the Avalonia UI thread. Configure the test assembly with `[assembly: AvaloniaTestApplication(typeof(TestApp))]` pattern.

### Success Gate

```
dotnet build tools/DdsMonitor/DdsMonitor.Avalonia.Core
dotnet test tests/DdsMonitor.Avalonia.Core.Tests
```

---

## 📌 TASK-B002 — Create `DdsMonitor.Avalonia` Shell Project

**Design Reference:** `.dev-workstream/avalonia-mvp/DESIGN.md` §5.2  
**Task Detail Reference:** `.dev-workstream/avalonia-mvp/TASK-DETAIL.md` TASK-B002 section  

### What To Do

See the complete scope, constraints, and success conditions in TASK-DETAIL.md#TASK-B002.

Create `tools/DdsMonitor/DdsMonitor.Avalonia/DdsMonitor.Avalonia.csproj` as Avalonia desktop app targeting `net8.0`. NuGet refs: `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`, `Microsoft.Extensions.Hosting`.

Key implementation points:

1. **`Program.cs`**: Generic Host bootstrap → `AddDdsMonitorServices` → register `IToolbarRegistry`, `IUserSettings`, `IAvaloniaViewRegistry`, `IAvaloniaTypeDrawerRegistry`, `IWindowManager` → build host → dual-boot decision based on `DdsSettings.HeadlessMode`.
2. **`App.axaml.cs`**: Wire `IServiceProvider` from the Generic Host into Avalonia's DI.
3. **`ShellWindow.axaml`**: Top `Menu` (bound to `IMenuRegistry`), `StackPanel` toolbar (bound to `IToolbarRegistry.Entries`), transport button row (`▶ Play`, `⏸ Pause`, `⏹ Reset` — verify exact `IDdsBridge` API method names from `tools/DdsMonitor/DdsMonitor.Blazor/MainLayout.razor`), status bar placeholder.
4. **`AvaloniaWindowManager.cs`**: implements `IWindowManager` — `SpawnPanel`, `ClosePanel`, `BringToFront`, `ShowPanel`. Focus existing window if already open (same `PanelId`). On window close: persist geometry to `ComponentState["__window"]`, publish `WorkspaceSaveRequestedEvent`.
5. **`ViewLocator.cs`**: delegates to `IAvaloniaViewRegistry.BuildView`.

Dynamically bind the `Menu` to `IMenuRegistry` — it must react to `IMenuRegistry.Changed`. Add a shell-built-in `"File/Exit"` top-level menu item. Dynamically bind the toolbar to `IToolbarRegistry.Changed`.

### Tests To Write (create `tests/DdsMonitor.Avalonia.Tests/`)

Use `Avalonia.Headless.XUnit`. Minimum 15 test methods:

- Shell builds and `ShellWindow` instantiates without exception (headless).
- `ShellWindow` has a `Menu` with at least `"File"` entry after initialization.
- `ShellWindow` has Play, Pause, Reset buttons in the transport row.
- `AvaloniaWindowManager.SpawnPanel("Test", null)` with a registered test ViewModel → window opens (window count = 1).
- `AvaloniaWindowManager.SpawnPanel` called twice with same panel id → window count stays 1, focus called.
- `AvaloniaWindowManager.ClosePanel` removes the window.
- Window close → geometry saved to `ComponentState["__window"]` (keys `X`, `Y`, `Width`, `Height` present).
- Re-spawn after close with previous `ComponentState` → window geometry restored.
- Headless mode (`--DdsSettings:HeadlessMode=Record`): dual-boot branch taken, no Avalonia window created.
- Menu reacts to `IMenuRegistry` item added after `ShellWindow.InitializeComponent()`.
- Toolbar reacts to `IToolbarRegistry.Register` called after toolbar is shown.
- Pause button click → `IDdsBridge` pause method called (mock bridge).
- Play button click → `IDdsBridge` play/resume method called (mock bridge).

### Success Gate

```
dotnet build tools/DdsMonitor/DdsMonitor.Avalonia
dotnet test tests/DdsMonitor.Avalonia.Tests
```

---

## 📌 TASK-B003 — Integrate PluginLoader + InitializePlugins into Shell

**Design Reference:** `.dev-workstream/avalonia-mvp/DESIGN.md` §5.2 "Generic Host Bootstrap"  
**Task Detail Reference:** `.dev-workstream/avalonia-mvp/TASK-DETAIL.md` TASK-B003 section  

### What To Do

See the complete scope, constraints, and success conditions in TASK-DETAIL.md#TASK-B003.

- Add `DdsMonitor.Avalonia.Core` assembly name to `PluginLoader.SharedAssemblyNames` in the shell bootstrap so type identity is maintained across the plugin ALC boundary.
- Call `pluginLoader.InitializePlugins(monitorContext)` in `Program.cs` after `host.StartAsync()` and before `ShellWindow` is shown.
- Verify `IMonitorContext.GetFeature<IToolbarRegistry>()`, `GetFeature<IAvaloniaTypeDrawerRegistry>()`, `GetFeature<IUserSettings>()` all return non-null from a plugin's `Initialize`.
- The `./plugins` folder missing must be silently ignored (Engine already handles this, but verify with a test).

### Tests To Write (extend `tests/DdsMonitor.Avalonia.Tests/`)

Create a helper test plugin assembly (two `IMonitorPlugin` classes in one DLL) compiled in the test setup, placed in a temp `./plugins` folder:

- Both `ConfigureServices` and both `Initialize` are called (confirmed by a flag/counter in the test plugin).
- `IMonitorContext.GetFeature<IToolbarRegistry>()` returns non-null inside `Initialize`.
- `IMonitorContext.GetFeature<IAvaloniaTypeDrawerRegistry>()` returns non-null.
- `IMonitorContext.GetFeature<IUserSettings>()` returns non-null.
- A corrupt DLL in `./plugins` does not crash the shell (error logged, startup continues).

### Success Gate

```
dotnet build tools/DdsMonitor/DdsMonitor.Avalonia
dotnet test tests/DdsMonitor.Avalonia.Tests
dotnet test tests/DdsMonitor.Engine.Tests
dotnet test tests/DdsMonitor.Blazor.Tests
```

---

## ⚠️ Quality Standards

### Test Quality Expectations

**❗ NOT ACCEPTABLE:**
- Tests that only verify code compiles or that a method returns non-null without checking meaningful behavior.
- Tests that stub out 100% of the system under test (testing nothing real).
- Tests with empty assertions or `Assert.True(true)`.
- Tests that do not reflect the success conditions in TASK-DETAIL.md.

**✅ REQUIRED:**
- Tests that validate actual behavior: correct values returned, events fired, side-effects produced.
- Tests for error paths: missing registrations throw the right exception, corrupt plugin DLLs don't crash.
- Tests are named to describe the scenario: `UserSettingsStore_GetBeforeSet_ReturnsDefault`.
- For Avalonia tests: use Avalonia headless testing infrastructure (`Avalonia.Headless.XUnit`), not mocked UI.

### Code Quality Expectations

- No `using Microsoft.AspNetCore.*` anywhere in the Engine after TASK-A001.
- The Blazor app must still build and all its tests pass after the adapters are in place.
- `AvaloniaWindowManager` must never throw if a panel is already open — it focuses instead.
- All `IEventBroker` subscriptions in ViewModels must be stored as `IDisposable` tokens and disposed in `Dispose()`.

### Report Quality Expectations

Write a thorough report. The review will check actual source files, not just the report.

---

## 🎯 Success Criteria (Batch Done When)

- [ ] `dotnet build tools/DdsMonitor/DdsMonitor.Engine` — zero errors, zero `AspNetCore` warnings.
- [ ] `dotnet build tools/DdsMonitor/DdsMonitor.Blazor` — zero errors.
- [ ] `dotnet build tools/DdsMonitor/DdsMonitor.Avalonia.Core` — zero errors.
- [ ] `dotnet build tools/DdsMonitor/DdsMonitor.Avalonia` — zero errors.
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests` — all pass.
- [ ] `dotnet test tests/DdsMonitor.Blazor.Tests` — all pass.
- [ ] `dotnet test tests/DdsMonitor.Avalonia.Core.Tests` — all pass.
- [ ] `dotnet test tests/DdsMonitor.Avalonia.Tests` — all pass.
- [ ] BATCH-01-REPORT.md submitted.

---

## 📚 Reference Materials

- **Task Detail:** `.dev-workstream/avalonia-mvp/TASK-DETAIL.md` — TASK-A001, TASK-B001, TASK-B002, TASK-B003
- **Design:** `.dev-workstream/avalonia-mvp/DESIGN.md` — §1–§5, §11
- **Onboarding:** `.dev-workstream/avalonia-mvp/ONBOARDING.md`
- **Engine Ui types:** `tools/DdsMonitor/DdsMonitor.Engine/Ui/` — ITypeDrawerRegistry, TypeDrawerRegistry, DrawerContext
- **Engine Plugin types:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/` — ISampleViewRegistry, IMonitorPlugin, PluginLoader
- **Engine existing tests:** `tests/DdsMonitor.Engine.Tests/`
- **Blazor layout (for transport API names):** `tools/DdsMonitor/DdsMonitor.Blazor/` — MainLayout.razor or equivalent

---

## 💡 Developer Insights Section

When writing your report, answer these questions:

**Q1:** What issues did you encounter during implementation? How did you resolve them?

**Q2:** Did you spot any weak points in the existing codebase? What would you improve?

**Q3:** What design decisions did you make beyond the instructions? What alternatives did you consider?

**Q4:** What edge cases did you discover that weren't mentioned in the spec?

**Q5:** Are there any performance concerns or optimization opportunities you noticed?

**Q6:** What is your suggested git commit message for this batch?
