# BATCH-04: IDL Code Emitter

**Batch Number:** BATCH-04  
**Tasks:** FCDC-007 (IDL Code Emitter)  
**Phase:** Phase 2 - Code Generator  
**Estimated Effort:** 5-7 days  
**Priority:** CRITICAL  
**Dependencies:** BATCH-03 (Schema Validation)

---

## 📋 Onboarding & Workflow

### Developer Instructions

This batch implements **IDL code generation** from validated C# schemas. You will generate standards-compliant OMG IDL 4.2 code that Cyclone DDS's `idlc` compiler can process.

The generated IDL will use **@appendable** for all types to enable backward-compatible schema evolution.

### Required Reading (IN ORDER)

1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Task Definition:** `docs/FCDC-TASK-MASTER.md` → See FCDC-007
3. **Design Document:** `docs/FCDC-DETAILED-DESIGN.md` → §4.2 Type Mapping Rules, §5.1 Phase 3
4. **Previous Batches:** BATCH-02.2 (CLI tool), BATCH-03 (Validation)
5. **IDL 4.2 Spec Reference:** https://www.omg.org/spec/IDL/4.2 (optional, for deep dive)

### Source Code Location

- **Primary Work Area:** `tools/CycloneDDS.CodeGen/`
- **Test Project:** `tests/CycloneDDS.CodeGen.Tests/`

### Report Submission

**When done, create:**  
`.dev-workstream/reports/BATCH-04-REPORT.md`

---

## 🎯 Objectives

Implement IDL code generation from validated C# schemas:

1. **Emit @appendable modules** - All types use @appendable extensibility
2. **Generate typedefs** - Map C# types to IDL (Guid → octet[16], etc.)
3. **Generate enums** - Emit IDL enums with correct underlying type
4. **Generate structs** - Topic types and nested types
5. **Generate unions** - With discriminator and cases
6. **Apply annotations** - @key for key fields, @optional for nullable types
7. **Type mapping** - Use design document type mapping rules

---

## ✅ Tasks

### Task 1: Implement IDL Type Mapper

**File:** `tools/CycloneDDS.CodeGen/Emitters/IdlTypeMapper.cs` (NEW)

Maps C# types to IDL types accordin to design document §4.2:

```csharp
namespace CycloneDDS.CodeGen.Emitters;

public static class IdlTypeMapper
{
    public static string MapToIdl(string csType)
    {
        return csType switch
        {
            // Primitives
            "byte" => "octet",
            "sbyte" => "int8",
            "short" => "int16",
            "ushort" => "uint16",
            "int" => "long",
            "uint" => "unsigned long",
            "long" => "long long",
            "ulong" => "unsigned long long",
            "float" => "float",
            "double" => "double",
            "bool" => "boolean",
            "char" => "wchar",  // Or char depending on encoding
            
            // String types
            "string" => "string",  // Unbounded
            
            // Fixed strings (wrapper types)
            "FixedString32" => "octet[32]",
            "FixedString64" => "octet[64]",
            "FixedString128" => "octet[128]",
            
            // Arrays handled separately (string[] -> sequence<string>)
            
            _ => MapCustomType(csType)
        };
    }
    
    private static string MapCustomType(string csType)
    {
        // Handle array types: int[] -> sequence<long>
        if (csType.EndsWith("[]"))
        {
            var elementType = csType[..^2];
            var idlElementType = MapToIdl(elementType);
            return $"sequence<{idlElementType}>";
        }
        
        // Handle nullable types: int? -> @optional long
        if (csType.EndsWith("?"))
        {
            var baseType = csType[..^1];
            return MapToIdl(baseType);  // @optional handled separately
        }
        
        // Handle generic types like BoundedSeq<T, N>
        if (csType.StartsWith("BoundedSeq<"))
        {
            // Extract T and N from BoundedSeq<T, N>
            // Return sequence<MapToIdl(T), N>
            // This is simplified - you'll need proper parsing
            return "sequence";  // TODO: Implement properly
        }
        
        // Assume it's a user-defined type or enum
        return csType;
    }
    
    public static bool IsNullableType(string csType)
    {
        return csType.EndsWith("?");
    }
    
    public static bool IsArrayType(string csType)
    {
        return csType.EndsWith("[]");
    }
    
    public static bool RequiresTypedef(string csType)
    {
        // Types that need typedef (Guid, DateTime, Quaternion, etc.)
        return csType switch
        {
            "Guid" => true,
            "DateTime" => true,
            "System.Guid" => true,
            "System.DateTime" => true,
            _ when csType.Contains("Quaternion") => true,
            _ => false
        };
    }
    
    public static string GetTypedefMapping(string csType)
    {
        return csType switch
        {
            "Guid" or "System.Guid" => "typedef octet Guid16[16];",
            "DateTime" or "System.DateTime" => "typedef long long Int64TicksUtc;",
            _ when csType.Contains("Quaternion") => "typedef QuaternionF32x4 { float x, y, z, w; };",
            _ => $"// Unknown typedef for {csType}"
        };
    }
}
```

