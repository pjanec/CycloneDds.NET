# FeatureDemo Application - Detailed Design Document

## 1. Executive Summary

The FeatureDemo application is a comprehensive console application designed to showcase the capabilities of the FastCycloneDDS C# binding library. It demonstrates real-world usage patterns through interactive scenarios, highlighting both ease of use and high-performance features.

## 2. Project Structure

```
examples/
  FeatureDemo/
    FeatureDemo.csproj           # Main project file
    Program.cs                   # Entry point with CLI parsing
    Schema.cs                    # All [DdsTopic] definitions
    
    Orchestration/
      DemoMode.cs                # Enum for mode selection
      DemoOrchestrator.cs        # Master/Slave/Standalone coordination
      
    Scenarios/
      IDemoScenario.cs           # Base interface for all scenarios
      ControlChannelManager.cs   # Manages DemoControl topic
      
      ChatRoom/
        ChatRoomScenario.cs      # Scenario A: Basic Pub/Sub
        ChatRoomPublisher.cs     # Pure DDS publisher logic
        ChatRoomSubscriber.cs    # Pure DDS subscriber logic
        ChatRoomUI.cs            # Spectre.Console visualization
        
      SensorArray/
        SensorArrayScenario.cs   # Scenario B: Zero-Alloc performance
        SensorPublisher.cs       # High-frequency publisher
        SensorSubscriber.cs      # Zero-copy subscriber
        SensorUI.cs              # Bar chart visualization
        
      StockTicker/
        StockTickerScenario.cs   # Scenario C: Content filtering
        StockPublisher.cs        # Multi-symbol publisher
        StockSubscriber.cs       # Filtered subscriber
        StockUI.cs               # Split-view visualization
        
      FlightRadar/
        FlightRadarScenario.cs   # Scenario D: Keyed instances
        FlightPublisher.cs       # Multi-instance publisher
        FlightSubscriber.cs      # Instance lookup subscriber
        FlightUI.cs              # Table visualization
        
      BlackBox/
        BlackBoxScenario.cs      # Scenario E: Durability/QoS
        BlackBoxPublisher.cs     # Historical data publisher
        BlackBoxSubscriber.cs    # Late-joining subscriber
        BlackBoxUI.cs            # Timeline visualization
        
    UI/
      DiagnosticHeader.cs        # Network status header
      MainMenu.cs                # Interactive menu
      WaitingScreen.cs           # Connection waiting UI
```

## 3. Data Schema Definitions

### 3.1 Control Channel (Orchestration)

```csharp
[DdsTopic]
[DdsQos(Reliability = ReliabilityQosPolicyKind.Reliable, 
        Durability = DurabilityQosPolicyKind.TransientLocal)]
public struct DemoControl
{
    [DdsKey]
    public byte NodeId;           // 0 = Master, 1 = Slave
    public ControlCommand Command; // Handshake, StartScenario, StopScenario, Ack
    public int ScenarioId;        // 1-5
    public long Timestamp;        // For synchronization
}

public enum ControlCommand : byte
{
    Handshake = 0,
    StartScenario = 1,
    StopScenario = 2,
    Ack = 3
}
```

### 3.2 Scenario A: Chat Room

```csharp
[DdsTopic]
[DdsQos(Reliability = ReliabilityQosPolicyKind.Reliable)]
public struct ChatMessage
{
    public long MessageId;
    public FixedString32 User;
    public FixedString128 Content;
    public long Timestamp;
}
```

**Features Demonstrated:**
- Basic Pub/Sub
- Sender tracking (identify source without sending metadata)
- Managed API ease of use
- Reliable delivery

### 3.3 Scenario B: Sensor Array (Zero-Allocation)

```csharp
[DdsTopic]
[DdsQos(Reliability = ReliabilityQosPolicyKind.BestEffort)]
public struct SensorData
{
    [DdsKey]
    public int SensorId;
    public double Value;
    public FixedString32 Location;
    public long Timestamp;
}
```

**Features Demonstrated:**
- Zero-allocation reads using `AsView()`
- High-frequency data (10,000+ samples/sec)
- GC pressure measurement
- Performance comparison (managed vs zero-copy)

### 3.4 Scenario C: Stock Ticker (Content Filtering)

```csharp
[DdsTopic]
[DdsQos(Reliability = ReliabilityQosPolicyKind.Reliable)]
public struct StockTick
{
    public long TickId;
    public FixedString32 Symbol;
    public double Price;
    public int Volume;
    public long Timestamp;
}
```

**Features Demonstrated:**
- Client-side filtering with predicates
- Efficient data selection
- Real-time data streams

### 3.5 Scenario D: Flight Radar (Keyed Instances)

```csharp
[DdsTopic]
[DdsQos(Reliability = ReliabilityQosPolicyKind.Reliable,
        History = HistoryQosPolicyKind.KeepLast,
        HistoryDepth = 10)]
public struct FlightPosition
{
    [DdsKey]
    public FixedString32 FlightId;
    public double Latitude;
    public double Longitude;
    public double Altitude;
    public long Timestamp;
}
```

