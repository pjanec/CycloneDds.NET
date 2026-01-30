# BATCH-12.1: Managed Types Polish + Type System Extensibility

**Batch Number:** BATCH-12.1 (Verification + Extension)  
**Parent:** BATCH-12  
**Tasks:** Edge case testing, validator, custom type support design  
**Phase:** Stage 2 - Code Generation (Polish + Design)  
**Estimated Effort:** 2-3 days  
**Priority:** MEDIUM  
**Dependencies:** BATCH-12 (Managed Types Core)

---

## 📋 Context

BATCH-12 delivered core managed types support (string, List<T>) with high-quality tests. This batch:
1. **Verifies edge cases** (null, empty, large, complex, mixed)
2. **Adds validator** to enforce `[DdsManaged]` attribute
3. **Documents extensibility** for future type additions (Quaternion, Guid, DateTime, etc.)

---

## Current Type Support Analysis

### ✅ Currently Supported Types:

**Primitives (TypeMapper.cs):**
- `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`
- `float`, `double`, `bool`

**Wrapper Types (Schema package):**
- `FixedString32`, `FixedString64`, `FixedString128` - Fixed-size zero-copy strings
- `BoundedSeq<T>` - Bounded sequence (uses List<T> internally, struct wrapper)

**Managed Types (BATCH-12):**
- `string` - Standard C# string (GC-allocated)
- `List<T>` - Standard C# List (GC-allocated)

**Complex Types:**
- Structs (user-defined) - Nested ser ialization via `.Serialize(ref writer)`
- Unions - `[DdsUnion]` with discriminator + cases
- Optional fields - `T?` for value types, uses EMHEADER

### ❌ NOT Currently Supported:

- `Guid` - UUID/GUID type
- `DateTime`, `DateTimeOffset` - Temporal types
- `System.Numerics.Quaternion`, `Vector3`, `Vector4` - 3D math types
- `T[]` - Standard C# arrays
- `Span<T>`, `Memory<T>` - Span-based types
- Custom types via attributes/mappers (extensibility mechanism)

---

## 🎯 Batch Objectives

###Part A: Edge Case Verification (6 tests)
1. Null string handling
2. Empty list handling
3. Large list performance (10,000 elements)
4. Complex types in lists (List<MyStruct>)
5. String lists (List<string>)
6. Mixed managed/unmanaged fields

### Part B: Validator Implementation (1 file + 1 test)
7. ManagedTypeValidator enforces `[DdsManaged]` attribute
8. Test: Unmarked managed type generates diagnostic error

### Part C: Extensibility Design (Documentation)
9. Document type extension pattern for future custom types
10. Design proposal for Guid, DateTime, Quaternion support

---

## ✅ Task 1: Null String Handling Test

**Duration:** 30 minutes

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.CodeGen.Tests\ManagedTypesTests.cs`

**Add this test:**

```csharp
[Fact]
public void ManagedString_Null_RoundTrip()
{
    var type = new TypeInfo
    {
        Name = "NullableStringStruct",
        Namespace = "TestManaged",
        Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
        Fields = new List<FieldInfo>
        {
            new FieldInfo { Name = "Id", TypeName = "int" },
            new FieldInfo { Name = "Text", TypeName = "string", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } }
        }
    };
    
    var emitter = new SerializerEmitter();
    string code = @"
using CycloneDDS.Schema;
using System.Collections.Generic;

namespace TestManaged
{
    [DdsManaged]
    public partial struct NullableStringStruct
    {
        public int Id;
        [DdsManaged]
        public string Text;
    }

    public static class TestHelper
    {
        public static void Serialize(object instance, IBuffer Writer<byte> buffer)
        {
            var typed = (NullableStringStruct)instance;
            var writer = new CycloneDDS.Core.CdrWriter(buffer);
            typed.Serialize(ref writer);
            writer.Complete();
        }

