# BATCH-21: Keyed Message Serialization - Comprehensive Test Suite

**Batch Number:** BATCH-21  
**Tasks:** Keyed Topic Testing (No new features - validation only)  
**Phase:** Stage 3.75 - Extended DDS API - Keyed Topics Validation  
**Estimated Effort:** 2-3 days  
**Priority:** **CRITICAL** (Validates XCDR2 keyed topic support)  
**Dependencies:** BATCH-19 complete, XCDR2 keyed serialization implemented

---

## 📋 Onboarding & Workflow

### Developer Instructions

Welcome to **BATCH-21**, a **testing-focused batch** to validate keyed topic support!

**Context:**
After extensive XCDR2 implementation work, we now need comprehensive tests to verify that keyed topics work correctly. This batch focuses SOLELY on testing - no new APIs, no instance management, just thorough validation of keyed message serialization/deserialization.

**What You'll Do:**
1. Create diverse test message types with different key configurations
2. Write comprehensive round-trip tests (Write → Read → Verify)
3. Test single keys, composite keys, nested keys
4. Test both extensibility modes (appendable and final)
5. Verify XCDR2 wire format correctness for keys

**What You WON'T Do:**
- ❌ No instance management APIs (LookupInstance, TakeInstance, etc.)
- ❌ No per-instance operations
- ❌ No lifecycle methods (those are tested separately)
- ✅ Focus: Plain send/receive with various keyed message structures

### Required Reading (IN ORDER)

**READ THESE BEFORE STARTING:**

1. **Workflow Guide:** `.dev-workstream\README.md`  
   - Batch system, report requirements, testing standards

2. **DDS Keys Documentation:** `docs\SERDATA-TASK-MASTER.md`  
   - Section: FCDC-S025 (lines 1780-1857) - IDL Generation Control (includes key handling)
   - Look for key field marking, descriptor generation

3. **XCDR2 Specification (Optional):** For wire format details
   - Key fields are serialized first in XCDR2
   - Keys affect instance identification

4. **Previous Batch Review:** `.dev-workstream\reviews\BATCH-19-REVIEW.md`  
   - Context on current test state

### Repository Structure

```
d:\Work\FastCycloneDdsCsharpBindings\
├── tests\
│   └── CycloneDDS.Runtime.Tests\     # Runtime tests
│       ├── KeyedMessages\            # ← NEW FOLDER (keyed test types)
│       │   ├── SingleKeyMessage.cs   # ← NEW (single primitive key)
│       │   ├── CompositeKeyMessage.cs # ← NEW (multiple keys)
│       │   ├── NestedKeyMessage.cs   # ← NEW (key in nested struct)
│       │   ├── FinalExtKeyMessage.cs # ← NEW (final extensibility)
│       │   └── StringKeyMessage.cs   # ← NEW (string key edge case)
│       │
│       └── KeyedTopicTests.cs        # ← NEW FILE (15+ tests)
│
└── .dev-workstream\
    ├── batches\
    │   └── BATCH-21-INSTRUCTIONS.md  # ← This file
    └── reports\
        └── BATCH-21-REPORT.md        # ← Submit your report here
```

### Critical Tool & Library Locations

**DDS Native Library:**
- **Location:** `cyclone-compiled\bin\ddsc.dll`
- **Usage:** Runtime tests link against this

**Code Generator:**
- **Location:** `tools\CycloneDDS.CodeGen\bin\Debug\net8.0\CycloneDDS.CodeGen.dll`
- **Trigger:** Build `CycloneDDS.Runtime.Tests` project after adding keyed message types
- **Output:** `*.Generated.cs` files with serialization + descriptor

**Build Order:**

```powershell
# 1. Schema (attributes)
dotnet build Src\CycloneDDS.Schema\CycloneDDS.Schema.csproj

# 2. Core (CDR)
dotnet build Src\CycloneDDS.Core\CycloneDDS.Core.csproj

# 3. Runtime (DDS API)
dotnet build Src\CycloneDDS.Runtime\CycloneDDS.Runtime.csproj

# 4. Tests (triggers code generation for keyed messages)
dotnet build tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj

# 5. Run tests
dotnet test tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj
```

### Report Submission

**When done, submit your report to:**  
`.dev-workstream\reports\BATCH-21-REPORT.md`

**If you have questions, create:**  
`.dev-workstream\questions\BATCH-21-QUESTIONS.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Progression

**CRITICAL: Complete keyed message types in sequence:**

1. **Phase 1 (Single Key):** Create SingleKeyMessage → Build → Write 3 tests → **Tests pass** ✅
2. **Phase 2 (Composite Key):** Create CompositeKeyMessage → Build → Write 3 tests → **Tests pass** ✅
3. **Phase 3 (Nested Key):** Create NestedKeyMessage → Build → Write 3 tests → **Tests pass** ✅
4. **Phase 4 (Final Ext):** Create FinalExtKeyMessage → Build → Write 2 tests → **Tests pass** ✅
5. **Phase 5 (String Key):** Create StringKeyMessage → Build → Write 3 tests → **Tests pass** ✅
6. **Phase 6 (Edge Cases):** Write 3+ edge case tests → **Tests pass** ✅

**After EACH phase:**
```powershell
# Verify ALL tests pass (not just new ones)
dotnet test tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj --no-build

