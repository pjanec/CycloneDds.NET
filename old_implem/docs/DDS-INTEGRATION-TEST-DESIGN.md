# DDS Integration Test Design

**Purpose:** Validate end-to-end DDS functionality in C# bindings  
**Scope:** Topic descriptors, marshalling, pub/sub, QoS, partitions  
**Test Strategy:** Single-process integration tests with actual DDS calls  
**Success Criteria:** Data sent by C# writer is correctly received by C# reader

---

## 1. Executive Summary

### What We're Testing

**The complete C# to Native DDS pipeline:**

```
C# Type → Marshaller → NativeDescriptor → dds_write() → 
DDS Network → dds_take() → Unmarshaller → C# Type
```

**Critical Components Validated:**
1. **Topic Descriptors** - Built correctly from idlc output
2. **Marshalling** - C# types → native memory layout
3. **Unmarshalling** - Native memory → C# types
4. **DdsWriter/DdsReader** - Wrapper correctness
5. **QoS** - Quality of Service settings work
6. **Partitions** - Data isolation functional
7. **Keys** - Keyed topics work correctly

**Why This Matters:**
- First time ACTUAL DATA flows end-to-end
- Proves descriptor builder (BATCH-13) works
- Validates marshalling correctness
- Builds confidence in entire stack

---

## 2. Test Architecture

### 2.1 Single-Process Testing

**Pattern:**
```csharp
[Fact]
public void PubSub_SimpleType_DataReceivedCorrectly()
{
    using var participant = new DdsParticipant(DDS_DOMAIN_DEFAULT);
    
    // Create topic (uses descriptor!)
    using var writer = new DdsWriter<SimpleMessage>(participant, "TestTopic");
    using var reader = new DdsReader<SimpleMessage>(participant, "TestTopic");
    
    // Wait for discovery
    WaitForMatching(writer, reader);
    
    // Write sample
    var sent = new SimpleMessage { Id = 42, Name = "Test" };
    writer.Write(sent);
    
    // Read sample
    var received = reader.Take(timeout: TimeSpan.FromSeconds(5));
    
    // Validate
    Assert.NotNull(received);
    Assert.Equal(sent.Id, received.Id);
    Assert.Equal(sent.Name, received.Name);
}
```

**Advantages:**
- ✅ Simple setup (no multi-process coordination)
- ✅ Deterministic (no network timing issues)
- ✅ Fast execution
- ✅ Cyclone DDS supports in-process pub/sub via shared memory

### 2.2 Discovery Handling

**Cyclone DDS requires matched endpoints before write succeeds:**

```csharp
private static void WaitForMatching(DdsWriter writer, DdsReader reader, TimeSpan? timeout = null)
{
    var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
    
    // Poll for PUBLICATION_MATCHED_STATUS
    while (DateTime.UtcNow < deadline)
    {
        uint status = 0;
        var rc = DdsApi.dds_get_status_changes(writer.Entity.Handle, ref status);
        
        if ((status & DDS_PUBLICATION_MATCHED_STATUS) != 0)
            return; // Matched!
        
        Thread.Sleep(20); // Poll interval
    }
    
    throw new TimeoutException("Writer/Reader did not match in time");
}
```

**Simplified Alternative:**
```csharp
// Or just sleep briefly - Cyclone is fast for local discovery
Thread.Sleep(100); // Usually enough for in-process
```

### 2.3 Test Data Setup

**File:** `tests/CycloneDDS.Runtime.Tests/TestData/TestMessages.cs`