        public static object Deserialize(ReadOnlyMemory<byte> buffer)
        {
            var reader = new CycloneDDS.Core.CdrReader(buffer.Span);
            var view = NullableStringStruct.Deserialize(ref reader);
            return view.ToOwned();
        }
    }
}
";
    code += emitter.EmitSerializer(type, false) + "\n";
    code += new DeserializerEmitter().EmitDeserializer(type, false);

    var assembly = CompileToAssembly(code, "NullStringTest");
    
    var instance = Instantiate(assembly, "TestManaged.NullableStringStruct");
    SetField(instance, "Id", 42);
    SetField(instance, "Text", null);  // NULL STRING
    
    var buffer = new ArrayBufferWriter<byte>();
    var helperType = assembly.GetType("TestManaged.TestHelper");
    helperType.GetMethod("Serialize").Invoke(null, new object[] { instance, buffer });
    
    var result = helperType.GetMethod("Deserialize").Invoke(null, new object[] { buffer.WrittenMemory });
    
    Assert.Equal(42, GetField(result, "Id"));
    
    // Document actual behavior:
    // Option 1: null becomes empty string ""
    // Option 2: null throws exception
    // Option 3: null survives as null
    var text = GetField(result, "Text");
    
    // CHOOSE ONE - document your decision in report:
    // Assert.Equal("", text);  // null → empty string
    // Assert.Throws<NullReferenceException>(...);  // null throws
    Assert.Null(text);  // null survives (preferred for consistency)
}
```

**✅ CHECKPOINT:** Test passes, null behavior documented in report.

---

## ✅ Task 2: Empty List Handling Test

**Duration:** 20 minutes

**Same file, add:**

```csharp
[Fact]
public void ManagedList_Empty_RoundTrip()
{
    var type = new TypeInfo
    {
        Name = "EmptyListStruct",
        Namespace = "TestManaged",
        Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
        Fields = new List<FieldInfo>
        {
            new FieldInfo { Name = "Items", TypeName = "List<int>", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } }
        }
    };
    
    // ... similar pattern ...
    
    var instance = Instantiate(assembly, "TestManaged.EmptyListStruct");
    var emptyList = new List<int>();  // Count = 0
    SetField(instance, "Items", emptyList);
    
    // ... serialize/deserialize ...
    
    var result = helperType.GetMethod("Deserialize").Invoke(null, new object[] { buffer.WrittenMemory });
    var resultItems = (List<int>)GetField(result, "Items");
    
    Assert.NotNull(resultItems);
    Assert.Empty(resultItems);
}
```

**✅ CHECKPOINT:** Empty list (Count=0) round trips correctly.

---

## ✅ Task 3: Large List Performance Test

**Duration:** 30 minutes

**Add:**

```csharp
[Fact]
public void ManagedList_Large_PerformanceTest()
{
    var type = new TypeInfo
    {
        Name = "LargeListStruct",
        Namespace = "TestManaged",
        Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
        Fields = new List<FieldInfo>
        {
            new FieldInfo { Name = "Data", TypeName = "List<int>", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } }
        }
    };
    
    // ... compile ...
    
    var instance = Instantiate(assembly, "TestManaged.LargeListStruct");
    var largeList = new List<int>(10000);
    for (int i = 0; i < 10000; i++) largeList.Add(i);
    SetField(instance, "Data", largeList);
    
    var sw = System.Diagnostics.Stopwatch.StartNew();
    
    // ... serialize/deserialize ...
    
    sw.Stop();
    
    var resultData = (List<int>)GetField(result, "Data");
    Assert.Equal(10000, resultData.Count);
    Assert.Equal(0, resultData[0]);
    Assert.Equal(9999, resultData[9999]);
    
    // Document performance in report
    _output.WriteLine($"Serialization + Deserialization of 10,000 ints: {sw.ElapsedMilliseconds}ms");
    
    // Sanity check: should be < 100ms
    Assert.True(sw.ElapsedMilliseconds < 100, "Performance regression: large list too slow");
}
```

**✅ CHECKPOINT:** 10,000 element list serializes in < 100ms.

---

## ✅ Task 4: List<ComplexStruct> Test

**Duration:** 40 minutes

**Add:**

```csharp
[Fact]
public void ManagedList_ComplexType_RoundTrip()
{
    var innerType = new TypeInfo
    {
        Name = "InnerStruct",
        Namespace = "TestManaged",
        Fields = new List<FieldInfo>
        {
            new FieldInfo { Name = "X", TypeName = "int" },
            new FieldInfo { Name = "Y", TypeName = "double" }
        }
    };
    
    var outerType = new TypeInfo
    {
        Name = "ComplexListStruct",
        Namespace = "TestManaged",
        Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
        Fields = new List<FieldInfo>
        {
            new FieldInfo { Name = "Structs", TypeName = "List<InnerStruct>", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } }
        }
    };
    
    var emitter = new SerializerEmitter();
    string code = @"
