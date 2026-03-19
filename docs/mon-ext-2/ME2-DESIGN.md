# ME2 — Monitoring Extensions 2: Design Document

**Workstream:** DDS Monitor Feature Extensions  
**Prefix:** ME2-  
**Status:** Design Phase  
**Last Updated:** 2026-03-18

**Documents:**
- This file: Design reference (read first)
- [ME2-TASK-DETAILS.md](./ME2-TASK-DETAILS.md) — Per-task implementation specs
- [ME2-TASK-TRACKER.md](./ME2-TASK-TRACKER.md) — Progress board

---

## Overview

This workstream delivers fourteen targeted improvements to the DDS Monitor tool and the CycloneDDS code generator. The changes span:

- **Bug fixes** (`DsMonitor.Engine`, `DdsMonitor.Blazor`): forward-compatible workspace type names, reset without losing subscriptions, sort regression in All Samples panel, timestamp formatting
- **Detail panel improvements** (`DdsMonitor.Blazor`): null value visibility, value type syntax highlighting, union rendering in Table and Tree tabs
- **CodeGen quick fix** (`CycloneDDS.CodeGen`): build log now shows which project is being compiled
- **Filter & column system** (`DdsMonitor.Engine`, `DdsMonitor.Blazor`): expose non-payload metadata fields (Topic, InstanceState) to filter builder and column picker; simplify hardcoded column layout
- **Samples panel track mode** (`DdsMonitor.Blazor`): autoscroll to the latest sample with performant sort
- **Topic properties panel** (`DdsMonitor.Blazor`): new non-modal window for topic schema inspection; right-click access from TopicExplorer and TopicSources panels
- **Folder-based assembly scanning** (`DdsMonitor.Engine`, `DdsMonitor.Blazor`): scan a whole folder for topic DLLs; FileDialog supports selecting a directory

All changes align with the current codebase. No existing test infrastructure is broken.

---

## Phase 1 — Bug Fixes

### 1.1 Workspace ComponentTypeName Forward Compatibility (ME2-T01)

**Goal:** Panels saved in `ddsmon.workspace` survive application version upgrades.

**Background:**  
`WindowManager.ResolveComponentTypeName` currently returns `AssemblyQualifiedName` (e.g., `DdsMonitor.Components.TopicExplorerPanel, DdsMonitor, Version=0.2.0.0, ...`). Because version and culture are embedded, a workspace file saved on version 0.2 fails to resolve panels on version 0.3. Only the `FullName` should be stored and compared.

**Current code (WindowManager.cs line 436):**
```csharp
private string ResolveComponentTypeName(string componentTypeName)
{
    if (_panelTypes.TryGetValue(componentTypeName, out var registered))
        return registered.AssemblyQualifiedName ?? registered.FullName ?? componentTypeName;
    var resolved = Type.GetType(componentTypeName);
    return resolved?.AssemblyQualifiedName ?? resolved?.FullName ?? componentTypeName;
}
```

**Changes required:**

1. **`WindowManager.cs`** — `ResolveComponentTypeName` must return `FullName` instead of `AssemblyQualifiedName`. The fix: prefer `registered.FullName`, then `Type.GetType(componentTypeName)?.FullName`.

2. **All panel-spawning callers** — every location that passes `typeof(SomePanel).AssemblyQualifiedName!` must change to `typeof(SomePanel).FullName!`:
   - `MainLayout.razor` — 5 static constant declarations (`TopicExplorerTypeName`, `SamplesPanelTypeName`, `SendSamplePanelTypeName`, `ReplayPanelTypeName`, `TopicSourcesPanelTypeName`)
   - `Desktop.razor` — any panel spawn calls
   - `TopicExplorerPanel.razor` — opens `TopicSourcesPanel`
   - `TopicExplorerPanel.razor` / `TopicSourcesPanel.razor` (after T12) — opens `TopicPropertiesPanel`

> **Note:** Do NOT change `AssemblyQualifiedName` uses for topic-type DLL resolution (in `TopicMetadata.ComponentState`, `WorkspaceLoader`, etc.). External topic DLLs require assembly identity for `Type.GetType()` to work. Only panel component type names are affected by this fix.

