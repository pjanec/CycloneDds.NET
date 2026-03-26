# PLA1-BATCH-04 Review

**Batch:** PLA1-BATCH-04  
**Reviewer:** Development Lead  
**Date:** 2026-03-26  
**Status:** APPROVED

**Report:** `.dev-workstream/reports/PLA1-BATCH-04-REPORT.md`

---

## Summary

All batch tasks are implemented and match **PLA1-TASK-DETAIL.md** Phase 4 and **PLA1-DESIGN.md** §7. Panels delegate menu composition to **`ContextMenuComposer.Compose`**. **`SampleViewRegistry`** resolves multiple interface matches using **`OrderBy(Type.FullName, StringComparer.Ordinal)`** with a regression test that registers **`IZzz`** before **`IAaa`** and asserts **`IAaa`** wins. **`WorkspaceSavingEvent` / `WorkspaceLoadedEvent`** live on **`DdsMonitor.Engine`**. **`WindowManager`** publishes them on save/load with an optional **`IEventBroker?`** ctor (backward compatible). **`EcsSettingsPersistenceService`** subscribes to both events, writes **`"ECS"`** into the mutable save bag, restores from loaded **`PluginSettings`**, and migrates legacy **`ecs-settings.json`** then deletes it.

Verified locally: **`dotnet test tests/DdsMonitor.Engine.Tests/`** — **585/585**; **`dotnet test tests/DdsMonitor.Plugins.ECS.Tests/`** — **48/48**.

---

## Issues Found

No blocking issues.

### Minor (documentation debt)

**File:** `tools/DdsMonitor/DdsMonitor.Plugins.ECS/EcsPlugin.cs` (lines 35–38)  
**Problem:** XML/comment still describes persistence via **`ecs-settings.json`** and **`IHostedService`** startup loading from file. Implementation now uses **workspace events**; the comment misleads future readers.  
**Tracking:** **PLA1-DEBT-009** — fix in PLA1-BATCH-05 (first corrective task).

### Report nit

Developer report / suggested commit body mentions **`bdc-settings.json`** — that looks like a typo for **`ecs-settings.json`**; codebase uses **`ecs-settings.json`** in **`EcsSettingsPersistenceService`**.

### Test quality notes

- **`WindowManagerPersistenceTests.Save_IncludesPluginDataInJson`** uses **`Assert.Contains` on JSON strings** — acceptable here because it verifies the plugin bag reaches serialized output; not a substitute for structural assertions but good smoke coverage alongside **`Save_PublishesWorkspaceSavingEvent`**.
- **`EcsSettingsPersistenceServiceTests`** exercise **`JsonElement`**-shaped values (realistic for **`System.Text.Json`**), round-trip, and empty load — **high value**.

---

## Design / DI note

**`AddScoped<IWindowManager, WindowManager>()`** resolves **`WindowManager(IEventBroker)`** with the registered **`IEventBroker`**, so production save/load raises workspace events. Tests that use **`new WindowManager()`** remain valid without a broker.

---

## Verdict

**Status:** APPROVED

---

## Commit Message

```
feat(ddsmon): workspace plugin settings API, composer in panels, ECS via events (PLA1-BATCH-04)

Completes PLA1-DEBT-006, PLA1-DEBT-007, PLA1-P4-T01, PLA1-P4-T02, PLA1-P4-T03, PLA1-P4-T04

- TopicExplorerPanel, SamplesPanel, InstancesPanel: ContextMenuComposer.Compose
- SampleViewRegistry: deterministic interface order (FullName ordinal) + test
- WorkspaceSavingEvent / WorkspaceLoadedEvent; WorkspaceDocument.PluginSettings
- WindowManager: optional IEventBroker; publish on save/load; PluginSettings in JSON
- EcsSettingsPersistenceService: event-driven persistence; legacy ecs-settings.json migrate

Tests: Engine.Tests 585; Plugins.ECS.Tests 48

Related: docs/plugin-api/PLA1-TASK-DETAIL.md Phase 4, PLA1-DESIGN.md §7
```

---

**Next batch:** `.dev-workstream/batches/PLA1-BATCH-05-INSTRUCTIONS.md`
