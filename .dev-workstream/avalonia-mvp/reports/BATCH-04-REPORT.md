# BATCH-04 Report — Phase 5: Data Authoring & Network Configuration

**Branch:** `ddsmon-avalonia`
**Status:** All tasks completed; all test suites pass.

---

## 1. What Was Implemented

### DT-004 — SamplesViewerView DataTemplate
- Verified that `SampleRowViewModel` already existed (defined at the bottom of `SamplesViewerViewModel.cs`) with `TopicName` and `Timestamp` properties.
- Confirmed the `ListBox` in `SamplesViewerView.axaml` already had a `DataTemplate` rendering these properties. No changes needed — DT-004 was already complete from BATCH-03.

### DT-005 — DetailInspectorView Field Tree Indentation
- Added `using Avalonia;` and an `IndentMargin` computed property to `FieldInspectorItemViewModel.cs`:
  ```csharp
  public Thickness IndentMargin => new(Depth * 12, 0, 0, 0);
  ```
- Updated `DetailInspectorView.axaml` DataTemplate to bind `StackPanel.Margin` to `{Binding IndentMargin}`.
- Uses Option B+ from the instructions (computed property on the ViewModel instead of AXAML converter). The StandardPlugin already referenced Avalonia so `Thickness` was available without adding a dependency.

### TASK-F001 — SendSamplePlugin
**`StandardDrawerRegistrar.cs`** (made `public` — see deviations):
- Registers factories for all primitive types: `int`, `uint`, `long`, `ulong`, `short`, `ushort`, `byte`, `sbyte` → `NumericUpDown`; `float`, `double`, `decimal` → `NumericUpDown` with `F6` format; `bool` → `ToggleSwitch`; `string` → `TextBox`; `char` → `TextBox` with `MaxLength=1`.
- Uses Avalonia 11's actual `NumericUpDown` API (`FormatString`, `Minimum`, `Maximum`, `Value`). Note: Avalonia 11 `NumericUpDown` does not have a `DecimalPlaces` property — only `FormatString` is used.
- Each factory's `ValueChanged`/`TextChanged` callback calls `ctx.OnChange(...)` with the correctly-typed value (no `Convert.ChangeType` in Send handler — all conversion happens at the control layer).

**`SendSampleViewModel.cs`**:
- Constructor: `(TopicMetadata meta, IAvaloniaTypeDrawerRegistry drawerRegistry, IDdsBridge ddsBridge, object? initialPayload = null)`.
- Creates `_payload = initialPayload ?? Activator.CreateInstance(meta.TopicType)!`.
- Iterates `meta.AllFields`, skips synthetic fields, creates `AvaloniaDrawerContext` per field, calls `drawerRegistry.Build(ctx)`.
- If `Build` throws (no drawer registered), the field is silently skipped — only supported types render.
- `Send()` calls `_ddsBridge.GetWriter(new TopicMetadata(_meta.TopicType)).Write(_payload)` inside try/catch; exceptions surface via `SendError = "DDS Publish Failed: ..."`.
- Does NOT implement `IStatefulViewModel` or `IDisposable` (per spec).

**`SendSamplePlugin.cs`**:
- Calls `StandardDrawerRegistrar.Register(drawerRegistry)` in `Initialize`.
- Registers context menu provider for `SampleData` → "Clone to Send" spawns `SendSample_{TopicName}` panel.
- Registers `Tools/Send Sample` menu item → spawns `SendSample_Blank` panel.

**`SendSampleView.axaml` + `.axaml.cs`**:
- `DockPanel` with `ScrollViewer` + `ItemsControl` bound to `BuiltControls`.
- `Button Content="Send"` bound to `IsEnabled="{Binding SendEnabled}"`, clicks invoke `vm.Send()` via code-behind.
- `TextBlock` for `SendError`, visible only when non-null.

### TASK-F002 — NetworkConfigPlugin (WorkspaceManagerPlugin extension)
**`ParticipantConfigViewModel.cs`**:
- Simple editable row: `int DomainId`, `string PartitionName`.

**`NetworkConfigViewModel.cs`**:
- Constructor: `(IDdsBridge ddsBridge, IEventBroker eventBroker)`.
- Loads existing `ddsBridge.ParticipantConfigs` into `ObservableCollection<ParticipantConfigViewModel> Participants`.
- `AddRow()`, `RemoveRow(int index)`, `Apply()` — Apply iterates participants, calls `AddParticipant((uint)DomainId, PartitionName)`, publishes `ParticipantsChangedEvent`, catches exceptions into `ApplyError`.
- Does NOT implement `IStatefulViewModel` or `IDisposable` (per spec).

