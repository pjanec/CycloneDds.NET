# MON-BATCH-19-REPORT: Heavy UI Bugfixes and Code Deduplication

**Status:** COMPLETE  
**Build:** `dotnet build` — 0 errors, 1 inconsequential nullable warning (CS8601 in Razor-generated code for a `Type?` → `Type?` assignment — not a real issue).

---

## Task 1: Unified `SamplesPanel` Architecture

`AllSamplesPanel.razor` has been **deleted**. `SamplesPanel.razor` now handles both modes via a single `private bool IsAllTopicsMode => TopicMetadata == null;` sentinel:

- The `@if (TopicMetadata == null) { placeholder } else { ... }` wrapper was removed. The panel HTML always renders in full — the same toolbar, grid, status bar, and column picker are used in both modes.
- `InitializeColumns()`: when `TopicMetadata == null`, clears `_availableColumns`/`_selectedColumns`, nulls out the size/delay fields, and calls `RebuildLayoutColumns()` directly. This yields the standard structural columns (Ordinal, Status, **Topic**, Timestamp, Actions) without any topic-payload columns — exactly the right appearance for the All-Samples view.
- `ApplyFilter()`: when `TopicMetadata == null`, compiles the filter expression against `null` (no type constraint) instead of binding it to a specific topic type.
- `EnsureView()`: when `TopicMetadata == null`, iterates all samples without a topic-type guard; the full sample set feeds through the optional filter predicate.
- `RefreshCounts()`: early-return on `TopicMetadata == null` was removed — the timer now always refreshes counts in both modes.
- `OnParametersSet()`: replaced the hard `if (TopicMetadata == null) return` guard with a `_columnsInitialized` flag + `TopicMetadata?.TopicType` comparison so the first render of the all-topics instance triggers column/filter initialization exactly once.

**Desktop.razor** was updated to spawn a `SamplesPanel` (with no `TopicMetadata` in state) instead of `AllSamplesPanel`. The All Samples panel is detected via `!panel.ComponentState.ContainsKey("SamplesPanel.TopicTypeName")` to distinguish it from topic-specific panels.

---

## Task 2: Index-Based Dimension Persistence for Samples Windows

The global All Samples panel is **force-assigned PanelId `"SamplesPanel.0"`** immediately after spawning, reserving index 0 as its permanent workspace identifier:

```csharp
var allPanel = WindowManager.SpawnPanel(SamplesPanelTypeName);
allPanel.PanelId = "SamplesPanel.0"; // Reserve index 0 for the global All Samples panel.
```

For **all** `SamplesPanel` and `FilterBuilderPanel` windows, `ClosePanel` in `Desktop.razor` now sets `panel.IsHidden = true` instead of calling `WindowManager.ClosePanel()`. This means the panel record — including its X, Y, Width, and Height — is **never removed from the active panel list**. The workspace serializer captures the full geometry on every save, so re-opening or reloading always restores the panel to its last known bounds.

`OpenSamplesPanel` (in `TopicExplorerPanel`) already reuses hidden panels by matching `TopicMetadata.TopicType`, so topic-specific windows snap back to their prior screen position on re-open.

---

## Task 3: Context Menu `mouseup` Glitch — Event Sequence Fix

