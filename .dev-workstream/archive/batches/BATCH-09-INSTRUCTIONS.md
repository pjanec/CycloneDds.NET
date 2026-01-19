# BATCH-09: Union Support

**Batch Number:** BATCH-09  
**Tasks:** FCDC-S013 (Union Support)  
**Phase:** Stage 2 - Code Generation (Union Serialization/Deserialization)  
**Estimated Effort:** 10-12 hours  
**Priority:** HIGH (required for DDS union types)  
**Dependencies:** BATCH-08 (deserializer complete)

---

## 📋 Onboarding & Workflow

### Developer Instructions

This batch implements code generation for discriminated unions using the `[DdsUnion]` attribute.

**Your Mission:**  
Extend serializer/deserializer emitters to support union types with discriminators and case arms.

**Critical Context:**
- Unions have a discriminator field (enum or primitive) that determines active case
- Only one case arm is active at a time
- XCDR2 ser serializes: discriminator + active case value
- Must validate: discriminator value matches declared case

### ⚠️⚠️⚠️ CRITICAL TEST REQUIREMENT ⚠️⚠️⚠️

**YOU MUST RUN ALL TESTS, NOT JUST NEW UNION TESTS**

```bash
dotnet test   # Must show: total: 110+; failed: 0
```

**DESIGN DECISION:** ALL types (structs AND unions) are @appendable (always)
- Unions HAVE DHEADER (4-byte size header before discriminator)
- Enables backward compatibility when adding new union arms  
- Unknown discriminators can be safely skipped using DHEADER
- See design-talk.md lines 3840-4147 for full rationale

**UNACCEPTABLE:**
- ❌ "My new union tests pass" (but Core/Schema tests fail)
- ❌ Only running CodeGen tests
- ❌ Any test failures anywhere in the solution

**REQUIRED:**
- ✅ **ALL 110+ tests must pass** (Core + Schema + CodeGen combined)
- ✅ Zero regression in existing tests
- ✅ No build warnings introduced

**Report must include:** Full `dotnet test` output showing all tests passing.

### Required Reading (IN ORDER)

1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Previous Reviews:**
   - `.dev-workstream/reviews/BATCH-08-REVIEW.md` - Deserializer pattern
   - `.dev-workstream/reviews/BATCH-06-REVIEW.md` - Serializer pattern
3. **Task Master:** `docs/SERDATA-TASK-MASTER.md` - **READ FCDC-S013 CAREFULLY**
4. **Design Document:** `docs/SERDATA-DESIGN.md` - Section 4.5 (Unions)
5. **Schema Package:** `Src/CycloneDDS.Schema/Attributes/DdsUnionAttribute.cs` - Union attribute usage

### Source Code Location

- **CLI Tool:** `tools/CycloneDDS.CodeGen/` (extend `SerializerEmitter.cs` and `DeserializerEmitter.cs`)
- **Test Project:** `tests/CycloneDDS.CodeGen.Tests/`

### Report Submission

**⚠️ CRITICAL: REPORT FOLDER LOCATION ⚠️**

**Submit to:** `.dev-workstream/reports/BATCH-09-REPORT.md`

---

## Context

**BATCH-08 Complete:** Serializer/deserializer for fixed and variable types, zero-copy views.

**This Batch:** Add discriminated union support.