---

### Task 2: Implement IDL Emitter

**File:** `tools/CycloneDDS.CodeGen/Emitters/IdlEmitter.cs` (NEW)

Generates IDL code from syntax trees:

```csharp
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CycloneDDS.CodeGen.Emitters;

public class IdlEmitter
{
    private readonly StringBuilder _sb = new();
    private int _indentLevel = 0;
    
    public string GenerateIdl(TypeDeclarationSyntax type, string topicName)
    {
        _sb.Clear();
        _indentLevel = 0;
        
        // IDL header
        EmitLine("// Auto-generated IDL from C# schema");
        EmitLine($"// Topic: {topicName}");
        EmitLine();
        
        // Module (namespace)
        var namespaceName = GetNamespace(type);
        EmitLine($"@appendable");
        EmitLine($"module {namespaceName} {{");
        _indentLevel++;
        
        // Emit typedefs if needed
        EmitTypedefs(type);
        
        // Emit the main struct
        EmitStruct(type);
        
        _indentLevel--;
        EmitLine("};");
        
        return _sb.ToString();
    }
    
    public string GenerateUnionIdl(TypeDeclarationSyntax type)
    {
        _sb.Clear();
        _indentLevel = 0;
        
        EmitLine("// Auto-generated Union IDL");
        EmitLine();
        
        var namespaceName = GetNamespace(type);
        EmitLine($"@appendable");
        EmitLine($"module {namespaceName} {{");
        _indentLevel++;
        
        EmitUnion(type);
        
        _indentLevel--;
        EmitLine("};");
        
        return _sb.ToString();
    }
    
    private void EmitTypedefs(TypeDeclarationSyntax type)
    {
        var fields = type.Members.OfType<FieldDeclarationSyntax>();
        var uniqueTypedefs = new HashSet<string>();
        
        foreach (var field in fields)
        {
            var fieldType = field.Declaration.Type.ToString();
            if (IdlTypeMapper.RequiresTypedef(fieldType) && uniqueTypedefs.Add(fieldType))
            {
                EmitLine(IdlTypeMapper.GetTypedefMapping(fieldType));
            }
        }
        
        if (uniqueTypedefs.Any())
            EmitLine();
    }
    
    private void EmitStruct(TypeDeclarationSyntax type)
    {
        EmitLine($"@appendable");
        EmitLine($"struct {type.Identifier.Text} {{");
        _indentLevel++;
        
        var fields = type.Members.OfType<FieldDeclarationSyntax>().ToList();
        
        foreach (var field in fields)
        {
            EmitField(field);
        }
        
        _indentLevel--;
        EmitLine("};");
    }
    
    private void EmitField(FieldDeclarationSyntax field)
    {
        var fieldType = field.Declaration.Type.ToString();
        var fieldName = field.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "unknown";
        
        var idlType = IdlTypeMapper.MapToIdl(fieldType);
        
        // Check for @key attribute
        var hasKeyAttr = field.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(attr => attr.Name.ToString() is "DdsKey" or "DdsKeyAttribute");
            
        // Check for nullable (optional)
        var isOptional = IdlTypeMapper.IsNullableType(fieldType);
        
        var annotations = new List<string>();
        if (hasKeyAttr) annotations.Add("@key");
        if (isOptional) annotations.Add("@optional");
        
        if (annotations.Any())
        {
            Emit(string.Join(" ", annotations) + " ");
        }
        
        EmitLine($"{idlType} {fieldName};");
    }
    
    private void EmitUnion(TypeDeclarationSyntax type)
    {
        // Find discriminator
        var discriminatorField = type.Members
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString() is "DdsDiscriminator" or "DdsDiscriminatorAttribute"));
                
        if (discriminatorField == null)
        {
            EmitLine("// ERROR: Union missing discriminator");
            return;
        }
        
        var discriminatorType = discriminatorField.Declaration.Type.ToString();
        var discriminatorName = discriminatorField.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "discriminator";
        
        EmitLine($"@appendable");
        EmitLine($"union {type.Identifier.Text} switch({IdlTypeMapper.MapToIdl(discriminatorType)}) {{");
        _indentLevel++;
        
        // Emit cases
        var caseFields = type.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString() is "DdsCase" or "DdsCaseAttribute"));
                
        foreach (var caseField in caseFields)
        {
            var caseAttr = caseField.AttributeLists
                .SelectMany(al => al.Attributes)
                .FirstOrDefault(attr => attr.Name.ToString() is "DdsCase" or "DdsCaseAttribute");
                
            if (caseAttr?.ArgumentList?.Arguments.Count > 0)
            {
                var caseValue = caseAttr.ArgumentList.Arguments[0].Expression.ToString();
                var caseType = caseField.Declaration.Type.ToString();
                var caseName = caseField.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "case";
                
                EmitLine($"case {caseValue}: {IdlTypeMapper.MapToIdl(caseType)} {caseName};");
            }
        }
        
        // Emit default case if present
        var defaultField = type.Members
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString() is "DdsDefaultCase" or "DdsDefaultCaseAttribute"));
                
        if (defaultField != null)
        {
            var defaultType = defaultField.Declaration.Type.ToString();
            var defaultName = defaultField.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "default";
            EmitLine($"default: {IdlTypeMapper.MapToIdl(defaultType)} {defaultName};");
        }
        
        _indentLevel--;
        EmitLine("};");
    }
    
    private string GetNamespace(TypeDeclarationSyntax type)
    {
        var namespaceDecl = type.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceDecl != null)
            return namespaceDecl.Name.ToString();

        var fileScopedNs = type.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        if (fileScopedNs != null)
            return fileScopedNs.Name.ToString();

        return "Default";
    }
    
    private void Emit(string text)
    {
        _sb.Append(text);
    }
    
    private void EmitLine(string text = "")
    {
        if (!string.IsNullOrEmpty(text))
        {
            _sb.Append(new string(' ', _indentLevel * 4));
            _sb.Append(text);
        }
        _sb.AppendLine();
    }
}
```

