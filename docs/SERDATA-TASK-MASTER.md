# FastCycloneDDS C# Serdata Bindings - Task Master

**Version:** 2.0 (Serdata Approach)  
**Date:** 2026-01-16  
**Status:** Planning Phase - Clean Slate

This document provides the master task list for the **serdata-based** implementation of FastCycloneDDS C# bindings. This is a clean-slate approach replacing the old plain-C native struct marshalling with high-performance CDR serialization.

---

## Task Status Legend

- 🔴 **Not Started** - Task has not begun
- 🟡 **In Progress** - Task is actively being worked on
- 🟢 **Completed** - Task is finished and tested
- 🔵 **Blocked** - Task is blocked by dependencies

---

## Overview: 5 Stages, 28 Tasks

**Total Estimated Effort:** 85-110 person-days (3.5-4.5 months with 1 developer)

**Critical Path:** Stage 1 → Stage 2 → Stage 3 (Core functionality: ~50-65 days)

---

## STAGE 1: Foundation - CDR Core (CRITICAL PATH)

**Goal:** Build and validate the foundational CDR serialization primitives **before** any code generation.

**Duration:** 12-16 days  
**Status:** 🔴 Not Started

### FCDC-S001: CycloneDDS.Core Package Setup
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 1 day  
**Dependencies:** None

**Description:**  
Create the `CycloneDDS.Core` package (`net8.0` target) with project structure, build configuration, and basic infrastructure.

**Deliverables:**
- `Src/CycloneDDS.Core/CycloneDDS.Core.csproj`
- Package metadata (version, authors, license)
- Initial README

---

### FCDC-S002: CdrWriter Implementation
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-S001

**Description:**  
Implement the core `CdrWriter ref struct` that wraps `IBufferWriter<byte>` and provides XCDR2-compliant serialization primitives.

**Must Support:**
- Alignment tracking (`_totalWritten` field)
- Primitive writes (int, uint, long, ulong, float, double, byte, bool)
- String writes (UTF-8 encoding with NUL terminator)
- Fixed buffer writes (`WriteFixedString`)
- Sequence length headers
- DHEADER (delimiter header) support

**Deliverables:**
- `Src/CycloneDDS.Core/CdrWriter.cs`
- Unit tests: `CdrWriterPrimitiveTests.cs`
- Alignment tests: `CdrWriterAlignmentTests.cs`

**Validation:**
- All primitive types serialize with correct alignment
- String writes include length header + NUL terminator
- `_totalWritten` tracks position accurately across buffer flushes

---

### FCDC-S003: CdrReader Implementation
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-S001

**Description:**  
Implement the core `CdrReader ref struct` that wraps `ReadOnlySpan<byte>` and provides XCDR2-compliant deserialization primitives.

**Must Support:**
- Alignment tracking (`_position` field)
- Primitive reads (matching CdrWriter types)
- String reads (return `ReadOnlySpan<byte>` for zero-copy)
- Seek (for skipping unknown fields)
- Bounds checking

**Deliverables:**
- `Src/CycloneDDS.Core/CdrReader.cs`
- Unit tests: `CdrReaderPrimitiveTests.cs`
- Round-trip tests with CdrWriter: `CdrRoundTripTests.cs`

**Validation:**
- Round-trip: `Write(x)` → `Read()` → assert `x == result`
- Alignment matches CdrWriter
- Bounds checks prevent buffer overruns

---

### FCDC-S004: CdrSizeCalculator Utilities
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 2 days  
**Dependencies:** FCDC-S001

**Description:**  
Implement static utility methods for calculating serialized sizes with alignment. Critical for DHEADER generation.

**Must Support:**
- `Align(int currentOffset, int alignment)` → aligned offset
- `GetPrimitiveSize(type)` → size with alignment
- `GetStringSize(string, int currentOffset)` → size with header
- `GetSequenceSize<T>(ReadOnlySpan<T>, int currentOffset)` → size with header

**Deliverables:**
- `Src/CycloneDDS.Core/CdrSizeCalculator.cs`
- Unit tests: `CdrSizeCalculatorTests.cs`

**Validation:**
- Size calculations match actual serialized bytes
- Alignment formulas match XCDR2 spec

