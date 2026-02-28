# MON-BATCH-21 Report

**Batch:** MON-BATCH-21  
**Status:** COMPLETE  
**Tests:** 102 passed / 0 failed  
**Build:** Succeeded (Release)

---

## Task 1 – Main Menu Interaction Fix ✅

**Files changed:** `tools/DdsMonitor/Components/Layout/MainLayout.razor`

- Added `CloseMenus()` call at the start of `ToggleTheme()` (async method). Previously the `View → Theme` dropdown item toggled the theme but left the dropdown visually open.
- Added `@onclick="CloseMenus"` handler to the `<label for="workspaceImport">Import Layout</label>` element. Labels can't directly call event methods without an `@onclick`, so this was the quick fix.
- The `File`, `View`, and `Windows` menu item action handlers now all close the dropdown before proceeding.

---

## Task 2 – Persistence for Topics & Instances Panels ✅

**Files changed:**  
- `tools/DdsMonitor/Components/Desktop.razor`  
- `tools/DdsMonitor/Components/TopicExplorerPanel.razor`

### Topics Panel
`Desktop.razor`'s `IsHideOnClosePanel()` now returns `true` for `TopicExplorerPanel`, meaning closing the panel hides it (sets `IsHidden = true`) instead of removing it from `ActivePanels`. On re-launch via the Windows menu, `OpenTopicsPanel()` (already correct) finds the hidden panel in `ActivePanels`, restores `IsHidden = false`, and brings it to the front – recovering exact position/size.

### Instances Panel – Stable PanelId
`Desktop.razor`'s `IsHideOnClosePanel()` was extended to include `InstancesPanel` (same hide-on-close semantics).

`OpenInstancesPanel()` in `TopicExplorerPanel.razor` was rewritten to:

1. **Primary lookup by stable PanelId** – computed via FNV-1a hash over `TopicType.FullName`. The same deterministic hash is computed on every call, so after a workspace reload the old hidden panel is immediately found by its ID.
2. **Fallback lookup by live `TopicMetadata` object** (for panels created in the same session).
3. **Fallback lookup by serialised `InstancesPanel.TopicTypeName` key** (for panels deserialized from JSON where the live object is gone):
   ```csharp
   if (existingPanel.ComponentState.TryGetValue("InstancesPanel.TopicTypeName", out var typeObj))
   { ... Type.GetType(typeName) == metadata.TopicType ... }
   ```
4. **Create new panel with stable PanelId**, e.g. `InstancesPanel.T1A2B3C4D`.

The FNV-1a hash used:
```csharp
var hash = 2166136261u;
foreach (var c in key) { hash ^= (uint)c; hash *= 16777619u; }
```
This is deterministic across .NET sessions (unlike `string.GetHashCode()`).

---

## Task 3 – Global Default Dimension Scales ✅

**Files changed:**  
- `tools/DdsMonitor/Components/Desktop.razor`  
- `tools/DdsMonitor/Components/Layout/MainLayout.razor`  
- `tools/DdsMonitor/Components/TopicExplorerPanel.razor`  
- `tools/DdsMonitor/Components/SamplesPanel.razor`

| Panel | Old Width | New Width | Multiplier |
|-------|-----------|-----------|------------|
| Topics Panel (TopicExplorerPanel) | 420 (default) | **840** | 2× |
| All Samples Panel | 560 | **1120** | 2× |
| Topic-specific Samples Panel | 420 (default) | **840** | 2× |
| Instances Panel | 420 (default) | **840** | 2× |
| Filter Builder Panel | 520 | **1040** | 2× |
| Sample Detail Panel | 420 (default) | **546** | ×1.3 |

---

## Task 4 – Card View Formatting (Expand All Mode) ✅

**Files changed:** `tools/DdsMonitor/Components/SamplesPanel.razor`

The expand card header was:
```html
<span>#@row.Sample.Ordinal</span>
<span>@row.Sample.Timestamp.ToString("HH:mm:ss.fff")</span>
<span>@GetStatusSymbol(row.Sample)</span>
```

Changed to:
```html
<span>#@row.Sample.Ordinal</span>
<span>[@row.Sample.Timestamp.ToString("HH:mm:ss.fff")]</span>
<span>@row.Sample.TopicMetadata.ShortName</span>
```

Result: `#35  [12:32:16.242]  SensorData`

---

## Task 5 – Topic Subscribe Checkbox Sync ✅