**Features Demonstrated:**
- Keyed topics and instance management
- O(1) instance lookup
- History depth and per-instance caching
- `LookupInstance` and `ReadInstance` APIs

### 3.6 Scenario E: Black Box (Durability)

```csharp
[DdsTopic]
[DdsQos(Reliability = ReliabilityQosPolicyKind.Reliable,
        Durability = DurabilityQosPolicyKind.TransientLocal,
        History = HistoryQosPolicyKind.KeepAll)]
public struct SystemLog
{
    public long LogId;
    public LogLevel Level;
    public FixedString64 Component;
    public FixedString256 Message;
    public long Timestamp;
}

public enum LogLevel : byte
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}
```

**Features Demonstrated:**
- Transient local durability
- Late-joining subscribers receive historical data
- KeepAll history policy
- Mission-critical data persistence

## 4. Application Modes

### 4.1 Standalone Mode (Default)
- Creates two participants in same process
- Simulates Publisher and Subscriber in parallel tasks
- No network required
- Ideal for demos and development

### 4.2 Master Mode
- Shows interactive menu
- Drives scenario selection
- Sends control commands to Slave
- Runs publisher side of scenarios

### 4.3 Slave Mode
- Waits for control commands
- Automatically switches scenarios
- Runs subscriber side
- Shows "Waiting for commands..." when idle

### 4.4 Interactive CLI Entry
If no arguments provided:
```
┌─────────────────────────────────────────┐
│ FastCycloneDDS Feature Demo             │
├─────────────────────────────────────────┤
│ Select Mode:                            │
│ > Standalone (Run all in this process) │
│   Master (Control Node)                │
│   Slave (Subscriber Node)              │
│   Autonomous Demo (Auto-run all)       │
└─────────────────────────────────────────┘
```

## 5. Synchronization and Handshake

### 5.1 Initial Connection
```
Master                          Slave
  |                              |
  |-- Handshake (NodeId=0) ----->|
  |                              |
  |<--- Handshake (NodeId=1) ----|
  |                              |
[Show Menu]              [Show Waiting]
```

### 5.2 Scenario Execution
```
Master                          Slave
  |                              |
  |-- StartScenario(2) --------->|
  |                              |
  |<--- Ack --------------------|
  |                              |
[Run Publisher]          [Run Subscriber]
  |                              |
  |-- StopScenario ------------>|
  |                              |
[Cleanup]                   [Cleanup]
```

### 5.3 Timeout Handling
- Wait for peer: 30 seconds timeout
- Show diagnostic info during wait:
  - Local IP addresses
  - Domain ID
  - Number of discovered participants

## 6. UI/UX Design

### 6.1 Diagnostic Header (All Screens)
```
╔════════════════════════════════════════════════════════╗
║ Mode: Master | IP: 192.168.1.55 | Domain: 0 | Peers: 1 ║
╚════════════════════════════════════════════════════════╝
```

### 6.2 Main Menu (Master Only)
```
┌─ FastCycloneDDS Feature Showcase ─┐
│                                    │
│ 1. Chat Room (Basic Pub/Sub)      │
│ 2. Sensor Array (Zero-Allocation) │
│ 3. Stock Ticker (Filtering)       │
│ 4. Flight Radar (Keyed Instances) │
│ 5. Black Box (Durability/QoS)     │
│ 6. Autonomous Demo (Run All)      │
│ 7. Exit                            │
│                                    │
│ Select scenario: _                 │
└────────────────────────────────────┘
```

### 6.3 Sensor Array Visualization
```
┌─ Zero-Allocation Sensor Demo ─────────────────┐
│                                                │
│ Throughput: [████████████████] 10,245 msg/s   │
│ GC Alloc:   [                ]      0 bytes   │
│                                                │
│ Mode: ⚪ Managed (Slow)  ⚫ Zero-Copy (Fast)   │
│                                                │
│ Messages Received: 50,123                      │
│ Duration: 5.2 seconds                          │
└────────────────────────────────────────────────┘
```

### 6.4 Stock Ticker Split View
```
┌─ Stock Ticker with Filtering ─────────────────┐
│                                                │
│ Network Traffic       │ Filtered Feed (AAPL)  │
│ ─────────────────────│─────────────────────── │
│ MSFT  $420.10        │ AAPL  $155.23  ✓      │
│ GOOG  $140.50        │ AAPL  $155.45  ✓      │
│ AAPL  $155.23  >>>   │ AAPL  $156.10  ✓      │
│ TSLA  $245.00        │                        │
│ AAPL  $155.45  >>>   │ Filter:                │
│ MSFT  $420.25        │   Symbol == "AAPL"    │
│ AAPL  $156.10  >>>   │   Price > $150        │
└────────────────────────────────────────────────┘
```