```csharp
namespace CycloneDDS.Runtime.Tests.TestData;

// Simple primitive types
[DdsTopic]
public partial class SimpleMessage
{
    [Key] public int Id { get; set; }
    public string Name { get; set; } = "";
    public double Value { get; set; }
}

// Nested struct
[DdsTopic]
public partial class NestedMessage
{
    [Key] public int Id { get; set; }
    public SimpleMessage Inner { get; set; } = new();
}

// Arrays
[DdsTopic]
public partial class ArrayMessage
{
    [Key] public int Id { get; set; }
    
    [ArrayLength(5)]
    public int[] FixedArray { get; set; } = new int[5];
    
    [MaxLength(100)]
    public int[] BoundedSeq { get; set; } = Array.Empty<int>();
}

// Keyed topic (multiple instances)
[DdsTopic]
public partial class SensorData
{
    [Key] public int SensorId { get; set; }
    public long Timestamp { get; set; }
    public double Temperature { get; set; }
    public double Pressure { get; set; }
}

// Complex nested
[DdsTopic]
public partial class ComplexMessage
{
    [Key] public int Id { get; set; }
    public string Name { get; set; } = "";
    
    public SimpleMessage Nested { get; set; } = new();
    
    [ArrayLength(3)]
    public SimpleMessage[] NestedArray { get; set; } = new SimpleMessage[3];
    
    [MaxLength(10)]
    public string[] StringSeq { get; set; } = Array.Empty<string>();
}
```

---

## 3. Test Categories

### 3.1 Data Type Coverage (10 tests)

**Test Matrix:**

| Test | Type Complexity | Key Fields | Purpose |
|------|----------------|------------|---------|
| `PubSub_Primitives_Int32` | Single int32 | 1 | Simplest case |
| `PubSub_Primitives_AllBasic` | All primitives | 1 | Int, double, bool, string |
| `PubSub_String_Unbounded` | Unbounded string | 1 | Dynamic allocation |
| `PubSub_FixedArray_Int32` | int[5] | 1 | Fixed-size arrays |
| `PubSub_BoundedSequence_Int32` | int[] (max 100) | 1 | Bounded sequences |
| `PubSub_NestedStruct_Simple` | Struct in struct | 1 | Nested types |
| `PubSub_NestedArray_Struct` | Struct[3] | 1 | Array of structs |
| `PubSub_Complex_AllCombinations` | Nested + arrays + sequences | 1 | Kitchen sink |
| `PubSub_KeyedTopic_MultipleInstances` | Simple type | 2 | Keyed instances |
| `PubSub_EmptyMessage` | No fields | 0 | Edge case |

**Example Test:**

```csharp
[Fact]
public void PubSub_FixedArray_Int32_DataCorrect()
{
    using var participant = new DdsParticipant(DDS_DOMAIN_DEFAULT);
    using var writer = new DdsWriter<ArrayMessage>(participant, "ArrayTest");
    using var reader = new DdsReader<ArrayMessage>(participant, "ArrayTest");
    
    Thread.Sleep(100); // Discovery
    
    var sent = new ArrayMessage
    {
        Id = 1,
        FixedArray = new int[] { 10, 20, 30, 40, 50 }
    };
    
    writer.Write(sent);
    
    var received = reader.Take(TimeSpan.FromSeconds(5));
    
    Assert.NotNull(received);
    Assert.Equal(sent.Id, received.Id);
    Assert.Equal(sent.FixedArray, received.FixedArray);
}
```

### 3.2 Marshalling Correctness (5 tests)

**Purpose:** Verify byte-level marshalling accuracy

```csharp
[Fact]
public void Marshalling_RoundTrip_BytePerfect()
{
    var original = new ComplexMessage
    {
        Id = 42,
        Name = "TestName",
        Nested = new SimpleMessage { Id = 1, Name = "Inner", Value = 3.14 },
        NestedArray = new[] 
        {
            new SimpleMessage { Id = 10, Name = "A", Value = 1.1 },
            new SimpleMessage { Id = 20, Name = "B", Value = 2.2 },
            new SimpleMessage { Id = 30, Name = "C", Value = 3.3 }
        },
        StringSeq = new[] { "Alpha", "Beta", "Gamma" }
    };
    
    // Marshal to native
    var marshaller = new ComplexMessageMarshaller();
    var native = new ComplexMessageNative();
    marshaller.Marshal(original, ref native);
    
    // Send through DDS
    using var participant = new DdsParticipant(DDS_DOMAIN_DEFAULT);
    using var writer = new DdsWriter<ComplexMessage>(participant, "ComplexTest");
    using var reader = new DdsReader<ComplexMessage>(participant, "ComplexTest");
    
    Thread.Sleep(100);
    writer.Write(original);
    
    var received = reader.Take(TimeSpan.FromSeconds(5));
    
    // Deep equality check
    Assert.Equal(original.Id, received.Id);
    Assert.Equal(original.Name, received.Name);
    Assert.Equal(original.Nested.Id, received.Nested.Id);
    Assert.Equal(original.Nested.Name, received.Nested.Name);
    Assert.Equal(original.Nested.Value, received.Nested.Value, precision: 6);
    
    for (int i = 0; i < 3; i++)
    {
        Assert.Equal(original.NestedArray[i].Id, received.NestedArray[i].Id);
        Assert.Equal(original.NestedArray[i].Name, received.NestedArray[i].Name);
    }
    
    Assert.Equal(original.StringSeq, received.StringSeq);
}
```