using CycloneDDS.Schema;
using System.Collections.Generic;
using CycloneDDS.Core;
using System.Buffers;

namespace TestManaged
{
    public partial struct InnerStruct
    {
        public int X;
        public double Y;
    }

    [DdsManaged]
    public partial struct ComplexListStruct
    {
        [DdsManaged]
        public List<InnerStruct> Structs;
    }

    public static class TestHelper
    {
        public static void Serialize(object instance, IBufferWriter<byte> buffer)
        {
            var typed = (ComplexListStruct)instance;
            var writer = new CdrWriter(buffer);
            typed.Serialize(ref writer);
            writer.Complete();
        }

        public static object Deserialize(ReadOnlyMemory<byte> buffer)
        {
            var reader = new CdrReader(buffer.Span);
            var view = ComplexListStruct.Deserialize(ref reader);
            return view.ToOwned();
        }
    }
}
";
    code += emitter.EmitSerializer(innerType, false) + "\n";
    code += emitter.EmitSerializer(outerType, false) + "\n";
    code += new DeserializerEmitter().EmitDeserializer(innerType, false) + "\n";
    code += new Deserializer Emitter().EmitDeserializer(outerType, false);

    var assembly = CompileToAssembly(code, "ComplexListTest");
    
    var instance = Instantiate(assembly, "TestManaged.ComplexListStruct");
    var innerStructType = assembly.GetType("TestManaged.InnerStruct");
    
    var list = new List<object>();
    for (int i = 0; i < 3; i++)
    {
        var inner = Activator.CreateInstance(innerStructType);
        SetField(inner, "X", i * 10);
        SetField(inner, "Y", i * 1.5);
        list.Add(inner);
    }
    
    // Convert List<object> to List<InnerStruct> via reflection
    var genericListType = typeof(List<>).MakeGenericType(innerStructType);
    var typedList = Activator.CreateInstance(genericListType);
    var addMethod = genericListType.GetMethod("Add");
    foreach (var item in list) addMethod.Invoke(typedList, new[] { item });
    
    SetField(instance, "Structs", typedList);
    
    // ... serialize/deserialize ...
    
    var resultList = (IEnumerable)GetField(result, "Structs");
    var resultArray = resultList.Cast<object>().ToArray();
    
    Assert.Equal(3, resultArray.Length);
    Assert.Equal(0, GetField(resultArray[0], "X"));
    Assert.Equal(0.0, GetField(resultArray[0], "Y"));
    Assert.Equal(20, GetField(resultArray[2], "X"));
    Assert.Equal(3.0, GetField(resultArray[2], "Y"));
}
```

**✅ CHECKPOINT:** List<ComplexType> works correctly.

---

## ✅ Task 5: List<string> Test

**Duration:** 25 minutes

**Add (simpler):**

```csharp
[Fact]
public void ManagedList_Strings_RoundTrip()
{
    var type = new TypeInfo
    {
        Name = "StringListStruct",
        Namespace = "TestManaged",
        Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
        Fields = new List<FieldInfo>
        {
            new FieldInfo { Name = "Messages", TypeName = "List<string>", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } }
        }
    };
    
    // ... compile...
    
    var instance = Instantiate(assembly, "TestManaged.StringListStruct");
    var strings = new List<string> { "Alpha", "Beta", "Gamma", "Delta" };
    SetField(instance, "Messages", strings);
    
    // ... serialize/deserialize ...
    
    var resultMessages = (List<string>)GetField(result, "Messages");
    Assert.Equal(4, resultMessages.Count);
    Assert.Equal("Alpha", resultMessages[0]);
    Assert.Equal("Delta", resultMessages[3]);
}
```

**✅ CHECKPOINT:** List<string> works.

---

## ✅ Task 6: Mixed Managed/Unmanaged Test

**Duration:** 30 minutes

**Add:**

```csharp
[Fact]
public void Mixed ManagedUnmanaged_RoundTrip()
{
    var type = new TypeInfo
    {
        Name = "MixedStruct",
        Namespace = "TestManaged",
        Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
        Fields = new List<FieldInfo>
        {
            new FieldInfo { Name = "Id", TypeName = "int" },
            new FieldInfo { Name = "Name", TypeName = "string", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } },
            new FieldInfo { Name = "Numbers", TypeName = "BoundedSeq<int>" },  // UNMANAGED
            new FieldInfo { Name = "Tags", TypeName = "List<string>", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } }  // MANAGED
        }
    };
    
    // ... code with both BoundedSeq and List ...
    
    var instance = Instantiate(assembly, "TestManaged.MixedStruct");
    SetField(instance, "Id", 999);
    SetField(instance, "Name", "Mixed");
    
    var bounded = new BoundedSeq<int>(5);
    bounded.Add(1); bounded.Add(2); bounded.Add(3);
    SetField(instance, "Numbers", bounded);
    
    var tags = new List<string> { "test", "managed" };
    SetField(instance, "Tags", tags);
    
    // ... serialize/deserialize ...
    
    Assert.Equal(999, GetField(result, "Id"));
    Assert.Equal("Mixed", GetField(result, "Name"));
    
    var resultNumbers = (BoundedSeq<int>)GetField(result, "Numbers");
    Assert.Equal(3, resultNumbers.Count);
    
    var resultTags = (List<string>)GetField(result, "Tags");
    Assert.Equal(2, resultTags.Count);
}
```

**✅ CHECKPOINT:** Mixed managed/unmanaged fields work.

---

## ✅ Task 7: Implement Managed TypeValidator

**Duration:** 1-2 hours

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\ManagedTypeValidator.cs` (NEW FILE)

