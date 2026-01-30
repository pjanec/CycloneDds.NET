# BATCH-16: Nested Struct Support + Type-Level Managed Attribute

**Batch Number:** BATCH-16  
**Tasks:** FCDC-S023 (Nested Struct Support), FCDC-S024 (Type-Level [DdsManaged])  
**Phase:** Stage 2 - Code Generation Enhancements  
**Estimated Effort:** 3-4 days  
**Priority:** HIGH  
**Dependencies:** Stage 2 complete (BATCH-01 through BATCH-15.3)

---

## 📋 Onboarding & Workflow

### Developer Instructions

Welcome to BATCH-16! You will implement two critical code generator enhancements:
1. **FCDC-S023:** Enable nested struct support via `[DdsStruct]` attribute with strict validation
2. **FCDC-S024:** Allow `[DdsManaged]` at type level (convenience feature)

These enhancements are essential for production readiness, enabling complex data models and reducing boilerplate.

### Required Reading (IN ORDER)

**READ THESE BEFORE STARTING:**

1. **Workflow Guide:** `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\README.md`  
   - Understand batch system, report requirements

2. **Task Definitions:** `d:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md`  
   - Section: FCDC-S023 (lines 946-1001)
   - Section: FCDC-S024 (lines 1021-1099)

3. **Design Documents:**  
   - `d:\Work\FastCycloneDdsCsharpBindings\docs\NESTED-STRUCT-SUPPORT-DESIGN.md` ← **CRITICAL** for S023
   - Read entire file (371 lines) - contains implementation pattern, validation logic, examples


### Repository Structure

```
d:\Work\FastCycloneDdsCsharpBindings\
├── src\
│   ├── CycloneDDS.Core\              # CDR serialization primitives
│   ├── CycloneDDS.Schema\            # Attributes ([DdsTopic], [DdsKey], etc.)
│   │   └── Attributes\
│   │       ├── TypeLevel\            # ← You'll add DdsStructAttribute.cs here
│   │       └── FieldLevel\
│   └── CycloneDDS.Runtime\           # DDS runtime (Participant, Writer, Reader)
│
├── tools\
│   └── CycloneDDS.CodeGen\           # CLI code generator (YOU WORK HERE)
│       ├── CycloneDDS.CodeGen.csproj # Project file
│       ├── Program.cs                # Entry point
│       ├── SchemaDiscovery.cs        # ← MODIFY for [DdsStruct] discovery
│       ├── SchemaValidator.cs        # ← MODIFY for strict type validation
│       ├── CodeGenerator.cs          # ← MODIFY for validation integration
│       ├── SerializerEmitter.cs      # ← MODIFY for type-level [DdsManaged]
│       ├── DeserializerEmitter.cs    # ← MODIFY for type-level [DdsManaged]
│       ├── IdlEmitter.cs             # ← MODIFY to emit [DdsStruct] types
│       └── ManagedTypeValidator.cs   # ← MODIFY for type-level checking
│
├── tests\
│   └── CycloneDDS.CodeGen.Tests\     # Code generator tests
│       ├── CycloneDDS.CodeGen.Tests.csproj
│       ├── SchemaDiscoveryTests.cs   # ← ADD tests here
│       ├── SchemaValidatorTests.cs   # ← ADD tests here
│       └── GeneratorIntegrationTests.cs # ← ADD roundtrip tests here
│
├── cyclone-compiled\                 # Cyclone DDS native binaries
│   ├── bin\
│   │   ├── idlc.exe                  # IDL compiler (used by tests)
│   │   ├── ddsc.dll                  # DDS native library
│   │   └── *.dll                     # MSVC runtime libraries
│   ├── lib\                          # Static libraries
│   └── include\                      # C headers
│
└── .dev-workstream\
    ├── batches\
    │   └── BATCH-16-INSTRUCTIONS.md  # ← This file
    └── reports\
        └── BATCH-16-REPORT.md        # ← Submit your report here
```

### Critical Tool & Library Locations

**IDL Compiler:**
- **Location:** `d:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\idlc.exe`
- **Usage:** Tests run this automatically via relative path calculation
- **Do NOT modify:** Already configured in ErrorHandlingTests.cs (BATCH-15.3)