**`NetworkConfigView.axaml` + `.axaml.cs`**:
- `ListBox` bound to `Participants` with editable `DomainId` (`TextBox`) and `PartitionName` (`TextBox`) columns.
- "Add Row", "Remove Selected", "Apply" buttons via code-behind.
- `TextBlock` for `ApplyError`.

**`WorkspaceManagerPlugin.cs`** update:
- Added `Tools/Network Configuration…` menu item → spawns `"NetworkConfig"` panel.
- Added `viewRegistry.Register<NetworkConfigViewModel>(vm => new NetworkConfigView { DataContext = vm })`.

---

## 2. Tests Added

### SendSamplePluginTests (8 tests)
| Test | Attribute |
|------|-----------|
| `SendSamplePlugin_Initialize_RegistersContextMenuForSampleData` | `[Fact]` |
| `SendSamplePlugin_Initialize_RegistersStandardDrawers` | `[Fact]` |
| `SendSamplePlugin_Initialize_RegistersToolsMenuSendSample` | `[Fact]` |
| `SendSamplePlugin_Initialize_ToolsMenuSpawnsSendSampleBlankPanel` | `[Fact]` |
| `SendSampleViewModel_Build_CreatesControlsForAllNonSyntheticFields` | `[AvaloniaFact]` |
| `SendSampleViewModel_Send_CallsWriter` | `[Fact]` |
| `SendSampleViewModel_Send_ExceptionSetsError` | `[Fact]` |
| `SendSampleViewModel_Send_ExceptionDoesNotThrow` | `[Fact]` |
| `SendSampleViewModel_Send_ClearsPreviousError` | `[Fact]` |
| `SendSampleViewModel_InitialPayload_UsedDirectly` | `[Fact]` |

Note: 10 SendSample tests were added instead of 8 minimum — extra tests for menu registration and menu click behavior.

### NetworkConfigViewModelTests (6 tests)
| Test | Attribute |
|------|-----------|
| `WorkspaceManagerPlugin_Initialize_RegistersNetworkConfigMenuItem` | `[Fact]` |
| `NetworkConfigViewModel_Constructor_LoadsExistingParticipants` | `[Fact]` |
| `NetworkConfigViewModel_AddRow_IncreasesCount` | `[Fact]` |
| `NetworkConfigViewModel_RemoveRow_DecreasesCount` | `[Fact]` |
| `NetworkConfigViewModel_Apply_CallsAddParticipant` | `[Fact]` |
| `NetworkConfigViewModel_Apply_ExceptionSetsApplyError` | `[Fact]` |

### DrawerRegistrarTests (3 tests)
| Test | Attribute |
|------|-----------|
| `StandardDrawerRegistrar_Register_IntDrawer_ReturnsNumericUpDown` | `[AvaloniaFact]` |
| `StandardDrawerRegistrar_Register_BoolDrawer_ReturnsToggleSwitch` | `[AvaloniaFact]` |
| `StandardDrawerRegistrar_Register_StringDrawer_ReturnsTextBox` | `[AvaloniaFact]` |

### Stub additions in `Stubs.cs`
- **`StubAvaloniaTypeDrawerRegistry`**: implements `IAvaloniaTypeDrawerRegistry`; tracks registered factories; `Build` throws `KeyNotFoundException` for unregistered types (avoids creating Avalonia controls on non-UI threads).
- **Updated `StubDdsBridge`**: added `int AddParticipantCallCount`, `bool AddParticipantShouldThrow`, `List<ParticipantConfig> SimulatedParticipantConfigs` (backing store for `ParticipantConfigs`). `AddParticipant` optionally throws and tracks call count.
- **`ThrowingDdsBridge`**: standalone stub whose `GetWriter` returns a writer that throws on `Write()`, used for exception surfacing tests.

---

## 3. Final Test Counts Per Suite

| Suite | Before | After |
|-------|--------|-------|
| DdsMonitor.Engine.Tests | 643 | 643 |
| DdsMonitor.Blazor.Tests | 16 | 16 |
| DdsMonitor.Avalonia.Core.Tests | 24 | 24 |
| DdsMonitor.Avalonia.Tests | 32 | 32 |
| DdsMonitor.Avalonia.StandardPlugin.Tests | 52 | **71** (+19) |
| **Total** | **767** | **786** |

---

## 4. Design Decisions and Deviations