### 6.5 Flight Radar Instance Lookup
```
┌─ Flight Radar - Instance History ─────────────┐
│                                                │
│ Tracking: Flight BA-123                        │
│                                                │
│ ┌────────┬──────────┬───────────┬──────────┐ │
│ │  Time  │   Lat    │    Lon    │ Altitude │ │
│ ├────────┼──────────┼───────────┼──────────┤ │
│ │ 12:01  │ 51.5074  │  -0.1278  │  35,000  │ │
│ │ 12:02  │ 51.5124  │  -0.1328  │  35,200  │ │
│ │ 12:03  │ 51.5174  │  -0.1378  │  35,500  │ │
│ └────────┴──────────┴───────────┴──────────┘ │
│                                                │
│ Retrieved via O(1) LookupInstance              │
└────────────────────────────────────────────────┘
```

### 6.6 Black Box Timeline
```
┌─ Black Box - Late-Joining Subscriber ─────────┐
│                                                │
│ Timeline:                                      │
│                                                │
│ 12:00:00 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ Now │
│          │    │    │                     │     │
│          ①    ②    ③                     ④     │
│          │    │    │                     │     │
│        ERROR WARN CRIT              [Subscriber│
│        sent  sent  sent              joins]   │
│                                                │
│ ① [ERROR] Boot sequence failed                │
│ ② [WARN] Temperature high                     │
│ ③ [CRIT] System shutdown imminent             │
│ ④ Late-joiner immediately receives ①②③        │
└────────────────────────────────────────────────┘
```

## 7. Performance Measurement Strategy

### 7.1 Zero-Allocation Proof
```csharp
// Before loop
var gcBefore = GC.GetTotalAllocatedBytes(true);
var sw = Stopwatch.StartNew();

// Zero-copy loop
while (subscriber.WaitData(timeout))
{
    using var samples = subscriber.Take();
    foreach (var sample in samples)
    {
        var view = sample.AsView();
        // Process view without allocations
        messagesProcessed++;
    }
}

// After loop
sw.Stop();
var gcAfter = GC.GetTotalAllocatedBytes(true);
var allocated = gcAfter - gcBefore;
var throughput = messagesProcessed / sw.Elapsed.TotalSeconds;
```

### 7.2 Comparison Mode
- Toggle between Managed and Zero-Copy modes
- Side-by-side metrics display
- Managed mode intentionally calls `ToManaged()` to show allocations

### 7.3 Live Updates
- Update metrics every 500ms during execution
- Use Spectre.Console `LiveDisplay` or `Progress` bars
- Don't measure inside hot loop (interferes with performance)

## 8. Error Handling and Diagnostics

### 8.1 Network Discovery Issues
When peer not found:
```
⚠ Waiting for peer connection... (15s elapsed)

Diagnostics:
  Domain ID: 0
  Local Interfaces:
    - 192.168.1.55 (Ethernet)
    - 127.0.0.1 (Loopback)
  Participants Discovered: 0

Tip: Check firewall settings and ensure both nodes use same Domain ID
```

### 8.2 Peer Disconnection
```
⚠ Peer Lost! 

  Last seen: 2 seconds ago
  Pausing demo...
  
  Press ESC to cancel or waiting for reconnection...
```

### 8.3 Timeout Protection
- All `WaitForReader/Writer` calls have 30s timeout
- User-friendly error messages
- Graceful fallback to menu

## 9. Code Organization Principles

### 9.1 Separation of Concerns
- **Scenario Classes**: Pure DDS logic, no UI code
- **Publisher/Subscriber Classes**: Reusable examples
- **UI Classes**: Only Spectre.Console rendering
- **User can copy Publisher/Subscriber files directly to their projects**

### 9.2 Interface-Based Design
```csharp
public interface IDemoScenario
{
    string Name { get; }
    string Description { get; }
    
    Task RunStandaloneAsync(CancellationToken ct);
    Task RunPublisherAsync(CancellationToken ct);
    Task RunSubscriberAsync(CancellationToken ct);
    
    void DisplayInstructions();
}
```

### 9.3 Testability
Each component should be unit-testable independently:
- Schema validation
- Publisher behavior
- Subscriber behavior
- UI rendering (can use mocked console)
- Control flow logic

## 10. Dependencies

### 10.1 NuGet Packages
```xml
<ItemGroup>
  <PackageReference Include="Spectre.Console" Version="0.49.1" />
  <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
</ItemGroup>

<ItemGroup>
  <ProjectReference Include="..\..\src\CycloneDDS.Core\CycloneDDS.Core.csproj" />
  <ProjectReference Include="..\..\src\CycloneDDS.Runtime\CycloneDDS.Runtime.csproj" />
  <ProjectReference Include="..\..\src\CycloneDDS.Schema\CycloneDDS.Schema.csproj" />
</ItemGroup>
```

### 10.2 Native DLLs
- Copy `ddsc.dll` and dependencies to output directory
- Handle via existing build configuration

## 11. Testing Strategy

Each task will have associated unit tests to verify:
- Schema generation succeeds
- Publishers can write data
- Subscribers can read data
- Filters work correctly
- Instance lookup returns correct data
- Control messages synchronize properly
- UI renders without exceptions

## 12. Future Enhancements (Out of Scope)

- Web-based dashboard (Blazor)
- Recording/playback of scenarios
- Custom scenario builder
- Performance profiling export (CSV/JSON)
- Integration with CI/CD metrics
