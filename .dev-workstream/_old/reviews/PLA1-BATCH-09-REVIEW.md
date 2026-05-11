# PLA1-BATCH-09 Review

**Batch:** PLA1-BATCH-09  
**Reviewer:** Development Lead  
**Date:** 2026-03-27  
**Status:** APPROVED (with P3/P4 follow-up debt — **P8-T05** scope, **TopicColorService** workspace snapshot)

**Report:** `.dev-workstream/reports/PLA1-BATCH-09-REPORT.md`

---

## Summary

**PLA1-DEBT-018** is fixed in code: **`TopicColorService`** is a **singleton** in **`AddDdsMonitorServices`**, built with **`new WorkspaceState(sp.GetService<AppSettings>())`**; **`Program.cs`** drops the old **`AddScoped<TopicColorService>`**; **`FeatureDemoPlugin`** calls **`RegisterColorRule`** for topic short names containing **`DEMO`**.

**PLA1-DEBT-019** is fixed: **`DetailPanel.ShowJsonTooltip`** passes **`ContextType`** / **`ContextValue`** from **`_currentSample`** (```749:754:tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor```).

**Phase 8:** **`DdsMonitor.Plugins.FeatureDemo.Tests`** exists (Razor SDK, bUnit), **`FeatureDemoPluginTests`** cover menus, panels, context menu, sample view, tooltip, **TopicColorService** rule (**`GetEffectiveColor("MY_DEMO_TOPIC")`**), workspace events, and null-context **`Initialize`**. **`DemoDashboardPanelTests`** and **`HeadlessPluginIntegrationTest`** exist.

Verified: **`dotnet test tests/DdsMonitor.Plugins.FeatureDemo.Tests/`** — **10/10** pass.

---

## Task-by-task verdict

| Task | Verdict |
|------|---------|
| **PLA1-DEBT-018** | **Met** for §10.2 intent + **`GetFeature<TopicColorService>()`** from root. |
| **PLA1-DEBT-019** | **Met.** |
| **PLA1-P8-T01** | **Met** (project + solution). Moq not used; stubs match repo style — acceptable. |
| **PLA1-P8-T02** | **Mostly met.** `Initialize_WhenAllFeaturesAvailable_RegistersAllExtensionPoints` validates several registries and **TopicColorService**; it does **not** assert **export**, **value formatter**, **type drawer**, or **filter macro** registrations — name oversells coverage (**PLA1-DEBT-021**). |
| **PLA1-P8-T03** | **Met** per **`DemoDashboardPanelTests`**. |
| **PLA1-P8-T04** | **Met** per batch-09 decision: canonical **`tests/DdsMonitor.Blazor.Tests/Components/PluginManagerPanelTests.cs`** (stub still not real **`PluginManagerPanel.razor`** — pre-existing limitation). |
| **PLA1-P8-T05** | **Partially met vs `PLA1-TASK-DETAIL`.** Spec asks **`HeadlessMode`** container, **`PluginConfigService`** enablement, and a store feeding **10 × `DemoPayload`**. Implemented test uses a **minimal** **`ServiceCollection`** (no **`AddDdsMonitorServices`**, no **loader**, **`FakeSampleStore`** only exposes **`TotalCount`** — no **`Append`** of ten samples). **`ProcessedCount >= 1`** and **&lt; 5 s** are satisfied; the scenario is **not** the documented integration path. **PLA1-DEBT-020**. |

---

## Design / architecture notes

- **Singleton `TopicColorService`** holds a **`WorkspaceState` snapshot** at container build time. **`IWorkspaceState`** remains **scoped** with a **fresh** **`WorkspaceState`** per scope but the same **`AppSettings`** shape — paths match **until** runtime workspace path overrides on **`AppSettings`** would diverge from the singleton’s captured state. Worth a short comment or follow-up if the app ever mutates workspace location without restarting (**PLA1-DEBT-022**).

---

## Tests: what matters

- **Strong:** DEMO color rule (**integration of DEBT-018**); graceful **`Initialize`** with null features; workspace save/load no-throw; processor tick under timeout.
- **Weak:** **P8-T05** vs written scope; **`RegistersAllExtensionPoints`** not exhaustive; **PluginManager** still stub-only.

---

## Suggested commit message

```
test(plugins): FeatureDemo.Tests, TopicColorService singleton, tooltip context (PLA1-BATCH-09)

Register TopicColorService in AddDdsMonitorServices for GetFeature from plugins; register
DEMO topic color rule in FeatureDemoPlugin. Pass ContextType/ContextValue from DetailPanel
JSON tooltips. Add FeatureDemo.Tests (plugin registration, dashboard bUnit, headless processor).
```

---

## References

- **Next batch:** `.dev-workstream/batches/PLA1-BATCH-10-INSTRUCTIONS.md`
- **Debt:** **PLA1-DEBT-020**–**022** added post-review
