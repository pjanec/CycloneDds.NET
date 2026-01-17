# BATCH-10: Optional Members Support

**Batch Number:** BATCH-10  
**Tasks:** FCDC-S014 (Optional Members Support)  
**Phase:** Stage 2 - Code Generation (Optional Fields)  
**Estimated Effort:** 6-8 hours  
**Priority:** MEDIUM  
**Dependencies:** BATCH-09 (Union support complete)

---

## 📋 Onboarding & Workflow

### Developer Instructions

This batch implements code generation for optional struct members using **nullable types (`?`)**. Optional members can be null/unset and affect wire format compatibility.

**Your Mission:**  
Extend serializer/deserializer emitters to support optional fields (detected via `?` type suffix) with XCDR2 EMHEADER (Extension Member Header) encoding.

**Critical Context:**
- Optional members = fields with `?` type suffix (`int?`, `string?`)
- Optional members use EMHEADER (4 bytes before field) when present
- EMHEADER contains: `[Must Understand: 1 bit][Length: 28 bits][Member ID: 3 bits (unused in appendable)]`
- Absent optionals skip EMHEADER entirely (zero bytes on wire)
- Enables adding optional fields without breaking old readers

### ⚠️⚠️⚠️ CRITICAL TEST REQUIREMENT ⚠️⚠️⚠️

**YOU MUST RUN ALL TESTS, NOT JUST NEW OPTIONAL TESTS**

```bash
dotnet test   # Must show: total: 112+; failed: 0
```

**UNACCEPTABLE:**
- ❌ "My new optional tests pass" (but Core/Schema tests fail)
- ❌ Only running CodeGen tests
- ❌ Any test failures anywhere in the solution

**RE QUIRED:**
- ✅ **ALL 112+ tests must pass** (Core + Schema + CodeGen combined)
- ✅ Zero regression in existing tests
- ✅ No build warnings introduced

**Report must include:** Full `dotnet test` output showing all tests passing.

### Required Reading (IN ORDER)

1. **Previous Reviews:**
   - `.dev-workstream/reviews/BATCH-09.2-REVIEW.md` - Union pattern
   - `.dev-workstream/reviews/BATCH-08-REVIEW.md` - Deserializer pattern
   - `.dev-workstream/reviews/BATCH-07-REVIEW.md` - Variable types
2. **Task Master:** `docs/SERDATA-TASK-MASTER.md` - **READ FCDC-S014**
3. **Design Document:** `docs/SERDATA-DESIGN.md` - Section 4.6 (Optionals)
4. **XCDR2 Spec:** Section 7.4.3.4.3 (Optional Members)

### Source Code Location

- **CLI Tool:** `tools/CycloneDDS.CodeGen/` (extend `SerializerEmitter.cs` and `DeserializerEmitter.cs`)
- **Test Project:** `tests/CycloneDDS.CodeGen.Tests/`

### Report Submission

**⚠️ CRITICAL: REPORT FOLDER LOCATION ⚠️**

**Submit to:** `.dev-workstream/reports/BATCH-10-REPORT.md`

**NOT:** `.dev-workstream/reports/old_implem/...` or anywhere else!

---

## Context

**BATCH-09 Complete:** Union support with DHEADER, byte-perfect C/C# interop verified.

**This Batch:** Add optional member support.

