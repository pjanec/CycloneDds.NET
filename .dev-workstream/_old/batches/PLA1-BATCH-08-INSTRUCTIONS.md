# PLA1-BATCH-08: Tooltip + config logging debt, Kitchen Sink demo plugin (Phase 7)

**Batch Number:** PLA1-BATCH-08  
**Tasks (order):** PLA1-DEBT-016, PLA1-DEBT-017, PLA1-P7-T01, PLA1-P7-T02, PLA1-P7-T03  
**Phase:** PLA1 Phase 7 (Feature Demo) + close BATCH-07 gaps  
**Estimated Effort:** 20–28 hours  
**Priority:** HIGH  
**Dependencies:** PLA1-BATCH-07 (reviewed)

---

## Developer instructions

Complete **PLA1-DEBT-016** and **PLA1-DEBT-017** before Phase 7 work so tooltip and config extensions behave in **production**, not only in tests.

Specs and success criteria: **`docs/plugin-api/PLA1-TASK-DETAIL.md`** (P7-T01–T03) and **`.dev-workstream/reviews/PLA1-BATCH-07-REVIEW.md`**.

### Required reading

1. `.dev-workstream/guides/DEV-GUIDE.md`
2. `.dev-workstream/reviews/PLA1-BATCH-07-REVIEW.md`
3. `docs/plugin-api/PLA1-DEBT-TRACKER.md` — **PLA1-DEBT-016**, **017**
4. `docs/plugin-api/PLA1-DESIGN.md` — [§10 Phase 7](PLA1-DESIGN.md#10-phase-7--kitchen-sink-demo-plugin)
5. `docs/plugin-api/PLA1-TASK-DETAIL.md` — **P7-T01** through **P7-T03**
6. `.dev-workstream/guides/CODE-STANDARDS.md`

### Paths

| Area | Path |
|------|------|
| Plugin config | `tools/DdsMonitor/DdsMonitor.Engine/Plugins/PluginConfigService.cs`, `Hosting/ServiceCollectionExtensions.cs` |
| Tooltips | `tools/DdsMonitor/DdsMonitor.Blazor/Services/TooltipService.cs`, `Components/TooltipPortal.razor`, panels that call **`Show`** |
| Demo plugin | `tools/DdsMonitor/DdsMonitor.Plugins.FeatureDemo/` (NEW per task detail) |
| Tests | `tests/DdsMonitor.Blazor.Tests/`, solution / CI layout for demo DLL |

### Builds / tests

```powershell
dotnet build tools/DdsMonitor/DdsMonitor.Engine/DdsMonitor.Engine.csproj
dotnet build tools/DdsMonitor/DdsMonitor.Blazor/DdsMonitor.csproj
dotnet test tests/DdsMonitor.Engine.Tests/
dotnet test tests/DdsMonitor.Blazor.Tests/
```

After the demo project exists, include it in **`dotnet build`** / solution validation commands you use in CI.

### Report / questions

- **Report:** `.dev-workstream/reports/PLA1-BATCH-08-REPORT.md`
- **Questions:** `.dev-workstream/questions/PLA1-BATCH-08-QUESTIONS.md`

---

## Mandatory workflow

1. **PLA1-DEBT-016** — **`PluginConfigService`** constructed in **`ServiceCollectionExtensions`** must receive a **non-null** **`ILogger<PluginConfigService>`** (or equivalent) so corrupt JSON logs a **Warning** in real runs. Prefer minimal **`Microsoft.Extensions.Logging`** usage already referenced by Engine. Do **not** break **`Batch28`/headless** test hosts. ✅  
2. **PLA1-DEBT-017** — At least **one** user-facing tooltip path (e.g. **`DetailPanel`**, **`SamplesPanel`**, or **`InstancesPanel`**) must call **`TooltipService.Show`** with **`ContextType`** and **`ContextValue`** reflecting the value being inspected. Add **bUnit** coverage so **`TooltipPortal`** renders **registry** HTML when a test provider returns markup (and still shows default JSON when providers return **null**). Consider **`<pre>`** vs free HTML when returning **`MarkupString`**. ✅  
3. **PLA1-P7-T01** — Demo plugin **csproj** + solution integration. ✅  
4. **PLA1-P7-T02** — **`FeatureDemoPlugin`** registers against **§10.2** extension points via **`GetFeature`** (null-safe). ✅  
5. **PLA1-P7-T03** — **`DemoDashboardPanel.razor`** + panel registration. ✅  

---

## Success criteria (batch)

- [ ] **PLA1-DEBT-016** and **017** marked ✅ in **`docs/plugin-api/PLA1-DEBT-TRACKER.md`**
- [ ] **PLA1-P7-T01–T03** meet **`PLA1-TASK-DETAIL.md`**
- [ ] Engine + Blazor + demo project build; tests you touch stay green
- [ ] Report filed

---

## Scope notes

- If **DEBT-016** reorders **`PluginLoader`** / **`PluginConfigService`** construction, regression-test **PLA1** plugin enablement and **DEBT-010** corrupt-file behavior.
- **Phase 8** (standalone **`FeatureDemo.Tests`**, **PluginManagerPanel** bUnit against real Razor) is a natural **PLA1-BATCH-09** after Phase 7 lands.

---

## References

- `docs/plugin-api/PLA1-TASK-TRACKER.md`
