# ME2 — Onboarding Guide

Welcome to the **Monitoring Extensions 2 (ME2)** workstream for `CycloneDDS.NET`.  
This document helps a new developer get oriented quickly.

> **Before writing any code**, read the developer guide:  
> [`.dev-workstream/guides/DEV-GUIDE.md`](../../.dev-workstream/guides/DEV-GUIDE.md)

---

## What We Are Building

This workstream delivers fourteen targeted improvements to the DDS Monitor tool and a quick fix to the CycloneDDS code generator.

### 1 — Bug Fixes (Phase 1)

| # | Feature | Summary |
|---|---|---|
| T01 | Workspace type-name compat | Store panel type `FullName` (not `AssemblyQualifiedName`) so workspaces survive version upgrades |
| T02 | Reset keeps subscriptions | ⏹ Reset clears sample history without disposing readers; Subscribe state is preserved |
| T03 | Ordinal sort fix | The sort in All Samples panel actually works in the all-topics view |
| T04 | Timestamp formatting | All timestamps show human-readable local time; SourceTimestamp nanoseconds are decoded |

### 2 — Detail Panel Improvements (Phase 2)

| # | Feature | Summary |
|---|---|---|
| T05 | Null + type highlighting | `null` shows as coloured "null"; enums/booleans/numbers get distinct CSS colours |
| T06 | Union rendering | Table tab expands union list items; Tree + Table show discriminator value, not type name |

### 3 — CodeGen Quick Fix (Phase 3)

| # | Feature | Summary |
|---|---|---|
| T07 | Build log project name | `Running CycloneDDS Code Generator (Incremental) for ProjectName...` in MSBuild output |

### 4 — Filter & Column System (Phase 4)

| # | Feature | Summary |
|---|---|---|
| T08 | Non-payload filter fields | Filter by `Sample.Topic` and `Sample.InstanceState` in addition to payload fields |
| T09 | "Filter Out Topic" menu | Right-click any sample to exclude that topic from the current filter |
| T10 | Selectable metadata columns | Timestamp, Topic, Size, Delay become user-selectable columns; only Ordinal and Status are fixed |

### 5 — Samples Panel Track Mode (Phase 5)

| # | Feature | Summary |
|---|---|---|
| T11 | Sort fix + track mode | Full sort fix; autoscroll to latest sample; O(N) fast path for Ordinal/Timestamp sort |

### 6 — Topic Properties Panel (Phase 6)

| # | Feature | Summary |
|---|---|---|
| T12 | TopicPropertiesPanel | New window showing DDS name, CLR type, extensibility, size, field table |
| T13-A | TopicExplorer right-click | Right-click topic in TopicExplorer → "Topic Properties" |
| T13-B | TopicSources improvements | Alphabetical sort; namespace on second line; right-click → "Topic Properties" |

### 7 — Folder Scanning (Phase 7)

| # | Feature | Summary |
|---|---|---|
| T14 | Folder assembly scan | Topic source can be a folder; all DLLs inside are scanned; assembly path shown in UI |

---

## Design & Task Documents

| Document | Purpose |
|---|---|
| [ME2-DESIGN.md](./ME2-DESIGN.md) | Full architecture — read this first |
| [ME2-TASK-DETAILS.md](./ME2-TASK-DETAILS.md) | Per-task implementation specs with success conditions |
| [ME2-TASK-TRACKER.md](./ME2-TASK-TRACKER.md) | Status board — find available tasks here |

**Workflow:** Read `ME2-DESIGN.md` end-to-end first. Pick a `[ ]` task in `ME2-TASK-TRACKER.md`. Read its full entry in `ME2-TASK-DETAILS.md` before writing a single line of code.

---

## Folder Layout — Where Things Live

```
tools/
  CycloneDDS.CodeGen/
    CycloneDDS.targets               ← ME2-T07: MSBuild message improvement

  DdsMonitor/
    DdsMonitor.Engine/
      DdsBridge.cs                   ← ME2-T02: ResetAll – keep readers alive
      FilterCompiler.cs              ← ME2-T08: PayloadFieldRegex – add Sample. prefix
      WindowManager.cs               ← ME2-T01: ResolveComponentTypeName – FullName
      AssemblyScanner/
        AssemblySourceService.cs     ← ME2-T14: ScanEntry – folder scanning
      Metadata/
        FieldMetadata.cs             ← (read-only reference: IsWrapperField, union arm properties)
        TopicMetadata.cs             ← ME2-T08: AppendSyntheticFields – Topic + InstanceState
                                        ME2-T14: AssemblyPath property
      Ui/
        FieldPickerFilter.cs         ← ME2-T08: Matches – prefix-aware search

    DdsMonitor.Blazor/
      Components/
        DetailPanel.razor            ← ME2-T04: RenderSampleInfo timestamps
                                        ME2-T05: RenderValue – null + type highlighting
                                        ME2-T06: GetUnionInfo, IsUnionArmVisible, RenderTableView, RenderNode
        SamplesPanel.razor           ← ME2-T03/T11: EnsureView sort fix + track mode
                                        ME2-T04: Timestamp cell formatting
                                        ME2-T09: ExcludeTopicFromFilter + context menu
                                        ME2-T10: ColumnKind simplification + column picker
        InstancesPanel.razor         ← ME2-T04: Time cell local formatting
                                        ME2-T09: ExcludeTopicFromFilter + context menu
        FilterBuilderPanel.razor     ← ME2-T08: ApplyField + GetFieldForCondition prefix handling
        FieldPicker.razor            ← ME2-T08: Sample./Payload. prefix display
        TopicExplorerPanel.razor     ← ME2-T01: FullName for panel spawning
                                        ME2-T13-A: right-click context menu
        TopicSourcesPanel.razor      ← ME2-T13-B: sort + namespace + context menu
                                        ME2-T14: 3-line CLR type display
        FileDialog.razor             ← ME2-T14: folder selection support
        TopicPropertiesPanel.razor   ← ME2-T12: NEW FILE
        Layout/
          MainLayout.razor           ← ME2-T01: FullName for all static panel type constants
      wwwroot/
        app.css                      ← ME2-T12: topic-properties CSS rules
```

---

## How to Build

```powershell
# From the workspace root
dotnet build CycloneDDS.NET.sln -c Release

# Run all tests
dotnet test CycloneDDS.NET.sln -c Release --nologo
```

The DDS Monitor application is in `tools/DdsMonitor/DdsMonitor.Blazor/`. Run it with:
```powershell
dotnet run --project tools/DdsMonitor/DdsMonitor.Blazor/DdsMonitor.csproj -c Release
```

The browser opens automatically at `http://localhost:5000` (or the configured port).

---

## Task Ordering Guidance

Tasks are mostly independent, but the following ordering is recommended:

1. **T01** first — all subsequent tasks that spawn panels must use `.FullName!`.
2. **T02** is isolated (DdsBridge only) — do early.
3. **T07** is trivial — good first task for a new developer.
4. **T05** before **T06** — T06 depends on `RenderValue` for union discriminator display.
5. **T08** before **T09** — T09 uses the `Sample.Topic` filter syntax introduced in T08.
6. **T08** and **T10** can be done in parallel (different methods).
7. **T11** supersedes **T03** — skip T03 if doing T11.
8. **T12** before **T13-A** and **T13-B** — the panel component must exist before it is wired up.
9. **T14** can be done independently at any point.
