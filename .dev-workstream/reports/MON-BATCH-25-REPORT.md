# MON-BATCH-25-REPORT

**Batch:** MON-BATCH-25  
**Tasks:** Export Format Fixes, File Browser Dialog, Replay Panel Redesign, Replay Filtering  
**Status:** ✅ COMPLETE  
**Build Result:** 0 errors, 1 pre-existing nullable warning (CS8601 in SamplesPanel — unrelated to batch work)

---

## Deliverables

| Task | File(s) | Status |
|---|---|---|
| Task 1 – Export format | `Export/ExportService.cs`, `Import/ImportService.cs` | ✅ |
| Task 2 – File Browser Dialog | `Components/FileDialog.razor`, `Components/FileDialogMode.cs`, `Components/FileEntry.cs` | ✅ |
| Task 2 – Wire into SamplesPanel | `Components/SamplesPanel.razor` | ✅ |
| Task 2 – Wire into ReplayPanel | `Components/ReplayPanel.razor` | ✅ |
| Task 3 – Replay Engine extended | `Engine/Replay/IReplayEngine.cs`, `Engine/Replay/ReplayEngine.cs`, `Engine/Replay/ReplayPlaybackMode.cs` | ✅ |
| Task 3 – ReplayPanel redesign | `Components/ReplayPanel.razor` | ✅ |
| Task 3 – CSS | `wwwroot/app.css` | ✅ |
| Task 4 – Replay filtering | `Engine/EventBrokerEvents.cs`, `Components/SamplesPanel.razor`, `Components/ReplayPanel.razor` | ✅ |

---

## Task 1 – Export Format Fixes

### Changes to `ExportService.cs`
- Removed the `SizeBytes` field from the written JSON (`writer.WriteNumber("SizeBytes", …)` line deleted).  
- Changed `TopicTypeName` serialization from `type.AssemblyQualifiedName` to `type.FullName` so exported files remain portable across assembly versions.

### Changes to `ImportService.cs`
`Type.GetType()` only resolves assembly-qualified names.  Since files now store a bare `FullName`, a fallback was added:

```
Type.GetType(typeName)          // works when typeName is an AQN (legacy files)
    ↓ returns null
Scan AppDomain.CurrentDomain.GetAssemblies()
    → type.FullName == typeName  // resolves from any already-loaded assembly
```

This keeps backward compatibility with old AQN-format exports while supporting the new FullName format.

---

## Task 2 – Custom File Browser Dialog

`FileDialog.razor` is a fully server-side Blazor modal that directly queries the host filesystem.  Because DdsMonitor is a Blazor Server application, the Razor component code runs on the server, giving it access to `DriveInfo`, `Directory`, and `File` APIs.

### Component Design

| Parameter | Type | Purpose |
|---|---|---|
| `IsOpen` | `bool` | Controls visibility |
| `Mode` | `FileDialogMode` | `Open` (must exist) or `Save` (directory must exist) |
| `InitialPath` | `string` | Pre-populates current directory and filename |
| `Filter` | `string` | Wildcard filter applied to file listing (e.g. `*.json`) |
| `OnResult` | `EventCallback<string?>` | `null` = cancelled; non-null = confirmed absolute path |

Navigation features:
- **Drive selector** – Windows-only strip of drive buttons (`DriveInfo.GetDrives()`)
- **Address bar** – Editable path input + "Go" button + "Up" button
- **Directory listing** – Folders first (double-click navigates), then files (single-click selects, double-click confirms)
- **Filename input** – Editable; supports entering a relative filename combined with `_currentDir`
- Validation: Open mode checks `File.Exists`; Save mode checks parent directory exists

### Integration Points
- **SamplesPanel export**: button previously invoked `window.prompt()` — now sets `_showExportDialog = true` which opens the `FileDialog` in `Save` mode with `Filter="*.json"`. The `OnExportDialogResult` callback captures the confirmed path and calls `ExportService.ExportSamplesAsync`.
- **ReplayPanel load**: "Browse…" button opens the `FileDialog` in `Open` mode. On confirmation the path populates the file path field; the existing "Load" button triggers `IReplayEngine.LoadAsync`.

