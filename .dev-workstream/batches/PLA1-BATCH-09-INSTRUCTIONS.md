# PLA1-BATCH-09: Phase 8 CI tests + §10.2 / tooltip debt

**Batch Number:** PLA1-BATCH-09  
**Tasks (order):** PLA1-DEBT-018, PLA1-DEBT-019, PLA1-P8-T01, PLA1-P8-T02, PLA1-P8-T03, PLA1-P8-T04, PLA1-P8-T05  
**Phase:** PLA1 Phase 8 (Autonomous CI Testing) + close BATCH-08 gaps  
**Estimated Effort:** 22–30 hours  
**Priority:** HIGH  
**Dependencies:** PLA1-BATCH-08 (reviewed)

---

## Developer instructions

Complete **PLA1-DEBT-018** (kitchen-sink completeness) and **PLA1-DEBT-019** (tooltip parity) **before** Phase 8 test work where possible, so **`FeatureDemoPluginTests`** and design **§10.2** stay aligned.

### Required reading

1. `.dev-workstream/guides/DEV-GUIDE.md`
2. `.dev-workstream/reviews/PLA1-BATCH-08-REVIEW.md`
3. `docs/plugin-api/PLA1-DEBT-TRACKER.md` — **PLA1-DEBT-018**, **019**
4. `docs/plugin-api/PLA1-DESIGN.md` — [§10.2](PLA1-DESIGN.md#102-scope), [§11 Phase 8](PLA1-DESIGN.md#11-phase-8--autonomous-ci-testing)
5. `docs/plugin-api/PLA1-TASK-DETAIL.md` — **P8-T01** through **P8-T05**
6. `.dev-workstream/guides/CODE-STANDARDS.md`

### Paths

| Area | Path |
|------|------|
| Demo plugin | `tools/DdsMonitor/DdsMonitor.Plugins.FeatureDemo/FeatureDemoPlugin.cs` |
| Topic color | `tools/DdsMonitor/DdsMonitor.Engine/TopicColorService.cs`, `Hosting/ServiceCollectionExtensions.cs`, `Program.cs` |
| Detail tooltips | `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor` |
| New test project | `tests/DdsMonitor.Plugins.FeatureDemo.Tests/` (per task detail) |
| Headless integration | `tests/DdsMonitor.Engine.Tests/Plugins/` |
| Plugin Manager tests | **`PLA1-TASK-DETAIL`** names `tests/DdsMonitor.Engine.Tests/Components/PluginManagerPanelTests.cs`; repo already has **`tests/DdsMonitor.Blazor.Tests/Components/PluginManagerPanelTests.cs`**. Prefer **one** canonical location: migrate real **`PluginManagerPanel.razor`** tests if references allow, or add the Engine path only if you avoid duplicating stubs — document the choice in **`PLA1-BATCH-09-REPORT.md`**. |

### Builds / tests

```powershell
dotnet build tools/DdsMonitor/DdsMonitor.Engine/DdsMonitor.Engine.csproj
dotnet build tools/DdsMonitor/DdsMonitor.Blazor/DdsMonitor.csproj
dotnet build tools/DdsMonitor/DdsMonitor.Plugins.FeatureDemo/DdsMonitor.Plugins.FeatureDemo.csproj
dotnet test tests/DdsMonitor.Engine.Tests/
dotnet test tests/DdsMonitor.Blazor.Tests/
dotnet test tests/DdsMonitor.Plugins.FeatureDemo.Tests/
```

Add the new test project to **`CycloneDDS.NET.sln`** if not already present.

### Report / questions

- **Report:** `.dev-workstream/reports/PLA1-BATCH-09-REPORT.md`
- **Questions:** `.dev-workstream/questions/PLA1-BATCH-09-QUESTIONS.md`

---

## Mandatory workflow

1. **PLA1-DEBT-018** — **`FeatureDemoPlugin`** registers **§10.2** **`TopicColorService`** behavior (e.g. red for topics containing **`DEMO`**). Likely requires **`TopicColorService`** lifetime / **`GetFeature`** alignment (**singleton** from root scope, or documented scoped resolution). Update **`PLA1-DESIGN.md` §10.2** only if the design intent changes. ✅  
2. **PLA1-DEBT-019** — **`DetailPanel`** tooltips that show payload JSON pass **`ContextType`** / **`ContextValue`** like **SamplesPanel**/**InstancesPanel**. ✅  
3. **PLA1-P8-T01** — xUnit + **bUnit** (+ Moq if per spec) test project. ✅  
4. **PLA1-P8-T02** — **`FeatureDemoPluginTests`** per task table (including workspace events; after **DEBT-018**, assert color rule or documented key). ✅  
5. **PLA1-P8-T03** — **`DemoDashboardPanel`** bUnit tests. ✅  
6. **PLA1-P8-T04** — **`PluginManagerPanel`** bUnit tests (**real** component preferred over stub if **`DdsMonitor.Blazor`** reference is acceptable in target project). ✅  
7. **PLA1-P8-T05** — Headless DI integration test (**&lt; 5 s**, **`ProcessedCount >= 1`**). ✅  

---

## Success criteria (batch)

- [ ] **PLA1-DEBT-018** and **019** marked ✅ in **`docs/plugin-api/PLA1-DEBT-TRACKER.md`**
- [ ] **PLA1-P8-T01–T05** meet **`PLA1-TASK-DETAIL.md`**
- [ ] CI-relevant projects build and listed tests pass
- [ ] Report filed

---

## References

- `docs/plugin-api/PLA1-TASK-TRACKER.md`
