# Onboarding — DDS Monitor

Welcome to the **DDS Monitor** workstream for `CycloneDDS.NET`.  
This document helps you get oriented quickly so you can start contributing.

> **Before writing any code**, read the developer guide:  
> [`.dev-workstream/guides/DEV-GUIDE.md`](../../.dev-workstream/guides/DEV-GUIDE.md)

---

## What We Are Building

A **ground-up reimplementation** of the original `ddsmon` desktop monitoring tool. The original was built on Veldrid/ImGui with the older `company.DDS` wrapper — **its source code is not available**. We are rebuilding from scratch based on the design talk, using a modern stack:

| Original | New |
|---|---|
| ImGui + Veldrid (C++) | **Blazor Server** (C# backend, HTML/CSS/JS frontend) |
| company.DDS (old wrapper) | **CycloneDDS.NET** (this repo's strongly-typed generic library) |
| Single-machine only | **Remote monitoring** via HTTP — open from any browser on the network |
| Custom windowing | **Web Desktop** paradigm — floating, resizable, dockable panels in the browser |

The tool monitors DDS network traffic in real-time: subscribes to topics, displays samples in high-performance virtualized grids, tracks keyed instance lifecycles, supports filtering/sorting, message replay, import/export, and domain-specific entity aggregation via hot-loadable plugins.

---

## Design and Task Documents

All design/task documents live in `docs/ddsmon/`:

| Document | Purpose |
|---|---|
| [DESIGN.md](./DESIGN.md) | Consolidated design document — architecture, data pipeline, UI specs, plugin system |
| [TASK-DETAIL.md](./TASK-DETAIL.md) | Per-task implementation descriptions with success conditions and unit test specs |
| [TASK-TRACKER.md](./TASK-TRACKER.md) | Status board — check here before picking up a task |

**Workflow:** Read `DESIGN.md` end-to-end first. Then find a `[ ]` task in `TASK-TRACKER.md` and read its full entry in `TASK-DETAIL.md` before starting implementation.

---

## Folder Layout — Where Things Live

```
src/
  CycloneDDS.Runtime/          ← Main DDS runtime library (DdsReader, DdsWriter, DdsParticipant, …)
    Interop/
      DdsApi.cs                ← P/Invoke declarations for native CycloneDDS
    DdsReader.cs               ← Generic strongly-typed DDS reader
    DdsWriter.cs               ← Generic strongly-typed DDS writer
    DdsParticipant.cs          ← DDS participant wrapper

  CycloneDDS.Core/             ← CDR serialization, code generation attributes ([DdsTopic], [DdsKey])

tools/
  DdsMonitor/                  ← NEW — Blazor Server application (to be created in DMON-001)
  DdsMonitor/DdsMonitor.Engine/← NEW — Headless data engine library (no Blazor dependency)

tests/
  DdsMonitor.Engine.Tests/     ← NEW — Unit tests for the engine (to be created in DMON-001)
  CycloneDDS.Runtime.Tests/    ← Existing runtime tests — reference for test patterns

docs/
  ddsmon/                      ← Design & task documents for this workstream
    DESIGN.md                  ← Consolidated design
    TASK-DETAIL.md             ← Detailed task descriptions
    TASK-TRACKER.md            ← Task status board

artifacts/
  native/win-x64/             ← Pre-built native CycloneDDS DLLs (required at runtime)
```

---

## Key Dependencies

| Package | Used For |
|---|---|
| `CycloneDDS.Runtime` (this repo) | Strongly-typed DDS readers/writers, `WaitDataAsync`, zero-copy loans |
| `CycloneDDS.Core` (this repo) | `[DdsTopic]`, `[DdsKey]` attributes, CDR serialization |
| `Fasterflect.Netstandard` | IL-emitted delegates for field access — reflection cost paid once at startup |
| `System.Linq.Dynamic.Core` | Compile user-typed filter expressions into native predicates |
| `System.Threading.Channels` | Lock-free, zero-allocation producer/consumer queue for DDS → UI pipeline |

---

## How to Build

### Prerequisites

- **.NET 8 SDK** or later
- Native CycloneDDS binaries in `artifacts/native/win-x64/` (already checked in)

### Build & Run

```powershell
# Build the entire solution
dotnet build CycloneDDS.NET.sln

# Run the monitor (once DMON-001 is completed)
dotnet run --project tools/DdsMonitor/DdsMonitor.csproj

# Run engine tests
dotnet test tests/DdsMonitor.Engine.Tests/DdsMonitor.Engine.Tests.csproj
```

### Build scripts

```powershell
# Full build + test
./build/build-and-test.ps1
```

---

## Architecture at a Glance

The application has three layers (see [DESIGN.md §3](./DESIGN.md#3-architecture-overview)):

1. **Headless Data Engine** — Topic discovery, DDS bridge, SampleStore, InstanceStore, FilterCompiler, Import/Export, Replay. No Blazor dependency. Can run as CLI.
2. **Plugin Ecosystem** — Hot-loadable Razor Class Libraries for domain-specific features (BDC entities, TKB entities, custom formatters).
3. **Blazor Workspace Shell** — Web Desktop with floating panels, virtualized grids, master-detail linking.

**Data flow:** `Native DDS → DynamicReader<T> → Channel → DdsIngestionService → SampleStore/InstanceStore → Blazor UI (30 Hz)`

---

## Performance Constraints

These are not optional — they are core architectural decisions:

1. **Lock-free ingestion** — DDS threads never block on UI
2. **30 Hz UI throttle** — `StateHasChanged()` only when data changes, max 30 fps
3. **DOM virtualization** — `<Virtualize>` renders only visible rows
4. **Zero-reflection at render** — all field access via pre-compiled `Fasterflect` delegates
5. **Background sorting** — merge sorts run off the UI thread

---

## Getting Started — Pick a Task

1. Open [TASK-TRACKER.md](./TASK-TRACKER.md)
2. Find the first `[ ]` task in the current phase
3. Read its full description in [TASK-DETAIL.md](./TASK-DETAIL.md)
4. Implement it, including all specified unit tests
5. Mark the task `[x]` in the tracker when done