---

## Task 3 – Replay Panel Redesign & Scrubbing

### IReplayEngine Extensions

```csharp
// Metrics
int FilteredTotalCount { get; }          // samples visible after filter
ReplayPlaybackMode PlaybackMode { get; set; }
DateTime StartTime { get; }
DateTime EndTime   { get; }
TimeSpan TotalDuration { get; }
DateTime CurrentTimestamp   { get; }
TimeSpan CurrentRelativeTime { get; }
DateTime NextSampleTimestamp { get; }    // timestamp of upcoming sample

// Navigation
void Step(ReplayTarget target);
void SeekToFrame(int frameIndex);
void SeekToTime(TimeSpan relativeTime);

// Filtering
void SetFilter(Func<SampleData, bool>? filterPredicate);
```

### ReplayEngine Implementation

The engine maintains two lists:
- `_samples` — full unfiltered list loaded from the JSON file
- `_filteredSamples` — computed view; equals `_samples` when no filter is active; otherwise materialized by `RebuildFilter()` into a new `List<SampleData>`

All playback and seeking operations index into `_filteredSamples` exclusively, so filtering produces a seamlessly smaller "recording" from the engine's perspective.

### ReplayPanel Sections

| Section | Contents |
|---|---|
| File | Path display, Browse button (FileDialog), Load button |
| Transport | Play/Pause toggle, Stop, Step, animated status badge |
| Position | Mode toggle (Frames/Time), scrubber slider, Jump-To input |
| Settings | Target select, Speed select (0.25–8×), Loop checkbox |
| Time Metrics | TotalSamples/Filtered, StartTime, EndTime, Duration, CurrentTimestamp, CurrentRelativeTime, NextSampleTimestamp |
| Filter | "Browse & Filter Replay Samples", "Clear Filter" button, active-filter indicator |

---

## 1. How the Replay Engine Manages the Playback Pointer Across Frame/Time Modes

`PlaybackMode` (an enum: `Frames` / `Time`) is a **UI rendering hint only**; the underlying playback pointer is always the integer field `_currentIndex` — a zero-based index into `_filteredSamples`.

**Frames mode** — the scrubber's integer value maps 1:1 to `SeekToFrame(value)`, which simply sets `_currentIndex = Math.Clamp(frameIndex, 0, _filteredSamples.Count - 1)`.

**Time mode** — the scrubber represents elapsed seconds (stored as a `double`).  `SeekToTime(TimeSpan relativeTime)` converts that to an absolute UTC target via `StartTime + relativeTime`, then performs a binary search on `_filteredSamples` to find the index whose `WrittenAt` timestamp is nearest:

```
lo = 0, hi = _filteredSamples.Count - 1
while lo < hi:
    mid = (lo + hi) / 2
    if _filteredSamples[mid].WrittenAt < target: lo = mid + 1
    else: hi = mid
_currentIndex = lo
```

After the seek, `_currentIndex` has been repositioned whether the mode is Frames or Time — there is no separate "time pointer" or "frame pointer". The UI properties `CurrentTimestamp` and `CurrentRelativeTime` both derive from `_filteredSamples[_currentIndex]`; `CurrentFrame` equals `_currentIndex`. Switching the mode toggle in the UI purely changes how the scrubber is labeled and how the Jump-To input is parsed — the engine itself is unaffected.

The playback loop likewise iterates `_filteredSamples` starting at `_currentIndex`, advancing the index after each dispatch and computing the inter-sample delay from the difference in consecutive `WrittenAt` values scaled by the speed multiplier, regardless of mode.

---

## 2. How the Replay Engine Dynamically Respects the Filter Applied by the Connected Samples Panel

The integration uses four cooperating components:

### Step 1 — Opening the Browse Panel

