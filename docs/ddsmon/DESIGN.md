# DDS Monitor — Design Document

**Status:** Draft  
**Task tracker:** [TASK-TRACKER.md](./TASK-TRACKER.md)  
**Task details:** [TASK-DETAIL.md](./TASK-DETAIL.md)

---

## 1. Background & Motivation

This project is a **ground-up rewrite** of an older tool that:

- Replaces the entire UI stack with **Blazor Server** (C# backend, HTML/CSS/JS frontend)
- Uses the new **`CycloneDDS.NET`** strongly-typed generic wrapper library
- Preserves all functional capabilities of the original tool
- Adds new capabilities (remote monitoring, plugin extensibility, modern web UX)


### 1.2 Performance Philosophy

DDS networks can produce tens of thousands of samples per second. The monitor must remain responsive under this load. Key performance constraints:

1. **Lock-free ingestion** — DDS threads must never block on UI rendering
2. **UI throttling** — the Blazor UI re-renders at a fixed ~30 Hz cadence, not per-sample
3. **DOM virtualization** — the browser renders only the ~30-50 rows currently visible, regardless of dataset size
4. **Zero-reflection at render time** — field accessors are compiled once at startup via `Fasterflect`, then invoked as fast delegates
5. **Background sorting** — incremental merge-sort runs on a background thread; the UI never waits for a sort to complete

---

## 2. Technology Stack

| Layer | Technology | Rationale |
|---|---|---|
| DDS Communication | `CycloneDDS.NET` (this repo) | Strongly-typed generic readers/writers, zero-copy loans, `WaitDataAsync` |
| Backend Runtime | .NET 8+ | LTS, AOT-friendly, `System.Threading.Channels`, modern hosting |
| Web Framework | Blazor Server (local) | Same-process memory sharing, no JSON serialization between backend and UI |
| Web Host | ASP.NET Core Kestrel | Lightweight, embeddable, auto-launches browser |
| Fast Reflection | `Fasterflect` | IL-emitted delegates for field access; pay reflection cost once at startup |
| Dynamic Filtering | `System.Linq.Dynamic.Core` | Compile user-typed filter expressions into native `Func<>` delegates |
| Plugin Loading | `AssemblyLoadContext` | Isolated assembly loading for domain-specific plugins |

---

## 3. Architecture Overview

The application is structured into three distinct layers:

```
┌─────────────────────────────────────────────────────────┐
│  Layer 3: Blazor Workspace Shell (UI)                   │
│  ┌─────────┐ ┌──────────┐ ┌────────┐ ┌──────────────┐  │
│  │ Window  │ │ Data     │ │ Detail │ │ Tool Panels  │  │
│  │ Manager │ │ Grids    │ │ Views  │ │ (Send/Replay)│  │
│  └─────────┘ └──────────┘ └────────┘ └──────────────┘  │
├─────────────────────────────────────────────────────────┤
│  Layer 2: Plugin Ecosystem (Middleware)                  │
│  ┌──────────────┐ ┌──────────────┐ ┌────────────────┐  │
│  │ BDC Plugin   │ │ TKB Plugin   │ │ Custom Plugins │  │
│  └──────────────┘ └──────────────┘ └────────────────┘  │
├─────────────────────────────────────────────────────────┤
│  Layer 1: Headless Data Engine (Backend)                │
│  ┌────────────┐ ┌────────────┐ ┌──────────────────┐   │
│  │ Topic      │ │ Dynamic    │ │ SampleStore &    │   │
│  │ Discovery  │ │ DDS Bridge │ │ InstanceStore    │   │
│  └────────────┘ └────────────┘ └──────────────────┘   │
│  ┌────────────┐ ┌────────────┐ ┌──────────────────┐   │
│  │ Playback   │ │ Import /   │ │ Filter Compiler  │   │
│  │ Engine     │ │ Export     │ │ (Dynamic LINQ)   │   │
│  └────────────┘ └────────────┘ └──────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

### 3.1 Layer 1 — Headless Data Engine

The core of the application. Knows nothing about Blazor or HTML. Can run headless (CLI mode).

**Components:**

| Component | Responsibility |
|---|---|
| `TopicDiscoveryService` | Scans assemblies in configurable directories for `[DdsTopic]` types; builds `TopicMetadata` registry with `Fasterflect` getters/setters |
| `DdsBridge` | Manages `DdsParticipant`, `IDynamicReader`, `IDynamicWriter` instances; handles partition changes |
| `SampleStore` | Append-only chronological ledger of all received samples; provides filtered/sorted virtual views for the UI |
| `InstanceStore` | Tracks keyed instance lifecycle (Alive/Disposed/NoWriters); fires transition events for plugins |
| `FilterCompiler` | Compiles user text expressions into `Func<SampleData, bool>` delegates via Dynamic LINQ |
| `PlaybackEngine` | Replays imported samples to the DDS network with time-accurate delays and speed control |
| `ImportExportService` | Streams samples to/from `.samples` JSON files using `Utf8JsonWriter`/`Utf8JsonReader` |

### 3.2 Layer 2 — Plugin Ecosystem

Domain-specific logic (BDC entities, TKB entities, custom formatters) is encapsulated in hot-loadable Razor Class Library DLLs. Plugins are loaded from a configurable `plugins/` directory at startup. See [§12 Plugin Architecture](#12-plugin-architecture) for full details.

### 3.3 Layer 3 — Blazor Workspace Shell

A local Blazor Server application rendering a **Web Desktop** UI paradigm — floating, resizable, dockable panels that persist their layout between sessions. See [§8 Web Desktop Window Manager](#8-web-desktop-window-manager) for details.

---

## 4. Data Pipeline

### 4.1 Ingestion Flow

```
Native CycloneDDS → DynamicReader<T> → Channel<SampleData> → SampleStore + InstanceStore → UI (30 Hz)
```

**Sequence:**

1. `DynamicReader<T>` calls `WaitDataAsync()` on the native DDS reader
2. On data arrival, it takes the `DdsLoan<T>`, boxes the payload as `object`, extracts `DdsSampleInfo` and `SenderIdentity`
3. The `SampleData` record is pushed into a `System.Threading.Channels.Channel<SampleData>` (lock-free, zero-allocation queue)
4. A background `DdsIngestionService` (`IHostedService`) reads from the channel and:
   - Appends to `SampleStore` (the chronological ledger)
   - Passes to `InstanceStore` (if the topic is keyed)
5. The UI components poll at ~30 Hz, checking if `CurrentFilteredCount` changed, and only then call `StateHasChanged()`

### 4.2 The `SampleData` Record

```csharp
public record SampleData
{
    public long Ordinal { get; init; }
    public object Payload { get; init; }         // Boxed DDS struct
    public TopicMetadata TopicMetadata { get; init; }
    public DdsSampleInfo SampleInfo { get; init; }
    public SenderIdentity? Sender { get; init; }
    public DateTime Timestamp { get; init; }     // Local receive time
    public int SizeBytes { get; init; }          // Serialized size (optional)
}
```

### 4.3 Dynamic DDS Bridge

Because the monitor only knows about topics at runtime via `System.Type`, the strongly-typed `DdsReader<T>` is wrapped in a non-generic interface:

```csharp
public interface IDynamicReader : IDisposable
{
    Type TopicType { get; }
    void Start(string partition);
    void Stop();
}

public interface IDynamicWriter : IDisposable
{
    Type TopicType { get; }
    void Write(object payload);
    void DisposeInstance(object payload);
}
```

Concrete `DynamicReader<T>` and `DynamicWriter<T>` implementations are instantiated at runtime via `MakeGenericType`:

```csharp
var readerType = typeof(DynamicReader<>).MakeGenericType(topicType);
var reader = (IDynamicReader)Activator.CreateInstance(readerType, comm, participant, partition);
reader.Start(partition);
```

### 4.4 Partition Management

Partitions are handled via the `CycloneDDS.NET` library's optional `partition` parameter on `DdsReader<T>` and `DdsWriter<T>`. The `DdsBridge` stores the current partition and, on change:

1. Stops all active `IDynamicReader` instances
2. Records which topic types were subscribed
3. Recreates all readers with the new partition string
4. The `DdsParticipant` remains stable and untouched

---

## 5. Topic Discovery & Metadata Registry

### 5.1 Assembly Scanning

At startup, the `TopicDiscoveryService`:

1. Reads one or more directories from configuration (`appsettings.json` or CLI `--plugin-dir`)
2. Enumerates all `.dll` files in those directories
3. Loads them into a custom `AssemblyLoadContext` (isolated from the host app)
4. Scans all exported types for the `[DdsTopic]` attribute
5. Generates `TopicMetadata` for each discovered type

### 5.2 TopicMetadata

```csharp
public class TopicMetadata
{
    public Type TopicType { get; }
    public string TopicName { get; }            // From [DdsTopic] attribute
    public string ShortName { get; }            // e.g. "RobotState"
    public string Namespace { get; }            // e.g. "company.DDS.DM"
    public bool IsKeyed { get; }
    public IReadOnlyList<FieldMetadata> AllFields { get; }
    public IReadOnlyList<FieldMetadata> KeyFields { get; }
}
```

### 5.3 FieldMetadata

All nested properties of the DDS struct are **flattened** into a list of `FieldMetadata` objects. The `Getter` and `Setter` are `Fasterflect`-compiled IL delegates — no reflection at render time.

```csharp
public class FieldMetadata
{
    public string StructuredName { get; }   // e.g. "Position.X"
    public string DisplayName { get; }      // e.g. "Position.X"
    public Type ValueType { get; }          // e.g. typeof(double)
    public MemberGetter Getter { get; }     // Fasterflect compiled delegate
    public MemberSetter Setter { get; }     // Fasterflect compiled delegate
}
```

### 5.4 Synthetic (Computed) Fields

The registry also injects synthetic fields that are not part of the DDS struct but are computed at display time:

| Synthetic Field | Computation |
|---|---|
| `Delay [ms]` | `LocalReceiveTime - DdsSourceTimestamp` in milliseconds |
| `Size [B]` | Serialized CDR byte count (optional, can be disabled for CPU savings) |

These appear in the Column Picker alongside physical fields and can be sorted/filtered.

---

## 6. SampleStore

The `SampleStore` is the **historical ledger** — it records every single message chronologically.

### 6.1 Interface

```csharp
public interface ISampleStore
{
    // Raw data access (for Export, Replay)
    IReadOnlyList<SampleData> AllSamples { get; }
    ITopicSamples GetTopicSamples(Type topicType);

    // Ingestion
    void Append(SampleData sample);
    void Clear();

    // UI view (filtered & sorted for Blazor <Virtualize>)
    int CurrentFilteredCount { get; }
    ReadOnlySpan<SampleData> GetVirtualView(int startIndex, int count);

    // View configuration
    void SetFilter(Func<SampleData, bool> compiledFilterPredicate);
    void SetSortSpec(FieldMetadata field, SortDirection direction);

    event Action OnViewRebuilt;
}

public interface ITopicSamples
{
    Type TopicType { get; }
    int TotalCount { get; }
    IReadOnlyList<SampleData> Samples { get; }
}
```

### 6.2 Internal Architecture

The store maintains data using **double-buffering** for thread safety:

1. **Append-Only Base** — a `List<SampleData> _allSamples` and per-topic `ConcurrentDictionary<Type, TopicSamples>`
2. **Filtered List** — samples that pass the current filter are appended to `_filteredSamples`
3. **Sorted View** — a background `SortMergeWorker` wakes every ~50ms, sorts newly arrived items, and merges them with the existing sorted array via `Interlocked.Exchange`
4. **UI Access** — `GetVirtualView(startIndex, count)` returns a zero-allocation `ReadOnlySpan` slice from the sorted array

### 6.3 Filter Application

- **New filter text** → `FilterCompiler` compiles to `Func<SampleData, bool>` → background task rebuilds `_filteredSamples` from `_allSamples`
- **Incoming live data** → the ingestion worker evaluates the active delegate inline; passing samples are appended to `_filteredSamples` immediately

### 6.4 Multiple View Instances

Each floating `SamplesPanel` window can instantiate its own local `ViewCache` that hooks into the shared `_allSamples` but applies its own independent filter and sort. This allows `SamplesPanel.1` to be filtered by Topic A sorted by Velocity, while `SamplesPanel.2` shows Topic B sorted by Timestamp.

---

## 7. InstanceStore

The `InstanceStore` groups samples by their `[DdsKey]` fields and tracks instance lifecycle.

### 7.1 Interface

```csharp
public interface IInstanceStore
{
    ITopicInstances GetTopicInstances(Type topicType);
    IObservable<InstanceTransitionEvent> OnInstanceChanged { get; }
    void Clear();
}

public interface ITopicInstances
{
    int LiveCount { get; }
    IReadOnlyDictionary<InstanceKey, InstanceData> InstancesByKey { get; }
    IReadOnlyList<InstanceJournalRecord> Journal { get; }
}
```

### 7.2 Key Extraction

```csharp
public readonly record struct InstanceKey(object[] Values);
```

Key values are extracted using `Fasterflect` getters from `TopicMetadata.KeyFields`. The `InstanceKey` uses `SequenceEqual` for equality and a combined hash for dictionary lookups.

### 7.3 State Machine

When `ProcessSample()` is called for a keyed topic:

```
┌──────────────┐   Alive sample   ┌──────────────┐
│  Not Seen    │ ───────────────► │   ALIVE      │
│  (new key)   │                  │              │
└──────────────┘                  └──────┬───────┘
                                         │ Disposed/NoWriters
                                         ▼
                                  ┌──────────────┐
                                  │   DEAD       │
                                  │              │
                                  └──────┬───────┘
                                         │ Alive sample (re-birth)
                                         ▼
                                  ┌──────────────┐
                                  │   ALIVE      │
                                  │ (NumRecent=0)│
                                  └──────────────┘
```

**Pseudocode:**

```
function ProcessSample(sample, topicMeta):
    key = ExtractKey(sample.Payload, topicMeta)
    topicInstances = _instancesByTopic.GetOrAdd(topicMeta.TopicType)
    isAlive = sample.SampleInfo.InstanceState == Alive

    if key NOT in topicInstances.InstancesByKey:
        // First time seen
        instance = new InstanceData(topic=topicMeta, key=key, creationSample=sample)
        topicInstances.InstancesByKey[key] = instance
        if isAlive: topicInstances.LiveCount++
        journal.Add(instance, sample)
        fire OnInstanceChanged(Added, instance, sample)

    else:
        instance = topicInstances.InstancesByKey[key]
        wasAlive = instance.RecentSample.InstanceState == Alive

        if wasAlive AND NOT isAlive:
            // Death
            topicInstances.LiveCount--
            journal.Add(instance, sample)
            fire OnInstanceChanged(Removed, instance, sample)

        else if NOT wasAlive AND isAlive:
            // Re-birth
            topicInstances.LiveCount++
            instance.RecentCreationSample = sample
            instance.NumSamplesRecent = 0
            journal.Add(instance, sample)
            fire OnInstanceChanged(Added, instance, sample)

        else:
            // Normal update
            fire OnInstanceChanged(Updated, instance, sample)

    instance.RecentSample = sample
    instance.NumSamplesTotal++
    instance.NumSamplesRecent++
```

---

## 8. Web Desktop Window Manager

### 8.1 Motivation

The monitor requires **multiple simultaneous panels** — several sample grids, detail views, send tools, and replay controls all visible at once. A tabbed interface is too restrictive. The solution is a **Web Desktop** paradigm: floating, resizable, dockable panels rendered as absolute-positioned HTML `<div>` elements.

### 8.2 Panel State

```csharp
public class PanelState
{
    public string PanelId { get; set; }             // e.g. "SamplesPanel.1"
    public string Title { get; set; }               // e.g. "Samples [SensorData]"
    public string ComponentTypeName { get; set; }   // Razor component type to render inside

    // Spatial properties
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int ZIndex { get; set; }
    public bool IsMinimized { get; set; }

    // Component-specific state (custom columns, filter text, linked source, etc.)
    public Dictionary<string, object> ComponentState { get; set; } = new();
}
```

### 8.3 IWindowManager

```csharp
public interface IWindowManager
{
    IReadOnlyList<PanelState> ActivePanels { get; }
    PanelState SpawnPanel(string componentTypeName, Dictionary<string, object> initialState = null);
    void ClosePanel(string panelId);
    void BringToFront(string panelId);
    void RegisterPanelType(string typeName, Type blazorComponentType);  // For plugins
    void SaveWorkspace(string filePath);
    void LoadWorkspace(string filePath);
}
```

### 8.4 Rendering

The root `Desktop.razor` component iterates over `ActivePanels` and renders each as an absolute-positioned `<div>` with:

- A title bar (draggable via mouse events)
- A body containing a Blazor `<DynamicComponent>` that resolves the panel's `ComponentTypeName` to the actual Razor component type
- Resize handles on borders

### 8.5 State Persistence

All panel positions, sizes, z-indices, and component-specific state are serialized to a single `workspace.json` file (replacing ImGui's `.ini`). On browser refresh, the workspace is restored exactly.

### 8.6 Panel Identity Generation

When spawning a new panel, the `WindowManager` auto-generates an ID (e.g. `SamplesPanel.1`, `SamplesPanel.2`) by finding the first free index for that component type.

---

## 9. Event Broker (Panel Communication)

### 9.1 Purpose

Floating panels must communicate without hardcoded references. The `IEventBroker` provides a type-safe pub/sub mechanism.

```csharp
public interface IEventBroker
{
    void Publish<TEvent>(TEvent eventMessage);
    IDisposable Subscribe<TEvent>(Action<TEvent> handler);
}
```

### 9.2 Key Events

| Event | Publisher | Subscriber(s) |
|---|---|---|
| `SampleSelectedEvent(SourcePanelId, SampleData)` | Data Grid on row click | Linked Inspector pane |
| `CloneAndSendRequestEvent(TopicMeta, Payload)` | Data Grid context menu or Inspector | Send Sample Panel |
| `SpawnPanelEvent(PanelType, State)` | Any panel | WindowManager |
| `AddColumnRequestEvent(TargetPanelId, FieldPath)` | Inspector "Quick Add" button | Target SamplesPanel |

### 9.3 Master-Detail Linking

- **Linked mode:** A `DetailPanel` subscribes to `SampleSelectedEvent`. It checks `event.SourcePanelId == this.LinkedSourceId`. If matched, it re-renders with the new sample.
- **Detached mode:** A `DetailPanel` has `LinkedSourceId = null`. It holds a static reference to a specific `SampleData` and ignores all selection events.
- **Visual indicator:** The title bar displays `[Linked to: SamplesPanel.1]` or `[Detached]`. A padlock icon toggles between modes.

---

## 10. UI Components — Functional Specification

### 10.1 Topic Explorer

The central dashboard showing all discovered topics with live statistics.

```
[Topics]                                                          [−][X]
+------------------------------------------------------------------------+
| [ ] All  [+] Received  [o] Subscribed  [o] Keyed  [o] Alive           |
|------------------------------------------------------------------------|
| [ ] Subscribe All | Search: [ type to filter topics...            ]    |
|------------------------------------------------------------------------|
| Sub | Name                    | Samples | Instances | Actions          |
|-----+-------------------------+---------+-----------+------------------|
| [X] | RobotState              | 15,420  | 5         | [Grid][Instances]|
| [ ] | CameraInfo              | 0       | 0         | [Grid]           |
| [X] | PlaySoundEvent          | 12      | 0         | [Grid]           |
| [X] | WeaponStatus            | 4,100   | 1         | [Grid][Instances]|
+------------------------------------------------------------------------+
```

**Features:**

- **Tri-State Display Filters:** Top-row toggles cycle through Include (+), Exclude (−), Ignore (o) for filtering by Received, Subscribed, Keyed, Alive
- **Subscription Checkboxes:** Toggle DDS reader creation/destruction per topic
- **Live Statistics:** Sample count and instance count update at ~30 Hz
- **[Grid] button / Double-click:** Spawns a new `SamplesPanel` filtered to this topic
- **[Instances] button:** Spawns an `InstancesPanel` (visible only for keyed topics)

### 10.2 Topic Picker

Reusable incremental-search component for selecting a topic from a large list.

```
+------------------------------------------------------------+
| [🔍 rob|                                                 ] |
+------------------------------------------------------------+
| RobotState                  company.DDS.DM.State            |
| RobotCommand                company.DDS.DM.Control          |
| GeoSpatial_Robot            company.BDC.Extensions          |
+------------------------------------------------------------+
```

**Features:**

- **Two-column display:** Short name (highlighted, e.g. green) on left; namespace (dimmed, e.g. gray) on right
- **Incremental filtering:** Matches on every keystroke against both short name and namespace
- **Keyboard navigation:** Arrow Up/Down to highlight, Enter to select
- **Reused in:** Send Sample panel, Filter Builder field selector

### 10.3 Column Picker

Modal dialog for selecting which payload fields appear as columns in a Data Grid.

```
[Select Columns for 'RobotState']                             [−][X]
+------------------------------------------------------------------------+
| Available Fields:                  Selected Columns:                   |
| [🔍 Search available...    ]      [🔍 Search selected...    ]         |
|                                                                        |
| > Header.Timestamp                 = Ordinal                           |
| > Header.SourceId       [ADD >]    = Status                            |
| > Payload.Position.X               = Payload.Id           [REMOVE]     |
| > Payload.Position.Y               = Payload.Velocity     [REMOVE]     |
| > Payload.Position.Z                                                   |
| > Payload.BatteryLevel                                                 |
|                                                                        |
|                                    [ Remove All ]                      |
|------------------------------------------------------------------------|
|                                              [ Cancel ] [ Apply ]      |
+------------------------------------------------------------------------+
```

**Features:**

- **Flattened field tree:** All nested properties presented as flat strings (e.g. `Header.Timestamp.Nanoseconds`)
- **Dual-list UI:** Search and move fields between Available and Selected panes
- **Drag-and-drop ordering:** Reorder selected columns by dragging
- **Per-panel state:** Each `SamplesPanel` remembers its own column set independently

### 10.4 Sample List (Data Grid)

The high-performance virtualized data grid for inspecting network traffic.

```
[Samples: RobotState]                                         [−][X]
+------------------------------------------------------------------------+
| [🔍 Filter...][🔃Sort:New][🪟Columns][vExpandAll][🔗Track][⚙️]         |
+------------------------------------------------------------------------+
| Ordinal|Stat|TopicName  |Size[B]|Time        |Delay|Pos.X              |
|--------+----+-----------+-------+------------+-----+--------------------|
| 10423  | 🗑️ | RobotState| 128   |14:02:01.123| 2   | 45.2              |
| 10424  | 🗑️ | RobotState| 128   |14:02:01.143| 1   | 46.1              |
| 10425  | ❌ | RobotState| 128   |14:02:01.163| 3   | 47.0              |
+------------------------------------------------------------------------+
| Showing 3 of 15,000 matching samples.                                  |
+------------------------------------------------------------------------+
```

**Features:**

- **Blazor `<Virtualize>`:** Only renders visible rows; scrolling maps to array index slicing
- **Standard columns:** Ordinal, Status icon, Topic Name, Size, Timestamp, Delay [ms]
- **Custom columns:** User-selected fields from the Column Picker, rendered via `Fasterflect` getters
- **Sorting:** Click column header to sort; background incremental merge-sort keeps UI responsive
- **Track mode ([🔗]):** Single-click or Arrow keys update the linked Inspector pane
- **Double-click:** Spawns a new frozen/detached Detail view tab
- **Context menu (right-click):** "Show Detail (New Window)", "Clone to Send/Emulator"
- **Expand All mode:** Toggles from table view to vertically scrolling list of full colored JSON trees (still virtualized)
- **Lifecycle icons (Stat column):**
  - 🗑️ Gray: Alive
  - 🗑️ Red: Disposed  
  - ❌ Red: No Writers
- **Panel settings export/import:** Save/load column layout, sort order, active filter to a `.samplepanelsettings` JSON file
- **Replay from filtered view:** Toolbar action spawns Replay Panel loaded only with samples currently visible in this specific grid
- **Hover payload hack:** Hovering over the action column icon renders the full colored object tree in a tooltip (using the Global Tooltip Portal pattern to avoid DOM bloat)

### 10.5 Sample Detail (Inspector)

Deep recursive inspection of a single sample's payload and DDS metadata.

```
[Detail [Linked to: Samples 1]]                               [−][X]
+------------------------------------------------------------------------+
| Status: [🟢 ALIVE]   [Send/Clone...]  [Open in New Window]            |
|------------------------------------------------------------------------|
| [Tree] [Table] [JSON] [Sample Info] [Sender]                          |
|------------------------------------------------------------------------|
| v Payload: (RobotState)                                                |
|   > Header: (HeaderType)                                               |
|   v Position:                                                          |
|       X: 45.2                                                          |
|       Y: 10.0                                                          |
|       Z: 0.0                                                           |
|   > ConfigJson: "{ \"mode\": \"auto\" }"                               |
+------------------------------------------------------------------------+
```

**Features:**

- **Tabbed renderers:** Tree (expandable, color-coded), Table (flat key-value), JSON (formatted, copyable), Sample Info (DDS metadata), Sender (PID, Machine, IP)
- **Color coding:** CSS classes for value types — strings in one color, numbers in another, booleans in another
- **Hover JSON hack:** If a `string` field contains valid JSON, hovering shows an indented, syntax-highlighted tooltip
- **Context menu:** Right-click a value for "Copy to Clipboard" (via JS `navigator.clipboard.writeText`) or "Show in Separate Window" (spawns `TextViewPanel`)
- **Clone to Send:** Button grabs the payload, deep-copies it via JSON serialize/deserialize, and opens the Send Sample panel pre-populated
- **Linked vs. Detached modes:** As described in [§9.3](#93-master-detail-linking)
- **Inspector debouncing:** If the user holds Arrow keys, the Inspector waits 50ms after the last keystroke before re-rendering the heavy tree (the grid highlight moves instantly)

### 10.6 Text View Panel

A floating window for viewing/editing large string values extracted from the Inspector.

**Features:**

- **Auto-detect JSON:** On open, checks if string starts with `{` or `[`; if valid JSON, auto-formats with indentation
- **Mode toggles:** Plain Text / JSON view; Wrapped (read-only) / Unwrapped (editable)
- **Syntax highlighting:** Via CSS or a lightweight JS library (e.g. `highlight.js`)

### 10.7 Visual Filter Builder

No-code tree-based UI for building complex boolean filter queries.

```
[Samples Filter]                                               [−][X]
+------------------------------------------------------------------------+
| [ Apply Filter ] [ Clear ] [ Load... ] [ Save... ]                     |
|------------------------------------------------------------------------|
| v [AND]  [+ Condition] [+ Sub-Group] [X]                              |
|   |                                                                    |
|   |-- [Payload.Id      ▼] [==        ▼] [42             ] [!] [X]    |
|   |                                                                    |
|   |-- v [OR]  [+ Condition] [+ Sub-Group] [X]                         |
|       |                                                                |
|       |-- [TopicName      ▼] [StartsWith▼] ["Robot"       ] [!] [X]  |
|       |-- [Payload.Status ▼] [==        ▼] [ERROR (Enum)▼] [!] [X]  |
+------------------------------------------------------------------------+
```

**Features:**

- **Recursive AST:** Users can infinitely nest AND/OR groups with leaf conditions
- **Field selection:** Uses the Incremental Picker component (two-column search)
- **Context-aware operators:**
  - Numbers: `==`, `!=`, `>`, `<`, `>=`, `<=`
  - Strings: `==`, `!=`, `StartsWith`, `EndsWith`, `Contains`
  - Booleans/Enums: `==`, `!=`
- **Smart value inputs:** Adapts to field type — enum becomes a dropdown, boolean becomes a checkbox, datetime becomes a date picker
- **Negation toggle ([!]):** Wraps a specific condition in `NOT`
- **Save/Load:** Export and import filter configurations as `.samplefilter` JSON files
- **Execution:** `Apply Filter` calls `RootNode.ToDynamicLinqString()` which outputs a text string; this is compiled by the `FilterCompiler` into a native delegate

### 10.8 Send Sample (Message Emulator)

Auto-generated data-entry form for crafting and publishing DDS messages.

```
[Send Sample]                                                  [−][X]
+------------------------------------------------------------------------+
| Topic: [🔍 RobotState (company.DDS.DM)              ▼]   [Clone 🗗]    |
|------------------------------------------------------------------------|
| > Header:                                                              |
|   Timestamp: [2023-10-27 14:02:01.123                  ]               |
|   SourceId:  [5                                        ]               |
| v Payload:                                                             |
|   v Position:                                                          |
|     X: [45.2                                           ]               |
|     Y: [10.0                                           ]               |
|     Z: [0.0                                            ]               |
|   State: [ACTIVE (Enum)                                ▼]              |
|   v Waypoints (List<Vector3>):                   [+ Add]              |
|     [0] X:[1.0] Y:[2.0] Z:[3.0]                [X]                   |
|------------------------------------------------------------------------|
|                                        [Send Dispose]  [Send]         |
+------------------------------------------------------------------------+
```

**Features:**

- **Topic selection:** Via the Incremental Topic Picker; creates a blank working copy via `Activator.CreateInstance`
- **Clone workflow:** Spawned via right-click "Send..." on any sample; deep-copies the payload via JSON round-trip to protect historical data
- **Dynamic binding:** Booleans → checkboxes, Enums → dropdowns, Numbers → numeric inputs, all driven by `Fasterflect` setters
- **Collection editing:** Arrays/Lists render with per-item editors, `[+ Add]` and `[X Remove]` buttons
- **Custom type drawers:** `ICustomComponentRegistry` allows plugins to register specialized editors for types like `Guid`, `FixedString128`, `Vector3`
- **[Send] action:** Passes the working copy to `IDynamicWriter.Write(payload)` which unboxes and calls native `dds_write()`
- **[Send Dispose] action:** Calls `IDynamicWriter.DisposeInstance(payload)`; only enabled for keyed topics

### 10.9 Export & Import

Streaming serialization of samples to/from `.samples` JSON files.

**Export features:**

- Uses `Utf8JsonWriter` coupled to `FileStream` — near-zero memory footprint regardless of dataset size
- Polymorphic type tagging: writes `TopicName` into each JSON record so the importer knows which `Type` to instantiate
- Progress bar showing `exported / total` with cancel support

**Import features:**

- Streaming deserialization via `Utf8JsonReader` / `DeserializeAsyncEnumerable`
- Halts live DDS ingestion to avoid mixing live and historical data
- Clears `SampleStore` and `InstanceStore`
- Pushes deserialized samples into the same `Channel<SampleData>` pipeline that live DDS uses — all filtering, sorting, and UI updates work automatically

### 10.10 Replay Engine

Time-accurate playback of imported samples back onto the DDS network.

```
[Replay Engine]                                                [−][X]
+------------------------------------------------------------------------+
| File: flight_test_04.samples | 14,052 Samples | 00:04:12.400          |
|------------------------------------------------------------------------|
| [▶ Play] [⏸ Pause] [⏭ Step] [⏹ Stop]                                |
|                                                                        |
| Speed: [====|================] 1.0x (Realtime)                         |
|        0.1x                 MAX                                        |
|                                                                        |
| Pause on Sample Ordinal: [5432      ] [Unset]                          |
|------------------------------------------------------------------------|
| Time: 00:01:05.123 [============|-----------------------] (4,102)      |
|------------------------------------------------------------------------|
| [🗗 Open Slaved Grid]                 [🧹 Dispose Sent Instances]      |
+------------------------------------------------------------------------+
```

**Features:**

- **Time-accurate delays:** Background task calculates `(next.Timestamp - current.Timestamp) / PlaybackRate` and awaits that duration
- **Speed slider:** 0.01x to 100x, plus MAX (skips delays entirely)
- **Pause on Ordinal:** Type a sample ID; the engine plays at full speed and pauses the instant it reaches that exact sample — essential for debugging crashes
- **Step:** Advance exactly one sample
- **Slaved Grid:** Spawns a standard `SamplesPanel` bound to the replay buffer instead of live network; supports all filtering/sorting/custom columns
- **Dispose Sent Instances:** Tracks all keyed instances created during replay; button sends DDS Dispose commands for all of them, cleaning up the test network
- **Timeline scrubber:** Drag to jump to any point; bound to `PlaybackEngine.CurrentIndex`

### 10.11 Samples Filter Panel — Tri-State Toggles & Quick Stats

The topic display filter panel uses cycling tri-state buttons:

- **Include (+):** Show only topics matching this criterion (e.g. only received topics)
- **Exclude (−):** Hide topics matching this criterion
- **Ignore (o):** Do not apply this filter

**Quick stats** at the top: `[checkbox icon] 5 / 120` (enabled/total topics), `[filter icon] 2` (topics with active field filters)

---

## 11. Dynamic Form Architecture

### 11.1 The Recursive UI Pattern

Both the Send Sample panel (read-write) and the Sample Detail panel (read-only) use the same recursive component architecture:

1. **`<DynamicObjectEditor>`** — loops over `TopicMetadata.AllFields`; for each field:
   - If primitive or enum → renders `<DynamicPrimitiveEditor>`
   - If collection → renders `<DynamicCollectionEditor>`
   - If nested struct → recursively renders `<DynamicObjectEditor>`
2. **`<DynamicPrimitiveEditor>`** — switches on `TypeCode` to render the correct HTML input (checkbox, number, text, enum dropdown)
3. **`<DynamicCollectionEditor>`** — reads `IList` from the payload, renders per-item editors with Add/Remove controls

### 11.2 Custom Type Drawers

Plugins can register custom Blazor components for specific types:

```csharp
public interface ICustomComponentRegistry
{
    void RegisterEditor<T>(RenderFragment<EditorContext> editorTemplate);
    void RegisterViewer<T>(RenderFragment<ViewerContext> viewerTemplate);
}
```

Example: A plugin registers a `Vector3` viewer that displays `[X, Y, Z]` in a compact format instead of expanding into three separate fields.

---

## 12. Plugin Architecture

### 12.1 Plugin Contract

```csharp
public interface IMonitorPlugin
{
    string Name { get; }
    string Version { get; }
    void ConfigureServices(IServiceCollection services);
    void Initialize(IMonitorContext context);
}

public interface IMonitorContext
{
    IObservable<SampleData> LiveSampleStream { get; }
    ISampleStore HistoricalStore { get; }
    IMenuRegistry MenuRegistry { get; }
    IWindowManager WindowManager { get; }
    IFormatterRegistry FormatterRegistry { get; }
}
```

### 12.2 Plugin Loading

Plugins are compiled as **Razor Class Libraries** (RCLs) and placed in the `plugins/` directory. At startup:

1. `PluginLoader` enumerates `.dll` files in the configured plugin directory
2. Each DLL is loaded into its own `AssemblyLoadContext` (preventing version conflicts)
3. Types implementing `IMonitorPlugin` are discovered via reflection
4. `ConfigureServices()` is called during DI setup (before the host builds)
5. `Initialize()` is called after the host starts, providing access to the data pipeline and UI registry

### 12.3 Plugin Capabilities

Plugins can:

- **Register background services** (e.g. `EntityStore`) into the DI container
- **Register custom UI panels** with the `WindowManager`
- **Register custom formatters** for specific types
- **Register menu items** in the top menu bar
- **Subscribe to the live sample stream** for cross-topic aggregation

---

## 13. Domain Entity Specification (BDC / TKB Plugins)

### 13.1 Concepts & Glossary

| Term | Definition |
|---|---|
| **Entity** | A logical object (e.g. a vehicle) that does not exist as a single DDS message; it is a grouping of multiple DDS messages sharing the same ID |
| **Descriptor** | A specific DDS Topic that makes up part of an Entity (e.g. `GeoSpatial`) |
| **EntityId** | The primary identifier; **always** the first `[DdsKey]` field in any descriptor topic |
| **Master Descriptor** | The specific topic that defines entity existence (BDC: `EntityMaster`, TKB: `TkbMaster`) |
| **Multi-Instance Descriptor** | A descriptor with two `[DdsKey]` fields: Key1 = EntityId, Key2 = PartId (e.g. `ArticulatedPartSpatial` for a tank's turret vs. gun) |

### 13.2 Entity States

| State | Symbol | Rule |
|---|---|---|
| **Alive** | 🟢 | Has a living Master Descriptor |
| **Zombie** | 🟡 | Has living descriptors but Master is missing or disposed |
| **Dead** | ⚫ | Zero descriptors exist for this EntityId |

### 13.3 Aggregation Algorithm

When the core `InstanceStore` fires an `InstanceTransitionEvent`:

1. **Filter:** Does topic namespace match the domain (e.g. `company.BDC.*`)? If not, ignore.
2. **Extract Identity:** Key1 → `EntityId`. If Key2 exists → `PartId`. Create `DescriptorIdentity { TopicType, PartId }`.
3. **Update Entity:**
   - Alive sample → Add/Update in `entity.Descriptors[descrIdent]`
   - Disposed sample → Remove from `entity.Descriptors[descrIdent]`
4. **Evaluate State:**
   - `Descriptors.IsEmpty` → Dead
   - `Descriptors` contains Master topic → Alive
   - Otherwise → Zombie
5. **Journal:** If state changed or descriptor added/removed, append `EntityJournalRecord`

### 13.4 Entity UI — Live Grid (BDC)

```
[BDC Entities]                                                 [−][X]
+------------------------------------------------------------------------+
| [🔍 Filter...] [🪟 Columns] [Toggle: Show Live / Show History]        |
+------------------------------------------------------------------------+
| Entity ID | State  | Info.Name   | Last Update  | Actions              |
|-----------+--------+-------------+--------------+----------------------|
| 1042      | 🟢 ALV | "Alpha Tank" | 14:02:01.123 | [🔍 Detail]         |
| 1043      | 🟡 ZMB | "Bravo Jeep" | 14:02:01.050 | [🔍 Detail]         |
+------------------------------------------------------------------------+
```

- **Virtual Columns:** Can pull values across aggregated descriptors (e.g. `Info.Name` from the `EntityInfo` topic)
- **Live/History toggle:** Switches between current living entities and the chronological `EntityJournal`

### 13.5 Entity UI — Folder Tree (TKB)

```
[TKB Entities]                                                 [−][X]
+------------------------------------------------------------------------+
| [Toggle: Flat / TREE] [Toggle: Show Live / Show History]               |
+------------------------------------------------------------------------+
| v 📁 BlueForce                                                         |
|   v 📁 Ground                                                          |
|       🟢 Tank1 (ID: 1042)                              [🔍 Detail]    |
|       🟢 Jeep2 (ID: 1045)                              [🔍 Detail]    |
|   > 📁 Air                                                             |
| > 📁 RedForce                                                          |
+------------------------------------------------------------------------+
```

- **Tree parsing:** Reads `master.Path` (e.g. `"BlueForce/Ground"`) and `master.Name` to build folder hierarchy
- **Flattened virtualization:** The HTML tree is rendered as flat rows with CSS `padding-left` for indentation, keeping DOM size constant regardless of tree depth

### 13.6 Entity Detail Inspector

```
[Inspector: Entity 1042]                                       [−][X]
+------------------------------------------------------------------------+
| Status: [🟢 ALIVE]     Last Update: 14:02:01.123                      |
| [🗗 Show All Entity Samples]                                           |
|------------------------------------------------------------------------|
| v EntityMaster (Topic)                           [⏱️ Hist] [JSON]     |
|     EntityId: 1042                                                     |
|     SimTime: 4500.2                                                    |
|------------------------------------------------------------------------|
| v GeoSpatial (Topic)                             [⏱️ Hist] [JSON]     |
|     EntityId: 1042                                                     |
|   v Position:                                                          |
|       X: 34.55                                                         |
|       Y: 88.12                                                         |
|------------------------------------------------------------------------|
| > ArticulatedPartSpatial [PartId: 1]             [⏱️ Hist] [JSON]     |
| > ArticulatedPartSpatial [PartId: 2]             [⏱️ Hist] [JSON]     |
+------------------------------------------------------------------------+
```

- **[⏱️ Hist] button:** Spawns a `SamplesPanel` pre-filtered to `Topic == X AND EntityId == Y` since last entity birth
- **[🗗 Show All Entity Samples]:** Spawns a `SamplesPanel` showing ALL descriptors for this entity
- **Multi-instance grouping:** Multiple instances of the same descriptor (different `PartId`) are listed separately

### 13.7 Historical State (Time-Travel)

```
[Historical State: Entity 1042 @ 14:00:00.000]                [−][X]
+------------------------------------------------------------------------+
| ⚠️ YOU ARE VIEWING A FROZEN HISTORICAL SNAPSHOT                        |
|------------------------------------------------------------------------|
| > EntityMaster          (Received at 13:59:59.100)                     |
| > GeoSpatial            (Received at 13:59:59.998)                     |
| > ArtPartSpatial [1]   (Received at 13:58:00.000)                     |
+------------------------------------------------------------------------+
```

**Time-Travel Algorithm:**

1. Input: `TargetEntityId`, `TargetTimestamp T`
2. Find Life Bounds: Query `EntityJournal` for the most recent "Master Added" event before T. If a "Master Removed" event occurred between birth and T, entity was dead — abort.
3. Query SampleStore: For every known descriptor topic:
   - Get the chronological sample list from `SampleStore.GetTopicSamples(topic)`
   - Binary search to find the index closest to T
   - Iterate backwards down to the birth timestamp
   - Find the first sample where `EntityId == TargetEntityId`
   - For multi-instance descriptors, continue searching to find each unique `PartId`
   - If the found sample is a Dispose, that descriptor didn't exist at T — discard it
4. Output: Dictionary of found descriptor samples, passed to the frozen Historical Detail view

---

## 14. Application Lifecycle & Dependency Injection

### 14.1 Startup Sequence

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Configuration
var ddsConfig = builder.Configuration.GetSection("DdsSettings").Get<DdsSettings>();

// 2. Topic Discovery
var topicRegistry = new TopicRegistry();
topicRegistry.ScanDirectory(pluginPath);
builder.Services.AddSingleton<ITopicRegistry>(topicRegistry);

// 3. Plugin Loading
var plugins = PluginLoader.Load(pluginPath);
foreach (var p in plugins) p.ConfigureServices(builder.Services);

// 4. Core Engine (Singletons — shared across all browser tabs)
builder.Services.AddSingleton<IDdsBridge, DdsBridge>();
builder.Services.AddSingleton<ISampleStore, SampleStore>();
builder.Services.AddSingleton<IInstanceStore, InstanceStore>();
builder.Services.AddSingleton<IEventBroker, EventBroker>();

// 5. UI State (Scoped — per browser tab)
builder.Services.AddScoped<IWindowManager, WindowManager>();
builder.Services.AddScoped<IWorkspaceState, WorkspaceState>();

// 6. Background Worker
builder.Services.AddHostedService<DdsIngestionService>();

// 7. Blazor Server
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
var app = builder.Build();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
```

### 14.2 DI Strategy

| Lifetime | Services | Rationale |
|---|---|---|
| **Singleton** | `SampleStore`, `InstanceStore`, `DdsBridge`, `TopicRegistry` | One DDS connection, one data store, shared across all browser tabs |
| **Scoped** | `WindowManager`, `WorkspaceState` | Per-browser-tab panel layout and selection state |

### 14.3 Background Ingestion Service

The `DdsIngestionService` implements `IHostedService`. It runs the main ingestion loop:

```
ExecuteAsync:
    while not cancelled:
        sample = await channel.ReadAsync()
        sampleStore.Append(sample)
        if sample.TopicMeta.IsKeyed:
            instanceStore.ProcessSample(sample)
```

On shutdown (`StopAsync`), it gracefully disposes the `DdsBridge` and disconnects from the DDS network.

---

## 15. UX Enhancement Ideas (Beyond Original Tool)

These are new capabilities enabled by the Blazor/web platform that the original ImGui tool could not provide:

### 15.1 Remote Monitoring
Run the monitor on a headless Linux node (e.g. a drone or robot). Open `http://drone-ip:5000` from any laptop on the network to view live traffic. No software installation required on the viewing machine.

### 15.2 Multi-Monitor via Browser Tabs
Open `localhost:5000` in two browser tabs. Each gets its own `WorkspaceState` (Scoped DI) with independent window layouts, while sharing the same underlying DDS data (Singleton stores). One tab watches `CameraInfo`, the other watches `RobotState`.

### 15.3 Quick-Add Columns from Inspector
When viewing a sample in the Inspector, a small "pin to grid" icon appears next to each field. Clicking it fires an `AddColumnRequestEvent` to the source grid, instantly adding that field as a column — no need to open the Column Picker dialog.

### 15.4 Visual Message Frequency Sparklines
In the Topic Explorer, add a mini sparkline chart next to each topic showing messages-per-second over the last 10 seconds. This gives an at-a-glance view of traffic patterns without opening a full grid.

### 15.5 Unified Search Bar
In addition to the Visual Filter Builder, provide a simple text input at the top of each grid where power users can type Dynamic LINQ expressions directly (e.g. `Payload.Id > 50 and TopicName.Contains("Alert")`).

### 15.6 Dark/Light Theme Toggle
CSS-based theming with a toggle in the top bar. Default to a dark theme (inspired by VS Code) for reduced eye strain during extended monitoring sessions.

---

## 16. Implementation Phases

### Phase 1: Foundation — Headless Engine & Basic UI

**Goal:** Build the data engine and prove it works with a minimal but functional Blazor shell.

- Core engine: Topic Discovery, DdsBridge, DynamicReader/Writer, SampleStore, InstanceStore
- Basic Blazor shell: WindowManager, Topic Explorer, one SamplesPanel, linked Detail view
- Text-based filtering (simple expression input)
- Keyboard navigation (arrow keys, single-click selection)

### Phase 2: Full UI — Grid Power & Detail Polish

**Goal:** Achieve feature parity with the original monitor's data exploration capabilities.

- Visual Filter Builder (recursive AND/OR tree)
- Column Picker with drag-and-drop ordering
- Custom columns with Fasterflect getters
- Expand All mode
- Context menus (right-click actions)
- Hover JSON tooltips
- Text View Panel
- Grid settings export/import

### Phase 3: Tools — Send, Import/Export, Replay

**Goal:** Complete the operational toolkit.

- Send Sample panel (dynamic forms, clone workflow)
- Export (streaming JSON write)
- Import (streaming JSON read)
- Replay Engine (time-accurate, pause-on-ordinal, slaved grid, dispose-sent-instances)

### Phase 4: Plugins — Domain Entity Aggregation

**Goal:** Ship the BDC/TKB Entity tracking as isolated plugins.

- Plugin loading infrastructure (AssemblyLoadContext, IMonitorPlugin)
- BDC Plugin: EntityStore, Entity Grid, Entity Detail Inspector, Time-Travel
- TKB Plugin: Folder Tree view, Path-based hierarchy
- Custom type formatters (Vector3, FixedString, etc.)

### Phase 5: Polish & Enhancement

**Goal:** Ship UX enhancements unique to the web platform.

- Remote monitoring support
- Multi-tab workspace isolation
- Sparkline charts in Topic Explorer
- Dark/Light theme toggle
- Quick-Add columns from Inspector
- Workspace persistence improvements
