# MON-BATCH-30 Report

**Batch:** MON-BATCH-30  
**Date:** 2026-03-22  
**Tasks:** Corrective Task 0 (MON-DEBT-024), DMON-048, DMON-049  
**Status:** ✅ All tasks complete. 41 BDC tests passing (26 existing + 15 new). 503 Engine tests passing.

---

## ✅ Task 0 — BdcSettings Persistence (MON-DEBT-024)

### What was done

Created `BdcSettingsPersistenceService` — a singleton `IHostedService` registered in `BdcPlugin.ConfigureServices`. It:

- **Loads** settings from `bdc-settings.json` in `StartAsync`, **before** the UI renders.
- **Saves** settings with a 2-second debounce whenever `BdcSettings.SettingsChanged` fires.
- Uses the same workspace folder as `workspace.json` via `IWorkspaceState.WorkspaceFilePath`.

### Persistence Handling

Threading `BdcSettings` into the existing storage model required navigating the captive-dependency constraint: `IWorkspaceState` is a Singleton, and the `WorkspacePersistenceService` is Scoped (per-tab). Rather than integrating with `IWindowManager.SaveWorkspace` (which serializes panel layout, not plugin configuration), the plugin owns its own sidecar file `bdc-settings.json`.

The plugin registers both as a named singleton **and** an `IHostedService`:

```csharp
services.AddSingleton<BdcSettingsPersistenceService>();
services.AddHostedService(sp => sp.GetRequiredService<BdcSettingsPersistenceService>());
```

This ensures a single instance is used by both the DI resolution path and the `IHostedService` startup path. The settings file is written with a `DebouncedAction` (2 s delay) so rapid user edits collapse into a single write, matching the pattern already established by `WorkspacePersistenceService`.

---

## ✅ Task 1 — Entity Detail Inspector (DMON-048)

### What was done

1. **`BdcEntityGridPanel.razor`** — Added `@inject IWindowManager WindowManager`. The previously-stubbed `OpenDetail(Entity entity)` now calls:
   ```csharp
   WindowManager.SpawnPanel("BDC Entity Detail",
       new Dictionary<string, object>(StringComparer.Ordinal) { ["EntityId"] = entity.EntityId });
   ```

2. **`Components/EntityDetailPanel.razor`** — New panel registered as `"BDC Entity Detail"` in `BdcPlugin.Initialize`. Key characteristics:
   - Accepts `[Parameter] public int EntityId { get; set; }` wired by the Desktop's dynamic component renderer from `PanelState.ComponentState`.
   - Also reads `EntityId` from `PanelState.ComponentState` via `OnParametersSet` for post-workspace-reload robustness (JSON deserializes numbers as `long`).
   - Injects `EntityStore` and subscribes to `Store.Changed` for live updates.
   - Renders all descriptor cards (topic name, optional PartId badge, field table via `field.Getter(sample.Payload)` for every non-synthetic field).
   - Includes a collapsible journal table showing all state transitions.

3. **`BdcPlugin.cs`** — `EntityDetailPanel`, `BdcSettingsPersistenceService` (as `IHostedService`), and `TimeTravelEngine` all registered.

---

## ✅ Task 2 — Historical State Time-Travel Engine (DMON-049)

### New files

| File | Purpose |
|------|---------|
| `TimeTravelEngine.cs` | Core history-reconstruction service |
| `EntityHistoricalState.cs` | Result record type |
| `tests/StubSampleStore.cs` | Test double for `ISampleStore` |
| `tests/TimeTravelTests.cs` | 15 unit tests |

### Algorithm Design

**Time Travel Extrapolation**

The engine cross-references `EntityStore`'s entity data with `ISampleStore`'s chronological ledger without blowing up memory:

1. **Single pass to collect topic types.** Iterate `ISampleStore.AllSamples` once and collect a `Dictionary<Type, TopicMetadata>`. This is O(n) in total sample count but stores only O(k) topic metadata objects where k is the number of distinct topic types (typically very small).

