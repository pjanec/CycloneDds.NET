# MON-BATCH-26-REPORT

**Batch:** MON-BATCH-26  
**Tasks:** Replay Slider Interactions, Next Sample Preview, InstanceState Serialization, SampleState Indicators, Toolbar Consolidation  
**Status:** ✅ COMPLETE  
**Build Result:** 0 errors, 9 pre-existing warnings (all unrelated to batch-26 changes)

---

## Deliverables

| Task | File(s) | Status |
|---|---|---|
| Task 1 – Target hardcoded to DDS Network | `Components/ReplayPanel.razor` | ✅ |
| Task 1 – Scrubber `oninput` real-time | `Components/ReplayPanel.razor` | ✅ |
| Task 1 – Scrubber locked during playback | `Components/ReplayPanel.razor` | ✅ |
| Task 2 – Next Sample preview section | `Components/ReplayPanel.razor`, `IReplayEngine.cs`, `ReplayEngine.cs` | ✅ |
| Task 3 – InstanceState export | `Export/ExportService.cs`, `Import/SampleExportRecord.cs` | ✅ |
| Task 3 – InstanceState import (backward compat) | `Import/ImportService.cs` | ✅ |
| Task 4 – SamplesPanel tooltip text | `Components/SamplesPanel.razor` | ✅ |
| Task 4 – DetailPanel state badge | `Components/DetailPanel.razor` | ✅ |
| Task 5 – ReplayPanel icon toolbar | `Components/ReplayPanel.razor`, `wwwroot/app.css` | ✅ |
| Task 5 – Muted SVG icon tints | `wwwroot/app.css` | ✅ |

---

## Task 1 – Replay UX Interactivity

### Target Removal
`_target` field (`ReplayTarget.LocalStore`) and `OnTargetChanged` method were removed.  `Play()` and `Step()` now hardcode `ReplayTarget.DdsNetwork`.  The Target `<select>` row was excised from the Settings grid, leaving only Speed and Loop.

### Real-Time Scrubber (`oninput`)
The range slider was changed from `@onchange` to `@oninput`.  Blazor wires `@oninput` to the DOM `input` event (fired on every pointer move) vs `@onchange` which fires only on `change` (finger/mouse release).  The handler `OnScrubberChanged` directly calls `SeekToFrame` or `SeekToTime` which update `_currentIndex` and fire `StateChanged`, causing an immediate `InvokeAsync(StateHasChanged)` in the panel — so `CurrentTimestamp` and `CurrentRelativeTime` in the metrics grid refresh on every drag tick without any polling or debounce.

### Scrubber Lock During Playback
Added `ReplayEngine.Status == ReplayStatus.Playing` to the `disabled` expression.  While `Playing`, the slider ignores pointer events entirely; the playback loop advances `_currentIndex` internally and raises `StateChanged`, which re-renders the scrubber position through the one-way `value="@GetScrubberValue()"` binding.

---

## Task 2 – Next Sample Preview

`SampleData? NextSample { get; }` was added to `IReplayEngine` and implemented in `ReplayEngine`:

```csharp
public SampleData? NextSample
{
    get
    {
        var idx = Volatile.Read(ref _currentIndex);
        return idx < _filteredSamples.Count ? _filteredSamples[idx] : null;
    }
}
```

`ReplayPanel.razor` renders a **"Next Sample"** section when `Status != Playing && NextSample != null`:
- Shows `#Ordinal` and `ShortName` (topic type, with `FullName` as tooltip)
- A "D" icon button calls `OpenPendingSampleDetail(pending)` which spawns a `DetailPanel` with `IsLinked = false` (pinned to that one sample, not tracking further selection events)

---

## Task 3 – InstanceState Serialization

