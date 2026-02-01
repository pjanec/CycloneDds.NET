# Native Marshaling: Alignment & Layout Rules

**Project:** FastCycloneDDS C# Bindings  
**Document Type:** Technical Deep Dive (Alignment Strategy)  
**Last Updated:** 2026-02-01  
**Context:** This document complements [MARSHAL-DESIGN.md](MARSHAL-DESIGN.md) by documenting the hard-won lessons from the "ABI Alignment War" (Phase 6).

---

## 1. Executive Summary

When marshaling C# objects to native C-struct memory, **alignment matters**. Incorrect alignment causes:
- `AccessViolation` crashes (in Cyclone DDS native code)
- Silent data corruption (wrong offsets, truncated sequences)
- Mysterious failures only reproducible in certain data scenarios

This document codifies the **two critical alignment strategies** that enable correct native marshaling:

1. **Union Memory Layout Strategy** (LayoutKind.Explicit)
2. **Bounded String Marshaling** (fixed byte[N+1] inline arrays)

---

## 2. Union Memory Layout Strategy

### 2.1 The Problem

IDL Unions are discriminated unions with a discriminator field followed by a payload. In C:

```c
// IDL:
// union MyUnion switch(short) {
//   case 1: long x;
//   case 2: double y;
// };

// Compiled C struct (by Cyclone DDS):
struct MyUnion {
    int16_t _d;        // Discriminator (2 bytes)
    // <Padding: 6 bytes to align payload to 8-byte boundary>
    union {
        int32_t x;     // Case 1 (4 bytes)
        double y;      // Case 2 (8 bytes, requires 8-byte alignment)
    } _u;
};
// sizeof(MyUnion) = 16 bytes (2 + 6 padding + 8)
```

The C compiler **automatically aligns** the payload (`_u`) to the **maximum alignment** of its members. For a `double` (8-byte aligned), the discriminator (2 bytes) is followed by **6 bytes of padding**.

### 2.2 The Naive C# Approach (BROKEN)

Using `[StructLayout(LayoutKind.Sequential)]`:

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct MyUnion_Native 
{
    public short _d;  // 2 bytes
    public MyUnion_Union_Native _u; // C# packs immediately after _d (WRONG!)
}

[StructLayout(LayoutKind.Sequential)]
internal struct MyUnion_Union_Native 
{
    public int x;     // Offset 0 (relative to _u)
    public double y;  // Offset 0 (overlapping x)
}
```

**Result:** C# places `_u` at **offset 2**, but C expects it at **offset 8**. This causes:
- **Read corruption:** C reads garbage when deserializing.
- **Write crashes:** DDS native code writes to wrong memory locations.

### 2.3 The Solution: Explicit Layout with Alignment Formula

We use `[StructLayout(LayoutKind.Explicit)]` to **manually control field offsets**:

```csharp
[StructLayout(LayoutKind.Explicit)]
internal unsafe struct MyUnion_Native 
{
    [FieldOffset(0)]
    public short _d;  // Discriminator at offset 0

    [FieldOffset(8)]  // <--- CALCULATED OFFSET (not 2!)
    public MyUnion_Union_Native _u;
}

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct MyUnion_Union_Native 
{
    [FieldOffset(0)] public int x;
    [FieldOffset(0)] public double y;  // All members start at 0 (overlap)
}
```

### 2.4 The Alignment Formula

**Implementation Location:** [SerializerEmitter.cs:1007](../tools/CycloneDDS.CodeGen/SerializerEmitter.cs#L1007)

```csharp
// 1. Determine discriminator size
int discSize = sizeof(discriminatorType); // e.g., 2 for short, 4 for int

// 2. Calculate maximum alignment of ALL union members
int maxAlign = 1;
foreach (var member in unionMembers) 
{
    int align = GetAlignment(member); // 8 for double, 4 for int, etc.
    if (align > maxAlign) maxAlign = align;
}

// 3. Align the payload offset
int payloadOffset = (discSize + (maxAlign - 1)) & ~(maxAlign - 1);

// Example:
// discSize = 2 (short)
// maxAlign = 8 (because union contains a double)
// payloadOffset = (2 + 7) & ~7 = 9 & ~7 = 8 ✓
```

**Explanation:**
- `(discSize + (maxAlign - 1))` rounds **up** to the next multiple of `maxAlign`.
- `& ~(maxAlign - 1)` masks to align to `maxAlign` boundary.
- This **exactly replicates** C compiler alignment rules.

### 2.5 Why Not LayoutKind.Sequential?

You might think: "Can't C# handle this automatically?"

**No.** C#'s `LayoutKind.Sequential` respects **field order** but does NOT enforce **C-style alignment rules**. Specifically:
- C# does not automatically align nested structs to their internal maximum alignment.
- The CLR may pack fields differently than a C compiler.

`LayoutKind.Explicit` gives us **full control** to match C memory layout byte-for-byte.

---

## 3. Bounded String Marshaling Strategy

### 3.1 The Problem

IDL Bounded Strings are **inline arrays**, not pointers:

```idl
struct MyStruct {
    string<10> name;  // Max 10 characters
};
```

In C (compiled by Cyclone DDS):

```c
struct MyStruct {
    char name[11];  // Inline array (10 + null terminator)
};
// sizeof(MyStruct) = 11 bytes (no padding)
```

The string is **not a pointer** but an **embedded array** in the struct's memory.

### 3.2 The Naive C# Approach (BROKEN)

Using an `IntPtr`:

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct MyStruct_Native 
{
    public IntPtr name;  // 8 bytes on x64 (WRONG!)
}
// sizeof(MyStruct_Native) = 8 bytes (C expects 11!)
```

