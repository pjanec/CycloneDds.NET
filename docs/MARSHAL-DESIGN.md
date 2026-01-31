# Native Marshaling Design (Object-Centric Architecture)

**Project:** FastCycloneDDS C# Bindings  
**Architectural Approach:** Arena/Native Marshaling (Object-Centric)  
**Last Updated:** 2026-01-31

---

## 1. Executive Summary

### 1.1 The Paradigm Shift

We are transitioning from **Protocol-Centric** (Direct CDR serialization in C#) to **Object-Centric** (Native C-struct marshaling with Cyclone DDS handling CDR).

**Current Approach (CDR-based):**
- C# serializers generate CDR bytes directly
- Complex XCDR1/XCDR2 logic in C# code
- Fragile alignment and DHEADER management
- Interoperability risks with native implementations

**New Approach (Native Marshaling):**
- C# marshals data into native C-struct layout
- Cyclone DDS handles all CDR serialization
- 100% guaranteed wire format compatibility
- Simpler, more robust code generation

### 1.2 Core Benefits

| Aspect | Current (CDR) | New (Native) | Impact |
|--------|--------------|--------------|--------|
| **Compliance** | Manual XCDR2 implementation | Cyclone DDS native serializer | 100% guaranteed compatibility |
| **Complexity** | High (protocol emulation) | Medium (memory layout) | Simpler code generation |
| **XCDR1/2 Support** | Manual switching logic | Cyclone handles automatically | Zero maintenance burden |
| **Performance** | Direct to network buffer | One extra memcpy (negligible) | ~95-98% of pure C speed |
| **Interop Risk** | High (one bug breaks wire format) | Zero (delegates to proven library) | Production-ready |

### 1.3 What Remains the Same

- **User DSL:** `[DdsTopic]`, `[DdsKey]`, attributes unchanged
- **Build Pipeline:** Roslyn code generation, `idlc` integration
- **API Surface:** `DdsWriter<T>.Write()`, `DdsReader<T>.Read()`
- **Zero-Allocation Goal:** ArrayPool, ref structs, Span-based

---

## 2. Architectural Overview

### 2.1 The "Head & Tail" Memory Model

We view a DDS sample as a contiguous memory block divided into two regions:

```
[ MEMORY BLOCK (ArrayPool) -------------------------------------------- ]
[ HEAD (Fixed Root Struct)    ] [ GAP (Alignment) ] [ TAIL (Dynamic) ]
[ int Id | char* MsgPtr ---]---[-------------------]->[ "Hello\0" ]
[ IntPtr DataPtr ------]---[---]---------------------->[ double[] data ]
             ^                                              ^
             |                                              |
             +--- Points to addresses in TAIL section -----+
```

**HEAD:** The root C-struct with primitives and pointers (fixed size, known at compile-time via `sizeof(T_Native)`).

**TAIL:** Variable-length data (strings, sequences) that grows from the end of HEAD forward.

### 2.2 Component Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         USER CODE (DSL)                         │
│   [DdsTopic] public struct CameraImage { int Id; string Name; } │
└─────────────────────────────────────────┬───────────────────────┘
                                          │
                 ┌────────────────────────┴────────────────────────┐
                 │      CODE GENERATION (Roslyn)                   │
                 │  ┌──────────────────────────────────────────┐   │
                 │  │ Ghost Struct Generation                  │   │
                 │  │ internal struct CameraImage_Native {     │   │
                 │  │   int id; IntPtr name;                   │   │
                 │  │ }                                        │   │
                 │  └──────────────────────────────────────────┘   │
                 │  ┌──────────────────────────────────────────┐   │
                 │  │ Marshaller Generation                    │   │
                 │  │ static void MarshalToNative(             │   │
                 │  │   in CameraImage src,                    │   │
                 │  │   ref CameraImage_Native dst,            │   │
                 │  │   ref NativeArena arena)                 │   │
                 │  └──────────────────────────────────────────┘   │
                 │  ┌──────────────────────────────────────────┐   │
                 │  │ View Struct Generation                   │   │
                 │  │ public ref struct CameraImageView {      │   │
                 │  │   unsafe CameraImage_Native* _ptr;       │   │
                 │  │   public int Id => _ptr->id;             │   │
                 │  │   public string Name => ...;             │   │
                 │  │ }                                        │   │
                 │  └──────────────────────────────────────────┘   │
                 └───────────────────────┬──────────────────────────┘
                                         │
        ┌────────────────────────────────┴─────────────────────────────┐
        │                    RUNTIME (DdsWriter/Reader)                │
        │  ┌──────────────────────────────────────────────────────┐    │
        │  │ WRITE PATH                                           │    │
        │  │ 1. Calculate size: GetNativeSize(sample)             │    │
        │  │ 2. Rent buffer: ArrayPool.Rent()                     │    │
        │  │ 3. Pin: fixed (byte* ptr = buffer)                   │    │
        │  │ 4. Initialize: NativeArena(buffer, ptr, headSize)    │    │
        │  │ 5. Marshal: MarshalToNative(sample, ref head, arena) │    │
        │  │ 6. Send: dds_write(ptr)                              │    │
        │  │ 7. Return: ArrayPool.Return(buffer)                  │    │
        │  └──────────────────────────────────────────────────────┘    │
        │  ┌──────────────────────────────────────────────────────┐    │
        │  │ READ PATH                                            │    │
        │  │ 1. Call: dds_take(reader, samples[], infos[])        │    │
        │  │ 2. Wrap: DdsLoan(samples, infos, count)              │    │
        │  │ 3. Iterate: foreach (sample in loan)                 │    │
        │  │    - Cast: new View((T_Native*)sample.DataPtr)       │    │
        │  │    - Access: view.Id, view.Name (zero-copy)          │    │
        │  │ 4. Dispose: dds_return_loan(samples)                 │    │
        │  └──────────────────────────────────────────────────────┘    │
        └──────────────────────────────────────────────────────────────┘
                                         │
        ┌────────────────────────────────┴─────────────────────────────┐
        │               CYCLONE DDS (Native Library)                   │
        │  dds_write() → Reads C-struct → Serializes to CDR → Network │
        │  Network → Deserializes CDR → dds_take() returns C-struct   │
        └──────────────────────────────────────────────────────────────┘
```

---

## 3. Core Components

### 3.1 Native Arena (Runtime)

**File:** `src/CycloneDDS.Core/NativeArena.cs`

**Purpose:** Manages the "Tail" region of the memory block, allocating space for variable-length data.

**Key Responsibilities:**
- String allocation (UTF-8 encoding + null terminator)
- Sequence buffer allocation (with alignment)
- Struct array allocation (for complex sequences)

**Design Principles:**
- `ref struct` to prevent escape from `fixed` block
- Zero-out allocated memory to prevent information leaks
- Alignment-aware (8-byte alignment for doubles, etc.)

**Example API:**
```csharp
public ref struct NativeArena
{
    public NativeArena(Span<byte> buffer, IntPtr baseAddress, int headSize);
    public IntPtr CreateString(string? text);
    public DdsSequenceNative CreateSequence<T>(ReadOnlySpan<T> data) where T : unmanaged;
    public Span<TNative> AllocateArray<TNative>(int count) where TNative : unmanaged;
}
```

### 3.2 Ghost Structs (Generated)

**Purpose:** C#-side representation of native C structs with exact memory layout matching.

**Attributes:**
- `[StructLayout(LayoutKind.Sequential)]` for automatic padding
- Field types: `IntPtr` for pointers, `byte` for `bool`, primitives for primitives

**Example:**
```csharp
// User DSL
[DdsTopic("Sensor")]
public struct Sensor {
    public int Id;
    public string Name;
    public List<double> Data;
}

// Generated Ghost Struct
[StructLayout(LayoutKind.Sequential)]
internal struct Sensor_Native {
    public int id;                // 4 bytes at offset 0
    // (4 bytes padding on x64)
    public IntPtr name;           // 8 bytes at offset 8 (x64)
    public DdsSequenceNative data; // 24 bytes at offset 16
}
```

### 3.3 Marshallers (Generated)

**Purpose:** Transform managed C# objects into native C-struct layouts.

**Generated Methods:**

#### 3.3.1 GetNativeSize
Calculates total buffer size needed (Head + Tail).

```csharp
public static int GetNativeSize(in Sensor source)
{
    int size = Unsafe.SizeOf<Sensor_Native>();
    
    // Strings
    if (source.Name != null)
        size += Encoding.UTF8.GetByteCount(source.Name) + 1;
    
    // Sequences
    if (source.Data != null && source.Data.Count > 0)
    {
        size = (size + 7) & ~7; // Align to 8
        size += source.Data.Count * sizeof(double);
    }
    
    return size;
}
```

#### 3.3.2 MarshalToNative
Populates the ghost struct and arena.

```csharp
internal static void MarshalToNative(
    in Sensor source,
    ref Sensor_Native target,
    ref NativeArena arena)
{
    // Primitive: direct copy
    target.id = source.Id;
    
    // String: allocate in arena
    if (source.Name != null)
        target.name = arena.CreateString(source.Name);
    else
        target.name = IntPtr.Zero;
    
    // Sequence: block copy for primitives
    if (source.Data != null && source.Data.Count > 0)
    {
        var span = CollectionsMarshal.AsSpan(source.Data);
        target.data = arena.CreateSequence(span);
    }
    else
        target.data = default;
}
```

### 3.4 View Structs (Generated)

**Purpose:** Zero-copy wrappers over native memory for reading.

**Design:**
- `ref struct` to prevent escape
- Pointer to native struct as private field
- Properties for field access

**Example:**
```csharp
public ref struct SensorView
{
    private unsafe readonly Sensor_Native* _ptr;
    
    internal unsafe SensorView(Sensor_Native* ptr) => _ptr = ptr;
    
    // Primitives: direct dereference
    public int Id => _ptr->id;
    
    // Strings: lazy conversion
    public string? Name => 
        _ptr->name == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(_ptr->name);
    
    // Sequences: span wrapper
    public ReadOnlySpan<double> Data => 
        new ReadOnlySpan<double>(
            (void*)_ptr->data.Buffer,
            (int)_ptr->data.Length);
}
```

### 3.5 DdsLoan (Runtime)

**File:** `src/CycloneDDS.Runtime/DdsLoan.cs`

**Purpose:** Manages lifecycle of loaned memory from `dds_take`.

**Key Features:**
- `IDisposable` for automatic cleanup
- Holds `IntPtr[]` samples and `DdsSampleInfo[]` metadata
- Enumerator pattern for zero-allocation iteration

**Example:**
```csharp
public class DdsLoan : IDisposable
{
    private readonly DdsEntity _reader;
    internal readonly IntPtr[] _samples;
    internal readonly DdsSampleInfo[] _infos;
    internal readonly int _length;
    
    public void Dispose()
    {
        if (_length > 0)
            DdsApi.dds_return_loan(_reader, _samples, _length);
        // Return arrays to ArrayPool
    }
    
    public Enumerator GetEnumerator() => new Enumerator(this);
    
    public ref struct Enumerator
    {
        public bool MoveNext() { ... }
        public DdsSampleRef Current { ... }
    }
}
```

---

## 4. Write Path (Detailed Flow)

### 4.1 Complete Sequence

```
User Code: writer.Write(sample)
    │
    ├─> 1. SIZING
    │   ├─ Call: GetNativeSize(sample)
    │   │  ├─ Calculate: sizeof(T_Native)         // Head size
    │   │  ├─ Add: String lengths (UTF-8 + NUL)
    │   │  ├─ Add: Sequence sizes (aligned)
    │   │  └─ Return: totalSize
    │   │
    ├─> 2. ALLOCATION
    │   ├─ Rent: ArrayPool<byte>.Shared.Rent(totalSize)
    │   │  └─ Result: byte[] buffer (may be larger)
    │   │
    ├─> 3. PINNING
    │   ├─ fixed (byte* ptr = buffer)
    │   │  └─ Result: IntPtr baseAddress
    │   │
    ├─> 4. INITIALIZATION
    │   ├─ Zero Head: buffer[0..headSize].Clear()
    │   ├─ Create Arena: new NativeArena(buffer, ptr, headSize)
    │   ├─ Cast Head: ref T_Native head = ref Unsafe.AsRef<T_Native>(ptr)
    │   │
    ├─> 5. MARSHALING
    │   ├─ Call: MarshalToNative(sample, ref head, ref arena)
    │   │  ├─ Primitives: head.field = sample.Field
    │   │  ├─ Strings: head.strPtr = arena.CreateString(sample.Str)
    │   │  │  └─ Arena encodes UTF-8, writes to Tail, returns pointer
    │   │  ├─ Sequences: head.seq = arena.CreateSequence(sample.List)
    │   │  │  └─ Arena aligns, memcpy's data, returns DdsSequenceNative
    │   │  └─ Nested: Recurse for complex types
    │   │
    ├─> 6. NATIVE CALL
    │   ├─ Call: DdsApi.dds_write(_writerHandle, ptr)
    │   │  └─ Cyclone reads struct at ptr
    │   │     ├─ Follows pointers (all point into buffer)
    │   │     ├─ Serializes to CDR (XCDR1/XCDR2 per QoS)
    │   │     └─ Sends to network
    │   │
    ├─> 7. CLEANUP
    │   └─ ArrayPool<byte>.Shared.Return(buffer)
    │      └─ Buffer recycled for next write
    │
    └─> Done (Zero allocations in steady state)
```

### 4.2 Memory Layout Example

**Sample:**
```csharp
var sample = new Sensor {
    Id = 100,
    Name = "Hi",
    Data = new List<double> { 1.1, 2.2 }
};
```

**Resulting Memory (x64):**

```
Offset  Content               Description
------  -------               -----------
0x0000  64 00 00 00           int id = 100
0x0004  [4 bytes padding]     (alignment for IntPtr)
0x0008  18 00 00 00           IntPtr name = 0x0018 (points to offset 0x18)
0x0010  02 00 00 00           DdsSequenceNative.Maximum = 2
0x0014  02 00 00 00           DdsSequenceNative.Length = 2
0x0018  20 00 00 00           DdsSequenceNative.Buffer = 0x0020
0x001C  00                    DdsSequenceNative.Release = 0
0x001D  [padding to 0x18]
--- Tail starts here ---
0x0018  48 69 00              "Hi\0" (UTF-8)
0x001B  [padding to 0x20]
0x0020  9A 99 99 99 99 99 F1 3F   double 1.1
0x0028  9A 99 99 99 99 99 01 40   double 2.2
```

---

## 5. Read Path (Detailed Flow)

### 5.1 Complete Sequence

```
User Code: using (var loan = reader.Read()) { ... }
    │
    ├─> 1. ALLOCATION
    │   ├─ Rent: IntPtr[] samples = ArrayPool<IntPtr>.Rent(maxSamples)
    │   ├─ Rent: DdsSampleInfo[] infos = ArrayPool<DdsSampleInfo>.Rent(maxSamples)
    │   │
    ├─> 2. NATIVE CALL
    │   ├─ Call: dds_take(_readerHandle, samples, infos, maxSamples)
    │   │  └─ Cyclone fills samples[] with pointers to its cache
    │   │  └─ Returns: count of samples read
    │   │
    ├─> 3. LOAN CREATION
    │   ├─ Wrap: new DdsLoan(_readerHandle, samples, infos, count)
    │   │  └─ Loan now owns arrays and native pointers
    │   │
    ├─> 4. ITERATION
    │   └─ foreach (var sampleRef in loan)
    │      ├─ Get: DdsSampleRef { DataPtr, Info }
    │      ├─ Check: if (sampleRef.Info.ValidData)
    │      │  ├─ Cast: new SensorView((Sensor_Native*)sampleRef.DataPtr)
    │      │  ├─ Access: view.Id        // Dereferences _ptr->id
    │      │  ├─ Access: view.Name      // Calls PtrToStringUTF8 (allocates!)
    │      │  └─ Access: view.Data      // Wraps pointer in ReadOnlySpan (zero-copy)
    │      │
    │      └─ Optional: ToManaged(view) for deep copy
    │
    ├─> 5. CLEANUP (Dispose)
    │   ├─ Call: dds_return_loan(_readerHandle, samples, count)
    │   │  └─ Cyclone unlocks and potentially frees memory
    │   └─ Return arrays to ArrayPool
    │
    └─> Done (Views invalid after Dispose!)
```

### 5.2 Safety Guarantees

- **View Lifetime:** `ref struct` prevents storage on heap
- **Loan Scope:** `IDisposable` ensures `return_loan` is called
- **Invalid Data:** `info.ValidData == false` for disposed instances (keys only)

---

## 6. Type Mapping Rules

### 6.1 Primitives

| IDL Type | C Type | C# Ghost Field | C# DSL Property |
|----------|--------|----------------|-----------------|
| `long` | `int32_t` | `int` | `int` |
| `unsigned long` | `uint32_t` | `uint` | `uint` |
| `long long` | `int64_t` | `long` | `long` |
| `float` | `float` | `float` | `float` |
| `double` | `double` | `double` | `double` |
| `boolean` | `uint8_t` | `byte` | `bool` |
| `octet` | `uint8_t` | `byte` | `byte` |
| `char` | `char` (8-bit) | `byte` | `char` (cast) |

**Critical:** Always use `byte` for `boolean` in ghost structs to match C ABI.

### 6.2 Strings

| IDL | C | C# Ghost | C# DSL | Marshaling |
|-----|---|----------|--------|------------|
| `string` | `char*` | `IntPtr` | `string?` | UTF-8 encode + NUL terminator in arena |

**Read Path Options:**
- **Fast:** `ReadOnlySpan<byte> NameRaw` (zero-copy)
- **Convenient:** `string Name` (allocates via `PtrToStringUTF8`)

### 6.3 Sequences

| IDL | C | C# Ghost | C# DSL |
|-----|---|----------|--------|
| `sequence<T>` | `dds_sequence_t` | `DdsSequenceNative` | `List<T>` |

**DdsSequenceNative Structure:**
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct DdsSequenceNative {
    public uint Maximum;   // Capacity
    public uint Length;    // Count
    public IntPtr Buffer;  // Pointer to data
    public byte Release;   // Ownership flag (false for arena)
}
```

**Marshaling:**
- **Primitives:** Block copy via `MemoryMarshal.AsBytes(span).CopyTo(arena)`
- **Structs:** Loop + recursive marshal for each element

### 6.4 Arrays (Fixed Size)

| IDL | C | C# Ghost | C# DSL |
|-----|---|----------|--------|
| `long[5]` | `int32_t[5]` | `unsafe fixed int values[5]` | `int[]` |

**Implementation Note:** For primitive types, use `unsafe fixed` buffers to ensure embedding. For struct arrays, generate inline fields (`values_0`, `values_1`, etc.) since C# doesn't support fixed buffers for custom types.

**Marshaling:** Use `fixed` block to get pointer to embedded array, then loop/copy element-by-element.

**View:** Expose as `ReadOnlySpan<int>` via pointer arithmetic to the fixed buffer.

**Zero-Copy:** Yes (array is inline).

### 6.5 Optional Fields

| IDL | C | C# Ghost | C# DSL |
|-----|---|----------|--------|
| `@optional long` | `int32_t*` | `IntPtr` | `int?` |
| `@optional MyStruct` | `MyStruct*` | `IntPtr` | `MyStruct?` |

**Marshaling:**
- Present: Allocate in arena via `AllocateOne<T>()`, store pointer
- Absent: Set pointer to `IntPtr.Zero`

**Read Path:**
- Check `ptr != IntPtr.Zero` before dereferencing

### 6.6 Nested Structs

| IDL | C | C# Ghost | C# DSL |
|-----|---|----------|--------|
| `Inner inner` | `Inner_Native inner` | `Inner_Native inner` | `Inner Inner` |

**Marshaling:** Embedded value type, no pointer. Recurse into marshaler.

### 6.7 Unions

| IDL | C | C# Ghost | C# DSL |
|-----|---|----------|--------|
| `union U switch(long) { ... }` | `struct { int _d; union { ... } _u; }` | `struct { int _d; UnionBlock_Native _u; }` | Union DSL type |

**UnionBlock_Native:** Uses `[FieldOffset(0)]` for all fields (overlapping).

---

## 7. Special Cases

### 7.1 Keyed Topics

**Challenge:** `DisposeInstance()` and `UnregisterInstance()` require only key fields.

**Solution:** Generate separate `MarshalKeyToNative()` method.

**Approach:**
- Reuse full `T_Native` struct (maintains offsets)
- Zero-initialize entire HEAD
- Populate only `[DdsKey]` fields
- Cyclone reads keys from descriptor-defined offsets

**Example:**
```csharp
public static int GetKeyNativeSize(in Sensor source)
{
    int size = Unsafe.SizeOf<Sensor_Native>();
    if (source.Name != null) // Assuming Name is a key
        size += Encoding.UTF8.GetByteCount(source.Name) + 1;
    return size; // No Data (non-key) size added
}

internal static void MarshalKeyToNative(
    in Sensor source,
    ref Sensor_Native target,
    ref NativeArena arena)
{
    target.id = source.Id;
    target.name = source.Name != null ? arena.CreateString(source.Name) : IntPtr.Zero;
    // target.data left as default (zeroed)
}
```

### 7.2 Empty vs Null Collections

**Rule:** Both `null` and empty `List<T>` marshal to `Length=0, Buffer=NULL`.

**Rationale:** DDS treats empty and absent sequences identically unless `@optional`.

### 7.3 Alignment Padding

**Problem:** `ArrayPool.Rent()` returns dirty memory.

**Solution:** Zero-out HEAD region in `NativeArena` constructor:
```csharp
_buffer.Slice(0, headSize).Clear();
```

**Impact:**
- Ensures padding bytes are zeroed (deterministic serialization)
- Prevents information leaks
- Minimal performance cost (~10 nanoseconds for 64-byte struct)

### 7.4 Large Samples

**Threshold:** 1MB

**Strategy:**
- Small samples (<1MB): Use `ArrayPool`
- Large samples (≥1MB): Use `GC.AllocateUninitializedArray(size, pinned: true)`

**Rationale:** Prevents pool fragmentation with massive buffers.

---

## 8. Performance Characteristics

### 8.1 Write Path Cost Breakdown

| Operation | Cost | Notes |
|-----------|------|-------|
| `GetNativeSize` | O(n) | Fast scan (UTF-8 byte counting) |
| `ArrayPool.Rent` | O(1) | Lock-free in steady state |
| Pinning (`fixed`) | 0 | Compiler directive, no runtime cost |
| Zeroing HEAD | ~10ns | memset for 64-byte struct |
| Primitive marshaling | ~1ns per field | Register mov |
| String marshaling | ~50ns per string | UTF-8 encoding + memcpy |
| Sequence marshaling | ~0.3ns per element | Block memcpy |
| `dds_write` | Varies | Network I/O dominates |
| `ArrayPool.Return` | O(1) | Lock-free |

**Total Overhead:** ~100-200ns for typical struct with 1 string + 1 small sequence.

### 8.2 Read Path Cost Breakdown

| Operation | Cost | Notes |
|-----------|------|-------|
| `ArrayPool.Rent` (IntPtr[]) | O(1) | For samples array |
| `dds_take` | Varies | Native call |
| View construction | ~5ns | Pointer cast |
| Primitive access | ~1ns | Pointer dereference |
| String access (span) | ~10ns | Span construction |
| String access (copy) | ~50ns + alloc | `PtrToStringUTF8` |
| Sequence access (span) | ~10ns | Span construction |
| `dds_return_loan` | Varies | Native call |

**Total Overhead:** ~20-30ns for zero-copy access, ~100ns+ if copying strings.

### 8.3 Comparison to Pure C

| Metric | Pure C | Native Marshaling | Ratio |
|--------|--------|-------------------|-------|
| Write latency | 100% | 105-110% | ~95-98% speed |
| Read latency (zero-copy) | 100% | 102-105% | ~95-98% speed |
| Allocations | 0 | 0 (steady state) | Same |
| Code complexity | High (manual structs) | Low (generated) | Much easier |

**Verdict:** Negligible performance loss for massive robustness gain.

---

## 9. Comparison to Current CDR Approach

### 9.1 What's Removed

| Component | Current Status | New Status | Reason |
|-----------|----------------|------------|--------|
| `CdrWriter.cs` | Core serialization | **Removed** | No longer writing CDR |
| `CdrReader.cs` | Core deserialization | **Removed** (or kept for fallback) | Reading from native structs |
| `AlignmentMath.cs` | Manual padding logic | **Removed** | `[StructLayout]` handles it |
| `CdrSizer.cs` | CDR size calculation | **Simplified to GetNativeSize** | Struct size + dynamic data |
| `CdrEncoding` enum | XCDR1/XCDR2 switching | **Kept** (for QoS only) | Not used in marshaling |
| DHEADER logic | Manual XCDR2 headers | **Removed** | Cyclone handles it |
| Serializer DHEADER emit | CodeGen emits DHEADER writes | **Removed** | Cyclone handles it |

### 9.2 What's Reused

| Component | Reusability | Changes Needed |
|-----------|-------------|----------------|
| **Schema Discovery** | 100% | None |
| **IDL Generation** | 100% | None |
| **`idlc` Integration** | 100% | None |
| **Topic Descriptor Parsing** | 100% | None |
| **`DdsParticipant`** | 90% | Remove `CreateTopic` CDR refs |
| **`DdsWriter` class** | 50% | Replace serialization logic |
| **`DdsReader` class** | 50% | Replace deserialization logic |
| **Test Infrastructure** | 80% | Update assertions (no CDR checks) |
| **Golden Rig Tests** | 100% | Verify against native C output |
| **Runtime P/Invoke** | 90% | Add `dds_write` (replace `dds_writecdr`) |
| **ArrayPool usage** | 100% | Extend to IntPtr[] arrays |

### 9.3 What's New

| Component | Purpose | Complexity |
|-----------|---------|------------|
| **NativeArena** | Tail memory manager | Medium |
| **DdsNativeTypes** | Sequence header definition | Low |
| **Ghost Struct Generation** | C-ABI compatible structs | Medium |
| **Marshaller Generation** | Object→Native transform | Medium |
| **View Struct Generation** | Zero-copy readers | Medium |
| **DdsLoan** | Loan lifecycle manager | Low |
| **Key Marshallers** | Sparse key-only marshaling | Low |

---

## 10. Migration Strategy

### 10.1 Phase Overview

| Phase | Goal | Breaking Changes | Risk |
|-------|------|------------------|------|
| **Phase 1** | Add core infrastructure | None (additive) | Low |
| **Phase 2** | Regenerate serializers | None (internal) | Medium |
| **Phase 3** | Regenerate deserializers | None (internal) | Medium |
| **Phase 4** | Update runtime plumbing | API remains same | Medium |
| **Phase 5** | Cleanup legacy code | None (deletion) | Low |

### 10.2 Detailed Steps

#### Phase 1: Core Infrastructure (Additive)
1. Create `NativeArena.cs` (arena manager)
2. Create `DdsNativeTypes.cs` (sequence definitions)
3. Create `DdsTextEncoding.cs` (UTF-8 helpers)

**Impact:** Zero (purely additive).

#### Phase 2: Writer CodeGen
1. Update `SerializerEmitter.cs`:
   - Add `EmitGhostStruct()`
   - Add `EmitNativeSizer()`
   - Add `EmitMarshaller()`
   - Remove CDR emission logic

**Impact:** Generated code changes (breaks compilation until Phase 4).

#### Phase 3: Reader CodeGen
1. Update `DeserializerEmitter.cs`:
   - Add `EmitViewStruct()`
   - Add `EmitToManaged()`
   - Remove CDR reader logic

**Impact:** Generated code changes.

#### Phase 4: Runtime Plumbing
1. Update `DdsWriter.cs`:
   - Replace `CdrWriter` usage with `NativeArena`
   - Replace `dds_writecdr` with `dds_write`
2. Create `DdsLoan.cs` (loan manager)
3. Update `DdsReader.cs`:
   - Replace deserialization with view casting
   - Use `dds_take` with pointer arrays

**Impact:** Implementation changes, API surface unchanged.

#### Phase 5: Cleanup
1. Delete `CdrWriter.cs`, `CdrReader.cs`, `AlignmentMath.cs`
2. Update tests (no longer check CDR bytes directly)
3. Verify Golden Rig tests (byte-perfect output)

**Impact:** Cleaner codebase.

### 10.3 Testing Strategy

| Test Type | Current | New | Verification Method |
|-----------|---------|-----|---------------------|
| **Unit Tests** | CDR byte checks | Ghost struct layout checks | `Marshal.OffsetOf` vs JSON offsets |
| **Roundtrip Tests** | C# write → C# read | C# write → Native read → C# | Interop verification |
| **Golden Rig** | Byte comparison | Byte comparison | C output vs C# marshaled bytes |
| **Performance** | Allocation tests | Allocation tests | BenchmarkDotNet |
| **Compatibility** | XCDR1/2 switching | QoS-based | Native subscriber receives correct data |

---

## 11. Edge Cases & Mitigations

### 11.1 ABI Mismatches

**Risk:** C# struct padding differs from C compiler.

**Mitigation:**
- Use `[StructLayout(LayoutKind.Sequential)]` (standard packing)
- Generate unit tests: `Marshal.OffsetOf<T_Native>("field")` vs JSON offset
- Golden Rig validation

### 11.2 32-bit vs 64-bit Pointer Sizes

**Issue:** `IntPtr` is 4 bytes on 32-bit, 8 bytes on 64-bit. Ghost struct layout will mismatch.

**Implementation Requirement:**
- **This library targets x64 only.** 32-bit support is not a goal due to:
  - Cyclone DDS native libraries are primarily distributed as x64.
  - Modern DDS deployments are x64.
  - Pointer size mismatches add significant complexity.
- Project files should specify `<PlatformTarget>x64</PlatformTarget>` or `AnyCPU` with x64 preference.
- CI tests run on x64 only.
- Documentation explicitly states x64 requirement.

### 11.3 Dirty Padding Bytes

**Risk:** ArrayPool returns dirty memory, padding contains garbage.

**Mitigation:**
- Zero HEAD region in `NativeArena` constructor
- Cost: ~10ns (negligible)

### 11.4 Large Samples

**Risk:** ArrayPool exhaustion or fragmentation.

**Mitigation:**
- Threshold: 1MB
- Large samples: use `GC.AllocateUninitializedArray(pinned: true)`
- Cost: One-time allocation (LOH-friendly)

### 11.5 String Encoding

**Risk:** UTF-8 encoding fails (invalid chars).

**Mitigation:**
- Use `UTF8Encoding(false, throwOnInvalidBytes: false)`
- Replace invalid sequences with replacement char
- Log warning if needed

### 11.6 Nested View Escaping

**Risk:** User stores View reference after loan disposal.

**Mitigation:**
- `ref struct` prevents heap storage (compiler enforced)
- Document: "Views invalid after `using` block"

---

## 12. Future Enhancements

### 12.1 Lazy String Decoding

**Current:** `view.Name` allocates string immediately.

**Enhancement:** `view.NameUtf8` returns `Utf8String` (zero-copy parsing).

**Benefit:** Eliminate all string allocations in hot path.

### 12.2 Struct Pooling

**Current:** `ToManaged()` always allocates new struct.

**Enhancement:** Allow passing reusable struct: `ToManaged(view, ref existingStruct)`.

**Benefit:** Reduce GC pressure for high-frequency reads.

### 12.3 Block Copy Optimization

**Current:** All sequences use loop for structs.

**Enhancement:** Detect blittable structs (no pointers), use `memcpy`.

**Benefit:** 10-100x speedup for large arrays of simple structs.

### 12.4 Key-Only Reads

**Current:** `dds_take` returns full sample.

**Enhancement:** Add `ReadKeys()` method using `dds_take_instance` variants.

**Benefit:** Avoid deserializing large payloads when only keys needed.

---

## 13. Glossary

| Term | Definition |
|------|------------|
| **Arena** | Memory management pattern: pre-allocate block, subdivide dynamically |
| **Ghost Struct** | C#-side representation of native C struct with exact memory layout |
| **Head** | Fixed-size root struct containing primitives and pointers |
| **Tail** | Variable-size region containing strings and sequence data |
| **Loan** | Memory borrowed from DDS middleware, must be returned via `return_loan` |
| **View** | Zero-copy wrapper (`ref struct`) over native memory |
| **Marshaling** | Converting managed C# objects to native memory layout |
| **Blittable** | Type with identical memory layout in managed/unmanaged code |
| **ABI** | Application Binary Interface (memory layout, calling conventions) |
| **CDR** | Common Data Representation (DDS wire format) |
| **XCDR1/XCDR2** | CDR encoding versions (v1 is legacy, v2 adds extensibility) |

---

## 14. References

### 14.1 Internal Documents
- Original design discussion: `docs/marshal-to-native-instead-of-cdr.md`
- Current CDR design: `docs/SERDATA-DESIGN.md`
- Task master: `docs/SERDATA-TASK-MASTER.md`
- Task tracker: `.dev-workstream/TASK-TRACKER.md`

### 14.2 External Standards
- OMG DDS 1.4 Specification (Topic Descriptor format)
- OMG CDR 2.0 Specification (XCDR2 encoding rules)
- Cyclone DDS Documentation (Native API reference)

### 14.3 Implementation Examples
- Cyclone DDS CXX Bindings (reference implementation)
- RTI Connext .NET Binding (commercial example)
- OpenDDS C# Wrapper (alternative approach)

---

**End of Document**
