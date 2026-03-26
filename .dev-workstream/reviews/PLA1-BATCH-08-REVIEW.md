# PLA1-BATCH-08 Review

**Batch:** PLA1-BATCH-08  
**Reviewer:** Development Lead  
**Date:** 2026-03-27  
**Status:** APPROVED (with follow-up debt — **§10.2 TopicColorService**, **DetailPanel** tooltips)

**Report:** `.dev-workstream/reports/PLA1-BATCH-08-REPORT.md`

---

## Summary

**PLA1-DEBT-016** and **017** are implemented in source: **`PluginConfigService`** accepts an optional **`ILoggerFactory`**; **`Program.cs`** passes a bootstrap **`LoggerFactory.Create` + console** into **`AddDdsMonitorServices`**; **`SamplesPanel.ShowDetailTooltip`** and **`InstancesPanel`** pass **`ContextType`** / **`ContextValue`**; **`TooltipPortal`** splits provider HTML (plain **`MarkupString`**) from default JSON (still inside **`<pre class="tooltip-content">`**). **`StubTooltipPortal`** + five **`TooltipPortalTests`** cover the branching behavior.

**PLA1-P7-T01–T03** are present: **`DdsMonitor.Plugins.FeatureDemo`** builds, stages to **`plugins/`**, **`FeatureDemoPlugin`** wires the major extension points, **`DemoDashboardPanel`** subscribes to **`DemoBackgroundProcessor.OnUpdated`**.

Verified: **`dotnet test tests/DdsMonitor.Blazor.Tests/`** — **16/16** pass.

---

## Task-by-task verdict

| Task | Verdict |
|------|---------|
| **PLA1-DEBT-016** | **Met.** **`new PluginConfigService(loggerFactory)`** in **`ServiceCollectionExtensions`**; **`HostWiringTests`** still call **`AddDdsMonitorServices(configuration)`** only (optional param). |
| **PLA1-DEBT-017** | **Met** for batch wording (“at least one path”): **Samples** + **Instances** detail payload tooltips supply context; portal markup split is correct. **DetailPanel** still uses the 3-argument **`TooltipState`** only (```749:749:tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor```) — registry not used there; logged as **PLA1-DEBT-019**. bUnit tests target **`StubTooltipPortal`**, not the production **`TooltipPortal.razor`** — acceptable documented stub pattern (same as earlier batches). |
| **PLA1-P7-T01** | **Met** per inspection + report (Razor class library, Engine-only reference, solution entry, staging target). |
| **PLA1-P7-T02** | **Partially met vs design §10.2.** **PLA1-TASK-DETAIL** and **`PLA1-DESIGN.md` §10.2** list **`TopicColorService`**: “Colors any topic containing DEMO in red”. **`FeatureDemoPlugin`** does **not** call **`GetFeature<TopicColorService>()`** / **`RegisterColorRule`** (report explains scoped-vs-root **`GetFeature`** — credible, but **§10.2 is still incomplete**). **`IFilterMacroRegistry`** is **not** in the §10.2 table; omission is consistent with the written design. |
| **PLA1-P7-T03** | **Met.** Panel renders metrics; counter updates on the processor timer / store snapshot (not strictly “per sample” — acceptable for the stated background service). |

---

## Report accuracy

- **Logger factory lifetime:** The report states the factory is disposed right after **`AddDdsMonitorServices`** returns. In **`Program.cs`**, **`using var startupLoggers`** applies to the **whole top-level program** — disposal runs at **process shutdown**, not immediately after registration. That is **safer** for any **`ILogger`** reference held by **`PluginConfigService`**; the report’s timing description is **slightly wrong**, the **code behavior is fine**.

---

## Design alignment

- **§10 Phase 7:** Strong alignment except **TopicColorService** demo row (**PLA1-DEBT-018**).
- **§10.3 graceful degradation:** Null-conditional **`GetFeature`** usage matches intent.

---

## Tests: what matters

- **Strong:** corrupt-config logging via **`FakeLogger`**; tooltip stub matrix (provider match, null provider, no registry, wrong type, null context); existing Engine suite (report: 616).
- **Weak:** No automated test that **`FeatureDemoPlugin.Initialize`** registers **§10.2** completely (**Phase 8**); production **`TooltipPortal`** not mounted in bUnit.

---

## Suggested commit message

```
feat(dds-monitor): plugin config logging, tooltip context, Feature Demo plugin (PLA1-BATCH-08)

Wire optional ILoggerFactory into PluginConfigService and pass console LoggerFactory
from Program. Pass TopicType/Payload into SamplesPanel and InstancesPanel detail
tooltips; render tooltip provider HTML outside <pre>. Add FeatureDemo plugin with
dashboard panel, demo registrations, and plugins/ staging; add StubTooltipPortal tests.
```

---

## References

- **Next batch:** `.dev-workstream/batches/PLA1-BATCH-09-INSTRUCTIONS.md`
- **Debt:** **PLA1-DEBT-018**, **019** added post-review
