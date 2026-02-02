using CycloneDDS.Schema;

namespace FeatureDemo;

// ============================================================================
// CONTROL CHANNEL (Orchestration)
// ============================================================================

/// <summary>
/// Control channel for coordinating demo scenarios between Master and Slave nodes.
/// Uses TransientLocal durability to ensure late-joining slaves receive current command.
/// </summary>
[DdsTopic("DemoControl")]
[DdsQos(
    Reliability = DdsReliability.Reliable,
    Durability = DdsDurability.TransientLocal)]
public partial struct DemoControl
{
    /// <summary>Node identifier: 0 = Master, 1 = Slave</summary>
    [DdsKey]
    public byte NodeId;

    /// <summary>Control command being sent</summary>
    public ControlCommand Command;

    /// <summary>Scenario identifier (1-5) when Command is StartScenario</summary>
    public int ScenarioId;

    /// <summary>Timestamp for synchronization (Unix milliseconds)</summary>
    public long Timestamp;
}

/// <summary>
/// Commands for demo orchestration
/// </summary>
public enum ControlCommand : byte
{
    /// <summary>Initial handshake to discover peers</summary>
    Handshake = 0,

    /// <summary>Start executing a specific scenario</summary>
    StartScenario = 1,

    /// <summary>Stop current scenario execution</summary>
    StopScenario = 2,

    /// <summary>Acknowledge receipt of command</summary>
    Ack = 3
}

// ============================================================================
// SCENARIO A: CHAT ROOM
// ============================================================================

/// <summary>
/// Chat message demonstrating basic Pub/Sub with managed API.
/// Features: Sender tracking, reliable delivery, easy-to-use C# strings.
/// </summary>
[DdsTopic("ChatMessage")]
[DdsQos(Reliability = DdsReliability.Reliable)]
public partial struct ChatMessage
{
    /// <summary>Unique message identifier</summary>
    public long MessageId;

    /// <summary>Username of sender</summary>
    public FixedString32 User;

    /// <summary>Message content</summary>
    public FixedString128 Content;

    /// <summary>Timestamp when message was sent (Unix milliseconds)</summary>
    public long Timestamp;
}

// ============================================================================
// SCENARIO B: SENSOR ARRAY (Zero-Allocation)
// ============================================================================

/// <summary>
/// High-frequency sensor data demonstrating zero-allocation reads.
/// Features: AsView() API, high throughput (10,000+ msg/s), GC pressure measurement.
/// Uses BestEffort for maximum performance.
/// </summary>
[DdsTopic("SensorData")]
[DdsQos(Reliability = DdsReliability.BestEffort)]
public partial struct SensorData
{
    /// <summary>Unique sensor identifier (keyed for multi-sensor support)</summary>
    [DdsKey]
    public int SensorId;

    /// <summary>Sensor reading value</summary>
    public double Value;

    /// <summary>Physical location of sensor</summary>
    public FixedString32 Location;

    /// <summary>Timestamp of reading (Unix milliseconds)</summary>
    public long Timestamp;
}

// ============================================================================
// SCENARIO C: STOCK TICKER (Content Filtering)
// ============================================================================

/// <summary>
/// Stock market tick data demonstrating content-based filtering.
/// Features: Client-side predicates, efficient data selection.
/// </summary>
[DdsTopic("StockTick")]
[DdsQos(Reliability = DdsReliability.Reliable)]
public partial struct StockTick
{
    /// <summary>Unique tick identifier</summary>
    public long TickId;

    /// <summary>Stock symbol (e.g., AAPL, MSFT, GOOG)</summary>
    public FixedString32 Symbol;

    /// <summary>Current price</summary>
    public double Price;

    /// <summary>Trade volume</summary>
    public int Volume;

    /// <summary>Timestamp of tick (Unix milliseconds)</summary>
    public long Timestamp;
}

// ============================================================================
// SCENARIO D: FLIGHT RADAR (Keyed Instances)
// ============================================================================

/// <summary>
/// Flight position data demonstrating keyed topics and instance management.
/// Features: O(1) instance lookup, per-instance history, ReadInstance API.
/// Keeps last 10 positions per flight.
/// </summary>
[DdsTopic("FlightPosition")]
[DdsQos(
    Reliability = DdsReliability.Reliable,
    HistoryKind = DdsHistoryKind.KeepLast,
    HistoryDepth = 10)]
public partial struct FlightPosition
{
    /// <summary>Flight identifier (e.g., BA-123, LH-456)</summary>
    [DdsKey]
    public FixedString32 FlightId;

    /// <summary>Latitude in degrees</summary>
    public double Latitude;

    /// <summary>Longitude in degrees</summary>
    public double Longitude;

    /// <summary>Altitude in feet</summary>
    public double Altitude;

    /// <summary>Timestamp of position update (Unix milliseconds)</summary>
    public long Timestamp;
}

// ============================================================================
// SCENARIO E: BLACK BOX (Durability/QoS)
// ============================================================================

/// <summary>
/// System log entry demonstrating durability and late-joining subscribers.
/// Features: TransientLocal durability, KeepAll history, mission-critical data persistence.
/// Late-joining subscribers receive all historical logs immediately upon connection.
/// </summary>
[DdsTopic("SystemLog")]
[DdsQos(
    Reliability = DdsReliability.Reliable,
    Durability = DdsDurability.TransientLocal,
    HistoryKind = DdsHistoryKind.KeepAll)]
public partial struct SystemLog
{
    /// <summary>Unique log entry identifier</summary>
    [DdsKey]
    public long LogId;

    /// <summary>Log severity level</summary>
    public LogLevel Level;

    /// <summary>System component that generated the log</summary>
    public FixedString64 Component;

    /// <summary>Log message</summary>
    public FixedString128 Message;

    /// <summary>Timestamp when log was created (Unix milliseconds)</summary>
    public long Timestamp;
}

/// <summary>
/// Log severity levels
/// </summary>
public enum LogLevel : byte
{
    /// <summary>Informational message</summary>
    Info = 0,

    /// <summary>Warning condition</summary>
    Warning = 1,

    /// <summary>Error condition</summary>
    Error = 2,

    /// <summary>Critical failure requiring immediate attention</summary>
    Critical = 3
}