**Related Tasks:**
- [FCDC-S013](../docs/SERDATA-TASK-MASTER.md#fcdc-s013-union-support)

**What Unions Are:**
```csharp
[DdsUnion]
public struct Shape
{
    [DdsDiscriminator]
    public ShapeKind Kind;
    
    [DdsCase(ShapeKind.Circle)]
    public double Radius;
    
    [DdsCase(ShapeKind.Rectangle)]
    public Rectangle Rect;
    
    [DdsDefaultCase]
    public int Other;
}
```

**XCDR2 Union Serialization:**
- Discriminator value
- Active case field value only
- Alignment handled per field type

---

## 🔄 MANDATORY WORKFLOW: Verify Then Implement

**CRITICAL: Must complete tasks in strict order**

1. **Task 0 (PREREQUISITE - BLOCKING):** Verify Cyclone DDS union DHEADER behavior via Golden Rig test
2. **Task 1:** Implement union serialization based on Task 0 findings
3. **Task 2:** Write union tests

**DO NOT** proceed to Task 1 until Task 0 complete and findings documented.

---

## 🎯 Batch Objectives

**Primary Goal:** Verify Cyclone's union format, then implement compatible union serialization/deserialization.

**Success Metrics:**
- Task 0: Golden Rig test confirms Cyclone's union wire format
- Generated code compiles
- Unions serialize **exactly** like Cyclone DDS C code
- Unions deserialize correctly (validates discriminator)
- **ALL 110+ tests passing** (no regressions)

---

## ✅ Task 0: Golden Rig Union Verification (PREREQUISITE - BLOCKING)

**Status:** ⚠️ **BLOCKING** - must complete before Tasks 1-2

**Goal:** Determine if Cyclone DDS writes DHEADER for @appendable unions.

**Why This Matters:**
- Design-talk.md (lines 3840-4147) discusses ambiguity in XCDR2 spec interpretation
- Some implementations may optimize away Union DHEADER
- We MUST match Cyclone's actual behavior for C/C# interop
- Cannot implement serializer correctly without knowing wire format

### Investigation Steps

1. **Create IDL file with @appendable union:**
   ```idl
   // Save as: tests/GoldenRigUnion.idl
   @appendable
   union MyUnion switch(long) {
       case 1: long valueA;
       case 2: double valueB;
   };
   
   @appendable
   struct Container {
       MyUnion u;
   };
   ```

2. **Generate C code:**
   ```bash
   cd tests
   idlc -l GoldenRigUnion.idl
   ```

3. **Write C test program:**
   ```c
   // tests/union_test.c
   #include "GoldenRigUnion.h"
   #include <stdio.h>
   #include <string.h>
   
   void print_hex(const unsigned char* data, size_t len) {
       for (size_t i = 0; i < len; i++) {
           printf("%02X ", data[i]);
           if ((i + 1) % 16 == 0) printf("\n");
       }
       printf("\n");
   }
   
   int main() {
       Container c;
       unsigned char buffer[256];
       
       // Test Case 1: Union with case 1 (long)
       c.u._d = 1;
       c.u._u.valueA = 0x12345678;
       
       // Serialize using Cyclone's generated code
       dds_ostream_t os;
       dds_ostream_init(&os, buffer, sizeof(buffer), 0);
       Container_ser(&c, &os);
       size_t size = dds_ostream_size(&os);
       
       printf("=== Cyclone DDS Union Serialization ===\n");
       printf("Size: %zu bytes\n", size);
       printf("Hex dump:\n");
       print_hex(buffer, size);
       
       // Analyze structure
       printf("\n=== Structure Analysis ===\n");
       printf("Expected if NO Union DHEADER:\n");
       printf("  [Container DHEADER: 4] [Disc: 4] [ValueA: 4] = 12 bytes\n");
       printf("  Container DHEADER would be: 08 00 00 00 (8 bytes body)\n");
       printf("\n");
       printf("Expected if HAS Union DHEADER:\n");
       printf("  [Container DHEADER: 4] [Union DHEADER: 4] [Disc: 4] [ValueA: 4] = 16 bytes\n");
       printf("  Container DHEADER would be: 0C 00 00 00 (12 bytes body)\n");
       printf("  Union DHEADER would be: 08 00 00 00 (8 bytes body)\n");
       
       return 0;
   }
   ```

4. **Compile and run:**
   ```bash
   gcc -I/path/to/cyclonedds/include -L/path/to/cyclonedds/lib \
       union_test.c GoldenRigUnion.c -lddsc -o union_test
   ./union_test
   ```

5. **Analyze output:**
   - **If size is 12 bytes:** Union has NO DHEADER
   - **If size is 16 bytes:** Union HAS DHEADER
   - Record exact hex dump in report

### Expected Outcomes

**Scenario A: No Union DHEADER (Size = 12)**
```
Hex: 08 00 00 00 01 00 00 00 78 56 34 12
     ^Container    ^Disc       ^ValueA
      DHEADER=8
```
**Action:** Implement unions WITHOUT DHEADER (discriminator + value only)

**Scenario B: Union Has DHEADER (Size = 16)**
```
Hex: 0C 00 00 00 08 00 00 00 01 00 00 00 78 56 34 12
     ^Container    ^Union      ^Disc       ^ValueA
      DHEADER=12    DHEADER=8
```
**Action:** Implement unions WITH DHEADER (as currently specified in BATCH-09)

### Deliverables

- IDL file: `tests/GoldenRigUnion.idl`
- C test program: `tests/union_test.c`
- Output hex dump (screenshot or text file)
- **Report section documenting findings:**
  - Exact size in bytes
  - Hex dump
  - Interpretation (DHEADER present or not)
  - **Decision for Task 1 implementation**

### Success Criteria

- ✅ C test program compiles and runs
- ✅ Output clearly shows union wire format
- ✅ Findings documented in report
- ✅ **Decision made:** Implement with or without DHEADER

**Estimated Time:** 1-2 hours

---

## ✅ Task 1: Union Code Generation (FCDC-S013)

**DEPENDENCY:** Task 0 must be complete with findings documented

**Files:** Modify `SerializerEmitter.cs` and `DeserializerEmitter.cs`  
**Task Definition:** See [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md#fcdc-s013-union-support)

### Union Detection

```csharp
private bool IsUnion(TypeInfo type)
{
    return type.Attributes.Any(a => a.Name == "DdsUnion");
}

private FieldInfo GetDiscriminator(TypeInfo type)
{
    return type.Fields.First(f => f.Attributes.Any(a => a.Name == "DdsDiscriminator"));
}

private FieldInfo GetDefaultCase(TypeInfo type)
{
    return type.Fields.FirstOrDefault(f => f.Attributes.Any(a => a.Name == "DdsDefaultCase"));
}

private IEnumerable<(FieldInfo field, object caseValue)> GetCaseArms(TypeInfo type)
{
    foreach (var field in type.Fields)
    {
        var caseAttr = field.Attributes.FirstOrDefault(a => a.Name == "DdsCase");
        if (caseAttr != null)
        {
            yield return (field, caseAttr.ConstructorArgs[0]);
        }
    }
}
```

### Generated Serialization Code

**NOTE:** The code below assumes Task 0 found that Cyclone DOES write Union DHEADER.
**If Task 0 finds NO DHEADER, you must adjust the implementation accordingly.**

**CRITICAL:** Per XCDR2 specification and design-talk.md (lines 3840-4147), **ALL unions SHOULD be @appendable with DHEADER**.

**However:** We must match Cyclone's actual behavior. Task 0 verification determines final implementation.

**Why DHEADER is Required:**
- Allows adding new union arms without breaking old readers
- Old readers use DHEADER to skip unknown discriminator values
- Maintains stream synchronization even with unknown cases

**For union type:**

```csharp
public void Serialize(ref CdrWriter writer)
{
    // DHEADER (required for @appendable unions)
    int dheaderPos = writer.Position;
    writer.WriteUInt32(0); // Placeholder
    
    int bodyStart = writer.Position;
    
    // Write discriminator
    writer.WriteInt32((int)this.Kind);
    
    // Switch on discriminator, write active case
    switch (this.Kind)
    {
        case ShapeKind.Circle:
            writer.WriteDouble(this.Radius);
            break;
        case ShapeKind.Rectangle:
            this.Rect.Serialize(ref writer);
            break;
        default:
            writer.WriteInt32(this.Other);
            break;
    }
    
    // Patch DHEADER with body size
    int bodySize = writer.Position - bodyStart;
    writer.PatchUInt32(dheaderPos, (uint)bodySize);
}

public int GetSerializedSize(int currentOffset)
{
    var sizer = new CdrSizer(currentOffset);
    
    // DHEADER (required for @appendable)
    sizer.WriteUInt32(0);
    
    // Discriminator
    sizer.WriteInt32(0);
    
    // Active case
    switch (this.Kind)
    {
        case ShapeKind.Circle:
            sizer.WriteDouble(0);
            break;
        case ShapeKind.Rectangle:
            sizer.Skip(this.Rect.GetSerializedSize(sizer.Position));
            break;
        default:
            sizer.WriteInt32(0);
            break;
    }
    
    return sizer.GetSizeDelta(currentOffset);
}
```

**Wire Format:**
```
[DHEADER: 4 bytes] [Discriminator: 4 bytes] [Active Member Data...]
```

**Example for Circle (Radius=5.0):**
```
[0C 00 00 00] [00 00 00 00] [00 00 00 00 00 00 14 40]
 ^DHEADER=12   ^Disc=0       ^Radius=5.0
```

### Generated Deserialization Code

**For union view (with DHEADER and unknown discriminator handling):**

```csharp
public readonly ref struct ShapeView
{
    private readonly ReadOnlySpan<byte> _buffer;
    private readonly int _offset;
    
    internal ShapeView(ReadOnlySpan<byte> buffer, int _offset)
    {
        _buffer = buffer;
        _offset = offset;
    }
    
    // DHEADER (for skipping unknown discriminators)
    private uint DHeader => BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Slice(_offset, 4));
    
    // Discriminator (after DHEADER)
    public ShapeKind Kind => (ShapeKind)BinaryPrimitives.ReadInt32LittleEndian(_buffer.Slice(_offset + 4, 4));
    
    // Case properties (check discriminator)
    public double Radius
    {
        get
        {
            if (Kind != ShapeKind.Circle)
                throw new InvalidOperationException($"Union discriminator is {Kind}, not Circle");
            return BinaryPrimitives.ReadDoubleLittleEndian(_buffer.Slice(_offset + 8, 8));
        }
    }
    
    public RectangleView Rect
    {
        get
        {
            if (Kind != ShapeKind.Rectangle)
                throw new InvalidOperationException($"Union discriminator is {Kind}, not Rectangle");
            return new RectangleView(_buffer, _offset + 8);
        }
    }
    
    // ToOwned creates heap copy based on discriminator
    public Shape ToOwned()
    {
        switch (Kind)
        {
            case ShapeKind.Circle:
                return new Shape { Kind = Kind, Radius = Radius };
            case ShapeKind.Rectangle:
                return new Shape { Kind = Kind, Rect = Rect.ToOwned() };
            default:
                // Unknown discriminator: Return with discriminator only
                // Actual member data cannot be safely read
                return new Shape { Kind = Kind };
        }
    }
}

// Deserialize method (with DHEADER skip for unknown arms)
public partial struct Shape
{
    public static ShapeView Deserialize(ref CdrReader reader)
    {
        // Read DHEADER
        uint dheaderSize = reader.ReadUInt32();
        int endPos = reader.Position + (int)dheaderSize;
        
        // Read discriminator
        var discriminator = (ShapeKind)reader.ReadInt32();
        
        // Handle known/unknown cases
        switch (discriminator)
        {
            case ShapeKind.Circle:
                // Member will be read by view on-demand
                break;
            case ShapeKind.Rectangle:
                // Member will be read by view on-demand
                break;
            default:
                // Unknown discriminator: skip to end using DHEADER
                reader.Seek(endPos);
                break;
        }
        
        // Return view (handles actual member access)
        return new ShapeView(reader.Buffer, reader.Position - (int)dheaderSize - 4);
    }
}
```

**Key Points:**
- **DHEADER read first** to calculate end position
- **Unknown discriminators** are handled by seeking to end position
- **Stream synchronization maintained** even with unknown union arms
- **View properties** validate discriminator before accessing members

### Implementation Changes

**Modify `EmitSerializer` method:**

```csharp
public string EmitSerializer(TypeInfo type)
{
    if (IsUnion(type))
    {
        return EmitUnionSerializer(type);
    }
    else
    {
        return EmitStructSerializer(type); // Existing logic
    }
}

private string EmitUnionSerializer(TypeInfo type)
{
    var sb = new StringBuilder();
    
    // Using directives...
    // Namespace...
    
    sb.AppendLine($"    public partial struct {type.Name}");
    sb.AppendLine("    {");
    
    EmitUnionGetSerializedSize(sb, type);
    EmitUnionSerialize(sb, type);
    
    sb.AppendLine("    }");
    
    // Close namespace...
    
    return sb.ToString();
}

private void EmitUnionSerialize(StringBuilder sb, TypeInfo type)
{
    var discriminator = GetDiscriminator(type);
    var cases = GetCaseArms(type).ToList();
    var defaultCase = GetDefaultCase(type);
    
    sb.AppendLine("        public void Serialize(ref CdrWriter writer)");
    sb.AppendLine("        {");
    sb.AppendLine("            // DHEADER (required for @appendable unions)");
    sb.AppendLine("            int dheaderPos = writer.Position;");
    sb.AppendLine("            writer.WriteUInt32(0);");
    sb.AppendLine();
    sb.AppendLine("            int bodyStart = writer.Position;");
    sb.AppendLine();
    sb.AppendLine($"            // Discriminator");
    sb.AppendLine($"            writer.Write{GetWriterMethod(discriminator.TypeName)}(({discriminator.TypeName})this.{ToPascalCase(discriminator.Name)});");
    sb.AppendLine();
    sb.AppendLine($"            // Active case");
    sb.AppendLine($"            switch (this.{ToPascalCase(discriminator.Name)})");
    sb.AppendLine("            {");
    
    foreach (var (field, caseValue) in cases)
    {
        sb.AppendLine($"                case ({discriminator.TypeName}){caseValue}:");
        string writerCall = GetWriterCall(field);
        sb.AppendLine($"                    {writerCall};");
        sb.AppendLine("                    break;");
    }
    
    if (defaultCase != null)
    {
        sb.AppendLine("                default:");
        string writerCall = GetWriterCall(defaultCase);
        sb.AppendLine($"                    {writerCall};");
        sb.AppendLine("                    break;");
    }
    
    sb.AppendLine("            }");
    sb.AppendLine();
    sb.AppendLine("            // Patch DHEADER");
    sb.AppendLine("            int bodySize = writer.Position - bodyStart;");
    sb.AppendLine("            writer.PatchUInt32(dheaderPos, (uint)bodySize);");
    sb.AppendLine("        }");
}
```

### Deliverables

- Modify `tools/CycloneDDS.CodeGen/SerializerEmitter.cs`
- Modify `tools/CycloneDDS.CodeGen/DeserializerEmitter.cs`
- Add union detection and case extraction helpers

### Tests Required

**Minimum 8-10 tests:**

#### Code Generation Tests (4-5 tests)
1. ✅ Detects union types correctly
2. ✅ Generates discriminator write
3. ✅ Generates switch statement with case arms
4. ✅ Generates default case if present
5. ✅ Union view properties validate discriminator

#### Execution Tests (4-5 tests - CRITICAL)
6. ✅ **Union serializes correctly** (discriminator + active case)
7. ✅ **Union deserializes correctly** (roundtrip)
8. ✅ **Different case arms serialize correctly**
9. ✅ **View throws on wrong discriminator access**
10. ✅ **ToOwned() creates correct union based on discriminator**

#### Regression Tests (CRITICAL)
11. ✅ **ALL 110+ existing tests still pass**

**Quality Standard:**

**✅ REQUIRED:**
- Tests MUST compile generated code (Roslyn)
- Tests MUST verify roundtrip for multiple case arms
- Tests MUST verify view discriminator validation
- **Tests MUST NOT break any existing tests**

**Example GOOD Test:**

```csharp
[Fact]
public void GeneratedUnion_Serializes_Correctly()
{
    var type = new TypeInfo
    {
        Name = "Shape",
        Attributes = new[] { new AttributeInfo { Name = "DdsUnion" } },
        Fields = new[]
        {
            new FieldInfo 
            { 
                Name = "Kind", 
                TypeName = "ShapeKind",
                Attributes = new[] { new AttributeInfo { Name = "DdsDiscriminator" } }
            },
            new FieldInfo 
            { 
                Name = "Radius", 
                TypeName = "double",
                Attributes = new[] { new AttributeInfo { Name = "DdsCase", ConstructorArgs = new object[] { 0 } } }
            }
        }
    };
    
    // Generate & compile
    var assembly = CompileToAssembly(...);
    
    // Test Circle case
    var circle = Activator.CreateInstance(assembly.GetType("Shape"));
    circle.GetType().GetField("Kind").SetValue(circle, 0); // Circle
    circle.GetType().GetField("Radius").SetValue(circle, 5.0);
    
    // Serialize
    var writer = new ArrayBufferWriter<byte>();
    var cdr = new CdrWriter(writer);
    circle.GetType().GetMethod("Serialize").Invoke(circle, new object[] { cdr });
    cdr.Complete();
    
    // Deserialize
    var reader = new CdrReader(writer.WrittenSpan);
    var view = deserialized = Shape.Deserialize(ref reader);
    var owned = view.ToOwned();
    
    // Verify
    Assert.Equal(0, owned.Kind);
    Assert.Equal(5.0, owned.Radius);
}
```

**Estimated Time:** 10-12 hours

---

## 🧪 Testing Requirements

**Minimum Total Tests:** 8-10 new tests

**Test Distribution:**
- Code Generation: 4-5 tests
- Execution (roundtrip): 4-5 tests

**⚠️⚠️⚠️ CRITICAL: ALL TESTS MUST PASS ⚠️⚠️⚠️**

```bash
dotnet test   # Must show: total: 118-120; failed: 0; succeeded: 118-120
```

**Your report MUST include full test output.**

---

## 📊 Report Requirements

**Submit to:** `.dev-workstream/reports/BATCH-09-REPORT.md`

**Required Sections:**

1. **Task 0: Golden Rig Union Verification (CRITICAL)**
   - **MUST INCLUDE:** Full hex dump from C test program
   - **MUST INCLUDE:** Size in bytes
   - **MUST INCLUDE:** Analysis (DHEADER present or not?)
   - **MUST INCLUDE:** Screenshot or text output of `union_test`
   - **DECISION:** Implement with or without DHEADER based on findings

2. **Implementation Summary**
2. **Implementation Summary**
   - Union detection logic
   - Switch generation approach
   - Case vs default handling
   - **How implementation matches Task 0 findings**

3. **Test Results**
3. **Test Results**
   - **MUST INCLUDE:** Full `dotnet test` output
   - **MUST SHOW:** All 110+ tests passing (no regressions)
   - New test count

4. **Issues Encountered**
   - Discriminator type handling?
   - Nested unions?

5. **Next Steps**
   - What's needed for optionals (FCDC-S014)?

---

## 🎯 Success Criteria

This batch is DONE when:

- ✅ **Task 0:** Golden Rig union verification complete, findings documented
- ✅ **FCDC-S013** Complete: Union support (matching Cyclone's format)
- ✅ 8-10 new tests passing
- ✅ Generated union code compiles
- ✅ Unions roundtrip correctly
- ✅ **Unions serialize EXACTLY like Cyclone DDS C code**
- ✅ **ALL 110+ tests passing** (ZERO regressions)
- ✅ Report includes Task 0 findings + full test output
- ✅ Report submitted

**BLOCKING:** Task 0 must be complete before Task 1. Any test regression blocks approval.

---

## ⚠️ Common Pitfalls to Avoid

1. **Breaking existing tests:**
   - MUST run full `dotnet test` before submitting
   - Any regression blocks approval

2. **Not handling default case:**
   - Default case is optional but must work if present

3. **Forgetting discriminator validation in views:**
   - View properties must throw if discriminator doesn't match

4. **Not writing DHEADER for unions:**
   - **CORRECT:** Unions ARE @appendable and MUST have DHEADER
   - Enables adding new arms without breaking old readers
   - Old readers use DHEADER to skip unknown discriminators

5. **Not testing multiple case arms:**
   - Must test different discriminator values

6. **Only running new tests:**
   - MUST run ALL tests (dotnet test at solution level)

---

## 📚 Reference Materials

- **Task Master:** [SERDATA-TASK-MASTER.md §FCDC-S013](../docs/SERDATA-TASK-MASTER.md)
- **Union Attribute:** `Src/CycloneDDS.Schema/Attributes/DdsUnionAttribute.cs`
- **BATCH-08 Review:** `.dev-workstream/reviews/BATCH-08-REVIEW.md` - Deserializer pattern

---

**Next Batch:** BATCH-10 (Optional Members Support) - `[DdsOptional]` fields