**Content:**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace CycloneDDS.CodeGen
{
    /// <summary>
    /// Validates that types using managed fields (string, List&lt;T&gt;) are marked with [DdsManaged].
    /// </summary>
    public class ManagedTypeValidator
    {
        public List<Diagnostic> Validate(TypeInfo type)
        {
            var diagnostics = new List<Diagnostic>();
            
            if (type == null) return diagnostics;
            
            // Check each field for managed types
            foreach (var field in type.Fields ?? Enumerable.Empty<FieldInfo>())
            {
                if (IsManagedFieldType(field.TypeName))
                {
                    // Field uses managed type - type MUST have [DdsManaged]
                    if (!HasDdsManagedAttribute(type) && !HasDdsManagedAttribute(field))
                    {
                        diagnostics.Add(new Diagnostic
                        {
                            Severity = DiagnosticSeverity.Error,
                            Message = $"Type '{type.FullName ?? type.Name}' has field '{field.Name}' " +
                                      $"of managed type '{field.TypeName}' but is not marked with [DdsManaged]. " +
                                      $"Add [DdsManaged] attribute to type or field to acknowledge GC allocations."
                        });
                    }
                }
            }
            
            return diagnostics;
        }
        
        private bool IsManagedFieldType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return false;
            
            return typeName == "string" ||
                   typeName.StartsWith("List<") ||
                   typeName.StartsWith("System.Collections.Generic.List<");
        }
        
        private bool HasDdsManagedAttribute(TypeInfo type)
        {
            return type.Attributes?.Any(a => a.Name == "DdsManaged") ?? false;
        }
        
        private bool HasDdsManagedAttribute(FieldInfo field)
        {
            return field.Attributes?.Any(a => a.Name == "DdsManaged") ?? false;
        }
    }
}
```

**✅ CHECKPOINT:** File created, compiles.

---

## ✅ Task 8: Integrate Validator + Add Test

**Duration:** 1 hour

### Step 8.1: Integrate into CodeGenerator

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\CodeGenerator.cs`