---

### 1.2 Reset Does Not Lose Subscriptions (ME2-T02)

**Goal:** The ⏹ Reset button clears sample history but keeps all active DDS readers alive.

**Background:**  
`DdsBridge.ResetAll()` (line 344) currently disposes all readers in `_activeReaders` and `_auxReadersPerParticipant`, fires `ReadersChanged`, and then clears the sample/instance stores. This causes the Subscribe checkboxes in TopicExplorer to show unchecked after a reset; the user must manually re-subscribe.

**Fix:**  
Remove the reader disposal loops, the dictionary `.Clear()` calls, and the `ReadersChanged?.Invoke()` trigger. Only reset the ordinal counter and clear the data stores. The readers remain active and will continue populating incoming samples immediately. The UI stores (`_sampleStore`, `_instanceStore`) fire their own change events to refresh the grids.

---

### 1.3 Ordinal Sort Broken in All Samples (ME2-T03)

**Goal:** The visual sort indicator in the All Samples panel accurately drives sample ordering.

**Background:**  
In `SamplesPanel.EnsureView()` (line 1109), the fixed-topic branch (`TopicMetadata != null`) calls `_viewCache.Sort(...)` after populating the cache, but the all-topics branch (`TopicMetadata == null`) returns early **without** sorting. The visual sort indicator changes (arrow up/down) but the displayed order never changes.

**Fix:**  
Extract the sort call into a shared `ApplySortToViewCache()` helper (see Phase 5 for the full optimized version) and call it from both branches of `EnsureView`.

---

### 1.4 Timestamp Display Formatting (ME2-T04)

**Goal:** All timestamp fields show human-readable local time instead of UTC round-trip or raw nanoseconds.

**Current state:**
- `DetailPanel.RenderSampleInfo` shows `Incoming Timestamp: {sample.Timestamp:O}` (ISO 8601 UTC) and `SourceTimestamp: {info.SourceTimestamp}` (raw nanoseconds since Unix epoch as a long).
- `SamplesPanel` Timestamp column renders `row.Sample.Timestamp.ToString("HH:mm:ss.fff")` — no `.ToLocalTime()`.
- `InstancesPanel` Time column renders `row.Row.Sample.Timestamp.ToString("HH:mm:ss.fff")` — no `.ToLocalTime()`.

**Conversions needed:**
- `.NET DateTime (UTC)` → `.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fffffff")` in Detail pane, `.ToLocalTime().ToString("HH:mm:ss.fff")` in grid cells.
- `SourceTimestamp (long, nanoseconds since 1970-01-01 UTC)` → `DateTime.UnixEpoch.AddTicks(sourceTimestamp / 100).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fffffff")`. Guard: show `"Unknown"` when value is ≤ 0 or equals `long.MaxValue`.

**Files to modify:** `DetailPanel.razor`, `SamplesPanel.razor`, `InstancesPanel.razor`.

---

## Phase 2 — Detail Panel Value Rendering

### 2.1 Null String Visibility + Value Type Syntax Highlighting (ME2-T05)

**Goal:** Null values are visually distinguishable from empty strings; enum, numeric, and boolean values have distinct colours from strings.

**Background:**  
`DetailPanel.RenderValue()` (line 591) has no early `null` check — a null value silently renders as an empty span with the base `detail-tree__value` CSS class. Additionally, the final `else` branch emits `"detail-tree__value"` regardless of whether the type is a number, bool, or enum, even though the existing CSS already defines `is-enum`, `is-number`, `is-bool` etc.

**Changes to `RenderValue`:**
1. Add `if (value == null)` at the top: emit `<span class="detail-tree__value is-null">null</span>`.
2. In the terminal `else` branch: replace the hardcoded `"detail-tree__value"` with `GetValueClass(value.GetType())`. The existing `GetValueClass` method already maps `.IsEnum` → `is-enum`, `.IsPrimitive` → `is-number`, `typeof(bool)` → `is-bool`, etc.
3. Format `bool` as lowercase `"true"` / `"false"` rather than `"True"` / `"False"`.

---

### 2.2 Union Rendering Improvements (ME2-T06)