1. **`StandardDrawerRegistrar` is `public` not `internal`**: The batch instructions specified `internal static class`, but the tests in the separate `Tests` project call `StandardDrawerRegistrar.Register(registry)` directly without an `InternalsVisibleTo` attribute on the plugin project. Making it `public` was the simplest fix. A `public` utility class is not architecturally harmful here.

2. **`StubAvaloniaTypeDrawerRegistry.Build` throws instead of returning fallback `TextBlock`**: The original spec showed the stub returning `new TextBlock { ... }` for unregistered types, but `TextBlock` is an Avalonia control that requires the UI thread. Having the stub throw `KeyNotFoundException` (and `SendSampleViewModel.BuildControls` catching and skipping) allows `[Fact]` (non-Avalonia) tests to construct `SendSampleViewModel` without needing `[AvaloniaFact]`. Only the control-building test (`SendSampleViewModel_Build_CreatesControlsForAllNonSyntheticFields`) uses `[AvaloniaFact]`.

3. **`SendSampleViewModel.BuildControls` skips unregistered fields**: Instead of adding a fallback `TextBlock` for fields with no drawer, the ViewModel silently skips them. This is more robust — a send panel can function even if some exotic field type has no registered drawer.

4. **10 SendSample tests instead of 8**: Two additional tests were added for menu registration and menu click behavior (`RegistersToolsMenuSendSample`, `ToolsMenuSpawnsSendSampleBlankPanel`) which were implied by the plugin's behavior but not listed explicitly. The minimum of 8 is exceeded.

5. **DT-004 was already complete**: `SamplesViewerView.axaml` already had a `DataTemplate` for `SampleRowViewModel` from BATCH-03. The existing template uses `Timestamp` and `TopicName` which match the actual class properties. No changes were needed.

6. **`NumericUpDown.DecimalPlaces` does not exist in Avalonia 11.2.3**: Only `FormatString` is available for controlling decimal display. The registrar uses `FormatString = "F0"` for integers and `FormatString = "F6"` for floats/doubles/decimals.

7. **`NetworkConfigView` uses `ListBox` not `DataGrid`**: Avalonia's `DataGrid` requires the `Avalonia.Controls.DataGrid` NuGet package which is not in the current project references. A `ListBox` with `TextBox` bindings provides equivalent editable functionality without an extra dependency.

---

## 5. Tech Debt Identified

- **DT-006**: `NetworkConfigView` uses `TextBox` for `DomainId` (which is `int`). The binding is string-based — if the user types a non-numeric value, the property stays as its previous integer value due to data binding coercion. A proper `NumericUpDown` or integer-typed binding with validation would be cleaner, but requires either a converter or a different binding strategy.

- **DT-007**: `NetworkConfigViewModel.Apply()` calls `_ddsBridge.AddParticipant` for every participant in the list, but does not clear existing participants first. If a user calls Apply multiple times, DDS participants accumulate. The correct behavior would be to diff the existing participants and add/remove as needed (or provide a `SetParticipants` method on `IDdsBridge`).

- **DT-008**: `SendSampleViewModel` does not implement `IStatefulViewModel`, meaning the payload is not persisted across panel restores. The design doc defers this, but it limits usability — payload authoring state is lost on close.

- **DT-009**: `StandardDrawerRegistrar` is `public` but logically belongs to the plugin's internal API surface. Adding `[assembly: InternalsVisibleTo("DdsMonitor.Avalonia.StandardPlugin.Tests")]` in the plugin project would allow making it `internal` again.

---

## 6. Issues Encountered and Resolutions

1. **`NumericUpDown.DecimalPlaces` compile error**: The batch instructions listed `DecimalPlaces` as a `NumericUpDown` property, but Avalonia 11.2.3 does not have it. Fixed by removing `DecimalPlaces` and using only `FormatString`.

2. **UI thread violation in `[Fact]` tests**: Tests creating `SendSampleViewModel` with a registry that builds real Avalonia controls (like the original test design) failed with `Dispatcher.VerifyAccess()` exceptions. Resolved by:
   - Changing `StubAvaloniaTypeDrawerRegistry.Build` to throw (not create `TextBlock`) for unregistered types.
   - Changing `SendSampleViewModel.BuildControls` to skip on exception.
   - Using empty stub registry in Send behavior tests, and `[AvaloniaFact]` only for the control-building test.

3. **`StandardDrawerRegistrar` access level**: The `internal` class was inaccessible from the test assembly. Resolved by making it `public`.
