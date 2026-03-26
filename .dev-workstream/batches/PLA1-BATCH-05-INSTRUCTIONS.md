# PLA1-BATCH-05: ECS doc cleanup + Plugin Manager foundation (Phase 5a)

**Batch Number:** PLA1-BATCH-05  
**Tasks:** PLA1-DEBT-009, PLA1-P5-T01, PLA1-P5-T02  
**Phase:** PLA1 Phase 5 — Plugin Manager UI (config + loader; panel/menu in BATCH-06)  
**Estimated Effort:** 12–16 hours  
**Priority:** HIGH  
**Dependencies:** PLA1-BATCH-04 (approved)

---

## Onboarding

### Developer instructions

Fix **stale ECS plugin documentation** left from the workspace migration, then implement **`DiscoveredPlugin`**, **`PluginConfigService`**, and **two-phase `PluginLoader`** per task detail. Panel UI and menu wiring follow in **PLA1-BATCH-06** so this batch stays reviewable.

### Required reading (in order)

1. **Workflow:** `.dev-workstream/guides/DEV-GUIDE.md`
2. **Onboarding:** `docs/plugin-api/PLA1-ONBOARDING.md`
3. **Previous review:** `.dev-workstream/reviews/PLA1-BATCH-04-REVIEW.md`
4. **Debt:** `docs/plugin-api/PLA1-DEBT-TRACKER.md` — PLA1-DEBT-009 (008 deferred)
5. **Design:** `docs/plugin-api/PLA1-DESIGN.md` — [§8 Phase 5](../../docs/plugin-api/PLA1-DESIGN.md#8-phase-5--plugin-manager-ui)
6. **Task specs:** `docs/plugin-api/PLA1-TASK-DETAIL.md` — PLA1-P5-T01, PLA1-P5-T02 (test tables authoritative)
7. **Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`

### Paths

| Work | Path |
|------|------|
| ECS plugin entry | `tools/DdsMonitor/DdsMonitor.Plugins.ECS/EcsPlugin.cs` |
| Plugin loader | `tools/DdsMonitor/DdsMonitor.Engine/Plugins/PluginLoader.cs` |
| New types | `tools/DdsMonitor/DdsMonitor.Engine/Plugins/DiscoveredPlugin.cs`, `PluginConfigService.cs` |
| Host registration | `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs` (register `PluginConfigService` / wire loader as needed) |
| Tests | `tests/DdsMonitor.Engine.Tests/Plugins/PluginConfigServiceTests.cs`, `PluginLoaderTests.cs` (or extend existing plugin tests) |

### Builds and tests

```powershell
dotnet build tools/DdsMonitor/DdsMonitor.Engine/DdsMonitor.Engine.csproj
dotnet test tests/DdsMonitor.Engine.Tests/
```

If plugin loading integration touches **`DdsMonitor.Blazor`**, build **`tools/DdsMonitor/DdsMonitor.Blazor/DdsMonitor.csproj`** as well.

### Report / questions

- **Report:** `.dev-workstream/reports/PLA1-BATCH-05-REPORT.md`
- **Questions:** `.dev-workstream/questions/PLA1-BATCH-05-QUESTIONS.md`

---

## Mandatory workflow

1. **PLA1-DEBT-009** — update **`EcsPlugin`** (and any other stale references you find) describing **`EcsSettingsPersistenceService`** → **workspace events**, legacy file **migrate-once** behavior. Mark **PLA1-DEBT-009** resolved in **`docs/plugin-api/PLA1-DEBT-TRACKER.md`**.  
2. **PLA1-P5-T01** — DTO + config service + **`PluginConfigServiceTests`** (full table). All Engine.Tests pass.  
3. **PLA1-P5-T02** — loader changes + **`PluginLoaderTests`** (full table). All Engine.Tests pass.

Do not implement **PLA1-P5-T03/T04** in this batch (next instruction file).

---

## Tasks

### Task 0 — PLA1-DEBT-009: ECS plugin documentation accuracy

Update **`EcsPlugin.ConfigureServices`** comments (and XML if present) so they state that **`EcsSettingsPersistenceService`** hooks **`WorkspaceSavingEvent` / `WorkspaceLoadedEvent`** and that **`ecs-settings.json`** is **legacy migration only**, not the primary store.

---

### Task 1 — PLA1-P5-T01: `DiscoveredPlugin` + `PluginConfigService`

Per **`PLA1-TASK-DETAIL.md`**. Atomic **`Save`**, tolerant **`Load`**.

---

### Task 2 — PLA1-P5-T02: `PluginLoader` two-phase behaviour

Per **`PLA1-TASK-DETAIL.md`**: discover all plugins, expose **`IReadOnlyList<DiscoveredPlugin> DiscoveredPlugins`**, call **`ConfigureServices` / `Initialize`** only for **enabled** plugins per **`PluginConfigService`**.

**Integration:** Ensure **`PluginLoader`** receives **`PluginConfigService`** via DI or explicit factory consistent with existing **`ServiceCollectionExtensions`** plugin bootstrap.

---

## Success criteria

- [ ] PLA1-DEBT-009 resolved in debt tracker
- [ ] PLA1-P5-T01–T02 meet **`PLA1-TASK-DETAIL.md`** success criteria and listed tests
- [ ] Application still starts with ECS and core plugins (manual smoke if automated coverage thin)
- [ ] **`dotnet test tests/DdsMonitor.Engine.Tests/`** passes
- [ ] Report submitted

---

## References

- `docs/plugin-api/PLA1-TASK-TRACKER.md`
- `.dev-workstream/reviews/PLA1-BATCH-04-REVIEW.md`
