# FastCycloneDDS C# Serdata Bindings - Task Master

**Version:** 2.0 (Serdata Approach)  
**Date:** 2026-01-16  


This document provides the master task list for the **serdata-based** implementation of FastCycloneDDS C# bindings. This is a clean-slate approach replacing the old plain-C native struct marshalling with high-performance CDR serialization.

This is not tracking the task status, only defining the tasks. There is a dedicated TASK-TRACKER.md document for tracking the status.
---




## STAGE 1: Foundation - CDR Core (CRITICAL PATH)

**Goal:** Build and validate the foundational CDR serialization primitives **before** any code generation.

**Duration:** 12-16 days  


### FCDC-S001: CycloneDDS.Core Package Setup

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


### FCDC-S006: Schema Package Migration

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

### FCDC-S007: CLI Tool Generator Infrastructure

**Priority:** Critical  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-S006, **FCDC-S005** (golden rig must pass)

**Description:**  
**CRITICAL: We use a CLI TOOL, NOT a Roslyn IIncrementalGenerator plugin.**

Reuse and adapt the existing CLI tool (`CycloneDDS.CodeGen`) infrastructure from the old implementation.

**Why CLI Tool (Not Roslyn Plugin):**
- Runs only at build time (via MSBuild target), not on every keystroke
- Easy to debug (standard console app)
- No caching complexity or "ghost generation" issues
- Uses `Microsoft.CodeAnalysis` to parse files from disk, not compiler pipeline

**Actions:**
1. Ensure `CycloneDDS.CodeGen` project is set up as a Console App (`net8.0`)
2. Ensure it accepts source paths as CLI arguments
3. Verify it can load C# files into a `Compilation` unit using `CSharpSyntaxTree`
4. Clean out old emitters (`NativeTypeEmitter`, `MarshallerEmitter`) but keep file finding logic
5. Set up MSBuild `.targets` file to run the tool during build

**Deliverables:**
- `tools/CycloneDDS.CodeGen/` (console app)
- `tools/CycloneDDS.CodeGen/Program.cs` (entry point)
- `tools/CycloneDDS.CodeGen/SchemaDiscovery.cs` (finds `[DdsTopic]` types)
- `tools/CycloneDDS.CodeGen/CycloneDDS.targets` (MSBuild integration)
- Unit tests: `GeneratorDiscoveryTests.cs`

**Validation:**
- Discovers all annotated types from `.cs` files
- Builds correct dependency graph
- Reports diagnostics for invalid schemas
- Runs successfully via `dotnet msbuild` with target

**Reference:** `old_implem/tools/CycloneDDS.CodeGen` (adapt infrastructure)

---

### FCDC-S008: Schema Validator

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

### FCDC-S008b: IDL Compiler Orchestration

**Priority:** High  
**Estimated Effort:** 2 days  
**Dependencies:** FCDC-S007

**Description:**  
Implement logic within the CLI tool to manage the external Cyclone IDL compiler (`idlc`).

**Responsibilities:**
1. **Locate `idlc`:** Check environment variables, NuGet package tools folder, or configured path. Report clear MSBuild error if missing.
2. **Execution:** Run `idlc -l c` on `.idl` files generated by FCDC-S009.
3. **IO Management:** Capture `stdout`/`stderr` from `idlc` and pipe to MSBuild logging (users see IDL syntax errors in Visual Studio).
4. **Cleanup:** Manage temporary `.c` and `.h` files (keep in `obj/` for debugging, or delete).

**Deliverables:**
- `tools/CycloneDDS.CodeGen/IdlcRunner.cs`
- Integration into main CLI execution loop
- Unit tests: `IdlcRunnerTests.cs` (mock process execution)

**Validation:**
- `idlc` found and executed successfully
- IDL syntax errors reported to MBuild
- Generated `.c` files parsed correctly

---

### FCDC-S009: IDL Text Emitter (Discovery Only)

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

### FCDC-S009b: Descriptor Parser (CppAst Replacement)

**Priority:** High  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-S008b

**Description:**  
Implement a **robust parser** for the `.c` files generated by `idlc` using **CppAst (libclang)** instead of Regex.

This component extracts the `m_ops` bytecode and `m_keys` metadata required to register topics with Cyclone runtime.

**Why CppAst?**
Regex fails if `idlc` changes whitespace, indentation, or macro usage. CppAst parses the actual C semantic tree, allowing reliable extraction regardless of formatting.

**Requirements:**
1. Parse the generated `.c` file
2. Locate the `dds_topic_descriptor_t` struct initializer
3. Extract the `m_ops` array (flattening any macros/offsets into raw integers)
4. Extract the `m_keys` array
5. Generate a C# byte array (`private static readonly byte[]`) for `TypeSupport` class

**Deliverables:**
- `tools/CycloneDDS.CodeGen/DescriptorExtraction/DescriptorParser.cs`
- Logic to compile C# byte array literal from parsed data
- Unit tests: `DescriptorParserTests.cs` (test with real `idlc` output)

**Validation:**
- Correctly extracts `m_ops` and `m_keys` from complex IDL
- Handles various `idlc` formatting changes
- Generated descriptor registers successfully with Cyclone

**Reference:** `old_implem/.../DescriptorExtractor.cs` (old regex approach - replace with CppAst)

---

### FCDC-S010: Serializer Code Emitter - Fixed Types

**Priority:** Critical  
**Estimated Effort:** 5-6 days  
**Dependencies:** FCDC-S007, **FCDC-S005**

**Description:**  
Generate `GetSerializedSize()` and `Serialize()` methods for **fixed-size types only** (primitives + fixed buffers).

**CRITICAL XCDR2 Implementation Requirements:**

This is NOT a simple `BinaryWriter`. XCDR2 has strict alignment and DHEADER rules that must be followed exactly.

#### 1. Implement Core Alignment Logic

**Alignment Formula (Must use this exact formula):**
```csharp
int padding = (alignment - (currentPos % alignment)) & (alignment - 1);
```

**Alignment Requirements:**
- `char`, `octet`, `bool`: 1-byte alignment
- `short`, `ushort`: 2-byte alignment
- `int`, `uint`, `float`, `enum`: 4-byte alignment
- `long`, `ulong`, `double`: 8-byte alignment

**Absolute Alignment:** Alignment is calculated from stream byte 0, NOT from struct start. This means nested structs shift alignment based on parent offset.

#### 2. Implement DHEADER Writing Logic

All `APPENDABLE` structs (default) must have a DHEADER (Delimiter Header):
- Type: `uint32`
- Value: Object size **excluding DHEADER itself** (ObjectSize - 4)
- Position: At start of struct, 4-byte aligned

#### 3. Two-Pass Architecture (CRITICAL)

**Pass 1: Size Calculation** - Create `CdrSizer` helper:
```csharp
public ref struct CdrSizer
{
    private int _cursor;
    
    public CdrSizer(int initialOffset) { _cursor = initialOffset; }
    
    public void WriteInt32(int value)
    {
        _cursor = AlignmentMath.Align(_cursor, 4);
        _cursor += 4;
    }
    
    public void WriteDouble(double value)
    {
        _cursor = AlignmentMath.Align(_cursor, 8);
        _cursor += 8;
    }
    
    public int GetSizeDelta(int startOffset) => _cursor - startOffset;
}
```

**Pass 2: Actual Writing** - Uses `CdrWriter` with **identical call sequence**

#### 4. Generated Code Pattern (Fixed Type)

```csharp
partial struct SensorData : IDdsSerializable
{
    public const int FixedSize = 16;  // 4 (DHEADER) + 4 (Id) + 8 (Value)
    
    public int GetSerializedSize(int currentOffset)
    {
        var sizer = new CdrSizer(currentOffset);
        sizer.WriteUInt32(0);        // DHEADER placeholder
        sizer.WriteInt32(this.Id);   // Field 1
        sizer.WriteDouble(this.Value); // Field 2
        return sizer.GetSizeDelta(currentOffset);
    }
    
    public void Serialize(ref CdrWriter writer)
    {
        // Calculate and write DHEADER
        int totalSize = GetSerializedSize(writer.Position);
        writer.WriteUInt32((uint)(totalSize - 4));  // DHEADER value
        
        // Write fields (MUST match GetSerializedSize call sequence)
        writer.WriteInt32(Id);
        writer.WriteDouble(Value);
        
        #if DEBUG
        // Debug safety net: verify size matches
        int actualWritten = writer.Position - startPos;
        if (actualWritten != totalSize)
            throw new DdsSerializationException("Size mismatch!");
        #endif
    }
}
```

#### 5. Symmetric Code Generation (CRITICAL)

The generator MUST emit both methods using **shared logic**. Do NOT write separate emit functions for sizing vs writing.

**Pattern:** Write one emit function that takes a "mode" parameter and emits isomorphic code:
- Sizing mode: emits calls to `sizer.WriteXxx()`
- Writing mode: emits calls to `writer.WriteXxx()`

**This ensures adding/changing a field updates both methods identically.**

#### 6. Create AlignmentMath Helper

Create `Src/CycloneDDS.Core/AlignmentMath.cs`:
```csharp
public static class AlignmentMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Align(int currentPosition, int alignment)
    {
        int mask = alignment - 1;
        int padding = (alignment - (currentPosition & mask)) & mask;
        return currentPosition + padding;
    }
}
```

This is the **single source of truth** for alignment. Both `CdrWriter` and `CdrSizer` MUST use this.

