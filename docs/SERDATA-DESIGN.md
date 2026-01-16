# FastCycloneDDS C# Bindings - Serdata-Based Design Document

**Version:** 2.0  
**Date:** 2026-01-16  
**Status:** Design Phase - Clean Slate Serdata Approach

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Architectural Transition](#architectural-transition)
3. [System Architecture](#system-architecture)
4. [Implementation Stages](#implementation-stages)
5. [Component Design](#component-design)
6. [CDR Writer/Reader Core](#cdr-writerreader-core)
7. [Code Generation Strategy](#code-generation-strategy)
8. [Memory Management](#memory-management)
9. [Type System](#type-system)
10. [XCDR2 Appendable Support](#xcdr2-appendable-support)
11. [Integration with Cyclone DDS](#integration-with-cyclone-dds)
12. [Testing Strategy](#testing-strategy)
13. [Performance Goals](#performance-goals)
14. [Migration from Old Implementation](#migration-from-old-implementation)

---

## 1. Executive Summary

### 1.1 The Paradigm Shift

This document describes a **fundamental architectural change** from the old plain-C native struct approach to a **high-performance CDR/Serdata-based serialization** approach for C# bindings to Cyclone DDS.

**Old Approach:**
- Generate native C-compatible structs (`TNative`)
- Marshal between C# types and native structs
- Rely on Cyclone's internal serialization

**New Approach (Serdata):**
- Implement custom XCDR2-compliant serializer in C#
- Generate serialization/deserialization code via source generator
- Pass pre-serialized CDR byte streams to Cyclone via serdata APIs
- **Zero allocations** for both fixed and variable-size data

### 1.2 Key Performance Advantages

1. **Eliminates GC Spikes:** Variable-size data (strings, sequences) serialized linearly to pooled buffers
2. **Single Memory Copy:** Managed object → CDR buffer → DDS (vs object → struct → internal buffer)
3. **JIT-Optimizable:** Generated serialization code with no reflection or branching
4. **Arena-Based:** All allocations from reusable memory pools

### 1.3 Design Principles

1. **Zero-Alloc First:** No GC allocations in steady state for both reads and writes
2. **Hybrid Type Support:** Fast path for fixed types, managed types for convenience (`[DdsManaged]`)
3. **XCDR2 Compliance:** Full support for Appendable extensibility with delimiter headers
4. **Source Generation:** All serialization logic generated at compile time
5. **View-Based Reads:** `ref struct` views over loaned CDR buffers for zero-copy reads

---

## 2. Architectural Transition

### 2.1 Why the Change?

The **design-talk.md** cumulative document (3092 lines) captures extensive architectural analysis. Key findings:

 From design-talk.md §504-578:
> **For variable-size data (strings, sequences, unions), the Serdata (CDR) approach offers massive GC and CPU benefits compared to the Native Struct approach.**
>
> **The "Graph vs. Linear" Memory Problem:**
> - Native Structs: Converting managed graph to unmanaged graph requires traversing disjointed memory, pinning multiple objects
> - Serdata: Flattens graph into single contiguous block, **zero allocations** with ArrayPool

### 2.2 What We Keep from Old Implementation

**Reusable Components (from old_implem):**
1. **Schema DSL** (from `old_implem/src/CycloneDDS.Schema`):
   - Attribute definitions (`[DdsTopic]`, `[DdsKey]`, `[DdsQos]`, `[DdsUnion]`)
   - Wrapper types (`FixedString32`, `BoundedSeq<T,N>`)
   - Global type map registry
   
2. **Runtime Foundation** (from `old_implem/src/CycloneDDS.Runtime`):
   - `DdsParticipant` wrapper
   - P/Invoke declarations (will modify for serdata APIs)
   - Error handling (`DdsException`, `DdsReturnCode`)
   - Arena memory manager (enhance for CDR usage)

3. **Test Structure** (from `old_implem/tests`):
   - Integration test patterns
   - Schema validation tests
   - Performance benchmark structure

### 2.3 What We Replace

**Removed Components:**
1. **Native Type Generation** (`*Native.g.cs` structs)
2. **Marshaller Generation** (`*Marshaller.g.cs`)
3. **Managed View Generation** (replaced with CDR view pattern)
4. **Descriptor Extractor** (regex/CppAst parsing of idlc output - no longer needed)

**Replaced With:**
1. **CdrWriter/CdrReader** (core serialization primitives)
2. **Serializer Code Generation** (`partial` methods on user types)
3. **View Structs** (`ref struct`s over CDR buffers)
4. **Type Support** (static metadata per topic)

---

## 3. System Architecture

### 3.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    User Application                          │
│  ┌──────────────────┐      ┌──────────────────────────┐    │
│  │ Schema Types     │      │ Generated Code           │    │
│  │ (C# partial)     │──>──>│ - Serialize() methods    │    │
│  │                  │      │ - Deserialize() methods  │    │
│  │ [DdsTopic(...)]  │      │ - GetSerializedSize()    │    │
│  │ partial struct   │      │ - View structs           │    │
│  └──────────────────┘      └──────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                    │                      │
        ┌───────────▼──────────┐  ┌────────▼─────────┐
        │ CLI Code Generator   │  │  Build Process   │
        │ (Build Tool)         │  │  - Generate IDL  │
        │ - Parse Schema       │  │  - Run idlc      │
        │ - Emit Serializers   │  │  (for discovery) │
        │ - Validate XCDR2     │  │                  │
        └──────────┬───────────┘  └────────┬─────────┘
                   │                       │
    ┌──────────────▼───────────────────────▼──────────────────┐
    │          FCDC Runtime (.NET)                            │
    │  ┌──────────────────┐  ┌─────────────┐  ┌───────────┐ │
    │  │ DdsWriter<T>     │  │ DdsReader<T>│  │ Arena     │ │
    │  │ - CdrWriter      │  │ - CdrReader │  │ Pooling   │ │
    │  │ - ArrayPool      │  │ - ViewScope │  │           │ │
    │  └──────────────────┘  └─────────────┘  └───────────┘ │
    └────────────────────────┬────────────────────────────────┘
                             │ P/Invoke (Serdata APIs)
    ┌────────────────────────▼────────────────────────────────┐
    │         Cyclone DDS C Core                              │
    │  - dds_writecdr / dds_create_serdata_from_cdr          │
    │  - dds_take (returns CDR buffer)                        │
    │  - Type descriptors (from idlc, for discovery)          │
    └─────────────────────────────────────────────────────────┘
```

### 3.2 Data Flow

**Write Path (Zero-Copy):**
```
User Type (SensorData)
    │
    ├─> GetSerializedSize(currentOffset) → Calculate total bytes
    │
    ├─> Rent buffer from ArrayPool<byte>
    │
    ├─> CdrWriter.Serialize(data, buffer)
    │       │
    │       ├─> Write DHEADER (object size)
    │       ├─> Write fields linearly (primitives, strings, sequences)
    │       └─> Result: CDR byte stream in pooled buffer
    │
    ├─> dds_create_serdata_from_cdr(buffer, len)
    │       └─> Cyclone copies CDR to internal serdata
    │
    ├─> dds_write_serdata(writer, serdata)
    │
    ├─> dds_free_serdata(serdata)
    │
    └─> Return buffer to ArrayPool
```

**Read Path (Zero-Copy):**
```
dds_take(reader, ...)
    │
    ├─> Returns loaned CDR buffer (IntPtr)
    │
    ├─> Wrap in ReadOnlySpan<byte>
    │
    ├─> CdrReader.Deserialize(span, out SensorDataView)
    │       │
    │       ├─> Read DHEADER
    │       ├─> Parse fields on-demand (view pattern)
    │       └─> Return ref struct view
    │
    ├─> User accesses: view.Id, view.NameBytes (as ReadOnlySpan)
    │
    ├─> Optional: view.ToOwned() → Allocates managed copy
    │
    └─> Dispose scope → dds_return_loan()
```

---

## 4. Implementation Stages

### Stage 1: Golden Foundation (Critical Path)

**Goal:** Prove CDR serialization correctness before ANY code generation

**Deliverables:**
1. `CycloneDDS.Core` library
   - `CdrWriter` (`IBufferWriter<byte>` based)
   - `CdrReader` (`ReadOnlySpan<byte>` based)
   - XCDR2 alignment and delimiter logic
   
2. Golden Rig Test
   - Define complex IDL in C (nested structs, strings, sequences)
   - Serialize using Cyclone native code → capture hex bytes
   - Manually write C# serialization for same data
   - **Assert byte-for-byte match**

**Reference:** design-talk.md §2850-2916

**Success Criteria:** 100% byte-identical output with Cyclone native serialization

### Stage 2: CLI Code Generator Core

**Goal:** Generate XCDR2-compliant serialization code from C# schema types

**Deliverables:**
1. **CLI Tool (`CycloneDDS.CodeGen`)**
   - Console Application (net8.0)
   - Uses `Microsoft.CodeAnalysis` to parse `.cs` files from disk
   - Runs via MSBuild Target (not compiler plugin)
   - Discover types with `[DdsTopic]`
   - Validate appendable evolution rules
   - Emit IDL (for discovery registration)

2. `SerializerEmitter`
   - Generate `GetSerializedSize(int currentOffset)` method
   - Generate `Serialize(ref CdrWriter)` method
   - Generate `Deserialize(ref CdrReader, out TView)` method
   - Handle fixed fields, variable fields, unions, optionals

3. `IdlcRunner`
   - Orchestrate `idlc.exe` execution
   - Manage descriptor files

4. `DescriptorParser`
   - Use CppAst (libclang) to parse `idlc` output
   - Extract `m_ops` and `m_keys` metadata robustly (no regex)

**Reference:** design-talk.md §3092-3332

**Success Criteria:** Generated code compiles and passes round-trip tests

### Stage 3: Runtime Integration

**Goal:** Integrate with Cyclone DDS via serdata APIs

**Deliverables:**
1. P/Invoke for serdata
   - `dds_create_serdata_from_cdr`
   - `dds_write_serdata`
   - `dds_free_serdata`
   - Enhanced `dds_take` (get CDR buffer)

2. `DdsWriter<T>` (serdata-based)
   - Use generated `GetSerializedSize()` and `Serialize()`
   - Rent buffer from `ArrayPool<byte>.Shared`
   - Call serdata APIs
   - Return buffer to pool

3. `DdsReader<T>` + `ViewScope`
   - Take loaned buffer
   - Wrap in `CdrReader`
   - Call generated `Deserialize()`
   - Return `ref struct` view

**Reference:** design-talk.md §1070-1229

**Success Criteria:** End-to-end pub/sub with zero GC allocations

### Stage 4: XCDR2 Appendable Compliance

**Goal:** Full support for schema evolution

**Deliverables:**
1. DHEADER generation (4-byte size prefix)
2. Fast-path vs robust-path deserialization
3. Schema fingerprinting and validation
4. Unknown field skipping

**Reference:** design-talk.md §1510-1660

**Success Criteria:** V1 reader can consume V2 writer (extra fields ignored)

### Stage 5: Advanced Features

**Goal:** Unions, optionals, managed types

**Deliverables:**
1. Union serialization (discriminator + active arm)
2. Optional members (presence flag + value)
3. `[DdsManaged]` support (List<T>, string) with allocations
4. Performance benchmarking

**Reference:** design-talk.md §732-944

**Success Criteria:** All type features work, benchmarks < 1μs overhead

---

## 5. Component Design

### 5.1 Packages

1. **CycloneDDS.Schema** (Attributes + Wrappers)
   - Source: `old_implem/src/CycloneDDS.Schema` (reuse)
   - No changes needed
   
2. **CycloneDDS.Core** (NEW - CDR Primitives)
   - `CdrWriter` (ref struct)
   - `CdrReader` (ref struct)
   - `CdrSizeCalculator` (static helpers)
   - Target: net8.0

3. **CycloneDDS.CodeGen** (CLI Tool)
   - `SerializerEmitter` (replaces NativeTypeEmitter + MarshallerEmitter)
   - `ViewEmitter` (generates ref struct views)
   - `IdlEmitter` (generates .idl files for idlc)
   - `DescriptorParser` (uses CppAst to parse idlc output)
   - `IdlcRunner` (orchestrates idlc.exe execution)
   - `SchemaValidator` (reuse schema validation logic)
   - **Target: net8.0 (Exe)**

4. **CycloneDDS.Runtime** (DDS Wrappers)
   - `DdsParticipant` (reuse)
   - `DdsWriter<T>` (rewrite for serdata)
   - `DdsReader<T>` (rewrite for serdata)
   - `ViewScope<T>` (ref struct)
   - `Arena` (enhance for CDR buffers)
   - Target: net8.0

5. **CycloneDDS.NativeShim** (Optional - for allocator override)
   - `fcdc_configure_allocator()`
   - Target: C library

---

## 6. CDR Writer/Reader Core

### 6.1 CdrWriter

```csharp
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace CycloneDDS.Core
{
    public ref struct CdrWriter
    {
        private IBufferWriter<byte> _output;
        private Span<byte> _span;
        private int _buffered;
        private int _totalWritten;  // CRITICAL: for alignment tracking

        public CdrWriter(IBufferWriter<byte> output)
        {
            _output = output;
            _span = output.GetSpan();
            _buffered = 0;
            _totalWritten = 0;
        }

        public int Position => _totalWritten + _buffered;

        public void Align(int alignment)
        {
            int currentPos = Position;
            int padding = (alignment - (currentPos % alignment)) & (alignment - 1);
            if (padding > 0)
            {
                EnsureCapacity(padding);
                _span.Slice(0, padding).Clear();  // Write zeros
                Advance(padding);
            }
        }

        public void WriteInt32(int value)
        {
            Align(4);
            EnsureCapacity(4);
            BinaryPrimitives.WriteInt32LittleEndian(_span, value);
            Advance(4);
        }

        public void WriteUInt32(uint value)
        {
            Align(4);
            EnsureCapacity(4);
            BinaryPrimitives.WriteUInt32LittleEndian(_span, value);
            Advance(4);
        }

        public void WriteString(ReadOnlySpan<char> value)
        {
            Align(4);  // String length header
            int utf8ByteCount = Encoding.UTF8.GetByteCount(value);
            WriteUInt32((uint)(utf8ByteCount + 1));  // +1 for NUL terminator
            
            EnsureCapacity(utf8ByteCount + 1);
            int bytesWritten = Encoding.UTF8.GetBytes(value, _span);
            _span[bytesWritten] = 0;  // NUL terminator
            Advance(bytesWritten + 1);
        }

        // Additional: WriteDouble, WriteFloat, WriteFixedString, WriteSequence, etc.

        private void EnsureCapacity(int bytes)
        {
            if (_buffered + bytes > _span.Length)
            {
                Flush();
                _span = _output.GetSpan(bytes);
            }
        }

        private void Advance(int bytes)
        {
            _span = _span.Slice(bytes);
            _buffered += bytes;
        }

        private void Flush()
        {
            if (_buffered > 0)
            {
                _output.Advance(_buffered);
                _totalWritten += _buffered;
                _buffered = 0;
            }
        }

        public void Complete()
        {
            Flush();
        }
    }
}
```

### 6.2 CdrReader

```csharp
using System;
using System.Buffers.Binary;
using System.Text;

namespace CycloneDDS.Core
{
    public ref struct CdrReader
    {
        private ReadOnlySpan<byte> _data;
        private int _position;

        public CdrReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
        }

        public int Position => _position;
        public int Remaining => _data.Length - _position;

        public void Align(int alignment)
        {
            int padding = (alignment - (_position % alignment)) & (alignment - 1);
            _position += padding;
        }

        public int ReadInt32()
        {
            Align(4);
            int value = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(_position));
            _position += 4;
            return value;
        }

        public uint ReadUInt32()
        {
            Align(4);
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(_position));
            _position += 4;
            return value;
        }

        public ReadOnlySpan<byte> ReadStringBytes()
        {
            Align(4);
            uint len = ReadUInt32();
            if (len == 0) return ReadOnlySpan<byte>.Empty;
            
            // len includes NUL terminator
            ReadOnlySpan<byte> bytes = _data.Slice(_position, (int)len - 1);
            _position += (int)len;
            return bytes;
        }

        public void Seek(int position)
        {
            _position = position;
        }

        // Additional: ReadDouble, ReadFloat, ReadSequence, etc.
    }
}
```

---

## 7. Code Generation Strategy

### 7.1 User Schema Example

```csharp
using CycloneDDS.Schema;

[DdsTopic("SensorData")]
[DdsQos(Reliability = DdsReliability.Reliable)]
public partial struct SensorData
{
    [DdsKey]
    public int Id;
    
    public double Value;
    
    public FixedString32 Status;  // Zero-alloc, inline
    
    // Optional: managed type for cold path
    [DdsManaged]
    public string? DebugMessage;
}
```

### 7.2 Generated Code (SerializerEmitter)

```csharp
// Generated/SensorData.Serialization.g.cs
using System;
using CycloneDDS.Core;

partial struct SensorData : IDdsSerializable
{
    // Precomputed for fixed portion
    private const int BaseSize = 4 + 4 + 8 + 32;  // DHEADER + Id + Value + Status

    public int GetSerializedSize(int currentOffset)
    {
        int size = currentOffset;
        size = CdrSizeCalculator.Align(size, 4);
        size += 4;  // DHEADER
        
        size = CdrSizeCalculator.Align(size, 4);
        size += 4;  // Id
        
        size = CdrSizeCalculator.Align(size, 8);
        size += 8;  // Value
        
        size = CdrSizeCalculator.Align(size, 1);
        size += 32;  // FixedString32
        
        // Variable part ([DdsManaged] string)
        if (DebugMessage != null)
        {
            size = CdrSizeCalculator.Align(size, 4);
            size += 4;  // Length header
            size += System.Text.Encoding.UTF8.GetByteCount(DebugMessage);
            size += 1;  // NUL terminator
        }
        else
        {
            size = CdrSizeCalculator.Align(size, 4);
            size += 4;  // Empty string (len=0)
        }
        
        return size - currentOffset;
    }

    public void Serialize(ref CdrWriter writer)
    {
        // Write DHEADER (total object size)
        int objectSize = GetSerializedSize(writer.Position + 4);  // +4 for DHEADER itself
        writer.WriteUInt32((uint)objectSize);
        
        // Write fields
        writer.WriteInt32(Id);
        writer.WriteDouble(Value);
        writer.WriteFixedString(Status.AsUtf8Span(), 32);
        
        if (DebugMessage != null)
        {
            writer.WriteString(DebugMessage);
        }
        else
        {
            writer.WriteUInt32(0);  // Empty string
        }
    }

    public static void Deserialize(ref CdrReader reader, out SensorDataView view)
    {
        uint objectSize = reader.ReadUInt32();
        int endPosition = reader.Position + (int)objectSize;
        
        // FAST PATH: Exact version match
        if (objectSize == BaseSize)
        {
            view = new SensorDataView
            {
                Id = reader.ReadInt32(),
                Value = reader.ReadDouble(),
                StatusBytes = reader.Read(32),
                DebugMessageBytes = ReadOnlySpan<byte>.Empty
            };
        }
        else
        {
            // ROBUST PATH: Handle evolution
            view = new SensorDataView
            {
                Id = reader.Position < endPosition ? reader.ReadInt32() : 0,
                Value = reader.Position < endPosition ? reader.ReadDouble() : 0.0,
                StatusBytes = reader.Position < endPosition ? reader.Read(32) : default,
                DebugMessageBytes = reader.Position < endPosition ? reader.ReadStringBytes() : default
            };
            
            // Skip unknown fields
            if (reader.Position < endPosition)
            {
                reader.Seek(endPosition);
            }
        }
    }
}

// View struct (zero-copy read)
public ref struct SensorDataView
{
    public int Id;
    public double Value;
    public ReadOnlySpan<byte> StatusBytes;
    public ReadOnlySpan<byte> DebugMessageBytes;
    
    // Convenience property
    public string DebugMessage => DebugMessageBytes.IsEmpty 
        ? string.Empty 
        : System.Text.Encoding.UTF8.GetString(DebugMessageBytes);
    
    // Allocating copy
    public SensorData ToOwned()
    {
        return new SensorData
        {
            Id = this.Id,
            Value = this.Value,
            Status = FixedString32.FromUtf8(StatusBytes),
            DebugMessage = DebugMessage  // Allocates
        };
    }
}
```

---

## 8. Memory Management

### 8.1 Write Path - ArrayPool

```csharp
public class DdsWriter<T> where T : IDdsSerializable
{
    // Constructor with partition support
    public DdsWriter(DdsParticipant participant, DdsQos? qos = null, string[]? partitions = null)
    {
        // If partitions specified, create implicit Publisher with partition QoS
        // Otherwise use default publisher
        // Discover topic metadata from registry (topic name, QoS defaults)
        // Create DDS topic and writer
    }

    public void Write(in T sample)
    {
        // 1. Calculate size
        int size = sample.GetSerializedSize(0);
        
        // 2. Rent buffer from shared pool (zero GC allocation)
        byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
        
        try
        {
            // 3. Serialize
            var writer = new ArrayBufferWriter<byte>(buffer, size);
            var cdr = new CdrWriter(writer);
            sample.Serialize(ref cdr);
            cdr.Complete();
            
            // 4. Create serdata
            IntPtr serdata = DdsApi.dds_create_serdata_from_cdr(
                _topicDescriptor, 
                writer.WrittenSpan);
            
            // 5. Write
            DdsApi.dds_write_serdata(_writerHandle, serdata);
            
            // 6. Free serdata
            DdsApi.dds_free_serdata(serdata);
        }
        finally
        {
            // 7. Return buffer to pool
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
```

### 8.2 Read Path - Loaned Buffers

```csharp
public class DdsReader<T, TView> 
    where T : IDdsSerializable
{
    // Constructor with partition support
    public DdsReader(DdsParticipant participant, DdsQos? qos = null, string[]? partitions = null)
    {
        // If partitions specified, create implicit Subscriber with partition QoS
        // Otherwise use default subscriber
        // Discover topic metadata from registry (topic name, QoS defaults)
        // Create DDS topic and reader
    }

    public ViewScope<TView> Take()
    {
        // 1. Take from Cyclone (loaned CDR buffer)
        IntPtr[] samples = stackalloc IntPtr[32];
        DdsSampleInfo[] infos = stackalloc DdsSampleInfo[32];
        
        int count = DdsApi.dds_take_cdr(_readerHandle, samples, infos, 32);
        
        // 2. Wrap in scope
        return new ViewScope<TView>(samples, infos, count, _readerHandle);
    }
}

public ref struct ViewScope<TView>
{
    private IntPtr[] _samplePointers;
    private DdsSampleInfo[] _infos;
    private int _count;
    private IntPtr _readerHandle;
    private TView[] _views;  // Stack-allocated in caller
    
    public ReadOnlySpan<TView> Samples => _views.AsSpan(0, _count);
    public ReadOnlySpan<DdsSampleInfo> Infos => _infos.AsSpan(0, _count);
    
    public void Dispose()
    {
        // Return loan to Cyclone
        DdsApi.dds_return_loan(_readerHandle, _samplePointers, _count);
    }
}
```

---

## 9. Type System

### 9.1 Type Categories

**Fixed-Only Types:**
- Primitives + fixed buffers only
- Serialize directly to span
- Example: `struct { int id; double value; FixedString32 name; }`

**Variable-Size Types:**
- Contains strings, sequences, or unions with variable arms
- Requires size calculation pass
- Example: `struct { int id; string description; float[] values; }`

**Hybrid Types:**
- Mix of fixed and `[DdsManaged]` variable fields
- Hot path: fixed fields, stack-allocated
- Cold path: managed fields, heap-allocated
- Example: `struct { FixedString32 status; [DdsManaged] string? debugInfo; }`

### 9.2 Union Handling

```csharp
[DdsUnion]
public partial struct Command
{
    [DdsDiscriminator]
    public CommandKind Kind;
    
    [DdsCase(CommandKind.Move)]
    public MoveData Move;
    
    [DdsCase(CommandKind.Spawn)]
    public SpawnData Spawn;
}

// Generated serialization
public void Serialize(ref CdrWriter writer)
{
    writer.WriteInt32((int)Kind);  // Discriminator
    
    // Write active arm only
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
```

---

## 10. XCDR2 Appendable Support

### 10.1 DHEADER (Delimiter Header)

Every appendable struct is prefixed with a 4-byte size:

```
Wire Format:
[DHEADER:4] [Field1] [Field2] ... [FieldN]

DHEADER = sizeof(Field1) + sizeof(Field2) + ... + sizeof(FieldN)
```

### 10.2 Fast Path vs Robust Path

```csharp
public static void Deserialize(ref CdrReader reader, out MyDataView view)
{
    uint objectSize = reader.ReadUInt32();
    int endPosition = reader.Position + (int)objectSize;
    
    // OPTIMIZATION: Exact version match (common case)
    if (objectSize == MyData.ExpectedSize)
    {
        // FAST PATH: No bounds checks, straight-line code
        view.Id = reader.ReadInt32();
        view.Value = reader.ReadDouble();
        // JIT can inline and optimize heavily
    }
    else
    {
        // ROBUST PATH: Schema evolution, check bounds
        view.Id = reader.Position < endPosition ? reader.ReadInt32() : 0;
        view.Value = reader.Position < endPosition ? reader.ReadDouble() : 0.0;
        
        // Skip unknown future fields
        if (reader.Position < endPosition)
        {
            reader.Seek(endPosition);
        }
    }
}
```

**Reference:** design-talk.md §1073-1131

---

## 11. Integration with Cyclone DDS

### 11.1 Serdata P/Invoke

```csharp
internal static class DdsApi
{
    [DllImport("ddsc", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr dds_create_serdata_from_cdr(
        IntPtr topicDescriptor,
        ReadOnlySpan<byte> cdrData,
        int cdrDataLen);
    
    [DllImport("ddsc", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int dds_write_serdata(
        IntPtr writer,
        IntPtr serdata);
    
    [DllImport("ddsc", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void dds_free_serdata(IntPtr serdata);
    
    [DllImport("ddsc", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int dds_take_cdr(
        IntPtr reader,
        Span<IntPtr> sampleBuffers,
        Span<DdsSampleInfo> infos,
        int maxSamples);
    
    [DllImport("ddsc", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void dds_return_loan(
        IntPtr reader,
        Span<IntPtr> sampleBuffers,
        int count);
}
```

### 11.2 Topic Registration (Discovery)

**Still need IDL generation for type discovery:**

```csharp
// Generator still emits .idl file
// Run idlc to get type descriptor
// Register descriptor with Cyclone for discovery

public static class SensorDataTypeSupport
{
    // Embedded descriptor (from idlc output)
    private static readonly byte[] s_typeDescriptor = { ... };
    
    public static IntPtr Register(DdsParticipant participant)
    {
        // Register type with Cyclone
        IntPtr descriptor = DdsApi.dds_create_topic_descriptor_from_cdr(
            participant.Handle,
            "SensorData",
            s_typeDescriptor);
        
        return descriptor;
    }
}
```

---

## 12. Testing Strategy

### 12.1 Stage 1: Golden Rig (Foundation)

**Test:** `GoldenConsistencyTest`

1. Define complex IDL in C:
```c
struct Nested { long a; double b; };
struct Golden {
    char c;
    Nested n;
    string<10> s;
    sequence<long> seq;
};
```

2. C program: Serialize with Cyclone → print hex
3. C# program: Serialize with `CdrWriter` → print hex
4. **Assert:** Byte-for-byte match

**Success:** 100% match proves CDR implementation correctness

### 12.2 Stage 2: Generator Tests

**Test:** `GeneratedSerializerTests`

1. Input: C# schema with `[DdsTopic]`
2. Run generator → capture emitted code
3. Compile generated code
4. Round-trip test: `Serialize(data)` → `Deserialize()` → assert equality
5. Snapshot test: Compare generated code with expected

### 12.3 Stage 3: Integration Tests

**Test:** `EndToEndPubSubTests`

1. Create participant, writer, reader (same process)
2. Write sample via `DdsWriter<T>`
3. Take sample via `DdsReader<T>`
4. Assert: sent == received
5. Verify: Zero GC allocations (using GC.GetTotalAllocatedBytes)

### 12.4 Stage 4: Evolution Tests

**Test:** `AppendableEvolutionTests`

1. Define V1 schema
2. Define V2 schema (appended field)
3. V1 writer → V2 reader: Assert extra field = default
4. V2 writer → V1 reader: Assert skips unknown field

---

## 13. Performance Goals

### 13.1 Latency Targets

- **Fixed-only types:** < 500ns serialization overhead
- **Variable types:** < 1μs serialization overhead
- **Read path:** < 100ns view construction (zero-copy)

### 13.2 Allocation Targets

- **Steady state writes:** 0 bytes GC allocated (ArrayPool only)
- **Steady state reads:** 0 bytes GC allocated (loaned buffers only)
- **Cold path (`[DdsManaged]`):** Allocations acceptable

### 13.3 Benchmarks

```csharp
[Benchmark]
public void WriteFixedType()
{
    var data = new SensorData { Id = 123, Value = 45.67 };
    _writer.Write(in data);
}

[Benchmark]
public void ReadFixedType()
{
    using var scope = _reader.Take();
    var view = scope.Samples[0];
    _ = view.Id;
}
```

**Baseline:** Compare against old marshaller-based approach

---

## 14. Migration from Old Implementation

### 14.1 What to Copy

**Direct Reuse:**
1. `old_implem/src/CycloneDDS.Schema/**` → `Src/CycloneDDS.Schema`
2. `old_implem/src/CycloneDDS.Runtime/DdsParticipant.cs` → `Src/CycloneDDS.Runtime/`
3. `old_implem/src/CycloneDDS.Runtime/DdsException.cs` → `Src/CycloneDDS.Runtime/`
4. `old_implem/src/CycloneDDS.Runtime/Arena.cs` → `Src/CycloneDDS.Runtime/` (enhance)

**Adapt:**
1. `old_implem/docs/FCDC-DETAILED-DESIGN.md` §4 (Schema DSL) → Reference
2. `old_implem/docs/FCDC-DETAILED-DESIGN.md` §5.4 (Schema Evolution) → Keep validation logic
3. `old_implem/tests/**/SchemaValidatorTests.cs` → Reuse test patterns

### 14.2 What to Discard

**Remove:**
1. `old_implem/src/CycloneDDS.Generator/**/NativeTypeEmitter.cs`
2. `old_implem/src/CycloneDDS.Generator/**/MarshallerEmitter.cs`
3. `old_implem/src/CycloneDDS.Generator/**/ManagedViewEmitter.cs`
4. `old_implem/src/CycloneDDS.Generator/**/DescriptorExtractor.cs`
5. All `*Native.g.cs` and `*Marshaller.g.cs` reference outputs

### 14.3 Evolution Path

**Phase 1: Foundation (Weeks 1-3)**
- Implement `CdrWriter`/`CdrReader`
- Create Golden Rig test
- Prove correctness

**Phase 2: Generator (Weeks 4-8)**
- Build `SerializerEmitter`
- Generate `Serialize()`/`Deserialize()`
- Unit test generated code

**Phase 3: Runtime (Weeks 9-12)**
- Implement `DdsWriter<T>` (serdata)
- Implement `DdsReader<T>` (serdata)
- Integration tests

**Phase 4: Polish (Weeks 13-16)**
- XCDR2 fast/robust paths
- Union support
- Performance benchmarks
- Documentation

---

## References

1. **Design Talk:** `docs/design-talk.md` (3092 lines) - Complete architectural analysis
2. **Old Design:** `old_implem/docs/FCDC-DETAILED-DESIGN.md` - Original spec (schema DSL, arena)
3. **Old Tasks:** `old_implem/docs/FCDC-TASK-MASTER.md` - 33 tasks (reference structure)
4. **XCDR2 Spec:** `docs/dds-xtypes-1.3-xcdr2-1-single-file.htm` - OMG standard
5. **Cyclone Source:** `cyclonedds/**` - Reference implementation

---

**Next Step:** Create FCDC-SERDATA-TASK-MASTER.md with detailed task breakdown for serdata implementation.
