# FastCycloneDDS C# Bindings - Detailed Design Document

**Version:** 1.0  
**Date:** 2026-01-14  
**Status:** Design Phase

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [System Architecture](#system-architecture)
3. [Component Design](#component-design)
4. [Schema DSL Design](#schema-dsl-design)
5. [Code Generation Pipeline](#code-generation-pipeline)
6. [Runtime Components](#runtime-components)
7. [Memory Management](#memory-management)
8. [Type System](#type-system)
9. [Union Support](#union-support)
10. [Optional Members](#optional-members)
11. [Native Interop Layer](#native-interop-layer)
12. [Testing Strategy](#testing-strategy)
13. [Performance Requirements](#performance-requirements)
14. [Build Integration](#build-integration)

---

## 1. Executive Summary

### 1.1 Purpose

FastCycloneDDS C# Bindings (FCDC) provides a high-performance C# binding layer over **Cyclone DDS C API** that supports:

- **C#-first DSL**: Define DDS topic types using C# with attributes
- **XTypes `@appendable`**: Automatic backward-compatible schema evolution
- **Unions**: Express DDS unions in C# with explicit discriminators
- **Optional members**: Nullable class references via XTypes `@optional`
- **Variable-size data**: Support both bounded and unbounded strings/sequences
- **Arena pooling**: Near-zero allocations in steady state
- **Minimal API**: Participant, Reader<T>, Writer<T> with IDisposable

### 1.2 Key Design Principles

1. **Performance First**: Zero-copy reads where possible, allocation-free hot paths
2. **Type Safety**: Strongly-typed generated code, compile-time validation
3. **Cyclone-Only**: Leverage Cyclone-specific features without interop constraints
4. **C# Idioms**: Make schemas feel like natural C# while mapping cleanly to IDL
5. **Explicit Lifetimes**: Arena scopes make memory ownership clear

### 1.3 Non-Goals

- Interoperability with non-Cyclone DDS implementations
- Support for XTypes `@mutable` or `@final` extensibility
- Full DDS API surface (no low-level entity lifecycle management)
- Runtime reflection-based type discovery

---

## 2. System Architecture

### 2.1 Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    User Application                          │
│  ┌──────────────────┐      ┌──────────────────────────┐    │
│  │ Schema Types     │      │ Generated Code           │    │
│  │ (C# with attrs)  │──>──>│ - TNative (blittable)    │    │
│  │                  │      │ - TManaged (views)       │    │
│  │ [DdsTopic(...)]  │      │ - Marshallers            │    │
│  │ partial class    │      │ - Metadata Registry      │    │
│  └──────────────────┘      └──────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                    │                      │
        ┌───────────▼──────────┐  ┌────────▼─────────┐
        │ Roslyn Source        │  │  Build Process   │
        │ Generator            │  │  - Generate IDL  │
        │ - Parse Schema       │  │  - Run idlc      │
        │ - Emit C# + IDL      │  │  - Compile Shim  │
        │ - Validate Rules     │  │                  │
        └──────────┬───────────┘  └────────┬─────────┘
                   │                       │
    ┌──────────────▼───────────────────────▼──────────────────┐
    │          FCDC Runtime (.NET)                            │
    │  ┌──────────────────┐  ┌─────────────┐  ┌───────────┐ │
    │  │ DdsParticipant   │  │ DdsReader<T>│  │ Arena     │ │
    │  │ DdsWriter<T>     │  │ TakeScope   │  │ Pooling   │ │
    │  └──────────────────┘  └─────────────┘  └───────────┘ │
    └────────────────────────┬────────────────────────────────┘
                             │ P/Invoke
    ┌────────────────────────▼────────────────────────────────┐
    │         Native Shim (C)                                 │
    │  - Allocator override integration                       │
    │  - Helper functions for efficient interop               │
    └────────────────────────┬────────────────────────────────┘
                             │
    ┌────────────────────────▼────────────────────────────────┐
    │         Cyclone DDS C Core                              │
    │  - dds_take / dds_write / dds_writedispose             │
    │  - Loaning support                                      │
    │  - Type descriptors (from idlc)                         │
    └─────────────────────────────────────────────────────────┘
```

### 2.2 Component Layers

#### Layer 1: Schema Definition
- User-authored C# partial types with attributes
- Minimal attribute noise (infer most from C# types)

#### Layer 2: Code Generation (Build-Time)
- **Roslyn Source Generator**: Parse schema, validate, emit code
- **IDL Compiler**: Run Cyclone `idlc` on generated IDL
- **Native Shim Build**: Compile C interop layer

#### Layer 3: Runtime (Managed)
- **DDS Wrappers**: Participant, Reader, Writer
- **Arena**: Pooled memory management
- **Marshallers**: Convert between managed and native representations

#### Layer 4: Native Interop
- **P/Invoke Declarations**: Safe wrappers around Cyclone C API
- **Allocator Integration**: Custom allocator hooks
- **Loan Management**: Zero-copy read support

---

## 3. Component Design

### 3.1 Deliverables

1. **CycloneDDS.Schema** (NuGet Package)
   - Attribute definitions
   - Wrapper types (FixedString32, BoundedSeq<T,N>, DdsUnion<...>)
   - Schema validation analyzers

2. **CycloneDDS.Generator** (NuGet Package - Source Generator)
   - Roslyn source generator
   - IDL emitter
   - C# code emitter (TNative, TManaged, marshallers, metadata)
   - Schema evolution validator

3. **CycloneDDS.Runtime** (NuGet Package)
   - DdsParticipant, DdsReader, DdsWriter
   - Arena, TakeScope
   - Error handling (DdsException, status codes)
   - P/Invoke declarations

4. **CycloneDDS.NativeShim** (Native Library - C)
   - Allocator integration hooks
   - Helper functions for efficient interop
   - Links against Cyclone DDS

5. **Build Integration**
   - MSBuild targets
   - Schema → IDL → idlc → native compilation

### 3.2 Module Dependencies

```
User Code
   │
   ├──> CycloneDDS.Schema (attributes + helpers)
   │
   └──> Generated Code (depends on Schema + Runtime)
           │
           └──> CycloneDDS.Runtime
                   │
                   └──> CycloneDDS.NativeShim (P/Invoke)
                           │
                           └──> Cyclone DDS (native lib)
```

---

## 4. Schema DSL Design

### 4.1 Design Philosophy

- **Infer everything possible** from C# type system
- **Attributes only where C# syntax is ambiguous** or for metadata
- **Use wrapper types** for bounded/specialized data (FixedString32, BoundedSeq<T,N>)
- **Make `@appendable` universal and implicit**

### 4.2 Type Mapping Rules

| C# Schema Type | IDL Emission | Native Representation | Notes |
|----------------|--------------|----------------------|-------|
| `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double` | Corresponding IDL primitive | Inline primitive | Direct mapping |
| `bool` | `boolean` | `byte` | Native uses 1 byte for determinism |
| `enum MyEnum : int` | `enum MyEnum : long` | `int` | Enum underlying type |
| `string` | `string` (unbounded) | `byte* + int len` | Arena-backed on read/write |
| `FixedString32` | `octet[32]` | `fixed byte[32]` | UTF-8 NUL-padded inline |
| `T[]` | `sequence<T>` (unbounded) | `T* + int len` | Arena-backed |
| `BoundedSeq<T, N>` | `sequence<T, N>` | Bounded storage (TBD) | Max capacity N |
| `NestedType` | `NestedType` struct | Inline struct | Nested schema type |
| `NestedType?` | `@optional NestedType` | Presence + value | XTypes optional |
| `Guid` | `octet[16]` (typedef Guid16) | `fixed byte[16]` | Global type map |
| `DateTime` | `int64 ticksUtc` | `long` | Global type map |
| `Numeric.Quaternion` | `QuaternionF32x4` struct | `float x,y,z,w` | Global type map |

### 4.3 Global Type Map Registry

Defined once per assembly via assembly-level attributes:

```csharp
[assembly: DdsTypeMap(typeof(Guid), DdsWire.Guid16)]
[assembly: DdsTypeMap(typeof(DateTime), DdsWire.Int64TicksUtc)]
[assembly: DdsTypeMap(typeof(Numeric.Quaternion), DdsWire.QuaternionF32x4)]
[assembly: DdsTypeMap(typeof(FixedString32), DdsWire.FixedUtf8Bytes32)]
```

**Built-in Wire Kinds:**
- `DdsWire.Guid16`: `octet[16]` (RFC4122 byte order)
- `DdsWire.Int64TicksUtc`: `int64` (UTC ticks since epoch)
- `DdsWire.QuaternionF32x4`: `struct { float x,y,z,w }`
- `DdsWire.FixedUtf8BytesN`: `octet[N]` (NUL-padded UTF-8)

### 4.4 Required Attributes

#### Type-Level Attributes

**`[DdsTopic(string topicName)]`** (Required)
- Specifies the DDS topic name for this type
- Example: `[DdsTopic("PoseUpdate")]`

**`[DdsQos(...)]`** (Required)
- Specifies default QoS settings for this topic
- Properties:
  - `Reliability`: `DdsReliability.Reliable` or `BestEffort`
  - `Durability`: `DdsDurability.Volatile`, `TransientLocal`, `Transient`, `Persistent`
  - `HistoryKind`: `DdsHistoryKind.KeepLast` or `KeepAll`
  - `HistoryDepth`: `int` (only for KeepLast)

**`[DdsTypeName(string idlTypeName)]`** (Optional)
- Override the IDL type name (default: C# type name)

#### Field-Level Attributes

**`[DdsKey]`** (Keyed topics only)
- Marks a field as part of the instance key
- Multiple fields can be keys (composite key)

**`[DdsBound(int max)]`** (Optional alternative to wrapper types)
- Specify max bound for strings/sequences
- Wrapper types (`FixedString32`, `BoundedSeq<T,N>`) are preferred

**`[DdsId(int id)]`** (Optional, future-proof)
- Explicit member ID for additional evolution safety
- If used, must remain stable across schema versions

#### Union-Specific Attributes

**`[DdsUnion]`** (Type-level)
- Marks the type as a DDS union

**`[DdsDiscriminator]`** (Field-level)
- Marks the discriminator field (exactly one required)
- Discriminator must be `enum` or integral type

**`[DdsCase(discriminatorValue)]`** (Field-level)
- Marks a union arm with its corresponding discriminator value
- Value must match discriminator type

**`[DdsDefaultCase]`** (Field-level, optional)
- Marks the default union arm

### 4.5 Schema Example

```csharp
using System;
using Bagira.CycloneDDS.Schema;

// Global type mappings
[assembly: DdsTypeMap(typeof(Guid), DdsWire.Guid16)]
[assembly: DdsTypeMap(typeof(DateTime), DdsWire.Int64TicksUtc)]
[assembly: DdsTypeMap(typeof(Numeric.Quaternion), DdsWire.QuaternionF32x4)]

namespace MyApp.Topics
{
    // Union example
    public enum CommandKind : int
    {
        None = 0,
        Move = 1,
        Spawn = 2,
    }

    [DdsUnion]
    public partial class Command
    {
        [DdsDiscriminator]
        public CommandKind Kind;

        [DdsCase(CommandKind.Move)]
        public MoveCommand Move;

        [DdsCase(CommandKind.Spawn)]
        public SpawnCommand Spawn;
    }

    public partial class MoveCommand
    {
        public float Dx, Dy, Dz;
    }

    public partial class SpawnCommand
    {
        public Guid PrefabId;  // Mapped via global type map
        public float Px, Py, Pz;
    }

    // Optional nested type
    public partial class EntityMeta
    {
        public int Team;
        public string Name;  // Unbounded (inferred)
    }

    // Main topic
    [DdsTopic("PoseUpdate")]
    [DdsQos(
        Reliability = DdsReliability.Reliable,
        Durability = DdsDurability.Volatile,
        HistoryKind = DdsHistoryKind.KeepLast,
        HistoryDepth = 8)]
    public partial class PoseUpdate
    {
        [DdsKey]
        public Guid EntityId;  // Key field

        public DateTime TimestampUtc;  // Mapped to ticks
        public Numeric.Quaternion Rotation;  // Mapped to float4

        public float X, Y, Z;

        public FixedString32 Frame;  // Bounded inline string

        public EntityMeta? Meta;  // Optional (nullable => @optional)

        public string DebugLabel;  // Unbounded string
        public float[] BoneWeights;  // Unbounded sequence

        public Command Cmd;  // Union
    }
}
```

**What's Inferred:**
- `@appendable` on all types (implicit, mandatory)
- `string` → unbounded IDL `string`
- `float[]` → unbounded `sequence<float>`
- `EntityMeta?` → `@optional EntityMeta`
- `Guid`, `DateTime`, `Quaternion` → mapped via global registry
- `FixedString32` → `octet[32]`

---

## 5. Code Generation Pipeline

### 5.1 Roslyn Source Generator Flow

```
Input: C# Schema Types
   │
   ├─> Phase 1: Discovery
   │   - Find types with [DdsTopic]
   │   - Find types with [DdsUnion]
   │   - Find global type mappings
   │
   ├─> Phase 2: Validation
   │   - Check appendable evolution rules
   │   - Validate union discriminator uniqueness
   │   - Validate type mapping consistency
   │   - Compute schema fingerprint
   │
   ├─> Phase 3: IDL Generation
   │   - Emit @appendable module
   │   - Emit typedefs for custom types
   │   - Emit enums, structs, unions
   │   - Apply @key, @optional annotations
   │
   ├─> Phase 4: Native Type Generation
   │   - Emit TNative structs (blittable layouts)
   │   - Compute alignment and padding
   │   - Emit union explicit layouts
   │   - Emit fixed buffers for bounded types
   │
   ├─> Phase 5: Managed Type Generation
   │   - Emit TManaged ref structs (views)
   │   - Emit ReadOnlySpan<byte> for strings
   │   - Emit ReadOnlySpan<T> for sequences
   │   - Emit optional wrapper structs
   │   - Emit union view structs
   │
   ├─> Phase 6: Marshaller Generation
   │   - Emit ToNative(managed, native, arena)
   │   - Emit ToManaged(native, arena) → managed view
   │   - Handle UTF-8 encoding/decoding
   │   - Handle optional presence
   │   - Handle union active arm switching
   │
   └─> Phase 7: Metadata Registry
       - Emit topic name → type mapping
       - Emit QoS defaults
       - Emit key field indices
```

### 5.2 Generated Code Structure

For each schema topic type `FooSchema`:

```
Generated/
├── Foo.idl                         # IDL for Cyclone idlc
├── FooNative.g.cs                  # TNative struct
├── FooManaged.g.cs                 # TManaged ref struct view
├── FooMarshaller.g.cs              # Marshalling logic
└── TopicMetadata.g.cs              # Registry entry
```

### 5.3 Alignment and Padding Calculation

**Critical Implementation Detail (from design talk §2279-2301):**

The generator must implement **C-compatible alignment rules** for unions:

```csharp
// Alignment calculation logic
int discriminatorSize = sizeof(TDiscriminator);
int maxArmAlignment = 1;

foreach (var arm in unionArms)
{
    int armAlign = GetAlignment(arm.Type);  // Recursive for nested structs
    if (armAlign > maxArmAlignment)
        maxArmAlignment = armAlign;
}

// Standard C padding rule
int payloadOffset = (discriminatorSize + (maxArmAlignment - 1)) & ~(maxArmAlignment - 1);

// Emit:
// [FieldOffset(0)] public TDiscriminator Kind;
// [FieldOffset(payloadOffset)] public TArmNative ArmField;
```

**Alignment of primitive types:**
- `byte`, `sbyte`: 1
- `short`, `ushort`: 2
- `int`, `uint`, `float`: 4
- `long`, `ulong`, `double`: 8
- Structs: max alignment of any field

### 5.4 Schema Evolution Validation

**Appendable Rules (enforced by generator):**
1. New members may only be **added at the end**
2. **No reordering** of existing members
3. **No removal** of existing members
4. **No type changes** for existing members
5. Optional members may be added, but still appended

**Implementation:**
- Generate schema fingerprint (hash of member names + types + order)
- Store in `obj/` directory during build
- Compare against previous build
- **Fail build** if breaking change detected
- Provide detailed diff in build output

---

## 6. Runtime Components

### 6.1 DdsParticipant

```csharp
namespace Bagira.CycloneDDS.Runtime
{
    public sealed class DdsParticipant : IDisposable
    {
        public int DomainId { get; }
        public string[] Partitions { get; }

        public DdsParticipant(int domainId, params string[] partitions);

        public void Dispose();

        // Internal
        internal IntPtr NativeHandle { get; }
    }
}
```

**Responsibilities:**
- Create Cyclone domain participant via `dds_create_participant`
- Store partition configuration for reader/writer creation
- Dispose native handle deterministically

### 6.2 DdsWriter<TNative> (Inline-Only)

```csharp
namespace Bagira.CycloneDDS.Runtime
{
    public sealed class DdsWriter<TNative> : IDisposable
        where TNative : unmanaged
    {
        public DdsWriter(DdsParticipant participant);

        public void Write(in TNative sample);
        public void WriteDispose(in TNative sample);
        public bool TryWrite(in TNative sample, out DdsReturnCode status);

        public void Dispose();
    }
}
```

**Responsibilities:**
- Auto-discover topic name, QoS from generated metadata registry
- Create DDS publisher, topic, writer
- Call `dds_write` or `dds_writedispose`
- Handle error mapping

### 6.3 DdsReader<TNative> (Inline-Only)

```csharp
namespace Bagira.CycloneDDS.Runtime
{
    public sealed class DdsReader<TNative> : IDisposable
        where TNative : unmanaged
    {
        public DdsReader(DdsParticipant participant);

        public int Take(Span<TNative> samples, Span<DdsSampleInfo> infos);
        public int Read(Span<TNative> samples, Span<DdsSampleInfo> infos);
        public bool TryTake(Span<TNative> samples, Span<DdsSampleInfo> infos, out int count);

        public void Dispose();
    }
}
```

**Responsibilities:**
- Auto-discover topic name, QoS from metadata
- Create DDS subscriber, topic, reader
- Call `dds_take` or `dds_read`
- Copy samples into caller-provided spans
- Return loan if used

### 6.4 DdsWriter<TManaged, TNative, TMarshaller> (Variable-Size Capable)

```csharp
namespace Bagira.CycloneDDS.Runtime
{
    public sealed class DdsWriter<TManaged, TNative, TMarshaller> : IDisposable
        where TNative : unmanaged
        where TMarshaller : IMarshaller<TManaged, TNative>, new()
    {
        public DdsWriter(DdsParticipant participant);

        public void Write(in TManaged sample, Arena arena);
        public void WriteDispose(in TManaged sample, Arena arena);

        public void Dispose();
    }
}
```

**Marshaller Interface:**
```csharp
public interface IMarshaller<TManaged, TNative>
    where TNative : unmanaged
{
    void ToNative(in TManaged managed, out TNative native, Arena arena);
    TManaged ToManaged(in TNative native, Arena arena);
}
```

### 6.5 DdsReader<TManaged, TNative, TMarshaller> + TakeScope

```csharp
namespace Bagira.CycloneDDS.Runtime
{
    public sealed class DdsReader<TManaged, TNative, TMarshaller> : IDisposable
        where TNative : unmanaged
        where TMarshaller : IMarshaller<TManaged, TNative>, new()
    {
        public DdsReader(DdsParticipant participant);

        public TakeScope<TManaged> Take(Arena arena, int maxSamples);

        public void Dispose();
    }

    public ref struct TakeScope<TManaged>
    {
        public ReadOnlySpan<TManaged> Samples { get; }
        public ReadOnlySpan<DdsSampleInfo> Infos { get; }

        public void Dispose();
        
        // Internal: holds DDS loan handle, arena slice
    }
}
```

**TakeScope Responsibilities:**
- Wrap `dds_take` loan
- Construct managed views over native samples
- Dispose returns loan via `dds_return_loan`
- Reset arena slice (if arena is scope-local)

### 6.6 DdsSampleInfo

```csharp
namespace Bagira.CycloneDDS.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DdsSampleInfo
    {
        public DdsSampleState SampleState;
        public DdsViewState ViewState;
        public DdsInstanceState InstanceState;
        public bool ValidData;
        public long SourceTimestamp;  // nanoseconds
        public Guid InstanceHandle;  // TBD: or IntPtr/ulong
        public Guid PublicationHandle;
        public int DisposedGenerationCount;
        public int NoWritersGenerationCount;
        public int SampleRank;
        public int GenerationRank;
        public int AbsoluteGenerationRank;
    }
}
```

---

## 7. Memory Management

### 7.1 Arena Design

**Purpose:** Provide pooled, reusable memory for variable-size data without GC pressure.

```csharp
namespace Bagira.CycloneDDS.Runtime
{
    public sealed class Arena : IDisposable
    {
        public Arena(int initialBytes = 64 * 1024);

        public unsafe byte* Alloc(int bytes, int alignment);
        public unsafe Span<byte> AllocSpan(int bytes, int alignment);

        public void Reset();
        public void Dispose();

        // Configuration
        public int Capacity { get; }
        public int Used { get; }
        public int MaxRetainedCapacity { get; set; }  // Trim on reset if exceeded
    }
}
```

**Implementation Details:**

1. **Storage:** Linked list of native memory blocks
2. **Allocation:** Bump-pointer allocation within current block
3. **Growth:** Geometric growth (2x) when block exhausted
4. **Reset:** Rewind all blocks to start (O(1) operation)
5. **Trim:** On reset, if `Capacity > MaxRetainedCapacity`, free excess blocks

**Thread Safety:** Arenas are **not thread-safe**. Use:
- One arena per thread (thread-local)
- Or pass arena explicitly and ensure single-threaded access

**Lifetime Rules:**
- TakeScope borrows arena for its lifetime
- On TakeScope.Dispose, arena is reset (or marked for reset)
- Managed views (ref structs) are only valid within TakeScope lifetime

### 7.2 Zero-Copy Read Strategy

For **inline-only types** (no variable-size fields):

```
Cyclone DDS Internal Buffer (loaned)
   │
   ├─> dds_take returns pointer to buffer
   │
   └─> Cast to Span<TNative>
       - Zero allocations
       - Zero copies (read-only access)
       - Valid until dds_return_loan
```

For **variable-size types**:

```
Cyclone DDS Internal Buffer (loaned)
   │
   ├─> dds_take returns pointer to buffer
   │
   ├─> Fixed fields: Direct access
   │
   └─> Variable fields (strings/sequences):
       - Pointers point into Cyclone's buffer
       - OR copied into Arena if transformation needed
       - Managed view wraps as ReadOnlySpan
       - Valid until TakeScope.Dispose
```

### 7.3 Cyclone Allocator Integration

**Optional but Recommended:**

Cyclone allows overriding its internal allocator via `ddsrt_set_allocator` (from design talk §574-585).

**Native Shim Initialization:**

```c
// In native shim library
#include "dds/ddsrt/heap.h"

// Custom allocator (e.g., mimalloc)
static void* custom_malloc(size_t size) { return mi_malloc(size); }
static void* custom_calloc(size_t count, size_t size) { return mi_calloc(count, size); }
static void* custom_realloc(void* ptr, size_t size) { return mi_realloc(ptr, size); }
static void custom_free(void* ptr) { mi_free(ptr); }

void fcdc_configure_allocator()
{
    ddsrt_heap_allocator_t alloc = {
        .malloc = custom_malloc,
        .calloc = custom_calloc,
        .realloc = custom_realloc,
        .free = custom_free
    };
    ddsrt_set_allocator(&alloc);
}
```

**Benefits:**
- Reduced fragmentation
- Better performance (mimalloc/jemalloc)
- Deterministic behavior for real-time systems

---

## 8. Type System

### 8.1 Three-Type Model

For each schema type, generate:

1. **Schema Type** (user-authored)
   - C# partial type with attributes
   - Used only as input to generator
   - May be used at runtime if convenient, but not required

2. **Native Type** (`TNative`, generated)
   - Blittable, `unmanaged` struct
   - Matches Cyclone C layout exactly
   - Uses fixed buffers, pointers, explicit layout
   - **Used directly by DDS P/Invoke calls**

3. **Managed Type** (`TManaged`, generated, optional)
   - `readonly ref struct` (stack-only, view)
   - Exposes ergonomic .NET types (Guid, DateTime, ReadOnlySpan<byte>)
   - **No heap allocations** (ref struct constraint)
   - Valid only within TakeScope/Arena lifetime

### 8.2 Fixed Buffers for Bounded Data

**FixedString32 Example:**

```csharp
// Generated native type
[StructLayout(LayoutKind.Sequential)]
public unsafe struct FixedString32Native
{
    public fixed byte Bytes[32];  // UTF-8 NUL-padded
}

// User-facing wrapper (in Schema package)
public readonly struct FixedString32
{
    private readonly byte[] _bytes;  // Or stackalloc in some methods

    public static bool TryFrom(string value, out FixedString32 result);
    public ReadOnlySpan<byte> AsUtf8Span();
    public string ToStringAllocated();  // Explicit allocation
}
```

**Encoding Rules:**
- UTF-8 encoding
- NUL-padding to full size
- **Reject at marshal time** if string exceeds max bytes (after UTF-8 encoding)
- Validate UTF-8 correctness in debug builds

### 8.3 Unbounded Data Representation

**Unbounded String (Native):**

```csharp
[StructLayout(LayoutKind.Sequential)]
public unsafe struct Utf8StringRef
{
    public byte* Ptr;
    public int ByteLen;
}
```

**Unbounded Sequence (Native):**

```csharp
[StructLayout(LayoutKind.Sequential)]
public unsafe struct SeqFloat
{
    public float* Ptr;
    public int Len;
}
```

**Managed View:**

```csharp
public readonly ref struct PoseUpdateView
{
    // Fixed fields
    public readonly Guid EntityId;
    public readonly float X, Y, Z;

    // Unbounded fields (views into arena/loan)
    public readonly ReadOnlySpan<byte> DebugLabelUtf8;
    public readonly ReadOnlySpan<float> BoneWeights;
}
```

---

## 9. Union Support

### 9.1 Union Schema Expression

**Explicit, One Arm Per Line:**

```csharp
[DdsUnion]
public partial class Command
{
    [DdsDiscriminator]
    public CommandKind Kind;

    [DdsCase(CommandKind.Move)]
    public MoveCommand Move;

    [DdsCase(CommandKind.Spawn)]
    public SpawnCommand Spawn;

    [DdsDefaultCase]
    public NoopCommand Default;
}
```

**Constraints:**
- Exactly one `[DdsDiscriminator]` field
- Discriminator type: `enum` or integral
- Each discriminator value used at most once
- At most one `[DdsDefaultCase]`

### 9.2 Generated IDL

```idl
@appendable enum CommandKind : long
{
    None = 0,
    Move = 1,
    Spawn = 2
};

@appendable union Command switch(CommandKind)
{
    case Move: MoveCommand move;
    case Spawn: SpawnCommand spawn;
    default: NoopCommand @default;
};
```

### 9.3 Generated Native Layout

**Explicit Layout with Correct Alignment:**

```csharp
[StructLayout(LayoutKind.Explicit)]
public unsafe struct CommandNative
{
    [FieldOffset(0)]
    public CommandKind Kind;

    // Payload offset calculated based on max alignment of arms
    // NOT hardcoded to 4! (see §5.3 Alignment Calculation)
    [FieldOffset(8)]  // Example: if max arm needs 8-byte alignment
    public MoveCommandNative Move;

    [FieldOffset(8)]
    public SpawnCommandNative Spawn;

    [FieldOffset(8)]
    public NoopCommandNative Default;
}
```

**Safe Accessors (Generated):**

```csharp
public static ref MoveCommandNative GetMove(ref CommandNative cmd)
{
    if (cmd.Kind != CommandKind.Move)
        throw new InvalidOperationException("Union arm mismatch");
    return ref cmd.Move;
}
```

### 9.4 Managed Union View

```csharp
public readonly ref struct CommandView
{
    private readonly ref CommandNative _native;

    public CommandKind Kind => _native.Kind;

    public ref readonly MoveCommand GetMove()
    {
        if (_native.Kind != CommandKind.Move)
            throw new InvalidOperationException("Union arm mismatch");
        return ref Unsafe.AsRef(in _native.Move);
    }

    public ref readonly SpawnCommand GetSpawn() { /* ... */ }
}
```

### 9.5 Variable-Size Union Arms

**Allowed:** Union arms may contain unbounded strings/sequences.

**Implication:**
- Arm native type includes pointer+length members
- Lifetime bound to TakeScope/Arena
- Marshaller ensures arena allocation for active arm

---

## 10. Optional Members

### 10.1 Semantics

**C# Schema:**
```csharp
public EntityMeta? Meta;  // Nullable => @optional
```

**IDL:**
```idl
@optional EntityMeta meta;
```

**Meaning:**
- Field may be absent ("null reference")
- XTypes presence semantics
- Compatible with XCDR2 (Cyclone-only assumption)

### 10.2 Native Representation

**Option A: Presence + Inline Value (for fixed-size optionals)**

```csharp
[StructLayout(LayoutKind.Sequential)]
public unsafe struct OptionalEntityMeta
{
    public byte HasValue;  // 0 or 1
    // Padding if needed for alignment
    public EntityMetaNative Value;
}
```

**Option B: Presence + Pointer (for variable-size optionals)**

```csharp
[StructLayout(LayoutKind.Sequential)]
public unsafe struct OptionalEntityMeta
{
    public byte HasValue;
    public EntityMetaNative* ValuePtr;  // Arena-owned if present
}
```

**Generator Chooses:** Based on whether `EntityMeta` contains variable-size fields.

### 10.3 Managed View

```csharp
public readonly ref struct PoseUpdateView
{
    public readonly EntityMetaView? Meta;  // Nullable ref struct
}

public readonly ref struct EntityMetaView
{
    public readonly int Team;
    public readonly ReadOnlySpan<byte> NameUtf8;
}
```

**Lifecycle:** Optional values are valid only within TakeScope lifetime.

---

## 11. Native Interop Layer

### 11.1 P/Invoke Strategy

**Wrapper around Cyclone C API:**

```csharp
namespace Bagira.CycloneDDS.Runtime.Interop
{
    internal static class CycloneNative
    {
        private const string DLL = "ddsc";

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr dds_create_participant(
            int domain_id,
            IntPtr qos,
            IntPtr listener);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int dds_write(
            IntPtr writer,
            IntPtr sample);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int dds_writedispose(
            IntPtr writer,
            IntPtr sample);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int dds_take(
            IntPtr reader,
            void** samples,
            IntPtr* infos,
            int max_samples,
            int sample_mask);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int dds_return_loan(
            IntPtr reader,
            IntPtr* samples,
            int count);

        // ... more functions
    }
}
```

**Handle Wrapping:**
- All Cyclone handles (`dds_entity_t`) as `IntPtr`
- Wrapped in safe disposable classes (DdsParticipant, DdsWriter, DdsReader)
- Deterministic disposal via `IDisposable`

### 11.2 Error Handling

**Return Code Mapping:**

```csharp
public enum DdsReturnCode
{
    Ok = 0,
    Error = -1,
    Unsupported = -2,
    BadParameter = -3,
    PreconditionNotMet = -4,
    OutOfResources = -5,
    NotEnabled = -6,
    ImmutablePolicy = -7,
    InconsistentPolicy = -8,
    AlreadyDeleted = -9,
    Timeout = -10,
    NoData = -11,
    // ... more
}

public class DdsException : Exception
{
    public DdsReturnCode ErrorCode { get; }
    public DdsException(DdsReturnCode code, string message)
        : base($"DDS Error {code}: {message}") { }
}
```

**Error Strategy:**
- Programmer errors (BadParameter, etc.) → throw `DdsException`
- Expected conditions (NoData, Timeout) → return status code via `Try*` methods
- Hot path methods have `Try*` variants that don't throw

### 11.3 Loan Management

**TakeScope Internal Implementation:**

```csharp
public ref struct TakeScope<TManaged>
{
    private IntPtr _reader;
    private unsafe void** _samplePtrs;
    private IntPtr* _infoPtrs;
    private int _count;
    private Arena _arena;
    private TManaged[] _managedViews;  // Stackalloc'd in real impl

    public ReadOnlySpan<TManaged> Samples => _managedViews.AsSpan(0, _count);

    public void Dispose()
    {
        if (_samplePtrs != null)
        {
            CycloneNative.dds_return_loan(_reader, _samplePtrs, _count);
            _samplePtrs = null;
        }
        _arena?.Reset();
    }
}
```

---

## 12. Testing Strategy

### 12.1 Unit Tests

**Schema Generator Tests:**
- Correct IDL emission for primitives, strings, sequences, unions, optional
- Alignment calculation correctness
- Schema evolution validation (detect breaking changes)
- Error reporting for invalid schemas

**Marshaller Tests:**
- Round-trip managed → native → managed
- UTF-8 encoding/decoding
- Bounded string truncation/rejection
- Optional presence/absence
- Union arm switching

**Arena Tests:**
- Allocation and reset
- Growth behavior
- Trim policy
- Thread-local usage

### 12.2 Integration Tests

**End-to-End:**
- Writer → Reader for inline-only types
- Writer → Reader for variable-size types
- Disposal samples and instance lifecycle
- Multiple participants, partitions

**Evolution Tests:**
- Reader v1 ← Writer v2 (new appended fields ignored)
- Reader v2 ← Writer v1 (missing fields get defaults)
- Assert no crashes, no garbage data

**Performance Tests:**
- Zero allocations in steady state (GC.GetTotalMemory)
- Loaning works correctly (no copies)
- Throughput benchmarks

### 12.3 Fuzz and Stress Tests

- Randomized variable-size payloads
- Long-running soak tests (leak detection)
- Large payloads (stress arena growth)
- Malformed data handling (robustness)

---

## 13. Performance Requirements

### 13.1 Inline-Only Types

- **Take/Read:** Zero GC allocations, prefer loaning (zero-copy)
- **Write:** Zero GC allocations
- **Latency:** <1μs overhead vs. raw C API

### 13.2 Variable-Size Types

- **Steady-State:** Zero GC allocations after arena warm-up
- **Arena Growth:** Only when encountering larger-than-ever samples
- **Pinning:** Minimal (use native arena memory, not pinned managed arrays)

### 13.3 No Runtime Reflection

- All type information baked into generated code
- No `typeof()` or `Activator.CreateInstance()` in hot paths
- Metadata registry is generated code, not reflection

---

## 14. Build Integration

### 14.1 MSBuild Targets

**Pipeline:**
1. Roslyn generator runs during compilation
2. Generates `.idl` files and C# code
3. MSBuild target detects `.idl` changes
4. Runs `idlc` (Cyclone IDL compiler)
5. Compiles native shim library
6. Copies native shim to output directory

**MSBuild Target (Conceptual):**

```xml
<Target Name="GenerateIDL" BeforeTargets="CoreCompile">
  <Exec Command="dotnet tool run fcdc-idlc --input Generated/*.idl --output Generated/Cyclone" />
</Target>

<Target Name="CompileNativeShim" AfterTargets="GenerateIDL">
  <Exec Command="cmake --build NativeShim/build --config $(Configuration)" />
  <Copy SourceFiles="NativeShim/build/fcdc_shim.dll" DestinationFolder="$(OutputPath)" />
</Target>
```

### 14.2 Developer Experience

**Workflow:**
1. Write schema types in C#
2. Build project → generator runs automatically
3. IDL and native types generated
4. Use DdsWriter/DdsReader in application code
5. Build again if schema changes (evolution validated)

**Diagnostics:**
- Generator emits warnings for potential issues
- Evolution validator shows diffs for breaking changes
- Build fails fast if schema rules violated

---

## End of Detailed Design Document

This document serves as the comprehensive reference for implementing FastCycloneDDS C# Bindings. See individual task files for breakdown of implementation work.