2. **Per-type binary search + backwards scan.** For each BDC candidate type, call `ISampleStore.GetTopicSamples(type).Samples` (an already-available chronological list) and perform two sub-steps:
   - **Binary search** O(log m) for the rightmost sample index where `Timestamp ≤ targetTime`.
   - **Backward linear scan** from that index to collect the **latest** sample per unique `PartId` for the target `EntityId`. The scan stops naturally when all PartIds have been found.

3. **Disposal detection.** Any found sample whose `SampleInfo.InstanceState` is `NotAliveDisposed` or `NotAliveNoWriters` is excluded from the result. This mirrors `InstanceStore.MapInstanceState`'s catch-all default (InstanceState = 0 is treated as alive).

4. **Memory safety.** The engine does not copy or hold references to all samples — it holds only the result set (one `SampleData` per descriptor per entity). The intermediate `seenMeta` dictionary is minimal.

**Why `Dictionary<long, SampleData>` internally (not `Dictionary<long?, SampleData>`)**

.NET 8's `Dictionary<TKey, TValue>` throws `ArgumentNullException` when `TKey` is `Nullable<T>` and the key is null (the `FindValue` null-check fires for nullable structs). The fix: use `long.MinValue` as an internal sentinel for "no PartId", then convert back to `null` on output as `List<(long? PartId, SampleData Sample)>`.

### Tests Written

| Test | What it validates |
|------|----------------- |
| `TimeTravel_FindsCorrectDescriptorsAtTimestamp` | Binary search returns T2 version when queried between T2 and T3 |
| `TimeTravel_ExcludesDisposedDescriptors` | Disposal sample at T=1.5 causes descriptor to be absent at T=2 |
| `TimeTravel_EntityDeadAtTimestamp_ReturnsEmpty` | Entity whose only Master was disposed returns empty Descriptors |
| `TimeTravel_MultiInstance_FindsAllPartIds` | Both PartId=1 and PartId=2 appear independently in the result |
| `TimeTravel_BoundaryBetweenEvent3And4_ReturnsEvent3Payload` | 5 states, 100 ms apart — query at +50 ms after event #3 returns "state-3" |
| `BinarySearch_EmptySamples_ReturnsEmpty` | No crash on empty input |
| `BinarySearch_AllSamplesAfterTarget_ReturnsEmpty` | All samples after targetTime returns empty |
| `BinarySearch_IgnoresDifferentEntityIds` | Samples for entityId=99 are not returned when querying entityId=1 |
| `IsAliveSample_DefaultInstanceState_ReturnsTrue` | Default (0) → alive |
| `IsAliveSample_DdsAlive_ReturnsTrue` | DdsInstanceState.Alive → alive |
| `IsAliveSample_NotAliveDisposed_ReturnsFalse` | NotAliveDisposed → not alive |
| `IsAliveSample_NotAliveNoWriters_ReturnsFalse` | NotAliveNoWriters → not alive |
| `TimeTravel_TopicOutsideNamespacePrefix_IsIgnored` | Namespace filter respected |

---

## 📊 Test Summary

| Project | Tests | Result |
|---------|-------|--------|
| `DdsMonitor.Plugins.Bdc.Tests` | 41 (26 existing + 15 new) | ✅ All Pass |
| `DdsMonitor.Engine.Tests` | 503 | ✅ All Pass |
| All other `CycloneDDS.NET.Core.slnf` projects | 403 | ✅ All Pass |

---

## 🎯 Success Criteria

- [x] Task 0 (Debt) eliminated — `BdcSettings` persists to `bdc-settings.json` across reboots.
- [x] Clicking "Detail" in the BDC Entity Grid spawns a rich `EntityDetailPanel` via `IWindowManager.SpawnPanel`.
- [x] `EntityDetailPanel` reflects live `EntityStore` updates in real time.
- [x] "View History At…" datetime picker reconstructs frozen historical entity state via binary-search time-travel.
- [x] All 4 DMON-049 success-condition unit tests implemented and passing.
- [x] Boundary test (5 states, query between #3 and #4) returns the correct payload.