# Expected progression:
# After Phase 1: 60 tests passing (57 + 3 single key)
# After Phase 2: 63 tests passing (60 + 3 composite key)
# After Phase 3: 66 tests passing (63 + 3 nested key)
# After Phase 4: 68 tests passing (66 + 2 final ext)
# After Phase 5: 71 tests passing (68 + 3 string key)
# After Phase 6: 74+ tests passing (71 + 3+ edge cases)
```

**DO NOT** move to the next phase until:
- ✅ Current keyed message type created
- ✅ Code generation successful (Generated.cs exists)
- ✅ Current phase tests written
- ✅ **ALL tests passing** (including previous BATCH-19 tests: 57)

---

## Context

**Background:**
After implementing XCDR2 support for keyed topics (with significant effort), we need comprehensive tests to ensure the implementation is correct. Keys are critical in DDS - they identify instances and affect wire format serialization.

**Why This Matters:**
- **Keyed topics** are the foundation for multi-instance DDS systems
- Incorrect key serialization breaks instance identification
- Different key types (primitive, composite, nested) have different XCDR2 layouts
- Extensibility modes interact with keys in complex ways

**Testing Strategy:**
This batch uses a "type diversity matrix" approach:
1. **Key Count:** Single key vs. multiple keys (composite)
2. **Key Location:** Top-level vs. nested in sub-structure
3. **Key Type:** Primitive (int) vs. managed (string)
4. **Extensibility:** Appendable (default) vs. Final
5. **Edge Cases:** Empty keys, max key count, key ordering

---

## 🎯 Batch Objectives

**Goal:** Validate that keyed topic serialization/deserialization works correctly across all key configurations

**Success Metrics:**
- ✅ 5 diverse keyed message types created
- ✅ 15+ comprehensive round-trip tests
- ✅ All tests pass (no skipped tests)
- ✅ Both extensibility modes tested
- ✅ Edge cases covered

**Why Testing Focus:**
- No new APIs means no implementation risk
- Focus on validation ensures XCDR2 keyed support is solid
- Comprehensive tests catch corner cases early
- Foundation for future instance management work

---

## ✅ Tasks

### Task 1: Single Primitive Key Tests

**Priority:** CRITICAL (Foundation)  
**Estimated Effort:** 0.5 day  
**Focus:** Most common use case - single int/long key

#### Keyed Message Type

**File:** `tests\CycloneDDS.Runtime.Tests\KeyedMessages\SingleKeyMessage.cs` (NEW)

```csharp
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    /// <summary>
    /// Single primitive key - most common DDS keyed topic pattern.
    /// Example: Vehicle tracking by VehicleId.
    /// </summary>
    [DdsTopic("SingleKeyTopic")]
    public partial struct SingleKeyMessage
    {
        [DdsKey, DdsId(0)]
        public int DeviceId;   // KEY FIELD
        
        [DdsId(1)]
        public int Value;      // Data field
        
        [DdsId(2)]
        public long Timestamp; // Data field
    }
}
```

**Verification After Creation:**
```powershell
dotnet build tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj
# Check for: tests\CycloneDDS.Runtime.Tests\obj\Debug\net8.0\generated\SingleKeyMessage.Generated.cs
```

#### Tests Required (Minimum 3)

**File:** `tests\CycloneDDS.Runtime.Tests\KeyedTopicTests.cs` (NEW)

**Test 1: SingleKey_RoundTrip_Basic**
```csharp
[Fact]
public void SingleKey_RoundTrip_Basic()
{
    // Arrange
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"SingleKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<SingleKeyMessage>(participant, topicName);
    using var reader = new DdsReader<SingleKeyMessage, SingleKeyMessage>(participant, topicName);
    
    var sample = new SingleKeyMessage
    {
        DeviceId = 42,
        Value = 100,
        Timestamp = 123456789L
    };
    
    // Act
    writer.Write(sample);
    Thread.Sleep(100); // Wait for propagation
    
    // Assert
    using var scope = reader.Take();
    Assert.Single(scope.Samples);
    
    var received = scope.Samples[0];
    Assert.Equal(42, received.DeviceId);
    Assert.Equal(100, received.Value);
    Assert.Equal(123456789L, received.Timestamp);
}
```

**Test 2: SingleKey_MultipleInstances_IndependentDelivery**
```csharp
[Fact]
public void SingleKey_MultipleInstances_IndependentDelivery()
{
    // Arrange
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"SingleKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<SingleKeyMessage>(participant, topicName);
    using var reader = new DdsReader<SingleKeyMessage, SingleKeyMessage>(participant, topicName);
    
    // Act - Write 3 different instances (different DeviceIds)
    writer.Write(new SingleKeyMessage { DeviceId = 1, Value = 100 });
    writer.Write(new SingleKeyMessage { DeviceId = 2, Value = 200 });
    writer.Write(new SingleKeyMessage { DeviceId = 3, Value = 300 });
    Thread.Sleep(100);
    
    // Assert - All 3 instances received
    using var scope = reader.Take();
    Assert.Equal(3, scope.Samples.Length);
    
    // Verify distinct instances (distinct DeviceIds)
    var deviceIds = scope.Samples.Select(s => s.DeviceId).OrderBy(x => x).ToArray();
    Assert.Equal(new[] { 1, 2, 3 }, deviceIds);
    
    // Verify values match keys
    Assert.Equal(100, scope.Samples.First(s => s.DeviceId == 1).Value);
    Assert.Equal(200, scope.Samples.First(s => s.DeviceId == 2).Value);
    Assert.Equal(300, scope.Samples.First(s => s.DeviceId == 3).Value);
}
```

**Test 3: SingleKey_SameInstance_UpdatesData**
```csharp
[Fact]
public void SingleKey_SameInstance_UpdatesData()
{
    // Arrange
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"SingleKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<SingleKeyMessage>(participant, topicName);
    using var reader = new DdsReader<SingleKeyMessage, SingleKeyMessage>(participant, topicName);
    
    // Act - Write same instance (DeviceId=5) twice with different values
    writer.Write(new SingleKeyMessage { DeviceId = 5, Value = 100, Timestamp = 1000 });
    writer.Write(new SingleKeyMessage { DeviceId = 5, Value = 200, Timestamp = 2000 });
    Thread.Sleep(100);
    
    // Assert - Both samples received (updates are separate samples in DDS)
    using var scope = reader.Take();
    Assert.Equal(2, scope.Samples.Length);
    Assert.All(scope.Samples, s => Assert.Equal(5, s.DeviceId));
    
    // Verify both updates present
    var values = scope.Samples.Select(s => s.Value).OrderBy(x => x).ToArray();
    Assert.Equal(new[] { 100, 200 }, values);
}
```

---

### Task 2: Composite Key Tests

**Priority:** HIGH  
**Estimated Effort:** 0.5 day  
**Focus:** Multiple key fields (e.g., SensorId + LocationId)

#### Keyed Message Type

**File:** `tests\CycloneDDS.Runtime.Tests\KeyedMessages\CompositeKeyMessage.cs` (NEW)

```csharp
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    /// <summary>
    /// Composite key (multiple key fields).
    /// Example: Sensor in a specific location - uniquely identified by both SensorId and LocationId.
    /// </summary>
    [DdsTopic("CompositeKeyTopic")]
    public partial struct CompositeKeyMessage
    {
        [DdsKey, DdsId(0)]
        public int SensorId;    // KEY FIELD 1
        
        [DdsKey, DdsId(1)]
        public int LocationId;  // KEY FIELD 2
        
        [DdsId(2)]
        public double Temperature; // Data field
    }
}
```

#### Tests Required (Minimum 3)

**Test 1: CompositeKey_RoundTrip_Basic**
```csharp
[Fact]
public void CompositeKey_RoundTrip_Basic()
{
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"CompositeKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<CompositeKeyMessage>(participant, topicName);
    using var reader = new DdsReader<CompositeKeyMessage, CompositeKeyMessage>(participant, topicName);
    
    var sample = new CompositeKeyMessage
    {
        SensorId = 10,
        LocationId = 20,
        Temperature = 25.5
    };
    
    writer.Write(sample);
    Thread.Sleep(100);
    
    using var scope = reader.Take();
    Assert.Single(scope.Samples);
    
    var received = scope.Samples[0];
    Assert.Equal(10, received.SensorId);
    Assert.Equal(20, received.LocationId);
    Assert.Equal(25.5, received.Temperature, precision: 2);
}
```

**Test 2: CompositeKey_DistinctInstances_BothKeysMustMatch**
```csharp
[Fact]
public void CompositeKey_DistinctInstances_BothKeysMustMatch()
{
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"CompositeKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<CompositeKeyMessage>(participant, topicName);
    using var reader = new DdsReader<CompositeKeyMessage, CompositeKeyMessage>(participant, topicName);
    
    // Write 4 samples - 4 distinct instances because composite key (SensorId, LocationId)
    writer.Write(new CompositeKeyMessage { SensorId = 1, LocationId = 1, Temperature = 10.0 });
    writer.Write(new CompositeKeyMessage { SensorId = 1, LocationId = 2, Temperature = 20.0 }); // Different location
    writer.Write(new CompositeKeyMessage { SensorId = 2, LocationId = 1, Temperature = 30.0 }); // Different sensor
    writer.Write(new CompositeKeyMessage { SensorId = 2, LocationId = 2, Temperature = 40.0 }); // Both different
    Thread.Sleep(100);
    
    using var scope = reader.Take();
    Assert.Equal(4, scope.Samples.Length); // 4 distinct instances
    
    // Verify all 4 combinations present
    Assert.Contains(scope.Samples, s => s.SensorId == 1 && s.LocationId == 1 && s.Temperature == 10.0);
    Assert.Contains(scope.Samples, s => s.SensorId == 1 && s.LocationId == 2 && s.Temperature == 20.0);
    Assert.Contains(scope.Samples, s => s.SensorId == 2 && s.LocationId == 1 && s.Temperature == 30.0);
    Assert.Contains(scope.Samples, s => s.SensorId == 2 && s.LocationId == 2 && s.Temperature == 40.0);
}
```

**Test 3: CompositeKey_SameInstance_RequiresBothKeysEqual**
```csharp
[Fact]
public void CompositeKey_SameInstance_RequiresBothKeysEqual()
{
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"CompositeKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<CompositeKeyMessage>(participant, topicName);
    using var reader = new DdsReader<CompositeKeyMessage, CompositeKeyMessage>(participant, topicName);
    
    // Write same instance (1, 1) twice with different data
    writer.Write(new CompositeKeyMessage { SensorId = 1, LocationId = 1, Temperature = 10.0 });
    writer.Write(new CompositeKeyMessage { SensorId = 1, LocationId = 1, Temperature = 15.0 }); // Update
    Thread.Sleep(100);
    
    using var scope = reader.Take();
    Assert.Equal(2, scope.Samples.Length); // Both updates received
    Assert.All(scope.Samples, s => Assert.Equal(1, s.SensorId));
    Assert.All(scope.Samples, s => Assert.Equal(1, s.LocationId));
    
    var temps = scope.Samples.Select(s => s.Temperature).OrderBy(x => x).ToArray();
    Assert.Equal(new[] { 10.0, 15.0 }, temps);
}
```

---

### Task 3: Nested Key Tests

**Priority:** HIGH  
**Estimated Effort:** 0.5 day  
**Focus:** Key field inside nested struct

#### Keyed Message Type

**File:** `tests\CycloneDDS.Runtime.Tests\KeyedMessages\NestedKeyMessage.cs` (NEW)

```csharp
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    [DdsStruct]
    public partial struct DeviceIdentifier
    {
        [DdsKey, DdsId(0)]
        public int DeviceId;   // KEY FIELD (inside nested struct)
        
        [DdsId(1)]
        public int Version;    // Non-key field
    }
    
    /// <summary>
    /// Key field inside nested struct.
    /// Tests that code generator correctly propagates [DdsKey] from nested structures.
    /// </summary>
    [DdsTopic("NestedKeyTopic")]
    public partial struct NestedKeyMessage
    {
        [DdsId(0)]
        public DeviceIdentifier Identifier; // Contains key field
        
        [DdsId(1)]
        public string Status;  // Data field
    }
}
```

**CRITICAL:** The `[DdsKey]` attribute on `DeviceIdentifier.DeviceId` should propagate to make the nested field part of the topic key.

#### Tests Required (Minimum 3)

**Test 1: NestedKey_RoundTrip_Basic**
```csharp
[Fact]
public void NestedKey_RoundTrip_Basic()
{
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"NestedKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<NestedKeyMessage>(participant, topicName);
    using var reader = new DdsReader<NestedKeyMessage, NestedKeyMessage>(participant, topicName);
    
    var sample = new NestedKeyMessage
    {
        Identifier = new DeviceIdentifier { DeviceId = 100, Version = 1 },
        Status = "active"
    };
    
    writer.Write(sample);
    Thread.Sleep(100);
    
    using var scope = reader.Take();
    Assert.Single(scope.Samples);
    
    var received = scope.Samples[0];
    Assert.Equal(100, received.Identifier.DeviceId);
    Assert.Equal(1, received.Identifier.Version);
    Assert.Equal("active", received.Status);
}
```

**Test 2: NestedKey_MultipleInstances_DistinctByNestedKey**
```csharp
[Fact]
public void NestedKey_MultipleInstances_DistinctByNestedKey()
{
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"NestedKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<NestedKeyMessage>(participant, topicName);
    using var reader = new DdsReader<NestedKeyMessage, NestedKeyMessage>(participant, topicName);
    
    // Write 3 instances with different DeviceIds (key field in nested struct)
    writer.Write(new NestedKeyMessage 
    { 
        Identifier = new DeviceIdentifier { DeviceId = 1, Version = 1 }, 
        Status = "online" 
    });
    writer.Write(new NestedKeyMessage 
    { 
        Identifier = new DeviceIdentifier { DeviceId = 2, Version = 1 }, 
        Status = "offline" 
    });
    writer.Write(new NestedKeyMessage 
    { 
        Identifier = new DeviceIdentifier { DeviceId = 3, Version = 1 }, 
        Status = "maintenance" 
    });
    Thread.Sleep(100);
    
    using var scope = reader.Take();
    Assert.Equal(3, scope.Samples.Length);
    
    var deviceIds = scope.Samples.Select(s => s.Identifier.DeviceId).OrderBy(x => x).ToArray();
    Assert.Equal(new[] { 1, 2, 3 }, deviceIds);
}
```

**Test 3: NestedKey_NonKeyFieldChange_SameInstance**
```csharp
[Fact]
public void NestedKey_NonKeyFieldChange_SameInstance()
{
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"NestedKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<NestedKeyMessage>(participant, topicName);
    using var reader = new DdsReader<NestedKeyMessage, NestedKeyMessage>(participant, topicName);
    
    // Write same key (DeviceId=10) but different Version (non-key field)
    writer.Write(new NestedKeyMessage 
    { 
        Identifier = new DeviceIdentifier { DeviceId = 10, Version = 1 }, 
        Status = "v1" 
    });
    writer.Write(new NestedKeyMessage 
    { 
        Identifier = new DeviceIdentifier { DeviceId = 10, Version = 2 }, // Version changed
        Status = "v2" 
    });
    Thread.Sleep(100);
    
    using var scope = reader.Take();
    Assert.Equal(2, scope.Samples.Length); // Same instance, 2 updates
    Assert.All(scope.Samples, s => Assert.Equal(10, s.Identifier.DeviceId));
    
    // Verify both versions present (non-key field variation)
    var versions = scope.Samples.Select(s => s.Identifier.Version).OrderBy(x => x).ToArray();
    Assert.Equal(new[] { 1, 2 }, versions);
}
```

---

### Task 4: Final Extensibility with Keys

**Priority:** MEDIUM  
**Estimated Effort:** 0.3 day  
**Focus:** Test final extensibility mode with keyed topics

#### Keyed Message Type

**File:** `tests\CycloneDDS.Runtime.Tests\KeyedMessages\FinalExtKeyMessage.cs` (NEW)

```csharp
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    /// <summary>
    /// Final extensibility with key.
    /// Tests interaction between extensibility modes and keyed topics.
    /// </summary>
    [DdsTopic("FinalExtKeyTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct FinalExtKeyMessage
    {
        [DdsKey, DdsId(0)]
        public long EntityId;  // KEY FIELD
        
        [DdsId(1)]
        public int Counter;    // Data field
    }
}
```

#### Tests Required (Minimum 2)

**Test 1: FinalExtKey_RoundTrip_Basic**
```csharp
[Fact]
public void FinalExtKey_RoundTrip_Basic()
{
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"FinalExtKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<FinalExtKeyMessage>(participant, topicName);
    using var reader = new DdsReader<FinalExtKeyMessage, FinalExtKeyMessage>(participant, topicName);
    
    var sample = new FinalExtKeyMessage { EntityId = 999L, Counter = 42 };
    
    writer.Write(sample);
    Thread.Sleep(100);
    
    using var scope = reader.Take();
    Assert.Single(scope.Samples);
    Assert.Equal(999L, scope.Samples[0].EntityId);
    Assert.Equal(42, scope.Samples[0].Counter);
}
```

**Test 2: FinalExtKey_MultipleInstances_DistinctKeys**
```csharp
[Fact]
public void FinalExtKey_MultipleInstances_DistinctKeys()
{
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"FinalExtKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<FinalExtKeyMessage>(participant, topicName);
    using var reader = new DdsReader<FinalExtKeyMessage, FinalExtKeyMessage>(participant, topicName);
    
    writer.Write(new FinalExtKeyMessage { EntityId = 1L, Counter = 10 });
    writer.Write(new FinalExtKeyMessage { EntityId = 2L, Counter = 20 });
    writer.Write(new FinalExtKeyMessage { EntityId = 3L, Counter = 30 });
    Thread.Sleep(100);
    
    using var scope = reader.Take();
    Assert.Equal(3, scope.Samples.Length);
    
    var entityIds = scope.Samples.Select(s => s.EntityId).OrderBy(x => x).ToArray();
    Assert.Equal(new[] { 1L, 2L, 3L }, entityIds);
}
```

---

### Task 5: String Key Tests (Managed Key)

**Priority:** HIGH (Edge Case)  
**Estimated Effort:** 0.5 day  
**Focus:** Managed type (string) as key field

#### Keyed Message Type

**File:** `tests\CycloneDDS.Runtime.Tests\KeyedMessages\StringKeyMessage.cs` (NEW)

```csharp
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    /// <summary>
    /// String key (managed type).
    /// Tests that managed types (string) work correctly as key fields.
    /// Example: User tracking by UserId (string).
    /// </summary>
    [DdsTopic("StringKeyTopic")]
    [DdsManaged] // Required for string fields
    public partial struct StringKeyMessage
    {
        [DdsKey, DdsId(0)]
        public string UserId;  // KEY FIELD (managed type)
        
        [DdsId(1)]
        public int Score;      // Data field
    }
}
```

**CRITICAL:** String keys are complex because:
1. Variable-length serialization
2. Managed memory allocation
3. Null handling
4. UTF-8 encoding in XCDR2

#### Tests Required (Minimum 3)

**Test 1: StringKey_RoundTrip_Basic**
```csharp
[Fact]
public void StringKey_RoundTrip_Basic()
{
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"StringKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<StringKeyMessage>(participant, topicName);
    using var reader = new DdsReader<StringKeyMessage, StringKeyMessage>(participant, topicName);
    
    var sample = new StringKeyMessage { UserId = "user123", Score = 100 };
    
    writer.Write(sample);
    Thread.Sleep(100);
    
    using var scope = reader.Take();
    Assert.Single(scope.Samples);
    Assert.Equal("user123", scope.Samples[0].UserId);
    Assert.Equal(100, scope.Samples[0].Score);
}
```

**Test 2: StringKey_MultipleInstances_DistinctStrings**
```csharp
[Fact]
public void StringKey_MultipleInstances_DistinctStrings()
{
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"StringKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<StringKeyMessage>(participant, topicName);
    using var reader = new DdsReader<StringKeyMessage, StringKeyMessage>(participant, topicName);
    
    writer.Write(new StringKeyMessage { UserId = "alice", Score = 10 });
    writer.Write(new StringKeyMessage { UserId = "bob", Score = 20 });
    writer.Write(new StringKeyMessage { UserId = "charlie", Score = 30 });
    Thread.Sleep(100);
    
    using var scope = reader.Take();
    Assert.Equal(3, scope.Samples.Length);
    
    var userIds = scope.Samples.Select(s => s.UserId).OrderBy(x => x).ToArray();
    Assert.Equal(new[] { "alice", "bob", "charlie" }, userIds);
}
```

**Test 3: StringKey_EmptyString_ValidInstance**
```csharp
[Fact]
public void StringKey_EmptyString_ValidInstance()
{
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"StringKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<StringKeyMessage>(participant, topicName);
    using var reader = new DdsReader<StringKeyMessage, StringKeyMessage>(participant, topicName);
    
    // Empty string is a valid key (different from null)
    var sample = new StringKeyMessage { UserId = "", Score = 999 };
    
    writer.Write(sample);
    Thread.Sleep(100);
    
    using var scope = reader.Take();
    Assert.Single(scope.Samples);
    Assert.Equal("", scope.Samples[0].UserId);
    Assert.Equal(999, scope.Samples[0].Score);
}
```

---

### Task 6: Edge Case Tests

**Priority:** MEDIUM  
**Estimated Effort:** 0.3 day  
**Focus:** Boundary conditions and corner cases

#### Tests Required (Minimum 3)

**File:** `tests\CycloneDDS.Runtime.Tests\KeyedTopicTests.cs` (add to existing file)

**Test 1: KeyedTopic_ZeroKey_Valid**
```csharp
[Fact]
public void KeyedTopic_ZeroKey_Valid()
{
    // Key value of 0 is valid (not special)
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"SingleKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<SingleKeyMessage>(participant, topicName);
    using var reader = new DdsReader<SingleKeyMessage, SingleKeyMessage>(participant, topicName);
    
    writer.Write(new SingleKeyMessage { DeviceId = 0, Value = 123 });
    Thread.Sleep(100);
    
    using var scope = reader.Take();
    Assert.Single(scope.Samples);
    Assert.Equal(0, scope.Samples[0].DeviceId);
    Assert.Equal(123, scope.Samples[0].Value);
}
```

**Test 2: KeyedTopic_NegativeKey_Valid**
```csharp
[Fact]
public void KeyedTopic_NegativeKey_Valid()
{
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"SingleKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<SingleKeyMessage>(participant, topicName);
    using var reader = new DdsReader<SingleKeyMessage, SingleKeyMessage>(participant, topicName);
    
    writer.Write(new SingleKeyMessage { DeviceId = -100, Value = 456 });
    Thread.Sleep(100);
    
    using var scope = reader.Take();
    Assert.Single(scope.Samples);
    Assert.Equal(-100, scope.Samples[0].DeviceId);
}
```

**Test 3: KeyedTopic_MaxInt32_Valid**
```csharp
[Fact]
public void KeyedTopic_MaxInt32_Valid()
{
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"SingleKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<SingleKeyMessage>(participant, topicName);
    using var reader = new DdsReader<SingleKeyMessage, SingleKeyMessage>(participant, topicName);
    
    writer.Write(new SingleKeyMessage { DeviceId = int.MaxValue, Value = 789 });
    Thread.Sleep(100);
    
    using var scope = reader.Take();
    Assert.Single(scope.Samples);
    Assert.Equal(int.MaxValue, scope.Samples[0].DeviceId);
}
```

**Test 4: CompositeKey_KeyOrdering_Deterministic (OPTIONAL)**
```csharp
[Fact]
public void CompositeKey_KeyOrdering_Deterministic()
{
    // Verify that (SensorId=1, LocationId=2) and (SensorId=2, LocationId=1) are distinct instances
    using var participant = new DdsParticipant(domain: 0);
    string topicName = $"CompositeKeyTopic_{Guid.NewGuid()}";
    
    using var writer = new DdsWriter<CompositeKeyMessage>(participant, topicName);
    using var reader = new DdsReader<CompositeKeyMessage, CompositeKeyMessage>(participant, topicName);
    
    writer.Write(new CompositeKeyMessage { SensorId = 1, LocationId = 2, Temperature = 10.0 });
    writer.Write(new CompositeKeyMessage { SensorId = 2, LocationId = 1, Temperature = 20.0 });
    Thread.Sleep(100);
    
    using var scope = reader.Take();
    Assert.Equal(2, scope.Samples.Length); // 2 distinct instances
    
    Assert.Contains(scope.Samples, s => s.SensorId == 1 && s.LocationId == 2);
    Assert.Contains(scope.Samples, s => s.SensorId == 2 && s.LocationId == 1);
}
```

---

## 🧪 Testing Requirements

### Test Counts

**Minimum Tests:** 15  
**Target Tests:** 17+ (15 mandatory + 2+ optional edge cases)

**Breakdown:**
- Task 1 (Single Key): 3 tests
- Task 2 (Composite Key): 3 tests
- Task 3 (Nested Key): 3 tests
- Task 4 (Final Ext): 2 tests
- Task 5 (String Key): 3 tests
- Task 6 (Edge Cases): 3+ tests

### Test Categories

1. **Round-trip Basic (5 tests):** One per keyed message type
2. **Multi-instance (5 tests):** Verify distinct keys create distinct instances
3. **Same Instance Updates (3 tests):** Verify same key updates existing instance
4. **Edge Cases (3+ tests):** Zero, negative, max values, empty strings

### Test Quality Standards

**⚠️ CRITICAL: ALL TESTS MUST VERIFY ACTUAL SERIALIZATION/DESERIALIZATION**

❌ **NOT ACCEPTABLE:**
```csharp
[Fact]
public void SingleKey_Works()
{
    var sample = new SingleKeyMessage { DeviceId = 1 };
    Assert.NotNull(sample); // Tests nothing about DDS
}
```

✅ **REQUIRED:**
```csharp
[Fact]
public void SingleKey_RoundTrip_Basic()
{
    // MUST: Create participant, writer, reader
    using var participant = new DdsParticipant(domain: 0);
    using var writer = new DdsWriter<SingleKeyMessage>(...);
    using var reader = new DdsReader<SingleKeyMessage, SingleKeyMessage>(...);
    
    // MUST: Write data
    writer.Write(sample);
    Thread.Sleep(100);
    
    // MUST: Read data back
    using var scope = reader.Take();
    
    // MUST: Verify ALL fields match
    Assert.Equal(expectedKey, scope.Samples[0].KeyField);
    Assert.Equal(expectedValue, scope.Samples[0].DataField);
}
```

### Verification Commands

After completing EACH task:
```powershell
# Build
dotnet build tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj

# Run ALL tests
dotnet test tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj --no-build

# Expected progression (see workflow section)
```

---

## 📊 Report Requirements

### Report File

Submit to: `.dev-workstream\reports\BATCH-21-REPORT.md`

Use template: `.dev-workstream\templates\BATCH-REPORT-TEMPLATE.md`

### Mandatory Sections

**1. Completion Checklist**
- [ ] Phase 1: SingleKeyMessage + 3 tests
- [ ] Phase 2: CompositeKeyMessage + 3 tests
- [ ] Phase 3: NestedKeyMessage + 3 tests
- [ ] Phase 4: FinalExtKeyMessage + 2 tests
- [ ] Phase 5: StringKeyMessage + 3 tests
- [ ] Phase 6: Edge case tests (3+)
- [ ] All tests passing (74+ total)
- [ ] No compiler warnings

**2. Test Results**
```
Total tests: XX
Passed: XX
Failed: 0
Skipped: 0

Test breakdown:
- BATCH-19 (existing): 57 passing
- Single key tests: 3
- Composite key tests: 3
- Nested key tests: 3
- Final ext key tests: 2
- String key tests: 3
- Edge case tests: X
```

**3. Implementation Notes**

Document for EACH keyed message type:
- Code generation success (Generated.cs created?)
- Any issues with [DdsKey] attribute handling
- Descriptor inspection (key metadata present?)
- XCDR2 serialization observations

**4. Developer Insights (CRITICAL)**

Answer these questions:

**Q1: Code Generation**
Did the code generator handle all keyed message types correctly? Any issues with nested keys or composite keys? How did you verify key metadata in descriptors?

**Q2: Serialization Observations**
What did you learn about XCDR2 keyed serialization? Are keys serialized first? Any differences between single vs composite keys?

**Q3: String Keys**
How does the system handle string keys (managed type)? Any performance concerns? Null handling? Empty string behavior?

**Q4: Nested Keys**
Did `[DdsKey]` correctly propagate from nested struct fields? How is this represented in the descriptor? Any edge cases discovered?

**Q5: Extensibility Interaction**
How do extensibility modes (final vs appendable) interact with keyed topics? Any differences in wire format or behavior?

**Q6: Test Coverage**
Are there any key configurations NOT tested by this batch? What additional tests would improve coverage?

**Q7: Issues Found**
Did you find any bugs or unexpected behavior during testing? If so, document them clearly.

---

## 🎯 Success Criteria

This batch is DONE when:

- ✅ **All Keyed Message Types Created:**
  - SingleKeyMessage (single primitive)
  - CompositeKeyMessage (multiple keys)
  - NestedKeyMessage (key in nested struct)
  - FinalExtKeyMessage (final extensibility)
  - StringKeyMessage (managed key)

- ✅ **Code Generation Successful:**
  - All 5 Generated.cs files exist
  - Descriptors include key metadata
  - No generation errors

- ✅ **15+ Tests Written:**
  - All test categories covered
  - All tests verify actual round-trip serialization
  - No shallow tests

- ✅ **All Tests Passing:**
  - 74+ tests total (57 existing + 17+ new)
  - Zero failures
  - Zero skipped tests

- ✅ **Quality Standards:**
  - No compiler warnings
  - Comprehensive test coverage
  - Clear test names and assertions
  - Proper resource disposal (using statements)

- ✅ **Documentation:**
  - Report submitted with all mandatory sections
  - Developer insights capture observations
  - Any issues/bugs clearly documented

---

## ⚠️ Common Pitfalls to Avoid

### Code Generation Issues

❌ **Don't:** Forget to build after creating keyed message types
```powershell
# WRONG - tests won't find Generated.cs
dotnet test tests\CycloneDDS.Runtime.Tests\...
```

✅ **Do:** Always build before testing
```powershell
dotnet build tests\CycloneDDS.Runtime.Tests\...
dotnet test tests\CycloneDDS.Runtime.Tests\... --no-build
```

### Test Quality

❌ **Don't:** Test only successful cases
```csharp
// WRONG - what about edge cases?
[Fact]
public void SingleKey_Works()
{
    writer.Write(new SingleKeyMessage { DeviceId = 1, Value = 100 });
    // ... verify ...
}
```

✅ **Do:** Test edge cases (zero, negative, max, empty)
```csharp
[Fact]
public void KeyedTopic_ZeroKey_Valid() { /* ... */ }