The root cause: `_ignoreNextClose` was a single boolean flag shared by both `HandleOverlayMouseUp` and `HandleOverlayContextMenu`. When both events fire for the same right-click (which occurs when Blazor's microtask-based rerender runs between `contextmenu` and `mouseup`), the first handler clears the flag and the second one proceeds to call `ContextMenuService.Hide()`, instantly dismissing the menu.

**Fix:** replaced the single `_ignoreNextClose` with two independent guards:

```csharp
private bool _ignoreNextMouseUp;
private bool _ignoreNextContextMenu;
```

`HandleChanged` arms **both** when the menu opens. Each handler consumes only its own flag:

```
HandleOverlayMouseUp()    → consumes _ignoreNextMouseUp    (no cascade)
HandleOverlayContextMenu() → consumes _ignoreNextContextMenu (no cascade)
```

Even if both events fire in rapid succession from the same right-click, neither can trigger a close — the menu stays open until the next independent user interaction on the overlay.

---

## Task 4: Filter Window Dimension Memory

`OpenFilterBuilder()` now **searches for an existing hidden `FilterBuilderPanel`** whose `LockedTargetPanelId` matches the calling samples panel's ID before spawning a new window:

```csharp
foreach (var existingPanel in WindowManager.ActivePanels)
{
    if (existingPanel.ComponentState.TryGetValue(nameof(FilterBuilderPanel.LockedTargetPanelId), out var locked) &&
        string.Equals(locked?.ToString(), sourcePanelId, StringComparison.Ordinal))
    {
        existingPanel.IsHidden = false;
        existingPanel.IsMinimized = false;
        WindowManager.BringToFront(existingPanel.PanelId);
        return;
    }
}
```

`FilterBuilderPanel` windows are also covered by the hide-on-close logic in Desktop.razor (`IsHideOnClosePanel`), so their geometry is always preserved in the workspace. Each filter window's identity is bound to its parent samples panel ID, making location/size restoration naturally keyed by panel identity.

---

## Task 5: Pre-Filled `"id"` Bug in Filter Builder

`AddCondition()` previously auto-selected `GetAvailableFields().FirstOrDefault()` and called `ApplyField(condition, firstField)`, which set `condition.FieldPath = "Payload.<firstFieldName>"`. For types whose first field is named `id`, this produced the visible `"id"` pre-fill.

**Fix:** `AddCondition` now simply appends a blank `FilterConditionNode` with no pre-filled values:

```csharp
private void AddCondition(FilterGroupNode group)
{
    var condition = new FilterConditionNode();
    group.Children.Add(condition);
}
```

The `FieldPicker` component receives `SelectedField=null` and shows its placeholder, forcing the user to explicitly select from the available fields.

---

## Task 6: Topics Panel Filter Bar Redesign

The `.topic-explorer__filters` container now uses tighter spacing (`gap: 4px`, `padding: 2px 0`) and a subtle bottom border to separate it visually from the toolbar row.

`.tri-toggle` buttons were radically condensed:
- Font: `0.68rem`, `font-weight: 700`, `text-transform: uppercase`, `letter-spacing: 0.06em`
- Padding: `2px 7px` (down from `6px 10px`)
- Background: `transparent` (removes the filled button appearance)
- State indicator circle: `14×14px` (down from `18×18px`)

The result is a sleek, compact tag/pill strip that is visually distinct from the standard heavy action buttons in the toolbar.

---

## Task 7: Icon-Only Toolbars

All top-level panel toolbar buttons now render inline SVG icons with descriptive `title` attributes (HTML tooltips). Text labels have been removed.

| Panel | Buttons replaced |
|-------|-----------------|
| **SamplesPanel** | Track (eye), Columns (three-column grid), Expand All (outward arrows), Filter (funnel), Save Settings (download), Load Settings (upload) |
| **DetailPanel** | Linked (chain link, with `is-on` highlight), Detached (broken chain), Clone to Send (copy squares) |
| **TopicExplorerPanel** | Grid (four-square grid), Instances (bullet list) |

All SVG icons are drawn at 14–16 px using `currentColor` strokes so they automatically inherit light/dark theme colours. Every button carries a `title="…"` attribute meeting the accessibility requirement.

CSS for the affected button classes (`.samples-panel__track`, `.samples-panel__columns`, `.samples-panel__expand-btn`, `.samples-panel__filter-btn`, `.samples-panel__settings-btn`, `.detail-panel__link`, `.detail-panel__clone`) was updated to use `display: inline-flex; align-items: center; justify-content: center` and reduced padding (`5px 9px`) fitting icon-only content.

---

## Success Criteria Verification

| Criterion | Status |
|-----------|--------|
| `AllSamplesPanel.razor` deleted; `SamplesPanel` handles all traffic | ✅ |
| Double-clicked topic samples windows restore their absolute screen bounds | ✅ (hide-on-close + workspace persistence) |
| Global "All Samples" window restores to index 0 | ✅ (`PanelId = "SamplesPanel.0"`) |
| Context menus stay open after right-clicking | ✅ (dual ignore flags) |
| Filter dialogs remember their bounds | ✅ (hide-on-close + panel reuse) |
| `"id"` pre-fill bug removed from filter builder | ✅ |
| Toolbars use icons with tooltips instead of text blocks | ✅ |
| Topic filter bar is distinctly sleeker and smaller | ✅ |
