# ME1 — Task Details

**Reference Design:** See [ME1-DESIGN.md](./ME1-DESIGN.md) for architecture and rationale.  
**Tracker:** See [ME1-TASK-TRACKER.md](./ME1-TASK-TRACKER.md) for current status.

> Each task below is self-contained. Read the referenced design chapter first, then this file — together they give complete implementation instructions.

---

## ME1-T01 — Typed Enum `@bit_bound` Support

**Design ref:** [Phase 1.1 — Typed Enum `@bit_bound`](./ME1-DESIGN.md#11-typed-enum-bit_bound-me1-t01)  
**Scope:** `CycloneDDS.CodeGen` (TypeInfo, SchemaDiscovery, IdlEmitter, SerializerEmitter)

### Description

C# enums can declare their underlying storage type:

```csharp
public enum EStatus : byte    { ... }  // → @bit_bound(8)
public enum EPriority : short { ... }  // → @bit_bound(16)
public enum EKind             { ... }  // → default 32-bit, no annotation
```

The code generator must read the underlying CLR type via Roslyn and emit:
- The `@bit_bound(N)` IDL annotation (only for N < 32).
- Matching narrow read/write calls in the generated serializer.

### Files to modify

| File | Change |
|---|---|
| `tools/CycloneDDS.CodeGen/TypeInfo.cs` | Add `int EnumBitBound { get; set; } = 32` |
| `tools/CycloneDDS.CodeGen/SchemaDiscovery.cs` | Map `INamedTypeSymbol.EnumUnderlyingType.SpecialType` to bit size |
| `tools/CycloneDDS.CodeGen/IdlEmitter.cs` | Emit `@bit_bound(N)` before enum when `N < 32` |
| `tools/CycloneDDS.CodeGen/SerializerEmitter.cs` | Use `WriteUInt8`/`ReadUInt8` (8-bit) or `WriteUInt16`/`ReadUInt16` (16-bit) |

### Success Conditions

1. **Unit test** (`CycloneDDS.CodeGen.Tests`): Given a schema with `public enum EStatus : byte`, the generated IDL file contains `@bit_bound(8)` immediately before `enum EStatus`.
2. **Unit test**: Given `public enum EKind` (no underlying type), the generated IDL does **not** contain `@bit_bound`.
3. **Unit test**: Given `public enum EPriority : short`, the generated serializer C# file emits `writer.WriteUInt16((ushort)` for that enum field, not `writer.WriteInt32`.
4. **Existing tests pass:** all `CycloneDDS.CodeGen.Tests` must remain green.

---

## ME1-T02 — `[InlineArray]` Support

**Design ref:** [Phase 1.2 — `[InlineArray]` Support](./ME1-DESIGN.md#12-inlinearray-support-me1-t02)  
**Scope:** `DdsMonitor.Engine` (TopicMetadata, FixedBufferJsonConverter), `CycloneDDS.CodeGen`

### Description

C# 12 `[InlineArray(N)]` structs offer a safe fixed-size array alternative to `unsafe fixed T[N]`. The pipeline must:

1. Detect them in reflection (same as `FixedBufferAttribute` today).
2. Serialize/deserialize them as JSON arrays (via span access).
3. Render them correctly in the UI (already works once #1 is done, since `IsFixedSizeArray` drives the UI).

`[InlineArray]` structs have exactly one user-named field (not `FixedElementField`). The element count comes from `System.Runtime.CompilerServices.InlineArrayAttribute.Length`.

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs` | Detect `InlineArrayAttribute`; build `FieldMetadata` with `isFixedSizeArray=true`, correct `elementType` and `fixedArrayLength` |
| `tools/DdsMonitor/DdsMonitor.Engine/Json/FixedBufferJsonConverter.cs` | Detect `[InlineArray]`; use `MemoryMarshal.CreateSpan<TElem>` for element access |
| `tools/CycloneDDS.CodeGen/SerializerEmitter.cs` | For `[InlineArray]` fields, generate span-based iteration instead of pointer arithmetic |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/DynamicForm.razor` | Setter delegate must use span to update individual element and write entire struct back |

### Success Conditions

1. **Unit test** (`DdsMonitor.Engine.Tests`): A `TopicMetadata` built for a struct containing an `[InlineArray(8)]` field of `float` produces a `FieldMetadata` with `IsFixedSizeArray == true`, `FixedArrayLength == 8`, `ElementType == typeof(float)`.
2. **Unit test**: JSON serialization of a struct with `[InlineArray(4)] int` field produces `[1,2,3,4]` (array form), not `{}` or the struct type name.
3. **Existing `FixedBufferAttribute`** tests must still pass.
4. The `DynamicForm` renders the inline-array field as a fixed-length list of inputs (visually identical to the `unsafe fixed` case).

---

## ME1-T03 — Default Topic Name from Namespace

**Design ref:** [Phase 1.3 — Default Topic Name](./ME1-DESIGN.md#13-default-topic-name-from-namespace-me1-t03)  
**Scope:** `CycloneDDS.Schema` (DdsTopicAttribute), `DdsMonitor.Engine` (TopicMetadata), `CycloneDDS.CodeGen` (SchemaDiscovery)

### Description

`[DdsTopic]` should accept an optional `topicName`. When omitted, the topic name is derived from `type.FullName.Replace('.', '_')`. Example:

```csharp
[DdsTopic]                  // → "FeatureDemo_Scenarios_Chat_Message"
public struct Message { ... }

[DdsTopic("MyTopicName")]   // → "MyTopicName" (explicit, unchanged)
public struct Telemetry { ... }
```

This enables `StartsWith` filtering across entire topic namespaces.

### Files to modify

| File | Change |
|---|---|
| `src/CycloneDDS.Schema/Attributes/TypeLevel/DdsTopicAttribute.cs` | Make `topicName` parameter `null`-defaulted; remove non-null guard |
| `tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs` | Compute fallback name from `topicType.FullName?.Replace('.','_') ?? topicType.Name` |
| `tools/CycloneDDS.CodeGen/SchemaDiscovery.cs` | Apply same fallback when Roslyn `[DdsTopic]` attribute has no argument |
| `tools/CycloneDDS.CodeGen/IdlEmitter.cs`, `SerializerEmitter.cs` | Consume resolved name; no structural change, just ensure no null-deref |

### Success Conditions

1. **Unit test** (`CycloneDDS.Core.Tests` or new schema test): `new DdsTopicAttribute()` is valid (no exception). `TopicName` property is `null`.
2. **Unit test** (`DdsMonitor.Engine.Tests`): `TopicMetadata` for a type `Foo.Bar.Baz` attributed with `[DdsTopic]` (no name) has `TopicName == "Foo_Bar_Baz"`.
3. **Unit test**: `TopicMetadata` for a type attributed with `[DdsTopic("ExplicitName")]` has `TopicName == "ExplicitName"`.
4. **CodeGen test**: IDL output for a type `My.Ns.Item` with `[DdsTopic]` (no name) includes the topic declaration using `My_Ns_Item`.
5. **Existing tests:** all existing topic/schema tests pass.

---

## ME1-T04 — StartsWith / EndsWith in Filter Builder UI

**Design ref:** [Phase 2.1 — StartsWith / EndsWith Filter Operators](./ME1-DESIGN.md#21-startswith--endswith-filter-operators-me1-t04)  
**Scope:** `DdsMonitor.Engine` (FilterNodes), `DdsMonitor.Blazor` (FilterBuilderPanel.razor)

### Description

`FilterComparisonOperator` already has `StartsWith`, `EndsWith`, `Contains`. This task exposes them in the visual query builder and fixes their Dynamic LINQ emission.

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Engine/Filtering/FilterNodes.cs` | In `FilterConditionNode.BuildLinq()`, emit `.StartsWith(@N)`, `.EndsWith(@N)`, `.Contains(@N)` for string method operators |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/FilterBuilderPanel.razor` | Add `<option>` entries for `StartsWith`, `EndsWith`, `Contains`; show only when `selectedField.ValueType == typeof(string)` |

### Success Conditions

1. **Unit test** (`DdsMonitor.Engine.Tests`): `FilterConditionNode { FieldPath="Payload.Name", Operator=StartsWith, Value="Foo" }.BuildLinq()` returns `Payload.Name.StartsWith(@0)`.
2. **Unit test**: `FilterCompiler.Compile("Payload.Name.StartsWith(\"Foo\")", meta)` on a string field compiles to a valid predicate. A `SampleData` sample where `Payload.Name == "Foobar"` returns `true`; one where `Payload.Name == "BarFoo"` returns `false`.
3. **Manual**: In the `FilterBuilderPanel`, dropdown for a `string` field shows `Starts With`, `Ends With`, `Contains`. These options are absent for non-string fields.

---

## ME1-T05 — CLI-Safe Filter Operators

**Design ref:** [Phase 2.2 — CLI-Safe Filter Operators](./ME1-DESIGN.md#22-cli-safe-filter-operators-me1-t05)  
**Scope:** `DdsMonitor.Engine` (FilterCompiler.cs)

### Description

Shell-unfriendly characters (`<`, `>`) break command-line argument parsing. Users must be able to write:

```
--DdsSettings:FilterExpression="Ordinal ge 100 and DomainId eq 0"
```

The `FilterCompiler` normalizes `gt`, `lt`, `ge`, `le`, `eq`, `ne` (space-delimited) to their C# symbol equivalents before compilation.

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Engine/FilterCompiler.cs` | Add `OrdinalIgnoreCase` string replacements at the start of `Compile()` |

### Success Conditions

1. **Unit test**: `FilterCompiler.Compile("Ordinal ge 100", null)` compiles without error and the predicate returns `true` for a sample with `Ordinal == 150`, `false` for `Ordinal == 50`.
2. **Unit test**: `FilterCompiler.Compile("DomainId eq 0 and Ordinal lt 200", null)` returns `true` for `DomainId=0, Ordinal=100`; returns `false` for `DomainId=1, Ordinal=100`.
3. **Unit test**: A field named `message` (containing the letter sequence `ge`) is **not** corrupted by the replacement (replacement is space-bounded).
4. **Unit test**: Mixed case (`GE`, `Le`, `GT`) is also normalized correctly.

---

## ME1-T06 — Multi-Participant Reception

**Design ref:** [Phase 3.1 — Multi-Participant Reception](./ME1-DESIGN.md#31-multi-participant-reception-me1-t06)  
**Scope:** `DdsMonitor.Engine` (DdsSettings, IDdsBridge, DdsBridge, ServiceCollectionExtensions)

### Description

Replace the single `DomainId` / `CurrentPartition` in the engine with a list of `ParticipantConfig`. The bridge creates one `DdsParticipant` per entry. Subscriptions create one `DynamicReader<T>` per participant and funnel samples into the shared channel. `IsPaused` gates writes; `ResetAll()` clears stores and resets the global ordinal.

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Engine/DdsSettings.cs` | Add `List<ParticipantConfig> Participants`; keep `DomainId` as deprecated compat |
| `tools/DdsMonitor/DdsMonitor.Engine/IDdsBridge.cs` | Add `Participants`, `IsPaused`, `AddParticipant`, `RemoveParticipant`, `ResetAll` |
| `tools/DdsMonitor/DdsMonitor.Engine/DdsBridge.cs` | Implement multi-participant init, aggregated subscribe, pause, reset |
| `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs` | Pass `settings.Participants` to `DdsBridge` constructor |

### Success Conditions

1. **Unit test** (using mock `DdsParticipant` or integration): `DdsBridge` initialized with two `ParticipantConfig` entries creates two `DdsParticipant` instances.
2. **Unit test**: When `IsPaused = true`, no new `SampleData` items are written to the channel (existing in-flight samples may finish).
3. **Unit test**: `ResetAll()` invokes `ISampleStore.Clear()` and `IInstanceStore.Clear()`.
4. **Integration**: Application starts with `--DdsSettings:Participants:0:DomainId=0 --DdsSettings:Participants:1:DomainId=1` and both participants appear in `IDdsBridge.Participants`.
5. **Backward compat**: Application starts with the old `--DdsSettings:DomainId=3` single-domain config and still works (via deprecated compat shim or migration in `ServiceCollectionExtensions`).

---

## ME1-T07 — Global Sample Ordinal + Participant Stamping

**Design ref:** [Phase 3.2 — Global Ordinal + Participant Stamping](./ME1-DESIGN.md#32-global-sample-ordinal--participant-stamping-me1-t07)  
**Scope:** `DdsMonitor.Engine` (SampleData, SampleExportRecord, DynamicReader/DdsBridge, ImportService, ExportService), `DdsMonitor.Blazor` (DetailPanel.razor)

### Description

`SampleData` gains `uint DomainId`, `string PartitionName`, `int ParticipantIndex`. The global ordinal counter is shared across all participants. The filter is evaluated **before** ordinal allocation — non-matching samples consume no ordinal. Export/import round-trips the new fields. The `DetailPanel` shows them in the Sample Info tab.

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Engine/Models/SampleData.cs` | Add `DomainId`, `PartitionName`, `ParticipantIndex` |
| `tools/DdsMonitor/DdsMonitor.Engine/Import/SampleExportRecord.cs` | Mirror `DomainId`, `PartitionName` for JSON persistence |
| `tools/DdsMonitor/DdsMonitor.Engine/Dynamic/DynamicReader.cs` or `DdsBridge.cs` | Filter-first ordinal allocation; inject participant metadata |
| `tools/DdsMonitor/DdsMonitor.Engine/Export/ExportService.cs` | Serialize `DomainId`, `PartitionName` |
| `tools/DdsMonitor/DdsMonitor.Engine/Import/ImportService.cs` | Deserialize `DomainId`, `PartitionName` |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor` | Add Domain ID, Partition rows to Sample Info tab |

### Success Conditions

1. **Unit test**: Two samples generated by two different-participant readers receive different `ParticipantIndex` values and correct `DomainId` / `PartitionName`.
2. **Unit test**: A sample that does not match the startup `FilterExpression` is not written to the channel and does not increment the global ordinal counter.
3. **Unit test**: `ExportService` serializes `DomainId` and `PartitionName`; `ImportService` deserializes them back correctly. Round-trip equality.
4. **Unit test**: `FilterCompiler.Compile("DomainId eq 1", null)` correctly filters `SampleData` with `DomainId == 0` (returns `false`) vs `DomainId == 1` (returns `true`).
5. **Manual**: Exported JSON file contains `"DomainId":1` and `"PartitionName":"Sensors"` fields. `DetailPanel` Sample Info tab shows them.

---

## ME1-T08 — Union Arm Visibility

**Design ref:** [Phase 4.1 — Union Arm Visibility](./ME1-DESIGN.md#41-union-arm-visibility-me1-t08)  
**Scope:** `DdsMonitor.Engine` (FieldMetadata, TopicMetadata), `DdsMonitor.Blazor` (DynamicForm.razor, DetailPanel.razor)

### Description

For `[DdsUnion]` types, `FieldMetadata` now carries which discriminator field it depends on and which value activates it. The UI reads this to show only the active arm; Blazor re-renders automatically when the discriminator changes.

### Files to modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Engine/Metadata/FieldMetadata.cs` | Add `DependentDiscriminatorPath`, `ActiveWhenDiscriminatorValue`, `IsDefaultUnionCase`, `IsDiscriminatorField` |
| `tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs` | Detect `[DdsUnion]`; propagate discriminator path and case values to arms |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/DynamicForm.razor` | Skip inactive union arms during render |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor` | Filter tree nodes to active arm only |

### Success Conditions

1. **Unit test** (`DdsMonitor.Engine.Tests`): `TopicMetadata` for a `[DdsUnion]` type with discriminator `Kind` and two arms `[DdsCase(1)] int IntValue`, `[DdsCase(2)] float FloatValue` produces:
   - The `Kind` field has `IsDiscriminatorField == true`.
   - `IntValue` arm has `DependentDiscriminatorPath == "Kind"`, `ActiveWhenDiscriminatorValue == 1`.
   - `FloatValue` arm has `DependentDiscriminatorPath == "Kind"`, `ActiveWhenDiscriminatorValue == 2`.
2. **Unit test**: `TopicMetadata` for a `[DdsUnion]` with `[DdsDefaultCase] string Fallback` produces `IsDefaultUnionCase == true` for `Fallback`.
3. **Manual**: In `DynamicForm` with a union payload, changing the discriminator dropdown hides one arm and shows the other without page reload.
4. **Manual**: In `DetailPanel` tree view for a union-typed field, only the active arm's sub-node is visible; inactive arms are absent.

---

## ME1-T09 — Start/Pause/Reset Toolbar + Participant Editor

**Design ref:** [Phase 4.2 — Toolbar + Participant Editor](./ME1-DESIGN.md#42-startpausereset-toolbar--participant-editor-me1-t09)  
**Scope:** `DdsMonitor.Blazor` (MainLayout.razor, new ParticipantEditorDialog.razor), `DdsMonitor.Engine` (EventBrokerEvents.cs)

**Depends on:** ME1-T06 (for `IsPaused`, `ResetAll`, `AddParticipant`, `RemoveParticipant`, `Participants`)

### Description

Three tape-recorder-style icon buttons (▶ Start, ⏸ Pause, ⏹ Reset) are placed in the main toolbar after the menu items. A `Listening: D:0,1 | P:*,Sensors` indicator opens the `ParticipantEditorDialog`. The dialog also appears from the `Windows` menu item.

### Files to create / modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Engine/EventBrokerEvents.cs` | Add `record ParticipantsChangedEvent` |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/Layout/MainLayout.razor` | Add transport buttons + participant indicator |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/ParticipantEditorDialog.razor` | New modal dialog component |

### Success Conditions

1. **Manual**: ▶ Start button sets `IDdsBridge.IsPaused = false`; samples resume flowing.
2. **Manual**: ⏸ Pause button sets `IDdsBridge.IsPaused = true`; no new samples appear in the Samples panel.
3. **Manual**: ⏹ Reset button calls `IDdsBridge.ResetAll()`; all panels clear.
4. **Manual**: Clicking the participant indicator opens the dialog; adding a new domain/partition row and clicking OK creates a new participant in `DdsBridge`.
5. **Manual**: Dialog is also reachable via `Windows → Participant Editor…` menu item.
6. **Unit test** (event broker): After `OK` in the dialog, `ParticipantsChangedEvent` is published via `IEventBroker`.

---

## ME1-T10 — Auto-Browser Open + HTTP-Only Lifecycle

**Design ref:** [Phase 5.1 — Auto-Browser + HTTP-Only](./ME1-DESIGN.md#51-auto-browser-open--http-only-lifecycle-me1-t10)  
**Scope:** `DdsMonitor.Engine` (new BrowserLifecycleOptions), `DdsMonitor.Blazor` (Program.cs, new services)

### Description

On launch, `ddsmon` auto-opens the default system browser at its local HTTP URL. It shuts down if no browser connects within `ConnectTimeout` seconds, or if all connected tabs close and none reconnect within `DisconnectTimeout` seconds. HTTPS support is removed.

### Files to create / modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Engine/Hosting/BrowserLifecycleOptions.cs` | New: `ConnectTimeout` and `DisconnectTimeout` int properties |
| `tools/DdsMonitor/DdsMonitor.Blazor/Services/BrowserTrackingCircuitHandler.cs` | New: `CircuitHandler` subclass; raises `ConnectionChanged` event |
| `tools/DdsMonitor/DdsMonitor.Blazor/Services/BrowserLifecycleService.cs` | New: `BackgroundService` managing connect/disconnect timers |
| `tools/DdsMonitor/DdsMonitor.Blazor/Program.cs` | Force HTTP, bind to port 0, launch browser, replace `app.Run()` with `StartAsync`+`WaitForShutdownAsync`, register new services |

### Success Conditions

1. **Manual**: Running `ddsmon.exe` without arguments opens the browser within 2 seconds.
2. **Manual**: Closing the browser tab; after `DisconnectTimeout` (default 5 s) the process exits cleanly.
3. **Manual**: `--ConnectTimeout=5 --DisconnectTimeout=2` overrides default timeouts.
4. **Manual**: If the file is launched and the browser cannot open within `ConnectTimeout` seconds, the process exits.
5. **Manual**: The application URL is plain HTTP (`http://127.0.0.1:<port>`).

---

## ME1-T11 — Headless Recorder / Replay Mode

**Design ref:** [Phase 5.2 — Headless Recorder/Replay](./ME1-DESIGN.md#52-headless-recorder--replay-mode-me1-t11)  
**Scope:** `DdsMonitor.Engine` (DdsSettings, DynamicReader/DdsBridge, ReplayEngine, new HeadlessRunnerService), `DdsMonitor.Blazor` (Program.cs)

**Depends on:** ME1-T05 (CLI-safe operators), ME1-T07 (filter-first ordinal)

### Description

`ddsmon` gains three new CLI settings (`HeadlessMode`, `HeadlessFilePath`, `FilterExpression`, `ReplayRate`). When `HeadlessMode=Record`, the process listens and writes a JSON stream to file; Ctrl+C stops it. When `HeadlessMode=Replay`, the process replays a JSON file to the live DDS network at `ReplayRate` speed, filtered by `FilterExpression`, then exits. The Blazor HTTP server is not started in headless mode.

### Files to create / modify

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Engine/DdsSettings.cs` | Add `HeadlessMode`, `HeadlessFilePath`, `FilterExpression`, `ReplayRate` |
| `tools/DdsMonitor/DdsMonitor.Engine/Replay/ReplayEngine.cs` | Apply filter predicate immediately after import; `SpeedMultiplier` already exists, verify it divides delay correctly |
| `tools/DdsMonitor/DdsMonitor.Engine/HeadlessRunnerService.cs` | New: `BackgroundService`, implements record and replay orchestration |
| `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs` | Register `HeadlessRunnerService` if headless mode is active |
| `tools/DdsMonitor/DdsMonitor.Blazor/Program.cs` | Skip Blazor setup when `HeadlessMode != None` |

### Success Conditions

1. **Unit test** (`DdsMonitor.Engine.Tests`): `ReplayEngine` loaded with 5 samples, filter predicate that matches 3 of them; `FilteredTotalCount == 3`; playback only dispatches those 3 samples.
2. **Unit test**: `ReplayEngine.SpeedMultiplier = 2.0` halves the inter-sample delay (verify via mocked `Task.Delay` or timer inspection).
3. **Integration test**: Run `HeadlessRunnerService` in `Replay` mode against a test JSON file; verify the correct number of samples are dispatched via `IDyamicWriter.Write()`.
4. **Integration test**: Run in `Record` mode with a 2-second time limit; verify the output JSON file is non-empty and parseable by `ImportService`.
5. **CLI smoke test**: Passing `--DdsSettings:HeadlessMode=Replay --DdsSettings:ReplayRate=2.0 --DdsSettings:FilterExpression="Ordinal ge 1"` does not crash on startup.
6. **Manual**: HTTP server is **not** started in headless mode (no browser opens, no port is bound).
