# FeatureDemo Application - Task List

## Task Organization

Tasks are organized by component and build upon each other. Each task includes:
- **Definition**: What needs to be built
- **Design Reference**: Section in FeatureDemo-Design.md
- **Unit Test**: Specific test to verify completion
- **Dependencies**: Prerequisites

---

## Phase 1: Foundation (Tasks 1-5)

### Task 1: Project Setup and Schema Definitions
**Definition**: Create the FeatureDemo project structure with all data schema definitions.

**Design Reference**: Section 2 (Project Structure), Section 3 (Data Schema)

**Implementation**:
- Create `examples/FeatureDemo/` directory
- Create `FeatureDemo.csproj` with .NET 8, Spectre.Console, project references
- Create `Schema.cs` with all 6 topic definitions:
  - `DemoControl`
  - `ChatMessage`
  - `SensorData`
  - `StockTick`
  - `FlightPosition`
  - `SystemLog`
- Create enum definitions: `ControlCommand`, `LogLevel`

**Unit Test**:
```csharp
[Test]
public void SchemaDefinitions_CompileSuccessfully()
{
    // Verify project builds without errors
    // Verify code generation produces expected types
    Assert.That(typeof(DemoControl), Is.Not.Null);
    Assert.That(typeof(ChatMessage), Is.Not.Null);
    Assert.That(typeof(SensorData), Is.Not.Null);
    Assert.That(typeof(StockTick), Is.Not.Null);
    Assert.That(typeof(FlightPosition), Is.Not.Null);
    Assert.That(typeof(SystemLog), Is.Not.Null);
}

[Test]
public void DemoControl_HasCorrectQoS()
{
    // Verify DemoControl has TransientLocal + Reliable
    var attr = typeof(DemoControl).GetCustomAttribute<DdsQosAttribute>();
    Assert.That(attr.Reliability, Is.EqualTo(ReliabilityQosPolicyKind.Reliable));
    Assert.That(attr.Durability, Is.EqualTo(DurabilityQosPolicyKind.TransientLocal));
}
```

**Completion Criteria**: Project compiles, all schemas generate code successfully.

---

### Task 2: Core Orchestration Infrastructure
**Definition**: Implement mode selection, CLI parsing, and base orchestrator.

**Design Reference**: Section 4 (Application Modes), Section 4.4 (Interactive CLI)

**Implementation**:
- Create `Orchestration/DemoMode.cs` enum (Standalone, Master, Slave, Autonomous)
- Create `Program.cs` with CLI argument parsing
- Create interactive mode selection prompt (if no args)
- Create `Orchestration/DemoOrchestrator.cs` base class with:
  - Mode property
  - Participant creation
  - Shutdown handling

**Unit Test**:
```csharp
[Test]
public void ParseArguments_StandaloneMode_ReturnsCorrectMode()
{
    var mode = Program.ParseArguments(new[] { "--mode", "standalone" });
    Assert.That(mode, Is.EqualTo(DemoMode.Standalone));
}

[Test]
public void ParseArguments_NoArgs_ReturnsInteractive()
{
    var mode = Program.ParseArguments(Array.Empty<string>());
    Assert.That(mode, Is.EqualTo(DemoMode.Interactive));
}

[Test]
public void DemoOrchestrator_CreateParticipant_Success()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var participant = orchestrator.GetParticipant();
    Assert.That(participant, Is.Not.Null);
}
```

**Completion Criteria**: Can launch app in any mode via CLI or interactive prompt.

---

### Task 3: Control Channel Manager
**Definition**: Implement handshake and scenario synchronization using DemoControl topic.

**Design Reference**: Section 5 (Synchronization and Handshake)

**Implementation**:
- Create `Scenarios/ControlChannelManager.cs`
- Implement `SendHandshakeAsync()`
- Implement `WaitForPeerHandshakeAsync(timeout)`
- Implement `SendStartScenarioAsync(scenarioId)`
- Implement `SendStopScenarioAsync()`
- Implement `WaitForCommandAsync()`
- Implement timeout handling with diagnostic info

