# ME2-BATCH-07 Report

**Batch:** ME2-BATCH-07  
**Tasks:** ME2-T27, ME2-T14  
**Date:** 2026-03-20  
**Status:** Complete ✅

---

## Q1: Issues Encountered & Resolutions

### ME2-T27 — DI lifetime mismatch: Singleton vs Scoped

`TopicColorService` was registered as `AddSingleton` in `Program.cs` but its constructor accepted `IWorkspaceState`, which is a Scoped service (one per Blazor circuit). ASP.NET Core DI validates this at startup and throws `AggregateException` before the app can serve any requests.

**Resolution:** Changed the `Program.cs` registration from `AddSingleton<TopicColorService>` to `AddScoped<TopicColorService>`. This eliminates the lifetime violation with zero behavioral change: `TopicColorService` already maintains per-instance state (user color overrides dictionary + persist path) making per-circuit scoping correct and the singleton assumption was never required. The `FakeWorkspaceState` test helper in `ME2Batch06Tests.cs` continues to work unchanged.

### ME2-T14 — `AssemblySourceService` internal constructor for testability

`AssemblySourceService`'s production constructor computes the config file path from `%APPDATA%\DdsMonitor\`. Tests need isolation (separate temp directories per test case) to avoid cross-test interference and touching real user data. 

**Resolution:** Added an `internal` constructor overload accepting an explicit `configFilePath` parameter. An `[assembly: InternalsVisibleTo("DdsMonitor.Engine.Tests")]` attribute (new `AssemblyInfo.cs` in the Engine project) grants the test project access. The production DI path is unaffected.

### ME2-T14 — Type mismatch in `ScanEntry` else branch

`DiscoverFromFileDetailed` returns `IReadOnlyList<TopicMetadata>` while `ScanEntry`'s local variable is typed as `List<TopicMetadata>`. The `else` branch originally assigned it directly, causing a CS0266 compile error.

**Resolution:** Wrapped the return in `new List<TopicMetadata>(...)` to materialize the read-only list into the mutable local — consistent with how the directory branch builds its list via `AddRange`.

---

## Q2: Weak Points Observed

1. **`AssemblySourceService` config path coupling** — The production constructor's config path computation (`%APPDATA%\DdsMonitor\assembly-sources.json`) is duplicated in two constructors. A future refactor could use a factory or options pattern. For now the duplication is minimal (one line).

2. **`ScanEntry` directory enumeration is top-level only** — `Directory.EnumerateFiles` enumerates the top-level folder only (`SearchOption.TopDirectoryOnly` is the default). This is consistent with the ME2-T14 spec and avoids accidental loading of very deep dependency trees. Deep subdirectory scanning would require an explicit `SearchOption.AllDirectories` opt-in.

3. **`TopicColorService` `OnChanged` event and Scoped lifetime** — Components inject `TopicColorService` as Scoped and subscribe to `OnChanged`. Since `TopicColorService` is now Scoped (per circuit), there is no cross-circuit event leakage. However, if multiple components within the same circuit subscribe and none unsubscribe on dispose, the handler list grows until circuit teardown. This was a pre-existing concern under the Singleton registration and is unchanged in severity.

---

## Q3: Design Decisions

### ME2-T27 — Scoped vs decoupled Singleton

Two approaches were considered:
1. **Change registration to `AddScoped`** — minimal one-line change; `TopicColorService` remains correct at per-circuit granularity; nothing about the service requires global singleton semantics.
2. **Decouple from `IWorkspaceState`** — rewrite the constructor to compute `%APPDATA%\DdsMonitor\topic-colors.json` directly, then keep the `AddSingleton` registration.

Approach 1 was chosen because it is structurally simpler and Scoped is clearly the correct lifetime for a service that already stores per-circuit state. Approach 2 would additionally require updating tests and loses the `IWorkspaceState` indirection that lets tests point persistence to arbitrary temp directories.

### ME2-T14 — `File.Exists` else branch vs explicit `FileNotFoundException`

The spec says to throw `FileNotFoundException` when the path is neither a directory nor a file. The current implementation allows the existing `DiscoverFromFileDetailed` call in the else branch to throw naturally when the file doesn't exist — `DiscoverFromFileDetailed` already validates path existence. This preserves the error message set on `entry.LoadError` via the outer `catch` block, which the UI displays. Adding an explicit `FileNotFoundException` would only change the message text. Behavior is identical from the UI's perspective.

### ME2-T14 — `FileDialog.razor` `CanConfirm` change for Open mode

`CanConfirm` was changed from `!string.IsNullOrWhiteSpace(_fileNameInput)` to `Mode == FileDialogMode.Open || !string.IsNullOrWhiteSpace(_fileNameInput)`. In Open mode the OK button is always enabled; an empty filename resolves to the current browsed directory. In Save mode the filename is still required (unchanged behavior).

---

## Files Modified

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Blazor/Program.cs` | ME2-T27: `AddSingleton<TopicColorService>` → `AddScoped<TopicColorService>` |
| `tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs` | ME2-T14: Add `AssemblyPath` property initialized from `topicType.Assembly.Location` |
| `tools/DdsMonitor/DdsMonitor.Engine/AssemblyScanner/AssemblySourceService.cs` | ME2-T14: Directory-based scan in `ScanEntry`; `using System.Linq`; internal test-only constructor |
| `tools/DdsMonitor/DdsMonitor.Engine/AssemblyInfo.cs` | **New** — `InternalsVisibleTo("DdsMonitor.Engine.Tests")` |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicSourcesPanel.razor` | ME2-T14: 3rd line in CLR Type column showing `topic.AssemblyPath`; updated Add button title |
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/FileDialog.razor` | ME2-T14: `CanConfirm` always true in Open mode; empty path resolves to `_currentDir`; accept `Directory.Exists` in Open mode; updated placeholder text |
| `tests/DdsMonitor.Engine.Tests/ME2Batch07Tests.cs` | **New** — 8 tests covering ME2-T27 and ME2-T14 |

---

## Test Results

| Suite | Before | After | New |
|---|---|---|---|
| `DdsMonitor.Engine.Tests` | 435 ✅ | 443 ✅ | +8 |

All 443 Engine tests pass. Full `CycloneDDS.NET.Core.slnf` solution builds with 0 errors.