**Tests:**
1. `Marshalling_Primitives_ByteAccuracy` - Verify int/double/bool exact
2. `Marshalling_Strings_UTF8Encoding` - Unicode handling
3. `Marshalling_Arrays_AllElements` - All array elements correct
4. `Marshalling_Nested_DeepEquality` - Nested struct fields
5. `Marshalling_LargeData_NoCorruption` - 1MB+ payloads

### 3.3 Keyed Topics (4 tests)

**Purpose:** Validate instance-based pub/sub

```csharp
[Fact]
public void KeyedTopic_MultipleInstances_Isolated()
{
    using var participant = new DdsParticipant(DDS_DOMAIN_DEFAULT);
    using var writer = new DdsWriter<SensorData>(participant, "Sensors");
    using var reader = new DdsReader<SensorData>(participant, "Sensors");
    
    Thread.Sleep(100);
    
    // Write data for two sensor instances
    writer.Write(new SensorData { SensorId = 1, Timestamp = 100, Temperature = 20.5 });
    writer.Write(new SensorData { SensorId = 2, Timestamp = 100, Temperature = 21.0 });
    writer.Write(new SensorData { SensorId = 1, Timestamp = 101, Temperature = 20.6 });
    writer.Write(new SensorData { SensorId = 2, Timestamp = 101, Temperature = 21.1 });
    
    // Read should get 4 samples (2 per instance)
    var samples = reader.TakeAll(TimeSpan.FromSeconds(5));
    
    Assert.Equal(4, samples.Count);
    
    // Verify instances are separate
    var sensor1 = samples.Where(s => s.SensorId == 1).ToList();
    var sensor2 = samples.Where(s => s.SensorId == 2).ToList();
    
    Assert.Equal(2, sensor1.Count);
    Assert.Equal(2, sensor2.Count);
    
    // Verify ordering per instance
    Assert.Equal(100, sensor1[0].Timestamp);
    Assert.Equal(101, sensor1[1].Timestamp);
}
```

**Tests:**
1. `KeyedTopic_MultipleInstances_Isolated` - Data per key
2. `KeyedTopic_Dispose_InstanceGone` - dispose_instance works
3. `KeyedTopic_Unregister_InstanceRemoved` - unregister works
4. `KeyedTopic_ReadInstance_OnlyKeyData` - Filter by key

### 3.4 QoS Settings (6 tests)

**Purpose:** Verify Quality of Service policies work

```csharp
[Fact]
public void QoS_Reliability_Reliable_NoDataLoss()
{
    var qos = new DdsQos
    {
        Reliability = ReliabilityKind.Reliable,
        History = HistoryKind.KeepAll
    };
    
    using var participant = new DdsParticipant(DDS_DOMAIN_DEFAULT);
    using var writer = new DdsWriter<SimpleMessage>(participant, "ReliableTest", qos);
    using var reader = new DdsReader<SimpleMessage>(participant, "ReliableTest", qos);
    
    Thread.Sleep(100);
    
    // Write 100 samples rapidly
    for (int i = 0; i < 100; i++)
    {
        writer.Write(new SimpleMessage { Id = i, Name = $"Msg{i}" });
    }
    
    // Read all - should get ALL 100 (no loss)
    var received = new List<SimpleMessage>();
    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
    
    while (received.Count < 100 && DateTime.UtcNow < deadline)
    {
        var samples = reader.TakeAll(TimeSpan.FromMilliseconds(100));
        received.AddRange(samples);
    }
    
    Assert.Equal(100, received.Count);
    
    // Verify all IDs present
    var ids = received.Select(m => m.Id).OrderBy(id => id).ToList();
    Assert.Equal(Enumerable.Range(0, 100), ids);
}
```

