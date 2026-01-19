# BATCH-08: Fix Core Tests + Deserializer Code Emitter

**Batch Number:** BATCH-08  
**Tasks:** Fix Core Test Regression (BATCH-07 issue), FCDC-S012 (Deserializer + View Structs)  
**Phase:** Stage 2 - Code Generation (Deserialization + Test Fixes)  
**Estimated Effort:** 12-15 hours  
**Priority:** CRITICAL (blocking + new functionality)  
**Dependencies:** BATCH-07 (variable types - needs fix)

---

## 📋 Onboarding & Workflow

### Developer Instructions

This batch has **TWO parts**:
1. **PREREQUISITE (BLOCKING):** Fix 2 failing Core tests from BATCH-07
2. **MAIN TASK:** Implement deserializer code generator with zero-copy view structs

**Your Mission:**  
First fix the regression, then generate C# deserialization code with view structs for zero-copy reads.

**Critical Context:**
- BATCH-07 broke 2 Golden Rig tests - must fix before proceeding
- Deserializer must support zero-copy views (no heap allocations)
- Use `CdrReader` (from BATCH-01) for parsing
- View structs provide read-only access to serialized data

### Required Reading (IN ORDER)

1. **BATCH-07 Review:** `.dev-workstream/reviews/BATCH-07-REVIEW.md` - **READ THE FAILING TESTS SECTION**
2. **Previous Reviews:**
   - `.dev-workstream/reviews/BATCH-01-REVIEW.md` - CdrReader
   - `.dev-workstream/reviews/BATCH-06-REVIEW.md` - Fixed types serializer
   - `.dev-workstream/reviews/BATCH-07-REVIEW.md` - Variable types serializer
3. **Task Master:** `docs/SERDATA-TASK-MASTER.md` - **READ FCDC-S012 CAREFULLY**
4. **Design Document:** `docs/SERDATA-DESIGN.md` - Section 4.4 (Deserializer), Section 5.3 (View Structs)
5. **Design Talk:** `docs/design-talk.md` - Search for "Loaned Sample" and "View" patterns

### Source Code Location

- **CLI Tool:** `tools/CycloneDDS.CodeGen/` (extend `SerializerEmitter.cs` or create `DeserializerEmitter.cs`)
- **Core:** `Src/CycloneDDS.Core/` (fix `CdrWriter.cs` or `CdrSizer.cs`)
- **Test Projects:** 
  - `tests/CycloneDDS.Core.Tests/` (fix failing tests)
  - `tests/CycloneDDS.CodeGen.Tests/` (new deserializer tests)

### Report Submission

**⚠️ CRITICAL: REPORT FOLDER LOCATION ⚠️**

**Submit to:** `.dev-workstream/reports/BATCH-08-REPORT.md`

---

## 🔄 MANDATORY WORKFLOW: Fix Then Build

**CRITICAL: Must complete tasks in strict order**

1. **Task 0 (PREREQUISITE):** Fix 2 failing Core tests → **ALL 108 tests pass** ✅
2. **Task 1:** Implement deserializer emitter → **Generate code** ✅
3. **Task 2:** Write deserializer tests → **ALL tests pass** ✅

**DO NOT** proceed to Task 1 until Task 0 complete and all tests passing.

---

## Context

**BATCH-07 Status:** Variable type serializer works correctly (CodeGen tests 41/41 pass), BUT 2 Core tests failing (regression).

**This Batch Part 1:** Fix the regression to unblock development.

**This Batch Part 2:** Implement deserializer with zero-copy view structs.