**Goal:** Union fields in the Table tab are expandable and show the discriminator value; the Tree tab also shows the discriminator value as the collapsed label.

**Background:**  
Currently:  
- **Table tab** (`RenderTableView`, line 217): the outer field rows filter on `!meta.IsSynthetic` but do not consider union arm visibility. A union *list* field renders list items without an expand toggle; union items cannot be opened to see their arm content.
- **Tree tab** (`RenderNode`, line 401): union nodes correctly show only the active arm when expanded (via the inline union branch at line 499). But the "current value" shown in the collapsed node label comes from `RenderValue(value)` which calls `value.ToString()` — showing the union class type name rather than the discriminator value.

**Changes required:**

1. **Extract `GetUnionInfo(object unionObj)` helper** — returns `(object? Discriminator, MemberInfo? ActiveArm, object? ArmValue)`.  
   - Scans members for `[DdsDiscriminatorAttribute]` to find `discValue`.
   - Checks `[DdsCaseAttribute]` arms for an explicit match to `discValue`.
   - Falls back to `[DdsDefaultCaseAttribute]` arm if no explicit match.
   - Returns the discriminator value, the winning arm's `MemberInfo`, and the arm value.

2. **Update `RenderTableView`** — filter top-level fields with `IsUnionArmVisible(meta, payload)` (suppresses inactive union arms at struct level). For list items: when `elemVal` has `[DdsUnionAttribute]`, add a per-item expand toggle, show discriminator as the value, and when expanded show the active arm in a nested row.

3. **Update `RenderNode`** — when the node's type has `[DdsUnionAttribute]`, set `displayValue = GetUnionInfo(value).Discriminator` and `displayType` accordingly. Already correctly renders only the active arm when expanded; no change needed to the expanded-children block.

> The `IsUnionArmVisible` helper can use the `FieldMetadata` union arm properties (`DependentDiscriminatorPath`, `ActiveWhenDiscriminatorValue`, `IsDefaultUnionCase`) that are already populated by `TopicMetadata.AppendFields`.

---

## Phase 3 — CodeGen Quick Fix

### 3.1 Schema Compiler Project Name in Build Log (ME2-T07)

**Goal:** The MSBuild build log identifies which project triggered the code generator.

**Background:**  
`tools/CycloneDDS.CodeGen/CycloneDDS.targets` (line 43) emits:
```xml
<Message Text="Running CycloneDDS Code Generator (Incremental)..." Importance="high" />
```
The code generator runs once per project (whole-project scan), so the message fires once per project but gives no indication of which project.

**Fix:** Inject `$(MSBuildProjectName)`:
```xml
<Message Text="Running CycloneDDS Code Generator (Incremental) for $(MSBuildProjectName)..." Importance="high" />
```

---

## Phase 4 — Filter & Column System

### 4.1 Expose Non-Payload Fields to Filter and Column Picker (ME2-T08)

**Goal:** Users can filter by `Topic` name and `InstanceState` in addition to payload fields; the field picker shows which prefix each field belongs to.

**Background:**  
`TopicMetadata.AppendSyntheticFields` (line 710) already adds `Timestamp` and `Ordinal` as `isWrapperField: true` synthetic fields. The `FilterCompiler.PayloadFieldRegex` pattern only matches `Payload.xxx`; `FilterBuilderPanel.ApplyField` always prefixes with `"Payload."`. Users cannot filter by topic name or instance state.

**Changes required:**

1. **`TopicMetadata.AppendSyntheticFields`** — add two new `isWrapperField: true` entries:
   - `"Topic"` (`typeof(string)`) → `((SampleData)input).TopicMetadata.ShortName`  
   - `"InstanceState"` (`typeof(DdsInstanceState)`) → `((SampleData)input).SampleInfo.InstanceState`

2. **`FilterCompiler.PayloadFieldRegex`** — extend to also match `Sample.xxx`:
   ```
   \b(?:Payload|Sample)\.([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)
   ```

3. **`FilterBuilderPanel.ApplyField`** — choose prefix based on `field.IsWrapperField`:
   - wrapper fields → `"Sample.{field.StructuredName}"`
   - payload fields → `"Payload.{field.StructuredName}"`