**Find:** Where types are processed for code generation

**Add validation before emission:**

```csharp
// After parsing types but before generating code:

var managedValidator = new ManagedTypeValidator();
var allDiagnostics = new List<Diagnostic>();

foreach (var type in types)
{
    var validationErrors = managedValidator.Validate(type);
    allDiagnostics.AddRange(validationErrors);
}

// If errors exist, report and fail
if (allDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
{
    foreach (var diagnostic in allDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
    {
        Console.WriteLine($"ERROR: {diagnostic.Message}");
    }
    return false;  // Or throw, depending on CodeGenerator API
}
```

**✅ CHECKPOINT:** Validator integrated.

---

### Step 8.2: Add Validator Test

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.CodeGen.Tests\ManagedTypesTests.cs`

**Add:**

```csharp
[Fact]
public void UnmarkedManagedType_FailsValidation()
{
    var type = new TypeInfo
    {
        Name = "UnmarkedStruct",
        Namespace = "TestManaged",
        // NO [DdsManaged] attribute
        Fields = new List<FieldInfo>
        {
            new FieldInfo { Name = "Text", TypeName = "string" }  // Managed type, but no attribute
        }
    };
    
    var validator = new ManagedTypeValidator();
    var diagnostics = validator.Validate(type);
    
    Assert.NotEmpty(diagnostics);
    Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    Assert.Contains(diagnostics, d => d.Message.Contains("[DdsManaged]"));
    Assert.Contains(diagnostics, d => d.Message.Contains("Text"));
}
```

**✅ CHECKPOINT:** Validator test passes.

---

## ✅ Task 9: Document Type Extensibility

**Duration:** 1 hour

**File:** `d:\Work\FastCycloneDdsCsharpBindings\docs\TYPE-EXTENSION-GUIDE.md` (NEW FILE)

**Content:**

```markdown
# Type Extension Guide

## Overview

This document explains how to add support for custom types to the code generator.

## Current Type Support

See BATCH-12.1 instructions for full list.

## Adding a New Simple Type (e.g., Guid)

### Step 1: Add to TypeMapper

File: `tools/CycloneDDS.CodeGen/TypeMapper.cs`

```csharp
public static string GetWriterMethod(string typeName)
{
    return typeName switch
    {
        // ... existing types ...
        "Guid" or "System.Guid" => "WriteGuid",  // ADD THIS
        _ => null
    };
}
```

### Step 2: Add CdrWriter Support

File: `src/CycloneDDS.Core/CdrWriter.cs`

```csharp
public void WriteGuid(Guid value)
{
    // Guid is 16 bytes (128 bits)
    // RFC 4122 format: time_low(4) + time_mid(2) + time_hi_version(2) + 
    //                 clock_seq(2) + node(6) = 16 bytes
    
    Span<byte> bytes = stackalloc byte[16];
    value.TryWriteBytes(bytes);
    WriteBytes(bytes);  // Or write as 2 ulongs for alignment
}
```

### Step 3: Add CdrReader Support

File: `src/CycloneDDS.Core/CdrReader.cs`

