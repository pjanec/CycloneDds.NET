# Native Marshaling - Task Details

**Project:** FastCycloneDDS C# Bindings  
**Architecture:** Arena/Native Marshaling (Object-Centric)  
**Last Updated:** 2026-01-31

**Design Reference:** [MARSHAL-DESIGN.md](MARSHAL-DESIGN.md)  
**Task Tracker:** [MARSHAL-TASK-TRACKER.md](MARSHAL-TASK-TRACKER.md)

---

## Overview

This document provides detailed specifications for each task in the Native Marshaling migration. Each task includes:
- **Unique ID** for tracking
- **Description** and scope
- **Dependencies** on other tasks
- **Success Criteria** with specific deliverables
- **Unit Test Specifications** with expected outcomes
- **Design References** to relevant sections

---

## Phase 1: Foundation - Core Infrastructure

### FCDC-M001: DdsNativeTypes Implementation

**Description:** Create native type definitions for DDS sequences and core structures.

**File:** `src/CycloneDDS.Core/DdsNativeTypes.cs` (NEW)

**Dependencies:** None

**Scope:**
- Define `DdsSequenceNative` struct matching C `dds_sequence_t` ABI
- Use correct field types and layout attributes
- Add XML documentation

**Success Criteria:**
1. File created at specified location
2. `DdsSequenceNative` struct defined with `[StructLayout(LayoutKind.Sequential)]`
3. Fields match C struct: `Maximum` (uint32), `Length` (uint32), `Buffer` (IntPtr), `Release` (byte)
4. XML documentation explaining each field
5. Namespace: `CycloneDDS.Core`

**Unit Test Specifications:**

**Test File:** `tests/CycloneDDS.Core.Tests/DdsNativeTypesTests.cs`

| Test Name | Assertion | Expected Result |
|-----------|-----------|-----------------|
| `DdsSequenceNative_LayoutIsSequential` | `typeof(DdsSequenceNative).StructLayoutAttribute.Value == LayoutKind.Sequential` | True |
| `DdsSequenceNative_SizeIsCorrect_x64` | `sizeof(DdsSequenceNative) == 24` (on x64) | True |
| `DdsSequenceNative_SizeIsCorrect_x86` | `sizeof(DdsSequenceNative) == 16` (on x86) | True |
| `DdsSequenceNative_MaximumOffset` | `Marshal.OffsetOf<DdsSequenceNative>("Maximum") == 0` | True |
| `DdsSequenceNative_LengthOffset` | `Marshal.OffsetOf<DdsSequenceNative>("Length") == 4` | True |
| `DdsSequenceNative_BufferOffset` | `Marshal.OffsetOf<DdsSequenceNative>("Buffer") == 8` | True |
| `DdsSequenceNative_ReleaseOffset_x64` | `Marshal.OffsetOf<DdsSequenceNative>("Release") == 16` | True |
| `DdsSequenceNative_ReleaseIsOneByte` | `typeof(DdsSequenceNative).GetField("Release").FieldType == typeof(byte)` | True |