**DDS Native Library:**
- **Location:** `d:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\ddsc.dll`
- **Usage:** Runtime tests link against this
- **Modified:** Custom serdata export (BATCH-13.2)

**Projects to Build:**

Build order (dependencies):
```bash
# 1. Schema (attributes)
dotnet build d:\Work\FastCycloneDdsCsharpBindings\src\CycloneDDS.Schema\CycloneDDS.Schema.csproj

# 2. Code Generator
dotnet build d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj

# 3. Tests
dotnet build d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.CodeGen.Tests\CycloneDDS.CodeGen.Tests.csproj

# 4. Run all tests
dotnet test d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.CodeGen.Tests\CycloneDDS.CodeGen.Tests.csproj
```

### Report Submission

**When done, submit your report to:**  
`d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reports\BATCH-16-REPORT.md`

**Use template:**  
`d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\templates\BATCH-REPORT-TEMPLATE.md`

**If you have questions before starting, create:**  
`d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\questions\BATCH-16-QUESTIONS.md`

---

## Context

### Why This Batch Matters

**Production Blocker:**  
Users cannot create complex data models without nested structs. Current limitation forces flat structures or manual marshalling.

**Example from Real DDS Systems:**
```csharp
// Common robotics pattern (IMPOSSIBLE today)
[DdsTopic("RobotState")]
public partial struct RobotState
{
    [DdsKey] public int RobotId;
    public Point3D Position;               // ERROR: Point3D not discovered
    public BoundedSeq<Waypoint> Path;      // ERROR: Waypoint not discovered
}

// What users MUST do today (ugly workaround)
[DdsTopic("RobotState")]
public partial struct RobotState
{
    [DdsKey] public int RobotId;
    public double PositionX, PositionY, PositionZ;  // Flat - no semantics
    // Can't use custom types at all!
}
```

**This batch fixes both issues:**
1. FCDC-S023: Enables `[DdsStruct]` for helper types
2. FCDC-S024: Reduces boilerplate for managed types

