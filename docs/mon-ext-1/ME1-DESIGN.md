# ME1 — Monitoring Extensions 1: Design Document

**Workstream:** DDS Monitor Feature Extensions  
**Prefix:** ME1-  
**Status:** Design Phase  
**Last Updated:** 2026-03-14

**Documents:**
- This file: Design reference (read first)
- [ME1-TASK-DETAILS.md](./ME1-TASK-DETAILS.md) — Per-task implementation specs
- [ME1-TASK-TRACKER.md](./ME1-TASK-TRACKER.md) — Progress board

---

## Overview

This workstream delivers eleven incremental feature extensions to the CycloneDDS.NET stack and the DDS Monitor tool. They span:

- **Code generation improvements** (`CycloneDDS.CodeGen`): typed enum bit-bounds, `[InlineArray]` support, default topic naming
- **Filter engine enhancements** (`DdsMonitor.Engine`): `StartsWith`/`EndsWith` in the visual builder, CLI-safe alphabetical operators
- **Engine architecture** (`DdsMonitor.Engine`): multi-participant ingestion, global sample ordinal, headless record/replay
- **UI/UX improvements** (`DdsMonitor.Blazor`): union arm visibility, Start/Pause/Reset toolbar, participant editor dialog, auto-browser lifecycle

All changes must align with the current codebase.  No legacy `unsafe` patterns are introduced; existing tests must continue to pass.

---

## Phase 1 — CodeGen & Schema Core

### 1.1 Typed Enum `@bit_bound` (ME1-T01)

**Goal:** When a C# enum specifies an underlying type (`public enum ESomething : byte`), emit the corresponding `@bit_bound` annotation in the generated IDL, and use the matching read/write width in the serializer.

**Background (current state):**  
`TypeInfo` (`tools/CycloneDDS.CodeGen/TypeInfo.cs`) has `IsEnum` and `EnumMembers` but no `EnumBitBound` property.  
`IdlEmitter.cs` emits enums without any annotation.  
`SerializerEmitter.cs` likely always reads/writes enums as 32-bit ints.

**Changes required:**

1. **`TypeInfo.cs`** — add `int EnumBitBound { get; set; } = 32;`.

2. **`SchemaDiscovery.cs`** — when populating a `TypeInfo` of kind `IsEnum`, inspect `INamedTypeSymbol.EnumUnderlyingType`:
   - `byte` / `sbyte` → `EnumBitBound = 8`
   - `short` / `ushort` → `EnumBitBound = 16`
   - anything else → `EnumBitBound = 32` (default, no annotation needed)

3. **`IdlEmitter.cs`** — before emitting `enum <Name> {`, conditionally prepend:
   ```
   @bit_bound(8)   // only when EnumBitBound == 8
   @bit_bound(16)  // only when EnumBitBound == 16
   ```
   Default (32) requires no annotation.

4. **`SerializerEmitter.cs`** — route enum fields through the correct primitive width:
   - `EnumBitBound == 8`  → `writer.WriteUInt8((byte)value)` / `reader.ReadUInt8()`
   - `EnumBitBound == 16` → `writer.WriteUInt16((ushort)value)` / `reader.ReadUInt16()`
   - default → existing 32-bit path (no change needed)

**Cyclone DDS mapping contract:**  
`@bit_bound(8)` → `uint8_t`, `@bit_bound(16)` → `uint16_t`, default → `int32_t`.  The C# cast to `(byte)` / `(ushort)` must be applied before writing to maintain binary compatibility.

---

### 1.2 `[InlineArray]` Support (ME1-T02)

**Goal:** Support C# 12 `[InlineArray(N)]` struct arrays as a clean alternative to `unsafe fixed T[N]` buffers across the full pipeline: code generation, metadata extraction, JSON serialization, and UI rendering.

**Background (current state):**  
The current system  uses `unsafe fixed` buffers detected via `FixedBufferAttribute` on the field (`FixedElementField`). The `TopicMetadata.AppendFields` method already handles the `FixedBufferAttribute` branch. `FieldMetadata` has `IsFixedSizeArray` / `FixedArrayLength` / `ElementType`.

**Changes required:**