**Design Reference:** [§3.1 Native Arena, §6.3 Sequences](MARSHAL-DESIGN.md#31-native-arena-runtime)

---

### FCDC-M002: NativeArena Implementation

**Description:** Implement the memory arena manager for marshaling variable-length data.

**File:** `src/CycloneDDS.Core/NativeArena.cs` (NEW)

**Dependencies:** FCDC-M001

**Scope:**
- **Ensure project configuration:** Verify `CycloneDDS.Core.csproj` and `CycloneDDS.Runtime.csproj` have `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` (required for all pointer operations in this architecture).
- Implement `ref struct NativeArena`
- Constructor with buffer initialization and HEAD zeroing
- `CreateString()` for UTF-8 string allocation
- `CreateSequence<T>()` for primitive sequences
- `AllocateArray<TNative>()` for complex struct arrays
- Proper alignment handling (8-byte alignment)
- Bounds checking

**Success Criteria:**
1. File created at specified location
2. `ref struct NativeArena` prevents heap escape
3. Constructor zeros HEAD region
4. All methods handle alignment correctly
5. Bounds checking throws `IndexOutOfRangeException` on overflow
6. XML documentation for all public members

**Unit Test Specifications:**

**Test File:** `tests/CycloneDDS.Core.Tests/NativeArenaTests.cs`

| Test Name | Assertion | Expected Result |
|-----------|-----------|-----------------|
| `Constructor_ZerosHeadRegion` | Buffer[0..headSize] all == 0 | True |
| `CreateString_HandlesNull` | `CreateString(null) == IntPtr.Zero` | True |
| `CreateString_EncodesUtf8` | String "Hello" → bytes match [0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x00] | True |
| `CreateString_ReturnsCorrectPointer` | Returned ptr points to start of string in buffer | True |
| `CreateString_AdvancesTail` | `_tail` increases by UTF-8 length + 1 | True |
| `CreateSequence_HandlesEmpty` | `CreateSequence(empty span)` returns default | True |
| `CreateSequence_AlignsTail` | `_tail % 8 == 0` after creating double sequence | True |
| `CreateSequence_CopiesData` | Span [1.1, 2.2] → buffer contains exact bytes | True |
| `CreateSequence_ReturnsHeader` | Header: Length=2, Buffer=correct ptr, Release=0 | True |
| `AllocateArray_AlignsTail` | `_tail % 8 == 0` after allocation | True |
| `AllocateArray_ZerosMemory` | Allocated span all bytes == 0 | True |
| `AllocateArray_ReturnsCorrectSpan` | Span points to buffer at correct offset | True |
| `BoundsCheck_ThrowsOnOverflow` | Creating string when space insufficient throws | `IndexOutOfRangeException` |

**Design Reference:** [§3.1 Native Arena](MARSHAL-DESIGN.md#31-native-arena-runtime)

---

### FCDC-M003: DdsTextEncoding Utilities

**Description:** Create UTF-8 encoding helpers for consistent string handling.

**File:** `src/CycloneDDS.Runtime/DdsTextEncoding.cs` (NEW)

**Dependencies:** None

**Scope:**
- Static helper class for string encoding/decoding
- `GetUtf8Size(string)` - calculate UTF-8 byte size + NUL
- `FromNativeUtf8(IntPtr)` - decode string from pointer
- `GetSpanFromPtr(IntPtr)` - zero-copy span access
- Handle .NET Standard 2.0 vs .NET 5+ API differences

**Success Criteria:**
1. File created at specified location
2. All methods are `static` and `[MethodImpl(AggressiveInlining)]`
3. UTF-8 encoding uses no-BOM variant
4. Cross-platform compatibility (.NET Standard 2.0)
5. XML documentation

**Unit Test Specifications:**

**Test File:** `tests/CycloneDDS.Runtime.Tests/DdsTextEncodingTests.cs`

| Test Name | Assertion | Expected Result |
|-----------|-----------|-----------------|
| `GetUtf8Size_HandlesNull` | `GetUtf8Size(null) == 0` | True |
| `GetUtf8Size_IncludesNulTerminator` | `GetUtf8Size("Hi") == 3` (2 + 1) | True |
| `GetUtf8Size_HandlesUnicode` | `GetUtf8Size("©") == 3` (UTF-8 © = 2 bytes + NUL) | True |
| `FromNativeUtf8_HandlesNull` | `FromNativeUtf8(IntPtr.Zero) == null` | True |
| `FromNativeUtf8_DecodesCorrectly` | Ptr to [0x48, 0x69, 0x00] → "Hi" | True |
| `GetSpanFromPtr_HandlesNull` | `GetSpanFromPtr(IntPtr.Zero).IsEmpty` | True |
| `GetSpanFromPtr_ReturnsCorrectSpan` | Ptr to [0x48, 0x69, 0x00] → span[0]==0x48, span[1]==0x69, Length==2 | True |

**Design Reference:** [§3.1 Native Arena, Component #7](MARSHAL-DESIGN.md#31-native-arena-runtime)

---

## Phase 2: Code Generation - Writer Path

### FCDC-M004: Ghost Struct Generation

**Description:** Generate native C-compatible structs from DSL types.

**File:** `tools/CycloneDDS.CodeGen/SerializerEmitter.cs` (MODIFY)

**Dependencies:** FCDC-M001 (for DdsSequenceNative)

**Scope:**
- Add `EmitGhostStruct(TypeInfo)` method
- Generate `internal struct {Name}_Native` with `[StructLayout(LayoutKind.Sequential)]`
- Map types correctly: primitives→primitives, strings→IntPtr, sequences→DdsSequenceNative
- Handle nested structs (embedded value types)
- Handle unions (discriminator + `[FieldOffset]` union block)
- Use `byte` for `boolean` fields (not `bool`)

**Success Criteria:**
1. Method `EmitGhostStruct` added to `SerializerEmitter`
2. Generated structs have correct `[StructLayout]` attribute
3. Boolean fields use `byte` type
4. Nested structs embed as value types
5. Unions use `[FieldOffset(0)]` for all union members
6. Generated code compiles without warnings

**Unit Test Specifications:**

**Test File:** `tests/CycloneDDS.CodeGen.Tests/SerializerEmitterTests_GhostStruct.cs`

| Test Name | Assertion | Expected Result |
|-----------|-----------|-----------------|
| `EmitGhostStruct_PrimitiveStruct` | Generated contains `internal struct Test_Native` with `int id;` | True |
| `EmitGhostStruct_HasLayoutAttribute` | Generated contains `[StructLayout(LayoutKind.Sequential)]` | True |
| `EmitGhostStruct_StringField` | String field → `public IntPtr fieldName;` | True |
| `EmitGhostStruct_SequenceField` | Sequence → `public DdsSequenceNative fieldName;` | True |
| `EmitGhostStruct_BooleanUsesBytes` | Boolean field → `public byte fieldName;` | True |
| `EmitGhostStruct_NestedStruct` | Nested struct → `public Nested_Native fieldName;` | True |
| `EmitGhostStruct_UnionHasDiscriminator` | Union → `public int _d;` field exists | True |
| `EmitGhostStruct_UnionFieldsOverlap` | Union fields have `[FieldOffset(0)]` | True |

**Design Reference:** [§3.2 Ghost Structs, §6 Type Mapping](MARSHAL-DESIGN.md#32-ghost-structs-generated)

---

### FCDC-M005: Native Sizer Generation

**Description:** Generate `GetNativeSize()` method to calculate buffer requirements.

**File:** `tools/CycloneDDS.CodeGen/SerializerEmitter.cs` (MODIFY)

**Dependencies:** FCDC-M004

**Scope:**
- Add `EmitNativeSizer(TypeInfo)` method
- Generate `public static int GetNativeSize(in {Type} source)`
- Calculate `Unsafe.SizeOf<T_Native>()` for HEAD
- Add UTF-8 string sizes (+ NUL terminator)
- Add sequence sizes (with alignment)
- Recursive calculation for nested structs
- Handle optional fields (only count if present)

**Success Criteria:**
1. Method `EmitNativeSizer` added to `SerializerEmitter`
2. Generated method returns correct size for all types
3. String size includes UTF-8 encoding + NUL
4. Sequence alignment (8-byte) included
5. Handles null strings/collections gracefully
6. Generated code compiles

**Unit Test Specifications:**

**Test File:** `tests/CycloneDDS.CodeGen.Tests/SerializerEmitterTests_Sizer.cs`

| Test Name | Assertion | Expected Result |
|-----------|-----------|-----------------|
| `GetNativeSize_Primitive` | `GetNativeSize(new Test{Id=1})` == `sizeof(Test_Native)` | True |
| `GetNativeSize_String` | `GetNativeSize(new Test{Msg="Hi"})` == structSize + 3 | True |
| `GetNativeSize_NullString` | `GetNativeSize(new Test{Msg=null})` == structSize | True |
| `GetNativeSize_Sequence` | `GetNativeSize(new Test{Data=[1.1,2.2]})` == structSize + aligned(16) | True |
| `GetNativeSize_EmptySequence` | `GetNativeSize(new Test{Data=[]})` == structSize | True |
| `GetNativeSize_NullSequence` | `GetNativeSize(new Test{Data=null})` == structSize | True |
| `GetNativeSize_Nested` | Nested struct contributes dynamic size | True |
| `GetNativeSize_Optional` | Optional field only counted if HasValue | True |

**Design Reference:** [§3.3.1 GetNativeSize, §4.1 Write Path](MARSHAL-DESIGN.md#331-getnativesize)

---

### FCDC-M006: Marshaller Generation

**Description:** Generate `MarshalToNative()` method to populate ghost structs.

**File:** `tools/CycloneDDS.CodeGen/SerializerEmitter.cs` (MODIFY)

**Dependencies:** FCDC-M004, FCDC-M005

**Scope:**
- Add `EmitMarshaller(TypeInfo)` method
- Generate `internal static void MarshalToNative(in {Type} source, ref {Type}_Native target, ref NativeArena arena)`
- Primitives: direct assignment
- Strings: `arena.CreateString()`
- Sequences (primitives): `arena.CreateSequence()` with span
- Sequences (structs): loop + recursive marshal
- Nested structs: recursive call
- Unions: switch on discriminator
- Optional fields: check HasValue, allocate with `AllocateOne()`

**Success Criteria:**
1. Method `EmitMarshaller` added
2. Generated method populates all fields correctly
3. Primitives assigned directly
4. Strings allocated in arena
5. Sequences use efficient block copy for primitives
6. Complex sequences use loop + recursion
7. Generated code compiles

**Unit Test Specifications:**

**Test File:** `tests/CycloneDDS.CodeGen.Tests/SerializerEmitterTests_Marshaller.cs`

| Test Name | Assertion | Expected Result |
|-----------|-----------|-----------------|
| `MarshalToNative_Primitive` | `target.id == source.Id` | True |
| `MarshalToNative_String` | `target.namePtr` points to UTF-8 "Hello\0" in arena | True |
| `MarshalToNative_NullString` | `target.namePtr == IntPtr.Zero` | True |
| `MarshalToNative_Sequence` | `target.seq.Length == 2`, Buffer points to data | True |
| `MarshalToNative_SequenceData` | Buffer contains correct double bytes | True |
| `MarshalToNative_EmptySequence` | `target.seq.Length == 0`, `Buffer == IntPtr.Zero` | True |
| `MarshalToNative_NestedStruct` | Nested fields populated correctly | True |
| `MarshalToNative_Union` | Discriminator set, active field populated | True |
| `MarshalToNative_Optional_Present` | Pointer non-zero, data allocated | True |
| `MarshalToNative_Optional_Absent` | Pointer == IntPtr.Zero | True |

**Design Reference:** [§3.3.2 MarshalToNative, §7 Special Cases](MARSHAL-DESIGN.md#332-marshaltonative)

---

### FCDC-M007: Key Marshaller Generation

**Description:** Generate sparse marshallers for keyed topic operations.

**File:** `tools/CycloneDDS.CodeGen/SerializerEmitter.cs` (MODIFY)

**Dependencies:** FCDC-M006

**Scope:**
- Add `EmitKeyNativeSizer(TypeInfo)` - size for key fields only
- Add `EmitKeyMarshaller(TypeInfo)` - marshal key fields only
- Reuse full `T_Native` struct (preserves offsets)
- Only populate fields marked with `[DdsKey]`
- Non-key fields remain zeroed

**Success Criteria:**
1. Methods added: `EmitKeyNativeSizer`, `EmitKeyMarshaller`
2. Generated `GetKeyNativeSize()` only sums key field sizes
3. Generated `MarshalKeyToNative()` only populates key fields
4. Non-key fields left as default (zero)
5. Works for keyed and non-keyed topics (gracefully degrades)

**Unit Test Specifications:**

**Test File:** `tests/CycloneDDS.CodeGen.Tests/SerializerEmitterTests_KeyMarshaller.cs`

| Test Name | Assertion | Expected Result |
|-----------|-----------|-----------------|
| `GetKeyNativeSize_OnlyCountsKeys` | Size = structSize + keyStringSize (no payload) | True |
| `MarshalKeyToNative_PopulatesKeys` | Key fields == source values | True |
| `MarshalKeyToNative_IgnoresPayload` | Non-key fields == 0 or IntPtr.Zero | True |
| `KeyMarshaller_SingleKeyField` | Works with 1 key | True |
| `KeyMarshaller_CompositeKey` | Works with multiple keys | True |
| `KeyMarshaller_NoKeys` | Gracefully handles non-keyed topics | True |

**Design Reference:** [§7.1 Keyed Topics](MARSHAL-DESIGN.md#71-keyed-topics)

---

## Phase 3: Code Generation - Reader Path

### FCDC-M008: View Struct Generation

**Description:** Generate zero-copy view structs for reading native data.

**File:** `tools/CycloneDDS.CodeGen/DeserializerEmitter.cs` (MODIFY)

**Dependencies:** FCDC-M004

**Scope:**
- Add `EmitViewStruct(TypeInfo)` method
- Generate `public ref struct {Name}View`
- Private `unsafe {Name}_Native* _ptr` field
- Internal constructor accepting pointer
- Primitives: direct dereference properties
- Strings: dual properties (Span<byte> raw, string allocated)
- Sequences (primitives): ReadOnlySpan<T> properties
- Sequences (structs): indexer/enumerator pattern
- Nested structs: return nested view
- Handle IntPtr.Zero gracefully (null strings, empty sequences)

**Success Criteria:**
1. Method `EmitViewStruct` added
2. Generated struct is `ref struct`
3. Primitives accessible via properties
4. Strings have both raw and allocated accessors
5. Sequences return ReadOnlySpan for primitives
6. Complex sequences accessible via indexer
7. Null-safe (checks IntPtr.Zero)

**Unit Test Specifications:**

**Test File:** `tests/CycloneDDS.CodeGen.Tests/DeserializerEmitterTests_View.cs`

| Test Name | Assertion | Expected Result |
|-----------|-----------|-----------------|
| `ViewStruct_IsRefStruct` | `typeof(TestView).IsByRefLike` | True |
| `ViewStruct_HasPrivatePointer` | Field `_ptr` exists and is `private` | True |
| `ViewProperty_Primitive` | `view.Id` returns correct value from native struct | True |
| `ViewProperty_String` | `view.Name` returns correct string | True |
| `ViewProperty_StringRaw` | `view.NameRaw` returns ReadOnlySpan with UTF-8 bytes | True |
| `ViewProperty_NullString` | `view.Name` returns null when ptr == IntPtr.Zero | True |
| `ViewProperty_Sequence` | `view.Data` returns ReadOnlySpan with correct elements | True |
| `ViewIndexer_ComplexSequence` | `view.GetItem(0)` returns correct nested view | True |
| `ViewProperty_Nested` | `view.Inner` returns nested view | True |

**Design Reference:** [§3.4 View Structs, Component #2](MARSHAL-DESIGN.md#34-view-structs-generated)

---

### FCDC-M009: ToManaged Generation

**Description:** Generate deep-copy methods to convert views to managed objects.

**File:** `tools/CycloneDDS.CodeGen/DeserializerEmitter.cs` (MODIFY)

**Dependencies:** FCDC-M008

**Scope:**
- Add `EmitToManaged(TypeInfo)` method
- Generate `public static {Type} ToManaged(in {Type}View view)`
- Primitives: copy values
- Strings: allocate via view property (calls PtrToStringUTF8)
- Sequences (primitives): `.ToArray()` → `new List<T>()`
- Sequences (structs): loop + recursive `ToManaged`
- Nested structs: recursive call
- Unions: switch on discriminator

**Success Criteria:**
1. Method `EmitToManaged` added
2. Generated method creates independent managed object
3. All fields copied correctly
4. Strings allocated (not sharing pointer)
5. Collections allocated and populated
6. Nested structs recursively copied

**Unit Test Specifications:**

**Test File:** `tests/CycloneDDS.CodeGen.Tests/DeserializerEmitterTests_ToManaged.cs`

| Test Name | Assertion | Expected Result |
|-----------|-----------|-----------------|
| `ToManaged_Primitive` | `managed.Id == view.Id` | True |
| `ToManaged_String` | `managed.Name == view.Name` (allocated independently) | True |
| `ToManaged_Sequence` | `managed.Data.Count == view.Data.Length` | True |
| `ToManaged_SequenceData` | `managed.Data[i] == view.Data[i]` for all i | True |
| `ToManaged_EmptySequence` | `managed.Data.Count == 0` | True |
| `ToManaged_NestedStruct` | `managed.Inner.Value == view.Inner.Value` | True |
| `ToManaged_ComplexSequence` | `managed.Items[i].Field == view.GetItem(i).Field` | True |
| `ToManaged_Union` | Discriminator and active field copied | True |

**Design Reference:** [§3.4 View Structs, Component #3](MARSHAL-DESIGN.md#34-view-structs-generated)

---

## Phase 4: Runtime Integration - Writer

### FCDC-M010: DdsWriter Marshaling Integration

**Description:** Update DdsWriter to use NativeArena and marshaling instead of CDR.

**File:** `src/CycloneDDS.Runtime/DdsWriter.cs` (MODIFY)

**Dependencies:** FCDC-M002, FCDC-M006

**Scope:**
- Remove `CdrWriter` usage
- Replace with `NativeArena` marshaling
- Update `Write(T sample)` method:
  - Call `GetNativeSize(sample)`
  - Rent from `ArrayPool<byte>`
  - Pin with `fixed`
  - Initialize `NativeArena`
  - Cast buffer to `T_Native*`
  - Call `MarshalToNative`
  - Call `dds_write` (not `dds_writecdr`)
  - Return buffer to pool
- Add threshold check for large samples (1MB → use GC.AllocateUninitializedArray)
- Update delegates to match new signatures

**Success Criteria:**
1. `Write()` method updated
2. Uses `NativeArena` for marshaling
3. Calls `dds_write` with native struct pointer
4. Zero allocations in steady state (verified)
5. Large sample handling (≥1MB)
6. ArrayPool integration correct

**Unit Test Specifications:**

**Test File:** `tests/CycloneDDS.Runtime.Tests/DdsWriterTests_Marshaling.cs`

| Test Name | Assertion | Expected Result |
|-----------|-----------|-----------------|
| `Write_CallsGetNativeSize` | Sizer delegate invoked | True |
| `Write_RentsFromArrayPool` | ArrayPool.Rent called (via mock or instrumentation) | True |
| `Write_PinsBuffer` | fixed block used (code inspection) | True |
| `Write_CallsMarshalToNative` | Marshaller delegate invoked with correct args | True |
| `Write_CallsDdsWrite` | `dds_write` P/Invoke called (not `dds_writecdr`) | True |
| `Write_ReturnsBufferToPool` | ArrayPool.Return called | True |
| `Write_ZeroAllocation_SteadyState` | Second write allocates 0 bytes | True |
| `Write_LargeSample_UsesGC` | Sample ≥1MB uses GC.AllocateUninitializedArray | True |
| `Write_ThrowsOnError` | Negative return from dds_write throws DdsException | True |

**Design Reference:** [§4.1 Write Path, §3.5 DdsLoan](MARSHAL-DESIGN.md#41-complete-sequence)

---

### FCDC-M011: Key Operation Integration

**Description:** Implement `DisposeInstance` and `UnregisterInstance` using key marshallers.

**File:** `src/CycloneDDS.Runtime/DdsWriter.cs` (MODIFY)

**Dependencies:** FCDC-M007, FCDC-M010

**Scope:**
- Add `DisposeInstance(in T keySample)` method
- Add `UnregisterInstance(in T keySample)` method
- Use `GetKeyNativeSize` for sizing
- Use `MarshalKeyToNative` for marshaling
- Zero HEAD region before marshaling (sparse population)
- Call `dds_dispose` / `dds_unregister_instance`

**Success Criteria:**
1. Methods added: `DisposeInstance`, `UnregisterInstance`
2. Use key marshallers (not full marshaller)
3. HEAD region zeroed before marshaling
4. Call correct P/Invoke methods
5. Works for keyed topics
6. Gracefully handles non-keyed topics

**Unit Test Specifications:**

**Test File:** `tests/CycloneDDS.Runtime.Tests/DdsWriterTests_KeyOperations.cs`

| Test Name | Assertion | Expected Result |
|-----------|-----------|-----------------|
| `DisposeInstance_CallsKeyMarshaller` | `MarshalKeyToNative` invoked (not full marshaller) | True |
| `DisposeInstance_ZerosHead` | HEAD region all zeros before marshal | True |
| `DisposeInstance_CallsDdsDispose` | `dds_dispose` P/Invoke called | True |
| `DisposeInstance_UsesSmallBuffer` | Buffer size == key size (not full size) | True |
| `UnregisterInstance_CallsDdsUnregister` | `dds_unregister_instance` P/Invoke called | True |
| `KeyOperation_NonKeyedTopic` | Works gracefully (marshals full struct) | True |

**Design Reference:** [§7.1 Keyed Topics](MARSHAL-DESIGN.md#71-keyed-topics)

---

## Phase 5: Runtime Integration - Reader

### FCDC-M012: DdsLoan Implementation

**Description:** Create loan manager for native memory lifecycle.

**File:** `src/CycloneDDS.Runtime/DdsLoan.cs` (NEW)

**Dependencies:** None

**Scope:**
- Create `DdsLoan<TView>` ref struct implementing `IDisposable`
- Hold `void** samples`, `dds_sample_info_t* infos`, `int count`, `dds_entity_t reader`
- Constructor accepting reader handle and native arrays
- `Dispose()` calls `dds_return_loan` and returns arrays to ArrayPool
- Custom `ref struct` enumerator for zero-alloc iteration
- Enumerator returns `DdsSample<TView>` (a struct containing `TView Data` and `DdsSampleInfo Info`)
- This allows idiomatic syntax: `foreach (var sample in loan) { if (sample.Info.ValidData) { var data = sample.Data; ... } }`
- `Data` property only valid when `Info.ValidData == true`

**Success Criteria:**
1. File created as `DdsLoan<TView>` ref struct
2. Implements `IDisposable`
3. `Dispose()` calls `dds_return_loan`
4. Enumerator is `ref struct` returning `DdsSample<TView>`
5. `DdsSample<TView>` has `Data` and `Info` properties
6. Arrays returned to pool on disposal

**Unit Test Specifications:**

**Test File:** `tests/CycloneDDS.Runtime.Tests/DdsLoanTests.cs`

| Test Name | Assertion | Expected Result |
|-----------|-----------|-----------------|
| `DdsLoan_ImplementsIDisposable` | `typeof(IDisposable).IsAssignableFrom(typeof(DdsLoan))` | True |
| `Dispose_CallsReturnLoan` | `dds_return_loan` P/Invoke called (via mock) | True |
| `Dispose_ReturnsArrays` | ArrayPool.Return called for samples and infos | True |
| `Dispose_Idempotent` | Multiple Dispose() calls safe | True |
| `GetEnumerator_ReturnsRefStruct` | `typeof(Enumerator).IsByRefLike` | True |
| `Enumerator_YieldsSampleRefs` | Iteration produces DdsSampleRef instances | True |
| `Enumerator_IteratesCorrectCount` | Enumerates exactly `_length` times | True |
| `SampleRef_HasDataPtr` | `sampleRef.DataPtr == samples[i]` | True |
| `SampleRef_HasInfo` | `sampleRef.Info` references correct info | True |

**Design Reference:** [§3.5 DdsLoan, §5.1 Read Path](MARSHAL-DESIGN.md#35-ddsloan-runtime)

---

### FCDC-M013: DdsReader View Integration

**Description:** Update DdsReader to use dds_take and return loans.

**File:** `src/CycloneDDS.Runtime/DdsReader.cs` (MODIFY)

**Dependencies:** FCDC-M008, FCDC-M012

**Scope:**
- Remove CDR deserialization logic
- Update `Read()` / `Take()` methods:
  - Rent `IntPtr[]` and `DdsSampleInfo[]` from pool
  - Call `dds_take` (not `dds_read_wl` with serdata)
  - Wrap result in `DdsLoan`
  - Return loan
- Add extension methods for casting to views (optional, for ergonomics)
- Update `ReadAsync()` / `TakeAsync()` similarly

**Success Criteria:**
1. `Read()` / `Take()` methods updated
2. Use `dds_take` native call
3. Return `DdsLoan<TView>` instance
4. Zero allocations except loan wrapper
5. Arrays rented from pool
6. Enumerator yields `DdsSample<TView>` with `Data` and `Info`

**Unit Test Specifications:**

**Test File:** `tests/CycloneDDS.Runtime.Tests/DdsReaderTests_Views.cs`

| Test Name | Assertion | Expected Result |
|-----------|-----------|-----------------|
| `Read_CallsDdsTake` | `dds_take` P/Invoke called (not serdata variant) | True |
| `Read_RentsArrays` | ArrayPool.Rent called for IntPtr[] and DdsSampleInfo[] | True |
| `Read_ReturnsLoan` | Return type is `DdsLoan` | True |
| `Read_LoanContainsSamples` | Loan.Length == count returned by dds_take | True |
| `Read_ViewAccessWorks` | Can cast sample to view and access fields | True |
| `Read_ZeroAllocation` | No allocations except loan wrapper | True |
| `Take_SameAsRead` | Take behaves identically to Read | True |
| `ReadAsync_ReturnsTask` | Method signature returns `Task<DdsLoan>` | True |

**Design Reference:** [§5.1 Read Path](MARSHAL-DESIGN.md#51-complete-sequence)

---

## Phase 6: Cleanup & Migration

### FCDC-M014: Legacy Code Removal

**Description:** Remove CDR-based serialization components no longer needed.

**Files:** Multiple (DELETE)

**Dependencies:** FCDC-M010, FCDC-M013 (runtime fully migrated)

**Scope:**
- Delete `src/CycloneDDS.Core/CdrWriter.cs`
- Delete `src/CycloneDDS.Core/CdrReader.cs`
- Delete `src/CycloneDDS.Core/AlignmentMath.cs`
- Delete `src/CycloneDDS.Core/CdrSizer.cs`
- Remove CDR logic from `SerializerEmitter.cs` and `DeserializerEmitter.cs`
- Update project files to remove deleted files
- Update any remaining references

**Success Criteria:**
1. Files deleted successfully
2. Solution builds without errors
3. No references to deleted types remain
4. Project files updated
5. All tests pass (except legacy CDR tests)

**Unit Test Specifications:**

**Test File:** N/A (verification via build + test suite)

| Test Name | Assertion | Expected Result |
|-----------|-----------|-----------------|
| `Solution_Builds` | `dotnet build` succeeds | Exit code 0 |
| `No_CdrWriter_References` | Grep for "CdrWriter" finds 0 matches (except comments) | 0 matches |
| `No_CdrReader_References` | Grep for "CdrReader" finds 0 matches (except comments) | 0 matches |
| `AllTests_Pass` | `dotnet test` succeeds | All tests pass |

**Design Reference:** [§9.1 What's Removed](MARSHAL-DESIGN.md#91-whats-removed)

---

### FCDC-M015: Test Suite Migration

**Description:** Update existing tests to work with native marshaling.

**Files:** Various test files (MODIFY)

**Dependencies:** FCDC-M014

**Scope:**
- Update Golden Rig tests (byte comparison against C output)
- Remove CDR byte inspection tests (no longer applicable)
- Update roundtrip tests (C# write → native C read → C# read)
- Add new layout validation tests (Marshal.OffsetOf checks)
- Update performance tests (allocation tracking)
- Add interop tests (C# writer → C++ reader, vice versa)

**Success Criteria:**
1. Golden Rig tests verify byte-perfect output
2. Roundtrip tests verify data integrity
3. Layout tests verify struct offsets match JSON
4. Performance tests verify zero-alloc goal
5. All tests pass
6. Test coverage ≥80% for new code

**Unit Test Specifications:**

**Test File:** Multiple

| Category | Test Count | Focus |
|----------|------------|-------|
| Golden Rig | 8+ | Byte-perfect native compatibility |
| Layout Validation | 10+ | Struct offsets match `idlc` JSON |
| Roundtrip | 15+ | Data integrity through marshal/unmarshal |
| Performance | 5+ | Zero-alloc verification |
| Interop | 10+ | C#/C/C++ cross-language tests |

**Design Reference:** [§10.3 Testing Strategy](MARSHAL-DESIGN.md#103-testing-strategy)

---

### FCDC-M016: Documentation Update

**Description:** Update documentation to reflect new architecture.

**Files:** Various .md files (MODIFY)

**Dependencies:** All implementation tasks complete

**Scope:**
- Update README.md with new architecture overview
- Update user guide with new API usage
- Add migration guide for existing users
- Update API reference
- Add troubleshooting section
- Update performance characteristics

**Success Criteria:**
1. README.md updated with correct architecture
2. User guide includes marshaling examples
3. Migration guide provided
4. API reference accurate
5. Troubleshooting section covers common issues
6. Performance section updated with benchmarks

**Deliverables:**
- `README.md` (UPDATED)
- `docs/USER-GUIDE.md` (UPDATED)
- `docs/MIGRATION-GUIDE.md` (NEW)
- `docs/API-REFERENCE.md` (UPDATED)
- `docs/TROUBLESHOOTING.md` (UPDATED)
- `docs/PERFORMANCE.md` (UPDATED)

**Design Reference:** [All sections](MARSHAL-DESIGN.md)

---

## Validation Gates

### GATE-1: Foundation Validation (After Phase 1)

**Criteria:**
- All Phase 1 tests pass (25+ tests)
- NativeArena allocation works correctly
- String encoding/decoding verified
- DdsSequenceNative layout matches C

**Validation Method:**
- Run `dotnet test --filter "Phase1"`
- Compare struct sizes on x64 and x86
- Verify arena allocation pattern with debugger

---

### GATE-2: CodeGen Validation (After Phase 2-3)

**Criteria:**
- Ghost structs compile
- Generated marshaller produces correct native layout
- Generated views compile
- Layout tests pass (offsets match JSON)

**Validation Method:**
- Run code generator on test schemas
- Compile generated code
- Run layout validation tests
- Visual inspection of generated code

---

### GATE-3: Integration Validation (After Phase 4-5)

**Criteria:**
- Write path works (C# → native)
- Read path works (native → C#)
- Roundtrip test passes (C# → native → C#)
- Zero-alloc goal verified

**Validation Method:**
- Run integration tests
- Use BenchmarkDotNet for allocation tracking
- Golden Rig test (compare C output)
- Interop test (C# ↔ C/C++)

---

### GATE-4: Production Readiness (After Phase 6)

**Criteria:**
- All tests pass (200+ tests)
- Performance benchmarks meet targets
- Documentation complete
- No known bugs
- Code review complete

**Validation Method:**
- Full test suite run
- Performance regression tests
- Documentation review
- Code review by team
- User acceptance testing

---

## Success Metrics

### Overall Project Success

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| **Test Pass Rate** | 100% | `dotnet test` |
| **Code Coverage** | ≥80% | Coverlet report |
| **Performance (Write)** | ≥95% of pure C | Benchmark comparison |
| **Performance (Read)** | ≥95% of pure C | Benchmark comparison |
| **Allocations** | 0 (steady state) | BenchmarkDotNet |
| **Golden Rig** | Byte-perfect match | Binary comparison |
| **Interop** | 100% data integrity | C#/C/C++ roundtrip |
| **Build Time** | ≤+10% vs current | Build duration |
| **Doc Completeness** | 100% public APIs | Manual review |

---

## Risk Register

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| ABI mismatch (C# vs C) | High | Medium | Layout validation tests, Golden Rig |
| 32-bit/64-bit issues | High | Low | Platform-specific tests |
| Alignment bugs | High | Medium | Zero-init HEAD, validation tests |
| Performance regression | Medium | Low | Continuous benchmarking |
| Breaking API changes | High | Low | Maintain API surface |
| Test migration effort | Medium | Medium | Phased approach |
| Documentation gaps | Low | Medium | Review checklist |

---

**End of Document**
