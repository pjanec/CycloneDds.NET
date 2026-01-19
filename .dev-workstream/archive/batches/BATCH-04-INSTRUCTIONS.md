# BATCH-04: Schema Validation & IDL Generation

**Batch Number:** BATCH-04  
**Tasks:** FCDC-S008 (Schema Validator), FCDC-S009 (IDL Emitter)  
**Phase:** Stage 2 - Code Generation (Validation & IDL)  
**Estimated Effort:** 10-12 hours  
**Priority:** CRITICAL (required for descriptor generation)  
**Dependencies:** BATCH-03 (CLI tool infrastructure)

---

## 📋 Onboarding & Workflow

### Developer Instructions

This batch adds **schema validation** and **IDL generation** to the CLI tool. You'll ensure discovered types are valid for DDS (XCDR2 appendable compliance) and generate IDL files for Cyclone registration.

**Your Mission:** 
1. Implement schema validator to check DDS type compatibility and appendable evolution rules
2. Implement IDL emitter to generate `.idl` files from validated types

**Critical Context:** IDL is used **only for discovery/registration** with Cyclone DDS. We do NOT use IDL for serialization - that's handled by our C# code generator in future batches.

### Required Reading (IN ORDER)

1. **Workflow Guide:** `.dev-workstream/README.md` - How to work with batches
2. **Previous Reviews:** 
   - `.dev-workstream/reviews/BATCH-02-REVIEW.md` - Golden Rig validation
   - `.dev-workstream/reviews/BATCH-03-REVIEW.md` - Excellent test quality example
3. **Task Master:** `docs/SERDATA-TASK-MASTER.md` - See FCDC-S008, FCDC-S009
4. **Design Document:** `docs/SERDATA-DESIGN.md` - Section 4 (Stage 2), Section 7.2 (Appendable)
5. **XCDR2 Details:** `docs/XCDR2-IMPLEMENTATION-DETAILS.md` - Alignment rules
6. **Old Implementation:** 
   - `old_implem/src/CycloneDDS.Generator/SchemaValidator.cs` - Validation logic to adapt
   - `old_implem/src/CycloneDDS.Generator/IdlEmitter.cs` - IDL generation to adapt

### Source Code Location

- **CLI Tool:** `tools/CycloneDDS.CodeGen/` (extend from BATCH-03)
- **Test Project:** `tests/CycloneDDS.CodeGen.Tests/` (extend from BATCH-03)

### Report Submission

**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-04-REPORT.md`

**Use template:**  
`.dev-workstream/templates/BATCH-REPORT-TEMPLATE.md`

**If you have questions, create:**  
`.dev-workstream/questions/BATCH-04-QUESTIONS.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1 (Schema Validator):** Implement → Write tests → **ALL tests pass** ✅
2. **Task 2 (IDL Emitter):** Implement → Write tests → **ALL tests pass** ✅

**DO NOT** move to Task 2 until:
- ✅ Task 1 implementation complete
- ✅ Task 1 tests written
- ✅ **ALL tests passing** (including BATCH-01/02/03 tests)

**Why:** IDL emitter depends on validated type information from validator.

---

## Context

**BATCH-03 Complete:** CLI tool discovers types with `[DdsTopic]`, extracts name/namespace.

**This Batch:** Add validation and IDL output:
- Validate types meet DDS requirements (no circular dependencies, valid field types, appendable evolution rules)
- Generate `.idl` files for Cyclone registration