1. **`TopicMetadata.AppendFields`** (`tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs`)  
   After the existing `FixedBufferAttribute` check, add detection of `[InlineArray]`:
   - Check `memberType.GetCustomAttribute<System.Runtime.CompilerServices.InlineArrayAttribute>()`.
   - Extract `Length` from the attribute, get the single defined field inside the struct as `elementType`.
   - Build a `FieldMetadata` with `isFixedSizeArray: true`, `elementType`, `fixedArrayLength = Length`.
   - The getter must yield a `T[]` snapshot (via `MemoryMarshal.CreateSpan`), matching the contract of `IsFixedSizeArray`.

2. **`FixedBufferJsonConverter<T>`** (`tools/DdsMonitor/DdsMonitor.Engine/Json/FixedBufferJsonConverter.cs`)  
   The factory checks for the `FixedBufferAttribute` sentinel field named `FixedElementField`.  
   Extend the detection to also match types with `[InlineArray]`:
   - Cast the struct instance to `Span<TElem>` via `MemoryMarshal.CreateSpan`.
   - Use the span to read/write elements during JSON serialization.

3. **`SerializerEmitter.cs`** / **`ViewEmitter.cs`** (`tools/CycloneDDS.CodeGen/...`)  
   When an `[InlineArray]` field is encountered, generate serializer code that iterates through the span rather than through unsafe pointer arithmetic. The IDL representation remains `array<T, N>` as today.

4. **`DynamicForm.razor`** (`tools/DdsMonitor/DdsMonitor.Blazor/Components/DynamicForm.razor`)  
   No rendering changes are required because the UI already reads `IsFixedSizeArray` from `FieldMetadata`. Step 1 above ensures `[InlineArray]` fields are tagged correctly.  
   **Data binding** requires care: because `[InlineArray]` is a value-type struct, the setter delegate must unbox the parent struct, cast it to `Span<T>`, update the specific index, and write the entire struct back.

5. **`CycloneDDS.IdlImporter`** (`tools/CycloneDDS.IdlImporter/...`)  
   Long-term, the importer may generate `[InlineArray]` instead of `unsafe fixed`.  This is deferred to a follow-on workstream; current tasks only add *reading* support.

---

### 1.3 Default Topic Name from Namespace (ME1-T03)

**Goal:** Make the `topicName` parameter of `[DdsTopic]` optional. When omitted, the topic name defaults to the fully-qualified C# type name with dots replaced by underscores (e.g. `FeatureDemo.Scenarios.Chat.Message` → `FeatureDemo_Scenarios_Chat_Message`).

**Background (current state):**  
`DdsTopicAttribute` (`src/CycloneDDS.Schema/Attributes/TypeLevel/DdsTopicAttribute.cs`) has a single constructor that validates the string is non-null/non-empty.  
`TopicMetadata` reads `topicAttribute.TopicName` directly.

**Changes required:**

1. **`DdsTopicAttribute.cs`** — Make `topicName` nullable with a `null` default:
   ```csharp
   public DdsTopicAttribute(string? topicName = null) { TopicName = topicName; }
   ```
   Remove the `ArgumentException` guard; allow `null`.

2. **`TopicMetadata.cs`** — After reading the attribute, compute the resolved name:
   ```csharp
   TopicName = string.IsNullOrWhiteSpace(topicAttribute.TopicName)
       ? (topicType.FullName?.Replace('.', '_') ?? topicType.Name)
       : topicAttribute.TopicName;
   ```

3. **`SchemaDiscovery.cs`** (`tools/CycloneDDS.CodeGen/SchemaDiscovery.cs`) — Apply the same fallback when the Roslyn semantic model finds a `[DdsTopic]` attribute with no argument.

4. **`IdlEmitter.cs`** / **`SerializerEmitter.cs`** — Consume `finalTopicName` (computed in step 3) rather than attempting to dereference a potentially null attribute value. No structural change; just ensure the resolved name flows through.

**Impact:** Enables `StartsWith` filtering across whole topic families (e.g. filter on `FeatureDemo_Scenarios_` covers every sub-topic).

---

## Phase 2 — Filter Engine Enhancements

### 2.1 StartsWith / EndsWith Filter Operators (ME1-T04)

**Goal:** Expose the `StartsWith` and `EndsWith` operators (already defined in `FilterComparisonOperator`) to the end user via the visual filter builder.

