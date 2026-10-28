# FastCycloneDDS C# Bindings

[![CI](https://github.com/pjanec/CycloneDds.NET/actions/workflows/ci.yml/badge.svg)](https://github.com/pjanec/CycloneDds.NET/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/CycloneDDS.NET.svg)](https://www.nuget.org/packages/CycloneDDS.NET/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A modern, high-performance, zero-allocation .NET binding for Eclipse Cyclone DDS, with idiomatic C# API.

See [detailed technical overview](DetailedOverview.md).

## Installation
 
### Using the NuGet Package (Recommended)

Install the CycloneDDS.NET package from NuGet:
 
```bash
dotnet add package CycloneDDS.NET
```

For watching a DDS domain (browser UI, or headless recording for scripts and AI agents) there is a companion tool, `dotnet tool install -g CycloneDDS.NET.DdsMonitor`: see [DDS Monitor tool](#dds-monitor-tool).
 
This single package includes:
- **Runtime Library:** High-performance managed bindings (targets `net8.0`; runs on .NET 8 and later).
- **Native Assets:** Pre-compiled native runtime and IDL compiler for both **Windows x64** (`ddsc.dll`, `idlc.exe`) and **Linux x64** (`libddsc.so`, `idlc`). NuGet selects the correct runtime asset per platform automatically.
- **Build Tools:** Automatic C# code generation during build.

### Supported platforms

| Platform | Native runtime | Target requirement |
| :--- | :--- | :--- |
| Windows x64 | `ddsc.dll` | [Visual C++ Redistributable for Visual Studio 2022](https://aka.ms/vs/17/release/vc_redist.x64.exe) installed on the target machine. |
| Linux x64 | `libddsc.so` | A glibc-based distribution (e.g. Ubuntu/Debian). No extra runtime install needed. |

Only applications that turn on [DDS Security](#12-dds-security-optional) additionally need
OpenSSL 3 at runtime; the package does not ship it.

### Working with Source Code

If you want to build the project from source or contribute:

1.  **Clone the repository** (recursively, to get the native submodule):
    ```bash
    git clone --recursive https://github.com/pjanec/CycloneDds.NET.git
    cd CycloneDds.NET
    ```

2.  **Build the native libraries** for your platform:
    -   **Windows** (PowerShell): `.\build\native-win.ps1`
    -   **Linux** (bash): `build/native-linux.sh Release`

    Then build and test the managed solution:
    ```bash
    dotnet build CycloneDDS.NET.Core.slnf -c Release
    dotnet test  CycloneDDS.NET.Core.slnf -c Release
    ```
    On Windows you can instead run the one-stop script `.\build\build-and-test.ps1`, which builds the native artifacts (if missing), builds the solution, and runs the tests.

3.  **Requirements:**
    -   **.NET SDK:** the solution targets `net8.0`, but building requires the **.NET 10 SDK** (the code uses C# 13 language features). Produced binaries still run on .NET 8+.
    -   **CMake 3.16+** on your `PATH`.
    -   **Windows:** Visual Studio 2022 with the *C++ Desktop Development* workload (for native compilation), plus OpenSSL 3.x with its development files to build the DDS Security plugins (e.g. the full "Win64 OpenSSL v3" installer from [slproweb.com](https://slproweb.com/products/Win32OpenSSL.html)). `build\native-win.ps1` looks in `C:\Program Files\OpenSSL`, `C:\Program Files\OpenSSL-Win64` and `C:\OpenSSL-Win64`; set `OPENSSL_ROOT_DIR` to override.
    -   **Linux:** a C toolchain, `patchelf` and the OpenSSL headers — e.g. `sudo apt-get install -y cmake build-essential patchelf libssl-dev`.

## Key Features

### 🚀 Performance Core
- **Zero-Allocation Writes:** Custom marshaller writes directly to pooled buffers (ArrayPool) using a C-compatible memory layout.
- **Zero-Copy Reads:** Read directly from native DDS buffers using `ref struct` views, bypassing deserialization.
- **Unified API:** Single reader provides both safe managed objects and high-performance zero-copy views.
- **Lazy Deserialization:** Only pay the cost of deep-copying objects when you explicitly access `.Data`.

### 🧬 Schema & Interoperability
- **Code-First DSL:** Define your data types entirely in C# using attributes (`[DdsTopic]`, `[DdsKey]`, `[DdsStruct]`, `[DdsQos]`). No need to write IDL files manually.
- **Automatic IDL Generation:** The build tools automatically generate standard OMG IDL files from your C# classes, ensuring perfect interoperability with other DDS implementations (C++, Python, Java) and tools. See [IDL Generation](IDL-GENERATION.md).
- **Modern Language Integration:** Full support for C# 12 `[InlineArray]` (safe fixed-size arrays without `unsafe`), typed enums (`enum E : byte` emits `@bit_bound(8)`), and default topic naming based on namespaces.
- **Auto-Magic Type Discovery:** Runtime automatically registers type descriptors based on your schema.
- **IDL Import:** Convert existing IDL files into C# DSL automatically using the `IdlImporter` tool.
- **100% Native Compliance:** Uses Cyclone DDS native serializer for wire compatibility.


### 🛠️ Developer Experience
- **Auto-Magic Type Discovery:** No manual IDL compilation or type registration required.
- **Async/Await:** `WaitDataAsync` for non-blocking, task-based consumers.
- **Client-Side Filtering:** High-performance predicates (`view => view.Id > 5`) compiled to JIT code.
- **Instance Management:** O(1) history lookup for keyed topics.
- **Sender Tracking:** Identify the source application (Computer, PID, custom app id) of every message.
- **Modern C#:** Events, Properties, and generic constraints instead of listeners and pointers.

### 📡 Partitioning & Monitoring
- **Partition Support:** Isolate traffic using DDS partitions. Set a partition on a participant once and every reader/writer inherits it automatically, or override per-reader/writer with a named argument.
- **Zero-Allocation WaitSet:** Monitor 100+ readers on a single OS thread. `DdsWaitSet.Wait(Span<IDdsReader>, timeout, ct)` never allocates in the hot path and supports `CancellationToken` for instant, safe interruption.

---

## 1. Defining Data (The Schema)

Define your data using standard C# `partial structs`. The build tools generate the serialization logic automatically.

### High-Performance Schema (Zero Alloc)
Use this for high-frequency data (1kHz+).

```csharp
using CycloneDDS.Schema;

namespace Factory.Monitoring;

// Topic name defaults to namespace + containing types + class name, joined with '_'
// ("Factory_Monitoring_SensorData") if omitted. DdsTopicName.Of<SensorData>() returns it.
[DdsTopic]
public partial struct SensorData
{
    [DdsKey, DdsId(0)]
    public int SensorId;

    [DdsId(1)]
    public double Value;

    // Fixed-size buffer (maps to char[32]). No heap allocation.
    [DdsId(2)]
    public FixedString32 LocationId; 

    // Safe, zero-allocation fixed array using C# 12 [InlineArray] (no 'unsafe' needed!)
    [DdsId(3)]
    public FloatBuffer8 Measurements;

    // Byte-backed enum yields IDL @bit_bound(8) automatically for optimal native network usage
    [DdsId(4)]
    public SensorStatus Status;
}

[System.Runtime.CompilerServices.InlineArray(8)]
public struct FloatBuffer8 { private float _element0; }

public enum SensorStatus : byte
{
    Offline,
    Online,
    Error
}
```

### Unmanaged Schema (Unsafe Fixed Buffers)
For scenarios requiring direct memory manipulation or porting legacy C/C++ structs, you can use `unsafe fixed` arrays. The runtime maps these directly to native memory with zero allocation.

```csharp
[DdsTopic("CustomTopicNameForVideoFrame")]
public unsafe partial struct VideoFrame
{
    [DdsKey]
    public int FrameId;

    // Classic C# unsafe fixed-size buffer
    public fixed byte Pixels[1920 * 1080 * 3];
}
```

### Convenient Schema (Managed Types)
Use this for business logic where convenience outweighs raw speed.

```csharp
[DdsStruct] // Helper struct to be used in the topic data struct (can be nested)
public partial struct GeoPoint { public double Lat; public double Lon; }

[DdsTopic("LogEvents")]
[DdsManaged] // Opt-in to GC allocations for the whole type
public partial struct LogEvent
{
    [DdsKey] 
    public int Id;

    // Standard string (Heap allocated)
    public string Message; 
    
    // Standard List (Heap allocated)
    public List<double> History;

    // Nested custom struct
    public GeoPoint Origin;
}
```

### Configuration & QoS
You can define Quality of Service settings directly on the type using the `[DdsQos]` attribute. The Runtime automatically applies these settings when creating Writers and Readers for this topic.

```csharp
[DdsTopic("MachineState")]
[DdsQos(
    Reliability = DdsReliability.Reliable,          // Guarantee delivery
    Durability = DdsDurability.TransientLocal,      // Late joiners get the last value
    HistoryKind = DdsHistoryKind.KeepLast,          // Keep only recent data
    HistoryDepth = 1                                // Only the latest sample
)]
public partial struct MachineState
{
    [DdsKey]
    public int MachineId;
    public StateEnum CurrentState;
}
```

Further `[DdsQos]` properties, all defaulting to the DDS defaults:

| Property | Meaning |
|---|---|
| `AutodisposeUnregisteredInstances` (default `true`) | Writers only: unregistering also disposes — see [Lifecycle](#8-lifecycle-dispose--unregister) |
| `Ownership` (default `Shared`) | `Exclusive`: only the strongest alive writer owns an instance — see [Ownership & Liveliness](#ownership--liveliness-failover) |
| `LivelinessKind`, `LivelinessLeaseDurationMs` (default automatic, infinite) | How fast a silent or crashed writer is declared gone |
| `DeadlineMs` (default infinite) | Maximum time between updates of each instance |

The attribute is per type, so readers and writers agree on it. Per-entity settings — and
overrides of the attribute — go in `DdsWriterOptions` / `DdsReaderOptions`:

```csharp
var writer = new DdsWriter<MachineState>(participant, topicName: null,
    new DdsWriterOptions { OwnershipStrength = 100, LivelinessLeaseDuration = TimeSpan.FromSeconds(1) });
var reader = new DdsReader<MachineState>(participant, topicName: null,
    new DdsReaderOptions { Deadline = TimeSpan.FromMilliseconds(500) });
```

The attribute and the options apply only when no explicit QoS handle is passed to the
`DdsWriter`/`DdsReader` constructor; with an explicit handle, set the policies yourself via
`DdsApi.dds_qset_*`.

### Ownership & Liveliness (Failover)

With exclusive ownership, readers only see the strongest alive writer of each instance. When it
unregisters, loses liveliness (misses its lease) or goes away, the next strongest takes over.

```csharp
[DdsTopic]
[DdsQos(Ownership = DdsOwnershipKind.Exclusive,
        LivelinessKind = DdsLivelinessKind.ManualByTopic, LivelinessLeaseDurationMs = 500,
        AutodisposeUnregisteredInstances = false)]
public partial struct Descriptor { [DdsKey] public int Id; public int Owner; }

var primary = new DdsWriter<Descriptor>(participant, null, new DdsWriterOptions { OwnershipStrength = 100 });
var backup  = new DdsWriter<Descriptor>(participant, null, new DdsWriterOptions { OwnershipStrength = 50 });

primary.AssertLiveliness();   // with manual liveliness: call within the lease when there is nothing to write

reader.LivelinessChanged += (s, st) => { /* st.NotAliveCountChange > 0: a writer is gone */ };
reader.RequestedDeadlineMissed += (s, st) => { /* an instance was not updated in time */ };
```

Cyclone treats a writer that loses liveliness like one that unregisters its instances: with the
default `AutodisposeUnregisteredInstances = true` readers then see `NOT_ALIVE_DISPOSED`, with
`false` they see `NOT_ALIVE_NO_WRITERS` (once no writer is left). The cumulative counters are
available as `reader.LivelinessChangedStatus`, `reader.RequestedDeadlineMissedStatus`,
`writer.LivelinessLostStatus` and `writer.OfferedDeadlineMissedStatus`; writers also raise
`LivelinessLost` and `OfferedDeadlineMissed`.

There is no autopurge (reader data lifecycle) option: Cyclone accepts that policy but does not
act on it. A reader's instance is freed once all its samples have been taken and no writer is
left.
---

## 2. Basic Usage

### Publishing
```csharp
using Factory.Monitoring;
using var participant = new DdsParticipant();

// Auto-discovers topic type and its default name ("Factory_Monitoring_SensorData")
using var writer = new DdsWriter<SensorData>(participant);

// Zero-allocation write path
var data = new SensorData 
{ 
    SensorId = 1, 
    Value = 25.5,
    LocationId = new FixedString32("Factory_A"),
    Status = SensorStatus.Online
};

// With C# 12, InlineArrays can be accessed directly by index
data.Measurements[0] = 1.0f; 

writer.Write(data);
```

### Subscribing (Polling)
Reading uses a **Scope** pattern to ensure safety and zero-copy semantics. You "loan" the data, read it, and return it by disposing the scope.

```csharp
using Factory.Monitoring;

using var reader = new DdsReader<SensorData>(participant);

// POLL FOR DATA
// Returns a "Loan" which manages native memory
using var loan = reader.Take(maxSamples: 10);

// Iterate received data
foreach (var sample in loan)
{
    // `sample.IsValid` indicates whether a full payload is present.
    // IMPORTANT: even when `ValidData == 0` (lifecycle/metadata-only samples),
    // the middleware provides the native memory with the topic key fields populated.
    // Therefore `sample.Data` is safe to call for every sample and will return
    // a managed object where key fields are set and non-key fields are defaulted.

    // Always obtain the managed copy (safe for metadata-only samples too)
    var data = sample.Data;

    if (sample.IsValid)
    {
        // OPTION A: Simple (Managed)
        // `data` is a full managed copy populated from native memory
        Console.WriteLine($"Received: {data.SensorId} = {data.Value}");
    }
    else
    {
        // Lifecycle event (e.g., instance disposed). Key fields are available in `data`.
        Console.WriteLine($"Instance {data.SensorId} state: {sample.Info.InstanceState}");
    }

    // OPTION B: Fast (Zero-Copy) — you can still use AsView() when you only need
    // transient, zero-allocation access to the native buffer (stack-only).
    // var view = sample.AsView();
}

```

---

## 3. Async/Await (Modern Loop)

Bridge the gap between real-time DDS and .NET Tasks. No blocking threads required.

```csharp
Console.WriteLine("Waiting for data...");

// Efficiently waits using TaskCompletionSource (no polling loop)
while (await reader.WaitDataAsync())
{
    // Take all available data
    using var scope = reader.Take();
    
    foreach (var sample in scope)
    {
        await ProcessAsync(sample);
    }
}
```

---

## 4. Advanced Filtering

Filter data **before** you pay the cost of processing it. This implementation uses C# delegates but executes on the raw buffer view, allowing JIT optimizations to make it extremely fast.

```csharp
// 1. Set a filter predicate on the Reader
// Logic executes during iteration, skipping irrelevant samples instantly.
// Since 'view' is a ref struct reading raw memory, this is Zero-Copy filtering.
reader.SetFilter(view => view.Value > 100.0 && view.LocationId.ToString() == "Lab_1");

// 2. Iterate
using var scope = reader.Take();
foreach (var highValueSample in scope)
{
    // Guaranteed to be > 100.0 and from Lab_1
}

// 3. Update filter dynamically at runtime
reader.SetFilter(null); // Clear filter
```

---

## 5. Instance Management (Keyed Topics)

For systems tracking many objects (fleets, tracks, sensors), efficiently query a specific object's history without iterating the entire database.

```csharp
// 1. Create a key template for the object we care about
var key = new SensorData { SensorId = 5 };

// 2. Lookup the Handle (O(1) hashing)
DdsInstanceHandle handle = reader.LookupInstance(key);

if (!handle.IsNil)
{
    // 3. Read history for ONLY Sensor 5
    // Ignores Sensor 1, 2, 3... Zero iteration overhead.
    using var history = reader.ReadInstance(handle, maxSamples: 100);
    
    foreach (var snapshot in history)
    {
        Plot(snapshot.Value);
    }
}
```

---

## 6. Sender Tracking (Identity)

Identify exactly which application instance sent a message. Essential for multi-process debugging.

### Sender Configuration
```csharp
var config = new SenderIdentityConfig 
{ 
    AppDomainId = 1, 
    AppInstanceId = 100 
};

// Enable tracking BEFORE creating writers
participant.EnableSenderTracking(config);

// Now, every writer created by this participant automatically broadcasts identity
using var writer = new DdsWriter<LogEvent>(participant, "Logs");
```

### Receiver Usage
```csharp
// Enable tracking on the reader
reader.EnableSenderTracking(participant.SenderRegistry);

using var scope = reader.Take();
for (int i = 0; i < scope.Count; i++)
{
    // O(1) Lookup of sender info
    // Returns: ComputerName, ProcessName, ProcessId, AppDomainId, etc.
    var sender = scope.GetSender(i); 
    var msg = scope[i];

    if (sender != null)
    {
        Console.WriteLine($"[{sender.ComputerName} : PID {sender.ProcessId}] says: {msg.Message}");
    }
}
```

---

## 7. Status & Discovery

Know when peers connect or disconnect using standard C# Events.

```csharp
// Writer Side
writer.PublicationMatched += (s, status) => 
{
    if (status.CurrentCountChange > 0)
        Console.WriteLine($"Subscriber connected! Total: {status.CurrentCount}");
    else
        Console.WriteLine("Subscriber lost.");
};

// Reliable Startup (Wait for Discovery)
// Solves the "Lost First Message" problem
await writer.WaitForReaderAsync(TimeSpan.FromSeconds(5));
writer.Write(new Message("Hello")); // Guaranteed to have a route

// Wait until every matched reliable reader acknowledged everything written so far
// (writes, disposes and unregisters). Returns false on timeout.
bool delivered = writer.WaitForAcks(TimeSpan.FromSeconds(5));
```

---

## 8. Lifecycle (Dispose & Unregister)

Properly manage the lifecycle of data instances in the Global Data Space.

```csharp
var key = new SensorData { SensorId = 1 };

// 1. Data is invalid/deleted
// Readers receive InstanceState = NOT_ALIVE_DISPOSED
writer.DisposeInstance(key);

// 2. Writer is shutting down (graceful disconnect)
// By default (AutodisposeUnregisteredInstances = true) unregistering ALSO disposes:
// readers receive InstanceState = NOT_ALIVE_DISPOSED
writer.UnregisterInstance(key);
```

To unregister **without** disposing — e.g. to hand a keyed instance over to another
writer — turn auto-dispose off. Readers then see `NOT_ALIVE_NO_WRITERS` only once no
writer is left for the instance:

```csharp
[DdsTopic]
[DdsQos(AutodisposeUnregisteredInstances = false)]
public partial struct Descriptor { [DdsKey] public int Id; public int Owner; }
```

## 9. Partitions

DDS partitions let you divide a domain into named logical channels. Readers and writers only communicate within the same partition, making it easy to run multiple isolated subsystems on the same DDS domain (e.g. separate a monitoring plane from a control plane, or multiplex tenants).

### Set a partition on the participant (inherited by all readers/writers)

```csharp
// All readers and writers created from this participant will use "monitoring" automatically.
using var participant = new DdsParticipant(domainId: 0, defaultPartition: "monitoring");

// Topic name comes from [DdsTopic("SensorData")] — no need to repeat it.
using var reader = new DdsReader<SensorData>(participant);
using var writer = new DdsWriter<SensorData>(participant);
```

### Override the partition per reader / writer

```csharp
using var participant = new DdsParticipant(0, defaultPartition: "*"); // wildcard default

// This writer specifically targets the "control" partition.
using var controlWriter = new DdsWriter<SensorData>(
    participant, "SensorData", partition: "control");

// This reader stays on the default "*" partition — sees everything.
using var broadcastReader = new DdsReader<SensorData>(participant);
```

### Resolution order

```
per-reader / per-writer partition  →  participant.DefaultPartition  →  (no partition)
```

---

## 10. WaitSet — Efficient Multi-Reader Monitoring

`DdsWaitSet` provides a native-backed mechanism for sleeping on many readers simultaneously on **a single OS thread**. This is ideal for monitoring applications that track 100+ topics and do not want the overhead of spawning a background `Task` per reader.

### Basic usage

```csharp
using var participant = new DdsParticipant(0, defaultPartition: "*");

// Create readers for every topic you want to monitor
using var tempReader    = new DdsReader<TemperatureEvent>(participant);
using var pressReader   = new DdsReader<PressureEvent>(participant);
using var statusReader  = new DdsReader<MachineStatus>(participant);

// Create WaitSet and attach all readers
using var waitset = new DdsWaitSet(participant);
waitset.Attach(tempReader);
waitset.Attach(pressReader);
waitset.Attach(statusReader);

// Pre-allocate result buffer once — no allocation inside the loop
IDdsReader[] triggered = new IDdsReader[16];

var cts = new CancellationTokenSource();

while (!cts.IsCancellationRequested)
{
    // Blocks until at least one reader has data, or the timeout expires, or ct is cancelled.
    // Zero allocation in this hot path.
    int count = waitset.Wait(triggered.AsSpan(), timeout: TimeSpan.FromSeconds(1), cts.Token);

    for (int i = 0; i < count; i++)
    {
        switch (triggered[i])
        {
            case DdsReader<TemperatureEvent> r:
                using (var loan = r.Take()) { /* handle temp */ }
                break;

            case DdsReader<PressureEvent> r:
                using (var loan = r.Take()) { /* handle pressure */ }
                break;

            case DdsReader<MachineStatus> r:
                using (var loan = r.Take()) { /* handle status */ }
                break;
        }
    }
}
```

### Attach / Detach at runtime

Readers can be added or removed while the WaitSet is not waiting, making the monitored set dynamic:

```csharp
// Start watching a new topic at runtime
var newReader = new DdsReader<AlarmEvent>(participant);
waitset.Attach(newReader);

// Stop watching (and dispose the reader when no longer needed)
waitset.Detach(newReader);
newReader.Dispose();
```

### CancellationToken

Pass a `CancellationToken` to `Wait` to interrupt the blocking native call safely from any thread:

```csharp
cts.Cancel(); // triggers the native guard condition, unblocks Wait() instantly
```

### Performance characteristics

| Operation | Allocation | Notes |
| :--- | :--- | :--- |
| `Wait(...)` hot path | **0 Bytes** | `ArrayPool` rent inside; result written into caller's `Span` |
| `Attach` / `Detach` | Small (one-time) | `GCHandle` + dictionary entry per reader |
| Cancellation callback | **0 Bytes** | Triggers native guard condition via P/Invoke |

---

## 11. Legacy IDL Import

If you have existing DDS systems defined in IDL, you can generate the corresponding C# DSL automatically.

```bash
# Import IDL to C#
CycloneDDS.IdlImporter MySystem.idl ./src/Generated
```

This generates C# `[DdsTopic]` structs that are binary-compatible with your existing system.
See [IDL Import Guide](docs/IDL-IMPORT.md) for advanced usage including multi-module support.

---

## 12. DDS Security (Optional)

The package includes Cyclone DDS's [DDS Security](https://cyclonedds.io/docs/cyclonedds/latest/security/index.html)
plugins (authentication, access control, encryption). They are loaded **only** when the domain
configuration contains a `<Security>` section; applications that don't configure security load
nothing extra and need no OpenSSL.

**Enabling it.** Point `CYCLONEDDS_URI` at a configuration with a `<Security>` section — identity
certificate and CA, private key, permissions CA, and signed governance and permissions documents.
See the [Cyclone DDS security configuration example](https://cyclonedds.io/docs/cyclonedds/latest/security/example_configuration.html).
Every peer of a secured domain needs a valid identity and permissions; unauthenticated
participants are not admitted.

**OpenSSL 3 is required at runtime and is not shipped** — you choose and update it:

- **Linux:** the distribution's `libcrypto.so.3` (OpenSSL 3, e.g. the `libssl3` package; standard on
  Ubuntu 22.04+, Debian 12+, RHEL 9+).
- **Windows:** `libcrypto-3-x64.dll` / `libssl-3-x64.dll` must be loadable by the application, in any of
  these ways:
  - install OpenSSL 3 (e.g. the "Win64 OpenSSL v3 Light" installer from [slproweb.com](https://slproweb.com/products/Win32OpenSSL.html)) with its `bin` folder on `PATH`
    or its DLLs in the Windows system directory;
  - let the build copy your OpenSSL DLLs next to the application:
    ```xml
    <PropertyGroup>
      <CycloneDdsOpenSslDir>C:\Program Files\OpenSSL\bin</CycloneDdsOpenSslDir>
    </PropertyGroup>
    ```
  - or load them yourself before the first secured participant is created —
    `NativeLibrary.Load(@"...\libcrypto-3-x64.dll")` (and `libssl-3-x64.dll`); Windows then reuses
    the loaded copies for the plugins.

If OpenSSL or a plugin cannot be loaded, creating a participant on a secured domain throws a
`DdsException` (Cyclone logs "Could not load ... library"); unsecured domains are unaffected.

On Windows the plugins are copied next to your application by the package's build targets, because
Windows looks for them there. Set `<CycloneDdsStageSecurityPlugins>false</CycloneDdsStageSecurityPlugins>`
to opt out.

## DDS Monitor tool

**DDS Monitor** (`ddsmonitor`) is a companion .NET global tool for watching a DDS domain. It has a browser UI with live sample grids, instance tracking, filtering, sample sending and replay. It can also record headless, to a JSON Lines file that scripts and AI agents can read while it is being written, and replay a recording back onto the network.

```bash
dotnet tool install -g CycloneDDS.NET.DdsMonitor
ddsmonitor --help
ddsmonitor --AppSettings:TopicSources:0=./bin/Debug/net8.0      # UI, with your topic types
```

On a secured domain it needs the same `CYCLONEDDS_URI` security configuration as any other participant; the Windows specifics (config-file URI form, OpenSSL on `PATH`) are in [Running with DDS Security](tools/DdsMonitor/README.md#running-with-dds-security).

It ships with the same version number as this package. See the [DDS Monitor README](https://github.com/pjanec/CycloneDds.NET/blob/main/tools/DdsMonitor/README.md), including its [runbook for scripts and AI agents](https://github.com/pjanec/CycloneDds.NET/blob/main/tools/DdsMonitor/README.md#recording-for-scripts-and-ai-agents).

## Examples

### Hello World
A complete "Hello World" example that demonstrates creating a topic, publishing, and subscribing in a single application can be found in [`examples/HelloWorld`](examples/HelloWorld).

This example is designed to verify the NuGet package installation and basic functionality using the locally built package.

To run it:
1. Build the packages: `.\build\pack.ps1`
2. Run the example:
   ```bash
   cd examples/HelloWorld
   dotnet run
   ```

---

## Dependencies

The `CycloneDDS.NET` package bundles these internal components:

*   **Managed Libraries:** `CycloneDDS.Core`, `CycloneDDS.Schema`, `CycloneDDS.CodeGen`, `CycloneDDS.Runtime`
*   **Native Assets (Windows x64):** `ddsc.dll` (Cyclone DDS), `idlc.exe` (IDL Compiler), `cycloneddsidljson.dll` (IDL JSON plugin)
*   **Native Assets (Linux x64):** `libddsc.so` (Cyclone DDS), `idlc` (IDL Compiler), `libcycloneddsidljson.so` (IDL JSON plugin)

## Performance Characteristics

| Feature | Allocation Cost | Performance Note |
| :--- | :--- | :--- |
| **Write** | **0 Bytes** | Uses ArrayPool + NativeArena |
| **Read (View)** | **0 Bytes** | Uses `.AsView()` + Ref Structs |
| **Read (Managed)** | Allocates | Uses `.Data` (Deep Copy) |
| **Take (Polling)** | **0 Bytes** | Uses Loaned Buffers |
| **Filtering** | **0 Bytes** | Manual loop filtering with Views |
| **Sender Lookup** | **0 Bytes** | O(1) Dictionary Lookup |
| **Async Wait** | ~80 Bytes | One Task per `await` cycle |
| **WaitSet.Wait** | **0 Bytes** | Span output + ArrayPool rent; no heap in hot path |

*Built for speed. Designed for developers.*