**Deliverables:**
- `tools/CycloneDDS.CodeGen/SerializerEmitter.cs`
- `Src/CycloneDDS.Core/AlignmentMath.cs` (NEW)
- `Src/CycloneDDS.Core/CdrSizer.cs` (NEW - shadow writer for sizing)
- Generated interface: `IDdsSerializable`
- Unit tests: `FixedTypeSerializerTests.cs` (compile generated code, round-trip)
- Unit tests: `AlignmentMathTests.cs` (verify alignment formula)

**Validation:**
- Generated code compiles without errors
- Round-trip tests pass
- Serialized bytes match golden rig for fixed types
- Debug assertion catches any size mismatches
- `AlignmentMath.Align` tested for all edge cases (offsets 0-7 with alignments 1/2/4/8)

**Reference:** design-talk.md §3333-3836 (XCDR2 Deep Dive)

---

### FCDC-S011: Serializer Code Emitter - Variable Types

**Priority:** Critical  
**Estimated Effort:** 6-7 days  
**Dependencies:** FCDC-S010

**Description:**  
Extend `SerializerEmitter` to handle **variable-size types** (strings, sequences). This task is significantly more complex than fixed types due to **absolute alignment propagation**.

**CRITICAL: The "Shifting Struct" Problem**

In XCDR2, alignment is calculated from stream byte 0, NOT struct start. This means:
- A struct at offset 0 has different padding   than the same struct at offset 1
- You CANNOT pre-calculate size as a constant
- `GetSerializedSize(int currentOffset)` is **mandatory** - the offset parameter propagates alignment state

**Example:**
```csharp
struct RobotState {
    byte Mode;      // 1 byte
    string Name;    // Variable
    double Speed;   // 8-byte aligned
}
```

If `RobotState` starts at offset 0:
- Mode at 4 (after DHEADER)
- Name at 5, align to 8 → 3 padding → offset 8
- Name content ends at X
- Speed aligns to next 8-boundary from current offset

If `RobotState` starts at offset 5 (nested):
- DHEADER aligns to 8 → starts at 8
- Mode at 12
- Name at 13...
- **Different total size due to different alignment!**

#### 1. Implement "Two-Pass with Propagation"

**Pass 1: Size Calculation** uses `CdrSizer(currentOffset)`:

```csharp
public int GetSerializedSize(int currentOffset)
{
    var sizer = new CdrSizer(currentOffset);
    
    // DHEADER
    sizer.WriteUInt32(0);
    
    // Field: Mode
    sizer.WriteByte(this.Mode);
    
    // Field: Name (string) - XCDR2 String Format
    sizer.WriteString(this.Name);
    // Inside WriteString logic:
    // - Align(4) for length header
    // - Write Int32(ByteCount + 1)
    // - Advance ByteCount bytes
    // - Advance 1 byte (NUL)
    
    // Field: Speed
    // CRITICAL: Alignment is based on current position AFTER string
    sizer.WriteDouble(this.Speed);
    
    return sizer.GetSizeDelta(currentOffset);
}
```

#### 2. Extend CdrSizer for Strings

Add to `CdrSizer`:
```csharp
public void WriteString(ReadOnlySpan<char> value)
{
    _cursor = AlignmentMath.Align(_cursor, 4); // Length header alignment
    _cursor += 4; // Length (Int32)
    _cursor += Encoding.UTF8.GetByteCount(value);
    _cursor += 1; // NUL terminator
    // Note: Cyclone XCDR2 includes NUL in SOME configurations.
    // Golden Rig (FCDC-S005) validates this.
}
```

#### 3. Implement Sequence Handling

**XCDR2 Sequence Format:**
1. Header: `uint32` Length (4-byte aligned)
2. Body: Elements (each element aligned natively)

```csharp
// Sizing
public void WriteSequence<T>(ReadOnlySpan<T> items) where T : struct
{
    _cursor = AlignmentMath.Align(_cursor, 4);
    _cursor += 4; // Length header
    
    int itemAlignment = GetAlignment<T>();
    for (int i = 0; i < items.Length; i++)
    {
        _cursor = AlignmentMath.Align(_cursor, itemAlignment);
        _cursor += Marshal.SizeOf<T>();
    }
}

// Writing (optimized for primitives)
public void WriteSequence(ReadOnlySpan<long> items)
{
    writer.WriteUInt32((uint)items.Length);
    
    // Optimization: Block copy for primitive arrays
    writer.Align(8);
    MemoryMarshal.Cast<long, byte>(items).CopyTo(writer.Span);
    writer.Advance(items.Length * 8);
}
```

#### 4. Generated Code Pattern (Variable Type)

```csharp
public int GetSerializedSize(int currentOffset)
{
    var sizer = new CdrSizer(currentOffset);
    
    // DHEADER
    sizer.WriteUInt32(0);
    
    // Field: Id (fixed)
    sizer.WriteInt32(this.Id);
    
    // Field: Name (variable string)
    sizer.WriteString(this.Name);
    // Propagates alignment state through string length
    
    // Field: Value (fixed, but alignment depends on name length!)
    sizer.WriteDouble(this.Value);
    
    return sizer.GetSizeDelta(currentOffset);
}

public void Serialize(ref CdrWriter writer)
{
    int totalSize = GetSerializedSize(writer.Position);
    writer.WriteUInt32((uint)(totalSize - 4));
    
    // MUST match GetSerializedSize call sequence
    writer.WriteInt32(this.Id);
    writer.WriteString(this.Name);
    writer.WriteDouble(this.Value);
}
```

#### 5. Handle Nested Structs

When emitting code for nested structs, **always pass currentOffset**:

```csharp
// For field: NestedStruct Inner
sizer.WriteInt32(0); // DHEADER placeholder for Inner
// Inner's size depends on current offset!
int innerSize = this.Inner.GetSerializedSize(sizer.Position);
// Adjust cursor manually (or call Inner.Simulate(ref sizer))
```

#### 6. String Encoding Details

**Critical:** XCDR2 string encoding differs from XCDR1:
- XCDR1: Length includes NUL, bytes include NUL
- XCDR2: Length excludes NUL (usually), bytes exclude NUL (usually)
- **Cyclone Behavior:** Depends on configuration!

**Solution:** Golden Rig (FCDC-S005) validates actual behavior. Generate code that matches Cyclone's configuration.

**Safe Interop Mode (use this):**
```csharp
int byteCount = Encoding.UTF8.GetByteCount(value);
writer.WriteInt32(byteCount + 1); // Include NUL in count
writer.WriteUtf8Bytes(value);
writer.WriteByte(0); // NUL terminator
```

**Deliverables:**
- Enhanced `SerializerEmitter.cs` (variable type handling)
- Extended `CdrSizer.cs` (WriteString, WriteSequence methods)
- Unit tests: `VariableTypeSerializerTests.cs`
- Unit tests: `SequenceSerializerTests.cs`
- Unit tests: `AlignmentPropagationTests.cs` (verify nested struct alignment shifts)

**Validation:**
- Strings serialize correctly (UTF-8, NUL, header)
- Sequences serialize correctly (length + elements)
- Nested structs propagate alignment correctly
- Golden Rig validates byte-perfect output for variable types
- Test case: Same struct at offset 0 vs offset 1 → different sizes

**Reference:** design-talk.md §3368-3519 (Absolute Alignment, String Encoding)

---
- Sequences serialize correctly (length + elements)
- Nested structs serialize recursively

---

### FCDC-S012: Deserializer Code Emitter + View Structs

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

### FCDC-S023: Nested Struct Support ([DdsStruct] Attribute)

**Priority:** HIGH  
**Estimated Effort:** 2-3 days  
**Dependencies:** Stage 2 Complete (Generator Infrastructure)

**Description:**  
Add support for custom helper structs nested within DDS topics using a new `[DdsStruct]` attribute. Implement strict type validation to ensure all nested types are properly marked.

**Design Reference:** `NESTED-STRUCT-SUPPORT-DESIGN.md`

**Implementation Steps:**
1. Add `DdsStructAttribute` to `CycloneDDS.Schema`
2. Update `SchemaDiscovery.cs` to discover `[DdsStruct]` types
3. Build type registry in `SchemaValidator.cs`
4. Implement strict field type validation (check against registry)
5. Support recursive validation for collections (e.g., `BoundedSeq<CustomStruct>`)
6. Update `CodeGenerator.cs` to validate before emitting
7. Update `IdlEmitter.cs` to emit `[DdsStruct]` types correctly

**Deliverables:**
- `Src/CycloneDDS.Schema/Attributes/TypeLevel/DdsStructAttribute.cs` (NEW)
- Updated `tools/CycloneDDS.CodeGen/SchemaDiscovery.cs`
- Updated `tools/CycloneDDS.CodeGen/SchemaValidator.cs`
- Updated `tools/CycloneDDS.CodeGen/CodeGenerator.cs`
- Updated `tools/CycloneDDS.CodeGen/IdlEmitter.cs`

**Tests (Minimum 7):**
- `Discovery_DdsStruct_Found`
  - Define struct with `[DdsStruct]`
  - Success: Type discovered by generator
- `Validation_UnknownStruct_EmitsError`
  - Struct A uses Struct B (no attribute on B)
  - Success: Clear error message emitted
- `Validation_KnownStruct_Passes`
  - Struct A uses Struct B (B has `[DdsStruct]`)
  - Success: No validation errors
- `Validation_NestedSequence_UnknownType_EmitsError`
  - Field: `BoundedSeq<UnknownStruct>`
  - Success: Error emitted