**Unit Test**:
```csharp
[Test]
public async Task ControlChannel_Handshake_SucceedsInStandaloneMode()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var master = new ControlChannelManager(orchestrator.GetParticipant(), nodeId: 0);
    var slave = new ControlChannelManager(orchestrator.GetParticipant(), nodeId: 1);
    
    await master.SendHandshakeAsync();
    var received = await slave.WaitForPeerHandshakeAsync(TimeSpan.FromSeconds(5));
    
    Assert.That(received, Is.True);
}

[Test]
public async Task ControlChannel_StartScenario_ReceivedBySlave()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var master = new ControlChannelManager(orchestrator.GetParticipant(), nodeId: 0);
    var slave = new ControlChannelManager(orchestrator.GetParticipant(), nodeId: 1);
    
    var receiveTask = slave.WaitForCommandAsync(CancellationToken.None);
    await Task.Delay(100);
    await master.SendStartScenarioAsync(2);
    
    var command = await receiveTask;
    Assert.That(command.Command, Is.EqualTo(ControlCommand.StartScenario));
    Assert.That(command.ScenarioId, Is.EqualTo(2));
}
```

**Completion Criteria**: Master and Slave can discover each other and exchange control messages.

---

### Task 4: Scenario Interface and Base Classes
**Definition**: Define common interfaces and utilities for all scenarios.

**Design Reference**: Section 9.2 (Interface-Based Design)

**Implementation**:
- Create `Scenarios/IDemoScenario.cs` interface
- Create base utilities:
  - `ScenarioBase` abstract class implementing common patterns
  - Helper methods for participant waiting
  - Cancellation token handling

**Unit Test**:
```csharp
[Test]
public void IDemoScenario_Interface_HasRequiredMembers()
{
    var interfaceType = typeof(IDemoScenario);
    Assert.That(interfaceType.GetProperty("Name"), Is.Not.Null);
    Assert.That(interfaceType.GetProperty("Description"), Is.Not.Null);
    Assert.That(interfaceType.GetMethod("RunStandaloneAsync"), Is.Not.Null);
    Assert.That(interfaceType.GetMethod("RunPublisherAsync"), Is.Not.Null);
    Assert.That(interfaceType.GetMethod("RunSubscriberAsync"), Is.Not.Null);
}
```

**Completion Criteria**: Interface defined, base class provides reusable functionality.

---

### Task 5: Diagnostic Header and UI Infrastructure
**Definition**: Create reusable UI components for status display.

**Design Reference**: Section 6.1 (Diagnostic Header)

**Implementation**:
- Create `UI/DiagnosticHeader.cs`
- Display: Mode, IP address(es), Domain ID, Peer count
- Update dynamically
- Create `UI/WaitingScreen.cs` for peer connection waiting
- Show network diagnostics during wait

**Unit Test**:
```csharp
[Test]
public void DiagnosticHeader_GetLocalIPs_ReturnsNonLoopback()
{
    var ips = DiagnosticHeader.GetLocalIPAddresses();
    Assert.That(ips, Is.Not.Empty);
    Assert.That(ips.Any(ip => ip != "127.0.0.1"), Is.True);
}

[Test]
public void DiagnosticHeader_Render_DoesNotThrow()
{
    var header = new DiagnosticHeader(DemoMode.Master, domainId: 0);
    Assert.DoesNotThrow(() => header.Render());
}

[Test]
public async Task WaitingScreen_ShowWithTimeout_ReturnsAfterTimeout()
{
    var screen = new WaitingScreen();
    var sw = Stopwatch.StartNew();
    var result = await screen.WaitForPeerAsync(timeout: TimeSpan.FromSeconds(1));
    sw.Stop();
    
    Assert.That(result, Is.False);
    Assert.That(sw.Elapsed.TotalSeconds, Is.EqualTo(1).Within(0.2));
}
```

**Completion Criteria**: Header displays correct info, waiting screen shows diagnostics.

---

## Phase 2: Scenario A - Chat Room (Tasks 6-8)

### Task 6: Chat Room Publisher
**Definition**: Implement pure DDS publisher for chat messages.

**Design Reference**: Section 3.2 (Chat Room Schema)

**Implementation**:
- Create `Scenarios/ChatRoom/ChatRoomPublisher.cs`
- Constructor takes participant, creates writer
- Method `SendMessage(user, content)`
- Proper disposal
- Enable sender tracking on participant

**Unit Test**:
```csharp
[Test]
public async Task ChatRoomPublisher_SendMessage_Success()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var participant = orchestrator.GetParticipant();
    participant.EnableSenderTracking();
    
    using var publisher = new ChatRoomPublisher(participant);
    Assert.DoesNotThrow(() => publisher.SendMessage("Alice", "Hello World"));
}

[Test]
public async Task ChatRoomPublisher_MultipleMessages_AllSent()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var participant = orchestrator.GetParticipant();
    
    using var publisher = new ChatRoomPublisher(participant);
    for (int i = 0; i < 10; i++)
    {
        publisher.SendMessage($"User{i}", $"Message {i}");
    }
    // Should not throw
}
```