### Problem
Reconstructed samples always got `SampleInfo.InstanceState = 0` (value zero, which is outside the `DdsInstanceState` enum's named values — not `Alive = 16`), so `GetStatusClass` fell through to `--no-writers` for every sample.

### Fix

**Export** (`ExportService.WriteSampleRecord`):
```csharp
writer.WriteString("InstanceState", sample.SampleInfo.InstanceState.ToString());
```
Written as a named string (e.g. `"Alive"`, `"NotAliveDisposed"`) — human-readable and stable.

**Record** (`SampleExportRecord`):
```csharp
public string? InstanceState { get; set; }
```
Absence (`null`) means old Batch-24 export; defaults to `Alive` on import.

**Import** (`ImportService.TryReconstructSample`):
```csharp
var instanceState = DdsInstanceState.Alive;   // default for old files
if (!string.IsNullOrEmpty(record.InstanceState))
    Enum.TryParse(record.InstanceState, ignoreCase: true, out instanceState);

return new SampleData
{
    ...
    SampleInfo = new DdsApi.DdsSampleInfo { InstanceState = instanceState }
};
```

This is backward-compatible: old JSON files that lack the `"InstanceState"` key deserialize `SampleExportRecord.InstanceState` as `null`, and the `if`-guard keeps `DdsInstanceState.Alive` as the default, which is correct for the vast majority of historical captures.

---

## 1. Blazor `oninput` vs `onchange` for Real-Time Trackbar Readouts

Blazor maps `@onchange` to the DOM `change` event and `@oninput` to the DOM `input` event.

| Event | When it fires |
|---|---|
| `change` | After the user **releases** the slider thumb (mouse-up / pointer-up) |
| `input` | On **every** movement while the thumb is held |

Because Blazor Server renders over a SignalR WebSocket, each `input` event sends a small message (~30–50 bytes carrying the integer slider value) to the server-side component.  The handler calls `SeekToFrame` / `SeekToTime`, which writes `_currentIndex` atomically and fires `StateChanged`, which calls `InvokeAsync(StateHasChanged)`.  Blazor then diffs the component tree and sends only the changed DOM patches back — typically updating a handful of `<span>` values in the metrics grid.

For the locked-during-playback case: the `disabled` attribute prevents any DOM `input` events from being generated, so no messages reach the server while the background playback loop is advancing the index.  This avoids a data race between the scrubber callback and `RunPlaybackAsync`.

---

## 2. InstanceState Serialization — Backward Compatibility with Batch-24 JSON

Batch-24 exports were produced with `AssemblyQualifiedName` type names and no `InstanceState` field.  Batch-25 switched to `FullName` and still had no `InstanceState`.

The new field is **additive only**:

1. `SampleExportRecord.InstanceState` is a nullable `string?`, so `System.Text.Json`'s streaming deserializer simply leaves it `null` when the key is absent in older files.
2. The import reconstructor checks `!string.IsNullOrEmpty(record.InstanceState)` before calling `Enum.TryParse`, so the code path for old files is unchanged — it always picks `DdsInstanceState.Alive` as the default.
3. The export side writes the field unconditionally, so all files written from Batch-26 onward will round-trip correctly.
4. `Enum.TryParse(..., ignoreCase: true, ...)` is used so any future casing inconsistency (e.g. `"notAliveDisposed"`) is tolerated gracefully.

No migration step is required. Old files load without errors and their samples all show as `Alive` (the most common state for a captured write-stream) — a reasonable approximation for pre-Batch-26 exports.

---

## Files Changed

| File | Change |
|---|---|
| `DdsMonitor.Engine/Replay/IReplayEngine.cs` | Added `SampleData? NextSample { get; }` |
| `DdsMonitor.Engine/Replay/ReplayEngine.cs` | Implemented `NextSample` property |
| `DdsMonitor.Engine/Export/ExportService.cs` | Write `InstanceState` string to JSON |
| `DdsMonitor.Engine/Import/SampleExportRecord.cs` | Added `string? InstanceState` property |
| `DdsMonitor.Engine/Import/ImportService.cs` | Added `CycloneDDS.Runtime` usings; reconstruct `SampleInfo.InstanceState`; backward-compat default |
| `DdsMonitor/Components/ReplayPanel.razor` | Removed Target selector; `oninput` scrubber; disabled during play; top toolbar with browse icon; Next Sample preview section; `OpenPendingSampleDetail`; removed large filter button |
| `DdsMonitor/Components/SamplesPanel.razor` | Added `GetStatusTooltip()` with descriptive labels; updated expanded card tooltip |
| `DdsMonitor/Components/DetailPanel.razor` | Added `CycloneDDS.Runtime` usings; state badge `<span>` in toolbar; `GetInstanceStateClass` / `GetInstanceStateTooltip` helpers |
| `DdsMonitor/wwwroot/app.css` | Updated status dot colors (green/red/amber); added `detail-state-badge` styles; added SVG icon tint rules; added `replay-panel__toolbar`, `replay-panel__toolbar-btn`, `replay-panel__next-sample` styles |