- `Validation_NestedSequence_KnownType_Passes`
  - Field: `BoundedSeq<KnownStruct>` (marked with `[DdsStruct]`)
  - Success: No errors
- `CodeGen_NestedStruct_Compiles`
  - Topic with nested `[DdsStruct]` field
  - Success: Generated code compiles
- `Roundtrip_NestedStruct_Preserves`
  - Topic with `Point3D` field (X=1, Y=2, Z=3)
  - Success: Round-trip preserves values

**Validation:**
- ✅ `[DdsStruct]` types generate serialization code
- ✅ Unknown nested types emit clear error messages
- ✅ Supports arbitrary nesting depth
- ✅ Zero-allocation maintained (struct method inlining)

**Example Usage:**
```csharp
[DdsStruct]
public partial struct Point3D
{
    public double X, Y, Z;
}

[DdsTopic("RobotPath")]
public partial struct RobotPath
{
    [DdsKey] public int RobotId;
    public Point3D StartLocation;              // Nested struct
    public BoundedSeq<Point3D> Waypoints;      // Collection of structs
}
```

---

### FCDC-S024: Type-Level [DdsManaged] Attribute

**Priority:** MEDIUM  
**Estimated Effort:** 1 day  
**Dependencies:** FCDC-S015 ([DdsManaged] Support exists), Stage 2 Complete

**Description:**  
Allow `[DdsManaged]` to be applied at the type level (struct/class) instead of just per-field, making it more convenient for types that are fully managed (e.g., logging, UI data).

**Rationale:**
Avoid verbose field-level annotations when entire type uses managed fields (strings, lists).

**Current Behavior (Verbose):**
```csharp
[DdsTopic("LogEvent")]
public partial struct LogEvent
{
    [DdsKey] public int Id;
    [DdsManaged] public string Message;     // Must mark each field
    [DdsManaged] public List<string> Tags;  // Must mark each field
}
```

**Desired Behavior (Convenient):**
```csharp
[DdsTopic("LogEvent")]
[DdsManaged]  // ← Type-level: applies to all managed fields
public partial struct LogEvent
{
    [DdsKey] public int Id;
    public string Message;      // Inherits [DdsManaged] from type
    public List<string> Tags;   // Inherits [DdsManaged] from type
}
```

**Implementation Steps:**
1. Update `ManagedTypeValidator.cs` to check parent type's attributes
2. Implement `IsManagedContext(TypeInfo type, FieldInfo field)` helper
3. Update validation logic: pass if field OR type has `[DdsManaged]`
4. Update `SerializerEmitter.cs` to check type-level attribute
5. Update `DeserializerEmitter.cs` to check type-level attribute
6. Add XML documentation explaining inheritance behavior

**Deliverables:**
- Updated `tools/CycloneDDS.CodeGen/ManagedTypeValidator.cs`
- Updated `tools/CycloneDDS.CodeGen/SerializerEmitter.cs`
- Updated `tools/CycloneDDS.CodeGen/DeserializerEmitter.cs`

**Tests (Minimum 3):**
- `TypeManaged_StringField_NoFieldAttribute_Validates`
  - Type has `[DdsManaged]`, field is `string` without attribute
  - Success: No validation errors
- `TypeManaged_GeneratedCode_Compiles`
  - Type with `[DdsManaged]`, multiple string/list fields
  - Success: Generated serializer compiles and works
- `TypeManaged_Roundtrip_Preserves`
  - Type with `[DdsManaged]`, strings "Hello", lists [1,2,3]
  - Success: Round-trip preserves all data

**Validation:**
- ✅ Type-level attribute applies to all managed fields
- ✅ Field-level attribute still works (explicit override)
- ✅ Validation error if neither type nor field has attribute
- ✅ Generated code identical to field-level approach

**Validator Logic:**
```csharp
bool isTypeManaged = type.HasAttribute("DdsManaged");
bool isFieldManaged = field.HasAttribute("DdsManaged");

if (IsManagedFieldType(field.TypeName))
{
    if (!isTypeManaged && !isFieldManaged)
    {
        Error: "Mark field or container type with [DdsManaged]"
    }
}
```

---

### FCDC-S025: Advanced IDL Generation Control

**Priority:** **CRITICAL** (Essential for real-world DDS systems)  
**Estimated Effort:** 5-7 days  
**Dependencies:** FCDC-S009 (IDL Emitter exists), Stage 2 Complete

**Description:**  
Enable smart IDL file grouping, module hierarchy overrides, and cross-assembly dependency resolution. Move from "one type = one IDL" to registry-based generation with automatic `#include` handling.

**Design Reference:** `ADVANCED-IDL-GENERATION-DESIGN.md`

**Key Features:**
1. **File Grouping:** Multiple types in one IDL file via `[DdsIdlFile]`
2. **Module Override:** Match legacy IDL modules via `[DdsIdlModule]`
3. **Smart Includes:** Auto-generate `#include` for cross-file dependencies
4. **Cross-Assembly:** Assembly B using types from A auto-includes A's IDL
5. **Metadata:** Bake type-to-IDL mappings into DLLs via `[DdsIdlMapping]`

**Implementation Steps:**
1. Add `DdsIdlFileAttribute` to `CycloneDDS.Schema`  
2. Add `DdsIdlModuleAttribute` to `CycloneDDS.Schema`
3. Add `DdsIdlMappingAttribute` (assembly-level, internal) to `CycloneDDS.Schema`
4. Create `GlobalTypeRegistry` class (three-phase generation)
5. Update `SchemaDiscovery.cs` to extract C# source filename
6. Implement attribute extraction with STRICT validation
7. Implement dependency graph resolution
8. Refactor `CodeGenerator.cs` for multi-phase generation:
   - Phase 1: Discovery & Mapping
   - Phase 2: Dependency Resolution
   - Phase 3: Grouped Emission
9. Implement assembly metadata emission (`CycloneDDS.IdlMap.g.cs`)
10. Implement cross-assembly type resolution via Roslyn metadata
11. Update `IdlEmitter.cs` to support module hierarchies
12. Add MSBuild targets for IDL file copying

**Deliverables:**
- `Src/CycloneDDS.Schema/Attributes/DdsIdlFileAttribute.cs` (NEW)
- `Src/CycloneDDS.Schema/Attributes/DdsIdlModuleAttribute.cs` (NEW)
- `Src/CycloneDDS.Schema/Attributes/DdsIdlMappingAttribute.cs` (NEW)
- `tools/CycloneDDS.CodeGen/GlobalTypeRegistry.cs` (NEW)
- Refactored `tools/CycloneDDS.CodeGen/CodeGenerator.cs` (major refactor)
- Updated `tools/CycloneDDS.CodeGen/SchemaDiscovery.cs`
- Updated `tools/CycloneDDS.CodeGen/IdlEmitter.cs`
- Updated `build/CycloneDDS.targets` (IDL file handling)

**Validation Logic (CRITICAL):**
```csharp
// Clear error messages for all invalid usage
ValidateIdlFileName(string name):
  - No extension (.idl)
  - No path separators
  - Valid file name characters only
  
ValidateIdlModule(string path):
  - Use :: not . (IDL syntax vs C# syntax)
  - Each segment must be valid IDL identifier
  - No leading/trailing separators
  
ValidateExternalReference(string type):
  - External DDS types must have [DdsIdlMapping]
  - Clear error if type used but not found
  
DetectIdlNameCollision(registry):
  - Check for duplicate IDL file::module::name combinations
  - Clear error with resolution guidance
```

**Critical Edge Cases:**
1. **Transitive IDL Copying:** Assembly A → B → C must copy A's IDL to C
2. **Stale File Cleanup:** Changing [DdsIdlFile] leaves old files (document limitation)
3. **IDL Name Collisions:** Detect when two C# types map to same IDL identity

(See ADVANCED-IDL-GENERATION-DESIGN.md Section 7.5 for detailed handling)

**Tests (Minimum 18):**
- `ValidateIdlFile_WithExtension_ThrowsError`
  - `[DdsIdlFile("Types.idl")]`
  - Success: Error "contains extension, use Types without .idl"
- `ValidateIdlFile_WithPath_ThrowsError`
  - `[DdsIdlFile("../Types")]`
  - Success: Error "contains path separators"
- `ValidateIdlFile_InvalidChars_ThrowsError`
  - `[DdsIdlFile("My<Type>")]`
  - Success: Error "invalid characters"
- `ValidateIdlModule_WithDots_ThrowsError`
  - `[DdsIdlModule("A.B.C")]`
  - Success: Error "uses C# syntax, use A::B::C"
- `ValidateIdlModule_InvalidIdentifier_ThrowsError`
  - `[DdsIdlModule("123::Name")]`
  - Success: Error "segment '123' invalid, must start with letter"
- `Registry_LocalType_StoresMapping`
  - Register type with custom file/module
  - Success: Correct mapping stored
- `Registry_ExternalType_ResolvedViaMetadata`
  - Reference assembly with [DdsIdlMapping]
  - Success: External type resolved
- `Dependencies_SameFile_NoInclude`
  - Two types in same IDL file, one uses other
  - Success: No #include generated
- `Dependencies_DifferentFile_AddsInclude`
  - Type A in File1 uses Type B in File2
  - Success: File1.idl has `#include "File2.idl"`
- `Dependencies_External_AddsInclude`
  - Type uses external type from Assembly A
  - Success: `#include "A.idl"` generated
- `EmitIdl_MultipleModules_NestedCorrectly`
  - `[DdsIdlModule("A::B::C")]`
  - Success: `module A { module B { module C { ... }}}`
