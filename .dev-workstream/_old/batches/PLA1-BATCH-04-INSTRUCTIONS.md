# PLA1-BATCH-04: Composer adoption + workspace plugin settings (Phase 4)

**Batch Number:** PLA1-BATCH-04  
**Tasks:** PLA1-DEBT-006, PLA1-DEBT-007 (corrective), PLA1-P4-T01, PLA1-P4-T02, PLA1-P4-T03, PLA1-P4-T04  
**Phase:** PLA1 Phase 4 — Workspace settings integration (plus small debt)  
**Estimated Effort:** 16–22 hours  
**Priority:** HIGH  
**Dependencies:** PLA1-BATCH-03 (approved)

---

## 📋 Onboarding & Workflow

### Developer instructions

Apply **`ContextMenuComposer`** in the three panels and tighten **`SampleViewRegistry`** interface matching docs, then implement **workspace plugin settings** end-to-end (events, document shape, `WindowManager`, ECS migration off `ecs-settings.json`).

### Required reading (in order)

1. **Workflow:** `.dev-workstream/guides/DEV-GUIDE.md`
2. **Onboarding:** `docs/plugin-api/PLA1-ONBOARDING.md`
3. **Previous review:** `.dev-workstream/reviews/PLA1-BATCH-03-REVIEW.md`
4. **Debt:** `docs/plugin-api/PLA1-DEBT-TRACKER.md` — PLA1-DEBT-006, PLA1-DEBT-007 (008 out of scope unless trivial)
5. **Design:** `docs/plugin-api/PLA1-DESIGN.md` — [§7 Phase 4](../../docs/plugin-api/PLA1-DESIGN.md#7-phase-4--workspace-settings-integration)
6. **Task specs:** `docs/plugin-api/PLA1-TASK-DETAIL.md` — Phase 4 tasks (tables are authoritative)
7. **Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`

### Key paths

| Work | Path |
|------|------|
| Panels (composer) | `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicExplorerPanel.razor`, `SamplesPanel.razor`, `InstancesPanel.razor` |
| Composer | `tools/DdsMonitor/DdsMonitor.Engine/Plugins/ContextMenuComposer.cs` |
| Sample view registry | `tools/DdsMonitor/DdsMonitor.Engine/Plugins/SampleViewRegistry.cs` |
| Events | `tools/DdsMonitor/DdsMonitor.Engine/EventBrokerEvents.cs` |
| Workspace model | `tools/DdsMonitor/DdsMonitor.Engine/WorkspaceDocument.cs` |
| Persistence orchestration | `tools/DdsMonitor/DdsMonitor.Engine/WindowManager.cs` |
| ECS plugin | `tools/DdsMonitor/DdsMonitor.Plugins.ECS/` (`EcsPlugin.cs`, `EcsSettingsPersistenceService.cs`, etc.) |

### Builds and tests

```powershell
dotnet build tools/DdsMonitor/DdsMonitor.Engine/DdsMonitor.Engine.csproj
dotnet build tools/DdsMonitor/DdsMonitor.Blazor/DdsMonitor.csproj
dotnet build tools/DdsMonitor/DdsMonitor.Plugins.ECS/DdsMonitor.Plugins.ECS.csproj
dotnet test tests/DdsMonitor.Engine.Tests/
dotnet test tests/DdsMonitor.Plugins.ECS.Tests/
```

### Report / questions

- **Report:** `.dev-workstream/reports/PLA1-BATCH-04-REPORT.md`
- **Questions:** `.dev-workstream/questions/PLA1-BATCH-04-QUESTIONS.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

1. **PLA1-DEBT-006** → panels use `ContextMenuComposer` → full solution builds; Engine.Tests pass ✅  
2. **PLA1-DEBT-007** → deterministic / documented interface resolution → Engine.Tests pass ✅  
3. **PLA1-P4-T01** → records compile ✅  
4. **PLA1-P4-T02** → `WorkspaceDocument` + **WorkspaceDocumentTests** per task table ✅  
5. **PLA1-P4-T03** → `WindowManager` + **WindowManagerPersistenceTests** per task table ✅  
6. **PLA1-P4-T04** → ECS migration; **ECS.Tests** pass ✅  

Update **`docs/plugin-api/PLA1-DEBT-TRACKER.md`** when 006 and 007 are resolved.

---

## ✅ Tasks

### Task 0 — PLA1-DEBT-006: Use `ContextMenuComposer` in panels

Replace inline “defaults + optional separator + `GetItems`” blocks with:

`ContextMenuComposer.Compose(defaultItemsEnumerable, ContextMenuRegistry, context)`

Keep labels and callbacks identical to current behavior. Run Blazor build; smoke right-click if possible.

---

### Task 1 — PLA1-DEBT-007: `SampleViewRegistry` interface matching

Implement one of: **(a)** XML doc stating that when several registered interfaces could match, resolution follows a **deterministic** rule (recommend: sort candidate interfaces by `FullName` and pick first hit), or **(b)** change loop to use sorted order. Add a unit test if two interfaces apply to one type—expected winner must match the documented rule.

---

### Task 2 — PLA1-P4-T01: `WorkspaceSavingEvent` / `WorkspaceLoadedEvent`

Per `PLA1-TASK-DETAIL.md` — `EventBrokerEvents.cs`.

---

### Task 3 — PLA1-P4-T02: `WorkspaceDocument.PluginSettings`

Per task detail + **`WorkspaceDocumentTests`** table.

---

### Task 4 — PLA1-P4-T03: `WindowManager` save/load integration

Per task detail + **`WindowManagerPersistenceTests`** table.

---

### Task 5 — PLA1-P4-T04: ECS workspace migration

Per task detail: event subscriptions, `workspace.json` key `"ECS"`, legacy `ecs-settings.json` one-time migration and delete.

---

## 🎯 Success criteria

- [ ] PLA1-DEBT-006, PLA1-DEBT-007 resolved in debt tracker
- [ ] PLA1-P4-T01–T04 meet `PLA1-TASK-DETAIL.md` success criteria and test tables
- [ ] Engine + ECS test projects green
- [ ] Report submitted

---

## 📚 References

- `docs/plugin-api/PLA1-TASK-TRACKER.md`
- `.dev-workstream/reviews/PLA1-BATCH-03-REVIEW.md`
