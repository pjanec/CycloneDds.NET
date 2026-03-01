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
 
This single package includes:
- **Runtime Library:** High-performance managed bindings.
- **Native Assets:** Pre-compiled `ddsc.dll` and `idlc.exe` (Windows x64).
- **Build Tools:** Automatic C# code generation during build.
 
**Important:** This package relies on native libraries that require the [Visual C++ Redistributable for Visual Studio 2022](https://aka.ms/vs/17/release/vc_redist.x64.exe) to be installed on the target system.

### Working with Source Code

If you want to build the project from source or contribute:

1.  **Clone the repository** (recursively, to get the native submodule):
    ```bash
    git clone --recursive https://github.com/pjanec/CycloneDds.NET.git
    cd CycloneDds.NET
    ```

2.  **Build and Test** (One-Stop Script):
    Run the developer workflow script. This will automatically check for native artifacts (building them if missing), build the solution, and run all tests.
    ```powershell
    .\build\build-and-test.ps1
    ```

3.  **Requirements:**
    -   .NET 8.0 SDK
    -   Visual Studio 2022 (C++ Desktop Development workload) for native compilation.
    -   CMake 3.16+ in your PATH.

## Key Features

### 🚀 Performance Core
- **Zero-Allocation Writes:** Custom marshaller writes directly to pooled buffers (ArrayPool) using a C-compatible memory layout.
- **Zero-Copy Reads:** Read directly from native DDS buffers using `ref struct` views, bypassing deserialization.
- **Unified API:** Single reader provides both safe managed objects and high-performance zero-copy views.
- **Lazy Deserialization:** Only pay the cost of deep-copying objects when you explicitly access `.Data`.

### 🧬 Schema & Interoperability
- **Code-First DSL:** Define your data types entirely in C# using attributes (`[DdsTopic]`, `[DdsKey]`, `[DdsStruct]`, `[DdsQos]`). No need to write IDL files manually.
- **Automatic IDL Generation:** The build tools automatically generate standard OMG IDL files from your C# classes, ensuring perfect interoperability with other DDS implementations (C++, Python, Java) and tools. See [IDL Generation](IDL-GENERATION.md).
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

[DdsTopic("SensorData")]
public partial struct SensorData
{
    [DdsKey, DdsId(0)]
    public int SensorId;

    [DdsId(1)]
    public double Value;

    // Fixed-size buffer (maps to char[32]). No heap allocation.
    [DdsId(2)]
    public FixedString32 LocationId; 
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
---

## 2. Basic Usage

### Publishing
```csharp
using var participant = new DdsParticipant();

// Auto-discovers topic type and registers it
using var writer = new DdsWriter<SensorData>(participant, "SensorData");

// Zero-allocation write path
writer.Write(new SensorData 
{ 
    SensorId = 1, 
    Value = 25.5,
    LocationId = new FixedString32("Factory_A")
});
```

### Subscribing (Polling)
Reading uses a **Scope** pattern to ensure safety and zero-copy semantics. You "loan" the data, read it, and return it by disposing the scope.

```csharp
using var reader = new DdsReader<SensorData>(participant, "SensorData");

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
// Readers receive InstanceState = NOT_ALIVE_NO_WRITERS (if ownership exclusive)
writer.UnregisterInstance(key);
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
*   **Native Assets:** `ddsc.dll` (Cyclone DDS), `idlc.exe` (IDL Compiler), `cycloneddsidljson.dll` (IDL JSON plugin)

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