- `EmitIdl_Dependencies_IncludesFirst`
  - IDL with dependencies
  - Success: `#include` directives at top, before modules
- `EmitMetadata_AllTypes_Recorded`
  - Three types generated
  - Success: Three `[assembly: DdsIdlMapping]` attributes
- `TwoAssemblies_BReferencesA_IncludeGenerated`
  - Assembly A defines Point, B uses Point
  - Success: B's IDL has `#include "A.idl"`
- `CustomFile_MultipleTypes_SingleIdl`
  - Three types with `[DdsIdlFile("Common")]`
  - Success: One Common.idl with all three types
- `CustomModule_LegacyInterop_CorrectHierarchy`
  - C# namespace MyApp, `[DdsIdlModule("Legacy::Sys")]`
  - Success: IDL uses Legacy::Sys hierarchy
- `CircularDependency_Detected_ClearError`
  - A uses B, B uses A (different files)
  - Success: Clear error "Circular dependency: A.idl → B.idl → A.idl"
- `IdlNameCollision_Detected_ClearError`
  - Two C# types map to same IDL file::module::name
  - Success: Clear error with guidance to use [DdsIdlModule]

**Validation:**
- ✅ Multiple types group into one IDL file correctly
- ✅ Module hierarchy overrides work
- ✅ Cross-file dependencies generate `#include`
- ✅ Cross-assembly dependencies auto-detected
- ✅ Assembly metadata embedded correctly
- ✅ MSBuild copies IDL files from referenced assemblies
- ✅ All validation errors are clear and actionable

**Example Usage:**
```csharp
// Default: file-based grouping, namespace-based modules
[DdsStruct]
public partial struct Point { ... }  // → Geometry.idl, module MyApp::Math

// Custom file grouping
[DdsStruct]
[DdsIdlFile("CommonDefs")]
public partial struct Header { ... }  // → CommonDefs.idl

// Legacy interop
[DdsTopic("State")]
[DdsIdlFile("LegacyCore")]
[DdsIdlModule("LegacySys::Core")]
public partial struct SystemState { ... }  // → LegacyCore.idl, module LegacySys::Core
```

**Cross-Assembly Example:**
Assembly A generates `Point.idl` + metadata in DLL.
Assembly B references A, uses Point → auto-generates `#include "Point.idl"`.

---

## STAGE 3: Runtime Integration - DDS Bindings

**Goal:** Integrate generated serializers with Cyclone DDS via serdata APIs.

**Duration:** 18-24 days  


### FCDC-S017: Runtime Package Setup + P/Invoke

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

### FCDC-S022b: Instance Lifecycle Management (Dispose/Unregister)
**Priority:** ⚠️ **HIGH** (Production Requirement)  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-S020 (DdsWriter complete) ✅

**Description:**  
Implement DDS instance lifecycle operations for keyed topics, enabling proper instance disposal and ownership management.

**Operations to Implement:**
1. **DisposeInstance(T)** - Mark instance as deleted/dead
2. **UnregisterInstance(T)** - Writer stops updating instance (releases ownership)

**Architecture:**
- Reuse existing Write() serialization path
- Add new native API calls: `dds_dispose_serdata`, `dds_unregister_serdata`
- Unified implementation via `PerformOperation(sample, operation)` pattern
- Maintain zero-allocation guarantee

**Use Cases:**
- Resource cleanup (dispose deleted entities)
- Graceful shutdown (unregister on app exit)
- Ownership transfer (exclusive ownership QoS)
- Instance lifecycle tracking (reader state management)

**Implementation Steps:**

1. **Native Extension:**
   - Export `dds_dispose_serdata` in ddsc.dll
   - Export `dds_unregister_serdata` in ddsc.dll
   - Rebuild cyclone-bin

2. **P/Invoke Layer:**
   ```csharp
   // DdsApi.cs
   [DllImport("ddsc")]
   public static extern int dds_dispose_serdata(DdsEntity writer, IntPtr serdata);
   
   [DllImport("ddsc")]
   public static extern int dds_unregister_serdata(DdsEntity writer, IntPtr serdata);
   ```

3. **DdsWriter Enhancement:**
   ```csharp
   private enum DdsOperation { Write, Dispose, Unregister }
   
   private void PerformOperation(in T sample, DdsOperation op) { ... }
   
   public void Write(in T sample) => PerformOperation(sample, DdsOperation.Write);
   public void DisposeInstance(in T sample) => PerformOperation(sample, DdsOperation.Dispose);
   public void UnregisterInstance(in T sample) => PerformOperation(sample, DdsOperation.Unregister);
   ```

**Deliverables:**
- `Src/CycloneDDS.Runtime/DdsWriter.cs` - Add DisposeInstance(), UnregisterInstance()
- `Src/CycloneDDS.Runtime/Interop/DdsApi.cs` - Add P/Invoke declarations
- `tests/CycloneDDS.Runtime.Tests/DdsWriterLifecycleTests.cs` - 7 unit tests
- `tests/CycloneDDS.Runtime.Tests/InstanceLifecycleIntegrationTests.cs` - 4 integration tests
- `docs/INSTANCE-LIFECYCLE-DESIGN.md` - ✅ Complete
- Update `Src/CycloneDDS.Runtime/README.md` with examples

**Test Requirements (11 tests):**

**Unit Tests (7):**
1. DisposeInstance_ValidSample_Succeeds
2. DisposeInstance_AfterWrite_SendsDisposalMessage
3. UnregisterInstance_ValidSample_Succeeds
4. UnregisterInstance_AfterWrite_SendsUnregisterMessage
5. DisposeInstance_NonKeySample_IgnoresNonKeyFields
6. DisposeInstance_AfterWriterDispose_Throws
7. UnregisterInstance_MultipleWriters_HandlesCorrectly

**Integration Tests (4):**
1. WriteDisposeRead_VerifiesInstanceStateNotAliveDisposed
2. WriteUnregisterRead_VerifiesInstanceStateNotAliveNoWriters
3. MultipleWritersUnregister_VerifiesOwnership
4. DisposeInstance_ZeroAllocation_1000Operations

**Success Criteria:**
- ✅ All 11 tests passing
- ✅ Zero GC allocations verified (same as Write)
- ✅ Reader instance states correct (NOT_ALIVE_DISPOSED, NOT_ALIVE_NO_WRITERS)
- ✅ Ownership transfer works (multiple writers)
- ✅ Documentation with usage examples

**Performance Target:**
- Dispose/Unregister: ~40 bytes/1000 operations (same as Write)

**Design Reference:**
- `docs/INSTANCE-LIFECYCLE-DESIGN.md` (complete architecture)
- `docs/design-talk.md` lines 5106-5412 (detailed discussion)

**Why HIGH Priority:**
1. Production systems require proper lifecycle management
2. Prevents reader resource leaks (stale instances accumulate)
3. Critical for graceful shutdown (avoid reader timeouts)
4. Required for exclusive ownership patterns
5. Low complexity (extends existing Write pattern)

---

## STAGE 3.75: Extended DDS API - Modern C# Idioms

**Goal:** Add essential DDS features with modern .NET idioms (type auto-discovery, async/await, events, filtering, discovery) plus optional sender tracking.

**Duration:** 16-24 days (8 tasks: 1 foundation + 5 extended API + 2 sender tracking)  

**Design References:**  
- `docs/EXTENDED-DDS-API-DESIGN.md` - Core extended API features
- `docs/SENDER-TRACKING-DESIGN.md` - Sender tracking specification

**Strategic Position:** These features provide core DDS functionality expected by users and must be implemented BEFORE advanced evolution features (Stage 4-Deferred) and production readiness (Stage 5).

**Note:** All tasks reference design documents to avoid repetition. Read design docs for full implementation details.

---

### FCDC-EXT00: Type Auto-Discovery & Topic Management

**Priority:** **CRITICAL** (Foundation for all other EXT tasks)  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-S020 (DdsWriter exists), FCDC-S021 (DdsReader exists)

**Description:**  
Eliminate manual descriptor passing by auto-discovering type metadata from generic type `T`. Manage topic lifecycle within DdsParticipant to ensure topics are created only once per name.

**Design Reference:** `EXTENDED-DDS-API-DESIGN.md` Section 4

**Implementation Steps:**
1. Create `DdsTypeSupport` static class with reflection-based descriptor extraction
2. Add `_topicCache` dictionary to `DdsParticipant`
3. Implement `GetOrRegisterTopic<T>(string topicName, DdsQos?)` in `DdsParticipant`
4. Update `DdsWriter<T>` constructor to call `GetOrRegisterTopic<T>()` internally
5. Update `DdsReader<T>` constructor to call `GetOrRegisterTopic<T>()` internally
6. Add topic cleanup to `DdsParticipant.Dispose()`
7. Cache `Func<uint[]>` delegates for `GetDescriptorOps()` calls

**Deliverables:**
- `Src/CycloneDDS.Runtime/DdsTypeSupport.cs` (new internal static class)
- Updated `Src/CycloneDDS.Runtime/DdsParticipant.cs`
- Updated `Src/CycloneDDS.Runtime/DdsWriter.cs`
- Updated `Src/CycloneDDS.Runtime/DdsReader.cs`

**Tests (Minimum 4):**
- `TopicCache_SameName_ReturnsSameHandle`
  - Create two writers for "TopicA"
  - Success: Both use same topic handle (verify via internal cache)
- `AutoDiscovery_ValidType_Succeeds`
  - Instantiate `DdsWriter<TestMessage>` without manual descriptor
  - Success: Writer created successfully, can send/receive data
