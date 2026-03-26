# Batch Report — PLA1-BATCH-01

**Batch Number:** PLA1-BATCH-01  
**Date Submitted:** 2026-03-26

---

## ✅ Completion Status

### Tasks Completed
- [x] PLA1-P1-T01: Redesign `IMonitorContext`
- [x] PLA1-P1-T02: Update `MonitorContext`
- [x] PLA1-P1-T03: Register core registries in host DI
- [x] PLA1-P1-T04: Migrate ECS plugin
- [x] PLA1-P2-T01: Create `IContextMenuRegistry`
- [x] PLA1-P2-T02: Implement `ContextMenuRegistry`
- [x] PLA1-P2-T03: Register `IContextMenuRegistry` in host DI

**Overall Status:** COMPLETE

---

## 🧪 Test Results

### Engine Tests (`tests/DdsMonitor.Engine.Tests/`)
```
Failed:  2, Passed: 560, Skipped: 0, Total: 562, Duration: ~4s

New tests added:
  IMonitorContextTests (3 tests) — all passing
  ContextMenuRegistryTests (5 tests) — all passing
```

The 2 failures are in `Batch28Tests.PluginLoader_ValidPlugin_ConfigureServicesCalled` and
`Batch28Tests.PluginLoader_AssemblyLoadContext_TypeIdentityPreserved`. Both tests pass when
run in isolation; they fail only in the full suite run due to a pre-existing file-locking
race condition: two Roslyn in-process GC compilation tests simultaneously write to the same
temp DLL file name. This was confirmed by running `git stash` and reproducing the same 2
failures from HEAD before any of my changes were present, and by running the tests in
isolation where both pass cleanly.

### ECS Plugin Tests (`tests/DdsMonitor.Plugins.ECS.Tests/`)
```
Failed:  4, Passed: 40, Skipped: 0, Total: 44, Duration: ~80ms

New tests added:
  EcsPluginTests (3 tests) — all passing
```

The 4 failures are all in `TimeTravelTests` and are pre-existing assertion failures unrelated
to this batch. Confirmed via `git stash` before my changes — identical 4 failures.

---

## 📝 Implementation Summary

### Files Added
```
tools/DdsMonitor/DdsMonitor.Engine/Plugins/ContextMenuItem.cs        — moved from Blazor; now shared with plugin authors
tools/DdsMonitor/DdsMonitor.Engine/Plugins/IContextMenuRegistry.cs   — P2-T01 interface
tools/DdsMonitor/DdsMonitor.Engine/Plugins/ContextMenuRegistry.cs    — P2-T02 thread-safe implementation
tests/DdsMonitor.Engine.Tests/Plugins/IMonitorContextTests.cs        — P1-T01 tests (3)
tests/DdsMonitor.Engine.Tests/Plugins/ContextMenuRegistryTests.cs    — P2-T02 tests (5)
tests/DdsMonitor.Plugins.ECS.Tests/EcsPluginTests.cs                 — P1-T04 tests (3)
```

### Files Modified
```
tools/DdsMonitor/DdsMonitor.Engine/Plugins/IMonitorContext.cs        — replaced properties with GetFeature<T>()
tools/DdsMonitor/DdsMonitor.Engine/Plugins/MonitorContext.cs         — constructor now takes IServiceProvider
tools/DdsMonitor/DdsMonitor.Engine/Plugins/IMonitorPlugin.cs        — updated XML doc cross-references
tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs — simplified MonitorContext factory; added IContextMenuRegistry singleton
tools/DdsMonitor/DdsMonitor.Plugins.ECS/EcsPlugin.cs                — migrated to GetFeature<T>()?.X pattern
tools/DdsMonitor/DdsMonitor.Blazor/Services/ContextMenuService.cs    — removed ContextMenuItem definition; added using
tools/DdsMonitor/DdsMonitor.Blazor/Components/_Imports.razor         — added @using DdsMonitor.Engine.Plugins
tests/DdsMonitor.Engine.Tests/Batch28Tests.cs                        — updated MonitorContext construction and plugin source string to new API
```

---

## 1. Issues Encountered and How They Were Fixed

**`ContextMenuItem` location.** The task spec says "use unchanged". The type currently lived
in `DdsMonitor.Services` inside the Blazor project, which Engine cannot reference. For
`IContextMenuRegistry` to live in Engine (so both plugins and tests can reference it without
a Blazor dependency), `ContextMenuItem` had to move to the Engine. I moved it to
`DdsMonitor.Engine.Plugins`, removed the duplicate definition from `ContextMenuService.cs`,
added a `using DdsMonitor.Engine.Plugins;` there, and added `@using DdsMonitor.Engine.Plugins`
to `_Imports.razor`. The Blazor components continue to use `ContextMenuItem` unchanged.

**`Batch28Tests` compile error.** `PluginLoader_ValidPlugin_InitializeReceivesContext` still
constructed `MonitorContext(menuRegistry, panelRegistry)` and its embedded plugin source
called `context.MenuRegistry.AddMenuItem(...)`. I updated the test to build a
`ServiceCollection`, add `IMenuRegistry` as singleton, build a `ServiceProvider`, and pass
that to `new MonitorContext(provider)`. The embedded plugin source was updated to use
`context.GetFeature<IMenuRegistry>()?.AddMenuItem(...)`.

**`Batch28Tests` IO failures at full suite.** Two tests that Roslyn-compile temporary
plugins fail when run in parallel because they share temp file names with other tests in
the class. This is a pre-existing issue — confirmed identical failures before my changes via
`git stash`. Not fixed in this batch (out of scope; would require changing the test
infrastructure to use unique temp paths per test instance).

---

## 2. What I Would Improve in the Plugin Host / Test Harness

- **`Batch28Tests` temp directories:** The class-level `_tempDir` is shared across all
  tests. Each `[Fact]` should use a unique subdirectory (e.g., a per-test GUID) to eliminate
  the file-locking race that causes two tests to fail under parallel execution.
- **`TimeTravelTests`:** Four pre-existing assertion failures need investigation —
  `TimeTravel_*` tests compare state machine outcomes that differ from expected values. This
  debt predates this batch.

---

## 3. Design Choices Beyond the Spec

- **`ContextMenuItem` moved to Engine, not duplicated.** The spec says "use unchanged". I
  chose to move rather than define a second identical type, because two types with the same
  name in different assemblies would cause an ambiguity error at Blazor call sites.
- **`ContextMenuRegistry` snapshot under lock, invoke outside lock.** Providers are invoked
  outside the lock so that a slow or blocking provider cannot starve registrations on other
  threads. This is the standard "copy-on-read" pattern.
- **`ILogger<ContextMenuRegistry>? logger = null`** follows the same optional-logger pattern
  already used by `PluginLoader`. This keeps the registry constructable in tests and in
  host production code without needing a separate test-double logger.

---

## 4. Edge Cases Found Not Mentioned in the Task Detail

- **`GetItems` with `null` context:** The implementation accepts `null` for the context
  argument (generic type parameter allows it for reference types). The tests exercise this by
  passing `null!` for `SampleData`. Works correctly because the delegate receives whatever the
  caller passes; providers that dereference context will throw and be caught+logged.
- **Provider list snapshot guarantees zero-copy on re-entrant reads:** If a provider's
  delegate internally calls `GetItems` again on the same registry instance, it gets the
  snapshot from before the re-entrant call — no deadlock, no infinite recursion.
- **Thread count in `RegisterProvider_IsThreadSafe`:** The test uses 20 threads × 5 items
  (100 total) with a `Barrier` for true simultaneous entry. This exercises the lock path
  under contention.
