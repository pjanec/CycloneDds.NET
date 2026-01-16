# BATCH-06: Serializer Code Emitter - Fixed Types

**Batch Number:** BATCH-06  
**Tasks:** FCDC-S010 (Serializer Code Emitter - Fixed Types)  
**Phase:** Stage 2 - Code Generation (Serializer Generation)  
**Estimated Effort:** 12-15 hours  
**Priority:** CRITICAL (first actual code generation)  
**Dependencies:** BATCH-05 (descriptor parsing complete)

---

## 📋 Onboarding & Workflow

### Developer Instructions

This batch implements the **core serializer code generator** for fixed-size types. You'll generate C# code that serializes topics using `CdrWriter` and calculates sizes using `CdrSizer`.

**Your Mission:**  
Generate C# partial classes with `Serialize` and `GetSerializedSize` methods for fixed-size structs.

**Critical Context:** 
- We generate code that uses `CdrWriter` (from BATCH-01) and `CdrSizer` (from BATCH-02)
- All types are `@appendable` → must emit DHEADER before struct body
- Use `AlignmentMath` for all alignment calculations
- **Symmetric generation:** both size calculation and serialization must use identical logic

### Required Reading (IN ORDER)

1. **Workflow Guide:** `.dev-workstream/README.md` - Batch workflow
2. **Previous Reviews:**
   - `.dev-workstream/reviews/BATCH-01-REVIEW.md` - CdrWriter/CdrReader
   - `.dev-workstream/reviews/BATCH-02-REVIEW.md` - Golden Rig validation
   - `.dev-workstream/reviews/BATCH-05-REVIEW.md` - Descriptor parsing
3. **Task Master:** `docs/SERDATA-TASK-MASTER.md` - **READ FCDC-S010 CAREFULLY**
4. **Design Document:** `docs/SERDATA-DESIGN.md` - Section 4 (Code Generation)
5. **XCDR2 Details:** `docs/XCDR2-IMPLEMENTATION-DETAILS.md` - **CRITICAL - alignment, DHEADER**
6. **Golden Rig Tests:** `tests/CycloneDDS.Core.Tests/GoldenConsistencyTests.cs` - Examples of correct serialization

### Source Code Location

- **CLI Tool:** `tools/CycloneDDS.CodeGen/` (extend from BATCH-05)
- **Test Project:** `tests/CycloneDDS.CodeGen.Tests/` (extend from BATCH-05)

### Report Submission

**⚠️ ⚠️ ⚠️ CRITICAL: REPORT FOLDER LOCATION ⚠️ ⚠️ ⚠️**

**Submit your report to:** `.dev-workstream/reports/BATCH-06-REPORT.md`

**NOT to:** `reports/` alone or `.dev-workstream/reviews/`