4. **`FilterBuilderPanel.GetFieldForCondition`** — strip either `"Payload."` or `"Sample."` prefix before looking up the field by `StructuredName`.

5. **`FieldPicker.razor`** — display `"Sample."` prefix for wrapper fields and `"Payload."` prefix for payload fields in the suggestion list.

6. **`FieldPickerFilter.Matches`** — build `fullPath = (field.IsWrapperField ? "Sample." : "Payload.") + field.StructuredName` and match query against it.

---

### 4.2 "Filter Out Topic" Context Menu (ME2-T09)

**Goal:** A context menu on any sample offers appending a topic exclusion condition to the current filter.

**Changes required:**

1. **`SamplesPanel.razor`** — add `ExcludeTopicFromFilter(string topicName)`:
   - Builds condition: `Sample.Topic != "TopicName"`
   - If filter is empty, sets it as the whole filter; otherwise appends as `(existingFilter) AND <condition>`.
   - Calls `ApplyFilter()`, `SavePanelState()`, `StateHasChanged()`.
   - Update `OpenRowContextMenu` to add a `"Filter Out Topic"` menu item.

2. **`InstancesPanel.razor`** — add the same `ExcludeTopicFromFilter` helper and update `OpenRowContextMenu`.

---

### 4.3 Decouple Hardcoded Columns — Make Metadata Fields Selectable (ME2-T10)

**Goal:** All non-mandatory columns (Topic, Timestamp, Size, Delay) are driven from the column picker, not hardcoded. Only `Ordinal` and `Status` and 'Act' remain fixed. A fresh panel defaults to showing `[Topic, Timestamp, Size [B], Delay [ms]]`.

**Background:**  
`SamplesPanel.RebuildLayoutColumns` unconditionally adds `Timestamp` with `ColumnKind.Timestamp`, `Topic` with `ColumnKind.Topic`, and conditionally `Size`/`Delay`. The `ColumnKind` enum has 8 variants. If the user adds Timestamp from the column picker, the layout gets a duplicate entry (one hardcoded, one from the picker with a `Field` kind), and the `Field` kind entry calls `GetFieldValue` which targets `sample.Payload` for non-synthetics but then fails the topic type check for the all-topics view — resulting in an always-empty column.

**Changes required:**

1. **`ColumnKind` enum** — simplify to 4 variants: `Ordinal`, `Status`, `Field`, `Actions`. Remove `Topic`, `Size`, `Timestamp`, `Delay`.

2. **`RebuildLayoutColumns`** — only hardcode `Ordinal`, `Status`, and `Actions`. Iterate `_selectedColumns` to build `Field` columns.

3. **`InitializeColumns`** — add all synthetic fields (except `Ordinal`) to `_availableColumns`. Default `_selectedColumns` to `["Topic", "Timestamp", "Size [B]", "Delay [ms]"]` when no saved layout exists.

4. **`PopulateAllTopicsAvailableColumns`** — include all synthetic fields (except `Ordinal`) in the pool.

5. **`GetFieldValue`** — target `(object)sample` (not `sample.Payload`) when `field.IsSynthetic`. The existing topic-type check should only execute for non-synthetic fields.

6. **`RenderCellValue`** — the `Topic`, `Size`, `Timestamp`, `Delay` branch cases are removed; all user-selected columns become `ColumnKind.Field` which routes to `GetFieldValue` → `RenderValue`. The `RenderValue` method (and `FormatValue`) handle `DateTime` and numeric formatting.

7. **`FormatValue`** — add a `DateTime` branch: `.ToLocalTime().ToString("HH:mm:ss.fff")`.

8. **Remove** `TimestampField`, `TopicField`, `_delayField`, `_sizeField` static/instance field declarations (they are no longer needed as separate references; the fields are found by name in `_availableColumns`).

---

## Phase 5 — Samples Panel Track Mode

### 5.1 Sort Fix + Autoscroll Track Mode (ME2-T11)

**Goal:** The Ordinal column sort works correctly; track mode auto-scrolls to the latest sample when new data arrives.

