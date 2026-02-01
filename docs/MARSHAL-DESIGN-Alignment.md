# MARSHAL-DESIGN-Alignment.md

## Union Memory Layout Strategy

To resolve ABI mismatches between C# and C (Native CycloneDDS), we use an **Explicit Layout Strategy** for Unions.

### 1. The Problem
- **C Layout:** Unions are often `byte` aligned (if payload allows) or tightly packed.
- **C# Default:** `StructLayout(Sequential)` aligns fields to 4 or 8 bytes.
- **Result:** `AccessViolationException` when marshaling because C# reads offsets (e.g., 8) where C expects data (e.g., 1).

### 2. The Solution: Explicit Offsets
We do NOT let the C# CLR decide the layout. We calculate it explicitly in `SerializerEmitter.cs`.

**Field 1: Discriminator**
- Offset: `0`
- Size: 1 (bool/byte), 4 (int/enum).

**Field 2: Payload (`_u`)**
- Offset: Calculated via Formula.
- Formula: `PayloadOffset = (DiscriminatorSize + (MaxMemberAlign - 1)) & ~(MaxMemberAlign - 1)`
- `MaxMemberAlign`: The maximum alignment requirement of any member IN the union payload.
    - `double`, `long`, `IntPtr`: 8
    - `int`, `float`, `enum`: 4
    - `short`: 2
    - `byte`, `bool`: 1

### 3. Bounded Strings
- **IDL:** `string<32>`
- **C Layout:** `char field[33]` (Inline Array).
- **C# Strategy:**
    - We do NOT use `string` or `IntPtr`.
    - We generate `fixed byte field[33]`.
    - We marshal UTF-8 bytes directly into this fixed buffer.
    - **Alignment:** 1 byte (Character array).

### 4. Architecture Constraint (CRITICAL)
**This Logic Assumes x64 (64-bit) Architecture.**
- `IntPtr` size is assumed to be **8 bytes**.
- `double` alignment is assumed to be **8 bytes**.
- If targeting 32-bit (x86 or ARM32), the Generator MUST be updated to use 4-byte pointers/alignment.
