# ME2 — Task Details

**Reference Design:** See [ME2-DESIGN.md](./ME2-DESIGN.md) for architecture and rationale.  
**Tracker:** See [ME2-TASK-TRACKER.md](./ME2-TASK-TRACKER.md) for current status.

> Each task below is self-contained. Read the referenced design chapter first, then this file — together they give complete implementation instructions.

---

## ME2-T01 — Workspace ComponentTypeName Forward Compatibility

**Design ref:** [Phase 1.1 — Workspace ComponentTypeName Forward Compatibility](./ME2-DESIGN.md#11-workspace-componenttypename-forward-compatibility-me2-t01)  
**Scope:** `DdsMonitor.Engine` (WindowManager), `DdsMonitor.Blazor` (several components)

### Description

Panel type names stored in `ddsmon.workspace` currently include the full assembly identity (version, culture, public key token). This means a workspace file saved on version 0.2 breaks on version 0.3. The fix is to store only the `FullName` (e.g., `DdsMonitor.Components.TopicExplorerPanel`).

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Engine/WindowManager.cs` | `ResolveComponentTypeName`: return `registered.FullName` instead of `registered.AssemblyQualifiedName`; fall back to `resolved?.FullName` |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/Layout/MainLayout.razor` | Change 5 static constants from `typeof(...).AssemblyQualifiedName!` to `typeof(...).FullName!` |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/Desktop.razor` | Same change: `.FullName!` wherever `.AssemblyQualifiedName!` is used for panel spawning |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicExplorerPanel.razor` | Change `typeof(TopicSourcesPanel).AssemblyQualifiedName!` to `.FullName!` |

When task ME2-T12 (TopicPropertiesPanel) is implemented, its spawning calls must also use `.FullName!`.

### Success Conditions

1. **Backward-compat test:** In `DdsMonitor.Engine.Tests`, parse a workspace JSON file containing `ComponentTypeName: "DdsMonitor.Components.TopicExplorerPanel, DdsMonitor, Version=0.1.0.0, ..."`. After calling `WindowManager.ResolveComponentTypeName`, assert the result equals `"DdsMonitor.Components.TopicExplorerPanel"` (FullName, no version).
2. **Forward-compat test:** Parse a workspace JSON with just `ComponentTypeName: "DdsMonitor.Components.TopicExplorerPanel"`. Assert the resolved value is `"DdsMonitor.Components.TopicExplorerPanel"`.
3. **No regression:** All existing `DdsMonitor.Engine.Tests` must pass.
4. **Manual verification:** Save and reopen a workspace; all panels restore to their saved positions and sizes.

---

## ME2-T02 — Reset Does Not Lose Subscriptions

**Design ref:** [Phase 1.2 — Reset Does Not Lose Subscriptions](./ME2-DESIGN.md#12-reset-does-not-lose-subscriptions-me2-t02)  
**Scope:** `DdsMonitor.Engine` (DdsBridge)

### Description

`DdsBridge.ResetAll()` currently disposes all active and auxiliary DDS readers, causing the Subscribe checkboxes in TopicExplorer to appear unchecked after a reset. The user then has to manually re-subscribe to every topic.

The fix: retain all readers. Only reset the ordinal counter and clear the sample/instance data stores.

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Engine/DdsBridge.cs` | Remove reader disposal loops and `ReadersChanged?.Invoke()` from `ResetAll()`. Keep `_ordinalCounter.Reset()`, `_sampleStore?.Clear()`, `_instanceStore?.Clear()` |

### Success Conditions

1. **Unit test** (`DdsMonitor.Engine.Tests`): After calling `DdsBridge.ResetAll()` with several active readers, assert that `_activeReaders.Count` is unchanged (readers still present), `_sampleStore.AllSamples.Count == 0`, and `_instanceStore` is empty.
2. **Unit test**: Assert that `ReadersChanged` event is **not** fired after `ResetAll()`. (Optionally: subscribe to the event and assert the handler is never called.)
3. **Unit test**: Assert that after `ResetAll()`, the ordinal for the next ingested sample starts from 0 (or 1, depending on counter reset semantics).
4. **No regression**: All existing `DdsMonitor.Engine.Tests` pass.

---

## ME2-T03 — Ordinal Sort Broken in All Samples

**Design ref:** [Phase 1.3 — Ordinal Sort Broken in All Samples](./ME2-DESIGN.md#13-ordinal-sort-broken-in-all-samples-me2-t03)  
**Scope:** `DdsMonitor.Blazor` (SamplesPanel)

> **Note:** This task overlaps with ME2-T11. If ME2-T11 is implemented first, ME2-T03 is automatically resolved. These tasks can be done together or sequentially; T03 is the minimal standalone fix.

### Description

In `SamplesPanel.EnsureView()`, the all-topics branch (`TopicMetadata == null`) returns after populating `_viewCache` without calling the sort. The single-topic branch does sort. The standalone fix is to ensure the sort call runs in both branches.

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor` | In `EnsureView()`, add `ApplySortToViewCache()` call in the all-topics (`TopicMetadata == null`) branch before returning (see ME2-T11 for the full optimized implementation) |

### Success Conditions

1. Open the All Samples panel (`TopicMetadata == null`).
2. Click the `Ordinal` column header once (ascending sort indicator active).
3. Click again (descending sort indicator active) — assert the most-recently received sample appears at the top of the visible list.
4. Click again (ascending) — assert the oldest sample is at top.
5. **No regression**: Sort works identically in the single-topic SamplesPanel view (TopicMetadata != null).

---

## ME2-T04 — Timestamp Display Formatting

**Design ref:** [Phase 1.4 — Timestamp Display Formatting](./ME2-DESIGN.md#14-timestamp-display-formatting-me2-t04)  
**Scope:** `DdsMonitor.Blazor` (DetailPanel, SamplesPanel, InstancesPanel)

### Description

Three panels display timestamps in UTC or unformatted nanoseconds. All must show local-time in a readable format.

### Files to modify

| File | Location | Change |
|---|---|---|
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor` | `RenderSampleInfo()` | `Timestamp` → `.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fffffff")`; `SourceTimestamp` → `DateTime.UnixEpoch.AddTicks(ts / 100).ToLocalTime().ToString(...)` with guard for ≤0 or `long.MaxValue` |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor` | `RenderCellValue` `ColumnKind.Timestamp` (or `FormatValue` after T10) | Add `.ToLocalTime()` |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/InstancesPanel.razor` | `RenderCellValue` `ColumnKind.Time` | Add `.ToLocalTime()` |

> If ME2-T10 is implemented first, the `FormatValue` static method in `SamplesPanel` must handle `DateTime` with `.ToLocalTime()`. The `InstancesPanel` cell renderer is always separate.

### Success Conditions

1. In the Detail panel's Sample Info tab, `Incoming Timestamp` shows the format `"2026-03-18 09:39:35.4147021"` in local time.
2. `SourceTimestamp` in the Detail panel's Sample Info tab shows the same readable format (or `"Unknown"` for unset values).
3. In the All Samples / Samples panel, the Timestamp cell shows `"HH:mm:ss.fff"` in local time.
4. In the Instances panel, the Time cell shows `"HH:mm:ss.fff"` in local time.
5. The system clock test: receive a sample, record the local time, assert the displayed timestamp is within 2 seconds of that recorded time.

---

## ME2-T05 — Null String Visibility + Value Type Syntax Highlighting

**Design ref:** [Phase 2.1 — Null String Visibility + Value Type Syntax Highlighting](./ME2-DESIGN.md#21-null-string-visibility--value-type-syntax-highlighting-me2-t05)  
**Scope:** `DdsMonitor.Blazor` (DetailPanel)

### Description

The `RenderValue` method in `DetailPanel.razor` has two issues:
1. `null` values render as invisible empty text.
2. Enum, bool, and numeric scalars all emit the generic `"detail-tree__value"` CSS class, losing their type-specific colour CSS rules.

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor` | Update `RenderValue`: add `null` guard at top (renders `<span class="detail-tree__value is-null">null</span>`); in the `else` branch replace hardcoded `"detail-tree__value"` with `GetValueClass(value.GetType())`; format `bool` as `"true"/"false"` (lowercase) |

### Success Conditions

1. In the Detail Tree or Table, a `null` string field displays the literal text `"null"` in a distinct color (mapped from the `is-null` CSS class, expected color `#f28ba8` per existing CSS).
2. An `""` (empty string) field displays as an empty string — visually different from `null`.
3. An `enum` field displays in the `is-enum` color (different from string and number colors).
4. A `bool` field displays `"true"` or `"false"` (lowercase) in the `is-bool` color.
5. A numeric field (`int`, `float`, etc.) displays in the `is-number` color.
6. No existing rendering behavior for string, array, list, or fixed-buffer fields is changed.

---

## ME2-T06 — Union Rendering Improvements

**Design ref:** [Phase 2.2 — Union Rendering Improvements](./ME2-DESIGN.md#22-union-rendering-improvements-me2-t06)  
**Scope:** `DdsMonitor.Blazor` (DetailPanel)

### Description

Two issues with union field display:
1. **Table tab**: a `List<UnionType>` shows items but items cannot be expanded to see the active arm. The item value shows the union type name instead of the discriminator.
2. **Tree tab**: a collapsed union node label shows the union class type name instead of the discriminator value.

The `IsUnionArmVisible` helper for top-level table filtering can leverage the existing `FieldMetadata` union properties (`DependentDiscriminatorPath`, `ActiveWhenDiscriminatorValue`, `IsDefaultUnionCase`).

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor` | Add `GetUnionInfo()` helper; add `IsUnionArmVisible()` helper; update `RenderTableView` to filter inactive union arms and add expand-toggle for union list items; update `RenderNode` to show discriminator as display value |

### Implementation notes

**`GetUnionInfo(object unionObj)`:**
- Use standard reflection on `unionObj.GetType()`.
- Find `[DdsDiscriminatorAttribute]` member → read `discValue`.
- Find `[DdsCaseAttribute]` member with matching `caseAttr.Value` (via `UnionValuesEqualTree`).
- If none matched, find `[DdsDefaultCaseAttribute]` member.
- Return `(discValue, activeArm MemberInfo, armValue)`.

**`IsUnionArmVisible(FieldMetadata field, object payload)`:**
- Returns `true` for discriminator fields.
- Returns `true` for non-union fields (no `DependentDiscriminatorPath`).
- For arms: reads the current discriminator value via the discriminator field's getter; checks `ActiveWhenDiscriminatorValue` or `IsDefaultUnionCase` logic.

**`RenderNode` change:**
- Before computing `displayValue`, check `isUnion && value != null`.
- If so: `var uInfo = GetUnionInfo(value); displayValue = uInfo.Discriminator;`
- The expanded-children block already handles the active-arm-only rendering via the inline union branch (no change needed there).

**`RenderTableView` list-item change:**
- For each element `elemVal` in the expanded array: check `elemVal?.GetType().GetCustomAttribute<DdsUnionAttribute>() != null`.
- If union element: add expand toggle button to the `[i]` name cell (using `ToggleTableField(elemName)`); show `GetUnionInfo(elemVal).Discriminator` as value via `RenderValue`; when `isElemExpanded`, render a nested row with the active arm's name and value.

### Success Conditions

1. In the Detail Tree tab, a collapsed union node shows the discriminator value as its label (not the class type name).
2. In the Detail Tree tab, expanding a union node shows only the active arm's name/value (as before).
3. In the Detail Table tab, a top-level union struct field: its row value shows the discriminator, inactive arms are hidden.
4. In the Detail Table tab, a `List<UnionType>` field: each list item `[i]` row shows the discriminator value and has an expand toggle arrow.
5. Expanding a union list item row shows a nested row with the active arm's field name and value.
6. Collapsing the expand toggle hides the nested arm row.
7. No regression in rendering of non-union struct, array, or list fields.

---

## ME2-T07 — Schema Compiler Project Name in Build Log

**Design ref:** [Phase 3.1 — Schema Compiler Project Name in Build Log](./ME2-DESIGN.md#31-schema-compiler-project-name-in-build-log-me2-t07)  
**Scope:** `CycloneDDS.CodeGen` (MSBuild targets file)

### Description

The MSBuild `<Message>` in the `CycloneDDSCodeGen` target shows no project context. A developer compiling a large solution sees the same message repeated with no way to tell which project is currently being processed.

### Files to modify

| File | Change |
|---|---|
| `tools/CycloneDDS.CodeGen/CycloneDDS.targets` | Line ~43: change `Text` attribute to include `$(MSBuildProjectName)` |

### Success Conditions

1. Build a solution containing at least two projects that use the CycloneDDS code generator (e.g., `FeatureDemo` and another IDL-containing project).
2. In the build output (normal verbosity), the message `Running CycloneDDS Code Generator (Incremental) for FeatureDemo...` appears for the `FeatureDemo` project specifically.
3. A separate message appears for each project that triggers the generator.
4. Changing the `Text` must not break the incremental build stamp logic or any existing code generation tests.

---

## ME2-T08 — Expose Non-Payload Fields to Filter and Column Picker

**Design ref:** [Phase 4.1 — Expose Non-Payload Fields to Filter and Column Picker](./ME2-DESIGN.md#41-expose-non-payload-fields-to-filter-and-column-picker-me2-t08)  
**Scope:** `DdsMonitor.Engine` (TopicMetadata, FilterCompiler, FieldPickerFilter), `DdsMonitor.Blazor` (FilterBuilderPanel, FieldPicker)

### Description

Users cannot filter by topic name or instance state because these are not exposed as filterable fields. Adding `Topic` and `InstanceState` as synthetic wrapper fields and updating the filter infrastructure to support the `Sample.` prefix enables this.

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs` | `AppendSyntheticFields`: add `Topic` (string, wrapper) and `InstanceState` (DdsInstanceState, wrapper) |
| `tools/DdsMonitor/DdsMonitor.Engine/FilterCompiler.cs` | `PayloadFieldRegex`: change to also match `\bSample\.` in addition to `\bPayload\.` |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/FilterBuilderPanel.razor` | `ApplyField`: use `"Sample."` prefix for wrapper fields; `GetFieldForCondition`: strip `"Sample."` prefix |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/FieldPicker.razor` | Show `"Sample."` prefix for wrapper fields, `"Payload."` for others |
| `tools/DdsMonitor/DdsMonitor.Engine/Ui/FieldPickerFilter.cs` | `Matches`: build `fullPath` with prefix and match against it |

### Success Conditions

1. **Unit test** (`DdsMonitor.Engine.Tests`, `FilterCompiler`): compile the expression `Sample.Topic == "NavStatus"`. Assert `IsValid == true` and the predicate returns `true` for a `SampleData` whose `TopicMetadata.ShortName == "NavStatus"`, and `false` for one with a different name.
2. **Unit test**: compile `Sample.InstanceState == Alive`. Assert predicate evaluates correctly.
3. **Unit test**: compile the existing `Payload.Field1 > 5` expression — assert it still works after the regex change.
4. In the FilterBuilderPanel UI, opening the field picker shows items prefixed with `"Sample."` (for Topic, InstanceState, Timestamp, Ordinal) and `"Payload."` (for all payload fields).
5. Typing `"Sample."` in the field picker search box filters to only wrapper fields.
6. Typing `"Payload."` filters to only payload fields.
7. No regression in existing filter expressions.

---

## ME2-T09 — "Filter Out Topic" Context Menu

**Design ref:** [Phase 4.2 — "Filter Out Topic" Context Menu](./ME2-DESIGN.md#42-filter-out-topic-context-menu-me2-t09)  
**Scope:** `DdsMonitor.Blazor` (SamplesPanel, InstancesPanel)

### Description

A single context menu item on a sample row offers instantly adding a `Sample.Topic != "TopicName"` exclusion to the current filter. This enables quick filtering out of high-frequency noisy topics without having to manually edit the filter expression.

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor` | Add `ExcludeTopicFromFilter(string topicName)` helper; update `OpenRowContextMenu` to include `"Filter Out Topic"` item |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/InstancesPanel.razor` | Same helper + updated `OpenRowContextMenu` |

### Success Conditions

1. In the All Samples panel, right-click any row — the context menu shows a `"Filter Out Topic"` option.
2. Clicking it sets the filter to `Sample.Topic != "TopicName"` (or appends `AND Sample.Topic != "TopicName"` to an existing filter).
3. The filter takes effect immediately — samples of that topic disappear from the view.
4. After filtering out, right-clicking a different topic's row and selecting "Filter Out Topic" compounds the filter correctly: both topics are excluded.
5. Same behavior in the Instances panel.
6. Existing context menu items (`"Show Detail"`, `"Clone to Send/Emulator"`) still function.

---

## ME2-T10 — Decouple Hardcoded Columns — Make Metadata Fields Selectable

**Design ref:** [Phase 4.3 — Decouple Hardcoded Columns — Make Metadata Fields Selectable](./ME2-DESIGN.md#43-decouple-hardcoded-columns--make-metadata-fields-selectable-me2-t10)  
**Scope:** `DdsMonitor.Blazor` (SamplesPanel)

### Description

`Timestamp`, `Topic`, `Size [B]`, and `Delay [ms]` are hardcoded in `RebuildLayoutColumns`. If the user opens the column picker and selects `Timestamp`, a duplicate column appears and it is always empty. The fix decouples all except `Ordinal` and `Status` from hardcoding and routes them through the standard column-picker mechanism.

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor` | Simplify `ColumnKind` enum; update `RebuildLayoutColumns`, `InitializeColumns`, `PopulateAllTopicsAvailableColumns`, `GetFieldValue`, `RenderCellValue`, `FormatValue`; remove orphaned static field declarations |

### Key implementation points

- **`ColumnKind` enum**: keep `Ordinal`, `Status`, `Field`, `Actions` only.
- **`GetFieldValue`**: `field.IsSynthetic` → target is `(object)sample`; skip topic-type check for synthetic fields.
- **`FormatValue`**: add `if (value is DateTime dt) return dt.ToLocalTime().ToString("HH:mm:ss.fff");` branch.
- **Default columns** (when no saved layout): `["Topic", "Timestamp", "Size [B]", "Delay [ms]"]` — these names must match the `StructuredName` values of the synthetic fields added in `AppendSyntheticFields`.
- **Remove**: `TimestampField`, `TopicField`, `_delayField`, `_sizeField` as named references; these fields are now found by name in `_availableColumns`.

### Success Conditions

1. Fresh All Samples panel (no saved state) shows columns: Ordinal, Status, Topic, Timestamp, Size, Delay, Actions.
2. Opening the column picker shows Topic, Timestamp, Size [B], Delay [ms] as already selected; other synthetic and payload fields are available for selection.
3. Removing `Timestamp` from selected columns removes it from the grid.
4. Adding `Timestamp` back shows the correctly formatted local-time timestamp — no empty column.
5. Adding a `Payload.SomeField` column in All Samples mode does not produce empty cells for topics that do not have that field; it shows empty/null values instead (not an error).
6. After saving and restoring workspace, the column selection is preserved.
7. `ColumnKind` enum has exactly 4 variants.
8. No regression in single-topic mode column selection.

---

## ME2-T11 — Sort Fix + Autoscroll Track Mode

**Design ref:** [Phase 5.1 — Sort Fix + Autoscroll Track Mode](./ME2-DESIGN.md#51-sort-fix--autoscroll-track-mode-me2-t11)  
**Scope:** `DdsMonitor.Blazor` (SamplesPanel)

> This task is a superset of ME2-T03 (sort fix). Implementing T11 resolves T03 automatically.

### Description

- Fix the sort regression in the all-topics branch (see T03).
- Add O(N) fast path for Ordinal/Timestamp sort (already-ordered data, just reverse for descending).
- When `_trackMode` is active, auto-select the latest sample on every refresh and scroll the virtualized list to keep it visible.
- When the user manually clicks the "latest" sample, auto-enable track mode; clicking any other sample disables it.

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor` | New helpers `ApplySortToViewCache()` and `UpdateSelectionAndTracking()`; update `EnsureView`, `RefreshCounts`, `ToggleSort`, `ToggleTrackMode`, `SelectSample` |

### Key implementation points

**`ApplySortToViewCache()`:**
```
if _sortField is OrdinalField or TimestampField:
    if descending: _viewCache.Reverse()  // O(N)
    else: nothing                         // already sorted ascending
else:
    _viewCache.Sort(...)                  // general O(N log N)
```

**`UpdateSelectionAndTracking()`:**
```
if (_trackMode):
    int latestIndex = (_sortDirection == Ascending) ? _viewCache.Count - 1 : 0
    select _viewCache[latestIndex]
    trigger debounced scroll (via _trackDebouncer)
else:
    verify _selectedSample is still at _selectedIndex; fix index if shifted
```

**`SelectSample` track-mode logic:**
```
bool isLatest = ascending ? (row.Index == _viewCache.Count-1) : (row.Index == 0)
_trackMode = isLatest
```

### Success Conditions

1. In All Samples panel, sort descending by Ordinal — newest sample appears at the top.
2. Enable track mode (eye icon) and wait for new samples — the selection automatically moves to the newest sample and the list scrolls to keep it visible.
3. With track mode on, manually click a non-latest sample — track mode turns off; new samples arrive but selection stays on the clicked sample.
4. Click the latest (top-most, in descending mode) sample — track mode turns back on automatically.
5. With 10,000+ samples in the view, toggling ascending ↔ descending sort does not cause noticeable UI lag (the O(N) reverse path is used).
6. When track mode is off, the previously selected sample remains selected after new samples arrive (selection index tracks the sample object, not the position).
7. No regression in single-topic mode sort and track mode.

---

## ME2-T12 — New TopicPropertiesPanel Component

**Design ref:** [Phase 6.1 — New TopicPropertiesPanel Component](./ME2-DESIGN.md#61-new-topicpropertiespanel-component-me2-t12)  
**Scope:** `DdsMonitor.Blazor` (new component, app.css)

### Description

A new non-modal panel component that shows a topic's DDS name, CLR type info, extensibility kind, plain-C struct size, and an alphabetically sorted flat field list with Field Name / Data Type / Key columns.

### Files to create/modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicPropertiesPanel.razor` | **New file** — full component (HTML + @code block) |
| `tools/DdsMonitor/DdsMonitor.Blazor/wwwroot/app.css` | Append CSS rules for `.topic-properties`, `.topic-properties__header`, `.topic-properties__info-grid`, `.topic-properties__label`, `.topic-properties__value`, `.topic-properties__value.is-emphasis`, `.topic-properties__content`, `.topic-properties__type` |

### Component structure

**`@code` block:**
- `[Parameter] TopicMetadata? TopicMetadata` — populated by caller when spawning.
- `[CascadingParameter] PanelState? PanelState` — used for workspace persistence.
- `OnParametersSet()`: if `TopicMetadata != null`, persist `TopicMetadata.TopicType.AssemblyQualifiedName` in `PanelState.ComponentState`; if `TopicMetadata == null` and PanelState has the key, restore by calling `TopicRegistry.GetByType(Type.GetType(savedName))`.
- `GetSortedFields()` — `TopicMetadata.AllFields.Where(f => !f.IsSynthetic).OrderBy(f => f.StructuredName, OrdinalIgnoreCase)`.
- `IsKey(field)` — `TopicMetadata.KeyFields.Contains(field)`.
- `GetPlainCSize(Type)` — reflect `static uint GetDescriptorSize()` method on the type; return `null` if absent.
- `GetExtensibility(Type)` — reflect `[DdsExtensibilityAttribute]`; return its `Kind.ToString()` or `"Appendable (Default)"`.
- `GetFriendlyTypeName(Type)` — `List<T>` → `"List<T.FullName>"`, array → `"T.FullName[]"`, else `FullName`.

**Spawning (implemented in T13-A and T13-B callers):**
```csharp
var typeName = typeof(TopicPropertiesPanel).FullName!;
// Recycle hidden window or spawn new one at 640×480
```

### Success Conditions

1. Right-clicking a topic row (after T13 is done wiring it up) opens a `TopicPropertiesPanel` window.
2. The window shows the correct DDS topic name, CLR full type name, extensibility, and size.
3. The field table is sorted alphabetically by field name.
4. Key fields are marked with the key indicator.
5. Closing and reopening the workspace restores the window to its saved position/size and the same topic is displayed (resolved from `AssemblyQualifiedName`).
6. Multiple `TopicPropertiesPanel` windows can be open simultaneously for different topics.
7. A hidden window is recycled (brought back to front) rather than a duplicate being spawned.

---

## ME2-T13-A — Topic Explorer Right-Click → Topic Properties

**Design ref:** [Phase 6.2 — Topic Explorer Right-Click → Topic Properties](./ME2-DESIGN.md#62-topic-explorer-right-click--topic-properties-me2-t13-a)  
**Scope:** `DdsMonitor.Blazor` (TopicExplorerPanel)

### Description

Add a right-click context menu to topic rows in `TopicExplorerPanel` that opens the `TopicPropertiesPanel`.

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicExplorerPanel.razor` | Add `@inject IWindowManager WindowManager` and `@inject ContextMenuService ContextMenuService`; add `@oncontextmenu:preventDefault` and `@onmousedown` to topic `<tr>`; add `HandleRowMouseDown` and `OpenTopicProperties` methods |

### Success Conditions

1. Right-clicking a topic row in TopicExplorer shows a context menu with `"Topic Properties"`.
2. Selecting the item opens a `TopicPropertiesPanel` window (see T12 success conditions).
3. Left-click double-click still works (opens Samples panel).
4. No regression for other TopicExplorerPanel interactions.

---

## ME2-T13-B — Topic Sources Panel Improvements

**Design ref:** [Phase 6.3 — Topic Sources Panel Improvements](./ME2-DESIGN.md#63-topic-sources-panel-improvements-me2-t13-b)  
**Scope:** `DdsMonitor.Blazor` (TopicSourcesPanel)

### Description

Sort topics alphabetically in the "Topics in selected assembly" table; show namespace as a grayed second line; add right-click "Topic Properties" context menu (same as T13-A).

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicSourcesPanel.razor` | Add service injections; update `RefreshSelectedTopics` to sort; update CLR Type `<td>` to two-line layout; add `HandleRowMouseDown` + `OpenTopicProperties` methods |

### Success Conditions

1. "Topics in selected assembly" list is sorted alphabetically by topic name (case-insensitive).
2. CLR Type column shows the class name on line one and the namespace (grayed) on line two.
3. Right-clicking a topic row shows `"Topic Properties"` context menu item → opens `TopicPropertiesPanel`.
4. No regression in existing assembly-source management (add/remove entries, scanning).

---

## ME2-T14 — Folder-Based Assembly Scanning

**Design ref:** [Phase 7.1 — TopicMetadata AssemblyPath + ScanEntry Folder Support](./ME2-DESIGN.md#71-topicmetadata-assemblypath--scanentry-folder-support-me2-t14)  
**Scope:** `DdsMonitor.Engine` (TopicMetadata, AssemblySourceService), `DdsMonitor.Blazor` (TopicSourcesPanel, FileDialog)

### Description

An assembly source entry can now point to a folder path. The service enumerates all `.dll` and `.exe` files in that folder and silently skips files that fail to load. Topics now expose `AssemblyPath` so the UI can show which file each topic came from.

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs` | Add `public string AssemblyPath { get; }` property; initialize from `topicType.Assembly.Location` in constructor |
| `tools/DdsMonitor/DdsMonitor.Engine/AssemblyScanner/AssemblySourceService.cs` | `ScanEntry`: if `Directory.Exists(entry.Path)` enumerate `*.dll`/`*.exe` (top-level); if `File.Exists(entry.Path)` use existing single-file path; else throw `FileNotFoundException` |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicSourcesPanel.razor` | Third `<div>` in CLR Type `<td>` showing `topic.AssemblyPath` (grayed, with `title` tooltip for full path); update Add button `title` text |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/FileDialog.razor` | Open mode: `CanConfirm` always `true`; `ConfirmAsync` accepts empty path as current directory; accept `Directory.Exists` as valid in Open mode; updated placeholder text |

### Success Conditions

1. **Unit test** (`DdsMonitor.Engine.Tests`): Create a temporary directory with 2 mock `*.dll` containing known topic types. Call `AssemblySourceService.ScanEntry` with the directory path. Assert that topics from both DLLs are returned.
2. **Unit test**: A non-loadable DLL in the folder does not throw; the other DLLs are still scanned successfully.
3. Adding a folder path via the Topic Sources panel's Add dialog causes all discovered topics to appear in the list.
4. Topics discovered from a folder scan show three lines in the CLR Type column: class name, namespace, assembly file path.
5. In `TopicMetadata`, `AssemblyPath` returns the correct `.dll` file path.
6. `FileDialog` in Open mode allows clicking OK with an empty filename — the result is the current browsed directory path.
7. Existing single-file assembly source entries (from previous workspaces) continue to work unchanged.