**Background:**  
- Sort regression: `EnsureView` all-topics branch returns without sorting (see T03).
- `_trackMode` field exists (default `true`) and `ToggleTrackMode` exists, but there is no logic to call `EnsureSelectionVisibleAsync` when new samples arrive while track mode is active.
- For every incoming sample, calling `_viewCache.Sort(...)` (O(N log N)) is unnecessary: samples arrive in ascending ordinal order, so the view cache is already sorted ascending. Reversing it (O(N)) is sufficient for descending.

**Changes required:**

1. **Extract `ApplySortToViewCache()`** helper:
   - For `OrdinalField` and `TimestampField`: if ascending, do nothing (already sorted); if descending, call `_viewCache.Reverse()`.
   - For other fields: `_viewCache.Sort(CompareSamples)` as before.

2. **Extract `UpdateSelectionAndTracking()`** helper (called at end of `EnsureView`):
   - If `_trackMode`: compute the "latest" index (last item when ascending; first item when descending); update `_selectedIndex`/`_selectedSample`; trigger debounced detail scroll.
   - If not `_trackMode`: verify the previously selected sample index has not shifted; update `_selectedIndex` if needed.

3. **Update `EnsureView`** — call both helpers at the end of every branch.

4. **Update `RefreshCounts`** — after `EnsureView` changes the total, if `_trackMode` is active, schedule `EnsureSelectionVisibleAsync`.

5. **Update `ToggleTrackMode`** — when track mode is turned on, immediately call `UpdateSelectionAndTracking` and scroll to selection.

6. **Update `SelectSample`** — when the user explicitly clicks the "latest" item (last-index ascending or first-index descending), auto-enable `_trackMode`; clicking any other item auto-disables it.

7. **Update `ToggleSort`** — after sorting, if `_trackMode` and `_selectedIndex >= 0`, schedule `EnsureSelectionVisibleAsync`.

---

## Phase 6 — Topic Properties Panel

### 6.1 New TopicPropertiesPanel Component (ME2-T12)

**Goal:** A non-modal "Topic Properties" window shows topic schema details for any selected topic.

**Content to display:**
- DDS topic name (from `TopicMetadata.TopicName`)
- CLR full type name (`TopicMetadata.TopicType.FullName`)
- IDL extensibility kind (from `[DdsExtensibilityAttribute]` on the type, default `"Appendable (Default)"`)
- Plain-C struct size (from `static uint GetDescriptorSize()` method on the type, if present)
- Flat field list table, alphabetically sorted by `StructuredName`, with columns: Field Name, Data Type (full namespace), Key

**Window persistence:**  
Uses standard window-index-based persistence (the same `ComponentState` dictionary pattern used by all panels). On workspace save, stores `TopicMetadata.TopicType.AssemblyQualifiedName`. On workspace restore (when `TopicMetadata` parameter is null), resolves from saved `AssemblyQualifiedName` via `TopicRegistry.GetByType`.

> Note: Unlike panel component type names (T01 above), topic type names in `ComponentState` use `AssemblyQualifiedName` so external DLL types can be resolved with `Type.GetType()`.