**Background (current state):**  
`FilterComparisonOperator` in `tools/DdsMonitor/DdsMonitor.Engine/Filtering/FilterNodes.cs` already includes `StartsWith`, `EndsWith`, and `Contains`.  
`FilterConditionNode.BuildLinq()` likely does not yet format these as method calls.  
`FilterBuilderPanel.razor` likely does not yet expose them in the operator `<select>`.

**Changes required:**

1. **`FilterNodes.cs`** — In `FilterConditionNode.BuildLinq()`, add the method-call format for these operators:
   ```csharp
   FilterComparisonOperator.StartsWith => $"{FieldPath}.StartsWith(@{argIdx})",
   FilterComparisonOperator.EndsWith   => $"{FieldPath}.EndsWith(@{argIdx})",
   FilterComparisonOperator.Contains   => $"{FieldPath}.Contains(@{argIdx})",
   ```

2. **`FilterBuilderPanel.razor`** (`tools/DdsMonitor/DdsMonitor.Blazor/Components/FilterBuilderPanel.razor`)  
   Add `<option>` entries for `StartsWith`, `EndsWith`, and `Contains` to the operator dropdown.  
   Wrap them in a Razor `@if` block that checks `selectedField.ValueType == typeof(string)` (string-only operators).

**Note:** Dynamic LINQ (`System.Linq.Dynamic.Core`) natively supports `.StartsWith()`, `.EndsWith()`, `.Contains()` as method calls on string parameters, so no changes to the `FilterCompiler.cs` LINQ compilation are required.

---

### 2.2 CLI-Safe Filter Operators (ME1-T05)

**Goal:** Support alphabetical comparison operators (`gt`, `lt`, `ge`, `le`, `eq`, `ne`) in filter expressions so that filters can be passed on the command line without shell-escape issues.

**Background (current state):**  
`FilterCompiler.cs` does not normalize alphabetical operators.  
`System.Linq.Dynamic.Core` natively understands OData-style alphabetical operators, so they already work at the LINQ compilation level.

**Changes required:**

1. **`FilterCompiler.Compile()`** — At the start of the method, before passing the expression to Dynamic LINQ, apply a word-boundary normalization:
   ```csharp
   expression = expression
       .Replace(" ge ", " >= ", StringComparison.OrdinalIgnoreCase)
       .Replace(" le ", " <= ", StringComparison.OrdinalIgnoreCase)
       .Replace(" gt ", " > ",  StringComparison.OrdinalIgnoreCase)
       .Replace(" lt ", " < ",  StringComparison.OrdinalIgnoreCase)
       .Replace(" eq ", " == ", StringComparison.OrdinalIgnoreCase)
       .Replace(" ne ", " != ", StringComparison.OrdinalIgnoreCase);
   ```
   The spaces around the operator tokens prevent accidental replacement inside field names.

**Usage:**  
```
ddsmon.exe --DdsSettings:FilterExpression="Ordinal ge 100 and DomainId eq 0"
```

---

## Phase 3 — Engine Architecture Extensions

### 3.1 Multi-Participant Reception (ME1-T06)

**Goal:** Support listening to multiple DDS domains and partitions simultaneously using multiple `DdsParticipant` instances. Each participant has an assigned `DomainId` and `PartitionName`.

**Background (current state):**  
`IDdsBridge` exposes a single `DdsParticipant Participant` and `string? CurrentPartition`.  
`DdsBridge` creates exactly one `DdsParticipant` in its constructor.  
`DdsSettings` has a single `int DomainId`.

**Changes required:**

1. **`DdsSettings.cs`** — Replace single `DomainId` with a list of participant configs:
   ```csharp
   public class ParticipantConfig
   {
       public uint DomainId { get; set; } = 0;
       public string PartitionName { get; set; } = string.Empty;
   }

   public sealed class DdsSettings
   {
       public List<ParticipantConfig> Participants { get; set; } = new()
       {
           new ParticipantConfig { DomainId = 0, PartitionName = "" }
       };
       // ... existing properties, remove single DomainId ...
   }
   ```
   Backward compat: keep `DomainId` as a deprecated get-only computed from `Participants[0]`.

