# PLA1-BATCH-10: Phase 8 hardening + PLA1 tech-debt closure

**Batch Number:** PLA1-BATCH-10  
**Tasks (order):** PLA1-DEBT-020, PLA1-DEBT-021, PLA1-DEBT-022, then optional polish below  
**Phase:** Post–Phase 8 — spec alignment and PLA1 wrap-up  
**Estimated Effort:** 10–18 hours  
**Priority:** MEDIUM  
**Dependencies:** PLA1-BATCH-09 (reviewed)

---

## Context

**PLA1** task phases **1–8** are complete on the tracker, but the BATCH-09 review recorded **three** follow-up items so debt does not linger.

---

## Developer instructions

Work **PLA1-DEBT-020** → **021** → **022** in order unless **022** is resolved quickly by product verification (“workspace path never changes without restart” → document in **`PLA1-DESIGN.md`** or **`DEV-GUIDE`** and mark resolved).

### Required reading

1. `.dev-workstream/reviews/PLA1-BATCH-09-REVIEW.md`
2. `docs/plugin-api/PLA1-DEBT-TRACKER.md` — rows **020–022**
3. `docs/plugin-api/PLA1-TASK-DETAIL.md` — **PLA1-P8-T05** (strict scope)
4. `docs/plugin-api/PLA1-DESIGN.md` — [§11](PLA1-DESIGN.md#11-phase-8--autonomous-ci-testing)

### Paths

| Area | Path |
|------|------|
| Headless integration | `tests/DdsMonitor.Plugins.FeatureDemo.Tests/HeadlessPluginIntegrationTest.cs` (extend or add sibling) |
| Feature demo tests | `tests/DdsMonitor.Plugins.FeatureDemo.Tests/FeatureDemoPluginTests.cs` |
| Host / color | `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs`, `TopicColorService.cs`, `WorkspaceState.cs` |

### Builds / tests

```powershell
dotnet test tests/DdsMonitor.Plugins.FeatureDemo.Tests/
dotnet test tests/DdsMonitor.Engine.Tests/
dotnet test tests/DdsMonitor.Blazor.Tests/
```

---

## Mandatory workflow

1. **PLA1-DEBT-020** — Add an integration test (or replace the current one) that follows **`PLA1-P8-T05`** **`Scope`** as written: **headless** host via **`AddDdsMonitorServices`** (or equivalent documented subset), **Feature Demo** enabled through **`PluginConfigService`** / **`PluginLoader`** path, and a **fake `ISampleStore` that contains or receives 10 `SampleData` with `DemoPayload`** where feasible. Keep **&lt; 5 s** and **`ProcessedCount >= 1`**. If the full stack is impractical in CI, update **`PLA1-TASK-DETAIL.md`** with a lead-approved **narrower scope** and still keep one “full path” test where possible. ✅  

2. **PLA1-DEBT-021** — Extend **`FeatureDemoPluginTests.Initialize_WhenAllFeaturesAvailable_RegistersAllExtensionPoints`** (or split into focused tests) to cover **export format**, **value formatter**, **type drawer**, and **`IExportFormatRegistry` / registry APIs** as appropriate; optionally **`IFilterMacroRegistry`** if the demo registers a macro. Alternatively **rename** the test to match what it asserts. ✅  

3. **PLA1-DEBT-022** — Confirm whether **workspace file path** can change at runtime. If **yes**, align **TopicColorService** persistence with **IWorkspaceState** (e.g. inject **IWorkspaceState** factory, refresh on workspace events, or document restart requirement). If **no**, add a **one-line design doc note** and close the debt. ✅  

---

## Optional polish (if time remains)

- **PLA1-P8-T04 / P5-T03:** Mount **real** **`PluginManagerPanel.razor`** in bUnit by referencing **`DdsMonitor.Blazor`** from a test project that already tolerates the Web SDK stack, **or** leave as-is and note in **`PLA1-TASK-DETAIL`**.
- Update **`docs/plugin-api/PLA1-TASK-TRACKER.md`** **Project Status** to “PLA1 complete (maintenance mode)” when debts **020–022** are closed.

---

## Success criteria

- [ ] **PLA1-DEBT-020**–**022** resolved in **`PLA1-DEBT-TRACKER.md`**
- [ ] Tests above green; report **`.dev-workstream/reports/PLA1-BATCH-10-REPORT.md`**

---

## References

- `docs/plugin-api/PLA1-TASK-TRACKER.md`