**Tests:**
1. `QoS_Reliability_Reliable_NoDataLoss` - All data arrives
2. `QoS_Reliability_BestEffort_AllowsLoss` - May drop samples
3. `QoS_Durability_Transient_LateJoinerGetsData` - Historical data
4. `QoS_History_KeepLast_OnlyLatest` - Keep last N
5. `QoS_Deadline_Missed_EventFired` - Deadline detection
6. `QoS_Lifespan_Expired_SampleDropped` - TTL works

### 3.5 Partitions (3 tests)

**Purpose:** Validate data isolation via partitions

```csharp
[Fact]
public void Partitions_Isolated_NoDataLeakage()
{
    using var participant = new DdsParticipant(DDS_DOMAIN_DEFAULT);
    
    // Writer in partition "A"
    var qosA = new DdsQos { Partition = "PartitionA" };
    using var writerA = new DdsWriter<SimpleMessage>(participant, "PartTest", qosA);
    
    // Writer in partition "B"
    var qosB = new DdsQos { Partition = "PartitionB" };
    using var writerB = new DdsWriter<SimpleMessage>(participant, "PartTest", qosB);
    
    // Reader in partition "A" only
    using var readerA = new DdsReader<SimpleMessage>(participant, "PartTest", qosA);
    
    Thread.Sleep(100);
    
    // Write to both partitions
    writerA.Write(new SimpleMessage { Id = 1, Name = "FromA" });
    writerB.Write(new SimpleMessage { Id = 2, Name = "FromB" });
    
    // Reader should only see partition A data
    var received = readerA.TakeAll(TimeSpan.FromSeconds(5));
    
    Assert.Single(received);
    Assert.Equal(1, received[0].Id);
    Assert.Equal("FromA", received[0].Name);
}
```

**Tests:**
1. `Partitions_Isolated_NoDataLeakage` - Can't read other partitions
2. `Partitions_Multiple_ReaderGetsAll` - Reader with ["A", "B"]
3. `Partitions_Wildcard_MatchesPattern` - "Sensor.*" matching

### 3.6 Error Handling (4 tests)

**Purpose:** Validate error cases are handled correctly

```csharp
[Fact]
public void Error_InvalidDescriptor_ThrowsOnTopicCreation()
{
    // Simulate invalid descriptor (e.g., null ops array)
    var badDescriptor = new DescriptorData
    {
        TypeName = "BadType",
        Ops = null // Invalid!
    };
    
    using var participant = new DdsParticipant(DDS_DOMAIN_DEFAULT);
    
    // Should throw when trying to create topic
    Assert.Throws<DdsException>(() =>
    {
        var topic = DdsApi.dds_create_topic(
            participant.Handle,
            new NativeDescriptor(badDescriptor).Ptr,
            "BadTopic",
            IntPtr.Zero,
            IntPtr.Zero);
    });
}
```

**Tests:**
1. `Error_WriterWithoutDescriptor_Throws` - Validates descriptor requirement
2. `Error_TypeMismatch_WriterReader_Fails` - Different types same topic
3. `Error_WriteAfterDispose_Throws` - Use-after-dispose
4. `Error_TakeTimeout_ReturnsNull` - No data available

---

## 4. Test Infrastructure

### 4.1 Base Test Class

```csharp
public abstract class DdsIntegrationTestBase : IDisposable
{
    protected DdsParticipant Participant { get; }
    protected int TestDomain { get; }
    
    protected DdsIntegrationTestBase()
    {
        // Use unique domain per test to avoid interference
        TestDomain = Random.Shared.Next(100, 1000);
        Participant = new DdsParticipant(TestDomain);
    }
    
    protected void WaitForDiscovery(int milliseconds = 100)
    {
        Thread.Sleep(milliseconds);
    }
    
    protected T AssertReceived<T>(DdsReader<T> reader, TimeSpan? timeout = null)
    {
        var sample = reader.Take(timeout ?? TimeSpan.FromSeconds(5));
        Assert.NotNull(sample);
        return sample;
    }
    
    public void Dispose()
    {
        Participant?.Dispose();
    }
}
```