- `AutoDiscovery_InvalidType_Throws`
  - Instantiate `DdsWriter<int>` (primitive has no descriptor ops)
  - Success: Throws `InvalidOperationException` with helpful message
- `Qos_AttributeApplied`
  - Define type with `[DdsQos(Reliability = Reliable)]`
  - Create writer without explicit QoS parameter
  - Success: Topic uses Reliable QoS (verify via native query)

**Validation:**
- ✅ No manual descriptor passing required in user code
- ✅ Type safety enforced (generics prevent type mixing)
- ✅ Topic created only once per participant/name combination
- ✅ QoS from `[DdsQos]` attributes applied automatically
- ✅ Reflection overhead amortized (one-time delegate cache)

---

### FCDC-EXT01: Read vs Take with Condition Masks

**Priority:** **CRITICAL**  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-S021 (DdsReader exists)

**Description:**  
Add non-destructive `Read()` and state filtering to `DdsReader`. Enables "observer" patterns and precise data selection (e.g., "only unread samples").

**Design Reference:** `EXTENDED-DDS-API-DESIGN.md` Section 4

**Implementation Steps:**
1. Define state enums (`DdsSampleState`, `DdsViewState`, `DdsInstanceState`) in `CycloneDDS.Runtime`
2. Add `dds_readcdr` P/Invoke to `DdsApi.cs`
3. Refactor `DdsReader.Take()` to use new `ReadOrTake(mask, operation)` helper
4. Add `Read()` methods with mask parameters
5. Update XML documentation

**Deliverables:**
- `Src/CycloneDDS.Runtime/DdsStateEnums.cs`
- Updated `Src/CycloneDDS.Runtime/Interop/DdsApi.cs`
- Updated `Src/CycloneDDS.Runtime/DdsReader.cs`

**Tests (Minimum 3):**
- `Read_IsNonDestructive_CallTwice_GetSameData`
  - Write sample, Read twice, assert identical results
  - Success: Both reads return same data, data remains in cache
- `Take_IsDestructive_CallTwice_SecondEmpty`
  - Write sample, Take, Take again
  - Success: Second Take returns 0 samples
- `TakeWithMask_NotRead_FiltersCorrectly`
  - Write 3 samples, Read 1st, Take(NotRead)
  - Success: Only 2nd and 3rd samples returned

**Validation:**
- ✅ `Read()` leaves data in cache (verified by second read)
- ✅ `Take()` removes data (verified by empty second take)
- ✅ Masks correctly filter (verified by sample count and content)

---

### FCDC-EXT02: Async/Await Support (WaitDataAsync)

**Priority:** **CRITICAL**  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-S021 (DdsReader exists)

**Description:**  
Bridge DDS listeners to .NET `async/await` via `TaskCompletionSource`. Enables non-blocking wait for data availability without burning threads.

**Design Reference:** `EXTENDED-DDS-API-DESIGN.md` Section 5

**Implementation Steps:**
1. Add listener P/Invoke (`dds_create_listener`, `dds_lset_data_available`, etc.) to `DdsApi.cs`
2. Add `WaitDataAsync(CancellationToken)` to `DdsReader`
3. Implement lazy listener attachment (only when `WaitDataAsync` called)
4. Implement native callback bridge with GC pinning
5. Add `StreamAsync()` convenience method (allocates, but idiomatic)
6. Update `Dispose()` to cleanup listener and GCHandle

**Deliverables:**
- Updated `Src/CycloneDDS.Runtime/Interop/DdsApi.cs` (listener APIs)
- Updated `Src/CycloneDDS.Runtime/DdsReader.cs`

**Tests (Minimum 4):**
- `WaitDataAsync_CompletesWhenDataArrives`
  - Start wait, write data from another task
  - Success: `WaitDataAsync` completes within 1 second
- `WaitDataAsync_RespectsCancellation`
  - Call with 100ms timeout, don't write data
  - Success: Task is canceled within 200ms
- `Polling_NoListener_NoOverhead`
  - Only call `Take()`, never `WaitDataAsync()`
  - Success: Verify listener is NOT created (check internal state)
- `DisposeWithListener_NoLeaks`
  - Call `WaitDataAsync()`, dispose reader
  - Success: No unmanaged handles leaked (can be manual verification)

**Validation:**
- ✅ Async wait completes on data arrival
- ✅ Cancellation works correctly
- ✅ Listener only created when needed (lazy)
- ✅ Proper cleanup on dispose

---

### FCDC-EXT03: Content Filtering (Reader-Side Predicates)

**Priority:** High  
**Estimated Effort:** 1-2 days  
**Dependencies:** FCDC-EXT01 (ViewScope infrastructure)

**Description:**  
Add client-side filtering using C# lambda expressions on `TView` struct. Filters are evaluated during `ViewScope` iteration with zero allocation overhead.

**Design Reference:** `EXTENDED-DDS-API-DESIGN.md` Section 6

**Implementation Steps:**
1. Add `SetFilter(Predicate<TView>?)` method to `DdsReader`
2. Add `_filter` field to `ViewScope` (passed from reader)
3. Update `ViewScope.Enumerator.MoveNext()` to skip filtered samples
4. Add XML documentation
5. No P/Invoke changes required

**Deliverables:**
- Updated `Src/CycloneDDS.Runtime/DdsReader.cs`
- Updated `Src/CycloneDDS.Runtime/ViewScope.cs`

**Tests (Minimum 3):**
- `Filter_Applied_OnlyMatchingSamples`
  - Write samples with values 1, 5, 10
  - Set filter `view.Value > 3`
  - Success: Iteration yields only 5, 10
- `Filter_UpdatedAtRuntime_NewFilterApplied`
  - Initial filter: `view.Value > 5`
  - Take samples, verify filtered
  - Update filter: `view.Value < 8`
  - Take again, verify new filter applied
- `Filter_Null_AllSamplesReturned`
  - Set filter to `view.Value > 100`
  - Set filter to `null`
  - Success: All samples visible in iteration

**Validation:**
- ✅ Filter predicates execute during iteration
- ✅ Filter updates are thread-safe (atomic assignment)
- ✅ Zero allocation overhead (no intermediate collections)
- ✅ JIT inlining verified for simple predicates

---

### FCDC-EXT04: Status & Discovery (Events)

**Priority:** High  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-EXT02 (async infrastructure), FCDC-S020, FCDC-S021

**Description:**  
Map DDS status callbacks to C# `event EventHandler<TStatus>`. Expose connectivity (PublicationMatched, SubscriptionMatched) and health (LivelinessChanged) monitoring. Add `WaitForReaderAsync()` helper.

**Design Reference:** `EXTENDED-DDS-API-DESIGN.md` Section 7

**Implementation Steps:**
1. Define status structs (`DdsPublicationMatchedStatus`, `DdsSubscriptionMatchedStatus`, etc.) in `DdsApi.cs`
2. Add status listener P/Invoke (`dds_lset_publication_matched`, `dds_get_publication_matched_status`, etc.)
3. Add `PublicationMatched` event and `PublicationMatchedStatus` property to `DdsWriter`
4. Add `SubscriptionMatched` and `LivelinessChanged` events to `DdsReader`
5. Implement `WaitForReaderAsync(TimeSpan)` using event + TaskCompletionSource pattern
6. Add lazy listener attachment logic (reuse pattern from FCDC-EXT02)

**Deliverables:**
- Updated `Src/CycloneDDS.Runtime/Interop/DdsApi.cs` (status structs and APIs)
- Updated `Src/CycloneDDS.Runtime/DdsWriter.cs`
- Updated `Src/CycloneDDS.Runtime/DdsReader.cs`

**Tests (Minimum 4):**
- `PublicationMatched_EventFires_OnReaderCreation`
  - Create writer, subscribe to event, create reader
  - Success: Event fires with `CurrentCount = 1`
- `WaitForReaderAsync_CompletesOnDiscovery`
  - Start wait, create reader from another task
  - Success: Task completes within 2 seconds
- `PublicationMatched_EventFires_OnReaderDispose`
  - Writer with reader, dispose reader
  - Success: Event fires with `CurrentCountChange < 0`
- `SubscriptionMatched_CurrentCount_Accurate`
  - Reader, create 2 writers, dispose 1
  - Success: CurrentCount progresses 0→2→1

**Validation:**
- ✅ Events fire correctly on discovery/loss
- ✅ `WaitForReaderAsync` solves "lost first message" problem
- ✅ `CurrentCount` tracks active connections accurately

---

### FCDC-EXT05: Instance Management (Keyed Topics)

**Priority:** Medium  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-EXT01 (Read/Take infrastructure), FCDC-S020, FCDC-S021, FCDC-S022b (Instance lifecycle from BATCH-14)

**Description:**  
Enable O(1) lookup and filtering by instance handle for keyed topics. Critical for systems tracking many objects (e.g., fleet management, air traffic control). This task completes the instance lifecycle work started in FCDC-S022b by adding keyed test types and comprehensive verification of instance operations.

**Design Reference:** `EXTENDED-DDS-API-DESIGN.md` Section 8

**Implementation Steps:**
1. Create keyed test type `KeyedTestMessage` with `[DdsKey]` attribute in test project
2. Create `DdsInstanceHandle` struct in `CycloneDDS.Runtime`
3. Add instance P/Invoke (`dds_lookup_instance`, `dds_take_instance`, `dds_read_instance`) to `DdsApi.cs`
4. Implement `DdsReader.LookupInstance(in T keySample)` using temporary serdata
5. Implement `DdsReader.TakeInstance(handle, maxSamples)`
6. Implement `DdsReader.ReadInstance(handle, maxSamples)`
7. Add internal `ReadOrTakeInstance` helper
8. Re-enable and fix the 3 skipped tests from BATCH-18 (IntegrationTests.cs lines 365-428)