```csharp
public Guid ReadGuid()
{
    Span<byte> bytes = stackalloc byte[16];
    ReadBytes(bytes);
    return new Guid(bytes);
}
```

### Step 4: Add Tests

File: `tests/CycloneDDS.Core.Tests/CdrWriterTests.cs`

```csharp
[Fact]
public void WriteGuid_RoundTrip()
{
    var guid = Guid.NewGuid();
    var buffer = new ArrayBufferWriter<byte>();
    var writer = new CdrWriter(buffer);
    
    writer.WriteGuid(guid);
    writer.Complete();
    
    var reader = new CdrReader(buffer.WrittenSpan);
    var result = reader.ReadGuid();
    
    Assert.Equal(guid, result);
}
```

## Adding a Complex Type (e.g., Quaternion)

### Step 1: Decide on Wire Format

Quaternion = 4 floats (X, Y, Z, W) = 16 bytes

Map to existing struct:
```csharp
// Quaternion serializes as struct { float X; float Y; float Z; float W; }
```

### Step 2: Add Type Mapper Entry

```csharp
// In SerializerEmitter.cs - EmitFieldWrite:

if (field.TypeName == "Quaternion" || field.TypeName.EndsWith(".Quaternion"))
{
    sb.AppendLine($"            writer.WriteFloat(value.{field.Name}.X);");
    sb.AppendLine($"            writer.WriteFloat(value.{field.Name}.Y);");
    sb.AppendLine($"            writer.WriteFloat(value.{field.Name}.Z);");
    sb.AppendLine($"            writer.WriteFloat(value.{field.Name}.W);");
    return;
}
```

### Step 3: Add Deserializer Support

```csharp
// In DeserializerEmitter.cs - EmitFieldRead:

if (field.TypeName == "Quaternion" || field.TypeName.EndsWith(".Quaternion"))
{
    sb.AppendLine($"            result.{field.Name} = new Quaternion(");
    sb.AppendLine($"                reader.ReadFloat(),  // X");
    sb.AppendLine($"                reader.ReadFloat(),  // Y");
    sb.AppendLine($"                reader.ReadFloat(),  // Z");
    sb.AppendLine($"                reader.ReadFloat()   // W");
    sb.AppendLine($"            );");
    return;
}
```

### Step 4: Add Tests

```csharp
[Fact]
public void Quaternion_RoundTrip()
{
    var type = new TypeInfo
    {
        Name = "QuaternionData",
        Namespace = "Math3D",
        Fields = new List<FieldInfo>
        {
            new FieldInfo { Name = "Rotation", TypeName = "System.Numerics.Quaternion" }
        }
    };
    
    // ... generate code, compile, test roundtrip ...
}
```

## Adding Array Support (T[])

Arrays require:
1. Decide: Fixed-size or dynamic-size
2. If dynamic: Serialize length + elements (like List<T>)
3. If fixed: Use attribute `[DdsArray(length)]`

See `BoundedSeq<T>` pattern for reference.

## Future Considerations

- **Custom Serializers:** Attribute-based custom serialization
- **Type Converters:** Auto-convert between wire format and C# type
- **Span<T> Support:** Zero-copy deserialization for value types

---

