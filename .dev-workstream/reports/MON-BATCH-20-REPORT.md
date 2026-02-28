# MON-BATCH-20 Report: Final Polish, Main Menu Redesign & Detail Persistence

**Batch:** MON-BATCH-20  
**Status:** ✅ COMPLETE  
**All tests pass:** 88/88  

---

## Tasks Completed

### Task 1 ✅ — Sample Detail Window Persistence

**Root Cause:**  
`DetailPanel` was not in the `IsHideOnClosePanel` guard in `Desktop.razor`. When a user pressed the panel's `x` button, `WindowManager.ClosePanel()` was called, which physically removed the panel from the active list. On next double-click, a brand-new panel was spawned with default X/Y/Width/Height.

**Fix:**  
Added `|| panel.ComponentTypeName.Contains("DetailPanel", StringComparison.Ordinal)` to `IsHideOnClosePanel()` in `Desktop.razor`. 

Now pressing `x` on a Detail panel sets `IsHidden = true` and keeps the `PanelState` (including X, Y, Width, Height) in the `WindowManager`'s active list. The workspace is saved immediately. On next `OpenDetail` call, `FindReusableDetailPanel` (which finds the first hidden Detail panel ordered by index) picks up that preserved panel and sets `IsHidden = false`, using its stored position.

**Persistence Mechanism:**  
`WorkspacePersistenceService.RequestSave()` is called on every close event. `FilterPersistableState` in `WindowManager` includes `IsHidden` panels in the workspace JSON. On reload, hidden panels are deserialized with their exact `X`, `Y`, `Width`, and `Height` values.

---

### Task 2 ✅ — Main Menu "Filter Builder" Removal

Removed the standalone "Filter Builder" `<button>` from `MainLayout.razor` (formerly `app-toolbar` structure) and removed the `OpenFilterBuilder()` method entirely. Filter builders are still accessible via each SamplesPanel's toolbar (the funnel button).

---

### Task 3 ✅ — Main Menu "All Samples" Launcher

Added `OpenAllSamplesPanel()` method to `MainLayout.razor`. It searches `WindowManager.ActivePanels` for a `SamplesPanel` that has **no** `SamplesPanel.TopicTypeName` in its `ComponentState` (the discriminating key for the global all-topics panel). If found, it restores and brings it to front. If not found, it spawns a new panel with reserved `PanelId = "SamplesPanel.0"`.

This item is now under the **Windows** pull-down menu alongside "Topics".

---

### Task 4 ✅ — Unified Column Picker for "All Samples"

**Issue:** `InitializeColumns()` in `SamplesPanel.razor` returned early for the all-topics mode (`TopicMetadata == null`) with an empty `_availableColumns`, so the column picker showed 0 fields.

**Fix — Column Aggregation:**  
Added `PopulateAllTopicsAvailableColumns()` which iterates `TopicRegistry.AllTopics` and collects non-synthetic fields, deduplicating by `StructuredName` (first-wins). A companion dictionary `_fieldTopicTypes: Dictionary<FieldMetadata, Type>` records the source `TopicType` for each aggregated field. This runs synchronously on the UI thread using the already thread-safe `TopicRegistry.AllTopics` snapshot (returns a locked array copy) — no background thread needed.

**Fix — Lazy Refresh on Picker Open:**  
`ToggleColumnPicker()` now calls `PopulateAllTopicsAvailableColumns()` before showing the dialog when in all-topics mode, and increments `_columnPickerRevision`. The `ColumnPickerDialog` renders with `@key="_columnPickerRevision"`, forcing a fresh `ColumnPickerState` initialization each time so new topics registered since initialization appear immediately.

**Fix — Cross-Topic Safe Rendering:**  
`GetFieldValue()` was modified to:  
1. Return `null` if `target` is `null`.  
2. Check `_fieldTopicTypes` — if the field belongs to TopicA but the sample is from TopicB, return `null` immediately (no exception path).  
3. Wrap the getter call in `try/catch` as a final safety net for unexpected type mismatches.