**Completion Criteria**: Can create publisher and send chat messages.

---

### Task 7: Chat Room Subscriber
**Definition**: Implement pure DDS subscriber for chat messages.

**Design Reference**: Section 3.2 (Chat Room Schema)

**Implementation**:
- Create `Scenarios/ChatRoom/ChatRoomSubscriber.cs`
- Constructor takes participant, creates reader
- Method `WaitForMessagesAsync(callback, ct)`
- Retrieve sender information from sample
- Proper disposal

**Unit Test**:
```csharp
[Test]
public async Task ChatRoomSubscriber_ReceiveMessage_Success()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var participant = orchestrator.GetParticipant();
    participant.EnableSenderTracking();
    
    using var publisher = new ChatRoomPublisher(participant);
    using var subscriber = new ChatRoomSubscriber(participant);
    
    ChatMessage? received = null;
    var receiveTask = Task.Run(async () =>
    {
        await subscriber.WaitForMessagesAsync((msg, sender) =>
        {
            received = msg;
            return false; // Stop after first message
        }, CancellationToken.None);
    });
    
    await Task.Delay(100);
    publisher.SendMessage("Alice", "Test Message");
    
    await receiveTask.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.That(received, Is.Not.Null);
    Assert.That(received.Value.User.ToString(), Is.EqualTo("Alice"));
    Assert.That(received.Value.Content.ToString(), Contains.Substring("Test"));
}
```

**Completion Criteria**: Can receive messages and extract sender info.

---

### Task 8: Chat Room UI and Scenario Integration
**Definition**: Create visualization and integrate into scenario system.

**Design Reference**: Section 6 (UI/UX Design)

**Implementation**:
- Create `Scenarios/ChatRoom/ChatRoomUI.cs`
- Display messages in colored panels
- Show sender name and source info
- Create `Scenarios/ChatRoom/ChatRoomScenario.cs` implementing `IDemoScenario`
- Wire up standalone, master, and slave modes

**Unit Test**:
```csharp
[Test]
public void ChatRoomUI_Render_DoesNotThrow()
{
    var ui = new ChatRoomUI();
    var message = new ChatMessage
    {
        MessageId = 1,
        User = new FixedString32("Alice"),
        Content = new FixedString128("Hello"),
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    };
    
    Assert.DoesNotThrow(() => ui.DisplayMessage(message, senderInfo: "PC-001"));
}

[Test]
public async Task ChatRoomScenario_Standalone_Completes()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var scenario = new ChatRoomScenario(orchestrator);
    
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    await scenario.RunStandaloneAsync(cts.Token);
    // Should complete without exception
}
```

**Completion Criteria**: Scenario runs in standalone mode, displays messages correctly.

---

## Phase 3: Scenario B - Sensor Array (Tasks 9-12)

### Task 9: Sensor Array Publisher
**Definition**: High-frequency publisher demonstrating performance.

**Design Reference**: Section 3.3 (Sensor Array Schema)

**Implementation**:
- Create `Scenarios/SensorArray/SensorPublisher.cs`
- Method `StartPublishing(frequency, duration, ct)`
- Generate realistic sensor data at high rate (10,000+ msg/s)
- Track messages sent
- Best-effort QoS for performance

**Unit Test**:
```csharp
[Test]
public async Task SensorPublisher_HighFrequency_AchievesTargetRate()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var participant = orchestrator.GetParticipant();
    
    using var publisher = new SensorPublisher(participant);
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
    
    var count = await publisher.StartPublishingAsync(frequency: 1000, cts.Token);
    
    Assert.That(count, Is.GreaterThan(900)); // Allow 10% tolerance
}
```

**Completion Criteria**: Can sustain 1,000+ messages/second.

---

### Task 10: Sensor Array Subscriber (Zero-Copy)
**Definition**: Zero-allocation subscriber using AsView() API.

**Design Reference**: Section 3.3, Section 7.1 (Performance Measurement)

**Implementation**:
- Create `Scenarios/SensorArray/SensorSubscriber.cs`
- Method `ReceiveWithZeroCopyAsync(ct)` using `AsView()`
- Method `ReceiveWithManagedAsync(ct)` using `ToManaged()` for comparison
- Track GC allocations using `GC.GetTotalAllocatedBytes()`
- Return metrics: message count, duration, bytes allocated