---

### FCDC-S005: Golden Rig Integration Test (VALIDATION GATE)
**Status:** 🔴 Not Started  
**Priority:** **CRITICAL - BLOCKING**  
**Estimated Effort:** 3-5 days  
**Dependencies:** FCDC-S002, FCDC-S003, FCDC-S004

**Description:**  
**DO NOT PROCEED TO STAGE 2 WITHOUT 100% PASS RATE ON THIS TEST.**

Create a validation suite that proves C# CDR serialization produces **byte-identical** output to Cyclone native serialization.

**Test Structure:**
1. **C Golden Data Generator:**
   - Define complex IDL: nested structs, strings, sequences, alignment traps
   - Serialize using Cyclone DDS native code
   - Output: Hex dump of serialized bytes

2. **C# Implementation:**
   - Manually write serialization logic (simulating generated code)
   - Use `CdrWriter` to serialize same data
   - Output: Hex dump of serialized bytes

3. **Validation:**
   - Byte-for-byte comparison
   - Print detailed diff on mismatch

**Test Cases (Minimum 8):**
1. Simple primitives (int, double, alignment test)
2. Nested struct (alignment after nested)
3. Fixed string (UTF-8, NUL-padding)
4. Unbounded string (length header, NUL terminator)
5. Sequence of primitives (length + elements)
6. Sequence of strings (nested headers)
7. Struct with mixed types (alignment traps)
8. DHEADER test (appendable struct with delimiter)

**Deliverables:**
- `tests/GoldenRig/golden_data_generator.c` (C program)
- `tests/CycloneDDS.Core.Tests/GoldenConsistencyTests.cs`
- Documented hex dumps for each test case
- CI integration (automated golden test)

**Success Criteria:**
- ✅ 100% byte match on all 8 test cases
- ✅ Automated test runs in CI
- ✅ Any future CDR changes trigger golden rig validation

**Gate:** **NO CODE GENERATION until this passes.**

---

## STAGE 2: Code Generation - Serializer Emitter

**Goal:** Generate XCDR2-compliant serialization code from C# schema types.

**Duration:** 20-25 days  
**Status:** 🔵 Blocked (depends on Stage 1)

### FCDC-S006: Schema Package Migration
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 1-2 days  
**Dependencies:** None (parallel with Stage 1)

**Description:**  
Copy and adapt schema definitions from `old_implem/src/CycloneDDS.Schema`.

**Actions:**
- Copy entire `old_implem/src/CycloneDDS.Schema/**` → `Src/CycloneDDS.Schema/`
- No changes needed (attributes are compatible)
- Add `[DdsManaged]` attribute for managed type opt-in

**Deliverables:**
- `Src/CycloneDDS.Schema/` (complete package)
- Attributes: `[DdsTopic]`, `[DdsKey]`, `[DdsQos]`, `[DdsUnion]`, `[DdsDiscriminator]`, `[DdsCase]`, `[DdsOptional]`, `[DdsManaged]`
- Wrapper types: `FixedString32/64/128`, `BoundedSeq<T,N>`

**Validation:**
- Package compiles
- Attributes have correct `AttributeTargets`

---

### FCDC-S007: Generator Infrastructure
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-S006, **FCDC-S005** (golden rig must pass)

**Description:**  
Set up Roslyn `IIncrementalGenerator` infrastructure for discovering and processing schema types.

**Phases:**
1. Discover types with `[DdsTopic]`
2. Discover types with `[DdsUnion]`
3. Discover global type mappings (`[assembly: DdsTypeMap]`)
4. Build type graph (handle nested types)
5. Set up diagnostic reporting

**Deliverables:**
- `Src/CycloneDDS.Generator/SerDataSourceGenerator.cs`
- `Src/CycloneDDS.Generator/SchemaDiscovery.cs`
- Unit tests: `GeneratorDiscoveryTests.cs`

**Validation:**
- Discovers all annotated types
- Builds correct dependency graph
- Reports diagnostics for invalid schemas

**Reference:** `old_implem/src/CycloneDDS.Generator` (adapt discovery logic)

---

### FCDC-S008: Schema Validator
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-S007

