# PLA1-BATCH-05 Review

**Batch:** PLA1-BATCH-05  
**Reviewer:** Development Lead  
**Date:** 2026-03-26  
**Status:** APPROVED (after P1 corrective — see below)

**Report:** `.dev-workstream/reports/PLA1-BATCH-05-REPORT.md`

---

## Summary

**PLA1-DEBT-009**, **PLA1-P5-T01**, and **PLA1-P5-T02** are implemented as described: **`EcsPlugin`** documents workspace events + legacy **`ecs-settings.json`**; **`DiscoveredPlugin`** + **`PluginConfigService`** (atomic **`Save`**, tolerant **`Load`**) with three unit tests; **`PluginLoader`** builds **`DiscoveredPlugins`** for every exported plugin and calls **`ConfigureServices`** only when enabled, with **`PluginConfigService`** optional for **`Batch28`** compatibility.

**Review adjustment (P1):** With **`PluginConfigService`** always constructed in **`ServiceCollectionExtensions`**, a **missing** **`enabled-plugins.json`** produced an **empty** enabled set so **`Contains(plugin.Name)`** was false for **every** plugin — **all plugins disabled** on first install / after deleting the file (ECS never registered).  

**Corrective applied in-repo:** **`PluginConfigService.HadConfigFileAtInitialization`** (file existed at ctor load **or** set **`true`** after **`Save()`**) and **`PluginLoader`** enables all discovered plugins when that flag is **`false`**. Added **`LoadPlugins_WhenConfigFileMissing_EnablesAllDiscoveredPlugins`** and assertions on **`HadConfigFile`** in config tests. **`PluginLoader.LoadPlugins`** XML summary updated so it no longer claims **`ConfigureServices`** runs for every discovery.

Verified: **`dotnet test tests/DdsMonitor.Engine.Tests/`** — **593/593** pass.

---

## Issues Found

### ~~Issue 1 (P1): First run disabled all plugins~~ — Fixed

See Summary. **`Save()`** must flip **`HadConfigFileAtInitialization`** so a user/agent saved empty **`[]`** still means strict opt-in (existing disabled-plugin test).

### Minor

- **`PLA1-TASK-DETAIL`** names **`DiscoveredPlugin` as a `record`**; implementation is a **sealed class** with identical shape — acceptable.

- **Corrupt config file:** file exists, **`Load()`** yields empty set, **`Had`** true → every plugin disabled. Rare; optional hardening later (**PLA1-DEBT-010**).

---

## Design alignment

Matches **PLA1-DESIGN §8** intent (discover all, activate subset from persisted set) with **backward-compatible first run** (no JSON file ⇒ all enabled).

---

## Test quality

- **PluginConfigServiceTests** and **PluginLoaderTests** check real behavior (round-trip, **`ConfigureServices`** side effect, Roslyn plugin, malformed DLL skip). **Not** string-only assertions for loader enablement.
- New **missing-config** regression closes the P1 gap.

---

## Verdict

**Status:** APPROVED

---

## Commit Message

```
feat(ddsmon): plugin enablement config + two-phase PluginLoader (PLA1-BATCH-05)

Completes PLA1-DEBT-009, PLA1-P5-T01, PLA1-P5-T02

- EcsPlugin: document workspace event persistence + legacy ecs-settings migration
- DiscoveredPlugin, PluginConfigService (atomic Save, tolerant Load)
- PluginLoader: DiscoveredPlugins; ConfigureServices only for enabled plugins
- First run: no enabled-plugins.json → all discovered plugins enabled; after Save, strict list
- PluginConfigService.HadConfigFileAtInitialization + tests; PluginLoader summary XML fix

Tests: DdsMonitor.Engine.Tests 593 passed

Related: docs/plugin-api/PLA1-TASK-DETAIL.md Phase 5 (T01–T02)
```

---

**Next batch:** `.dev-workstream/batches/PLA1-BATCH-06-INSTRUCTIONS.md`