**Unit Test**:
```csharp
[Test]
public async Task SensorSubscriber_ZeroCopy_NoAllocations()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var participant = orchestrator.GetParticipant();
    
    using var publisher = new SensorPublisher(participant);
    using var subscriber = new SensorSubscriber(participant);
    
    var publishTask = publisher.StartPublishingAsync(frequency: 100, 
        new CancellationTokenSource(2000).Token);
    
    await Task.Delay(100); // Let publisher start
    
    var metrics = await subscriber.ReceiveWithZeroCopyAsync(
        new CancellationTokenSource(2000).Token);
    
    Assert.That(metrics.MessagesReceived, Is.GreaterThan(100));
    Assert.That(metrics.BytesAllocated, Is.LessThan(1024)); // Minimal allocations
}

[Test]
public async Task SensorSubscriber_Managed_HasAllocations()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var participant = orchestrator.GetParticipant();
    
    using var publisher = new SensorPublisher(participant);
    using var subscriber = new SensorSubscriber(participant);
    
    var publishTask = publisher.StartPublishingAsync(frequency: 100, 
        new CancellationTokenSource(2000).Token);
    
    await Task.Delay(100);
    
    var metrics = await subscriber.ReceiveWithManagedAsync(
        new CancellationTokenSource(2000).Token);
    
    Assert.That(metrics.MessagesReceived, Is.GreaterThan(100));
    Assert.That(metrics.BytesAllocated, Is.GreaterThan(10000)); // Significant allocations
}
```

**Completion Criteria**: Zero-copy mode allocates <1KB, managed mode shows measurable allocations.

---

### Task 11: Sensor Array Performance Visualization
**Definition**: Bar chart showing throughput and GC pressure.

**Design Reference**: Section 6.3 (Sensor Array Visualization)

**Implementation**:
- Create `Scenarios/SensorArray/SensorUI.cs`
- Live-updating bar chart with Spectre.Console
- Display throughput (messages/sec)
- Display GC allocation delta
- Toggle between zero-copy and managed modes
- Update every 500ms

**Unit Test**:
```csharp
[Test]
public void SensorUI_Render_DoesNotThrow()
{
    var ui = new SensorUI();
    var metrics = new PerformanceMetrics
    {
        MessagesReceived = 1000,
        Duration = TimeSpan.FromSeconds(1),
        BytesAllocated = 0
    };
    
    Assert.DoesNotThrow(() => ui.UpdateMetrics(metrics));
}

[Test]
public void SensorUI_ToggleMode_ChangesDisplay()
{
    var ui = new SensorUI();
    Assert.That(ui.CurrentMode, Is.EqualTo(SensorMode.ZeroCopy));
    
    ui.ToggleMode();
    Assert.That(ui.CurrentMode, Is.EqualTo(SensorMode.Managed));
}
```

**Completion Criteria**: UI displays live metrics, clearly shows allocation difference.

---

### Task 12: Sensor Array Scenario Integration
**Definition**: Complete scenario with all modes.

**Design Reference**: Section 9.2 (Interface-Based Design)

**Implementation**:
- Create `Scenarios/SensorArray/SensorArrayScenario.cs`
- Implement standalone, master, slave modes
- Coordinate mode switching between publisher/subscriber
- Handle graceful cancellation

**Unit Test**:
```csharp
[Test]
public async Task SensorArrayScenario_Standalone_ShowsPerformance()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var scenario = new SensorArrayScenario(orchestrator);
    
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await scenario.RunStandaloneAsync(cts.Token);
    // Should complete and display metrics
}

[Test]
public async Task SensorArrayScenario_MasterSlave_Synchronize()
{
    using var masterOrch = new DemoOrchestrator(DemoMode.Master);
    using var slaveOrch = new DemoOrchestrator(DemoMode.Slave);
    
    var masterScenario = new SensorArrayScenario(masterOrch);
    var slaveScenario = new SensorArrayScenario(slaveOrch);
    
    var slaveTask = slaveScenario.RunSubscriberAsync(
        new CancellationTokenSource(10000).Token);
    
    await Task.Delay(500); // Let slave start
    
    var masterTask = masterScenario.RunPublisherAsync(
        new CancellationTokenSource(5000).Token);
    
    await Task.WhenAll(masterTask, slaveTask);
    // Should complete without exception
}
```

**Completion Criteria**: Demonstrates measurable zero-allocation performance.

---

## Phase 4: Scenario C - Stock Ticker (Tasks 13-15)

### Task 13: Stock Ticker Publisher
**Definition**: Multi-symbol stock data publisher.

**Design Reference**: Section 3.4 (Stock Ticker Schema)