**Correct path:** `.dev-workstream/reports/BATCH-06-REPORT.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Development

**CRITICAL: Generate code, then verify it compiles and produces correct output**

1. **Implement Code Emitter** → Generate C# code to temp file
2. **Write Compilation Test** → Use Roslyn to compile generated code
3. **Write Execution Test** → Invoke generated methods, verify output
4. **Verify Against Golden Rig** → Compare output to known-good hex strings
5. **ALL tests pass** before submitting

**Why:** Generated code must compile AND produce byte-perfect output.

---

## Context

**BATCH-05 Complete:** IDL compiler integration, descriptor parsing via CppAst.

**This Batch:** Generate actual C# serialization code for fixed-size structs.

**Related Tasks:**
- [FCDC-S010](../docs/SERDATA-TASK-MASTER.md#fcdc-s010-serializer-code-emitter---fixed-types)

**What "Fixed Types" Means:**
- Primitives: `int`, `double`, `bool`, etc.
- Fixed-size structs: all fields are fixed-size
- Fixed-size strings: `FixedString32`, `FixedString64`
- **NOT** variable-size: `string`, `BoundedSeq<T>`, nested variable types

**Why This Matters:** Fixed-size types have predictable layout → simpler generation logic.

---

## 🎯 Batch Objectives

**Primary Goal:** Generate working C# serialization code for fixed-size structs.

**Success Metrics:**
- Generated code compiles without errors
- Generated code produces byte-perfect output matching XCDR2 spec
- All tests pass

---

## ✅ Task: Serializer Code Emitter (FCDC-S010)

**Files:** `tools/CycloneDDS.CodeGen/SerializerEmitter.cs` (NEW)  
**Task Definition:** See [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md#fcdc-s010-serializer-code-emitter---fixed-types)

**Description:**  
Generate C# partial class with `Serialize` and `GetSerializedSize` methods for fixed-size structs.

**Design Reference:** [SERDATA-TASK-MASTER.md §FCDC-S010](../docs/SERDATA-TASK-MASTER.md), [XCDR2-IMPLEMENTATION-DETAILS.md](../docs/XCDR2-IMPLEMENTATION-DETAILS.md)

### Critical XCDR2 Requirements

#### 1. All Types are @appendable → DHEADER Required

**XCDR2 Rule:** Appendable types must write DHEADER (4-byte size) before struct body.

```csharp
// Generated GetSerializedSize:
public int GetSerializedSize(int currentOffset)
{
    var sizer = new CdrSizer(currentOffset);
    
    // DHEADER (4 bytes, align 4)
    sizer.WriteUInt32(0); // Placeholder for size
    
    // Struct fields
    sizer.WriteInt32(0);    // field1
    sizer.WriteDouble(0);   // field2
    
    return sizer.GetSizeDelta(currentOffset);
}
```

#### 2. Symmetric Code Generation

**Requirement:** `GetSerializedSize` and `Serialize` must use IDENTICAL logic (field order, alignment).

```csharp
// GetSerializedSize uses CdrSizer
public int GetSerializedSize(int currentOffset)
{
    var sizer = new CdrSizer(currentOffset);
    sizer.WriteUInt32(0); // DHEADER
    sizer.WriteInt32(0);  // id
    sizer.WriteDouble(0); // value
    return sizer.GetSizeDelta(currentOffset);
}

// Serialize uses CdrWriter (SAME STRUCTURE)
public void Serialize(CdrWriter writer)
{
    int dheaderPos = writer.Position;
    writer.WriteUInt32(0); // DHEADER placeholder
    
    int bodyStart = writer.Position;
    writer.WriteInt32(this.Id);
    writer.WriteDouble(this.Value);
    
    // Patch DHEADER with body size
    int bodySize = writer.Position - bodyStart;
    writer.PatchUInt32(dheaderPos, (uint)bodySize);
}
```

#### 3. Use AlignmentMath for All Alignment

**From BATCH-02:** `AlignmentMath.Align(currentPos, alignment)` is single source of truth.

`CdrWriter` and `CdrSizer` both use `AlignmentMath` internally. Code generator doesn't need manual alignment - just emit Write calls in correct order.

### Generated Code Pattern

**Input IDL:**
```idl
@appendable
struct SensorData {
    @key int32 id;
    double value;
};
```

**Generated C# Output:**

```csharp
// Auto-generated by CycloneDDS.CodeGen
using CycloneDDS.Core;

namespace MyApp
{
    public partial struct SensorData
    {
        // GetSerializedSize for two-pass architecture
        public int GetSerializedSize(int currentOffset)
        {
            var sizer = new CdrSizer(currentOffset);
            
            // DHEADER (4 bytes, required for @appendable)
            sizer.WriteUInt32(0);
            
            // Struct body (in field order)
            sizer.WriteInt32(0);  // id
            sizer.WriteDouble(0); // value
            
            return sizer.GetSizeDelta(currentOffset);
        }
        