**For Questions:** Ask in `.dev-workstream/questions/`
```

**✅ CHECKPOINT:** Documentation complete.

---

## ✅ Task 10: Type System Analysis Report

**Duration:** 30 minutes

**In Your Report, include this section:**

### Type Extensibility Analysis

**Question:** Can we easily add Guid, DateTime, Quaternion, arrays?

**Answer:**

**✅ Easy to Add (Pattern Exists):**
- **Guid:** 16-byte primitive → Add to TypeMapper + CdrWriter/Reader (2 hours)
- **DateTime:** Map to `long` (ticks) or `ulong` (Unix milliseconds) (1 hour)
- **Quaternion/Vector3/Vector4:** Struct of floats → Special case in emitters (3 hours)

**⚠️ Moderate Effort:**
- **T[] Fixed Arrays:** Need `[DdsArray(N)]` attribute + emitter logic (1 day)
- **T[] Dynamic Arrays:** Similar to List<T> but different C# semantics (1 day)

**❌ Requires Architecture Changes:**
- **Custom Serializers:** Need attribute `[DdsSerializer(typeof(MySerializer))]` system
- **Span<T>/Memory<T>:** Requires view struct changes, not simple

**Recommendation for Future Batches:**
- BATCH-13.1 Add Guid + DateTime (high value, low cost)
- BATCH-13.2: Add Quaternion/Vector3/Vector4 (3D math types)
- BATCH-14: Fixed arrays `T[N]` with attribute
- BATCH-15: Custom serializer framework

---

## 📊 Success Criteria

This batch is DONE when:

### Code:
- ✅ ManagedTypeValidator implemented and integrated
- ✅ 6 edge case tests added
- ✅ All 162+ tests passing (156 + 6+ new)

### Documentation:
- ✅ TYPE-EXTENSION-GUIDE.md created
- ✅ Report includes type extensibility analysis
- ✅ Null/empty handling behaviors documented

### Report Sections (MANDATORY):
1. Executive Summary
2. Edge Case Test Results (with timing for large list)
3. Validator Implementation Notes
4. Type Extensibility Analysis (answer questions above)
5. Full `dotnet test` output
6. Design Decisions (null handling, validator trigger conditions)
7. Future Work Recommendations

---

## 📝 Report Template

**File:** `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reports\BATCH-12.1-REPORT.md`

```markdown
# BATCH-12.1 Report

## Executive Summary
- 6 edge case tests added
- ManagedTypeValidator implemented
- Type extensibility documented
- Total: 162 tests passing

## Edge Case Test Results

### Null String Handling:
**Behavior:** [null survives | null → empty | null throws]
**Rationale:** [why this choice]

### Empty List:
**Result:** ✅ Passes, Count=0 serializes/deserializes correctly

### Large List (10,000 elements):
**Performance:** [X]ms for serialize + deserialize
**Assessment:** [adequate | needs optimization]

### List<ComplexStruct>:
**Result:** [PASS/FAIL with details]

### List<string>:
**Result:** [PASS/FAIL]

### Mixed Managed/Unmanaged:
**Result:** [PASS/FAIL]

## Validator Implementation

**When it triggers:** [explain conditions]

**Error message:** [show actual error]

**Integration point:** [where in CodeGenerator]

## Type Extensibility Analysis

[Answer questions from Task 10]

**Guid:** [Easy/Moderate/Hard] - [rationale]  
**DateTime:** [Easy/Moderate/Hard] - [rationale]  
**Quaternion:** [Easy/Moderate/Hard] - [rationale]  
**T[]:** [Easy/Moderate/Hard] - [rationale]

## Full Test Output

\`\`\`
[paste dotnet test output]
\`\`\`

## Design Decisions

### Q1: How is null handled?
**A:** [your decision with rationale]

### Q2: What triggers validator error?
**A:** [exact conditions]

### Q3: Can we easily extend types?
**A:** [yes/no with analysis]

## Future Work Recommendations

1. [Most valuable type to add next]
2. [Suggested BATCH for arrays]
3. [Any refactoring needed]
```

---

## Common Pitfalls

1. **Forgetting to test null in List<string>** - List containing null elements
2. **Not timing large list test** - Performance matters
3. **Incomplete validator** - Must check field AND type attributes
4. **Not documenting extensibility** - Future developers need guide

---

**Estimated Time:** 2-3 days (16-24 hours)
- Edge tests: 3-4 hours
- Validator: 2-3 hours
- Documentation: 2 hours
- Report: 2 hours

**Next:** After approval, proceed to Stage 3 (Runtime Integration) with complete type system!