**Implementation**:
- Create `Scenarios/StockTicker/StockPublisher.cs`
- Publish multiple symbols: AAPL, MSFT, GOOG, TSLA
- Random price fluctuations
- Continuous publishing at moderate rate (10-50 msg/s)

**Unit Test**:
```csharp
[Test]
public async Task StockPublisher_MultipleSymbols_AllPublished()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var participant = orchestrator.GetParticipant();
    
    using var publisher = new StockPublisher(participant);
    var symbols = new HashSet<string>();
    
    using var subscriber = participant.CreateSubscriber<StockTick>();
    var readTask = Task.Run(() =>
    {
        for (int i = 0; i < 20; i++)
        {
            if (subscriber.WaitData(TimeSpan.FromSeconds(1)))
            {
                using var samples = subscriber.Take();
                foreach (var sample in samples)
                {
                    symbols.Add(sample.Data.Symbol.ToString());
                }
            }
        }
    });
    
    await publisher.StartPublishingAsync(new CancellationTokenSource(2000).Token);
    await readTask;
    
    Assert.That(symbols, Contains.Item("AAPL"));
    Assert.That(symbols, Contains.Item("MSFT"));
    Assert.That(symbols.Count, Is.GreaterThanOrEqualTo(3));
}
```

**Completion Criteria**: Publishes multiple symbols with varying prices.

---

### Task 14: Stock Ticker Filtered Subscriber
**Definition**: Subscriber with content-based filtering.

**Design Reference**: Section 3.4 (Stock Ticker Schema)

**Implementation**:
- Create `Scenarios/StockTicker/StockSubscriber.cs`
- Method `SetFilter(symbol, minPrice)` using predicate
- Track filtered vs. total messages
- Return statistics

**Unit Test**:
```csharp
[Test]
public async Task StockSubscriber_Filter_OnlyMatchingSymbols()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var participant = orchestrator.GetParticipant();
    
    using var publisher = new StockPublisher(participant);
    using var subscriber = new StockSubscriber(participant);
    
    subscriber.SetFilter("AAPL", minPrice: 150.0);
    
    var publishTask = publisher.StartPublishingAsync(
        new CancellationTokenSource(3000).Token);
    
    var received = new List<StockTick>();
    var receiveTask = Task.Run(async () =>
    {
        await subscriber.ReceiveFilteredAsync((tick) =>
        {
            received.Add(tick);
            return received.Count < 10;
        }, new CancellationTokenSource(3000).Token);
    });
    
    await receiveTask;
    
    Assert.That(received.Count, Is.GreaterThan(0));
    Assert.That(received.All(t => t.Symbol.ToString() == "AAPL"), Is.True);
    Assert.That(received.All(t => t.Price >= 150.0), Is.True);
}
```

**Completion Criteria**: Filter correctly excludes non-matching data.

---

### Task 15: Stock Ticker Split-View UI and Scenario
**Definition**: Visualization showing filtered vs. unfiltered feed.

**Design Reference**: Section 6.4 (Stock Ticker Split View)

**Implementation**:
- Create `Scenarios/StockTicker/StockUI.cs`
- Two-column layout: Network Traffic | Filtered Feed
- Highlight when filtered message passes through
- Create `Scenarios/StockTicker/StockTickerScenario.cs`
- Implement all modes

**Unit Test**:
```csharp
[Test]
public void StockUI_SplitView_Renders()
{
    var ui = new StockUI();
    var tick = new StockTick
    {
        TickId = 1,
        Symbol = new FixedString32("AAPL"),
        Price = 155.50,
        Volume = 1000,
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    };
    
    Assert.DoesNotThrow(() => ui.DisplayTick(tick, filtered: true));
    Assert.DoesNotThrow(() => ui.DisplayTick(tick, filtered: false));
}

[Test]
public async Task StockTickerScenario_Standalone_Filters()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var scenario = new StockTickerScenario(orchestrator);
    
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await scenario.RunStandaloneAsync(cts.Token);
    // Should display split view
}
```

**Completion Criteria**: Split view clearly shows filtering in action.

---

## Phase 5: Scenario D - Flight Radar (Tasks 16-18)

### Task 16: Flight Radar Publisher
**Definition**: Multi-instance publisher with keyed data.

**Design Reference**: Section 3.5 (Flight Radar Schema)

**Implementation**:
- Create `Scenarios/FlightRadar/FlightPublisher.cs`
- Simulate 3-5 flights with unique IDs
- Update positions periodically
- Each flight follows a trajectory