---

### Task 3: Integrate IDL Generation into CodeGenerator

**File:** `tools/CycloneDDS.CodeGen/CodeGenerator.cs` (MODIFY)

Add IDL generation after validation:

```csharp
// After validation succeeds, generate IDL
if (!validator.HasErrors)
{
    var idlEmitter = new IdlEmitter();
    
    if (topicTypes.Any())
    {
        foreach (var type in topicTypes)
        {
            var topicName = ExtractTopicName(type);
            var idlCode = idlEmitter.GenerateIdl(type, topicName);
            
            var idlFile = Path.Combine(generatedDir, $"{type.Identifier.Text}.idl");
            File.WriteAllText(idlFile, idlCode);
            Console.WriteLine($"[CodeGen]   Generated IDL: {idlFile}");
        }
    }
    
    if (unionTypes.Any())
    {
        foreach (var type in unionTypes)
        {
            var idlCode = idlEmitter.GenerateUnionIdl(type);
            
            var idlFile = Path.Combine(generatedDir, $"{type.Identifier.Text}.idl");
            File.WriteAllText(idlFile, idlCode);
            Console.WriteLine($"[CodeGen]   Generated Union IDL: {idlFile}");
        }
    }
}
```

---

### Task 4: Handle Enums

IDL requires enums to be emitted. If an enum is used as a discriminator or field type, emit it:

```csharp
private void EmitEnum(EnumDeclarationSyntax enumDecl)
{
    var underlyingType = "long";  // Default
    if (enumDecl.BaseList?.Types.Count > 0)
    {
        var baseType = enumDecl.BaseList.Types[0].Type.ToString();
        underlyingType = IdlTypeMapper.MapToIdl(baseType);
    }
    
    EmitLine($"@appendable");
    EmitLine($"enum {enumDecl.Identifier.Text} : {underlyingType} {{");
    _indentLevel++;
    
    var members = enumDecl.Members;
    for (int i = 0; i < members.Count; i++)
    {
        var member = members[i];
        var comma = i < members.Count - 1 ? "," : "";
        
        if (member.EqualsValue != null)
        {
            var value = member.EqualsValue.Value.ToString();
            EmitLine($"{member.Identifier.Text} = {value}{comma}");
        }
        else
        {
            EmitLine($"{member.Identifier.Text}{comma}");
        }
    }
    
    _indentLevel--;
    EmitLine("};");
}
```

---

## 🧪 Testing Requirements

### Test Project: `tests/CycloneDDS.CodeGen.Tests/IdlEmitterTests.cs` (NEW)

**Minimum 10 tests required:**

1. ✅ `SimpleStruct_GeneratesCorrectIdl`
2. ✅ `StructWithKeyField_EmitsKeyAnnotation`
3. ✅ `StructWithOptionalField_EmitsOptionalAnnotation`
4. ✅ `StructWithArray_EmitsSequence`
5. ✅ `StructWithFixedString_EmitsOctetArray`
6. ✅ `StructWithGuid_EmitsTypedef`
7. ✅ `Union_GeneratesCorrectIdl`
8. ✅ `UnionWithDefaultCase_EmitsDefaultCase`
9. ✅ `Enum_GeneratesCorrectIdl`
10. ✅ `NestedStruct_EmitsNestedType`