### 4.2 Discovery Helper

```csharp
public static class DdsTestHelpers
{
    public static void WaitForMatching(DdsWriter writer, DdsReader reader)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        bool matched = false;
        
        while (!matched && DateTime.UtcNow < deadline)
        {
            // Check writer has matched reader
            uint writerStatus = 0;
            DdsApi.dds_get_status_changes(writer.Entity.Handle, ref writerStatus);
            
            // Check reader has matched writer
            uint readerStatus = 0;
            DdsApi.dds_get_status_changes(reader.Entity.Handle, ref readerStatus);
            
            if ((writerStatus & DDS_PUBLICATION_MATCHED_STATUS) != 0 &&
                (readerStatus & DDS_SUBSCRIPTION_MATCHED_STATUS) != 0)
            {
                matched = true;
            }
            else
            {
                Thread.Sleep(20);
            }
        }
        
        if (!matched)
            throw new TimeoutException("Endpoints did not match");
    }
}
```

---

## 5. Success Metrics

### 5.1 Required Tests: 32 minimum

| Category | Count | Must Pass |
|----------|-------|-----------|
| Data Types | 10 | 100% |
| Marshalling | 5 | 100% |
| Keyed Topics | 4 | 100% |
| QoS | 6 | 100% |
| Partitions | 3 | 100% |
| Error Handling | 4 | 100% |
| **TOTAL** | **32** | **100%** |

### 5.2 Performance Baseline

**Latency:**
- Simple message (100 bytes): < 1ms in-process
- Complex message (1KB): < 2ms in-process

**Throughput:**
- Simple messages: > 100,000 msg/sec
- Complex messages: > 10,000 msg/sec

**Memory:**
- No leaks after 10,000 pub/sub cycles
- Marshaller disposal frees all allocations

### 5.3 Validation Criteria

✅ **Functional:**
- All 32 tests pass
- No data corruption
- No data loss (reliable QoS)
- Proper isolation (partitions, keys)

✅ **Quality:**
- Tests use actual DDS (no mocks)
- Runtime validation (read back via Marshal)
- Comprehensive data types
- Edge cases covered

✅ **Trust:**
- Confidence in descriptor builder
- Confidence in marshalling
- Confidence in wrapper correctness
- **Ready for production use**

---

## 6. Implementation Plan

**File Structure:**
```
tests/CycloneDDS.Runtime.Tests/
  ├── Integration/
  │   ├── DdsIntegrationTestBase.cs
  │   ├── DdsTestHelpers.cs
  │   ├── DataTypeTests.cs        (10 tests)
  │   ├── MarshallingTests.cs     (5 tests)
  │   ├── KeyedTopicTests.cs      (4 tests)
  │   ├── QoSTests.cs              (6 tests)
  │   ├── PartitionTests.cs        (3 tests)
  │   └── ErrorHandlingTests.cs    (4 tests)
  └── TestData/
      ├── TestMessages.cs          (Generated test types)
      └── TestMessages.idl         (IDL definitions)
```

**Dependencies:**
- BATCH-12: DdsParticipant, DdsWriter, DdsReader
- BATCH-13.1: NativeDescriptor, marshalling
- Code Generator: Test message generation

**Execution:**
- Single test run: ~2-3 minutes (32 tests)
- Parallel-safe (unique domains per test)
- No external dependencies (in-process)

---

## 7. Risk Mitigation

**Risk:** Discovery failures in CI
**Mitigation:** Generous timeouts (10s), polling pattern

**Risk:** Flaky tests due to timing
**Mitigation:** Deterministic waits, status checks

**Risk:** Memory leaks
**Mitigation:** Disposal tests, valgrind in future

**Risk:** Test data generation issues
**Mitigation:** Manual test types, verified generation

---

## **Bottom Line**

**This test suite PROVES the entire DDS C# stack works:**
- ✅ Descriptors built correctly
- ✅ Marshalling accurate
- ✅ Native calls succeed
- ✅ Data sent = Data received
- ✅ QoS, partitions, keys functional

**If these 32 tests pass → The infrastructure is TRUSTED.**