**Unit Test**:
```csharp
[Test]
public async Task FlightPublisher_MultipleFlights_AllInstances()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var participant = orchestrator.GetParticipant();
    
    using var publisher = new FlightPublisher(participant);
    using var subscriber = participant.CreateSubscriber<FlightPosition>();
    
    var publishTask = publisher.StartPublishingAsync(
        new CancellationTokenSource(2000).Token);
    
    var flightIds = new HashSet<string>();
    var readTask = Task.Run(() =>
    {
        for (int i = 0; i < 20; i++)
        {
            if (subscriber.WaitData(TimeSpan.FromSeconds(1)))
            {
                using var samples = subscriber.Take();
                foreach (var sample in samples)
                {
                    flightIds.Add(sample.Data.FlightId.ToString());
                }
            }
        }
    });
    
    await readTask;
    
    Assert.That(flightIds.Count, Is.GreaterThanOrEqualTo(3));
}
```

**Completion Criteria**: Multiple flight instances published with unique keys.

---

### Task 17: Flight Radar Instance Lookup Subscriber
**Definition**: Demonstrate O(1) instance lookup and history.

**Design Reference**: Section 3.5 (Flight Radar Schema)

**Implementation**:
- Create `Scenarios/FlightRadar/FlightSubscriber.cs`
- Method `LookupFlight(flightId)` using `LookupInstance`
- Method `GetFlightHistory(handle)` using `ReadInstance`
- Return position history for specific flight

**Unit Test**:
```csharp
[Test]
public async Task FlightSubscriber_LookupInstance_ReturnsHistory()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var participant = orchestrator.GetParticipant();
    
    using var publisher = new FlightPublisher(participant);
    using var subscriber = new FlightSubscriber(participant);
    
    var publishTask = publisher.StartPublishingAsync(
        new CancellationTokenSource(3000).Token);
    
    await Task.Delay(1000); // Let some data accumulate
    
    var history = await subscriber.GetFlightHistoryAsync("BA-123");
    
    Assert.That(history, Is.Not.Empty);
    Assert.That(history.All(p => p.FlightId.ToString() == "BA-123"), Is.True);
}

[Test]
public async Task FlightSubscriber_LookupInstance_OOne()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var participant = orchestrator.GetParticipant();
    
    using var publisher = new FlightPublisher(participant);
    using var subscriber = new FlightSubscriber(participant);
    
    await publisher.StartPublishingAsync(
        new CancellationTokenSource(1000).Token);
    
    var sw = Stopwatch.StartNew();
    var history = await subscriber.GetFlightHistoryAsync("BA-123");
    sw.Stop();
    
    Assert.That(sw.Elapsed.TotalMilliseconds, Is.LessThan(10)); // O(1) lookup
}
```

**Completion Criteria**: Lookup returns correct instance history in <10ms.

---

### Task 18: Flight Radar Table UI and Scenario
**Definition**: Table visualization of flight history.

**Design Reference**: Section 6.5 (Flight Radar Instance Lookup)

**Implementation**:
- Create `Scenarios/FlightRadar/FlightUI.cs`
- Display table with timestamp, position, altitude
- Allow user to select which flight to track
- Create `Scenarios/FlightRadar/FlightRadarScenario.cs`
- Implement all modes

**Unit Test**:
```csharp
[Test]
public void FlightUI_Table_Renders()
{
    var ui = new FlightUI();
    var positions = new List<FlightPosition>
    {
        new FlightPosition 
        { 
            FlightId = new FixedString32("BA-123"),
            Latitude = 51.5,
            Longitude = -0.1,
            Altitude = 35000
        }
    };
    
    Assert.DoesNotThrow(() => ui.DisplayHistory("BA-123", positions));
}

[Test]
public async Task FlightRadarScenario_Standalone_TracksFlights()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var scenario = new FlightRadarScenario(orchestrator);
    
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await scenario.RunStandaloneAsync(cts.Token);
    // Should display table
}
```

**Completion Criteria**: Can select flight and see its history instantly.

---

## Phase 6: Scenario E - Black Box (Tasks 19-21)

### Task 19: Black Box Publisher
**Definition**: Publisher with transient local durability.

**Design Reference**: Section 3.6 (Black Box Schema)

**Implementation**:
- Create `Scenarios/BlackBox/BlackBoxPublisher.cs`
- Publish critical system logs
- Use TransientLocal + KeepAll QoS
- Publish before subscriber exists (for demo)

