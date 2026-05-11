# PLA1-BATCH-06 Review

**Batch:** PLA1-BATCH-06  
**Reviewer:** Development Lead  
**Date:** 2026-03-26  
**Status:** APPROVED (with P3 follow-up debt — see trackers)

**Report:** `.dev-workstream/reports/PLA1-BATCH-06-REPORT.md`

---

## Summary

The batch delivers the intended **Engine** and **Blazor** behavior: corrupt **`enabled-plugins.json`** is treated like **no valid config** (**`HadConfigFileAtInitialization = false`**) so **all discovered plugins stay enabled** until the user saves a good file; **`PluginManagerPanel`** lists plugins, toggles **`DiscoveredPlugin.IsEnabled`**, calls **`PluginConfigService.Save`**, and shows **Restart Required**; **File → Plugin Manager…** reuses or spawns a panel like **Topic Sources**; **`IValueFormatterRegistry`** / **`ITypeDrawerRegistry`** resolve via **`GetFeature`** from **`AddDdsMonitorServices`**; **`TopicColorService.RegisterColorRule`** + **`GetEffectiveColor`** match the Phase 6 sketch; **`IExportFormatRegistry`** + **`SamplesPanel`** split export (JSON unchanged, caret + dropdown for registered formats) are wired.

Verified locally: **`dotnet test tests/DdsMonitor.Engine.Tests/`** — **602** passed; **`dotnet test tests/DdsMonitor.Blazor.Tests/`** — **11** passed.

---

## Task-by-task verdict

| Task | Verdict |
|------|---------|
| **PLA1-DEBT-010** | **Met.** **`TryParseFile`** returns **`(empty, false)`** on corrupt/unreadable file; ctor sets **`HadConfigFileAtInitialization`** accordingly; **`PluginLoaderTests`** covers end-to-end enablement. Optional “log warning” from the debt text was **not** implemented — logged as **PLA1-DEBT-015**. |
| **PLA1-DEBT-008** | **Partially met.** Tests exercise **`SampleViewRegistry`** and a **`StubDetailTreeView`**; they do **not** render **`DetailPanel.razor`**. The stub uses **`GetViewer(Sample.Payload?.GetType())`** while production uses **`GetViewer(_currentSample.TopicMetadata.TopicType)`** — different lookup keys if topic type ≠ runtime payload type. **PLA1-DEBT-011**. |
| **PLA1-P5-T03** | **Functionally met** for the shipped panel. **PLA1-TASK-DETAIL** asked bUnit tests under **`DdsMonitor.Engine.Tests`** and “same CSS BEM classes” as **`TopicSourcesPanel`**; implementation uses a parallel **`plugin-manager__*`** block and **`TestablePluginManager`** stub — tests never mount **`PluginManagerPanel.razor`**. **`app.css`** has **no** **`.plugin-manager`** rules (browser-default table). **PLA1-DEBT-012**. |
| **PLA1-P5-T04** | **Met.** **`MainLayout`** menu + **`OpenPluginManagerPanel`** mirrors **`OpenTopicSourcesPanel`**. |
| **PLA1-P6-T01 / T02** | **Met.** **`HostWiringTests`** assert non-null **`GetFeature`** for both registries. |
| **PLA1-P6-T03** | **Met** for API and the three named tests. Success text also asks that a **null** rule is **skipped** in favor of the **next** rule — no test with **two** rules (first null, second non-null). **PLA1-DEBT-014**. |
| **PLA1-P6-T04** | **Met.** Registry + **`ExportFormatRegistryTests`**. **`GetFeature<IExportFormatRegistry>`** is not explicitly tested (acceptable given task success criteria). |
| **PLA1-P6-T05** | **Met** in UI: JSON button preserved; custom entries from **`GetFormats()`**; **`ExportFunc`** invoked with filtered samples. No automated Blazor test for dropdown behavior (deferred with stubs elsewhere). |

---

## Design alignment

- **§8 Plugin Manager:** Structure and menu placement match; **visual parity** with **`TopicSourcesPanel`** is weak without shared **`app.css`** patterns (**PLA1-DEBT-012**).
- **§9 Phase 6:** Formatter/drawer **`GetFeature`**, topic color rules, export registry + Samples export dropdown align with the design. **`TopicColorService`** is still **`AddScoped`** in **`Program.cs`** only — **§9.3** implies plugins may use **`GetFeature<TopicColorService>()`**; that remains a **pre-existing / documentation** gap, not introduced here.

---

## Tests: what actually matters

- **Strong:** **`PluginLoader`** + corrupt config integration test; **`ExportFormatRegistry`**; **`TopicColorService`** priority order; **`GetFeature`** smoke tests for formatter/drawer registries.
- **Weak / indirect:** **DEBT-008** and **P5-T03** rely on **stubs** that can diverge from production (**type key** for **`GetViewer`**, **Razor** markup and **`@onclick`** on the real checkbox). The report’s rationale (avoid referencing **`DdsMonitor.Blazor`** Web SDK + native deps) is understandable; debt items capture the residual risk until **PLA1-P8-T04** or a shared test harness lands.

---

## Suggested commit message

```
feat(dds-monitor): plugin manager, export formats, and PLA1-BATCH-06 plugin UX

Treat corrupt enabled-plugins.json like first run so discovered plugins stay enabled.
Add PluginManagerPanel with File menu spawn, IExportFormatRegistry, SamplesPanel
export split-button, TopicColorService.RegisterColorRule, and GetFeature wiring for
UI registries from AddDdsMonitorServices. Add DdsMonitor.Blazor.Tests (bUnit) for
stubbed panel and sample-view registry coverage.
```

---

## References

- **Next batch:** `.dev-workstream/batches/PLA1-BATCH-07-INSTRUCTIONS.md`
- **Task tracker:** `docs/plugin-api/PLA1-TASK-TRACKER.md`
- **Debt tracker:** `docs/plugin-api/PLA1-DEBT-TRACKER.md`