**New file:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicPropertiesPanel.razor`

**CSS additions in `app.css`:** `.topic-properties`, `.topic-properties__header`, `.topic-properties__info-grid`, `.topic-properties__label`, `.topic-properties__value`, `.topic-properties__value.is-emphasis`, `.topic-properties__content`, `.topic-properties__type`.

**Spawning:** The window manager assigns width 640 × height 480 on first open. If a hidden instance already exists it is recycled (shown again), matching the window-index-based pattern from `MainLayout`.

---

### 6.2 Topic Explorer Right-Click → Topic Properties (ME2-T13-A)

**Goal:** Right-clicking any row in `TopicExplorerPanel`'s topic table opens the Topic Properties window.

**Current state:** `TopicExplorerPanel.razor` has no `oncontextmenu` handler; rows only have `@ondblclick`.

**Changes required:**
- Inject `ContextMenuService` and `IWindowManager`.
- Add `@oncontextmenu:preventDefault="true"` and `@onmousedown` to the topic `<tr>`.
- Add `HandleRowMouseDown` method (right-click fires a single-item context menu: `"Topic Properties"` → `OpenTopicProperties(topic.Metadata)`).
- Add `OpenTopicProperties(TopicMetadata metadata)` — uses `typeof(TopicPropertiesPanel).FullName!`, standard window-index recycling logic.

---

### 6.3 Topic Sources Panel Improvements (ME2-T13-B)

**Goal:** Topics in the "Topics in selected assembly" table are sorted alphabetically; namespace shown as a grayed second line; same "Topic Properties" context menu available.

**Changes required:**
- Inject `IWindowManager` and `ContextMenuService`.
- `RefreshSelectedTopics` — sort by `TopicName` (case-insensitive).
- Table `<tbody>` row `<td>` for CLR Type — add two-line layout: `<div class="topic-explorer__name">@topic.TopicType.Name</div>` + `<div class="topic-explorer__namespace">@topic.TopicType.Namespace</div>`. Reuses existing `topic-explorer__namespace` CSS class.
- Add `@oncontextmenu:preventDefault="true"` and `@onmousedown` to `<tr>`; add `HandleRowMouseDown` + `OpenTopicProperties` (same as T13-A).

---

## Phase 7 — Folder-Based Assembly Scanning

### 7.1 TopicMetadata AssemblyPath + ScanEntry Folder Support (ME2-T14)

**Goal:** A topic source entry can point to a folder; the system auto-scans all `.dll` / `.exe` files in that folder. Each discovered topic displays its originating assembly file path.

**Changes required:**

1. **`TopicMetadata.cs`** — add `public string AssemblyPath { get; }` property, populated in the constructor from `topicType.Assembly.Location`.

2. **`AssemblySourceService.ScanEntry`** — check `Directory.Exists(entry.Path)`:
   - If directory: enumerate `*.dll` and `*.exe` (top-level only, `SearchOption.TopDirectoryOnly`), call `_discoveryService.DiscoverFromFileDetailed(file)` for each, ignoring per-file exceptions.
   - If file: existing single-file path (unchanged).
   - If neither: throw `FileNotFoundException`.

3. **`TopicSourcesPanel.razor`** — update the CLR Type `<td>` to include a third line: `<div class="topic-explorer__namespace" title="@topic.AssemblyPath">@topic.AssemblyPath</div>`. Also update the Add-button tooltip to `"Add folder or DLL assembly"`.

4. **`FileDialog.razor`** — allow confirming with an empty filename (returns the current directory):
   - `CanConfirm` in Open mode → always `true` (blank = select current folder).
   - `ConfirmAsync`: if path is blank in Open mode, set `path = _currentDir`.
   - `File.Exists` check is still applied when a non-empty path is provided in Open mode, but `Directory.Exists` is also allowed.
   - Updated placeholder text: `"Select a file or leave empty for current folder…"` (Open mode).

---

## Cross-Cutting Concerns

### CSS Variables
All new styling uses existing CSS variables (`--bg-2`, `--panel-border`, `--panel-muted`, `--panel-text`, `--accent-2`, `--mono-font`) to ensure consistent light/dark theme support.

### Workspace Compatibility
- T01 ensures panel type names saved in workspace files are version-independent.
- Topic type names for external DLLs (e.g., `ComponentState["TopicMetadata"]`) continue to use `AssemblyQualifiedName` so `Type.GetType()` can resolve them across application restarts.

### FilterCompiler Scope
The `Sample.` prefix (T08) is decoded identically to `Payload.` within the expression evaluator — both ultimately call `field.Getter(sampleData)`. The regex change only needs to capture the field name after the prefix; the getter already targets the right source object (`SampleData` for wrapper fields, `SampleData.Payload` for payload fields).

### Test Implications
Most changes are UI/Blazor rendering changes with no dedicated unit test targets. However:
- **T02** (`ResetAll`) should be covered by an `DdsBridge` unit test verifying readers are still active after `ResetAll`.
- **T08** (`FilterCompiler` regex change) should be covered by a `FilterCompiler` unit test for `Sample.Topic` expressions.
- **T14** (`ScanEntry` folder support) should be covered by an `AssemblySourceService` unit test for directory scanning.