**Unit Test**:
```csharp
[Test]
public async Task BlackBoxPublisher_TransientLocal_DataPersists()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var participant = orchestrator.GetParticipant();
    
    using var publisher = new BlackBoxPublisher(participant);
    
    publisher.LogError("System", "Critical failure");
    publisher.LogWarning("Temperature", "Overheating");
    
    // Create subscriber AFTER publishing
    await Task.Delay(100);
    
    using var subscriber = participant.CreateSubscriber<SystemLog>();
    
    var received = new List<SystemLog>();
    if (subscriber.WaitData(TimeSpan.FromSeconds(2)))
    {
        using var samples = subscriber.Take();
        foreach (var sample in samples)
        {
            received.Add(sample.Data);
        }
    }
    
    Assert.That(received.Count, Is.EqualTo(2));
}
```

**Completion Criteria**: Late-joining subscriber receives historical data.

---

### Task 20: Black Box Late-Joining Subscriber
**Definition**: Demonstrate durability with late joiner.

**Design Reference**: Section 3.6 (Black Box Schema)

**Implementation**:
- Create `Scenarios/BlackBox/BlackBoxSubscriber.cs`
- Method `WaitAndReceiveHistoricalData()`
- Track when subscriber was created vs. data timestamps
- Prove data was sent before subscriber existed

**Unit Test**:
```csharp
[Test]
public async Task BlackBoxSubscriber_LateJoiner_ReceivesHistory()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var participant = orchestrator.GetParticipant();
    
    using var publisher = new BlackBoxPublisher(participant);
    
    var sendTime = DateTimeOffset.UtcNow;
    publisher.LogCritical("Engine", "Failure detected");
    
    await Task.Delay(500); // Simulate delay before subscriber
    
    using var subscriber = new BlackBoxSubscriber(participant);
    var logs = await subscriber.ReceiveAllAsync();
    
    Assert.That(logs, Is.Not.Empty);
    Assert.That(logs[0].Timestamp, Is.LessThan(sendTime.ToUnixTimeMilliseconds() + 100));
}
```

**Completion Criteria**: Subscriber receives data sent before it existed.

---

### Task 21: Black Box Timeline UI and Scenario
**Definition**: Visual timeline showing late-joining concept.

**Design Reference**: Section 6.6 (Black Box Timeline)

**Implementation**:
- Create `Scenarios/BlackBox/BlackBoxUI.cs`
- Timeline visualization with markers
- Show "Publisher sends" then "Subscriber joins"
- Display received historical logs
- Create `Scenarios/BlackBox/BlackBoxScenario.cs`
- Implement all modes with dramatic pause

**Unit Test**:
```csharp
[Test]
public void BlackBoxUI_Timeline_Renders()
{
    var ui = new BlackBoxUI();
    var logs = new List<SystemLog>
    {
        new SystemLog
        {
            LogId = 1,
            Level = LogLevel.Critical,
            Component = new FixedString64("Engine"),
            Message = new FixedString256("Failure"),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        }
    };
    
    Assert.DoesNotThrow(() => ui.DisplayTimeline(logs, subscriberJoinTime: DateTimeOffset.UtcNow));
}

[Test]
public async Task BlackBoxScenario_Standalone_ShowsDurability()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var scenario = new BlackBoxScenario(orchestrator);
    
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await scenario.RunStandaloneAsync(cts.Token);
    // Should show timeline
}
```

**Completion Criteria**: Timeline clearly shows late-joiner receiving historical data.

---

## Phase 7: Main Menu and Autonomous Mode (Tasks 22-24)

### Task 22: Main Menu Implementation
**Definition**: Interactive menu for scenario selection.

**Design Reference**: Section 6.2 (Main Menu)

**Implementation**:
- Create `UI/MainMenu.cs`
- Display all 5 scenarios plus autonomous mode
- Handle user input
- Show description for each scenario
- Coordinate with control channel for master/slave sync

**Unit Test**:
```csharp
[Test]
public void MainMenu_Display_AllScenariosListed()
{
    var menu = new MainMenu();
    var scenarios = menu.GetScenarios();
    
    Assert.That(scenarios.Count, Is.EqualTo(6)); // 5 scenarios + autonomous
    Assert.That(scenarios.Any(s => s.Name.Contains("Chat")), Is.True);
    Assert.That(scenarios.Any(s => s.Name.Contains("Sensor")), Is.True);
    Assert.That(scenarios.Any(s => s.Name.Contains("Stock")), Is.True);
    Assert.That(scenarios.Any(s => s.Name.Contains("Flight")), Is.True);
    Assert.That(scenarios.Any(s => s.Name.Contains("Black Box")), Is.True);
}

[Test]
public async Task MainMenu_SelectScenario_LaunchesCorrectOne()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var menu = new MainMenu(orchestrator);
    
    // Simulate selecting scenario 1 (Chat Room)
    var scenario = menu.GetScenarioById(1);
    
    Assert.That(scenario, Is.InstanceOf<ChatRoomScenario>());
}
```