**Related Tasks:**
- [FCDC-S008](../docs/SERDATA-TASK-MASTER.md#fcdc-s008-schema-validator) - Schema validation
- [FCDC-S009](../docs/SERDATA-TASK-MASTER.md#fcdc-s009-idl-emitter-discovery-only) - IDL generation

**Why IDL Matters:**
- Cyclone DDS needs IDL to generate type descriptors for discovery
- IDL defines wire format for interoperability
- Our C# serializer will match this wire format exactly

---

## 🎯 Batch Objectives

**Primary Goal:** Validate discovered types and generate IDL for Cyclone registration.

**Success Metrics:** 
- Schema validator detects invalid types and appendable violations
- IDL generator produces compilable `.idl` files
- All tests pass

---

## ✅ Tasks

### Task 1: Schema Validator (FCDC-S008)

**Files:** `tools/CycloneDDS.CodeGen/SchemaValidator.cs` (NEW)  
**Task Definition:** See [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md#fcdc-s008-schema-validator)

**Description:**  
Implement schema validation to ensure discovered types are valid for DDS and follow appendable evolution rules.

**Design Reference:** [SERDATA-DESIGN.md §7.2](../docs/SERDATA-DESIGN.md), old_implem/src/CycloneDDS.Generator/SchemaValidator.cs

**Validation Rules to Implement:**

#### 1. Appendable Evolution Rules (CRITICAL for XCDR2)

All `[DdsTopic]` types are **@appendable** by default. This means:

**✅ ALLOWED:**
- Adding new fields **at the end**
- Making fields optional (via `[DdsOptional]`)

**❌ FORBIDDEN:**
- Removing fields
- Changing field types
- Reordering fields
- Adding fields in the middle

**Why:** XCDR2 appendable types use DHEADER. Readers must be able to skip unknown fields at the end.

**Implementation:**
```csharp
public class SchemaValidator
{
    public ValidationResult Validate(TypeInfo type)
    {
        var errors = new List<string>();
        
        // 1. Check for circular dependencies
        if (HasCircularDependency(type))
            errors.Add($"Circular dependency detected in {type.FullName}");
        
        // 2. Validate field types
        foreach (var field in type.Fields)
        {
            if (!IsValidFieldType(field.Type))
                errors.Add($"Invalid field type: {field.Type} in {type.FullName}.{field.Name}");
        }
        
        // 3. Check union structure (if [DdsUnion])
        if (type.HasAttribute("DdsUnion"))
        {
            ValidateUnion(type, errors);
        }
        
        return new ValidationResult(errors);
    }
    
    private void ValidateUnion(TypeInfo type, List<string> errors)
    {
        // Must have exactly one [DdsDiscriminator]
        var discriminators = type.Fields.Where(f => f.HasAttribute("DdsDiscriminator")).ToList();
        if (discriminators.Count != 1)
            errors.Add($"Union {type.FullName} must have exactly one [DdsDiscriminator]");
        
        // All [DdsCase] values must be unique
        var caseValues = new HashSet<int>();
        foreach (var field in type.Fields.Where(f => f.HasAttribute("DdsCase")))
        {
            var cases = field.GetAttribute("DdsCase").CaseValues;
            foreach (var c in cases)
            {
                if (!caseValues.Add(c))
                    errors.Add($"Duplicate case value {c} in union {type.FullName}");
            }
        }
    }
}
```

#### 2. Type Mapping Validation

**Valid Field Types:**
- Primitives: `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `bool`, `char`
- Fixed strings: `FixedString32`, `FixedString64`, `FixedString128`, `FixedString256`
- Sequences: `BoundedSeq<T>` (with explicit capacity)
- Nested structs: Other `[DdsTopic]` types (check for cycles!)
- Enums: C# enums

**Invalid:**
- `System.String` (unless field has `[DdsManaged]`)
- `List<T>`, `Dictionary<K, V>` (unless `[DdsManaged]`)
- Pointers, delegates, generic types

#### 3. Circular Dependency Detection

```csharp
private bool HasCircularDependency(TypeInfo type, HashSet<string> visited = null)
{
    visited ??= new HashSet<string>();
    
    if (!visited.Add(type.FullName))
        return true; // Cycle detected
    
    foreach (var field in type.Fields)
    {
        if (field.Type is TypeInfo nestedType)
        {
            if (Has CircularDependency(nestedType, new HashSet<string>(visited)))
                return true;
        }
    }
    
    return false;
}
```

**Deliverables:**
- `tools/CycloneDDS.CodeGen/SchemaValidator.cs`
- `tools/CycloneDDS.CodeGen/ValidationResult.cs`
- Integration into `CodeGenerator.cs` (call validator before emitting)

**Tests Required:** (Add to `tests/CycloneDDS.CodeGen.Tests/`)

**Minimum 12-15 tests:**
1. ✅ Valid struct with primitives passes
2. ✅ Struct with invalid field type fails
3. ✅ Circular dependency detected (A contains B, B contains A)
4. ✅ Union without discriminator fails
5. ✅ Union with multiple discriminators fails
6. ✅ Union with duplicate case values fails
7. ✅ Valid union passes
8. ✅ Nested struct without cycle passes
9. ✅ Self-referential struct detected
10. ✅ String field without `[DdsManaged]` fails
11. ✅ FixedString field passes
12. ✅ BoundedSeq field passes
13. ✅ Enum field passes
14. ✅ Invalid generic type fails
15. ✅ Validation errors have clear messages

**Quality Standard:**
- Tests must create **actual TypeInfo objects** with fields
- Tests must verify **actual validation logic**, not just "method returns"
- Tests must check **error messages** are informative

**Example Good Test:**
```csharp
[Fact]
public void Validator_DetectsCircularDependency()
{
    var typeA = new TypeInfo 
    { 
        Name = "A", 
        Fields = new[] 
        { 
            new FieldInfo { Name = "B", Type = new TypeInfo { Name = "B" } } 
        } 
    };
    var typeB = typeA.Fields[0].Type as TypeInfo;
    typeB.Fields = new[] 
    { 
        new FieldInfo { Name = "A", Type = typeA } 
    };
    
    var validator = new SchemaValidator();
    var result = validator.Validate(typeA);
    
    Assert.False(result.IsValid);
    Assert.Contains("circular", result.Errors[0].ToLower());
}
```

**Estimated Time:** 5-6 hours

---

### Task 2: IDL Emitter (FCDC-S009)

**Files:** `tools/CycloneDDS.CodeGen/IdlEmitter.cs` (NEW)  
**Task Definition:** See [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md#fcdc-s009-idl-emitter-discovery-only)

**Description:**  
Generate IDL files from validated types. IDL is used **only for Cyclone registration**, not for C# serialization.

**Design Reference:** [SERDATA-DESIGN.md §4](../docs/SERDATA-DESIGN.md), old_implem/src/CycloneDDS.Generator/IdlEmitter.cs

**IDL Generation Rules:**

#### 1. All Types are @appendable

```idl
@appendable
struct SensorData {
    int32 id;
    double value;
    string message;
};
```

#### 2. Key Fields

```csharp
[DdsTopic]
struct EntityData 
{
    [DdsKey]
    public int Id;
    public string Name;
}
```

**Generates:**
```idl
@appendable
struct EntityData {
    @key int32 id;
    string name;
};
```

#### 3. Type Mapping (C# → IDL)

| C# Type | IDL Type |
|---------|----------|
| `byte` | `octet` |
| `sbyte` | `int8` |
| `short` | `int16` |
| `ushort` | `uint16` |
| `int` | `int32` |
| `uint` | `uint32` |
| `long` | `int64` |
| `ulong` | `uint64` |
| `float` | `float` |
| `double` | `double` |
| `bool` | `boolean` |
| `char` | `char` |
| `FixedString32` | `char message[32]` |
| `BoundedSeq<int>` | `sequence<int32, 100>` |
| `string` (with `[DdsManaged]`) | `string` |

#### 4. Unions

```csharp
[DdsUnion]
struct Command
{
    [DdsDiscriminator]
    public CommandKind Kind;
    
    [DdsCase(CommandKind.Move)]
    public MoveData Move;
    
    [DdsCase(CommandKind.Spawn)]
    public SpawnData Spawn;
}
```

**Generates:**
```idl
union Command switch (CommandKind) {
    case MOVE: MoveData move;
    case SPAWN: SpawnData spawn;
};
```

#### 5. Nested Structs

Forward-declare nested types, emit all types in dependency order.

**Implementation:**
```csharp
public class IdlEmitter
{
    public string EmitIdl(TypeInfo type)
    {
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine("// Auto-generated by CycloneDDS.CodeGen");
        sb.AppendLine();
        
        // Module (namespace)
        if (!string.IsNullOrEmpty(type.Namespace))
        {
            sb.AppendLine($"module {type.Namespace.Replace('.', '_')} {{");
            sb.AppendLine();
        }
        
        // Type definition
        if (type.HasAttribute("DdsUnion"))
        {
            EmitUnion(sb, type);
        }
        else
        {
            EmitStruct(sb, type);
        }
        
        // Close module
        if (!string.IsNullOrEmpty(type.Namespace))
        {
            sb.AppendLine("}; // module");
        }
        
        return sb.ToString();
    }
    
    private void EmitStruct(StringBuilder sb, TypeInfo type)
    {
        sb.AppendLine("@appendable");
        sb.AppendLine($"struct {type.Name} {{");
        
        foreach (var field in type.Fields)
        {
            string idlType = MapType(field.Type);
            string annotations = "";
            
            if (field.HasAttribute("DdsKey"))
                annotations = "@key ";
            
            if (field.HasAttribute("DdsOptional"))
                annotations += "@optional ";
            
            sb.AppendLine($"    {annotations}{idlType} {ToCamelCase(field.Name)};");
        }
        
        sb.AppendLine("};");
    }
    
    private string MapType(TypeInfo type)
    {
        // Map C# types to IDL types
        // Handle FixedString, BoundedSeq, primitives, nested structs
    }
}
```

**Deliverables:**
- `tools/CycloneDDS.CodeGen/IdlEmitter.cs`
- Integration into `CodeGenerator.cs` (write `.idl` files to output directory)
- Helper: `tools/CycloneDDS.CodeGen/IdlTypeMapper.cs`

**Tests Required:** (Add to `tests/CycloneDDS.CodeGen.Tests/`)

**Minimum 12-15 tests:**
1. ✅ Simple struct emits correct IDL
2. ✅ Struct with key field emits `@key` annotation
3. ✅ All primitive types map correctly
4. ✅ FixedString emits `char[N]` array
5. ✅ BoundedSeq emits `sequence<T, N>`
6. ✅ Nested struct included in IDL
7. ✅ Union emits `union switch` syntax
8. ✅ Module (namespace) wraps types
9. ✅ Field names convert to camelCase
10. ✅ Optional field emits `@optional`
11. ✅ Enum emits IDL enum
12. ✅ Generated IDL is compilable by `idlc` (validation test)
13. ✅ Multiple types in same namespace share module
14. ✅ Type dependencies emit in correct order
15. ✅ @appendable annotation present on all structs

**Quality Standard:**
- Tests must verify **actual IDL syntax**, not just "contains string"
- Tests should validate **compilable IDL** (can be parsed)
- Use string comparison or IDL parser for validation

**Example Good Test:**
```csharp
[Fact]
public void IdlEmitter_GeneratesCorrectSyntax_ForSimpleStruct()
{
    var type = new TypeInfo
    {
        Name = "SensorData",
        Namespace = "MyApp",
        Fields = new[]
        {
            new FieldInfo { Name = "Id", Type = PrimitiveType.Int32 },
            new FieldInfo { Name = "Value", Type = PrimitiveType.Double,  }
        }
    };
    
    var emitter = new IdlEmitter();
    string idl = emitter.EmitIdl(type);
    
    Assert.Contains("@appendable", idl);
    Assert.Contains("struct SensorData", idl);
    Assert.Contains("int32 id;", idl);
    Assert.Contains("double value;", idl);
    Assert.Contains("module MyApp", idl);
}
```

**Estimated Time:** 5-6 hours

---

## 🧪 Testing Requirements

**Minimum Total Tests:** 24-30 new tests

**Test Distribution:**
- SchemaValidator tests: 12-15 tests
- IdlEmitter tests: 12-15 tests

**Test Quality Standards:**

**✅ REQUIRED:**
- Validator tests create **actual TypeInfo objects** with fields
- Validator tests verify **actual validation logic** and error messages
- IDL tests verify **actual IDL syntax** (not just string presence)
- Consider testing with actual `idlc` compiler (if available)

**❌ NOT ACCEPTABLE:**
- Tests that just check "method returns string"
- Shallow validation tests that don't check error messages
- IDL tests that don't verify syntax structure

**All tests must pass before submitting report.**

---

## 📊 Report Requirements

Use template: `.dev-workstream/templates/BATCH-REPORT-TEMPLATE.md`

**Required Sections:**

1. **Implementation Summary**
   - Tasks completed (FCDC-S008, FCDC-S009)
   - Test counts
   - Any deviations from instructions

2. **Issues Encountered**
   - Roslyn API challenges for type analysis?
   - IDL syntax edge cases?
   - Validation logic complexity?

3. **Design Decisions**
   - How did you handle type dependency ordering?
   - What validation error format did you choose?
   - Any simplifications in IDL generation?

4. **Weak Points Spotted**
   - Areas needing more robust validation?
   - IDL syntax edge cases to handle later?
   - Type mapping gaps?

5. **Next Steps**
   - What's needed for IDL compiler integration (FCDC-S008b)?
   - Dependencies for serializer generation (FCDC-S010)?

---

## 🎯 Success Criteria

This batch is DONE when:

- ✅ **FCDC-S008** Complete: Schema validator implemented, 12-15 tests pass
- ✅ **FCDC-S009** Complete: IDL emitter implemented, 12-15 tests pass
- ✅ All 101-107 tests passing (77 existing + 24-30 new)
- ✅ No compiler warnings
- ✅ CLI tool validates types and generates `.idl` files
- ✅ Generated IDL is syntactically correct
- ✅ Report submitted to `.dev-workstream/reports/BATCH-04-REPORT.md`

**GATE:** Schema validator must detect invalid types before moving to serializer generation (BATCH-05).

---

## ⚠️ Common Pitfalls to Avoid

1. **Shallow validation tests:** Must verify actual error messages
   - Wrong: `Assert.False(result.IsValid)`
   - Right: `Assert.Contains("circular dependency", result.Errors[0])`

2. **String-presence IDL tests:** Must verify syntax structure
   - Wrong: `Assert.Contains("struct", idl)`
   - Right: `Assert.Matches(@"@appendable\s+struct SensorData", idl)`

3. **Forgetting @appendable annotation:** All structs must have it

4. **Not handling nested types:** IDL must include dependencies

5. **Case conversion errors:** C# PascalCase → IDL camelCase

6. **Type mapping gaps:** Ensure all valid C# types map to IDL

---

## 📚 Reference Materials

- **Task Master:** [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md) - FCDC-S008, S009
- **Design:** [SERDATA-DESIGN.md](../docs/SERDATA-DESIGN.md) - Section 4, 7.2
- **XCDR2 Details:** [XCDR2-IMPLEMENTATION-DETAILS.md](../docs/XCDR2-IMPLEMENTATION-DETAILS.md)
- **Old Validator:** `old_implem/src/CycloneDDS.Generator/SchemaValidator.cs`
- **Old IDL Emitter:** `old_implem/src/CycloneDDS.Generator/IdlEmitter.cs`
- **IDL Spec:** OMG IDL 4.2 Specification (for syntax reference)
- **DDS-XTypes Spec:** OMG DDS-XTypes 1.3 (for @appendable, @key, etc.)

---

**Next Batch:** BATCH-05 (IDL Compiler Integration + Descriptor Parser) - External tool orchestration
