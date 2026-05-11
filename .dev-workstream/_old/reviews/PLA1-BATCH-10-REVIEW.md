# PLA1-BATCH-10 Review

**Batch:** PLA1-BATCH-10  
**Reviewer:** Development Lead  
**Date:** 2026-03-27  
**Status:** APPROVED (with **P2 corrective**: Feature Demo → host **output** `plugins\`)

**Report:** `.dev-workstream/reports/PLA1-BATCH-10-REPORT.md`

---

## Summary

**PLA1-DEBT-020–022** are implemented as described in the report: narrowed **`PLA1-P8-T05`** scope in **`PLA1-TASK-DETAIL.md`**; **`DemoBackgroundProcessor_EnabledViaPluginConfigService_ProcessesAtLeastOneSample`** exercises the **`PluginLoader`** enablement predicate in-process plus **10× `DemoPayload`** in **`FakeSampleStore.AllSamples`**; **`RegistersAllExtensionPoints`** asserts export, **`GeoCoord`** formatter, and **`int`** drawer; **`DEBT-022`** rationale is documented on **`TopicColorService`** registration in **`ServiceCollectionExtensions.cs`**.

Reported test counts: **FeatureDemo.Tests 11**, **Engine 616**, **Blazor 16**.

---

## Lead corrective (post-review)

**Issue:** **`DdsMonitor.Plugins.FeatureDemo`** already **StagePlugin**’s into **`tools/DdsMonitor/DdsMonitor.Blazor/plugins/`** (like ECS), but **`DdsMonitor.csproj`** only copied **ECS** to **`$(OutputPath)plugins\`**. Running or debugging from **bin** could find **ECS** but not **Feature Demo**.

**Fix applied in-repo:** build-only **`ProjectReference`** to **`DdsMonitor.Plugins.FeatureDemo`** and **`CopyFeatureDemoPluginToOutput`** (mirrors **`CopyEcsPluginToOutput`**). See **`tools/DdsMonitor/DdsMonitor.Blazor/DdsMonitor.csproj`**.

---

## Task verdict

| Item | Verdict |
|------|---------|
| **PLA1-DEBT-020** | **Met** under the **documented narrow scope** — not a full **`AddDdsMonitorServices`** headless host; **`PluginConfigService`** in the test still only mutates in-memory **`EnabledPlugins`** (no disk round-trip). Acceptable with **`PLA1-TASK-DETAIL`** text. |
| **PLA1-DEBT-021** | **Met.** |
| **PLA1-DEBT-022** | **Met** for current product (immutable **`WorkspaceState`** path + singleton **`AppSettings`**). Revisit if workspace path ever becomes mutable at runtime. |

---

## Suggested commit message

```
test(plugins): close PLA1-DEBT 020-022; stage Feature Demo to host output plugins

Add PluginConfigService-path headless test with 10 DemoPayload samples; extend
FeatureDemo registration assertions; document P8-T05 CI scope and TopicColor
workspace note. Copy DdsMonitor.Plugins.FeatureDemo.dll to output plugins like ECS.
```

---

## Further PLA1 batches?

**Phases 1–8 and listed PLA1 debt rows are complete.** There is **no PLA1-BATCH-11** in the repo unless you open a **new** workstream (e.g. real **`PluginManagerPanel`** bUnit, macro demo, or doc-only follow-ups). Track discretionary work in **`TODO.md`** or a new prefix if scope grows.

---

## References

- **Task tracker:** `docs/plugin-api/PLA1-TASK-TRACKER.md`
- **Debt tracker:** `docs/plugin-api/PLA1-DEBT-TRACKER.md`