        // Serialize method
        public void Serialize(CdrWriter writer)
        {
            // Write DHEADER (patch later with body size)
            int dheaderPos = writer.Position;
            writer.WriteUInt32(0); // Placeholder
            
            int bodyStart = writer.Position;
            
            // Struct body (SAME ORDER as GetSerializedSize!)
            writer.WriteInt32(this.Id);
            writer.WriteDouble(this.Value);
            
            // Patch DHEADER
            int bodySize = writer.Position - bodyStart;
            writer.PatchUInt32(dheaderPos, (uint)bodySize);
        }
    }
}
```

### Type Mapping (C# field type → CdrWriter method)

| C# Type | CdrWriter Method | CdrSizer Method | Size | Align |
|---------|-----------------|----------------|------|-------|
| `byte` | `WriteUInt8` | `WriteUInt8` | 1 | 1 |
| `sbyte` | `WriteInt8` | `WriteInt8` | 1 | 1 |
| `short` | `WriteInt16` | `WriteInt16` | 2 | 2 |
| `ushort` | `WriteUInt16` | `WriteUInt16` | 2 | 2 |
| `int` | `WriteInt32` | `WriteInt32` | 4 | 4 |
| `uint` | `WriteUInt32` | `WriteUInt32` | 4 | 4 |
| `long` | `WriteInt64` | `WriteInt64` | 8 | 8 |
| `ulong` | `WriteUInt64` | `WriteUInt64` | 8 | 8 |
| `float` | `WriteFloat` | `WriteFloat` | 4 | 4 |
| `double` | `WriteDouble` | `WriteDouble` | 8 | 8 |
| `bool` | `WriteBool` | `WriteBool` | 1 | 1 |
| `FixedString32` | `WriteFixedString(value, 32)` | `WriteFixedString(null, 32)` | 32 | 1 |
| Nested struct | `value.Serialize(writer)` | `value.GetSerializedSize(...)` | varies | varies |

### Implementation Structure

```csharp
public class SerializerEmitter
{
    public string EmitSerializer(TypeInfo type)
    {
        var sb = new StringBuilder();
        
        // Using directives
        sb.AppendLine("using CycloneDDS.Core;");
        sb.AppendLine();
        
        // Namespace
        if (!string.IsNullOrEmpty(type.Namespace))
        {
            sb.AppendLine($"namespace {type.Namespace}");
            sb.AppendLine("{");
        }
        
        // Partial class/struct
        string typeKind = type.IsStruct ? "struct" : "class";
        sb.AppendLine($"    public partial {typeKind} {type.Name}");
        sb.AppendLine("    {");
        
        // GetSerializedSize method
        EmitGetSerializedSize(sb, type);
        
        // Serialize method
        EmitSerialize(sb, type);
        
        // Close class
        sb.AppendLine("    }");
        
        // Close namespace
        if (!string.IsNullOrEmpty(type.Namespace))
        {
            sb.AppendLine("}");
        }
        
        return sb.ToString();
    }
    
    private void EmitGetSerializedSize(StringBuilder sb, TypeInfo type)
    {
        sb.AppendLine("        public int GetSerializedSize(int currentOffset)");
        sb.AppendLine("        {");
        sb.AppendLine("            var sizer = new CdrSizer(currentOffset);");
        sb.AppendLine();
        sb.AppendLine("            // DHEADER (required for @appendable)");
        sb.AppendLine("            sizer.WriteUInt32(0);");
        sb.AppendLine();
        sb.AppendLine("            // Struct body");
        
        foreach (var field in type.Fields)
        {
            string sizerCall = GetSizerCall(field);
            sb.AppendLine($"            {sizerCall}; // {field.Name}");
        }
        
        sb.AppendLine();
        sb.AppendLine("            return sizer.GetSizeDelta(currentOffset);");
        sb.AppendLine("        }");
        sb.AppendLine();
    }
    
    private void EmitSerialize(StringBuilder sb, TypeInfo type)
    {
        sb.AppendLine("        public void Serialize(CdrWriter writer)");
        sb.AppendLine("        {");
        sb.AppendLine("            // DHEADER");
        sb.AppendLine("            int dheaderPos = writer.Position;");
        sb.AppendLine("            writer.WriteUInt32(0);");
        sb.AppendLine();
        sb.AppendLine("            int bodyStart = writer.Position;");
        sb.AppendLine();
        sb.AppendLine("            // Struct body");
        
        foreach (var field in type.Fields)
        {
            string writerCall = GetWriterCall(field);
            sb.AppendLine($"            {writerCall}; // {field.Name}");
        }
        
        sb.AppendLine();
        sb.AppendLine("            // Patch DHEADER");
        sb.AppendLine("            int bodySize = writer.Position - bodyStart;");
        sb.AppendLine("            writer.PatchUInt32(dheaderPos, (uint)bodySize);");
        sb.AppendLine("        }");
    }
    
