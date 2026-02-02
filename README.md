# FastCycloneDDS C# Bindings

A modern, high-performance, zero-allocation .NET binding for Eclipse Cyclone DDS, with idiomatic C# API.

See a [short presentation of principles](docs/CsharpBindings_presentation.pdf).

See [detailed technical overview](DetailedOverview.md).

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
    // Check for valid data (skip metadata-only samples like Dispose events)
    if (sample.IsValid)
    {
        // OPTION A: Simple (Managed)
        // Lazy property triggers deep copy to managed object
        // SensorData data = sample.Data;
        
        // OPTION B: Fast (Zero-Copy)
        // Get a view over native memory. Zero allocations.
        var view = sample.AsView();
        
        Console.WriteLine($"Received: {view.SensorId} = {view.Value}");
    }
    else
    {
        // Handle lifecycle events (e.g., instance disposed)
        Console.WriteLine($"Instance state: {sample.Info.InstanceState}");
    }
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

## 9. Legacy IDL Import

If you have existing DDS systems defined in IDL, you can generate the corresponding C# DSL automatically.

```bash
# Import IDL to C#
CycloneDDS.IdlImporter MySystem.idl ./src/Generated
```

This generates C# `[DdsTopic]` structs that are binary-compatible with your existing system.
See [IDL Import Guide](docs/IDL-IMPORT.md) for advanced usage including multi-module support.

---

## Dependencies

*   `CycloneDDS.Core`: Core memory management (NativeArena) and native types
*   `CycloneDDS.Schema`: Attributes and Type System
*   `CycloneDDS.CodeGen`: Build-time source generator
*   `CycloneDDS.Runtime`: The high-level API described above
*   `ddsc.dll`: Native Cyclone DDS library
*   `idlc.exe`: Cyclone DDS IDL compiler
*   `cycloneddsidljson.dll`: our custom json plugin for IDL compiler - build using `build_cyclone.bat` script

---

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

*Built for speed. Designed for developers.*