**Deliverables:**
- `tests/CycloneDDS.Runtime.Tests/KeyedTestMessage.cs` (NEW - keyed test type)
- `Src/CycloneDDS.Runtime/DdsInstanceHandle.cs`
- Updated `Src/CycloneDDS.Runtime/Interop/DdsApi.cs`
- Updated `Src/CycloneDDS.Runtime/DdsReader.cs`
- Updated `tests/CycloneDDS.Runtime.Tests/IntegrationTests.cs` (re-enable 3 skipped tests)

**Keyed Test Type Definition:**
```csharp
[DdsTopic("KeyedTestTopic")]
public partial struct KeyedTestMessage
{
    [DdsKey, DdsId(0)]
    public int SensorId;   // Key field for instance identification
    
    [DdsId(1)]
    public int Value;      // Data field
}
```

**Tests (Minimum 9):**

**A. Instance Lookup & Filtering (3 tests):**
- `LookupInstance_ReturnsValidHandle`
  - Write sample with `SensorId=5`
  - Lookup with key `{SensorId=5}`
  - Success: Returns non-Nil handle
- `TakeInstance_OnlyReturnsMatchingData`
  - Write `SensorId=1` and `SensorId=2`
  - Lookup handle for `SensorId=1`, TakeInstance
  - Success: Only `SensorId=1` returned, `SensorId=2` remains in cache
- `LookupInstance_UnknownKey_ReturnsNil`
  - Never written `SensorId=999`
  - Lookup `{SensorId=999}`
  - Success: Returns `DdsInstanceHandle.Nil`

**B. Instance Lifecycle (3 tests from BATCH-18 - currently skipped):**
- `DisposeInstance_ValidSample_MarksDisposed`
  - Create writer/reader with `KeyedTestMessage`
  - Write sample: `{SensorId=1, Value=100}`
  - Call `writer.DisposeInstance({SensorId=1, Value=0})`
  - Reader takes samples
  - Success: `info.InstanceState == DdsInstanceState.NotAliveDisposed`
- `UnregisterInstance_ValidSample_ReleasesOwnership`
  - Create writer/reader with `KeyedTestMessage`
  - Write sample: `{SensorId=2, Value=200}`
  - Call `writer.UnregisterInstance({SensorId=2, Value=0})`
  - Reader takes samples
  - Success: `info.InstanceState == DdsInstanceState.NotAliveNoWriters`
- `InstanceLifecycle_MultipleInstances_TrackedSeparately`
  - Write 3 instances: SensorId=1, 2, 3
  - Dispose SensorId=2
  - Take all samples
  - Success: Only instance 2 marked as DISPOSED, others ALIVE

**C. Multi-Instance Operations (3 additional tests):**
- `ReadInstance_NonDestructive_DataRemains`
  - Write `SensorId=1`, `SensorId=2`
  - Lookup instance 1, ReadInstance
  - ReadInstance again
  - Success: Both reads return same data
- `MultipleInstances_IndependentLifecycles`
  - Write 5 instances (SensorId 1-5)
  - Take instance 1, dispose instance 3, unregister instance 5
  - Success: Instance states correct (ALIVE for 2,4; DISPOSED for 3; NO_WRITERS for 5; taken for 1)
- `InstanceHandle_PersistsAcrossUpdates`
  - Write `{SensorId=10, Value=100}`
  - Lookup handle H1
  - Write `{SensorId=10, Value=200}` (update)
  - Lookup handle H2
  - Success: H1 == H2 (same instance)

**Validation:**
- ✅ Lookup returns correct handles
- ✅ Instance-specific take filters correctly (O(1) access)
- ✅ Unknown instances return Nil handle
- ✅ DisposeInstance marks correct instance state
- ✅ UnregisterInstance releases writer ownership
- ✅ Multiple instances tracked independently
- ✅ Instance handles persist across updates

---

### FCDC-EXT06: Sender Tracking Infrastructure

**Priority:** Medium  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-S020 (DdsWriter), FCDC-S021 (DdsReader), FCDC-EXT02 (Async/Await)

**Description:**  
Implement optional sender tracking feature allowing attribution of each received sample to its source application instance (AppDomainId, AppInstanceId, ComputerName, ProcessName, ProcessId).

**Design Reference:** `SENDER-TRACKING-DESIGN.md` (full specification)

**Implementation Steps:**
1. Define `DdsGuid` struct (16-byte DDS GUID wrapper) in `DdsApi.cs`
2. Define `SenderIdentity` struct in `CycloneDDS.Runtime.Tracking`
3. Define `SenderIdentityConfig` record
4. Create `SenderRegistry` class (background identity cache + correlation)
5. Add P/Invoke: `dds_get_guid`, `dds_get_matched_publications`, `dds_get_matched_publication_data`, `dds_free`
6. Implement background async monitoring of identity topic

**Deliverables:**
- `Src/CycloneDDS.Runtime/Tracking/SenderIdentity.cs`
- `Src/CycloneDDS.Runtime/Tracking/SenderIdentityConfig.cs`
- `Src/CycloneDDS.Runtime/Tracking/SenderRegistry.cs`
- Updated `Src/CycloneDDS.Runtime/Interop/DdsApi.cs`

**Tests (Minimum 5):**
- `IdentityPublishing_WriterCreated_PublishesSenderInfo`
  - Enable tracking with config, create writer
  - Success: SenderIdentity published with correct AppDomainId, ProcessId
- `IdentityCache_RemoteIdentity_CachedInRegistry`
  - Two participants, enable tracking on both
  - Success: Registry caches remote participant's identity
- `HandleCorrelation_PublicationHandle_MapsToIdentity`
  - Receive data from remote writer
  - Success: PublicationHandle maps to correct SenderIdentity
- `GetSender_O1Lookup_FastPerformance`
  - Benchmark `GetSender(index)` call
  - Success: < 10ns per lookup (dictionary access)
- `DisabledOverhead_TrackingOff_ZeroImpact`
  - Don't call EnableSenderTracking
  - Success: No listeners created, no overhead

**Validation:**
- ✅ Identity topic uses TransientLocal (late joiners work)
- ✅ O(1) lookup performance verified
- ✅ Zero overhead when feature disabled

---

### FCDC-EXT07: Sender Tracking Integration

**Priority:** Medium  
**Estimated Effort:** 1-2 days  
**Dependencies:** FCDC-EXT06 (Sender tracking infrastructure)

**Description:**  
Integrate sender tracking into DdsParticipant, DdsWriter, DdsReader, and ViewScope APIs.

**Design Reference:** `SENDER-TRACKING-DESIGN.md` Sections 6, 7, 8

**Implementation Steps:**
1. Add `EnableSenderTracking(SenderIdentityConfig)` to `DdsParticipant`
2. Add internal `RegisterWriter()`/`UnregisterWriter()` to `DdsParticipant`
3. Update `DdsWriter` constructor/dispose to call registration hooks
4. Add `EnableSenderTracking(SenderRegistry)` to `DdsReader`
5. Hook `SubscriptionMatched` event in reader to register remote writers
6. Add `GetSender(int index)` to `ViewScope<TView>`
7. Add private `_registry` field to `ViewScope`

**Deliverables:**
- Updated `Src/CycloneDDS.Runtime/DdsParticipant.cs`
- Updated `Src/CycloneDDS.Runtime/DdsWriter.cs`
- Updated `Src/CycloneDDS.Runtime/DdsReader.cs`
- Updated `Src/CycloneDDS.Runtime/ViewScope.cs`

**Tests (Minimum 3):**
- `SenderTracking_MultiProcess_CorrectIdentity`
  - Two processes with different AppDomainIds
  - Process A sends data, Process B receives
  - Success: B's `GetSender()` returns A's identity
- `SenderTracking_LateJoiner_TransientLocalWorks`
  - Start sender, publish identity, then start receiver
  - Success: Receiver retrieves sender identity from history
- `SenderTracking_MultiInstance_ProcessIdDisambiguates`
  - Same AppDomainId, different ProcessId
  - Success: ProcessId correctly distinguishes instances

**Validation:**
- ✅ GetSender() returns correct identity for all samples
- ✅ Thread-safe concurrent access
- ✅ Late joiner scenario works (TransientLocal)

---

### Stage 3.75 Success Criteria

**Functional:**
- ✅ All 43 tests pass (6 auto-discovery + 3 read/take + 18 extended API + 9 instance management + 8 sender tracking)
- ✅ No breaking changes to existing APIs
- ✅ All new APIs work with zero-copy core
- ✅ Keyed topic instance lifecycle fully verified

**Performance:**
- ✅ Zero-Copy path remains allocation-free
- ✅ Async overhead only when WaitDataAsync used
- ✅ Content filtering has minimal overhead (JIT inlining)
- ✅ Sender tracking O(1) lookups (<10ns)
- ✅ Type reflection overhead amortized (one-time cache)

**Usability:**
- ✅ APIs feel natural to C# developers
- ✅ Common patterns require minimal code
- ✅ Opt-in features have zero overhead when disabled
- ✅ **No manual descriptor passing required** (auto-discovery)

---

## STAGE 4-DEFERRED: XCDR1/XCDR2 Compatibility