This ensures the data grid renders an empty cell when a column field doesn't apply to a given row's topic.

---

### Task 5 ✅ — Main Menu Pull-Down Redesign (DEBT-012)

**Old structure:** A flat `<div class="app-toolbar">` with a row of `<button class="app-toolbar__button">` elements side by side.

**New structure:** A `<nav class="app-menu">` with nested `<div class="app-menu__item">` containers, each holding:
- A trigger `<button class="app-menu__trigger">` (always visible)  
- A `<div class="app-menu__dropdown">` containing `<button class="app-menu__dropdown-item">` entries

**Blazor State Approach:**  
A single `_openMenu: string?` field tracks which (if any) dropdown is open. Clicking a trigger calls `ToggleMenu(name)` — if the same menu is clicked again, it closes. A `CloseMenus()` handler is wired to `@onclick` on the outer `.app-shell` wrapper (`@onclick:stopPropagation="true"` on the `nav` prevents the outer click from immediately closing the menu on every trigger-click).

**Categories:**
| Menu | Items |
|------|-------|
| File | Reset Layout, Export Layout, Import Layout |
| View | Toggle Theme |
| Windows | Topics, All Samples |

**CSS Strategy:**  
`.app-menu__item.is-open > .app-menu__dropdown { display: flex; }` drives visibility. The dropdown uses `position: absolute; top: calc(100% - 1px)` with `z-index: 10000` to overlay the desktop panels below. The `--app-toolbar-height` CSS variable (44px) remains unchanged so all layout calculations (`calc(100vh - var(--app-toolbar-height))`) continue to work without modification.

---

## New Tests Added

**File:** `tests/DdsMonitor.Engine.Tests/Batch20Tests.cs`

| Test | Category |
|------|----------|
| `DetailPanel_HiddenState_PreservesPosition` | Task 1 |
| `DetailPanel_HiddenState_SurvivesWorkspaceRoundTrip` | Task 1 |
| `DetailPanel_Restore_ResetsHiddenAndMinimized` | Task 1 |
| `DetailPanel_MultipleIndexed_EachPreservesItsOwnPosition` | Task 1 |
| `AllSamplesPanel_IsIdentifiedByAbsenceOfTopicTypeKey` | Task 3 |
| `AllSamplesPanel_PanelId_IsReservedAsIndex0` | Task 3 |
| `AllSamplesPanel_Hidden_CanBeRestored` | Task 3 |
| `ColumnAggregation_DeduplicatesFieldsByStructuredName` | Task 4 |
| `ColumnAggregation_EmptyRegistry_ProducesNoFields` | Task 4 |
| `ColumnAggregation_SingleTopic_AllNonSyntheticFieldsIncluded` | Task 4 |
| `ColumnAggregation_FieldTopicTypeMap_AllowsCrossTopicNullReturn` | Task 4 |
| `ColumnAggregation_MatchingTopicType_FieldGetterCanExecute` | Task 4 |

**Test result:** 88/88 passed ✅

---

## Files Modified

| File | Change |
|------|--------|
| `tools/DdsMonitor/Components/Desktop.razor` | Added `DetailPanel` to `IsHideOnClosePanel()` |
| `tools/DdsMonitor/Components/Layout/MainLayout.razor` | Full rewrite — pull-down menu, removed Filter Builder, added All Samples launcher |
| `tools/DdsMonitor/Components/SamplesPanel.razor` | Column aggregation, ToggleColumnPicker refresh, GetFieldValue null-safety, @key on ColumnPickerDialog |
| `tools/DdsMonitor/wwwroot/app.css` | Added `.app-menu` pull-down menu CSS styles |
| `tests/DdsMonitor.Engine.Tests/Batch20Tests.cs` | New file — 12 tests covering Tasks 1, 3, and 4 |