**Files changed:**  
- `tools/DdsMonitor/DdsMonitor.Engine/IDdsBridge.cs`  
- `tools/DdsMonitor/DdsMonitor.Engine/DdsBridge.cs`  
- `tools/DdsMonitor/Components/TopicExplorerPanel.razor`

Added `event Action? ReadersChanged` to `IDdsBridge`. `DdsBridge` fires this event after a reader is added (`TrySubscribe`) or removed (`Unsubscribe`).

`TopicExplorerPanel.OnInitialized` now subscribes:
```csharp
DdsBridge.ReadersChanged += HandleReadersChanged;
```
Where `HandleReadersChanged` marshals `RefreshAndRender()` onto the Blazor render thread via `InvokeAsync`. This gives instant checkbox updates whenever background subscriptions change, rather than waiting up to 1 second for the polling timer.

`Dispose()` unsubscribes to prevent memory leaks.

---

## Task 6 – Global Filter Builder Metadata Crash Fix ✅

**Files changed:** `tools/DdsMonitor/DdsMonitor.Engine/FilterCompiler.cs`

### Root Cause
`FilterCompiler.Compile(expression, null)` returned an error `"Topic metadata is required for payload field filters."` for any expression containing `Payload.X` when `TopicMetadata` was `null` (All-Topics mode).

### Solution – Lazy Per-Type Compilation
When `topicMeta == null` and the expression contains payload field references, `FilterCompiler` now returns a **lazy predicate** built by `BuildDynamicNullMetaPredicate(expression)`:

1. The predicate captures the original filter expression string in a closure.
2. On its first invocation for a given sample, it reads the **runtime payload type** (`sample.Payload.GetType()`).
3. It constructs `new TopicMetadata(payloadType)` to get strongly-typed field accessors, then calls `FilterCompiler.Compile(expression, typedMeta)` directly.
4. The compiled typed predicate is **cached** for that payload type (`Dictionary<Type, Func<SampleData, bool>>`).
5. Subsequent samples of the same payload type re-use the cached predicate with zero overhead.

If the payload type does not have a `[DdsTopic]` attribute, or the field path does not exist on the type, the predicate safely returns `false` instead of throwing.

This means `Payload.Id == 2` in the All Samples filter correctly matches **any sample whose payload has an `Id` property equal to `2`** across all active topics.

---

## Task 7 – Visual Negation Indicator ✅

**Files changed:**  
- `tools/DdsMonitor/Components/FilterBuilderPanel.razor`  
- `tools/DdsMonitor/wwwroot/app.css`

The `!` (negation) buttons on both filter groups and conditions now carry a second CSS class:
```html
class="filter-builder__btn filter-builder__negate @(node.IsNegated ? "is-active" : string.Empty)"
```

New CSS rules:
```css
.filter-builder__negate.is-active {
  background: var(--accent-2);
  border-color: var(--accent-2);
  color: var(--panel-bg);   /* contrast text */
}
.filter-builder__negate.is-active:hover { opacity: 0.85; }
```

When a rule is negated the button shows a filled accent-colored background as a persistent "pressed" indicator.

---

## New Tests – `Batch21Tests.cs`

**File:** `tests/DdsMonitor.Engine.Tests/Batch21Tests.cs`

| Test | Coverage |
|------|----------|
| `FilterCompiler_NullMeta_PayloadField_CompilesSuccessfully` | Task 6 – compile succeeds |
| `FilterCompiler_NullMeta_PayloadField_MatchesSampleWithProperty` | Task 6 – correct match |
| `FilterCompiler_NullMeta_PayloadField_ReturnsFalse_WhenPropertyMissing` | Task 6 – safe miss |
| `FilterCompiler_NullMeta_OrdinalFilter_StillWorks` | Task 6 – non-payload still works |
| `FilterCompiler_NullMeta_PayloadField_NoExceptionOnMissingProperty` | Task 6 – no throw |
| `FilterCompiler_NullMeta_MultiplePayloadFields_MatchCorrectly` | Task 6 – multiple types |
| `InstancesPanel_StablePanelId_IsDeterministicAcrossCalls` | Task 2 – stable hash |
| `InstancesPanel_StablePanelId_DifferentTypes_ProduceDifferentIds` | Task 2 – uniqueness |
| `InstancesPanel_HideOnClose_PersistedGeometryRestored` | Task 2 – geometry survives JSON |
| `DefaultPanelWidths_*` (×4) | Task 3 – verify constants |
| `CardHeader_Format_MatchesExpectedPattern` | Task 4 – format string |

Total: **102 tests, 0 failures**.