**Related Tasks:**
- Fix: BATCH-07 regression
- [FCDC-S012](../docs/SERDATA-TASK-MASTER.md#fcdc-s012-deserializer-code-emitter--view-structs)

---

## 🎯 Batch Objectives

**Primary Goal:** Fix regression, then generate deserialization code with view structs.

**Success Metrics:**
- All 108 tests passing (including fixed Core tests)
- Generated deserializer code compiles
- View structs provide zero-copy access
- All new tests pass

---

## ✅ Task 0: Fix Core Test Regression (PREREQUISITE - BLOCKING)

**Status:** ⚠️ **BLOCKING** - must complete before Tasks 1-2

**Issue:** 2 tests failing in `CycloneDDS.Core.Tests`:
- `GoldenConsistencyTests.MultiplePrimitives_SequenceAlignment`
- One other (output truncated in review)

**Root Cause:** Likely changes to `CdrWriter` or `CdrSizer` in BATCH-07 for variable type support.

### Investigation Steps

1. **Run tests with full output:**
   ```bash
   dotnet test tests/CycloneDDS.Core.Tests/CycloneDDS.Core.Tests.csproj --logger "console;verbosity=detailed"
   ```

2. **Identify both failing tests:**
   - Read full error messages
   - Note expected vs actual values

3. **Review BATCH-07 changes:**
   - Check `Src/CycloneDDS.Core/CdrWriter.cs`
   - Check `Src/CycloneDDS.Core/CdrSizer.cs`
   - Look for changes that affect fixed-type serialization

4. **Common issues to check:**
   - Did `WriteString` or sequence methods change alignment behavior?
   - Did `CdrSizer` change size calculation for primitives?
   - Were any methods made virtual that weren't before?
   - Did any primitive write methods get modified?

### Fix Requirements

- ✅ Both failing tests must pass
- ✅ All 108 tests must pass (no new regressions)
- ✅ Golden Rig tests remain byte-perfect
- ✅ No changes to test expectations (fix the code, not the tests)

### Deliverables

- Fixed `CdrWriter.cs` and/or `CdrSizer.cs` (or other Core files)
- Brief explanation in report of what caused regression and how fixed

**Estimated Time:** 1-2 hours

---

## ✅ Task 1: Deserializer Code Emitter (FCDC-S012)

**DEPENDENCY:** Task 0 must be complete (all tests passing)

**Files:** Create `tools/CycloneDDS.CodeGen/DeserializerEmitter.cs` OR extend `SerializerEmitter.cs`  
**Task Definition:** See [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md#fcdc-s012-deserializer-code-emitter--view-structs)

**Description:**  
Generate C# deserializer with zero-copy view structs for read-only access.

**Design Reference:** [SERDATA-TASK-MASTER.md §FCDC-S012](../docs/SERDATA-TASK-MASTER.md), design-talk.md (search "Loaned Sample")

### Zero-Copy View Structs

**Key Concept:** Instead of allocating heap memory and copying data, views provide **direct read access** to serialized bytes.

**Benefits:**
- Zero heap allocations
- Zero copy overhead
- Ideal for hot read paths

**Tradeoff:**
- Read-only (can't modify)
- Lifetime tied to buffer

### View Struct Pattern

**For fixed types:**
```csharp
// Original user struct
[DdsTopic]
public struct SensorData
{
    public int Id;
    public double Value;
}

// Generated view struct (zero-copy)
public readonly ref struct SensorDataView
{
    private readonly ReadOnlySpan<byte> _buffer;
    private readonly int _offset;
    
    internal SensorDataView(ReadOnlySpan<byte> buffer, int offset)
    {
        _buffer = buffer;
        _offset = offset;
    }
    
    // Property readers (zero-copy)
    public int Id => BinaryPrimitives.ReadInt32LittleEndian(_buffer.Slice(_offset + 4, 4));
    public double Value => BinaryPrimitives.ReadDoubleLittleEndian(_buffer.Slice(_offset + 8, 8));
}
```

**For variable types (strings):**
```csharp
// Generated view for struct with string
public readonly ref struct MessageDataView
{
    private readonly ReadOnlySpan<byte> _buffer;
    private readonly int _offset;
    
    internal MessageDataView(ReadOnlySpan<byte> buffer, int offset)
    {
        _buffer = buffer;
        _offset = offset;
    }
    
    public int Id => BinaryPrimitives.ReadInt32LittleEndian(_buffer.Slice(_offset + 4, 4));
    
    // String property: returns ReadOnlySpan (zero-copy UTF-8)
    public ReadOnlySpan<byte> MessageUtf8
    {
        get
        {
            int strOffset = _offset + 8; // After DHEADER + Id
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Slice(strOffset, 4));
            return _buffer.Slice(strOffset + 4, (int)length - 1); // -1 excludes NUL
        }
    }
    
    // Helper to decode to string (allocates)
    public string Message => Encoding.UTF8.GetString(MessageUtf8);
}
```

### Generated Code Pattern

**Input:**
```csharp
[DdsTopic]
struct SensorData
{
    public int Id;
    public double Value;
}
```

**Generated Output:**

```csharp
// View struct (zero-copy read)
public readonly ref struct SensorDataView
{
    private readonly ReadOnlySpan<byte> _buffer;
    private readonly int _offset;
    
    internal SensorDataView(ReadOnlySpan<byte> buffer, int offset)
    {
        _buffer = buffer;
        _offset = offset;
    }
    
    public int Id => BinaryPrimitives.ReadInt32LittleEndian(_buffer.Slice(_offset + 4, 4));
    public double Value => BinaryPrimitives.ReadDoubleLittleEndian(_buffer.Slice(_offset + 8, 8));
}

// Extension to original struct
public partial struct SensorData
{
    // Deserialize from buffer (allocating)
    publicstatic SensorData Deserialize(ReadOnlySpan<byte> buffer)
    {
        var reader = new CdrReader(buffer);
        
        // Skip DHEADER
        reader.ReadUInt32();
        
        return new SensorData
        {
            Id = reader.ReadInt32(),
            Value = reader.ReadDouble()
        };
    }
    
    // Create view (zero-copy)
    public static SensorDataView AsView(ReadOnlySpan<byte> buffer)
    {
        return new SensorDataView(buffer, 0);
    }
}
```

### Type Mapping for View Properties

| C# Type | View Property Type | Read Method |
|---------|-------------------|-------------|
| `int` | `int` | `BinaryPrimitives.ReadInt32LittleEndian` |
| `double` | `double` | `BinaryPrimitives.ReadDoubleLittleEndian` |
| `bool` | `bool` | `buffer[offset] != 0` |
| `string` | `ReadOnlySpan<byte>` (UTF-8) | Slice with length lookup |
| `BoundedSeq<T>` | `ReadOnlySpan<T>` (if blittable) | Slice with count |
| Nested struct | `NestedView` | Create nested view |

### Offset Calculation

**Critical:** View properties must calculate correct offsets accounting for:
- DHEADER (4 bytes at start)
- Alignment of each field
- Variable-size fields (strings, sequences)

**For fixed types:** Offsets are constant (calculable at generation time).

**For variable types:** Offsets must be calculated at runtime (walk through fields).

### Implementation Structure

```csharp
public class DeserializerEmitter
{
    public string EmitDeserializer(TypeInfo type)
    {
        var sb = new StringBuilder();
        
        // Using directives
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Buffers.Binary;");
        sb.AppendLine("using System.Text;");
        sb.AppendLine("using CycloneDDS.Core;");
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(type.Namespace))
        {
            sb.AppendLine($"namespace {type.Namespace}");
            sb.AppendLine("{");
        }
        
        // Generate View struct
        EmitViewStruct(sb, type);
        
        // Extend original struct with Deserialize  and AsView
        EmitDeserializeMethod(sb, type);
        
        if (!string.IsNullOrEmpty(type.Namespace))
        {
            sb.AppendLine("}");
        }
        
        return sb.ToString();
    }
    
    private void EmitViewStruct(StringBuilder sb, TypeInfo type)
    {
        sb.AppendLine($"    public readonly ref struct {type.Name}View");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly ReadOnlySpan<byte> _buffer;");
        sb.AppendLine("        private readonly int _offset;");
        sb.AppendLine();
        sb.AppendLine("        internal {type.Name}View(ReadOnlySpan<byte> buffer, int offset)");
        sb.AppendLine("        {");
        sb.AppendLine("            _buffer = buffer;");
        sb.AppendLine("            _offset = offset;");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Properties
        int currentOffset = 4; // After DHEADER
        foreach (var field in type.Fields)
        {
            EmitViewProperty(sb, field, ref currentOffset);
        }
        
        sb.AppendLine("    }");
        sb.AppendLine();
    }
    
    private void EmitViewProperty(StringBuilder sb, FieldInfo field, ref int offset)
    {
        string propName = ToPascalCase(field.Name);
        
        if (field.TypeName == "int")
        {
            sb.AppendLine($"        public int {propName} => BinaryPrimitives.ReadInt32LittleEndian(_buffer.Slice(_offset + {offset}, 4));");
            offset += 4;
        }
        else if (field.TypeName == "double")
        {
            offset = AlignTo(offset, 8);
            sb.AppendLine($"        public double {propName} => BinaryPrimitives.ReadDoubleLittleEndian(_buffer.Slice(_offset + {offset}, 8));");
            offset += 8;
        }
        // ... other types
    }
    
    private void EmitDeserializeMethod(StringBuilder sb, TypeInfo type)
    {
        sb.AppendLine($"    public partial struct {type.Name}");
        sb.AppendLine("    {");
        sb.AppendLine($"        public static {type.Name} Deserialize(ReadOnlySpan<byte> buffer)");
        sb.AppendLine("        {");
        sb.AppendLine("            var reader = new CdrReader(buffer);");
        sb.AppendLine("            reader.ReadUInt32(); // DHEADER");
        sb.AppendLine();
        sb.AppendLine($"            return new {type.Name}");
        sb.AppendLine("            {");
        
        foreach (var field in type.Fields)
        {
            string propName = ToPascalCase(field.Name);
            string readCall = GetReaderCall(field);
            sb.AppendLine($"                {propName} = {readCall},");
        }
        
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        public static {type.Name}View AsView(ReadOnlySpan<byte> buffer)");
        sb.AppendLine("        {");
        sb.AppendLine($"            return new {type.Name}View(buffer, 0);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }
    
    private string GetReaderCall(FieldInfo field)
    {
        return field.TypeName switch
        {
            "int" => "reader.ReadInt32()",
            "double" => "reader.ReadDouble()",
            "string" when field.HasAttribute("DdsManaged") => "reader.ReadString()",
            _ => throw new NotImplementedException()
        };
    }
}
```

### Deliverables

- `tools/CycloneDDS.CodeGen/DeserializerEmitter.cs` (or extend SerializerEmitter)
- Integration into `CodeGenerator.cs`

**Estimated Time:** 8-10 hours

---

## Task 2: Deserializer Tests

**Files:** Extend `tests/CycloneDDS.CodeGen.Tests/SerializerEmitterTests.cs` or create `DeserializerEmitterTests.cs`

**Minimum 8-12 tests:**

#### Code Generation Tests (4-6 tests)
1. ✅ Generates view struct with correct name
2. ✅ Generates view properties with correct types
3. ✅ Generates Deserialize method
4. ✅ Generates AsView method
5. ✅ Handles strings (view returns ReadOnlySpan<byte>)
6. ✅ Handles nested structs

#### Execution Tests (4-6 tests - CRITICAL)
7. ✅ **View reads match serialized values** (fixed types)
8. ✅ **Deserialize produces correct struct** (allocating)
9. ✅ **View reads match serialized values** (variable types - strings)
10. ✅ **View string UTF-8 span is correct**
11. ✅ **Roundtrip: Serialize → View → values match**
12. ✅ **Roundtrip: Serialize → Deserialize → values match**

**Quality Standard:**

**✅ REQUIRED:**
- Tests MUST compile generated code (Roslyn)
- Tests MUST serialize data, then deserialize it
- Tests MUST verify roundtrip correctness
- Tests MUST verify view zero-copy (no heap allocations if possible to measure)

**Example GOOD Test:**

```csharp
[Fact]
public void GeneratedDeserializer_Roundtrip_Fixed()
{
    // Generate serializer & deserializer
    var emitter = new SerializerEmitter();
    var desEmitter = new DeserializerEmitter();
    
    string serCode = emitter.EmitSerializer(type);
    string desCode = desEmitter.EmitDeserializer(type);
    
    // Compile
    var assembly = CompileToAssembly(serCode + "\n" + desCode);
    var generatedType = assembly.GetType("SensorData");
    
    // Serialize
    var original = Activator.CreateInstance(generatedType);
    generatedType.GetField("Id").SetValue(original, 42);
    generatedType.GetField("Value").SetValue(original, 3.14);
    
    var writer = new ArrayBufferWriter<byte>();
    var cdr = new CdrWriter(writer);
    generatedType.GetMethod("Serialize").Invoke(original, new object[] { cdr });
    cdr.Complete();
    
    // Deserialize (allocating)
    var deserializeMethod = generatedType.GetMethod("Deserialize");
    var deserialized = deserializeMethod.Invoke(null, new object[] { writer.WrittenSpan });
    
    Assert.Equal(42, generatedType.GetField("Id").GetValue(deserialized));
    Assert.Equal(3.14, generatedType.GetField("Value").GetValue(deserialized));
    
    // View (zero-copy)
    var asViewMethod = generatedType.GetMethod("AsView");
    var view = asViewMethod.Invoke(null, new object[] { writer.WrittenSpan });
    var viewType = view.GetType();
    
    Assert.Equal(42, viewType.GetProperty("Id").GetValue(view));
    Assert.Equal(3.14, viewType.GetProperty("Value").GetValue(view));
}
```

**Estimated Time:** 2-3 hours

---

## 🧪 Testing Requirements

**Task 0 (Regression Fix):**
- ✅ All 108 tests passing

**Task 1-2 (Deserializer):**
- Minimum 8-12 new tests
- Test Distribution:
  - Code Generation: 4-6 tests
  - Execution (roundtrip): 4-6 tests

**Critical:** Roundtrip tests verify serialization → deserialization produces original values.

---

## 📊 Report Requirements

**Submit to:** `.dev-workstream/reports/BATCH-08-REPORT.md`

**Required Sections:**

1. **Task 0: Regression Fix**
   - **MUST INCLUDE:** What were the 2 failing tests?
   - What caused the regression?
   - How was it fixed?
   - All 108 tests passing confirmation

2. **Task 1: Implementation Summary**
   - View struct generation approach
   - Offset calculation strategy
   - Type mapping decisions

3. **Task 2: Test Results**
   - Test counts
   - Roundtrip verification results

4. **Issues Encountered**
   - Offset calculation challenges?
   - Variable type view complexity?

5. **Next Steps**
   - What's needed for unions (FCDC-S013)?

---

## 🎯 Success Criteria

This batch is DONE when:

- ✅ **Task 0:** All 108 tests passing (regression fixed)
- ✅ **FCDC-S012** Complete: Deserializer + view structs
- ✅ 8-12 new tests passing
- ✅ Generated deserializer compiles
- ✅ Roundtrip tests pass (serialize → deserialize → match)
- ✅ View structs provide zero-copy access
- ✅ Report submitted

**GATE:** All Core tests must pass before proceeding to union support.

---

## ⚠️ Common Pitfalls to Avoid

1. **Not fixing regression first:** Task 0 is BLOCKING - don't skip it

2. **Incorrect offset calculation:** Must account for DHEADER and alignment
   - Fixed types: Offsets constant
   - Variable types: Must walk fields

3. **Not using `ReadOnlySpan<byte>` for strings in views:**
   - Wrong: `string` property (allocates)
   - Right: `ReadOnlySpan<byte>` property (zero-copy)

4. **Forgetting DHEADER skip in Deserialize:**
   - Must read/skip DHEADER before reading fields

5. **Not testing roundtrip:**
   - Must verify: original → serialize → deserialize → equals original

6. **View lifetime issues:**
   - Views tied to buffer lifetime - document in generated code

---

## 📚 Reference Materials

- **Task Master:** [SERDATA-TASK-MASTER.md §FCDC-S012](../docs/SERDATA-TASK-MASTER.md)
- **Design Talk:** `docs/design-talk.md` - Search "Loaned Sample" and "View"
- **CdrReader:** `Src/CycloneDDS.Core/CdrReader.cs`
- **BATCH-07 Review:** `.dev-workstream/reviews/BATCH-07-REVIEW.md` - Failing tests

---

**Next Batch:** BATCH-09 (Union Support) - Discriminated unions with [DdsUnion]
