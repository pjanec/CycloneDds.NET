# PLA1-BATCH-01: Capability Context + Context Menu Registry (Core)

**Batch Number:** PLA1-BATCH-01  
**Tasks:** PLA1-P1-T01, PLA1-P1-T02, PLA1-P1-T03, PLA1-P1-T04, PLA1-P2-T01, PLA1-P2-T02, PLA1-P2-T03  
**Phase:** PLA1 Phase 1 (complete) + Phase 2 (through host DI for context menus)  
**Estimated Effort:** 12–16 hours  
**Priority:** HIGH  
**Dependencies:** None (first PLA1 batch)

---

## 📋 Onboarding & Workflow

### Developer Instructions

This batch establishes the `GetFeature<T>()` pattern on `IMonitorContext`, migrates the ECS plugin, and adds the `IContextMenuRegistry` abstraction plus a thread-safe `ContextMenuRegistry` implementation registered in the host DI container. UI integration of context menus into Blazor panels is **not** in this batch (that is PLA1-P2-T04+).

Work in **task ID order** below; each task has detailed success criteria and test tables in the task detail document—read those sections before coding.

### Required Reading (in order)

1. **Workflow:** `.dev-workstream/guides/DEV-GUIDE.md` — batch reporting, questions, and expectations.
2. **Onboarding / tree:** `docs/plugin-api/PLA1-ONBOARDING.md` — solution layout, build commands, file map.
3. **Design:** `docs/plugin-api/PLA1-DESIGN.md` — [§4 Phase 1](../../docs/plugin-api/PLA1-DESIGN.md#4-phase-1--capability-querying-context-future-proof-foundation) and [§5 Phase 2](../../docs/plugin-api/PLA1-DESIGN.md#5-phase-2--context-menu-registry) (read for intent; do not re-derive requirements from design alone).
4. **Task specs:** `docs/plugin-api/PLA1-TASK-DETAIL.md` — sections for each task ID in this batch (success criteria and unit test tables are authoritative).
5. **Code standards:** `.dev-workstream/guides/CODE-STANDARDS.md` — applies to tests and production code.
6. **Previous review:** N/A (first PLA1 batch).

### Source Code Locations (repo root: `D:\Work\FastCycloneDdsCsharpBindings`)

| Area | Path |
|------|------|
| `IMonitorContext`, `MonitorContext`, plugin API | `tools/DdsMonitor/DdsMonitor.Engine/Plugins/` |
| Host service registration (Menu, panels, `IMonitorContext`) | `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs` |
| Blazor host entry (uses `IMonitorContext` at startup) | `tools/DdsMonitor/DdsMonitor.Blazor/Program.cs` |
| ECS plugin | `tools/DdsMonitor/DdsMonitor.Plugins.ECS/EcsPlugin.cs` |
| Engine tests | `tests/DdsMonitor.Engine.Tests/` |
| ECS plugin tests | `tests/DdsMonitor.Plugins.ECS.Tests/` |

### Builds and tests (from onboarding)

```powershell
dotnet build tools/DdsMonitor/DdsMonitor.Engine/DdsMonitor.Engine.csproj
dotnet build tools/DdsMonitor/DdsMonitor.Blazor/DdsMonitor.csproj
dotnet build tools/DdsMonitor/DdsMonitor.Plugins.ECS/DdsMonitor.Plugins.ECS.csproj -c Debug
dotnet test tests/DdsMonitor.Engine.Tests/
dotnet test tests/DdsMonitor.Plugins.ECS.Tests/
```

After changes, fix any **compile errors or test failures** in these projects before reporting. Run the **full** test projects above, not only new tests.

### Report and questions

- **Report:** `.dev-workstream/reports/PLA1-BATCH-01-REPORT.md`
- **Questions:** `.dev-workstream/questions/PLA1-BATCH-01-QUESTIONS.md` (only if something is truly ambiguous after reading task detail + design)

### Anti-laziness

Finish the batch end-to-end: implement, add/update tests, run builds and tests, fix root causes until green, then write the report. Do not stop to ask permission for obvious next steps (e.g. running `dotnet test`).

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **PLA1-P1-T01 / T02 / T03 / T04:** Implement and test through **Task 4** → **ALL tests pass** ✅  
2. **PLA1-P2-T01:** Interface → **solution builds** ✅  
3. **PLA1-P2-T02:** Registry implementation → new tests pass → **ALL tests pass** ✅  
4. **PLA1-P2-T03:** DI registration → **ALL tests pass** ✅  

**DO NOT** move to the next task until:

- Current task implementation is complete per `PLA1-TASK-DETAIL.md`
- Required tests exist and **ALL tests passing** (including prior PLA1-affected tests and unrelated regressions)

---

## Context

Phase 1 replaces fixed `IMonitorContext` properties with `GetFeature<T>()` so new host capabilities can be added without binary-breaking interface changes. Phase 2 begins with the context-menu registry type and its registration so later batches can wire Blazor panels.

**Task definitions (links):**

- [PLA1-P1-T01 – P1-T04](../../docs/plugin-api/PLA1-TASK-DETAIL.md#phase-1--capability-querying-context)
- [PLA1-P2-T01 – P2-T03](../../docs/plugin-api/PLA1-TASK-DETAIL.md#phase-2--context-menu-registry)

---

## 🎯 Batch Objectives

- `IMonitorContext` exposes only `GetFeature<TFeature>()`; `MonitorContext` resolves via `IServiceProvider`.
- Host registers services so `GetFeature<IMenuRegistry>()`, `GetFeature<PluginPanelRegistry>()`, and (after P2-T03) `GetFeature<IContextMenuRegistry>()` work.
- ECS plugin uses optional chaining on `GetFeature` and never throws when features are missing.
- `ContextMenuRegistry` meets behavior and thread-safety requirements in `PLA1-TASK-DETAIL.md`.

---

## ✅ Tasks

For each task, follow **Scope**, **Success Criteria**, and **Unit Test Specifications** in `docs/plugin-api/PLA1-TASK-DETAIL.md`. This section only points at files and gotchas; it does not replace the task detail doc.

### PLA1-P1-T01: Redesign `IMonitorContext`

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/IMonitorContext.cs` (MODIFY)  
**Tests:** `tests/DdsMonitor.Engine.Tests/Plugins/IMonitorContextTests.cs` (NEW or extend if present)

**Notes:** XML docs must describe the graceful-degradation contract (`null` when unsupported).

---

### PLA1-P1-T02: Update `MonitorContext`

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/MonitorContext.cs` (MODIFY)

**Notes:** Constructor should accept `IServiceProvider` (or narrow abstraction if already present—prefer consistency with existing engine patterns). Implement `GetFeature` via `GetService<TFeature>()`.

---

### PLA1-P1-T03: Register core registries in host DI

**Files:** Primarily `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs` (MODIFY).  
**Also:** Update any `MonitorContext` factory registration that still passes `IMenuRegistry` + `PluginPanelRegistry` directly.

**Notes:** Task detail allows `Program.cs`; in this solution, singleton wiring for plugins lives in `ServiceCollectionExtensions.cs` (see current `AddSingleton<IMonitorContext>(...)`). Preserve existing lifetimes: `IMenuRegistry` and `PluginPanelRegistry` remain singletons; `IMonitorContext` must resolve to `MonitorContext`. Confirm `Program.cs` still obtains `IMonitorContext` successfully at startup after your change.

---

### PLA1-P1-T04: Migrate ECS plugin

**Files:** `tools/DdsMonitor/DdsMonitor.Plugins.ECS/EcsPlugin.cs` (MODIFY)  
**Tests:** `tests/DdsMonitor.Plugins.ECS.Tests/EcsPluginTests.cs` (MODIFY)

**Notes:** Update XML references to old property names on `IMonitorPlugin` / `IMonitorContext` if present. Implement the three tests named in `PLA1-TASK-DETAIL.md` for this task.

---

### PLA1-P2-T01: Create `IContextMenuRegistry`

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/IContextMenuRegistry.cs` (NEW)

**Notes:** Namespace `DdsMonitor.Engine.Plugins`; exactly two methods per task detail.

---

### PLA1-P2-T02: Implement `ContextMenuRegistry`

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/ContextMenuRegistry.cs` (NEW)  
**Tests:** `tests/DdsMonitor.Engine.Tests/Plugins/ContextMenuRegistryTests.cs` (NEW)

**Notes:** Implement all tests listed in the task detail table, including thread-safety and isolated provider failures (log + continue; no crash). Use the engine’s existing logging patterns; avoid swallowing exceptions silently without logging.

---

### PLA1-P2-T03: Register `IContextMenuRegistry` in host DI

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs` or `tools/DdsMonitor/DdsMonitor.Blazor/Program.cs` (wherever sibling singletons are registered—stay consistent)

**Notes:** Singleton `ContextMenuRegistry` as `IContextMenuRegistry`. After this task, a full host `IServiceProvider` must resolve `IContextMenuRegistry`, and `GetFeature<IContextMenuRegistry>()` on `IMonitorContext` must return a non-null instance in normal UI configuration.

---

## 🧪 Testing Requirements

- New/modified tests must assert **behavior**, not only compilation or string fragments in source (see `.dev-workstream/guides/DEV-LEAD-GUIDE.md` test-quality expectations).
- Minimum: all tests specified in `PLA1-TASK-DETAIL.md` for P1-T01 and P1-T04, plus all five scenarios for `ContextMenuRegistry` (P2-T02).
- Update any tests elsewhere that construct `MonitorContext` with the old constructor.

---

## 📊 Report Requirements

Answer in `.dev-workstream/reports/PLA1-BATCH-01-REPORT.md`:

1. What issues did you hit (API surprises, DI ordering, ECS tests)? How did you fix them?
2. What would you improve in the current plugin host or test harness?
3. What design choices did you make beyond the written spec?
4. Any edge cases you found that the task detail did not mention?

Do not use the report for “explain what `GetFeature` is” type filler.

---

## 🎯 Success Criteria (batch complete when)

- [ ] PLA1-P1-T01–T04 and PLA1-P2-T01–T03 meet success criteria in `PLA1-TASK-DETAIL.md`
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/` and `dotnet test tests/DdsMonitor.Plugins.ECS.Tests/` pass
- [ ] Applicable production projects build without new warnings you introduced
- [ ] Report submitted to `.dev-workstream/reports/PLA1-BATCH-01-REPORT.md`

---

## ⚠️ Common Pitfalls

- Leaving call sites on `context.MenuRegistry` / `context.PanelRegistry` anywhere in the repo (search the solution).
- Registering `IMonitorContext` before `IServiceProvider` is usable in the `MonitorContext` factory—use the correct overload of `AddSingleton` / factory delegate.
- `ContextMenuRegistry`: one provider’s exception must not hide results from others; confirm with tests.

---

## 📚 Reference Materials

- `docs/plugin-api/PLA1-TASK-TRACKER.md`
- `docs/plugin-api/PLA1-TASK-DETAIL.md` — authoritative per-task requirements
- `docs/plugin-api/PLA1-DESIGN.md` — §4, §5
- `docs/plugin-api/PLA1-ONBOARDING.md` — build/orientation