### Example Test:

```csharp
using Xunit;
using CycloneDDS.CodeGen.Emitters;
using Microsoft.CodeAnalysis.CSharp;

namespace CycloneDDS.CodeGen.Tests;

public class IdlEmitterTests
{
    [Fact]
    public void SimpleStruct_GeneratesCorrectIdl()
    {
        var csCode = @"
[DdsTopic(""SimpleTopic"")]
public partial class SimpleType
{
    public int Id;
    public string Name;
}";
        
        var tree = CSharpSyntaxTree.ParseText(csCode);
        var type = tree.GetRoot().DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .First();
            
        var emitter = new IdlEmitter();
        var idl = emitter.GenerateIdl(type, "SimpleTopic");
        
        Assert.Contains("@appendable", idl);
        Assert.Contains("struct SimpleType", idl);
        Assert.Contains("long Id;", idl);
        Assert.Contains("string Name;", idl);
    }
    
    [Fact]
    public void StructWithKeyField_EmitsKeyAnnotation()
    {
        var csCode = @"
[DdsTopic(""KeyedTopic"")]
public partial class KeyedType
{
    [DdsKey]
    public int EntityId;
    public string Data;
}";
        
        var tree = CSharpSyntaxTree.ParseText(csCode);
        var type = tree.GetRoot().DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .First();
            
        var emitter = new IdlEmitter();
        var idl = emitter.GenerateIdl(type, "KeyedTopic");
        
        Assert.Contains("@key long EntityId;", idl);
        Assert.Contains("string Data;", idl);
    }
    
    // ... implement remaining 8 tests ...
}
```

---

### Integration Test

Create an end-to-end test that generates IDL from a realistic schema:

```csharp
[Fact]
public void ComplexSchema_GeneratesValidIdl()
{
    // Test with nested types, arrays, optionals, keys
    // Verify the complete IDL structure
}
```

---

## 📊 Report Requirements

### Required Sections

1. **Executive Summary**
   - IDL generation capabilities implemented
   - Type mappings covered
   - Annotation support

2. **Implementation Details**
   - Type mapping strategy
   - How @appendable is applied
   - How @key and @optional are emitted

3. **Test Results**
   - All 10+ tests passing
   - Example generated IDL

4. **Developer Insights**

   **Q1:** What was the most challenging aspect of IDL code generation?

   **Q2:** How did you handle nested types and dependencies in IDL?

   **Q3:** What edge cases exist for type mapping that aren't handled yet?

   **Q4:** How would you extend this to support custom type mappings from [DdsTypeMap]?

5. **Code Quality Checklist**
   - [ ] IdlTypeMapper implemented
   - [ ] IdlEmitter implemented
   - [ ] Integration into CodeGenerator complete
   - [ ] @appendable emitted for all types
   - [ ] @key annotation working
   - [ ] @optional annotation working
   - [ ] Enums emitted correctly
   - [ ] Unions emitted correctly
   - [ ] 10+ tests passing
   - [ ] Generated IDL syntactically correct

---

## 🎯 Success Criteria

This batch is DONE when:

1. ✅ IdlTypeMapper maps all basic C# types to IDL
2. ✅ IdlEmitter generates @appendable modules
3. ✅ Structs emitted with correct field types
4. ✅ Enums emitted with underlying types
5. ✅ Unions emitted with discriminator and cases
6. ✅ @key annotation applied to key fields
7. ✅ @optional annotation applied to nullable types
8. ✅ Generated .idl files created in Generated/ directory
9. ✅ Minimum 10 tests passing
10. ✅ Report submitted

---

## ⚠️ Common Pitfalls to Avoid

1. **Don't forget @appendable** - EVERY type must be @appendable
2. **Don't skip typedefs** - Guid, DateTime need typedef declarations
3. **Handle arrays correctly** - `int[]` → `sequence<long>` not `long[]`
4. **Namespace mapping** - C# namespaces → IDL modules
5. **Indentation matters** - Generate properly formatted IDL
6. **Optional on structs** - Only emit @optional for nullable types
7. **Test with real schemas** - Don't just test trivial cases

---

## 📚 Reference Materials

- **Task Definition:** `docs/FCDC-TASK-MASTER.md` (FCDC-007)
- **Design:** `docs/FCDC-DETAILED-DESIGN.md` (§4.2 Type Mapping, §5.1 Phase 3)
- **Type Mapping Table:** Design document §4.2
- **IDL 4.2 Spec:** https://www.omg.org/spec/IDL/4.2/PDF
- **Cyclone DDS IDL Guide:** https://cyclonedds.io/docs/cyclonedds/latest/idl_support.html

---

**Focus: Generate syntactically correct, @appendable IDL that Cyclone's idlc can compile.**