**Related Tasks:**
- [FCDC-S023](../docs/SERDATA-TASK-MASTER.md#fcdc-s023-nested-struct-support-ddsstruct-attribute) - Nested Struct Support
- [FCDC-S024](../docs/SERDATA-TASK-MASTER.md#fcdc-s024-type-level-ddsmanaged-attribute) - Type-Level Managed Attribute

---

## 🎯 Batch Objectives

**You will accomplish:**

1. ✅ Add `[DdsStruct]` attribute to CycloneDDS.Schema
2. ✅ Update SchemaDiscovery to find `[DdsStruct]` types
3. ✅ Implement strict type validation (all nested types must be marked)
4. ✅ Support recursive validation for collections (`BoundedSeq<CustomType>`)
5. ✅ Update CodeGenerator to validate before emitting
6. ✅ Update IdlEmitter to emit `[DdsStruct]` types
7. ✅ Enable type-level `[DdsManaged]` (convenience)
8. ✅ Write 10 tests (7 for S023, 3 for S024)

**Success:** Users can compose complex data models with nested structs and use cleaner managed type syntax.

---

## ✅ Tasks

### Task 1: Add DdsStructAttribute (FCDC-S023 Part 1)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\src\CycloneDDS.Schema\Attributes\TypeLevel\DdsStructAttribute.cs` **(NEW FILE)**

**Task Definition:** [FCDC-S023](../docs/SERDATA-TASK-MASTER.md#fcdc-s023-nested-struct-support-ddsstruct-attribute)

**Description:**  
Create new attribute for marking helper structs that generate serialization but are not Topics.

**Requirements:**

```csharp
using System;

namespace CycloneDDS.Schema
{
    /// <summary>
    /// Marks a struct or class as a DDS data type that can be nested within Topics.
    /// Triggers code generation for serialization but does not define a Topic.
    /// Use this for helper types like Point3D, Quaternion, etc.
    /// </summary>
    /// <example>
    /// <code>
    /// [DdsStruct]
    /// public partial struct Point3D
    /// {
    ///     public double X;
    ///     public double Y;
    ///     public double Z;
    /// }
    /// 
    /// [DdsTopic("Robot")]
    /// public partial struct RobotState
    /// {
    ///     [DdsKey] public int Id;
    ///     public Point3D Position;  // Uses the [DdsStruct] type
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false)]
    public sealed class DdsStructAttribute : Attribute
    {
    }
}
```

**Design Reference:** NESTED-STRUCT-SUPPORT-DESIGN.md Section 2.1

**Tests Required:**
- ✅ Attribute compiles
- ✅ Can be applied to struct
- ✅ Can be applied to class
- ✅ Cannot be applied multiple times (AllowMultiple = false)

---

### Task 2: Update SchemaDiscovery (FCDC-S023 Part 2)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\SchemaDiscovery.cs` **(MODIFY)**

**Task Definition:** [FCDC-S023](../docs/SERDATA-TASK-MASTER.md#fcdc-s023-nested-struct-support-ddsstruct-attribute)

**Description:**  
Extend type discovery to find `[DdsStruct]` types in addition to `[DdsTopic]` types.

**Requirements:**

1. Locate the type discovery loop (searches for `[DdsTopic]`)
2. Add `[DdsStruct]` detection:
   ```csharp
   bool isTopic = HasAttribute(typeSymbol, "CycloneDDS.Schema.DdsTopicAttribute");
   bool isStruct = HasAttribute(typeSymbol, "CycloneDDS.Schema.DdsStructAttribute"); // NEW
   bool isEnum = typeSymbol.TypeKind == TypeKind.Enum;
   
   if (isTopic || isStruct || isEnum)
   {
       var typeInfo = ExtractTypeInfo(typeSymbol);
       typeInfo.IsTopic = isTopic;   // Flag for IDL generation
       typeInfo.IsStruct = isStruct; // NEW
       typeInfo.IsEnum = isEnum;
       discoveredTypes.Add(typeInfo);
   }
   ```
3. Ensure `TypeInfo` class has `IsStruct` property (add if missing)

**Design Reference:** NESTED-STRUCT-SUPPORT-DESIGN.md Section 2.2

**Tests Required:**
- ✅ `Discovery_DdsStruct_Found`: Struct with `[DdsStruct]` is discovered
- ✅ `Discovery_DdsTopic_StillWorks`: Existing topic discovery unchanged
- ✅ `Discovery_Mixed_FindsBoth`: Project with both `[DdsTopic]` and `[DdsStruct]` finds all

---

### Task 3: Implement Strict Type Validation (FCDC-S023 Part 3)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\SchemaValidator.cs` **(MODIFY)**

**Task Definition:** [FCDC-S023](../docs/SERDATA-TASK-MASTER.md#fcdc-s023-nested-struct-support-ddsstruct-attribute)

**Description:**  
Add validation logic to ensure all nested custom types are properly marked with `[DdsStruct]` or `[DdsTopic]`.

**Requirements:**

1. **Build Type Registry** (constructor):
   ```csharp
   private readonly HashSet<string> _knownTypeNames;
   
   public SchemaValidator(IEnumerable<TypeInfo> discoveredTypes)
   {
       _knownTypeNames = new HashSet<string>(
           discoveredTypes.Select(t => t.FullName)
       );
   }
   ```

2. **Validate Each Field Type:**
   ```csharp
   private void ValidateFieldType(FieldInfo field, string containerName, List<string> errors)
   {
       string typeName = field.TypeName;
       
       // Handle nullable
       if (typeName.EndsWith("?"))
           typeName = typeName.TrimEnd('?');
       
       // Primitives OK
       if (TypeMapper.IsPrimitive(typeName)) return;
       
       // Known wrappers OK
       if (typeName.Contains("FixedString")) return;
       if (typeName == "Guid" || typeName == "DateTime" || typeName == "TimeSpan") return;
       if (typeName.Contains("Vector") || typeName == "Quaternion") return;
       
       // Collections - recurse
       if (typeName.StartsWith("BoundedSeq<") || typeName.StartsWith("List<"))
       {
           string innerType = ExtractGenericArgument(typeName);
           // Recursively validate inner type
           if (!IsValidUserType(innerType) && !TypeMapper.IsPrimitive(innerType) && innerType != "string")
           {
               errors.Add($"Field '{containerName}.{field.Name}' uses collection of type '{innerType}', " +
                          $"which is not a valid DDS type. Mark '{innerType}' with [DdsStruct] or [DdsTopic].");
           }
           return;
       }
       
       // Managed strings
       if (typeName == "string")
       {
           // Already validated by ManagedTypeValidator
           return;
       }
       
       // User-defined types - THE STRICT CHECK
       if (!IsValidUserType(typeName))
       {
           errors.Add($"Field '{containerName}.{field.Name}' uses type '{typeName}', " +
                      $"which is not a valid DDS type. " +
                      $"Did you forget to add [DdsStruct] or [DdsTopic] to '{typeName}'?");
       }
   }
   
   private bool IsValidUserType(string typeName)
   {
       return _knownTypeNames.Contains(typeName);
   }
   
   private string ExtractGenericArgument(string typeName)
   {
       int start = typeName.IndexOf('<') + 1;
       int end = typeName.LastIndexOf('>');
       if (start > 0 && end > start)
       {
           return typeName.Substring(start, end - start).Trim();
       }
       return typeName;
   }
   ```

**Critical Error Messages:**
```
Field 'RobotPath.StartLocation' uses type 'Point3D', which is not a valid DDS type. 
Did you forget to add [DdsStruct] or [DdsTopic] to 'Point3D'?

Field 'RobotPath.Waypoints' uses collection of type 'Waypoint', which is not a valid DDS type. 
Mark 'Waypoint' with [DdsStruct] or [DdsTopic].
```

**Design Reference:** NESTED-STRUCT-SUPPORT-DESIGN.md Section 2.3

**Tests Required:**
- ✅ `Validation_UnknownStruct_EmitsError`: Nested type without attribute → error
- ✅ `Validation_KnownStruct_Passes`: Nested type with `[DdsStruct]` → no error
- ✅ `Validation_NestedSequence_UnknownType_EmitsError`: `BoundedSeq<Unknown>` → error
- ✅ `Validation_NestedSequence_KnownType_Passes`: `BoundedSeq<KnownStruct>` → no error

---

### Task 4: Update CodeGenerator Integration (FCDC-S023 Part 4)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\CodeGenerator.cs` **(MODIFY)**

**Task Definition:** [FCDC-S023](../docs/SERDATA-TASK-MASTER.md#fcdc-s023-nested-struct-support-ddsstruct-attribute)

**Description:**  
Integrate validation into the generation pipeline and generate serializers for both `[DdsTopic]` and `[DdsStruct]`.

**Requirements:**

```csharp
public void Generate(string sourceDir, string outputDir)
{
    // 1. Discover all types
    var types = _discovery.DiscoverTopics(sourceDir);
    
    // 2. Validate ALL types with strict checking
    var validator = new SchemaValidator(types);  // Pass all discovered types
    
    bool hasErrors = false;
    foreach (var type in types)
    {
        var result = validator.Validate(type);
        if (!result.IsValid)
        {
            hasErrors = true;
            foreach (var err in result.Errors)
            {
                Console.Error.WriteLine($"ERROR: {err}");
            }
        }
    }
    
    if (hasErrors)
    {
        throw new InvalidOperationException("Schema validation failed. Fix errors above.");
    }
    
    // 3. Generate serialization for both [DdsTopic] and [DdsStruct]
    foreach (var type in types)
    {
        if (type.IsTopic || type.IsStruct)
        {
            _serializerEmitter.Emit(type, outputDir);
            _deserializerEmitter.Emit(type, outputDir);
        }
        
        // 4. Generate IDL and descriptor ONLY for [DdsTopic]
        if (type.IsTopic)
        {
            _idlEmitter.Emit(type, outputDir);
            _descriptorEmitter.Emit(type, outputDir);
        }
    }
}
```

**Design Reference:** NESTED-STRUCT-SUPPORT-DESIGN.md Section 3

---

### Task 5: Update IdlEmitter (FCDC-S023 Part 5)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\IdlEmitter.cs` **(MODIFY)**

**Task Definition:** [FCDC-S023](../docs/SERDATA-TASK-MASTER.md#fcdc-s023-nested-struct-support-ddsstruct-attribute)

**Description:**  
Ensure `[DdsStruct]` types are emitted to IDL files (needed for nested references).

**Requirements:**

1. Emit `[DdsStruct]` types as plain `@appendable struct`
2. Emit before any `[DdsTopic]` that uses them (dependency order)
3. Example output:
   ```idl
   // Helper struct (from [DdsStruct])
   @appendable
   struct Point3D {
       @id(0) double X;
       @id(1) double Y;
       @id(2) double Z;
   };
   
   // Topic (from [DdsTopic])
   @appendable
   struct RobotPath {
       @key @id(0) long RobotId;
       @id(1) Point3D StartLocation;
   };
   ```

**Design Reference:** NESTED-STRUCT-SUPPORT-DESIGN.md Section 4

---

### Task 6: Integration Tests (FCDC-S023 Part 6)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.CodeGen.Tests\GeneratorIntegrationTests.cs` **(MODIFY)**

**Task Definition:** [FCDC-S023](../docs/SERDATA-TASK-MASTER.md#fcdc-s023-nested-struct-support-ddsstruct-attribute)

**Description:**  
End-to-end tests proving nested structs work.

**Tests Required:**

**Test 1: CodeGen_NestedStruct_Compiles**
```csharp
[Fact]
public void CodeGen_NestedStruct_Compiles()
{
    // Define test types
    string source = @"
        using CycloneDDS.Schema;
        
        namespace Test {
            [DdsStruct]
            public partial struct Point3D
            {
                public double X;
                public double Y;
                public double Z;
            }
            
            [DdsTopic(""Robot"")]
            public partial struct RobotState
            {
                [DdsKey] public int Id;
                public Point3D Position;
            }
        }
    ";
    
    // Generate code
    var generatedCode = RunGenerator(source);
    
    // Verify compilation
    var assembly = CompileToAssembly(source, generatedCode);
    Assert.NotNull(assembly); // Success: Compiles without errors
}
```

**Test 2: Roundtrip_NestedStruct_Preserves**
```csharp
[Fact]
public void Roundtrip_NestedStruct_Preserves()
{
    // Setup: Compile generated code
    var assembly = CompileIntegratedCode(@"
        [DdsStruct]
        public partial struct Point3D { public double X, Y, Z; }
        
        [DdsTopic(""Robot"")]
        public partial struct RobotState {
            [DdsKey] public int Id;
            public Point3D Position;
        }
    ");
    
    // Create instance with nested data
    var robot = CreateInstance(assembly, "RobotState");
    robot.Id = 42;
    robot.Position.X = 1.0;
    robot.Position.Y = 2.0;
    robot.Position.Z = 3.0;
    
    // Serialize
    var bytes = Serialize(robot);
    
    // Deserialize
    var view = Deserialize(bytes, assembly, "RobotStateView");
    
    // Assert: Nested struct preserved
    Assert.Equal(42, view.Id);
    Assert.Equal(1.0, view.Position.X);
    Assert.Equal(2.0, view.Position.Y);
    Assert.Equal(3.0, view.Position.Z);
}
```

---

### Task 7: Type-Level [DdsManaged] Support (FCDC-S024)

**Files:**  
- `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\ManagedTypeValidator.cs` **(MODIFY)**
- `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\SerializerEmitter.cs` **(MODIFY)**
- `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\DeserializerEmitter.cs` **(MODIFY)**

**Task Definition:** [FCDC-S024](../docs/SERDATA-TASK-MASTER.md#fcdc-s024-type-level-ddsmanaged-attribute)

**Description:**  
Allow `[DdsManaged]` at type level to apply to all managed fields (convenience).

**Current Behavior (Verbose):**
```csharp
[DdsTopic("LogEvent")]
public partial struct LogEvent
{
    [DdsKey] public int Id;
    [DdsManaged] public string Message;     // Must mark each field
    [DdsManaged] public List<string> Tags;  // Must mark each field
}
```

**Desired Behavior (Convenient):**
```csharp
[DdsTopic("LogEvent")]
[DdsManaged]  // ← Type-level: applies to all managed fields
public partial struct LogEvent
{
    [DdsKey] public int Id;
    public string Message;      // Inherits [DdsManaged]
    public List<string> Tags;   // Inherits [DdsManaged]
}
```

**Requirements:**

1. **Update ManagedTypeValidator.cs:**
   ```csharp
   private bool IsManagedContext(TypeInfo type, FieldInfo field)
   {
       bool isTypeManaged = type.HasAttribute("DdsManaged");
       bool isFieldManaged = field.HasAttribute("DdsManaged");
       
       return isTypeManaged || isFieldManaged;
   }
   
   public void ValidateField(TypeInfo type, FieldInfo field)
   {
       if (IsManagedFieldType(field.TypeName))
       {
           if (!IsManagedContext(type, field))
           {
               errors.Add($"Field '{type.Name}.{field.Name}' is a managed type (string/List), " +
                          $"but neither the field nor the containing type has [DdsManaged]. " +
                          $"Add [DdsManaged] to the field or to type '{type.Name}'.");
           }
       }
   }
   ```

2. **Update SerializerEmitter.cs:**
   ```csharp
   private bool ShouldUseManagedSerialization(TypeInfo type, FieldInfo field)
   {
       return type.HasAttribute("DdsManaged") || field.HasAttribute("DdsManaged");
   }
   ```

3. **Update DeserializerEmitter.cs:**
   ```csharp
   private bool ShouldUseManagedDeserialization(TypeInfo type, FieldInfo field)
   {
       return type.HasAttribute("DdsManaged") || field.HasAttribute("DdsManaged");
   }
   ```

**Tests Required:**

**Test 1: TypeManaged_StringField_NoFieldAttribute_Validates**
```csharp
[Fact]
public void TypeManaged_StringField_NoFieldAttribute_Validates()
{
    string source = @"
        [DdsTopic(""Log"")]
        [DdsManaged]  // Type-level
        public partial struct LogEvent
        {
            [DdsKey] public int Id;
            public string Message;  // No field attribute
        }
    ";
    
    var result = RunValidator(source);
    Assert.True(result.IsValid); // No errors
}
```

**Test 2: TypeManaged_GeneratedCode_Compiles**
```csharp
[Fact]
public void TypeManaged_GeneratedCode_Compiles()
{
    string source = @"
        [DdsTopic(""Log"")]
        [DdsManaged]
        public partial struct LogEvent
        {
            [DdsKey] public int Id;
            public string Message;
            public List<string> Tags;
        }
    ";
    
    var generatedCode = RunGenerator(source);
    var assembly = CompileToAssembly(source, generatedCode);
    Assert.NotNull(assembly);
}
```

**Test 3: TypeManaged_Roundtrip_Preserves**
```csharp
[Fact]
public void TypeManaged_Roundtrip_Preserves()
{
    var logEvent = new LogEvent 
    { 
        Id = 1, 
        Message = "Hello", 
        Tags = new List<string> { "A", "B" } 
    };
    
    var bytes = Serialize(logEvent);
    var view = Deserialize(bytes);
    
    Assert.Equal(1, view.Id);
    Assert.Equal("Hello", view.Message);
    Assert.Equal(2, view.Tags.Count);
    Assert.Equal("A", view.Tags[0]);
}
```

---

## 🧪 Testing Requirements

### Minimum Test Counts

**FCDC-S023 Tests:** 7 minimum
- 3 Discovery tests
- 4 Validation tests
- 2 Integration tests (compile + roundtrip)

**FCDC-S024 Tests:** 3 minimum
- 1 Validation test
- 1 Compilation test
- 1 Roundtrip test

**Total:** 10 tests minimum

### Test Quality Standards

**❌ BAD TEST (String Presence):**
```csharp
// This is UNACCEPTABLE
Assert.Contains("Point3D", generatedCode);
```

**✅ GOOD TEST (Actual Behavior):**
```csharp
// This is REQUIRED
var assembly = CompileToAssembly(source, generatedCode);
var instance = CreateInstance(assembly, "RobotState");
instance.Position.X = 1.0;
var bytes = Serialize(instance);
var view = Deserialize(bytes);
Assert.Equal(1.0, view.Position.X); // Actual runtime value
```

**Tests MUST verify:**
- ✅ Generated code compiles
- ✅ Runtime behavior correct (roundtrip)
- ✅ Error messages clear and actionable
- ✅ Edge cases handled (collections, nullable, etc.)

---

## 📊 Report Requirements

### Focus: Developer Insights, Not Understanding Checks

**✅ ANSWER THESE:**

**Q1:** What issues did you encounter during implementation? How did you solve them?

**Q2:** Did you spot any weak points in the existing codebase? What would you improve?

**Q3:** What design decisions did you make beyond the instructions? What alternatives did you consider?

**Q4:** What edge cases did you discover that weren't mentioned in the spec?

**Q5:** Are there any performance concerns or optimization opportunities you noticed?

**Q6:** How did you handle the type registry? Did you encounter any namespace/FullName resolution issues?

**Q7:** Did the validation error messages give enough information to fix the problem?

### Report Must Include

1. **Completion Status:** Which tasks completed, test counts
2. **Code Changes:** Files modified/created
3. **Test Results:** Pass/fail counts, any skipped tests
4. **Issues Encountered:** Problems and solutions
5. **Design Decisions:** Choices you made beyond spec
6. **Edge Cases:** Scenarios discovered during testing

---

## 🎯 Success Criteria

This batch is DONE when:

- [x] **FCDC-S023:** Nested struct support implemented
  - [x] `DdsStructAttribute` added
  - [x] SchemaDiscovery finds `[DdsStruct]` types
  - [x] SchemaValidator enforces strict type checking
  - [x] Clear error messages for unmarked types
  - [x] Collections recursively validated
  - [x] Generated code compiles
  - [x] Roundtrip test passes
  
- [x] **FCDC-S024:** Type-level managed attribute implemented
  - [x] ManagedTypeValidator checks type attribute
  - [x] SerializerEmitter respects type attribute
  - [x] DeserializerEmitter respects type attribute
  - [x] Validation passes without field attributes
  - [x] Generated code compiles
  - [x] Roundtrip test passes
  
- [x] **Tests:** Minimum 10 tests passing (7 + 3)
- [x] **Report:** Submitted to `.dev-workstream/reports/BATCH-16-REPORT.md`

---

## ⚠️ Common Pitfalls to Avoid

### Pitfall 1: Forgetting to Check Type Registration

**Problem:** Validation runs before all types discovered  
**Solution:** Build registry FIRST, then validate ALL types

### Pitfall 2: Namespace Issues

**Problem:** Type registry stores `Point3D` but field uses `MyNamespace.Point3D`  
**Solution:** Use `FullName` consistently (namespace + type name)

### Pitfall 3: Recursive Validation Missing

**Problem:** `BoundedSeq<CustomType>` not validated  
**Solution:** Extract generic argument and recursively validate

### Pitfall 4: Error Messages Too Vague

**❌ Bad:** "Invalid type"  
**✅ Good:** "Field 'RobotPath.Position' uses type 'Point3D', which is not a valid DDS type. Did you forget to add [DdsStruct] to 'Point3D'?"

### Pitfall 5: IDL Generation Order

**Problem:** Topic emitted before nested struct (idlc error)  
**Solution:** Emit `[DdsStruct]` types first, then `[DdsTopic]`

---

## 📚 Reference Materials

**Task Definitions:**
- [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md) - FCDC-S023, FCDC-S024

**Design Documents:**
- [NESTED-STRUCT-SUPPORT-DESIGN.md](../docs/NESTED-STRUCT-SUPPORT-DESIGN.md) - Complete implementation guide

**Previous Reviews:**
- [BATCH-15.3-REVIEW.md](../reviews/BATCH-15.3-REVIEW.md) - Latest completed (portability)
- [BATCH-12.1-REVIEW.md](../reviews/BATCH-12.1-REVIEW.md) - Managed types pattern

**Workflow Guide:**
- [README.md](../README.md) - Batch system, report template

**Code Examples:**
- Existing tests in `tests/CycloneDDS.CodeGen.Tests/`
- Look at `SchemaValidatorTests.cs` for validation patterns
- Look at `GeneratorIntegrationTests.cs` for roundtrip patterns

---

## 🔄 Workflow Reminder

1. ✅ Read all required documents
2. ✅ Build projects to verify environment
3. ✅ Run existing tests to establish baseline
4. ✅ Implement Task 1 → test → verify
5. ✅ Implement Task 2 → test → verify
6. ✅ Continue sequentially
7. ✅ Run ALL tests before submitting report
8. ✅ Submit report to `.dev-workstream/reports/BATCH-16-REPORT.md`

**DO NOT** skip ahead - each task builds on the previous.

**DO NOT** submit report until ALL tests pass.

---

**Good luck! This batch enables complex real-world data models. Take your time with validation logic - it's the most critical part.**