**Result:**
- **Size mismatch:** C expects 11 bytes, C# allocates 8.
- **Pointer leak:** C expects inline bytes, gets a pointer to heap.
- **Corruption:** DDS overwrites the pointer value with string bytes.

### 3.3 The Solution: Fixed Buffers

We use **`fixed byte[N+1]`** (C#'s inline array feature):

```csharp
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct MyStruct_Native 
{
    public fixed byte name[11];  // Inline array (10 + null)
}
// sizeof(MyStruct_Native) = 11 bytes ✓
```

**Implementation Location:** [SerializerEmitter.cs:1031](../tools/CycloneDDS.CodeGen/SerializerEmitter.cs#L1031)

```csharp
int? maxLen = GetMaxLength(field); // Extract @max_length from IDL
if (maxLen.HasValue && field.TypeName == "string") 
{
    sb.AppendLine($"public fixed byte {field.Name}[{maxLen.Value + 1}];");
}
```

### 3.4 Marshaling Bounded Strings

**Write (C# to Native):**

```csharp
fixed (byte* dst = native._u.name) 
{
    int written = Encoding.UTF8.GetBytes(source.Name, new Span<byte>(dst, 11));
    dst[written] = 0; // Null terminator
}
```

**Read (Native to C#):**

```csharp
fixed (byte* src = native._u.name) 
{
    int len = 0;
    while (len < 11 && src[len] != 0) len++;
    return Encoding.UTF8.GetString(src, len);
}
```

### 3.5 Unbounded Strings

Unbounded strings (`string` without `<N>`) are **still pointers**:

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct MyStruct_Native 
{
    public IntPtr unboundedName;  // Pointer to heap-allocated string
}
```

**Rule:** Bounded = Inline Array. Unbounded = Pointer.

---

## 4. Practical Guidelines

### 4.1 When to Use LayoutKind.Explicit

| Type | Layout Kind | Reason |
|------|------------|---------|
| Union Wrapper | **Explicit** | Must manually align payload to maxAlign |
| Union Payload | **Explicit** | All members must overlap (FieldOffset 0) |
| Regular Struct | **Sequential** | C# aligns correctly for sequential fields |

### 4.2 Debugging Alignment Issues

**Symptoms:**
- `AccessViolation` crashes in `dds_write` or `dds_take`.
- Corrupted data (sequences have wrong lengths, strings truncated).
- Tests fail with seemingly random data.

**Investigation Steps:**

1. **Compare C struct size:**
   ```c
   printf("sizeof(MyUnion) = %zu\n", sizeof(MyUnion));
   ```
   vs C#:
   ```csharp
   Console.WriteLine($"sizeof(MyUnion_Native) = {Marshal.SizeOf<MyUnion_Native>()}");
   ```
   **Must match exactly.**

2. **Compare field offsets:**
   ```c
   printf("offsetof(MyUnion, _u) = %zu\n", offsetof(MyUnion, _u));
   ```
   vs C#:
   ```csharp
   Console.WriteLine($"FieldOffset(_u) = {Marshal.OffsetOf<MyUnion_Native>("_u")}");
   ```

3. **Use the Layout Probe Tests:**
   - See [LayoutProbeTests.cs](../tests/CycloneDDS.Runtime.Tests/LayoutProbeTests.cs)
   - These tests perform **roundtrip verification** through DDS.
   - If data survives a Write → Read cycle, alignment is correct.

### 4.3 The "Golden Rule"

> **Never guess at struct layout. Always calculate alignment explicitly for unions, and use fixed buffers for bounded strings.**

C# is not C. The CLR's memory model is **similar but not identical** to C's ABI. When marshaling to native memory, we must **manually replicate** C compiler behavior.

---

## 5. Historical Context (For Maintainers)

### 5.1 The "ABI Alignment War" (Jan 2026)

**Timeline:**
- **BATCH-10:** Implemented union support with `Sequential` layout. Tests passed in simple cases.
- **BATCH-11:** `TestComplexRoundTrip` started failing with `AccessViolation` crashes.
- **BATCH-12:** Root cause analysis revealed:
  - Sequences had **wrong sizes** (expected 24, got 16).
  - Union payloads were **misaligned** (payload at offset 2, not 8).
  - Bounded strings were **pointers** instead of inline arrays.

**The Turning Point:**
- We created `UnionProbe.idl` to reverse-engineer Cyclone's C struct layout.
- We built `LayoutProbeTests.cs` to perform roundtrip verification.
- We implemented the **explicit layout algorithm** (formula above).

**Result:** `TestComplexRoundTrip` now passes. All tests green.

### 5.2 Lessons Learned

1. **Alignment is not optional.** C ABIs are rigid. A single byte misalignment breaks everything.
2. **Trust but verify.** Always compare C and C# struct sizes/offsets.
3. **Test with real data.** Simple structs (int, double) hide alignment bugs. Complex nested unions expose them.
4. **Document aggressively.** Future developers will try `Sequential` for unions. Stop them **now** with this doc.

---

## 6. References

- **Code Implementation:** [SerializerEmitter.cs](../tools/CycloneDDS.CodeGen/SerializerEmitter.cs#L970-L1050)
- **Test Verification:** [LayoutProbeTests.cs](../tests/CycloneDDS.Runtime.Tests/LayoutProbeTests.cs)
- **Main Design Doc:** [MARSHAL-DESIGN.md](MARSHAL-DESIGN.md)
- **Task Tracker:** [MARSHAL-TASK-TRACKER.md](MARSHAL-TASK-TRACKER.md)

---

**End of Document**  
*Survived the ABI War. May future developers learn from our battles.*
