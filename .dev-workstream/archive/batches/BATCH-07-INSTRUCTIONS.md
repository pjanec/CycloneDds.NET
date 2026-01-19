# BATCH-07: Serializer Code Emitter - Variable Types

**Batch Number:** BATCH-07  
**Tasks:** FCDC-S011 (Serializer Code Emitter - Variable Types)  
**Phase:** Stage 2 - Code Generation (Variable Serialization)  
**Estimated Effort:** 15-18 hours  
**Priority:** CRITICAL (required for real-world DDS usage)  
**Dependencies:** BATCH-06 (fixed types serializer)

---

## 📋 Onboarding & Workflow

### Developer Instructions

This batch extends the serializer code generator to support **variable-size types**: strings, sequences, and structs containing variable fields.

**Your Mission:**  
Extend `SerializerEmitter` to generate code for variable types with dynamic DHEADER calculation.

**Critical Context:**
- Variable types have **unpredictable size** at compile time
- Must calculate DHEADER size **dynamically** based on actual field values
- `GetSerializedSize` must traverse actual field values, not use placeholder zeros
- More complex than BATCH-06 due to runtime size calculation

### Required Reading (IN ORDER)

1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Previous Reviews:**
   - `.dev-workstream/reviews/BATCH-06-REVIEW.md` - **Fixed types pattern**
   - `.dev-workstream/reviews/BATCH-02-REVIEW.md` - Golden Rig
3. **Task Master:** `docs/SERDATA-TASK-MASTER.md` - **READ FCDC-S011 CAREFULLY**
4. **XCDR2 Details:** `docs/XCDR2-IMPLEMENTATION-DETAILS.md` - **Variable type rules**
5. **Design Document:** `docs/SERDATA-DESIGN.md` - Section 4.3 (Variable Types)

### Source Code Location

- **CLI Tool:** `tools/CycloneDDS.CodeGen/` (extend `SerializerEmitter.cs`)
- **Test Project:** `tests/CycloneDDS.CodeGen.Tests/` (extend `SerializerEmitterTests.cs`)

### Report Submission

**⚠️ CRITICAL: REPORT FOLDER LOCATION ⚠️**

**Submit to:** `.dev-workstream/reports/BATCH-07-REPORT.md`

---

## Context

**BATCH-06 Complete:** Serializer for fixed types (primitives, FixedString, fixed structs).

**This Batch:** Add support for variable types (strings, sequences, nested variable structs).