2. **`IDdsBridge.cs`** — Expand the interface:
   ```csharp
   IReadOnlyList<DdsParticipant> Participants { get; }
   bool IsPaused { get; set; }
   void AddParticipant(uint domainId, string partitionName);
   void RemoveParticipant(int participantIndex);
   void ResetAll();
   ```
   Keep `Participant` (single) as a backward-compat shortcut pointing to `Participants[0]`.

3. **`DdsBridge.cs`** — Initialize one `DdsParticipant` per entry in `DdsSettings.Participants`.  
   `Subscribe()` creates a `DynamicReader<T>` per participant and aggregates their sample streams into the shared `Channel<SampleData>`.  
   `IsPaused` gates the channel writes.  
   `ResetAll()` clears `ISampleStore`, `IInstanceStore`, and resets the global ordinal counter.

4. **`ServiceCollectionExtensions.cs`** — Pass the full `settings.Participants` list when constructing `DdsBridge`.

---

### 3.2 Global Sample Ordinal + Participant Stamping (ME1-T07)

**Goal:** Each `SampleData` carries a globally unique monotonic `Ordinal`, plus the originating `DomainId` and `PartitionName`. This data is persisted in export files and filterable.

**Background (current state):**  
`SampleData` already has `long Ordinal` but it is currently assigned per-topic or per-reader.  
`SampleExportRecord` does not carry `DomainId` or `PartitionName`.

**Changes required:**

1. **`SampleData.cs`** — Add fields:
   ```csharp
   public uint DomainId { get; init; }
   public string PartitionName { get; init; } = string.Empty;
   public int ParticipantIndex { get; init; }
   ```

2. **`SampleExportRecord.cs`** — Mirror the new fields for JSON persistence:
   ```csharp
   public uint DomainId { get; set; }
   public string PartitionName { get; set; } = string.Empty;
   ```

3. **`DynamicReader.cs`** / **`DdsBridge.cs`** — Maintain a single `static long _globalOrdinal` (or inject an `IOrdinalService`).  Evaluate the pre-compiled filter **before** incrementing the ordinal:
   ```csharp
   // Stage 1: build temp sample (ordinal = 0)
   var temp = new SampleData { Ordinal = 0, DomainId = ..., PartitionName = ..., Payload = ... };
   if (!_predicate(temp)) return;                         // reject without allocating ordinal
   long ordinal = Interlocked.Increment(ref _globalOrdinal);
   var final = temp with { Ordinal = ordinal };
   _channelWriter.TryWrite(final);
   ```

4. **`FilterCompiler.cs`** — Because `DomainId`, `PartitionName`, and `Ordinal` are top-level properties of `SampleData`, they are automatically addressable in filter expressions without any code change. Verify that `PayloadFieldRegex` does not inadvertently rewrite these names (it targets `Payload.*` only, so they are safe).

5. **`ImportService.cs`** / **`ExportService.cs`** — Round-trip the new fields in JSON import/export.

6. **`DetailPanel.razor`** — Add new rows to the "Sample Info" tab:
   - Global Ordinal
   - Incoming Timestamp (already `Timestamp`, ensure field label reflects it)
   - Domain ID
   - Partition (show `<default>` when empty)

---

## Phase 4 — DDS Monitor UI

### 4.1 Union Arm Visibility (ME1-T08)

**Goal:** In both the `DynamicForm` (send panel) and the `DetailPanel` (inspect panel), display only the union arm that matches the current discriminator value. Inactive arms are hidden.

**Background (current state):**  
`DdsUnionAttribute`, `DdsDiscriminatorAttribute`, `DdsCaseAttribute`, and `DdsDefaultCaseAttribute` are all defined in `src/CycloneDDS.Schema`.  
`TopicMetadata.AppendFields` currently flattens all fields of a union type without filtering. `FieldMetadata` has no union-specific properties.

**Changes required:**

1. **`FieldMetadata.cs`** — Add union-awareness properties:
   ```csharp
   public string? DependentDiscriminatorPath { get; }   // path of the governing discriminator field
   public object? ActiveWhenDiscriminatorValue { get; } // from [DdsCase(value)]
   public bool IsDefaultUnionCase { get; }              // from [DdsDefaultCase]
   public bool IsDiscriminatorField { get; }            // field is [DdsDiscriminator]
   ```
   Extend the constructor with these parameters (all nullable/defaulted; non-union fields pass `null`/`false`).

