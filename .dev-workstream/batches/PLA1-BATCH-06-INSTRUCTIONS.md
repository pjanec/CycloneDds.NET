# PLA1-BATCH-06: Plugin Manager UI + DetailPanel test + Phase 6a (export & formatters)

**Batch Number:** PLA1-BATCH-06  
**Tasks:** PLA1-DEBT-010, PLA1-DEBT-008, PLA1-P5-T03, PLA1-P5-T04, PLA1-P6-T01, PLA1-P6-T02, PLA1-P6-T03, PLA1-P6-T04, PLA1-P6-T05  
**Phase:** PLA1 Phase 5 (complete) + Phase 6 (first slice: GetFeature surfacing, topic color rules, export formats)  
**Estimated Effort:** 24–32 hours  
**Priority:** HIGH  
**Dependencies:** PLA1-BATCH-05 (approved)

---

## Onboarding

### Developer instructions

Work **debt first**, then finish the **Plugin Manager** (panel + menu), then implement **Phase 6** items through **Samples export UI**. Each block has its own tests in **`PLA1-TASK-DETAIL.md`** — do not skip tables.

### Required reading (in order)

1. `.dev-workstream/guides/DEV-GUIDE.md`
2. `docs/plugin-api/PLA1-ONBOARDING.md`
3. `.dev-workstream/reviews/PLA1-BATCH-05-REVIEW.md`
4. `docs/plugin-api/PLA1-DEBT-TRACKER.md` — PLA1-DEBT-008, PLA1-DEBT-010
5. `docs/plugin-api/PLA1-DESIGN.md` — [§8 Plugin Manager](../../docs/plugin-api/PLA1-DESIGN.md#8-phase-5--plugin-manager-ui), [§9 Advanced extension points](../../docs/plugin-api/PLA1-DESIGN.md#9-phase-6--advanced-extension-points)
6. `docs/plugin-api/PLA1-TASK-DETAIL.md` — tasks named in the header (authoritative success criteria + unit test tables)
7. `.dev-workstream/guides/CODE-STANDARDS.md`

### Paths (repo root)

| Area | Path |
|------|------|
| Plugin manager | `tools/DdsMonitor/DdsMonitor.Blazor/Components/PluginManagerPanel.razor` (NEW) |
| Menu / shell | `tools/DdsMonitor/DdsMonitor.Blazor/Components/Desktop.razor`, `Components/Layout/MainLayout.razor` |
| Style reference | `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicSourcesPanel.razor` |
| Plugins / loader / config | `tools/DdsMonitor/DdsMonitor.Engine/Plugins/PluginLoader.cs`, `PluginConfigService.cs`, `DiscoveredPlugin.cs` |
| Detail + tooltips baseline | `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor` |
| Topic color | `tools/DdsMonitor/DdsMonitor.Engine/TopicColorService.cs` |
| Export | `tools/DdsMonitor/DdsMonitor.Engine/Export/`, `SamplesPanel.razor` |
| Host DI | `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs` |
| Formatter / drawer registries | `tools/DdsMonitor/DdsMonitor.Engine/Ui/` |
| Engine tests | `tests/DdsMonitor.Engine.Tests/` |

### Builds / tests

```powershell
dotnet build tools/DdsMonitor/DdsMonitor.Engine/DdsMonitor.Engine.csproj
dotnet build tools/DdsMonitor/DdsMonitor.Blazor/DdsMonitor.csproj
dotnet test tests/DdsMonitor.Engine.Tests/
```

Add or extend a **Blazor component test project** (e.g. **bUnit**) if **`PLA1-P5-T03`** / **PLA1-DEBT-008** require it — the task detail may reference paths under `DdsMonitor.Engine.Tests`; place component tests where the solution already references **Razor** + **bUnit**, or create **`tests/DdsMonitor.Blazor.Tests/`** (or equivalent) and reference it from the solution.

### Report / questions

- **Report:** `.dev-workstream/reports/PLA1-BATCH-06-REPORT.md`
- **Questions:** `.dev-workstream/questions/PLA1-BATCH-06-QUESTIONS.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

Complete **in this order**; do not advance until the current slice builds and **all tests you have touched are green**.

1. **PLA1-DEBT-010** → corrupt / invalid `enabled-plugins.json` behavior defined + regression test → Engine.Tests pass ✅  
2. **PLA1-DEBT-008** → `DetailPanel` + `ISampleViewRegistry` automated test → pass ✅  
3. **PLA1-P5-T03** → `PluginManagerPanel` + bUnit (or agreed) tests per task table → pass ✅  
4. **PLA1-P5-T04** → menu + single-instance spawn → Blazor build + tests pass ✅  
5. **PLA1-P6-T01** → `GetFeature<IValueFormatterRegistry>()` test on built `MonitorContext` → pass ✅  
6. **PLA1-P6-T02** → `GetFeature<ITypeDrawerRegistry>()` same pattern → pass ✅  
7. **PLA1-P6-T03** → `TopicColorService` + **`TopicColorServiceTests`** (full table) → pass ✅  
8. **PLA1-P6-T04** → `IExportFormatRegistry` + implementation + DI + minimal tests per task success criteria → pass ✅  
9. **PLA1-P6-T05** → `SamplesPanel` export UI (JSON default + registry entries) → pass ✅  

---

## ✅ Tasks

### Task 0 — PLA1-DEBT-010: Corrupt `enabled-plugins.json`

**Problem:** See **`docs/plugin-api/PLA1-DEBT-TRACKER.md`**.

**Goal:** Choose explicit behavior: e.g. treat corrupt file like **missing file** (all discovered plugins enabled until user saves valid config) **and/or** log a warning via existing logging. Add a **unit test** that creates a corrupt JSON file, constructs **`PluginConfigService`**, and asserts the chosen behavior together with **`PluginLoader`** enablement.

Update **`PLA1-DEBT-TRACKER.md`** when resolved.

---

### Task 1 — PLA1-DEBT-008: `DetailPanel` custom viewer regression

Automated test that fails if **`RenderTreeView`** stops calling **`ISampleViewRegistry.GetViewer`** or does not render the custom **`RenderFragment`**. Prefer **bUnit** + minimal **`SampleData`** / **`TopicMetadata`** stubs; document any unavoidable test doubles in the report.

Mark **PLA1-DEBT-008** resolved in the debt tracker when done.

---

### Task 2 — PLA1-P5-T03: `PluginManagerPanel.razor`

**Spec:** [PLA1-TASK-DETAIL.md](../../docs/plugin-api/PLA1-TASK-DETAIL.md#pla1-p5-t03-create-pluginmanagerpanelrazor)

Table UI (Name, Version, Path, enable checkbox), **Restart Required** badge after toggle, **`PluginConfigService.Save`** on change. Style alignment with **`TopicSourcesPanel`**.

---

### Task 3 — PLA1-P5-T04: Wire Plugin Manager into application menu

**Spec:** [PLA1-TASK-DETAIL.md](../../docs/plugin-api/PLA1-TASK-DETAIL.md#pla1-p5-t04-wire-plugin-manager-into-application-menu)

**“Plugin Manager…”** entry; **`IWindowManager.SpawnPanel`** for **`PluginManagerPanel`**; reuse existing panel if already open (same pattern as **Topic Sources** / similar tools).

---

### Task 4 — PLA1-P6-T01: Expose `IValueFormatterRegistry` via `GetFeature`

**Spec:** [PLA1-TASK-DETAIL.md](../../docs/plugin-api/PLA1-TASK-DETAIL.md#pla1-p6-t01-expose-ivalueformatterregistry-via-getfeature)

Confirm singleton registration; add test that **`MonitorContext`** built from the same **`ServiceCollection`** shape as the host resolves **`GetFeature<IValueFormatterRegistry>()`** non-null.

---

### Task 5 — PLA1-P6-T02: Expose `ITypeDrawerRegistry` via `GetFeature`

**Spec:** [PLA1-TASK-DETAIL.md](../../docs/plugin-api/PLA1-TASK-DETAIL.md#pla1-p6-t02-expose-itypedrawerregistry-via-getfeature)

Same integration pattern as **P6-T01**.

---

### Task 6 — PLA1-P6-T03: `RegisterColorRule` on `TopicColorService`

**Spec:** [PLA1-TASK-DETAIL.md](../../docs/plugin-api/PLA1-TASK-DETAIL.md#pla1-p6-t03-add-registercolorrule-to-topiccolorservice)

Implement **`TopicColorServiceTests`** per the task table (override, null fallback, user override precedence).

---

### Task 7 — PLA1-P6-T04: `IExportFormatRegistry` + `ExportFormatRegistry`

**Spec:** [PLA1-TASK-DETAIL.md](../../docs/plugin-api/PLA1-TASK-DETAIL.md#pla1-p6-t04-create-iexportformatregistry)

New types under **`DdsMonitor.Engine/Export/`** (or as specified). Register singleton in **`ServiceCollectionExtensions`** (and expose via **`GetFeature`** if design §9 implies plugins register formats — at minimum host DI must resolve the registry for **P6-T05**).

---

### Task 8 — PLA1-P6-T05: Samples Panel export dropdown

**Spec:** [PLA1-TASK-DETAIL.md](../../docs/plugin-api/PLA1-TASK-DETAIL.md#pla1-p6-t05-expose-export-formats-in-samples-panel-export-button)

Default **JSON** export unchanged; append formats from **`IExportFormatRegistry.GetFormats()`**; invoke selected **`ExportFunc`** with current filtered samples.

---

## 🎯 Success criteria (batch)

- [ ] PLA1-DEBT-008 and PLA1-DEBT-010 marked ✅ in **`docs/plugin-api/PLA1-DEBT-TRACKER.md`**
- [ ] PLA1-P5-T03, PLA1-P5-T04, PLA1-P6-T01–T05 meet **`PLA1-TASK-DETAIL.md`** success criteria and listed tests (where applicable)
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/` (and any new Blazor test project) pass
- [ ] `dotnet build` for Engine + Blazor succeeds
- [ ] Report: `.dev-workstream/reports/PLA1-BATCH-06-REPORT.md`

---

## ⚠️ Scope notes

- **Phase 6** continues in **PLA1-BATCH-07** with **PLA1-P6-T06**–**T09** (tooltip registry, filter macros) unless you complete early and the lead extends the batch.
- If **bUnit** project setup dominates time, finish **DEBT-010**, **P5**, **P6-T01–T03** first, then **T04–T05** — but prefer **not** splitting without a written note in **`PLA1-BATCH-06-QUESTIONS.md`**.

---

## References

- `docs/plugin-api/PLA1-TASK-TRACKER.md`
- `.dev-workstream/reviews/PLA1-BATCH-05-REVIEW.md`