**Completion Criteria**: Menu displays and launches scenarios correctly.

---

### Task 23: Autonomous Demo Mode
**Definition**: Auto-run all scenarios in sequence.

**Design Reference**: Section 4 (Autonomous Mode)

**Implementation**:
- Create autonomous mode runner
- Execute scenarios 1-5 sequentially
- 2-3 second delay between scenarios
- Clear console between demos
- Show progress indicator
- Handle cancellation gracefully

**Unit Test**:
```csharp
[Test]
public async Task AutonomousMode_RunsAllScenarios()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var autonomous = new AutonomousDemoRunner(orchestrator);
    
    var executed = new List<string>();
    autonomous.OnScenarioStart += (name) => executed.Add(name);
    
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    await autonomous.RunAsync(cts.Token);
    
    Assert.That(executed.Count, Is.EqualTo(5));
}

[Test]
public async Task AutonomousMode_Cancellation_StopsGracefully()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var autonomous = new AutonomousDemoRunner(orchestrator);
    
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    
    await autonomous.RunAsync(cts.Token);
    // Should stop without throwing
}
```

**Completion Criteria**: Runs all 5 scenarios automatically with proper timing.

---

### Task 24: Integration Testing and Polish
**Definition**: End-to-end tests and final polish.

**Design Reference**: All sections

**Implementation**:
- Create integration tests for all modes
- Test master/slave synchronization across processes
- Error handling for network issues
- Graceful degradation
- Performance validation
- Documentation comments
- README for examples/FeatureDemo

**Unit Test**:
```csharp
[Test]
public async Task Integration_StandaloneMode_AllScenarios()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    
    foreach (var scenarioId in new[] { 1, 2, 3, 4, 5 })
    {
        var scenario = ScenarioFactory.Create(scenarioId, orchestrator);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        
        await scenario.RunStandaloneAsync(cts.Token);
    }
    // All should complete
}

[Test]
public async Task Integration_MasterSlaveHandshake_Success()
{
    using var master = new DemoOrchestrator(DemoMode.Master);
    using var slave = new DemoOrchestrator(DemoMode.Slave);
    
    var masterControl = new ControlChannelManager(master.GetParticipant(), 0);
    var slaveControl = new ControlChannelManager(slave.GetParticipant(), 1);
    
    var handshakeTask1 = masterControl.SendHandshakeAsync();
    var handshakeTask2 = slaveControl.SendHandshakeAsync();
    var waitTask1 = masterControl.WaitForPeerHandshakeAsync(TimeSpan.FromSeconds(5));
    var waitTask2 = slaveControl.WaitForPeerHandshakeAsync(TimeSpan.FromSeconds(5));
    
    await Task.WhenAll(handshakeTask1, handshakeTask2, waitTask1, waitTask2);
    
    Assert.That(await waitTask1, Is.True);
    Assert.That(await waitTask2, Is.True);
}

[Test]
public async Task Integration_PerformanceBaseline_Sensor()
{
    using var orchestrator = new DemoOrchestrator(DemoMode.Standalone);
    var scenario = new SensorArrayScenario(orchestrator);
    
    // Run in zero-copy mode
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await scenario.RunStandaloneAsync(cts.Token);
    
    // Verify performance met expectations
    var metrics = scenario.GetLastMetrics();
    Assert.That(metrics.Throughput, Is.GreaterThan(1000)); // 1000 msg/s minimum
    Assert.That(metrics.BytesAllocated, Is.LessThan(10240)); // <10KB allocations
}
```

**Completion Criteria**: All scenarios work in all modes, performance targets met.

---

## Summary

**Total Tasks**: 24
**Estimated Completion**: Progressive implementation, each task builds on previous

**Success Criteria**:
- All 24 unit tests pass
- All 5 scenarios work in standalone, master, and slave modes
- Autonomous mode runs all scenarios sequentially
- Performance targets met (1000+ msg/s, <10KB allocations for zero-copy)
- UI is intuitive and informative
- Code is reusable (Publisher/Subscriber classes usable outside demo)

**Testing Strategy**:
- Unit tests for each component
- Integration tests for end-to-end flows
- Performance benchmarks for sensor scenario
- Manual testing for UI/UX validation
