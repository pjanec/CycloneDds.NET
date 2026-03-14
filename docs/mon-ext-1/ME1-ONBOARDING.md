# ME1 — Onboarding Guide

Welcome to the **Monitoring Extensions 1 (ME1)** workstream for `CycloneDDS.NET`.  
This document helps a new developer get oriented quickly.

> **Before writing any code**, read the developer guide:  
> [`.dev-workstream/guides/DEV-GUIDE.md`](../../.dev-workstream/guides/DEV-GUIDE.md)

---

## What We Are Building

This workstream delivers eleven incremental improvements across two areas:

### 1 — Code Generator (`CycloneDDS.CodeGen`)

| # | Feature | Summary |
|---|---|---|
| T01 | Typed enum `@bit_bound` | Map C# enum underlying type (`byte`, `short`) to IDL `@bit_bound(8/16)` and narrow serializer widths |
| T02 | `[InlineArray]` support | Treat C# 12 `[InlineArray(N)]` structs as fixed-size arrays everywhere (metadata, JSON, UI) |
| T03 | Default topic name | Make `[DdsTopic]` optional; fall back to the full namespace-qualified class name with `_` delimiters |

### 2 — DDS Monitor (`DdsMonitor.Engine` + `DdsMonitor.Blazor`)

| # | Feature | Summary |
|---|---|---|
| T04 | StartsWith / EndsWith UI | Expose string-method filter operators in the visual query builder |
| T05 | CLI-safe operators | Support `gt`, `lt`, `ge`, `le`, `eq`, `ne` in CLI filter expressions |
| T06 | Multi-participant | Multiple `DdsParticipant` instances (domain+partition combos); pause/reset support |
| T07 | Global ordinal + stamping | Thread-safe global ordinal; per-sample `DomainId`/`PartitionName`; filter-first allocation |
| T08 | Union arm visibility | Show only the active discriminator arm in edit forms and the detail tree |
| T09 | Transport toolbar + participant editor | ▶ ⏸ ⏹ buttons + participant config dialog in the main menu bar |
| T10 | Auto-browser + HTTP lifecycle | HTTP-only, auto-open browser, shutdown when browser closes |
| T11 | Headless record/replay | CLI-driven recording to / replay from JSON without starting the Blazor UI |

---

## Design & Task Documents

| Document | Purpose |
|---|---|
| [ME1-DESIGN.md](./ME1-DESIGN.md) | Full architecture — read this first |
| [ME1-TASK-DETAILS.md](./ME1-TASK-DETAILS.md) | Per-task implementation specs with success conditions and test cases |
| [ME1-TASK-TRACKER.md](./ME1-TASK-TRACKER.md) | Status board — find available tasks here |

**Workflow:** Read `ME1-DESIGN.md` end-to-end first. Pick a `[ ]` task in `ME1-TASK-TRACKER.md`. Read its full entry in `ME1-TASK-DETAILS.md` before writing a single line of code.

---

## Folder Layout — Where Things Live

```
src/
  CycloneDDS.Schema/
    Attributes/TypeLevel/
      DdsTopicAttribute.cs           ← ME1-T03: make topicName optional
    Attributes/UnionSpecific/
      DdsCaseAttribute.cs            ← ME1-T08: [DdsCase] for discriminator arms
      DdsDefaultCaseAttribute.cs     ← ME1-T08: fallback arm
      DdsDiscriminatorAttribute.cs   ← ME1-T08: the discriminator field

tools/
  CycloneDDS.CodeGen/
    TypeInfo.cs                      ← ME1-T01: add EnumBitBound
    SchemaDiscovery.cs               ← ME1-T01, T02, T03: Roslyn enum/inline-array/topic-name analysis
    IdlEmitter.cs                    ← ME1-T01, T03: emit @bit_bound, default topic name
    SerializerEmitter.cs             ← ME1-T01, T02: narrow enum I/O, span-based InlineArray

  DdsMonitor/
    DdsMonitor.Engine/
      Models/SampleData.cs           ← ME1-T07: DomainId, PartitionName, ParticipantIndex
      Filtering/FilterNodes.cs       ← ME1-T04: BuildLinq for StartsWith/EndsWith/Contains
      FilterCompiler.cs              ← ME1-T05: alphabetical operator normalization
      DdsSettings.cs                 ← ME1-T06, T11: ParticipantConfig list, HeadlessMode
      IDdsBridge.cs                  ← ME1-T06: IsPaused, AddParticipant, RemoveParticipant, ResetAll
      DdsBridge.cs                   ← ME1-T06, T07: multi-participant init, filter-first ordinal
      Dynamic/DynamicReader.cs       ← ME1-T07: ordinal allocation gate
      Metadata/FieldMetadata.cs      ← ME1-T02, T08: InlineArray + union arm properties
      Metadata/TopicMetadata.cs      ← ME1-T02, T03, T08: detection + fallback name + union scanning
      Import/SampleExportRecord.cs   ← ME1-T07: DomainId, PartitionName persistence
      Replay/ReplayEngine.cs         ← ME1-T11: filter predicate applied after import
      HeadlessRunnerService.cs       ← ME1-T11: new file — record/replay orchestrator
      EventBrokerEvents.cs           ← ME1-T09: ParticipantsChangedEvent
      Json/FixedBufferJsonConverter.cs ← ME1-T02: [InlineArray] span JSON access
      Hosting/
        ServiceCollectionExtensions.cs ← ME1-T06: pass participant list to DdsBridge
        BrowserLifecycleOptions.cs   ← ME1-T10: new file — connect/disconnect timeouts

    DdsMonitor.Blazor/
      Program.cs                     ← ME1-T10, T11: HTTP-only, browser launch, headless branch
      Components/
        FilterBuilderPanel.razor     ← ME1-T04: string operator <option> entries
        DynamicForm.razor            ← ME1-T08: skip inactive union arms
        DetailPanel.razor            ← ME1-T07, T08: domain/partition rows; union tree filter
        Layout/MainLayout.razor      ← ME1-T09: transport buttons + participant indicator
        ParticipantEditorDialog.razor ← ME1-T09: new file — participant editor modal
      Services/
        BrowserTrackingCircuitHandler.cs ← ME1-T10: new file — Blazor CircuitHandler
        BrowserLifecycleService.cs   ← ME1-T10: new file — BackgroundService
```

---

## How To Build

```powershell
# From the repo root:

# Build the full solution
dotnet build CycloneDDS.NET.sln -c Release

# Build only the core (no DdsMonitor)
dotnet build CycloneDDS.NET.Core.slnf -c Release

# Run all unit tests (core slnf)
dotnet test CycloneDDS.NET.Core.slnf -c Release --no-build

# Run DDS Monitor engine tests only
dotnet test tests/DdsMonitor.Engine.Tests/ -c Release --no-build

# Launch DDS Monitor (interactive mode)
dotnet run --project tools/DdsMonitor/DdsMonitor.Blazor -c Release
```

> Native CycloneDDS binaries for Windows are pre-built in `artifacts/native/win-x64/`. The build system references them via `Directory.Build.props` so no separate native build is required for day-to-day development.

---

## Task Execution Checklist

1. Read `ME1-DESIGN.md` (the architecture chapter for your task).
2. Read the full task entry in `ME1-TASK-DETAILS.md`.
3. Understand the success conditions — they define what your tests must prove.
4. Implement + write tests.
5. Run the test suite to verify all success conditions are met and no previously passing tests regressed.
6. Mark the task `[x]` in `ME1-TASK-TRACKER.md`.
7. Submit your batch report per the process in `.dev-workstream/guides/DEV-GUIDE.md`.