2. **`TopicMetadata.AppendFields`** — When recursing into a type annotated with `[DdsUnion]`:
   - Identify the discriminator field (`[DdsDiscriminator]`); record its `structuredName` as `discriminatorPath`.
   - For each non-discriminator member, read `[DdsCase]` and `[DdsDefaultCase]` and pass them to `FieldMetadata`.
   - Pass `discriminatorPath` into each arm's `FieldMetadata` constructor.

3. **`DynamicForm.razor`** — In the field iteration loop, skip arms where `DependentDiscriminatorPath != null` and the live discriminator value does not match `ActiveWhenDiscriminatorValue`. Re-render automatically on discriminator change (Blazor reactivity via `OnChange` callback).

4. **`DetailPanel.razor`** — When building the object tree for a `[DdsUnion]` type:
   - Read the discriminator field value via reflection.
   - Yield only the discriminator itself + the matching case arm (or the `[DdsDefaultCase]` arm if none match).
   - Skip all other arms.

---

### 4.2 Start/Pause/Reset Toolbar + Participant Editor (ME1-T09)

**Goal:** Expose transport controls (▶ Start, ⏸ Pause, ⏹ Reset) and a live participant configuration dialog directly from the main application toolbar.

**Background (current state):**  
`MainLayout.razor` contains the main menu bar (`app-toolbar`/`app-menu`).  
`IDdsBridge` does not yet expose `IsPaused` or `ResetAll()` (added in **ME1-T06**).

**Changes required:**

1. **`MainLayout.razor`** — After the existing menu items, inject into `app-toolbar`:
   - Three transport icon buttons (Start, Pause, Reset) wired to `IDdsBridge.IsPaused` setters and `IDdsBridge.ResetAll()`.
   - A `Listening: D:<domains> | P:<partitions>` indicator button that opens the participant editor.  Uses `IDdsBridge.Participants` to build the summary.
   - Add `Participant Editor…` item to the `Windows` dropdown, calling the same dialog.

2. **`ParticipantEditorDialog.razor`** (new component) — Modal dialog using the existing `.file-dialog__backdrop` / `.file-dialog__window` CSS classes.  
   Layout (wireframe):
   ```
   +--------------------------------------------------------------------+
   | Participant Editor                                             [X] |
   +--------------------------------------------------------------------+
   | Domain ID     | Partition Name                 |                   |
   |--------------------------------------------------------------------+
   | [ 0         ] | [ <default>                  ] | [ Remove ]        |
   | [ 1         ] | [ SensorNetwork              ] | [ Remove ]        |
   +--------------------------------------------------------------------+
   | [ + Add Participant ]                                              |
   +--------------------------------------------------------------------+
   |                                          [ Cancel ]  [ OK ]        |
   +--------------------------------------------------------------------+
   ```
   On **OK**: diff the participant list; call `IDdsBridge.AddParticipant()` / `RemoveParticipant()` for changes; publish `ParticipantsChangedEvent` via `IEventBroker`.

3. **`EventBrokerEvents.cs`** — Add `ParticipantsChangedEvent` record.

4. **CLI args** — Participants can be seeded at startup via .NET configuration:  
   `--DdsSettings:Participants:0:DomainId=0 --DdsSettings:Participants:1:DomainId=1 --DdsSettings:Participants:1:PartitionName=Sensors`

---

## Phase 5 — Lifecycle & Headless Mode

### 5.1 Auto-Browser Open + HTTP-Only Lifecycle (ME1-T10)

**Goal:** When launched without arguments, `ddsmon` opens the default web browser at its local HTTP address and shuts down when the browser tab is closed. HTTPS is removed for simplicity. Command-line options control the connect/disconnect timeouts.

**Background (current state):**  
`Program.cs` (`tools/DdsMonitor/DdsMonitor.Blazor/Program.cs`) calls `app.Run()` synchronously and has no browser auto-open or lifecycle logic.  The app currently supports HTTPS based on the default ASP.NET template.

**Changes required:**

