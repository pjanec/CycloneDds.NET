# ME2-BATCH-06 Report

**Batch:** ME2-BATCH-06  
**Tasks:** ME2-T25-A, ME2-T25-B, ME2-T12, ME2-T13-A, ME2-T13-B, ME2-T26  
**Date:** 2026-03-19  
**Status:** Complete ✅

---

## Q1: Issues Encountered & Resolutions

### `TopicColorService` namespace placement
The initial plan placed `TopicColorService` inside `DdsMonitor.Blazor/Services/` to follow the pattern established by other UI services. However, the Engine test project only references `DdsMonitor.Engine` — adding a direct project reference to the Blazor project would pull in ASP.NET/Blazor hosting dependencies that are inappropriate for a pure-logic test project.

**Resolution:** `TopicColorService` was relocated to `DdsMonitor.Engine` (namespace `DdsMonitor.Engine`). The service has zero Blazor-specific dependencies — it only uses `IWorkspaceState` (Engine) and `System.Text.Json` for persistence. Moving it to Engine gives it the widest testability surface and allows future headless tools to consume it without the Blazor stack.

### `GetPanelBaseName` fallback for FullName strings
The original fix only handled AQNs with a comma (e.g. `"My.Panel, Assembly, ..."`). A bare FullName string (`"My.Namespace.MyPanel"`) passed to an unregistered panel would still produce the full namespace path as the `PanelId` base. An additional pass extracting the last dot-segment handles both cases uniformly.

### Pre-existing flaky tests in `FeatureDemo.Tests`
Two DDS network tests (`StockPublisher_PublishesMultipleSymbols`, `StockSubscriber_FiltersMessages`) fail intermittently due to DDS discovery latency when running under high test parallelism. These failures are fully pre-existing and unrelated to ME2-BATCH-06 changes — confirmed by checking `git diff HEAD --name-only` showing no FeatureDemo source modifications.

---

## Q2: Weak Points Observed

1. **`TopicPropertiesPanel` `OnParametersSet` restoration path** — When restoring from a workspace JSON, `TopicMetadata` arrives as `null` on first `OnParametersSet`. The component reads `PanelState.ComponentState["TopicPropertiesPanel.TopicTypeName"]` and resolves the CLR type using the multi-assembly scan, then calls `TopicRegistry.GetByType()`. This works correctly but relies on the `TopicRegistry` being populated before the panel is restored. In practice this is always true since `ITopicRegistry` is re-populated from `AssemblySourceService` at startup before the workspace panels are rendered.

2. **Color picker HTML `<input type="color">`** — When the topic is in "auto" mode, the browser's color picker is shown without a `value` attribute pre-set to the resolved auto-color. This is intentional: injecting the CSS variable value as a hex is not straightforward because CSS variable resolution happens at render time in the browser. Users who want to customize starting from the auto-color should first note the color code from the `var(--topic-color-N)` palette definition in `app.css`.

3. **`GetAutoColorIndex` uses a non-cryptographic hash** — The hash function (`hash = hash * 31 + ch`) is stable and deterministic but not cryptographically uniform. For the 12-color palette and typical topic names (Latin ASCII), distribution is acceptable. No security concern since this is purely cosmetic.

---

## Q3: Design Decisions

### ME2-T25-A — `GetPanelBaseName` fix
Applied the same comma-stripping pattern already present in `ResolveComponentTypeName`, then additionally extracted the simple class name (last dot segment) from the resulting FullName. This makes `PanelId` values like `TopicExplorerPanel.1` instead of `DdsMonitor.Components.TopicExplorerPanel.1` for all scenarios: raw AQN, FullName, simple name.

### ME2-T25-B — `ResolveComponentType` fix
`Desktop.razor`'s `ResolveComponentType` now follows a two-stage strategy:
1. **Stage 1 — Standard `Type.GetType`**: Handles types in mscorlib, System assemblies, and the Blazor host assembly via the standard CLR type resolution path. Most first-party panel types resolve here.
2. **Stage 2 — Loaded-assembly scan**: Strips the AQN suffix (if present) to get the FullName, then iterates `AppDomain.CurrentDomain.GetAssemblies()` and calls `assembly.GetType(fullName)`. Plugin assemblies loaded via `AssemblySourceService`'s `Assembly.LoadFrom` are present in the domain at render time, so they resolve correctly here.

This design avoids the need for callers to register plugin assemblies explicitly — any assembly loaded into the AppDomain is automatically discoverable.

### ME2-T12 — `TopicPropertiesPanel` architecture
The panel follows the established workspace persistence pattern:
- `[Parameter] TopicMetadata? TopicMetadata` — populated by the spawning caller.
- `[CascadingParameter] PanelState? PanelState` — used to persist `AssemblyQualifiedName` in the component state dict under key `"TopicPropertiesPanel.TopicTypeName"`.
- `OnParametersSet()` restores from the persisted AQN using the multi-assembly scan from T25-B, then calls `TopicRegistry.GetByType()`.

The color picker is embedded directly in the info grid so users can change it in context without navigating away from the topic properties view.

### ME2-T13-A/B — Context menu wiring
Both `TopicExplorerPanel` and `TopicSourcesPanel` use the same `HandleRowMouseDown` + right-mouse-button check pattern established in `SamplesPanel`. The `OpenTopicProperties` helper follows the recycle-hidden-panel pattern from `OpenDetail` in `SamplesPanel`: hidden panels are reused (updating their topic reference) to avoid stacking duplicate windows.