**Description:**  
Validate schema types for XCDR2 appendable compliance and detect breaking changes.

**Validation Rules:**
1. **Appendable Evolution:**
   - New fields only at end
   - No removal of fields
   - No type changes
   - No reordering

2. **Union Validation:**
   - Exactly one `[DdsDiscriminator]`
   - All cases have unique discriminator values
   - Default case is optional

3. **Type Mapping:**
   - Custom types have valid wire representations
   - No circular dependencies

**Deliverables:**
- `Src/CycloneDDS.Generator/SchemaValidator.cs`
- `Src/CycloneDDS.Generator/SchemaFingerprint.cs` (hash of schema structure)
- Unit tests: `SchemaValidatorTests.cs`, `SchemaEvolutionTests.cs`

**Validation:**
- Detects all breaking changes
- Computes stable fingerprint hash
- Generates detailed error messages

**Reference:** `old_implem/src/CycloneDDS.Generator/SchemaValidator.cs` (reuse logic)

---

### FCDC-S009: IDL Emitter (Discovery Only)
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-S007

**Description:**  
Generate IDL files for topic type discovery/registration with Cyclone DDS.

**Note:** IDL is **only** used for discovery, **not** for serialization (we handle that in C#).

**Must Emit:**
- `@appendable` annotation (all types)
- Typedef for custom type mappings
- Enums, structs, unions
- `@key`, `@optional` annotations
- Sequence bounds

**Deliverables:**
- `Src/CycloneDDS.Generator/IdlEmitter.cs`
- Unit tests: `IdlEmitterTests.cs` (snapshot testing)

**Validation:**
- Generated IDL compiles with `idlc`
- Type descriptor created successfully

**Reference:** `old_implem/src/CycloneDDS.Generator/IdlEmitter.cs` (adapt)

---

### FCDC-S010: Serializer Code Emitter - Fixed Types
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 4-5 days  
**Dependencies:** FCDC-S007, **FCDC-S005**

**Description:**  
Generate `GetSerializedSize()` and `Serialize()` methods for **fixed-size types only** (primitives + fixed buffers).

**Generated Code Pattern:**
```csharp
partial struct SensorData : IDdsSerializable
{
    public const int FixedSize = 16;  // Precomputed
    
    public int GetSerializedSize(int currentOffset)
    {
        return FixedSize;
    }
    
    public void Serialize(ref CdrWriter writer)
    {
        writer.WriteUInt32(FixedSize - 4);  // DHEADER
        writer.WriteInt32(Id);
        writer.WriteDouble(Value);
    }
}
```

**Deliverables:**
- `Src/CycloneDDS.Generator/SerializerEmitter.cs`
- Generated interface: `IDdsSerializable`
- Unit tests: `FixedTypeSerializerTests.cs` (compile generated code, round-trip)

**Validation:**
- Generated code compiles without errors
- Round-trip tests pass
- Serialized bytes match golden rig for fixed types

---

### FCDC-S011: Serializer Code Emitter - Variable Types
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 5-6 days  
**Dependencies:** FCDC-S010

**Description:**  
Extend `SerializerEmitter` to handle **variable-size types** (strings, sequences).

**Additional Logic:**
- `GetSerializedSize()` calculates dynamic size
- String serialization (length + UTF-8 bytes + NUL)
- Sequence serialization (length + elements)
- Nested struct recursion

**Generated Code Pattern:**
```csharp
public int GetSerializedSize(int currentOffset)
{
    int size = currentOffset;
    size = CdrSizeCalculator.Align(size, 4);
    size += 4;  // DHEADER
    
    size = CdrSizeCalculator.Align(size, 4);
    size += 4;  // Id
    
    // Variable: string
    size = CdrSizeCalculator.Align(size, 4);
    size += 4;  // Length header
    size += System.Text.Encoding.UTF8.GetByteCount(Name);
    size += 1;  // NUL terminator
    
    return size - currentOffset;
}
```

**Deliverables:**
- Enhanced `SerializerEmitter.cs`
- Unit tests: `VariableTypeSerializerTests.cs`

**Validation:**
- Strings serialize correctly (UTF-8, NUL, header)
- Sequences serialize correctly (length + elements)
- Nested structs serialize recursively

---

### FCDC-S012: Deserializer Code Emitter + View Structs
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 5-6 days  
**Dependencies:** FCDC-S011

**Description:**  
Generate `Deserialize()` methods that return `ref struct` views for zero-copy reads.

**Generated Code Pattern:**
```csharp
public static void Deserialize(ref CdrReader reader, out SensorDataView view)
{
    uint objectSize = reader.ReadUInt32();  // DHEADER
    int endPosition = reader.Position + (int)objectSize;
    
    // FAST PATH: Exact version match
    if (objectSize == SensorData.FixedSize)
    {
        view.Id = reader.ReadInt32();
        view.Value = reader.ReadDouble();
    }
    else
    {
        // ROBUST PATH: Handle evolution
        view.Id = reader.Position < endPosition ? reader.ReadInt32() : 0;
        view.Value = reader.Position < endPosition ? reader.ReadDouble() : 0.0;
        
        // Skip unknown fields
        if (reader.Position < endPosition)
            reader.Seek(endPosition);
    }
}

public ref struct SensorDataView
{
    public int Id;
    public double Value;
    public ReadOnlySpan<byte> NameBytes;  // Zero-copy
    
    public string Name => Encoding.UTF8.GetString(NameBytes);
    
    public SensorData ToOwned()  // Allocating copy
    {
        return new SensorData { Id = Id, Value = Value, Name = Name };
    }
}
```

**Deliverables:**
- `DeserializerEmitter.cs`
- View struct generation
- Unit tests: `DeserializerTests.cs`, `ViewStructTests.cs`

**Validation:**
- Fast path taken when DHEADER matches expected size
- Robust path handles extra fields (evolution)
- View structs provide zero-copy access
- `ToOwned()` creates independent managed copy

---

### FCDC-S013: Union Support
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 4-5 days  
**Dependencies:** FCDC-S011

**Description:**  
Generate serialization for DDS unions (discriminator + active arm).

**Generated Code Pattern:**
```csharp
public void Serialize(ref CdrWriter writer)
{
    writer.WriteInt32((int)Kind);  // Discriminator
    
    switch (Kind)
    {
        case CommandKind.Move:
            Move.Serialize(ref writer);
            break;
        case CommandKind.Spawn:
            Spawn.Serialize(ref writer);
            break;
    }
}

public int GetSerializedSize(int currentOffset)
{
    int size = currentOffset;
    size += 4;  // Discriminator
    
    switch (Kind)
    {
        case CommandKind.Move:
            size += Move.GetSerializedSize(size);
            break;
        case CommandKind.Spawn:
            size += Spawn.GetSerializedSize(size);
            break;
    }
    
    return size - currentOffset;
}
```

**Deliverables:**
- Union-specific emitter logic
- Union view structs
- Unit tests: `UnionSerializerTests.cs`

**Validation:**
- Only active arm serialized
- Discriminator correctly written/read
- Union views provide safe access

---

### FCDC-S014: Optional Members Support
**Status:** 🔴 Not Started  
**Priority:** Medium  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-S011

**Description:**  
Support `@optional` members (nullable reference types in C#).

**Generated Code Pattern:**
```csharp
// Serialize
if (OptionalField != null)
{
    writer.WritePresenceFlag(true);
    OptionalField.Serialize(ref writer);
}
else
{
    writer.WritePresenceFlag(false);
}

// Deserialize
bool hasValue = reader.ReadPresenceFlag();
if (hasValue)
{
    // Deserialize value
}
```

**Deliverables:**
- Optional serialization logic
- Unit tests: `OptionalMemberTests.cs`

**Validation:**
- Presence flag correctly written/read
- Optional values skip serialization when null

---

### FCDC-S015: [DdsManaged] Support (Managed Types)
**Status:** 🔴 Not Started  
**Priority:** Medium  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-S011

**Description:**  
Support `[DdsManaged]` attribute for convenience types (`string`, `List<T>`) that allow GC allocations.

**Generated Code:**
- IDL emits standard `string`/`sequence<T>`
- Serializer uses `List<T>.Count`, allocates on deserialize
- Emits compiler warning if used without attribute

**Deliverables:**
- `[DdsManaged]` attribute handling
- Diagnostic analyzer (error if `string`/`List` without attribute)
- Unit tests: `ManagedTypeTests.cs`

**Validation:**
- `List<T>` serializes/deserializes correctly
- GC allocations measured and documented
- Compiler error for unmarked managed types

---

### FCDC-S016: Generator Testing Suite
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-S010 through FCDC-S015

**Description:**  
Comprehensive testing of all generated code patterns.

**Test Categories:**
1. **Snapshot Tests:** Compare generated code with expected output
2. **Compilation Tests:** Ensure generated code compiles
3. **Round-Trip Tests:** Serialize → Deserialize → assert equality
4. **Evolution Tests:** V1 ↔ V2 compatibility
5. **Error Tests:** Invalid schemas produce diagnostics

**Deliverables:**
- `tests/CycloneDDS.Generator.Tests/**`
- At least 40 tests covering all features

**Validation:**
- 100% pass rate
- Code coverage > 90%

---

## STAGE 3: Runtime Integration - DDS Bindings

**Goal:** Integrate generated serializers with Cyclone DDS via serdata APIs.

**Duration:** 18-24 days  
**Status:** 🔵 Blocked (depends on Stage 2)

### FCDC-S017: Runtime Package Setup + P/Invoke
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 2 days  
**Dependencies:** None (parallel)

**Description:**  
Set up `CycloneDDS.Runtime` package and define P/Invoke declarations for serdata APIs.

**P/Invoke Additions:**
```csharp
[DllImport("ddsc")]
static extern IntPtr dds_create_serdata_from_cdr(
    IntPtr descriptor, ReadOnlySpan<byte> cdrData, int len);

[DllImport("ddsc")]
static extern int dds_write_serdata(IntPtr writer, IntPtr serdata);

[DllImport("ddsc")]
static extern void dds_free_serdata(IntPtr serdata);

[DllImport("ddsc")]
static extern int dds_take_cdr(
    IntPtr reader, Span<IntPtr> buffers, Span<DdsSampleInfo> infos, int max);
```

**Deliverables:**
- `Src/CycloneDDS.Runtime/CycloneDDS.Runtime.csproj`
- `Src/CycloneDDS.Runtime/DdsApi.cs` (P/Invoke)
- Copy `DdsException`, `DdsReturnCode` from old_implem

**Validation:**
- P/Invoke signatures match Cyclone DDS C API

---

### FCDC-S018: DdsParticipant Migration
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 1 day  
**Dependencies:** FCDC-S017

**Description:**  
Copy `DdsParticipant.cs` from `old_implem/src/CycloneDDS.Runtime/`.

**Actions:**
- Copy as-is (no changes needed)
- Wraps `dds_create_participant`
- Stores partition configuration

**Deliverables:**
- `Src/CycloneDDS.Runtime/DdsParticipant.cs`

**Validation:**
- Compiles
- Creates participant successfully

---

### FCDC-S019: Arena Enhancement for CDR
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-S017

**Description:**  
Copy and enhance `Arena.cs` from old_implem for CDR buffer management.

**Enhancements:**
- Add methods for byte buffer allocation
- Pool integration with `ArrayPool<byte>` (optional)
- Trim policy for long-running applications

**Deliverables:**
- `Src/CycloneDDS.Runtime/Arena.cs`
- Unit tests: `ArenaTests.cs`

**Validation:**
- Arena allocates/resets correctly
- No memory leaks (valgrind/profiler)

---

### FCDC-S020: DdsWriter<T> (Serdata-Based)
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 4-5 days  
**Dependencies:** FCDC-S017, FCDC-S018, **FCDC-S010** (serializer generation)

**Description:**  
Implement `DdsWriter<T>` using generated serializers and serdata APIs.

**Write Flow:**
1. Rent buffer from `ArrayPool<byte>.Shared`
2. Call `sample.GetSerializedSize(0)`
3. Create `CdrWriter` over buffer
4. Call `sample.Serialize(ref cdr)`
5. Create serdata: `dds_create_serdata_from_cdr()`
6. Write: `dds_write_serdata()`
7. Free serdata: `dds_free_serdata()`
8. Return buffer to pool

**API:**
```csharp
public class DdsWriter<T> : IDisposable where T : IDdsSerializable
{
    // Constructor with partition support
    public DdsWriter(DdsParticipant participant, DdsQos? qos = null, string[]? partitions = null);
    // If partitions != null: Create implicit Publisher with partition QoS
    // Otherwise: Use default publisher
    // Auto-discover topic metadata from registry
    
    public void Write(in T sample);
    public void WriteDispose(in T sample);
    public bool TryWrite(in T sample, out DdsReturnCode status);
}
```

**Implementation Notes:**
- Constructor must handle partition logic:
  - If `partitions` parameter is provided, create DDS publisher with partition QoS
  - Call `dds_create_publisher` with partition configuration
  - Create topic and writer under this publisher
- If `partitions` is null/empty, use participant's default publisher
- Discover topic name and default QoS from metadata registry

**Deliverables:**
- `Src/CycloneDDS.Runtime/DdsWriter.cs`
- Unit tests: `DdsWriterTests.cs`

**Validation:**
- Zero GC allocations in steady state (measure with `GC.GetTotalAllocatedBytes`)
- Samples written successfully

---

### FCDC-S021: DdsReader<T> + ViewScope
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 5-6 days  
**Dependencies:** FCDC-S017, FCDC-S018, **FCDC-S012** (deserializer generation)

**Description:**  
Implement `DdsReader<T>` using generated deserializers and loaned CDR buffers.

**Read Flow:**
1. Call `dds_take_cdr()` → returns loaned CDR buffers
2. Wrap each buffer in `CdrReader`
3. Call generated `Deserialize(ref reader, out view)`
4. Return `ViewScope<TView>` with views
5. On dispose: `dds_return_loan()`

**API:**
```csharp
public class DdsReader<T, TView> : IDisposable
    where T : IDdsSerializable
{
    // Constructor with partition support
    public DdsReader(DdsParticipant participant, DdsQos? qos = null, string[]? partitions = null);
    // If partitions != null: Create implicit Subscriber with partition QoS
    // Otherwise: Use default subscriber
    // Auto-discover topic metadata from registry
    
    public ViewScope<TView> Take(int maxSamples = 32);
}

public ref struct ViewScope<TView>
{
    public ReadOnlySpan<TView> Samples { get; }
    public ReadOnlySpan<DdsSampleInfo> Infos { get; }
    public void Dispose();  // Returns loan
}
```

**Implementation Notes:**
- Constructor must handle partition logic:
  - If `partitions` parameter is provided, create DDS subscriber with partition QoS
  - Call `dds_create_subscriber` with partition configuration
  - Create topic and reader under this subscriber
- If `partitions` is null/empty, use participant's default subscriber
- Discover topic name and default QoS from metadata registry

**Deliverables:**
- `Src/CycloneDDS.Runtime/DdsReader.cs`
- `Src/CycloneDDS.Runtime/ViewScope.cs`
- Unit tests: `DdsReaderTests.cs`

**Validation:**
- Zero GC allocations for views (measure)
- Loan returned correctly on dispose

---

### FCDC-S022: End-to-End Integration Tests (VALIDATION GATE)
**Status:** 🔴 Not Started  
**Priority:** **CRITICAL - BLOCKING**  
**Estimated Effort:** 5-7 days  
**Dependencies:** FCDC-S020, FCDC-S021

**Description:**  
**DO NOT PROCEED TO STAGE 4 WITHOUT 100% PASS RATE.**

Comprehensive end-to-end tests validating the complete pipeline.

**Test Structure:**
1. Create participant, writer, reader (same process)
2. Write samples via `DdsWriter<T>`
3. Take samples via `DdsReader<T>`
4. Assert: sent == received
5. Measure: Zero GC allocations

**Test Categories (Minimum 20 tests):**

**A. Data Type Coverage (8 tests):**
1. Primitives only (int, double, bool)
2. Fixed strings (FixedString32)
3. Unbounded strings
4. Sequences of primitives
5. Sequences of strings
6. Nested structs
7. Unions (all arms)
8. Optional members

**B. QoS Settings (4 tests):**
1. Reliable vs Best-Effort
2. Durability (TransientLocal)
3. History (KeepLast vs KeepAll)
4. Partitions (isolation)

**C. Keyed Topics (3 tests):**
1. Multiple instances (different keys)
2. Dispose instance
3. Unregister instance

**D. Error Handling (3 tests):**
1. Invalid type (mismatched topic)
2. Disposal after writer close
3. Loan timeout

**E. Performance (2 tests):**
1. Burst write (1000 samples, measure time + GC)
2. Sustained throughput (10K samples)

**Deliverables:**
- `tests/CycloneDDS.Runtime.IntegrationTests/**`
- At least 20 tests
- CI integration

**Success Criteria:**
- ✅ 100% pass rate (20/20 tests)
- ✅ Zero GC allocations on hot path (measure per test)
- ✅ Data integrity (sent == received, byte-perfect)
- ✅ QoS respected (reliability, durability)

**Gate:** **NO STAGE 4 until this passes.**

---

## STAGE 4: XCDR2 Compliance \u0026 Evolution

**Goal:** Full XCDR2 appendable support with schema evolution.

**Duration:** 10-14 days  
**Status:** 🔵 Blocked (depends on Stage 3)

### FCDC-S023: DHEADER Fast/Robust Path Optimization
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-S012, **FCDC-S022**

**Description:**  
Optimize generated deserializers with fast-path vs robust-path branching.

**Enhancement:**
- Fast path: `if (objectSize == ExpectedSize)` → inline, no bounds checks
- Robust path: `if (position < endPosition)` → bounds checks, skip unknown fields

**Deliverables:**
- Enhanced `DeserializerEmitter.cs`
- Performance benchmarks: Fast vs Robust path

**Validation:**
- Fast path measurably faster (< 100ns overhead)
- Robust path handles evolution correctly

---

### FCDC-S024: Schema Evolution Validation
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-S008

**Description:**  
Implement build-time validation to detect breaking schema changes.

**Mechanism:**
- Compute schema fingerprint (hash of field names + types + order)
- Store in `obj/` directory
- Compare on rebuild
- Fail build if breaking change detected

**Deliverables:**
- `Src/CycloneDDS.Generator/SchemaFingerprint.cs`
- Build integration (MSBuild target)
- Unit tests: `SchemaFingerprintTests.cs`

**Validation:**
- Detects field removal, reordering, type changes
- Allows appending new fields

---

### FCDC-S025: Cross-Version Compatibility Tests
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-S023, FCDC-S024

**Description:**  
Test schema evolution scenarios (V1 ↔ V2 compatibility).

**Test Scenarios:**
1. V1 writer → V2 reader (new fields = default)
2. V2 writer → V1 reader (extra fields skipped)
3. Multiple evolutions (V1 → V2 → V3)

**Deliverables:**
- `tests/EvolutionTests/**`
- At least 6 evolution scenarios

**Validation:**
- No data loss on forward/backward compatibility
- Unknown fields skipped gracefully

---

### FCDC-S026: XCDR2 Specification Compliance Audit
**Status:** 🔴 Not Started  
**Priority:** Medium  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-S023

**Description:**  
Audit implementation against OMG XCDR2 specification.

**Checklist:**
- Alignment rules (1, 2, 4, 8)
- DHEADER format (4-byte unsigned)
- String encoding (UTF-8 + NUL)
- Sequence format (length + elements)
- Endianness (Little-endian default)
- Delimiter headers for appendable types

**Deliverables:**
- Compliance report document
- Reference test cases

**Validation:**
- 100% compliance with XCDR2 spec

---

## STAGE 5: Advanced Features \u0026 Production Readiness

**Goal:** Polish, performance, documentation, packaging.

**Duration:** 15-20 days  
**Status:** 🔵 Blocked (depends on Stage 4)

### FCDC-S027: Performance Benchmarks
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 4-5 days  
**Dependencies:** **FCDC-S022**

**Description:**  
Comprehensive performance benchmarking with BenchmarkDotNet.

**Benchmarks (Minimum 12):**

**A. Serialization (6 benchmarks):**
1. Fixed-only struct (baseline)
2. Struct with unbounded string
3. Struct with sequence (varying sizes: 10, 100, 1000 elements)
4. Nested structs (3 levels deep)
5. Union (varying arms)
6. Optional members (present vs absent)

**B. Deserialization (3 benchmarks):**
1. Fast path (exact version)
2. Robust path (extra fields)
3. View struct construction

**C. End-to-End (3 benchmarks):**
1. Write latency (fixed type)
2. Write latency (variable type)
3. Read latency (loaned buffer)

**Deliverables:**
- `tests/Benchmarks/**`
- Benchmark report (markdown)
- Comparison with old marshaller approach (if data available)

**Success Criteria:**
- Fixed types: < 500ns serialization overhead
- Variable types: < 1μs serialization overhead
- Zero allocations on steady-state hot path

---

### FCDC-S028: XCDR2 Serializer Design Document
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-S023

**Description:**  
Create detailed design document for XCDR2 serialization implementation.

**Contents:**
1. XCDR2 specification summary (alignment, headers, encoding)
2. Implementation details (`CdrWriter`/`CdrReader` algorithms)
3. Generated code patterns (examples)
4. Performance optimizations (fast path, inline caching)
5. Edge cases and error handling
6. Test strategy

**Deliverables:**
- `docs/XCDR2-SERIALIZER-DESIGN.md`

**Validation:**
- Document reviewed and approved

---

### FCDC-S029: NuGet Packaging \u0026 Build Integration
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 3-4 days  
**Dependencies:** All previous tasks

**Description:**  
Package all components as NuGet packages with proper dependencies.

**Packages:**
1. `CycloneDDS.Schema` (attributes + wrappers)
2. `CycloneDDS.Core` (CdrWriter/Reader)
3. `CycloneDDS.Generator` (source generator)
4. `CycloneDDS.Runtime` (DDS wrappers)

**Deliverables:**
- `.nupkg` files
- Package metadata (README, license, version)
- MSBuild targets (idlc integration)
- Installation guide

**Validation:**
- Test installation in fresh project
- Verify code generation on build

---

### FCDC-S030: Documentation \u0026 Examples
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 4-5 days  
**Dependencies:** FCDC-S029

**Description:**  
Comprehensive user documentation and example projects.

**Deliverables:**
1. **Getting Started Guide**
   - Installation
   - First pub/sub example
   - Schema definition

2. **User Guide**
   - Schema DSL reference
   - Type mapping
   - QoS configuration
   - Performance best practices

3. **Example Projects**
   - Simple pub/sub (fixed types)
   - Variable-size data (strings, sequences)
   - Unions and optionals
   - Multi-partition setup

4. **API Reference**
   - XML doc comments on all public APIs
   - Reference site generation

**Validation:**
- Examples compile and run
- Documentation reviewed

---

## Summary Statistics

**Total Tasks:** 30  
**Total Estimated Effort:** 85-110 person-days  

**By Stage:**
- Stage 1 (Foundation): 12-16 days (**Blocking**, 5 tasks)
- Stage 2 (Code Gen): 20-25 days (11 tasks)
- Stage 3 (Runtime): 18-24 days (**Blocking**, 6 tasks)
- Stage 4 (XCDR2): 10-14 days (4 tasks)
- Stage 5 (Polish): 15-20 days (4 tasks)

**Critical Path (MVP):** Stages 1-3 = ~50-65 days  
**Production Readiness:** All Stages = ~85-110 days

**Validation Gates:**
1. **FCDC-S005** (Golden Rig) → Blocks Stage 2
2. **FCDC-S022** (Integration Tests) → Blocks Stage 4

---

## Next Steps

1. ✅ Review this task breakdown
2. ✅ Prioritize Stage 1 (Foundation) tasks
3. ▶ **START:** FCDC-S001 (CycloneDDS.Core package setup)
4. ▶ Build CDR primitives (S002-S004)
5. ▶ **VALIDATE:** Golden Rig (S005) before proceeding

---

**Critical Success Factor:** Do not skip validation gates (Golden Rig, Integration Tests). These ensure correctness before building on top.