1. **`BrowserLifecycleOptions.cs`** (new, `tools/DdsMonitor/DdsMonitor.Engine/Hosting/`) — Configuration class:
   ```csharp
   public class BrowserLifecycleOptions
   {
       public int ConnectTimeout    { get; set; } = 15;  // seconds to wait for first browser connection
       public int DisconnectTimeout { get; set; } = 5;   // seconds to wait after last tab closes
   }
   ```

2. **`BrowserTrackingCircuitHandler.cs`** (new, `tools/DdsMonitor/DdsMonitor.Blazor/Services/`) — Blazor `CircuitHandler` subclass. Raises `ConnectionChanged(bool isConnected)` when the first circuit connects or all circuits disconnect.

3. **`BrowserLifecycleService.cs`** (new, `tools/DdsMonitor/DdsMonitor.Blazor/Services/`) — `BackgroundService` that:
   - Starts a connect-timeout timer on boot.
   - Cancels the timer on first connection.
   - Starts a disconnect-timeout timer when all connections drop.
   - Calls `IHostApplicationLifetime.StopApplication()` when a timeout fires.

4. **`Program.cs`** — Replace `app.Run()` with `app.StartAsync()` + browser launch + `app.WaitForShutdownAsync()`. Force HTTP-only with a dynamic port (`http://127.0.0.1:0`). Register `BrowserTrackingCircuitHandler` as both a `CircuitHandler` singleton and the class itself, plus `BrowserLifecycleService` as a hosted service.

---

### 5.2 Headless Recorder / Replay Mode (ME1-T11)

**Goal:** Run `ddsmon` in headless mode for CI/scripted workflows: either **record** live DDS traffic to a JSON file, or **replay** a JSON file back to the network — both honoring a filter expression and replay-rate multiplier.

**Background (current state):**  
`ReplayEngine` handles replay with `SpeedMultiplier`. The existing `IExportService` / `IImportService` can export/import JSON files.  
`DdsSettings` has no headless mode fields.

**Changes required:**

1. **`DdsSettings.cs`** — Add headless options:
   ```csharp
   public enum HeadlessMode { None, Record, Replay }

   // In DdsSettings:
   public HeadlessMode HeadlessMode { get; set; } = HeadlessMode.None;
   public string HeadlessFilePath { get; set; } = string.Empty;
   public string FilterExpression { get; set; } = string.Empty;
   public float ReplayRate { get; set; } = 1.0f;
   ```

2. **`DynamicReader.cs`** / **`DdsBridge.cs`** — Filter-first ordinal allocation (see **ME1-T07**). When `DdsSettings.FilterExpression` is set, compile the predicate at startup and apply it before incrementing the global ordinal.

3. **`ReplayEngine.cs`** — Extend `PlayAsync` / `LoadAsync` to accept a filter predicate and apply it immediately after import, so skipped frames do not affect timing or frame counts. The existing `SpeedMultiplier` doubling already uses `(delay / multiplier)`; verify the implementation matches design intent.

4. **`HeadlessRunnerService.cs`** (new, `tools/DdsMonitor/DdsMonitor.Engine/`) — `BackgroundService`:
   - If `HeadlessMode == None`, returns immediately (UI takes over).
   - If `HeadlessMode == Replay`: calls `ReplayEngine.PlayAsync(filePath, filter, replayRate)` to the live DDS network, then calls `StopApplication()`.
   - If `HeadlessMode == Record`: opens a `FileStream`, serializes incoming `SampleData` as a streaming JSON array (same format as `ExportService`), until `Ctrl+C` / cancellation.

5. **`Program.cs`** — Register `HeadlessRunnerService`.  For headless mode, skip Blazor middleware setup and HTTP binding:
   ```csharp
   if (settings.HeadlessMode == HeadlessMode.None)
   {
       app.UseStaticFiles();
       app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
       await app.StartAsync();
       // browser auto-open (ME1-T10) ...
       await app.WaitForShutdownAsync();
   }
   else
   {
       await app.RunAsync(); // starts hosted services only
   }
   ```