    private string GetSizerCall(FieldInfo field)
    {
        return field.TypeName switch
        {
            "int" => "sizer.WriteInt32(0)",
            "double" => "sizer.WriteDouble(0)",
            "bool" => "sizer.WriteBool(false)",
            "byte" => "sizer.WriteUInt8(0)",
            "CycloneDDS.Schema.FixedString32" => "sizer.WriteFixedString(null, 32)",
            // ... etc
            _ when IsNestedStruct(field) => $"default({field.TypeName}).GetSerializedSize(sizer.Position)",
            _ => throw new NotImplementedException($"Type {field.TypeName} not supported")
        };
    }
    
    private string GetWriterCall(FieldInfo field)
    {
        string fieldAccess = $"this.{ToPascalCase(field.Name)}";
        
        return field.TypeName switch
        {
            "int" => $"writer.WriteInt32({fieldAccess})",
            "double" => $"writer.WriteDouble({fieldAccess})",
            "bool" => $"writer.WriteBool({fieldAccess})",
            "byte" => $"writer.WriteUInt8({fieldAccess})",
            "CycloneDDS.Schema.FixedString32" => $"writer.WriteFixedString({fieldAccess}, 32)",
            _ when IsNestedStruct(field) => $"{fieldAccess}.Serialize(writer)",
            _ => throw new NotImplementedException($"Type {field.TypeName} not supported")
        };
    }
}
```

### Deliverables

- `tools/CycloneDDS.CodeGen/SerializerEmitter.cs`
- Integration into `CodeGenerator.cs` (call SerializerEmitter after descriptor parsing)
- Helper: `tools/CycloneDDS.CodeGen/TypeMapper.cs` (C# type → CdrWriter method)

### Tests Required (Add to `tests/CycloneDDS.CodeGen.Tests/`)

**Minimum 15-20 tests:**

#### Code Generation Tests (8-10 tests)
1. ✅ Generates `GetSerializedSize` method
2. ✅ Generates `Serialize` method
3. ✅ Emits DHEADER code in both methods
4. ✅ Emits fields in correct order
5. ✅ Maps int → WriteInt32
6. ✅ Maps double → WriteDouble
7. ✅ Maps FixedString32 → WriteFixedString(value, 32)
8. ✅ Handles nested structs
9. ✅ Generates namespace correctly
10. ✅ Generates partial class/struct

#### Compilation Tests (3-5 tests)
11. ✅ Generated code compiles without errors
12. ✅ Generated code references CycloneDDS.Core correctly
13. ✅ Can create instance of generated type
14. ✅ Can invoke GetSerializedSize method
15. ✅ Can invoke Serialize method

#### Execution Tests (4-5 tests - CRITICAL)
16. ✅ **Serialized output matches Golden Rig hex** (SimplePrimitive)
17. ✅ **Serialized output matches Golden Rig hex** (NestedStruct)
18. ✅ GetSerializedSize matches actual Serialize output
19. ✅ DHEADER contains correct body size
20. ✅ Field alignment is correct (byte + int32 → padding)

**Quality Standard:**

**✅ REQUIRED:**
- Tests MUST compile generated code using Roslyn
- Tests MUST execute generated methods and verify output
- Tests MUST compare against Golden Rig known-good values
- **Execution tests are BLOCKING** - if generated code doesn't produce correct bytes, batch fails

**❌ NOT ACCEPTABLE:**
- Tests that only check "code string contains X"
- Tests that don't compile generated code
- Tests that don't verify actual serialized output

**Example GOOD Test:**

```csharp
[Fact]
public void GeneratedCode_Serializes_MatchesGoldenRig()
{
    // Define type
    var type = new TypeInfo
    {
        Name = "SimplePrimitive",
        Fields = new[]
        {
            new FieldInfo { Name = "Id", TypeName = "int" },
            new FieldInfo { Name = "Value", TypeName = "double" }
        }
    };
    
    // Generate code
    var emitter = new SerializerEmitter();
    string code = emitter.EmitSerializer(type);
    
    // Compile code
    var assembly = CompileToAssembly(code);
    var generatedType = assembly.GetType("SimplePrimitive");
    
    // Create instance and set values
    var instance = Activator.CreateInstance(generatedType);
    generatedType.GetField("Id").SetValue(instance, 123456789);
    generatedType.GetField("Value").SetValue(instance, 123.456);
    
    // Serialize
    var writer = new ArrayBufferWriter<byte>();
    var cdr = new CdrWriter(writer);
    var serializeMethod = generatedType.GetMethod("Serialize");
    serializeMethod.Invoke(instance, new object[] { cdr });
    cdr.Complete();
    
    // Verify against Golden Rig
    string expected = "00 00 00 0C 15 CD 5B 07 77 BE 9F 1A 2F DD 5E 40";
    string actual = ToHex(writer.WrittenSpan.ToArray());
    
    Assert.Equal(expected, actual);
}
```

**Estimated Time:** 12-15 hours

---

## 🧪 Testing Requirements

**Minimum Total Tests:** 15-20 tests

**Test Distribution:**
- Code Generation: 8-10 tests
- Compilation: 3-5 tests
- Execution (Golden Rig match): 4-5 tests

**Critical:** Execution tests MUST match Golden Rig byte-for-byte.

---

## 📊 Report Requirements

**Submit to:** `.dev-workstream/reports/BATCH-06-REPORT.md`

**Required Sections:**

1. **Implementation Summary**
   - Generated code examples
   - Type mapping decisions
   - Test counts

2. **Issues Encountered**
   - Roslyn compilation challenges?
   - DHEADER patching logic?
   - Alignment issues?

3. **Design Decisions**
   - How did you handle nested structs?
   - Any deviations from pattern?

4. **Golden Rig Validation**
   - **MUST INCLUDE:** Which test cases match byte-for-byte
   - Any discrepancies and how fixed

5. **Next Steps**
   - What's needed for variable types (FCDC-S011)?

---

## 🎯 Success Criteria

This batch is DONE when:

- ✅ **FCDC-S010** Complete: Serializer emitter for fixed types
- ✅ All 15-20 tests passing
- ✅ **Generated code compiles** (verified via Roslyn)
- ✅ **Generated code produces byte-perfect output** (Golden Rig match)
- ✅ GetSerializedSize matches actual Serialize output
- ✅ No compiler warnings in generated code
- ✅ Report submitted to `.dev-workstream/reports/BATCH-06-REPORT.md`

**GATE:** Golden Rig validation MUST pass before variable types (BATCH-07).

---

## ⚠️ Common Pitfalls to Avoid

1. **Not compiling generated code:** MUST use Roslyn to verify compilation
   - Use `CSharpCompilation.Create()` + `Emit()`

2. **Shallow code generation tests:** Must verify EXECUTION, not just syntax
   - Wrong: `Assert.Contains("WriteInt32", code)`
   - Right: Compile, execute, verify serialized bytes

3. **DHEADER size mismatch:** Must patch with bodySize, not total size
   - DHEADER value = bytes AFTER DHEADER only

4. **Non-symmetric generation:** Size and Serialize must match exactly
   - Use shared logic or mode parameter

5. **Missing alignment:** `CdrWriter`/`CdrSizer` handle it, but field order matters

6. **Not testing Golden Rig match:** **BLOCKING** - must verify byte-perfect output

---

## 📚 Reference Materials

- **Task Master:** [SERDATA-TASK-MASTER.md §FCDC-S010](../docs/SERDATA-TASK-MASTER.md)
- **XCDR2 Details:** [XCDR2-IMPLEMENTATION-DETAILS.md](../docs/XCDR2-IMPLEMENTATION-DETAILS.md)
- **Golden Rig Tests:** `tests/CycloneDDS.Core.Tests/GoldenConsistencyTests.cs`
- **CdrWriter:** `Src/CycloneDDS.Core/CdrWriter.cs`
- **CdrSizer:** `Src/CycloneDDS.Core/CdrSizer.cs`
- **AlignmentMath:** `Src/CycloneDDS.Core/AlignmentMath.cs`

---

**Next Batch:** BATCH-07 (Serializer - Variable Types) - strings, sequences, variable nested structs