**Goal:** Full backward compatibility with legacy XCDR1 systems while maintaining XCDR2 as default  
**Duration:** 2-3 days  
**Priority:** MEDIUM (Important for legacy system interop)

**Design Reference:** `XCDR1-XCDR2-COMPATIBILITY-DESIGN.md`

**Context:**
The C# bindings currently use strict XCDR2 encoding (Appendable CDR2). This prevents interoperability with legacy DDS systems that only support XCDR1 (Classic CDR). The solution implements a stateful encoding context that allows runtime selection between formats while maintaining clean architecture.

---

### FCDC-COMPAT-01: XCDR1/XCDR2 Dual Encoding Support

**Priority:** Medium  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-S020 (DdsWriter), FCDC-S021 (DdsReader), All Stage 2 tasks

**Description:**  
Implement dual encoding support for XCDR1 (legacy) and XCDR2 (modern) CDR formats with automatic format selection based on type extensibility and auto-detection on the read path.

**Design Reference:** `XCDR1-XCDR2-COMPATIBILITY-DESIGN.md` (full specification)

**Key Architectural Decision:**  
Use **stateful encoding context** rather than parameter passing. The `CdrWriter`, `CdrReader`, and `CdrSizer` structs carry an immutable `Encoding` field that propagates through the entire serialization chain.

**Implementation Steps:**

1. **Create CdrEncoding Enum** (`Src/CycloneDDS.Core/CdrEncoding.cs`):
   - Define `Xcdr1 = 0` and `Xcdr2 = 2` enum values
   - Document wire format differences (NUL terminators, DHEADER)

2. **Update Core Primitives with Stateful Context**:
   - **CdrWriter**: Add `readonly CdrEncoding Encoding` field, update `WriteString()` to check encoding
   - **CdrReader**: Add auto-detection in constructor (inspect header[1]), update `ReadString()` to check encoding
   - **CdrSizer**: Add `readonly CdrEncoding Encoding` field, update `WriteString()` sizing logic

3. **Update Code Generation (Conditional DHEADER)**:
   - **SerializerEmitter**: Add runtime check `if (writer.Encoding == CdrEncoding.Xcdr2)` before DHEADER logic
   - **DeserializerEmitter**: Add runtime check `if (reader.Encoding == CdrEncoding.Xcdr2)` before DHEADER skip
   - **SizerEmitter**: Add runtime check `if (sizer.Encoding == CdrEncoding.Xcdr2)` before DHEADER sizing

4. **Integrate Runtime Encoding Selection**:
   - **DdsWriter**: Determine encoding from `DdsTypeSupport.GetExtensibility<T>()`
     - `Final` → `Xcdr1`, write header `0x0001` (LE) or `0x0000` (BE)
     - `Appendable/Mutable` → `Xcdr2`, write header `0x0009` (LE) or `0x0008` (BE)
   - **DdsWriter**: Configure QoS with `dds_qset_data_representation(qos, 1, {encoding})`
   - **DdsReader**: Configure QoS to accept both formats: `dds_qset_data_representation(qos, 2, {0, 2})`

5. **Add P/Invoke for QoS Configuration**:
   - Add `dds_qset_data_representation(IntPtr qos, uint n, short[] values)` to `DdsApi.cs`

6. **Regenerate All Test Types** to use updated emitters

**Deliverables:**
- `Src/CycloneDDS.Core/CdrEncoding.cs` (new file)
- Updated `Src/CycloneDDS.Core/CdrWriter.cs`
- Updated `Src/CycloneDDS.Core/CdrReader.cs`
- Updated `Src/CycloneDDS.Core/CdrSizer.cs`
- Updated `tools/CycloneDDS.CodeGen/SerializerEmitter.cs`
- Updated `tools/CycloneDDS.CodeGen/DeserializerEmitter.cs`
- Updated `tools/CycloneDDS.CodeGen/SizerEmitter.cs`
- Updated `Src/CycloneDDS.Runtime/DdsWriter.cs`
- Updated `Src/CycloneDDS.Runtime/DdsReader.cs`
- Updated `Src/CycloneDDS.Runtime/Interop/DdsApi.cs`

**Tests (Minimum 8):**

**Unit Tests (6):**
1. **Xcdr1String_Roundtrip**
   - Create `@final` struct with string field
   - Serialize, verify NUL terminator in buffer (check byte at offset)
   - Deserialize, verify string matches original
   
2. **Xcdr2String_Roundtrip**
   - Create `@appendable` struct with string field
   - Serialize, verify NO NUL at end of string bytes
   - Deserialize, verify string matches original

3. **Xcdr1Appendable_DegradeToFinal**
   - Force `@appendable` struct to serialize in XCDR1 mode
   - Verify NO DHEADER in buffer (check expected offsets)
   - Verify strings have NUL terminators
   - Deserialize successfully

4. **MixedNesting_OuterFinal_InnerAppendable**
   - Define `OuterStruct(@final)` containing `InnerStruct(@appendable)` field
   - Serialize `OuterStruct`
   - Verify entire blob is XCDR1 (header `0x0001`, no DHEADER, strings with NUL)
   - Deserialize, verify nested data correct

5. **AutoDetection_Xcdr1Message_CorrectRead**
   - Manually construct XCDR1 blob with header `0x0001`
   - Pass to `CdrReader`
   - Verify `reader.Encoding == CdrEncoding.Xcdr1`
   - Deserialize string, verify NUL handling correct

6. **AutoDetection_Xcdr2Message_CorrectRead**
   - Manually construct XCDR2 blob with header `0x0009`
   - Pass to `CdrReader`
   - Verify `reader.Encoding == CdrEncoding.Xcdr2`
   - Deserialize string, verify no NUL expected

**Integration Tests (2):**
7. **QosHandshake_FinalType_AdvertisesXcdr1**
   - Create `DdsWriter<FinalStruct>`
   - Query published QoS (if possible) or verify via discovery
   - Success: QoS advertises `DDS_DATA_REPRESENTATION_XCDR1`

8. **QosHandshake_AppendableType_AdvertisesXcdr2**
   - Create `DdsWriter<AppendableStruct>`
   - Query published QoS or verify via discovery
   - Success: QoS advertises `DDS_DATA_REPRESENTATION_XCDR2`

**Validation:**
- ✅ `@final` types produce XCDR1-compliant blobs (verified byte-by-byte)
- ✅ `@appendable` types produce XCDR2-compliant blobs (verified byte-by-byte)
- ✅ Reader auto-detects both formats correctly
- ✅ Mixed nesting handled correctly (context propagates)
- ✅ QoS handshake configured properly
- ✅ Zero performance overhead for XCDR2 default path
- ✅ All existing tests still pass

**Migration Guide for Users:**
- Default behavior unchanged (`@appendable` → XCDR2)
- For legacy interop: Use `[DdsExtensibility(DdsExtensibilityKind.Final)]`
- Reader automatically accepts both formats (no config needed)

---

## STAGE 4: XCDR2 Compliance & Evolution (DEFERRED)

**Goal:** Full XCDR2 appendable support with schema evolution.

**Duration:** 10-14 days  

**Note:** Stage 3.75 Extended API features are now higher priority than evolution support.

### FCDC-S023: DHEADER Fast/Robust Path Optimization

**Priority:** Medium (downgraded from High)  
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


### FCDC-S027: Performance Benchmarks

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

**Priority:** High  
**Estimated Effort:** 3-4 days  
**Dependencies:** All previous tasks

**Description:**  
Package all components as NuGet packages with proper dependencies.

**Packages:**
1. `CycloneDDS.Schema` (attributes + wrappers)
2. `CycloneDDS.Core` (CdrWriter/Reader)
3. `CycloneDDS.CodeGen` (CLI Tool - tools folder packaging)
   - Must include `.targets` file to run the exe during build
   - Add MSBuild task to invoke `CycloneDDS.CodeGen.exe` with source paths
4. `CycloneDDS.Runtime` (DDS wrappers)

**Deliverables:**
- `.nupkg` files
- Package metadata (README, license, version)
- MSBuild targets (`CycloneDDS.targets` for code generation + idlc integration)
- Installation guide

**Validation:**
- Test installation in fresh project
- Verify code generation runs on build (via MSBuild target)
- Verify generated code compiles

---

### FCDC-S030: Documentation \u0026 Examples

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

## STAGE 6: Advanced Optimizations (Performance++)

**Goal:** Extended type support and block copy optimizations for maximum performance  

**Duration:** 16-21 days  
**Reference:** [ADVANCED-OPTIMIZATIONS-DESIGN.md](ADVANCED-OPTIMIZATIONS-DESIGN.md)

### FCDC-ADV01: Custom Type Support (Guid, DateTime)

**Priority:** Medium  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-S002, FCDC-S003, FCDC-S004, FCDC-S007

**Description:**  
Add built-in serialization support for common .NET types: `Guid` and `DateTime`.

**Implementation:**

1. **CdrWriter Extensions** (`Src/CycloneDDS.Core/CdrWriter.cs`):
   ```csharp
   public void WriteGuid(Guid value)        // 16 bytes, align 1
   public void WriteDateTime(DateTime value) // int64 ticks, align 4
   ```

2. **CdrSizer Extensions** (`Src/CycloneDDS.Core/CdrSizer.cs`):
   ```csharp
   public void WriteGuid(Guid value)        // _cursor += 16
   public void WriteDateTime(DateTime value) // WriteInt64(0)
   ```