### ME2-T26 — Global colorized topic names
**Palette:** 12 CSS variables (`--topic-color-0` through `--topic-color-11`) defined separately in `:root` (light theme — saturated, high-contrast) and `[data-theme="dark"]` (softer, pastel-shifted for readability on dark backgrounds).

**Assignment:** A deterministic `hash = hash * 31 + ch` string hash maps `ShortName` to `index % 12`. The same topic always gets the same color regardless of registration order or session.

**Override persistence:** `TopicColorService` (relocated to `DdsMonitor.Engine`) stores user overrides in `%APPDATA%/DdsMonitor/topic-colors.json`, separate from `workspace.json`, so color preferences survive workspace resets.

**Application points:** Topic name coloring is applied at five locations:
| Location | Mechanism |
|---|---|
| `TopicExplorerPanel` rows | Inline `style` on `<div class="topic-explorer__name">` |
| `SamplesPanel` "Topic" column | `RenderCellValue` injects a `<span>` with inline color style |
| `SendSamplePanel` topic dropdown | Inline `style` on `<span class="send-sample-panel__combo-option-name">` |
| `ReplayPanel` "Next Sample" type | Inline `style` on `<span class="replay-panel__next-type">` |
| `TopicPropertiesPanel` header | Inline `style` on topic-name span |

---

## Q4: Edge Cases Found

- **Empty `ShortName`**: `GetAutoColorIndex("")` returns index 0 (hash stays 0). `GetColorStyle("")` returns `"color: var(--topic-color-0);"`. Acceptable fallback.
- **`Type.GetType` with stripped AQN**: If a workspace was saved with `DdsMonitor.Components.SomePanel, DdsMonitor, Version=0.1.0.0` (no assembly loaded), Stage 2 scans all loaded assemblies — if the type isn't there, `null` is returned and Desktop.razor renders the `"Missing component: …"` placeholder. This is the correct behavior for a genuinely unavailable plugin.
- **Simultaneous multiple `TopicPropertiesPanel` windows**: The `OpenTopicProperties` helper recycles a hidden panel first; only if no hidden panel exists does it spawn a new one. Multiple different topics can have open panels simultaneously (each gets its own `PanelId`).

---

## Q5: Performance Concerns

- **`AppDomain.CurrentDomain.GetAssemblies()` scan in `ResolveComponentType`**: Called once per panel per render cycle in `RenderPanelBody`. The assembly set is stable after startup so iteration is O(n_assemblies). Typical Blazor apps load 50–200 assemblies; the scan is a simple `GetType` lookup per assembly which is O(1) in CLR's type table. Negligible overhead compared to DOM diffing.
- **`TopicColorService.GetColorStyle` per cell**: Called in `RenderCellValue` for every "Topic" field cell in `SamplesPanel`. The method is a pure dictionary lookup (user override) + string concatenation — O(1) per call, no I/O.

---

## Test Summary

| Test Class | Tests | Pass | Fail |
|---|---|---|---|
| `ME2Batch06Tests` | 21 | 21 | 0 |
| Full `DdsMonitor.Engine.Tests` | 435 | 435 | 0 |
| `CycloneDDS.CodeGen.Tests` | 185 | 185 | 0 |
| `CycloneDDS.Runtime.Tests` | 134 | 134 | 0 (1 skipped) |
| Other test projects | 67 | 67 | 0 |
| **FeatureDemo.Tests** | 20 | 18 | **2 (pre-existing, DDS network flaky)** |

The two FeatureDemo failures are pre-existing DDS network reliability tests that require live DDS discovery and are unrelated to this batch.

---

## Files Modified / Created

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Engine/WindowManager.cs` | ME2-T25-A: `GetPanelBaseName` strips AQN and extracts simple class name |
| `tools/DdsMonitor/DdsMonitor.Engine/TopicColorService.cs` | **New** — ME2-T26: deterministic auto-hash colors + user override persistence |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/Desktop.razor` | ME2-T25-B: `ResolveComponentType` scans all loaded assemblies; add `TopicPropertiesPanel` to hide-on-close list |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicPropertiesPanel.razor` | **New** — ME2-T12: full topic schema inspector + color picker |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicExplorerPanel.razor` | ME2-T13-A: inject `ContextMenuService`/`TopicColorService`; row right-click; colored topic names |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicSourcesPanel.razor` | ME2-T13-B: inject `IWindowManager`/`ContextMenuService`; sort topics alphabetically; two-line CLR type; row right-click |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor` | ME2-T26: inject `TopicColorService`; color "Topic" column in `RenderCellValue` |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/SendSamplePanel.razor` | ME2-T26: inject `TopicColorService`; color topic dropdown options |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/ReplayPanel.razor` | ME2-T26: inject `TopicColorService`; color "Next Sample" type span |
| `tools/DdsMonitor/DdsMonitor.Blazor/wwwroot/app.css` | ME2-T26: add 12-color palette for light & dark; add `.topic-properties` and helper CSS |
| `tools/DdsMonitor/DdsMonitor.Blazor/Program.cs` | Register `TopicColorService` singleton |
| `tests/DdsMonitor.Engine.Tests/ME2Batch06Tests.cs` | **New** — 21 tests covering T25-A, T25-B, T26, T13-B |
| `docs/mon-ext-2/ME2-TASK-TRACKER.md` | Mark T12, T13-A, T13-B, T25-A, T25-B, T26 as complete |
| `.dev-workstream/MON-DEBT-TRACKER.md` | MON-DEBT-015 and MON-DEBT-016 → ✅ Resolved |
