# PLA1-BATCH-07: Phase 6b (tooltips + filter macros) + BATCH-06 test/UI debt

**Batch Number:** PLA1-BATCH-07  
**Tasks (order):** PLA1-DEBT-011, PLA1-DEBT-012, PLA1-DEBT-013, PLA1-DEBT-014, PLA1-DEBT-015, PLA1-P6-T06, PLA1-P6-T07, PLA1-P6-T08, PLA1-P6-T09  
**Phase:** PLA1 Phase 6 (complete) + closure of BATCH-06 follow-ups  
**Estimated Effort:** 22–30 hours  
**Priority:** HIGH  
**Dependencies:** PLA1-BATCH-06 (reviewed)

---

## Developer instructions

Work **technical debt first** (011→015), then **PLA1-P6-T06–T09** in order. Each P6 task’s success criteria and test tables live in **`docs/plugin-api/PLA1-TASK-DETAIL.md`**.

### Required reading

1. `.dev-workstream/guides/DEV-GUIDE.md`
2. `.dev-workstream/reviews/PLA1-BATCH-06-REVIEW.md`
3. `docs/plugin-api/PLA1-DEBT-TRACKER.md` — rows **PLA1-DEBT-011** through **015**
4. `docs/plugin-api/PLA1-DESIGN.md` — [§9 Phase 6](PLA1-DESIGN.md#9-phase-6--advanced-extension-points)
5. `docs/plugin-api/PLA1-TASK-DETAIL.md` — **P6-T06** through **P6-T09**
6. `.dev-workstream/guides/CODE-STANDARDS.md`

### Paths

| Area | Path |
|------|------|
| Tooltip registry (new) | `tools/DdsMonitor/DdsMonitor.Engine/Ui/` |
| Tooltip UI | `tools/DdsMonitor/DdsMonitor.Blazor/Components/TooltipPortal.razor` |
| Filter macros | `tools/DdsMonitor/DdsMonitor.Engine/IFilterMacroRegistry.cs` (per task detail), `FilterCompiler.cs` |
| Plugin Manager / samples toolbar CSS | `tools/DdsMonitor/DdsMonitor.Blazor/wwwroot/app.css` |
| Detail / plugin tests | `tests/DdsMonitor.Blazor.Tests/` |
| Host DI | `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs`, `tools/DdsMonitor/DdsMonitor.Blazor/Program.cs` |

### Builds / tests

```powershell
dotnet build tools/DdsMonitor/DdsMonitor.Engine/DdsMonitor.Engine.csproj
dotnet build tools/DdsMonitor/DdsMonitor.Blazor/DdsMonitor.csproj
dotnet test tests/DdsMonitor.Engine.Tests/
dotnet test tests/DdsMonitor.Blazor.Tests/
```

### Report / questions

- **Report:** `.dev-workstream/reports/PLA1-BATCH-07-REPORT.md`
- **Questions:** `.dev-workstream/questions/PLA1-BATCH-07-QUESTIONS.md`

---

## Mandatory workflow

1. **PLA1-DEBT-011** — Align **`StubDetailTreeView`** (and any related tests) with **`DetailPanel`**: **`GetViewer`** must use the same type key as production (**`TopicMetadata.TopicType`**). Supply minimal **`TopicMetadata`** in test **`SampleData`** (no `null!`). Green tests ✅  
2. **PLA1-DEBT-012** — Add **`app.css`** rules for **`.plugin-manager`** (toolbar, table, badge, empty state) consistent with **`.topic-sources`** spacing/typography; add styles for **`.samples-panel__export-group`** / caret / dropdown if the markup is visually raw. Green Blazor build ✅  
3. **PLA1-DEBT-013** — **Single registration** for **`IValueFormatterRegistry`** and **`ITypeDrawerRegistry`** (prefer **`AddDdsMonitorServices`** as source of truth; remove duplicate **`Program.cs`** registrations if safe, or document and test that **one** instance is resolved everywhere). Green Engine + Blazor tests ✅  
4. **PLA1-DEBT-014** — **`TopicColorServiceTests`**: two rules, first returns **`null`**, second returns a color; assert second wins. ✅  
5. **PLA1-DEBT-015** — On corrupt **`enabled-plugins.json`** at ctor, **log** a clear warning (existing host logging pattern). Test via logger mock/fake if the project already uses one; otherwise document in report. ✅  
6. **PLA1-P6-T06** → **T07** → **T08** → **T09** per **PLA1-TASK-DETAIL.md**. ✅  

---

## Task notes

### PLA1-P6-T06 / T07

Implement **`ITooltipProviderRegistry`** + **`TooltipProviderRegistry`**, register in **`AddDdsMonitorServices`**, inject into **`TooltipPortal.razor`** (optional injection pattern per task detail), **`MarkupString`** for HTML when provider returns content.

### PLA1-P6-T08 / T09

Implement **`IFilterMacroRegistry`** and wire **`FilterCompiler`** to resolve unknown “methods” via **`GetMacros()`**. Follow the **`DistanceTo`** example in the task detail.

### Scope control

If **DEBT-013** risks large Blazor/headless fallout, finish **011, 012, 014, 015** and **P6-T06–T07** first, then **T08–T09**, and record any split in **`PLA1-BATCH-07-QUESTIONS.md`**.

---

## Success criteria (batch)

- [ ] **PLA1-DEBT-011**–**015** marked ✅ in **`docs/plugin-api/PLA1-DEBT-TRACKER.md`**
- [ ] **PLA1-P6-T06**–**T09** meet **`PLA1-TASK-DETAIL.md`** criteria
- [ ] Engine + Blazor tests pass (commands above)
- [ ] Report filed

---

## References

- `docs/plugin-api/PLA1-TASK-TRACKER.md`