3. **CdrReader Extensions** (`Src/CycloneDDS.Core/CdrReader.cs`):
   ```csharp
   public Guid ReadGuid()
   public DateTime ReadDateTime()
   ```

4. **TypeMapper Updates** (`tools/CycloneDDS.CodeGen/TypeMapper.cs`):
   - Add mappings: `"Guid" => "WriteGuid"`, `"DateTime" => "WriteDateTime"`
   - Add alignment: Guid=1, DateTime=4

5. **IdlEmitter Updates** (`tools/CycloneDDS.CodeGen/IdlEmitter.cs`):
   - Map `Guid` → `typedef octet Guid[16];`
   - Map `DateTime` → `typedef int64 DateTime;`

**Deliverables:**
- Updated Core library (Writer/Sizer/Reader)
- Updated CodeGen (TypeMapper, IdlEmitter)
- Unit tests: `CustomTypesTests.cs` (8+ tests)

**Validation:**
- Round-trip tests pass for Guid and DateTime
- Generated IDL compiles with idlc
- Byte-perfect wire format (verified via Golden Rig if needed)

---

### FCDC-ADV02: System.Numerics Support

**Priority:** Medium  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-ADV01

**Description:**  
Add built-in support for `System.Numerics` math types for robotics/graphics applications.

**Supported Types:**
- `Vector2` (8 bytes: 2 × float)
- `Vector3` (12 bytes: 3 × float)
- `Vector4` (16 bytes: 4 × float)
- `Quaternion` (16 bytes: 4 × float)
- `Matrix4x4` (64 bytes: 16 × float)

**Implementation:**

1. **CdrWriter Extensions**:
   ```csharp
   public void WriteVector2(System.Numerics.Vector2 value)
   public void WriteVector3(System.Numerics.Vector3 value)
   public void WriteVector4(System.Numerics.Vector4 value)
   public void WriteQuaternion(System.Numerics.Quaternion value)
   public void WriteMatrix4x4(System.Numerics.Matrix4x4 value)
   ```

2. **TypeMapper Updates**:
   - Add all `System.Numerics.*` mappings
   - All align to 4 (float alignment)

3. **IdlEmitter Updates**:
   - Emit struct definitions for each type (e.g., `struct Quaternion { float x,y,z,w; }`)

**Deliverables:**
- Core extensions
- CodeGen updates
- Unit tests: `SystemNumericsTests.cs` (6+ tests)

**Validation:**
- Round-trip tests for all types
- IDL structs compile correctly

---

### FCDC-ADV03: Array Support (`T[]`)

**Priority:** Medium  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-S010, FCDC-S011, FCDC-S012, FCDC-S015

**Description:**  
Add support for native C# arrays (`T[]`) with `[DdsManaged]` attribute.

**Wire Format:** `sequence<T>` (length header + elements)

**Implementation:**

1. **SerializerEmitter** (`tools/CycloneDDS.CodeGen/SerializerEmitter.cs`):
   - Add `EmitArrayWriter(FieldInfo field)`
   - OPTIMIZATION: For primitive arrays, use block copy:
     ```csharp
     var byteSpan = MemoryMarshal.AsBytes(new ReadOnlySpan<T>(array));
     writer.WriteBytes(byteSpan);
     ```

2. **DeserializerEmitter** (`tools/CycloneDDS.CodeGen/DeserializerEmitter.cs`):
   - Add `EmitArrayReader(FieldInfo field)`
   - OPTIMIZATION: For primitives, use block copy:
     ```csharp
     var src = reader.ReadFixedBytes(len * elemSize);
     MemoryMarshal.Cast<byte, T>(src).CopyTo(array);
     ```

3. **ManagedTypeValidator**:
   - Enforce `[DdsManaged]` on all array fields

**Deliverables:**
- Updated emitters
- Updated validator
- Unit tests: `ArraySupportTests.cs` (6+ tests: primitive arrays, string arrays, struct arrays)

**Validation:**
- Round-trip tests for `int[]`, `double[]`, `string[]`, custom struct arrays
- Block copy optimization verified for primitives
- Performance test: 10k element array < 1ms

---

### FCDC-ADV04: Dictionary Support (`Dictionary<K,V>`)

**Priority:** Medium  
**Estimated Effort:** 4-5 days  
**Dependencies:** FCDC-S010, FCDC-S011, FCDC-S012, FCDC-S015

**Description:**  
Add support for `Dictionary<K,V>` with `[DdsManaged]` attribute.

**Wire Format:** `sequence<Entry<K,V>>` (NOT DDS `map<K,V>` to avoid sorting overhead)

**Implementation:**

1. **IdlEmitter** (`tools/CycloneDDS.CodeGen/IdlEmitter.cs`):
   - Pre-scan struct fields for dictionaries
   - Emit Entry struct for each unique `<K,V>` combination:
     ```idl
     struct Entry_String_Int {
         string key;
         int32 value;
     };
     ```
   - Track emitted structs to avoid duplicates

2. **SerializerEmitter**:
   - Add `EmitDictionaryWriter(FieldInfo field)`
   - Iterate dictionary pairs:
     ```csharp
     writer.WriteUInt32((uint)dict.Count);
     foreach (var kvp in dict) {
         WriteKey(kvp.Key);
         WriteValue(kvp.Value);
     }
     ```

3. **DeserializerEmitter**:
   - Add `EmitDictionaryReader(FieldInfo field)`
   - Read count, loop to read pairs:
     ```csharp
     var dict = new Dictionary<K,V>((int)count);
     for (int i=0; i<count; i++) {
         var key = ReadKey();
         var val = ReadValue();
         dict.Add(key, val);
     }
     ```

4. **CdrSizer**:
   - Add `EmitDictionarySizer(FieldInfo field)`
   - Iterate if K or V are variable-length

**Helper Methods:**
- `(string kType, string vType) GetDictTypes(string typeName)` - Parse `Dictionary<K,V>`
- `string CleanName(string type)` - Convert type to valid IDL identifier (replace dots, remove brackets)

**Deliverables:**
- Updated IdlEmitter with Entry struct generation
- Updated SerializerEmitter/DeserializerEmitter/CdrSizer
- Unit tests: `DictionarySupport Tests.cs` (8+ tests)

**Validation:**
- Test combinations: `Dictionary<int,string>`, `Dictionary<string,double>`, `Dictionary<Guid,SensorData>`
- Verify linear O(N) serialization (no sorting)
- Round-trip correctness

---

### FCDC-ADV05: Block Copy Optimization (`[DdsOptimize]`)

**Priority:** Medium  
**Estimated Effort:** 5-6 days  
**Dependencies:** FCDC-ADV02, FCDC-ADV03, FCDC-S015

**Description:**  
Implement `[DdsOptimize]` attribute for enabling block copy (memcpy) on user-defined blittable structs and external library types.

**Design:** Three-layer priority system:
1. **Field Attribute** (highest) - Override on specific field
2. **Internal Whitelist** - Automatic for `System.Numerics.*`
3. **Type Attribute** - User's own struct definitions

**Implementation:**

1. **Attribute Definition** (`Src/CycloneDDS.Schema/Attributes/TypeLevel/DdsOptimizeAttribute.cs`):
   ```csharp
   [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property)]
   public sealed class DdsOptimizeAttribute : Attribute
   {
       public bool BlockCopy { get; set; } = true;
       public int Alignment { get; set; } = 4;
   }
   ```

2. **SerializerEmitter Updates**:
   - Add `GetOptimizationSettings(FieldInfo field, string elementType)` helper
   - Add `IsWhitelisted(string typeName, out int alignment)` helper
   - Update `EmitListWriter`/`EmitArrayWriter` to check optimization settings
   - For optimized types, use block copy path (same as primitives)

3. **DeserializerEmitter Updates**:
   - Same helper methods
   - Update `EmitListReader`/`EmitArrayReader` for block copy
   - Use `Unsafe.SizeOf<T>()` for user types

4. **CdrSizer Updates**:
   - Skip iteration for optimized types:
     ```csharp
     int totalBytes = list.Count * Unsafe.SizeOf<T>();
     sizer.Skip(totalBytes);
     ```

5. **Whitelist**:
   - Vector2, Vector3, Vector4, Quaternion, Plane, Matrix4x4 (all align 4)

**User Scenarios:**

**A. Whitelisted (zero config):**
```csharp
[DdsManaged]
public List<Vector3> Waypoints;  // Automatically optimized
```

**B. User struct:**
```csharp
[DdsOptimize(BlockCopy=true, Alignment=4)]
[StructLayout(LayoutKind.Sequential, Pack=1)]
public struct LidarPoint {
    public float Distance;
    public byte Intensity;
}

[DdsManaged]
public List<LidarPoint> Points;  // Optimized via type attribute
```

**C. External type:**
```csharp
[DdsManaged]
[DdsOptimize(BlockCopy=true, Alignment=4)]  // Field-level override
public List<OpenCV.Point2f> Features;
```

**Deliverables:**
- `DdsOptimizeAttribute.cs`
- Updated emitters (Serializer/Deserializer/Sizer)
- Whitelist implementation
- Unit tests: `BlockCopyOptimizationTests.cs` (12+ tests)
- Documentation: `docs/BLOCK-COPY-GUIDE.md`

**Validation:**
- Whitelist types automatically optimized
- User structs with attribute use block copy
- Field-level override works for external types
- Performance test: `List<Vector3>` (10k items) < 1ms serialization
- Safety: Verify blittable requirement enforced

---


**Critical Success Factor:** Do not skip validation gates (Golden Rig, Integration Tests). These ensure correctness before building on top.