**Related Tasks:**
- [FCDC-S011](../docs/SERDATA-TASK-MASTER.md#fcdc-s011-serializer-code-emitter---variable-types)

**What "Variable Types" Means:**
- **Strings:** `string` (with `[DdsManaged]`)
- **Sequences:** `BoundedSeq<T>`
- **Variable structs:** Structs containing strings or sequences
- **Nested variable:** Struct A contains Struct B which has string

---

## 🎯 Batch Objectives

**Primary Goal:** Generate serialization code for variable-size types.

**Success Metrics:**
- Generated code compiles
- Generated code produces byte-perfect XCDR2 output
- DHEADER dynamically calculated based on actual field values
- All tests pass (minimum 10-15)

---

## ✅ Task: Extend Serializer for Variable Types (FCDC-S011)

**Files:** Modify `tools/CycloneDDS.CodeGen/SerializerEmitter.cs`  
**Task Definition:** See [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md#fcdc-s011-serializer-code-emitter---variable-types)

### Key Difference from Fixed Types

**Fixed Types (BATCH-06):**
```csharp
public int GetSerializedSize(int currentOffset)
{
    var sizer = new CdrSizer(currentOffset);
    sizer.WriteUInt32(0); // DHEADER placeholder
    sizer.WriteInt32(0);  // id - uses zero
    sizer.WriteDouble(0); // value - uses zero
    return sizer.GetSizeDelta(currentOffset);
}
```

**Variable Types (THIS BATCH):**
```csharp
public int GetSerializedSize(int currentOffset)
{
    var sizer = new CdrSizer(currentOffset);
    sizer.WriteUInt32(0); // DHEADER placeholder
    sizer.WriteInt32(0);  // id - still zero
    sizer.WriteString(this.Message); // USES ACTUAL VALUE!
    return sizer.GetSizeDelta(currentOffset);
}
```

**Critical:** For variable types, `GetSerializedSize` must use **actual field values** to calculate correct size.

### Variable Type Handling

#### 1. Strings (`string` with `[DdsManaged]`)

**XCDR2 Format:**
- UInt32 length (including NUL terminator)
- UTF-8 bytes
- NUL terminator (0x00)

**Generated Code:**
```csharp
// GetSerializedSize
sizer.WriteString(this.Message); // Actual value

// Serialize
writer.WriteString(this.Message); // Actual value
```

**CdrWriter.WriteString Implementation (already exists from BATCH-01):**
```csharp
public void WriteString(string value)
{
    if (value == null) value = "";
    
    Align(4); // String length is UInt32
    
    int length = Encoding.UTF8.GetByteCount(value) + 1; // +1 for NUL
    WriteUInt32((uint)length);
    
    Span<byte> utf8 = stackalloc byte[length];
    Encoding.UTF8.GetBytes(value, utf8);
    utf8[length - 1] = 0; // NUL terminator
    
    Write(utf8);
}
```

#### 2. Sequences (`BoundedSeq<T>`)

**XCDR2 Format:**
- UInt32 count
- Elements (each aligned)

**Generated Code:**
```csharp
// GetSerializedSize
sizer.WriteUInt32(0); // count placeholder
for (int i = 0; i < this.Values.Count; i++)
{
    sizer.WriteInt32(0); // element (or nested call)
}

// Serialize
writer.WriteUInt32((uint)this.Values.Count);
for (int i = 0; i < this.Values.Count; i++)
{
    writer.WriteInt32(this.Values[i]);
}
```

**Note:** For sequences of primitives, size is `4 + (count * elementSize)`. For variable elements, must iterate and sum.

#### 3. Nested Variable Structs

**Generated Code:**
```csharp
// GetSerializedSize
int nestedSize = this.Nested.GetSerializedSize(sizer.Position);
sizer.Skip(nestedSize);

// Serialize
this.Nested.Serialize(ref writer);
```

**Same as BATCH-06** - nested structs delegate to their own methods.

### Type Detection Logic

**Extend `SerializerEmitter` with type classification:**

```csharp
private bool IsVariableType(FieldInfo field)
{
    if (field.TypeName == "string" && field.HasAttribute("DdsManaged"))
        return true;
    
    if (field.TypeName.StartsWith("BoundedSeq<"))
        return true;
    
    // Check if nested struct is variable
    if (field.Type != null && HasVariableFields(field.Type))
        return true;
    
    return false;
}

private bool HasVariableFields(TypeInfo type)
{
    return type.Fields.Any(f => IsVariableType(f));
}
```

### Generated Code Pattern for Variable Struct

**Input:**
```csharp
[DdsTopic]
struct MessageData
{
    public int Id;
    
    [DdsManaged]
    public string Message;
}
```

**Generated Output:**

```csharp
public partial struct MessageData
{
    public int GetSerializedSize(int currentOffset)
    {
        var sizer = new CdrSizer(currentOffset);
        
        // DHEADER
        sizer.WriteUInt32(0);
        
        // Fields
        sizer.WriteInt32(0);              // Id (fixed)
        sizer.WriteString(this.Message);  // Message (VARIABLE - uses actual value!)
        
        return sizer.GetSizeDelta(currentOffset);
    }
    
    public void Serialize(ref CdrWriter writer)
    {
        // DHEADER
        int dheaderPos = writer.Position;
        writer.WriteUInt32(0);
        
        int bodyStart = writer.Position;
        
        // Fields
        writer.WriteInt32(this.Id);
        writer.WriteString(this.Message);
        
        // Patch DHEADER
        int bodySize = writer.Position - bodyStart;
        writer.PatchUInt32(dheaderPos, (uint)bodySize);
    }
}
```

### Implementation Changes

**Modify `GetSizerCall` in `SerializerEmitter.cs`:**

```csharp
private string GetSizerCall(FieldInfo field)
{
    return field.TypeName switch
    {
        // Fixed types (from BATCH-06)
        "int" => "sizer.WriteInt32(0)",
        "double" => "sizer.WriteDouble(0)",
        
        // Variable types (NEW)
        "string" when field.HasAttribute("DdsManaged") => $"sizer.WriteString(this.{ToPascalCase(field.Name)})",
        
        _ when field.TypeName.StartsWith("BoundedSeq<") => EmitSequenceSizer(field),
        _ when IsNestedStruct(field) => $"sizer.Skip(this.{ToPascalCase(field.Name)}.GetSerializedSize(sizer.Position))",
        
        _ => throw new NotImplementedException($"Type {field.TypeName} not supported")
    };
}

private string EmitSequenceSizer(FieldInfo field)
{
    string fieldAccess = $"this.{ToPascalCase(field.Name)}";
    string elementType = ExtractSequenceElementType(field.TypeName);
    
    // For primitive sequences, can calculate size  without loop
    if (IsPrimitiveType(elementType))
    {
        int elementSize = GetPrimitiveSize(elementType);
        return $"sizer.WriteUInt32(0); sizer.Skip({fieldAccess}.Count * {elementSize})";
    }
    
    // For variable element sequences, need loop (complex - may defer to future batch)
    return $"sizer.WriteUInt32(0); for (int i = 0; i < {fieldAccess}.Count; i++) {{ /* element sizer */ }}";
}
```

**Sequence generation is COMPLEX** - for this batch, focus on:
1. Sequences of primitives (can calculate size)
2. Strings
3. Nested variable structs

Sequences of variable elements can be deferred or simplified.

### Deliverables

- Modify `tools/CycloneDDS.CodeGen/SerializerEmitter.cs`
- Add helper methods for variable type detection
- Update `TypeMapper.cs` if needed

### Tests Required (Add to `tests/CycloneDDS.CodeGen.Tests/SerializerEmitterTests.cs`)

**Minimum 10-15 tests:**

#### Code Generation Tests (5-8 tests)
1. ✅ Generates sizer call with actual field value for string
2. ✅ Generates write call for string
3. ✅ Handles struct with mixed fixed/variable fields
4. ✅ Generates sequence sizer (primitives)
5. ✅ Generates sequence write
6. ✅ Handles nested variable struct
7. ✅ Detects variable vs fixed types correctly
8. ✅ Generates correct field order (fixed before variable recommended, but not required)

#### Execution Tests (5-7 tests - CRITICAL)
9. ✅ **String serialization matches expected bytes**
10. ✅ **DHEADER size correct for variable struct**
11. ✅ **GetSerializedSize matches actual Serialize output** (variable data)
12. ✅ **Empty string handled correctly**
13. ✅ **Sequence of primitives serializes correctly**
14. ✅ **Nested variable struct serializes correctly**
15. ✅ **String with non-ASCII characters (UTF-8) handled**

**Quality Standard:**

**✅ REQUIRED:**
- Tests MUST compile generated code (Roslyn)
- Tests MUST execute with variable data (different string lengths, etc.)
- Tests MUST verify byte-perfect output
- Tests MUST verify GetSerializedSize == Serialize output for same data

**Example GOOD Test:**

```csharp
[Fact]
public void GeneratedCode_String_Serializes_Correctly()
{
    var type = new TypeInfo
    {
        Name = "MessageData",
        Fields = new[]
        {
            new FieldInfo { Name = "Id", TypeName = "int" },
            new FieldInfo 
            { 
                Name = "Message", 
                TypeName = "string",
                Attributes = new[] { new AttributeInfo { Name = "DdsManaged" } }
            }
        }
    };
    
    // Generate & compile
    var emitter = new SerializerEmitter();
    var assembly = CompileToAssembly(emitter.EmitSerializer(type));
    
    // Create instance with variable data
    var instance = Activator.CreateInstance(assembly.GetType("MessageData"));
    instance.GetType().GetField("Id").SetValue(instance, 42);
    instance.GetType().GetField("Message").SetValue(instance, "Hello");
    
    // GetSerializedSizevariance
    var sizeMethod = instance.GetType().GetMethod("GetSerializedSize");
    int size = (int)sizeMethod.Invoke(instance, new object[] { 0 });
    
    // Serialize
    var writer = new ArrayBufferWriter<byte>();
    var cdr = new CdrWriter(writer);
    var serializeMethod = instance.GetType().GetMethod("Serialize");
    serializeMethod.Invoke(instance, new object[] { cdr });
    cdr.Complete();
    
    // Verify
    Assert.Equal(size, writer.WrittenCount); // Size matches
    
    // Verify DHEADER + structure
    // DHEADER: 4 + 4 (id) + 4 (str len) + 6 ("Hello\0") = 18 bytes body
    // Total: 4 (DHEADER) + 18 = 22
    Assert.Equal(22, writer.WrittenCount);
    
    byte[] bytes = writer.WrittenSpan.ToArray();
    Assert.Equal(18, BitConverter.ToUInt32(bytes, 0)); // DHEADER = 18
}
```

**Estimated Time:** 15-18 hours

---

## 🧪 Testing Requirements

**Minimum Total Tests:** 10-15 tests

**Test Distribution:**
- Code Generation: 5-8 tests
- Execution (with variable data): 5-7 tests

**Critical:** Tests MUST use variable data (different string lengths, sequence sizes).

---

## 📊 Report Requirements

**Submit to:** `.dev-workstream/reports/BATCH-07-REPORT.md`

**Required Sections:**

1. **Implementation Summary**
   - How variable types differ from fixed
   - Type detection logic
   - Test counts

2. **Issues Encountered**
   - String edge cases?
   - Sequence complexity?
   - Size calculation challenges?

3. **Design Decisions**
   - How did you handle sequence sizing?
   - Any type restrictions?
   - Deferred complexity?

4. **Byte Validation**
   - **MUST INCLUDE:** Which variable type tests pass
   - String length correctness
   - DHEADER size verification

5. **Next Steps**
   - What's needed for deserializer (FCDC-S012)?
   - Any limitations for unions/optionals?

---

## 🎯 Success Criteria

This batch is DONE when:

- ✅ **FCDC-S011** Complete: Variable type serializer
- ✅ 10-15 tests passing
- ✅ Generated code compiles
- ✅ **Variable data serializes byte-perfect**
- ✅ GetSerializedSize matches Serialize for variable data
- ✅ Strings, sequences (primitives), nested variable structs supported
- ✅ Report submitted

---

## ⚠️ Common Pitfalls to Avoid

1. **Using placeholder zero for variable fields in GetSerializedSize**
   - Wrong: `sizer.WriteString("")` 
   - Right: `sizer.WriteString(this.Message)`

2. **Not handling NULL strings**
   - Treat as empty string

3. **Forgetting NUL terminator in string length**
   - String length = UTF-8 byte count + 1

4. **Not aligning string length (it's UInt32)**
   - `CdrWriter.WriteString` handles this

5. **Sequence size calculation errors**
   - Count + (element size * count) for fixed elements
   - Must iterate for variable elements

6. **DHEADER size mismatch with variable data**
   - Must patch with actual body size after serialization

---

## 📚 Reference Materials

- **Task Master:** [SERDATA-TASK-MASTER.md §FCDC-S011](../docs/SERDATA-TASK-MASTER.md)
- **XCDR2 Details:** [XCDR2-IMPLEMENTATION-DETAILS.md](../docs/XCDR2-IMPLEMENTATION-DETAILS.md)
- **CdrWriter:** `Src/CycloneDDS.Core/CdrWriter.cs` (WriteString, WriteSequence methods)
- **CdrSizer:** `Src/CycloneDDS.Core/CdrSizer.cs`
- **BATCH-06 Review:** `.dev-workstream/reviews/BATCH-06-REVIEW.md` - Fixed types pattern

---

**Next Batch:** BATCH-08 (Deserializer + View Structs) - Read-only zero-copy views