[Fact]
public void KeyedTopic_NegativeKey_Valid() { /* ... */ }

[Fact]
public void StringKey_EmptyString_ValidInstance() { /* ... */ }
```

### Assertion Completeness

❌ **Don't:** Verify only key fields
```csharp
// WRONG - doesn't verify data fields
Assert.Equal(expectedKey, received.DeviceId);
```

✅ **Do:** Verify ALL fields
```csharp
Assert.Equal(expectedKey, received.DeviceId);
Assert.Equal(expectedValue, received.Value);
Assert.Equal(expectedTimestamp, received.Timestamp);
```

### Instance Understanding

❌ **Don't:** Confuse instance identity with sample updates
```csharp
// WRONG - assumes only 1 sample returned
writer.Write(new SingleKeyMessage { DeviceId = 1, Value = 100 });
writer.Write(new SingleKeyMessage { DeviceId = 1, Value = 200 }); // Same instance
using var scope = reader.Take();
Assert.Single(scope.Samples); // WRONG - DDS keeps both updates
```

✅ **Do:** Understand DDS delivers all samples (including updates)
```csharp
writer.Write(new SingleKeyMessage { DeviceId = 1, Value = 100 });
writer.Write(new SingleKeyMessage { DeviceId = 1, Value = 200 });
using var scope = reader.Take();
Assert.Equal(2, scope.Samples.Length); // 2 samples, same instance
```

### String Key Handling

❌ **Don't:** Assume null strings are valid keys
```csharp
// POTENTIALLY WRONG - null string behavior undefined
var sample = new StringKeyMessage { UserId = null, Score = 100 };
```

✅ **Do:** Test empty string, not null (or document null behavior)
```csharp
// Test empty string (valid)
var sample = new StringKeyMessage { UserId = "", Score = 100 };

// If testing null, document expected behavior in test name
[Fact]
public void StringKey_Null_ThrowsOrDefaultsBehavior() { /* ... */ }
```

---

## 📚 Reference Materials

### Task Definitions
- **SERDATA-TASK-MASTER.md:**
  - FCDC-S025 (lines 1780-1857) - IDL Generation Control (key handling)

### Design Documents
- **DDS Keyed Topics Specification:**
  - Keys identify instances
  - Instance state (ALIVE, DISPOSED, NO_WRITERS)
  - Multiple samples per instance

### Code Examples
- **Previous Batches:**
  - BATCH-19: `.dev-workstream\batches\BATCH-19-INSTRUCTIONS.md` (test patterns)
  - BATCH-18: Integration test examples

### External References
- **XCDR2 Specification:** Key fields serialized with special markers
- **DDS Keyed Topics:** https://cyclonedds.io/docs/cyclonedds/latest/api/keyed_topics.html

---

**Good luck! Focus on comprehensive test coverage, proper verification of all fields, and edge case testing.** 🚀

**Remember:** This is a TESTING batch - no implementation, just validation. If you find bugs, document them clearly in your report!