**Related Tasks:**
- [FCDC-S014](../docs/SERDATA-TASK-MASTER.md#fcdc-s014-optional-members-support)

**What Optionals Are:**
```csharp
[DdsTopic]
public struct SensorConfig
{
    public int Id;                    // Required (non-nullable)
    
    public string? Name;              // Optional (nullable reference type)
    
    public double? Threshold;         // Optional (nullable value type)
}
```

**Detection:** Fields with `?` type suffix are treated as optional.

**XCDR2 Optional Serialization:**
- If present: `[EMHEADER: 4 bytes] [Field Value]`
- If absent: (nothing - zero bytes)
- EMHEADER format: `[M: 1 bit][Length: 28 bits][ID: 3 bits]`

---

## 🎯 Batch Objectives

**Primary Goal:** Generate serialization/deserialization code for optional members.

**Success Metrics:**
- Generated code compiles
- Optionals serialize correctly (EMHEADER when present, nothing when absent)
- Optionals deserialize correctly
- **ALL 112+ tests passing** (no regressions)

---

## ✅ Task 1: Optional Member Detection

**Files:** Modify `SerializerEmitter.cs` and `DeserializerEmitter.cs`

### Detection Logic

**Context:** We're working with `CodeGen.FieldInfo` where `TypeName` is a **string parsed from schema**, NOT runtime reflection.

```csharp
private bool IsOptional(FieldInfo field)
{
    // TypeName is a string from schema (e.g., "int?", "string?")
    // Check if type ends with '?' character
    return field.TypeName.EndsWith("?");
}
```

**This works because:**
- `FieldInfo` is `CodeGen.FieldInfo` (line 19 of TypeInfo.cs)
- `TypeName` is `string` property (line 22 of TypeInfo.cs)
- Schema parser populates TypeName with "int?", "string?", etc.

**No attribute needed!** The `?` suffix in TypeName is explicit.

### ⚠️ Note: Runtime Reflection is Different

**If you were doing runtime reflection** (NOT applicable in this batch), you'd need:

```csharp
// FOR RUNTIME REFLECTION ONLY (not used in CodeGen)
public static bool IsMarkedAsNullable(System.Reflection.FieldInfo field)
{
    // Value types: int? is Nullable<int>
    if (field.FieldType.IsValueType)
        return Nullable.GetUnderlyingType(field.FieldType) != null;
    
    // Reference types: Check NullableAttribute
    var nullableAttr = field.GetCustomAttributes()
        .FirstOrDefault(a => a.GetType().FullName == 
            "System.Runtime.CompilerServices.NullableAttribute");
    
    if (nullableAttr != null)
    {
        var flags = (byte[])nullableAttr.GetType()
            .GetField("NullableFlags").GetValue(nullableAttr);
        return flags != null && flags.Length > 0 && flags[0] == 2;
    }
    
    return false;
}
```

**But in our CodeGen context, we parse schema strings, so `EndsWith("?")` is sufficient.**

### Type Unwrapping

For nullable value types (`int?`, `double?`), unwrap to base type:

```csharp
private string GetBaseType(string typeName)
{
    if (typeName.EndsWith("?"))
        return typeName.TrimEnd('?');
    return typeName;
}
```

---

## ✅ Task 2: Serialization with EMHEADER

### Generated Serialization Pattern

**For optional field:**

```csharp
// Optional int? field
if (this.OptionalValue.HasValue)
{
    // EMHEADER: [MustUnderstand=0][Length=4][MemberID=0]
    // For int: length = 4 bytes
    uint emheader = 0x00000004; // Length = 4, M=0, ID=0
    writer.WriteUInt32(emheader);
    writer.WriteInt32(this.OptionalValue.Value);
}
// If null, write nothing

// Optional string? field  
if (this.OptionalName != null)
{
    // Calculate string length first
    int strLen = 4 + Encoding.UTF8.GetByteCount(this.OptionalName) + 1;
    uint emheader = (uint)strLen; // Length includes string header + bytes + NUL
    writer.WriteUInt32(emheader);
    writer.WriteString(this.OptionalName);
}
```

### EMHEADER Calculation

**Format:** `[M:1bit][Length:28bits][ID:3bits]`

For appendable types, simplified:
- M (Must Understand) = 0
- Length = size of field value in bytes
- ID = 0 (unused in appendable)

**Formula:** `EMHEADER = Length << 3` (shift left 3 bits for ID field)

**Examples:**
- `int` (4 bytes): `0x00000004` (no shift for appendable)
- `double` (8 bytes): `0x00000008`
- `string "Hello"` (4 + 6 = 10 bytes): `0x0000000A`

### GetSerializedSize for Optionals

```csharp
// For optional int?
if (this.OptionalValue.HasValue)
{
    sizer.WriteUInt32(0); // EMHEADER
    sizer.WriteInt32(0);  // Value
}
// Else: nothing

// For optional string?
if (this.OptionalName != null)
{
    sizer.WriteUInt32(0); // EMHEADER
    sizer.WriteString(this.OptionalName); // Actual string
}
```

---

## ✅ Task 3: Deserialization with EMHEADER

### Generated Deserialization Pattern

**For View struct:**

```csharp
public readonly ref struct SensorConfigView
{
    private readonly ReadOnlySpan<byte> _buffer;
    private readonly int _offset;
    
    // Required field (always present)
    public int Id => BinaryPrimitives.ReadInt32LittleEndian(_buffer.Slice(_offset + 4, 4));
    
    // Optional nullable field
    public int? OptionalValue
    {
        get
        {
            int pos = _offset + 8; // After DHEADER + Id
            
            // Read EMHEADER
            if (pos + 4 > _buffer.Length)
                return null; // End of buffer, field absent
            
            uint emheader = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Slice(pos, 4));
            if (emheader == 0)
                return null; // Field absent (or next required field starts)
            
            // EMHEADER present, read value
            return BinaryPrimitives.ReadInt32LittleEndian(_buffer.Slice(pos + 4, 4));
        }
    }
}
```

**Challenge:** Determining field presence without knowing full struct layout ahead of time.

**Solution:** Track position while reading, check if EMHEADER exists at expected location.

### ToOwned() for Optionals

```csharp
public SensorConfig ToOwned()
{
    return new SensorConfig
    {
        Id = this.Id,
        OptionalValue = this.OptionalValue, // Copies nullable
        OptionalName = this.OptionalName.HasValue 
            ? Encoding.UTF8.GetString(this.OptionalNameUtf8) 
            : null
    };
}
```

---

## ✅ Task 4: Tests Required

**Minimum 10-12 tests:**

### Code Generation Tests (4-5 tests)
1. ✅ Detects nullable types (`?` suffix)
2. ✅ Generates EMHEADER write for present optional
3. ✅ Skips write for absent optional
4. ✅ Handles nullable value types (`int?`, `double?`)
5. ✅ Handles nullable reference types (`string?`)

### Execution Tests (6-7 tests - CRITICAL)
6. ✅ **Optional present serializes with EMHEADER**
7. ✅ **Optional absent serializes as zero bytes**
8. ✅ **Roundtrip: present → serialize → deserialize → matches**
9. ✅ **Roundtrip: absent → serialize → deserialize → null**
10. ✅ **Mixed: some present, some absent**
11. ✅ **View correctly detects presence/absence**
12. ✅ **ToOwned() handles null optionals**

### Regression Tests (CRITICAL)
13. ✅ **ALL 112+ existing tests still pass**

**Quality Standard:**

**✅ REQUIRED:**
- Tests MUST compile generated code (Roslyn)
- Tests MUST verify EMHEADER presence when field set
- Tests MUST verify zero bytes when field null
- Tests MUST verify roundtrip with mixed optionals
- **Tests MUST NOT break any existing tests**

**Example GOOD Test:**

```csharp
[Fact]
public void Optional_Present_SerializesWithEMHEADER()
{
    var type = new TypeInfo
    {
        Name = "OptionalData",
        Fields = new[]
        {
            new FieldInfo { Name = "Id", TypeName = "int" },   // Required (no ?)
            new FieldInfo { Name = "Value", TypeName = "int?" } // Optional (has ?)
        }
    };
    
    // Generate & compile
    var assembly = CompileToAssembly(...);
    
    // Test with optional PRESENT
    var data = Activator.CreateInstance(assembly.GetType("OptionalData"));
    data.GetType().GetField("Id").SetValue(data, 123);
    data.GetType().GetField("Value").SetValue(data, 456); // Set optional
    
    // Serialize
    var writer = new ArrayBufferWriter<byte>();
    var cdr = new CdrWriter(writer);
    data.GetType().GetMethod("Serialize").Invoke(data, new object[] { cdr });
    cdr.Complete();
    
    byte[] bytes = writer.WrittenSpan.ToArray();
    
    // Verify structure:
    // [DHEADER: 4] [Id: 4] [EMHEADER: 4] [Value: 4] = 16 bytes
    Assert.Equal(16, bytes.Length);
    
    // Verify EMHEADER exists (at position 8)
    uint emheader = BitConverter.ToUInt32(bytes, 8);
    Assert.Equal(0x00000004u, emheader); // Length = 4 for int
}

[Fact]
public void Optional_Absent_SerializesAsZeroBytes()
{
    // Same type as above
    var data = Activator.CreateInstance(assembly.GetType("OptionalData"));
    data.GetType().GetField("Id").SetValue(data, 123);
    // Value NOT set (null)
    
    // Serialize
    var writer = new ArrayBufferWriter<byte>();
    var cdr = new CdrWriter(writer);
    data.GetType().GetMethod("Serialize").Invoke(data, new object[] { cdr });
    cdr.Complete();
    
    byte[] bytes = writer.WrittenSpan.ToArray();
    
    // Verify structure:
    // [DHEADER: 4] [Id: 4] = 8 bytes (NO EMHEADER, NO Value)
    Assert.Equal(8, bytes.Length);
}
```

**Estimated Time:** 6-8 hours

---

## 🧪 Testing Requirements

**Minimum Total Tests:** 10-12 new tests

**Test Distribution:**
- Code Generation: 4-5 tests
- Execution (roundtrip with optionals): 6-7 tests

**⚠️⚠️⚠️ CRITICAL: ALL TESTS MUST PASS ⚠️⚠️⚠️**

```bash
dotnet test   # Must show: total: 122-124; failed: 0; succeeded: 122-124
```

**Your report MUST include full test output.**

---

## 📊 Report Requirements

**Submit to:** `.dev-workstream/reports/BATCH-10-REPORT.md`

**Required Sections:**

1. **Implementation Summary**
   - Optional detection logic
   - EMHEADER calculation approach
   - Presence/absence handling in views

2. **Test Results**
   - **MUST INCLUDE:** Full `dotnet test` output
   - **MUST SHOW:** All 112+ tests passing (no regressions)
   - New test count
   - Example hex dumps showing EMHEADER vs no EMHEADER

3. **Issues Encountered**
   - EMHEADER length calculation challenges?
   - View offset tracking with optionals?

4. **Next Steps**
   - What's needed for managed types (FCDC-S015)?

---

## 🎯 Success Criteria

This batch is DONE when:

- ✅ **FCDC-S014** Complete: Optional support
- ✅ 10-12 new tests passing
- ✅ Generated optional code compiles
- ✅ Optionals roundtrip correctly (present and absent)
- ✅ **ALL 112+ tests passing** (ZERO regressions)
- ✅ Report includes full test output
- ✅ Report submitted

**BLOCKING:** Any test regression blocks approval.

---

## ⚠️ Common Pitfalls to Avoid

1. **Breaking existing tests:**
   - MUST run full `dotnet test` before submitting
   - Any regression blocks approval

2. **Incorrect EMHEADER length:**
   - Must include ALL bytes of the field value
   - For strings: length header + UTF-8 bytes + NUL
   - For nested structs: entire struct size (recursive)

3. **Not handling null in GetSerializedSize:**
   - Must check HasValue/null before calculating size

4. **Forgetting to skip absent optionals:**
   - If null, write NOTHING (not even EMHEADER)

5. **View offset calculation with optionals:**
   - Can't use fixed offsets for fields after optional
   - Must track position dynamically

6. **Only running new tests:**
   - MUST run ALL tests (dotnet test at solution level)

---

## 📚 Reference Materials

- **Task Master:** [SERDATA-TASK-MASTER.md §FCDC-S014](../docs/SERDATA-TASK-MASTER.md)
- **XCDR2 Spec:** OMG XTypes 1.3, Section 7.4.3.4.3 (Optional Members)
- **BATCH-09 Review:** `.dev-workstream/reviews/BATCH-09.2-REVIEW.md` - Code generation pattern
- **BATCH-08 Review:** `.dev-workstream/reviews/BATCH-08-REVIEW.md` - View pattern

---

## 💡 Optional Member Examples

**Example 1: Nullable Value Type**
```csharp
public struct Config
{
    public int Id;           // Required (no ?)
    public double? Threshold; // Optional (has ?)
}
```

**Wire Format (present):** `[DHEADER][Id: 4][EMHEADER: 4][Threshold: 8]`  
**Wire Format (absent):** `[DHEADER][Id: 4]` (nothing for Threshold)

**Example 2: Nullable Reference Type**
```csharp
public struct Message
{
    public int Seq;       // Required (no ?)
    public string? Source; // Optional (has ?)
}
```

**Wire Format (present):** `[DHEADER][Seq: 4][EMHEADER: 4][StrLen: 4][UTF-8 bytes + NUL]`  
**Wire Format (absent):** `[DHEADER][Seq: 4]`

**Detection is automatic:** The `?` suffix is sufficient. No attributes needed!

---

**Next Batch:** BATCH-11 (Generator Testing Suite) - Comprehensive test coverage before Stage 3
