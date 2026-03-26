# PLA1-BATCH-07 Review

**Batch:** PLA1-BATCH-07  
**Reviewer:** Development Lead  
**Date:** 2026-03-27  
**Status:** APPROVED (with P3/P4 follow-up debt)

**Report:** `.dev-workstream/reports/PLA1-BATCH-07-REPORT.md`

---

## Summary

**PLA1-DEBT-011–014** are addressed in code as described: **`StubDetailTreeView`** now keys **`GetViewer`** with **`TopicMetadata.TopicType`**, **`BlazorTestTypes`** supply **`TopicMetadata`**, **`app.css`** gains **`.plugin-manager*`** and **`.samples-panel__export-*`** rules, **`Program.cs`** no longer double-registers formatter/drawer registries, and **`TopicColorServiceTests`** includes a two-rule **(null, then color)** case.

**PLA1-P6-T06–T09** are implemented: tooltip provider registry + **`TooltipPortal`** branch, filter macro registry + **`FilterCompiler`** macro expansion via **`MacroShim`** and tests including **`Squared(Payload.Id)`** plus unknown-method compile failure.

Verified: **`dotnet test tests/DdsMonitor.Engine.Tests/`** — **616/616** pass (report claims +14 vs prior batch).

---

## Task-by-task verdict

| Item | Verdict |
|------|---------|
| **PLA1-DEBT-011** | **Met.** Stub and tests align with **`DetailPanel`**’s **`TopicMetadata.TopicType`** lookup; **`MakeSample`** builds real **`TopicMetadata`**. |
| **PLA1-DEBT-012** | **Met.** **`app.css`** contains the promised blocks (verified via source search). |
| **PLA1-DEBT-013** | **Met.** UI-mode **`Program.cs`** no longer adds competing singletons; Engine registration remains the single source. |
| **PLA1-DEBT-014** | **Met.** Two-rule test present. |
| **PLA1-DEBT-015** | **Partially met.** Warning is implemented and covered with **`FakeLogger`** on the internal ctor, but the **production** path **`new PluginConfigService()`** still passes **`null`** for **`ILogger`**, so operators see **no** log line when the file is corrupt. **PLA1-DEBT-016** captures the remainder. |
| **PLA1-P6-T06** | **Met.** Interface, thread-safe registry, unit + **`GetFeature`** test. |
| **PLA1-P6-T07** | **Infrastructure met; product path incomplete.** **`TooltipPortal`** consults **`ITooltipProviderRegistry`** when **`ContextType`** is set, but **all** current **`TooltipService.Show`** call sites still use **`new TooltipState(html, x, y)`** only — **no** **`ContextType` / `ContextValue`**. Registry-backed tooltips are **inactive** until callers pass context (or Phase 7 demo wires one). **`PLA1-DEBT-017`**. Success criteria (1)(2) are not exercised by an integration/component test. |
| **PLA1-P6-T08** | **Met.** Registry + tests + **`GetFeature`**. |
| **PLA1-P6-T09** | **Met** for behavior described in tests. Implementation uses **source rewriting** to **`MacroShim.Invoke`** rather than binding “unknown methods” inside Dynamic LINQ; that is acceptable and covered by **`FilterCompiler_WithRegisteredMacro_ExecutesCorrectly`** / **`FilterCompiler_WithUnknownMethodName_ReturnsError`**. |

---

## Design alignment

- **§9** tooltip + macro stories are **structurally** present.
- **Gap:** Tooltip HTML is still wrapped in **`<pre class="tooltip-content">`**; rich HTML from providers may need a **non-`<pre>`** branch (optional polish, can ride with **DEBT-017**).

---

## Tests: what matters

- **Strong:** macro compile + predicate evaluation; macro registry; tooltip registry unit tests; plugin config corrupt + **`Had`** semantics unchanged; stub/type-key alignment tests.
- **Weak:** **DEBT-015** production logging; **P6-T07** end-to-end (no call site, no **`TooltipPortal`** bUnit test proving **`MarkupString`** override).

---

## Suggested commit message

```
feat(dds-monitor): Phase 6 tooltips + filter macros (PLA1-BATCH-07)

Add ITooltipProviderRegistry, IFilterMacroRegistry, FilterCompiler macro
expansion, TooltipPortal registry hook, and PluginConfigService warning hook.
Align DetailPanel stub tests with TopicMetadata.TopicType; add plugin-manager
and export UI styles; dedupe formatter/drawer DI registration.
```

---

## References

- **Next batch:** `.dev-workstream/batches/PLA1-BATCH-08-INSTRUCTIONS.md`
- **Debt:** `docs/plugin-api/PLA1-DEBT-TRACKER.md` — **PLA1-DEBT-016**, **017** added post-review