**CLI examples:**
```bash
# Record all traffic matching a topic family, for 60 seconds, then Ctrl+C
ddsmon.exe --DdsSettings:HeadlessMode=Record --DdsSettings:HeadlessFilePath=capture.json ^
           --DdsSettings:FilterExpression="TopicTypeName.StartsWith('FeatureDemo_')"

# Replay at 2x speed, only samples with Ordinal >= 100
ddsmon.exe --DdsSettings:HeadlessMode=Replay --DdsSettings:HeadlessFilePath=capture.json ^
           --DdsSettings:ReplayRate=2.0 --DdsSettings:FilterExpression="Ordinal ge 100"
```

---

## Cross-Cutting Concerns

### File/Type Locations

| Component | Path |
|---|---|
| `DdsTopicAttribute` | `src/CycloneDDS.Schema/Attributes/TypeLevel/DdsTopicAttribute.cs` |
| `TypeInfo` | `tools/CycloneDDS.CodeGen/TypeInfo.cs` |
| `SchemaDiscovery` | `tools/CycloneDDS.CodeGen/SchemaDiscovery.cs` |
| `IdlEmitter` | `tools/CycloneDDS.CodeGen/IdlEmitter.cs` |
| `SerializerEmitter` | `tools/CycloneDDS.CodeGen/SerializerEmitter.cs` |
| `TopicMetadata` | `tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs` |
| `FieldMetadata` | `tools/DdsMonitor/DdsMonitor.Engine/Metadata/FieldMetadata.cs` |
| `FilterNodes` | `tools/DdsMonitor/DdsMonitor.Engine/Filtering/FilterNodes.cs` |
| `FilterCompiler` | `tools/DdsMonitor/DdsMonitor.Engine/FilterCompiler.cs` |
| `SampleData` | `tools/DdsMonitor/DdsMonitor.Engine/Models/SampleData.cs` |
| `SampleExportRecord` | `tools/DdsMonitor/DdsMonitor.Engine/Import/SampleExportRecord.cs` |
| `DdsBridge` | `tools/DdsMonitor/DdsMonitor.Engine/DdsBridge.cs` |
| `IDdsBridge` | `tools/DdsMonitor/DdsMonitor.Engine/IDdsBridge.cs` |
| `DdsSettings` | `tools/DdsMonitor/DdsMonitor.Engine/DdsSettings.cs` |
| `DynamicReader` | `tools/DdsMonitor/DdsMonitor.Engine/Dynamic/DynamicReader.cs` |
| `ReplayEngine` | `tools/DdsMonitor/DdsMonitor.Engine/Replay/ReplayEngine.cs` |
| `EventBrokerEvents` | `tools/DdsMonitor/DdsMonitor.Engine/EventBrokerEvents.cs` |
| `ServiceCollectionExtensions` | `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs` |
| `Program.cs` | `tools/DdsMonitor/DdsMonitor.Blazor/Program.cs` |
| `MainLayout.razor` | `tools/DdsMonitor/DdsMonitor.Blazor/Components/Layout/MainLayout.razor` |
| `DynamicForm.razor` | `tools/DdsMonitor/DdsMonitor.Blazor/Components/DynamicForm.razor` |
| `DetailPanel.razor` | `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor` |
| `FilterBuilderPanel.razor` | `tools/DdsMonitor/DdsMonitor.Blazor/Components/FilterBuilderPanel.razor` |

### Dependency Order

Tasks have the following hard dependencies:
- **ME1-T07** depends on **ME1-T06** (participants must exist before stamping)
- **ME1-T09** depends on **ME1-T06** (IsPaused/ResetAll/Participants needed)
- **ME1-T11** depends on **ME1-T05** (CLI-safe operators needed for headless filter)
- **ME1-T10** and **ME1-T11** both touch `Program.cs` — coordinate or implement T10 first

### Testing Strategy

- **Unit tests** (xUnit) for: `FilterCompiler` operator normalization (T05), `FilterNodes` method-call format (T04), `TopicMetadata` inline-array detection (T02), `TopicMetadata` union arm metadata (T08), `DdsTopicAttribute` optional name + fallback logic (T03).
- **Code-gen tests** (existing `CycloneDDS.CodeGen.Tests`): add cases for `@bit_bound` emission (T01), for default topic naming in IDL output (T03).
- **Integration / self-send**: headless record → count ordinals; headless replay → verify sample count against filter (T11).
- **Manual**: browser lifecycle (T10), participant editor (T09), union arm hiding in UI (T08).