When the user clicks **"Browse & Filter Replay Samples"** in `ReplayPanel`, the panel calls `BuildReplaySamplesSnapshot()`, which downcasts `IReplayEngine` to the concrete `ReplayEngine` and calls `GetSnapshotForBrowsing()` — returning `_samples.AsReadOnly()`.

`WindowManager.SpawnPanel` is then called with:
```csharp
[nameof(SamplesPanel.FixedSamples)] = snapshot   // IReadOnlyList<SampleData>
```

### Step 2 — SamplesPanel in FixedSamples Mode

`SamplesPanel` has a new `[Parameter] IReadOnlyList<SampleData>? FixedSamples`.  When this is non-null, `EnsureView()` uses it as the data source instead of `SampleStore.AllSamples`.  The filter, sort, and column logic work identically.

### Step 3 — Filter Change Broadcast

`SamplesPanel` now has a `NotifyFilterChanged()` helper that is called every time `_filterPredicate` is assigned in `ApplyFilter()`.  When `FixedSamples != null`, it also publishes:

```csharp
EventBroker.Publish(new ReplayFilterChangedEvent(_filterPredicate));
```

`ReplayFilterChangedEvent` is a record carrying a `Func<SampleData, bool>?` predicate.

### Step 4 — Replay Engine Synchronisation

`ReplayPanel` subscribes to `ReplayFilterChangedEvent` during `OnAfterRenderAsync`:

```csharp
_replayFilterSubscription = EventBroker.Subscribe<ReplayFilterChangedEvent>(evt =>
{
    _filterActive = evt.Predicate != null;
    ReplayEngine.SetFilter(evt.Predicate);
    InvokeAsync(StateHasChanged);
});
```

`ReplayEngine.SetFilter` stores the predicate and calls `RebuildFilter()`, which materializes `_filteredSamples`:

```csharp
_filteredSamples = _filterPredicate == null
    ? _samples
    : _samples.Where(_filterPredicate).ToList();
_currentIndex = 0;   // reset head after filter change
```

From this point, all playback, stepping, and seeking operates on the filtered subset.  The filter remains active until the user clicks "Clear Filter" (`SetFilter(null)` → full `_samples` is restored).

### Data Flow Summary

```
User types filter in SamplesPanel (FixedSamples mode)
    ↓ ApplyFilter() → NotifyFilterChanged()
    ↓ EventBroker.Publish(ReplayFilterChangedEvent(predicate))
    ↓ ReplayPanel subscription handler
    ↓ ReplayEngine.SetFilter(predicate)
    ↓ RebuildFilter() materializes _filteredSamples
    ↓ _currentIndex reset to 0
Next Play / Step / Seek operates on filtered subset only
```

---

## Files Changed

| File | Change |
|---|---|
| `DdsMonitor.Engine/Export/ExportService.cs` | Removed SizeBytes write; `FullName` instead of `AssemblyQualifiedName` |
| `DdsMonitor.Engine/Import/ImportService.cs` | Added assembly-scan fallback for FullName resolution |
| `DdsMonitor.Engine/Replay/IReplayEngine.cs` | Extended with metrics, Step, Seek, SetFilter |
| `DdsMonitor.Engine/Replay/ReplayEngine.cs` | Complete rewrite; dual-list filter model; `GetSnapshotForBrowsing()` |
| `DdsMonitor.Engine/Replay/ReplayPlaybackMode.cs` | New enum (Frames / Time) |
| `DdsMonitor.Engine/EventBrokerEvents.cs` | Added `ReplayFilterChangedEvent` |
| `DdsMonitor/Components/FileDialog.razor` | New server-side file browser modal |
| `DdsMonitor/Components/FileDialogMode.cs` | New enum (Open / Save) |
| `DdsMonitor/Components/FileEntry.cs` | New record for file listing entries |
| `DdsMonitor/Components/SamplesPanel.razor` | FixedSamples param, NotifyFilterChanged, FileDialog integration |
| `DdsMonitor/Components/ReplayPanel.razor` | Complete rewrite with new UX sections |
| `DdsMonitor/wwwroot/app.css` | ~400 lines added for FileDialog and ReplayPanel styles |
