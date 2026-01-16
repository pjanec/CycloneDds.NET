# BATCH-07: Native Types + Unions + Test Quality Fixes (COMBINED)

**Batch Number:** BATCH-07  
**Tasks:** FCDC-009 completion (unions + fixes), FCDC-010 partial (managed views for structs), Test Quality Improvements  
**Phase:** Phase 2 - Code Generator  
**Estimated Effort:** 6-8 days  
**Priority:** CRITICAL  
**Dependencies:** BATCH-06 (Native Structs)

---

## 📋 Onboarding &  Workflow

### Developer Instructions

This is a **COMBINED BATCH** addressing multiple critical areas:

1. **Fix BATCH-06 Issues** - Pack=1 padding problem, test quality
2. **Complete Native Types** - Implement union native types with explicit layout
3. **Start Managed Views** - Begin TManaged ref struct generation (structs only)
4. **Improve Tests** - Add actual layout validation, not just string presence

**You're combining 3 tasks because you're very fast. This batch is intentionally challenging.**

### Required Reading (IN ORDER)

1. **Workflow Guide:** `.dev-workstream/README.md`
2. **BATCH-06 Review:** `.dev-workstream/reviews/BATCH-06-REVIEW.md` - **CRITICAL - Read issues!**
3. **Task Definitions:** `docs/FCDC-TASK-MASTER.md` → FCDC-009, FCDC-010
4. **Design Document:** `docs/FCDC-DETAILED-DESIGN.md` → §8 Type System, §9 Unions, §8.1 Three-Type Model
5. **Union Layout:** Review `Layout/UnionLayoutCalculator.cs` from BATCH-05

### Source Code Location

- **Primary Work Area:** `tools/CycloneDDS.CodeGen/`
- **Test Project:** `tests/CycloneDDS.CodeGen.Tests/`

### Report Submission

**When done, create:**  
`.dev-workstream/reports/BATCH-07-REPORT.md`

---

## 🎯 Objectives

**Part 1: Fix BATCH-06 Issues**
1. Decide and implement Pack=1 strategy (emit padding fields OR remove Pack=1)
2. Add REAL layout validation tests using Roslyn compilation

**Part 2: Native Union Types**
3. Emit unions with `[StructLayout(LayoutKind.Explicit)]`
4. Use `[FieldOffset]` for discriminator and payload
5. Handle union payload alignment correctly

**Part 3: Managed View Types (Structs Only)**
6. Generate TManaged ref struct wrappers
7. Emit ReadOnlySpan<byte> for FixedString fields
8. Emit Int32/Float/etc for primitives
9. Defer managed unions to later batch

---

## ✅ Tasks

### Task 1: Fix Pack=1 Padding Issue

**Decision Required:** Choose ONE strategy:

**Option A: Remove Pack=1, Use Natural Alignment**
```csharp
[StructLayout(LayoutKind.Sequential)]  // No Pack=1
public unsafe struct SimpleStructNative
{
    public byte B;    // offset 0
    public int I;     // offset 4 (C# compiler adds 3 bytes padding)
}
```
**Pros:** C# compiler handles padding automatically  
**Cons:** Only works if C# natural alignment == C natural alignment (usually true)

**Option B: Keep Pack=1, Emit Explicit Padding**
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct SimpleStructNative
{
    public byte B;                      // offset 0
    private fixed byte _padding0[3];   // explicit padding
    public int I;                       // offset 4
}
```
**Pros:** Explicit control, matches layout calculator exactly  
**Cons:** More code generation complexity

**CHOOSE ONE and implement it.**

**Recommended: Option B** (explicit padding) because it's deterministic and matches layout calculator.

**File:** `tools/CycloneDDS.CodeGen/Emitters/NativeTypeEmitter.cs` (MODIFY)

Update `EmitField` to emit padding fields:

```csharp
private int _currentFieldIndex = 0;

private void EmitField(FieldDeclarationSyntax field, FieldLayout layout)
{
    var fieldType = field.Declaration.Type.ToString();
    var fieldName = field.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "unknown";
    
    // Emit explicit padding if needed (for Pack=1)
    if (layout.PaddingBefore > 0)
    {
        EmitLine($"    /// <summary>Padding: {layout.PaddingBefore} bytes</summary>");
        EmitLine($"    private fixed byte _padding{_currentFieldIndex}[{layout.PaddingBefore}];");
        EmitLine();
    }
    
    // ... rest of field emission ...
    
    _currentFieldIndex++;
}
```

---

### Task 2: Add Real Layout Validation Tests

**File:** `tests/CycloneDDS.CodeGen.Tests/NativeTypeValidationTests.cs` (NEW)

Add tests that verify ACTUAL layout using Roslyn compilation:

```csharp
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;

namespace CycloneDDS.CodeGen.Tests;

public class NativeTypeValidationTests
{
    [Fact]
    public void GeneratedStruct_SizeMatchesCalculatedLayout()
    {
        var csCode = @"
[DdsTopic(""TestTopic"")]
public partial class TestType
{
    public byte B;
    public int I;
    public short S;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        // Get expected size from layout calculator
        var calc = new StructLayoutCalculator();
        var layout = calc.CalculateLayout(type);
        
        // Compile generated code and get actual size
        var actualSize = GetCompiledStructSize(nativeCode, "TestTypeNative");
        
        Assert.Equal(layout.TotalSize, actualSize);
    }
    
    [Fact]
    public void GeneratedStruct_FieldOffsetsMatchLayout()
    {
        var csCode = @"
[DdsTopic(""TestTopic"")]
public partial class TestType
{
    public byte B;
    public long L;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        // Get expected offsets
        var calc = new StructLayoutCalculator();
        var layout = calc.CalculateLayout(type);
        
        // Compile and get actual offsets
        var offsets = GetCompiledFieldOffsets(nativeCode, "TestTypeNative");
        
        Assert.Equal(layout.Fields[0].Offset, offsets["B"]);
        Assert.Equal(layout.Fields[1].Offset, offsets["L"]);
    }
    
    private int GetCompiledStructSize(string code, string typeName)
    {
        var assembly = CompileToAssembly(code);
        var type = assembly.GetType($"TestNamespace.{typeName}");
        return Marshal.SizeOf(type);
    }
    
    private Dictionary<string, int> GetCompiledFieldOffsets(string code, string typeName)
    {
        var assembly = CompileToAssembly(code);
        var type = assembly.GetType($"TestNamespace.{typeName}");
        
        var offsets = new Dictionary<string, int>();
        foreach (var field in type.GetFields())
        {
            var offset = Marshal.OffsetOf(type, field.Name).ToInt32();
            offsets[field.Name] = offset;
        }
        return offsets;
    }
    
    private Assembly CompileToAssembly(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var references = new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(StructLayoutAttribute).Assembly.Location)
        };
        
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true));
        
        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        
        if (!result.Success)
        {
            var errors = string.Join("\n", result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage()));
            throw new Exception($"Compilation failed:\n{errors}");
        }
        
        ms.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(ms.ToArray());
    }
}
```

**MINIMUM 5 new validation tests required.**

---

### Task 3: Implement Union Native Types

**File:** `tools/CycloneDDS.CodeGen/Emitters/NativeTypeEmitter.cs` (MODIFY)

Add method to generate unions with explicit layout:

```csharp
public string GenerateNativeUnion(TypeDeclarationSyntax type, string namespaceName)
{
    _sb.Clear();
    
    var typeName = type.Identifier.Text;
    var nativeTypeName = $"{typeName}Native";
    
    // File header
    EmitLine("// <auto-generated/>");
    EmitLine($"// Generated native union for {typeName}");
    EmitLine();
    EmitLine("using System;");
    EmitLine("using System.Runtime.InteropServices;");
    EmitLine();
    EmitLine($"namespace {namespaceName};");
    EmitLine();
    
    // Calculate union layout
    var unionCalc = new UnionLayoutCalculator();
    var layout = unionCalc.CalculateLayout(type);
    
    // Union declaration - EXPLICIT layout
    EmitLine($"/// <summary>");
    EmitLine($"/// Native blittable union for {typeName}.");
    EmitLine($"/// Total size: {layout.TotalSize} bytes");
    EmitLine($"/// Discriminator offset: 0, Payload offset: {layout.PayloadOffset}");
    EmitLine($"/// </summary>");
    EmitLine("[StructLayout(LayoutKind.Explicit)]");
    EmitLine($"public unsafe struct {nativeTypeName}");
    EmitLine("{");
    
    // Emit discriminator at offset 0
    var discriminatorField = type.Members.OfType<FieldDeclarationSyntax>()
        .First(f => f.AttributeLists.SelectMany(al => al.Attributes)
            .Any(attr => attr.Name.ToString().Contains("Discriminator")));
    
    var discType = discriminatorField.Declaration.Type.ToString();
    var discName = discriminatorField.Declaration.Variables.First().Identifier.Text;
    var nativeDiscType = MapToNativeType(discType, out _, out _);
    
    EmitLine($"    /// <summary>Discriminator at offset 0</summary>");
    EmitLine($"    [FieldOffset(0)]");
    EmitLine($"    public {nativeDiscType} {discName};");
    EmitLine();
    
    // Emit each case arm at payload offset
    var caseFields = type.Members.OfType<FieldDeclarationSyntax>()
        .Where(f => f.AttributeLists.SelectMany(al => al.Attributes)
            .Any(attr => attr.Name.ToString().Contains("Case")));
    
    foreach (var caseField in caseFields)
    {
        var armType = caseField.Declaration.Type.ToString();
        var armName = caseField.Declaration.Variables.First().Identifier.Text;
        var nativeArmType = MapToNativeType(armType, out var isFixed, out var size);
        
        EmitLine($"    /// <summary>Union arm at offset {layout.PayloadOffset}</summary>");
        EmitLine($"    [FieldOffset({layout.PayloadOffset})]");
        
        if (isFixed)
        {
            EmitLine($"    public fixed {nativeArmType} {armName}[{size}];");
        }
        else
        {
            EmitLine($"    public {nativeArmType} {armName};");
        }
        EmitLine();
    }
    
    EmitLine("}");
    
    return _sb.ToString();
}
```

---

### Task 4: Implement Managed View Types (Structs Only)

**File:** `tools/CycloneDDS.CodeGen/Emitters/ManagedViewEmitter.cs` (NEW)

Generate TManaged ref struct views:

```csharp
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace CycloneDDS.CodeGen.Emitters;

public class ManagedViewEmitter
{
    private readonly StringBuilder _sb = new();
    
    /// <summary>
    /// Generate TManaged ref struct view for topic type.
    /// </summary>
    public string GenerateManagedView(TypeDeclarationSyntax type, string namespaceName)
    {
        _sb.Clear();
        
        var typeName = type.Identifier.Text;
        var managedTypeName = $"{typeName}Managed";
        var nativeTypeName = $"{typeName}Native";
        
        // File header
        EmitLine("// <auto-generated/>");
        EmitLine($"// Generated managed view for {typeName}");
        EmitLine();
        EmitLine("using System;");
        EmitLine();
        EmitLine($"namespace {namespaceName};");
        EmitLine();
        
        // Ref struct declaration
        EmitLine($"/// <summary>");
        EmitLine($"/// Managed view over native {typeName} data.");
        EmitLine($"/// </summary>");
        EmitLine($"public ref struct {managedTypeName}");
        EmitLine("{");
        
        // Store reference to native
        EmitLine($"    private readonly ref {nativeTypeName} _native;");
        EmitLine();
        
        // Constructor
        EmitLine($"    internal {managedTypeName}(ref {nativeTypeName} native)");
        EmitLine("    {");
        EmitLine("        _native = ref native;");
        EmitLine("    }");
        EmitLine();
        
        // Emit property accessors
        var fields = type.Members.OfType<FieldDeclarationSyntax>().ToList();
        foreach (var field in fields)
        {
            EmitManagedProperty(field);
        }
        
        EmitLine("}");
        
        return _sb.ToString();
    }
    
    private void EmitManagedProperty(FieldDeclarationSyntax field)
    {
        var fieldType = field.Declaration.Type.ToString();
        var fieldName = field.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "unknown";
        
        // Primitives: direct access
        if (fieldType is "byte" or "sbyte" or "short" or "ushort" or 
                       "int" or "uint" or "long" or "ulong" or 
                       "float" or "double" or "bool")
        {
            EmitLine($"    public {fieldType} {fieldName} => _native.{fieldName};");
            EmitLine();
            return;
        }
        
        // FixedString: ReadOnlySpan<byte>
        if (fieldType.StartsWith("FixedString"))
        {
            EmitLine($"    public unsafe ReadOnlySpan<byte> {fieldName}");
            EmitLine("    {");
            EmitLine("        get");
            EmitLine("        {");
            EmitLine($"            fixed (byte* ptr = _native.{fieldName})");
            EmitLine("            {");
            var size = fieldType switch {
                "FixedString32" => 32,
                "FixedString64" => 64,
                "FixedString128" => 128,
                _ => 32
            };
            EmitLine($"                return new ReadOnlySpan<byte>(ptr, {size});");
            EmitLine("            }");
            EmitLine("        }");
            EmitLine("    }");
            EmitLine();
            return;
        }
        
        // Guid: return as Guid
        if (fieldType is "Guid" or "System.Guid")
        {
            EmitLine($"    public unsafe Guid {fieldName}");
            EmitLine("    {");
            EmitLine("        get");
            EmitLine("        {");
            EmitLine($"            fixed (byte* ptr = _native.{fieldName})");
            EmitLine("            {");
            EmitLine("                return *(Guid*)ptr;");
            EmitLine("            }");
            EmitLine("        }");
            EmitLine("    }");
            EmitLine();
            return;
        }
        
        // DateTime: return as DateTime
        if (fieldType is "DateTime" or "System.DateTime")
        {
            EmitLine($"    public DateTime {fieldName} => new DateTime(_native.{fieldName});");
            EmitLine();
            return;
        }
        
        // TODO: Handle string (unbounded), arrays, nested types
        // For now, skip
    }
    
    private void EmitLine(string text = "")
    {
        _sb.AppendLine(text);
    }
}
```

---

### Task 5: Integration into CodeGenerator

**File:** `tools/CycloneDDS.CodeGen/CodeGenerator.cs` (MODIFY)

Add union and managed view generation:

```csharp
// After native type generation...

// Generate Unions
var unionTypes = root.DescendantNodes()
    .OfType<TypeDeclarationSyntax>()
    .Where(HasDdsUnionAttribute)
    .ToList();

foreach (var union in unionTypes)
{
    var nativeCode = nativeEmitter.GenerateNativeUnion(union, namespaceName);
    var nativeFile = Path.Combine(generatedDir, $"{union.Identifier.Text}Native.g.cs");
    File.WriteAllText(nativeFile, nativeCode);
    Console.WriteLine($"[CodeGen]   Generated Native Union: {nativeFile}");
}

// Generate Managed Views (structs only for now)
var managedEmitter = new ManagedViewEmitter();

foreach (var type in topicTypes)
{
    var managedCode = managedEmitter.GenerateManagedView(type, namespaceName);
    var managedFile = Path.Combine(generatedDir, $"{type.Identifier.Text}Managed.g.cs");
    File.WriteAllText(managedFile, managedCode);
    Console.WriteLine($"[CodeGen]   Generated Managed View: {managedFile}");
}
```

---

## 🧪 Testing Requirements

### Minimum 20 Tests Required (Across All Parts):

**Part 1: Padding/Validation Tests (5 tests)**
1. ✅ `GeneratedStruct_SizeMatchesCalculatedLayout`
2. ✅ `GeneratedStruct_FieldOffsetsMatchLayout`
3. ✅ `StructWithPadding_HasExplicitPaddingFields` (if Pack=1 + padding)
4. ✅ `GeneratedCode_CompilesWithoutErrors`
5. ✅ `ComplexStruct_AllOffsetsCorrect`

**Part 2: Union Tests (8 tests)**
6. ✅ `Union_HasExplicitLayout`
7. ✅ `Union_DiscriminatorAtOffset0`
8. ✅ `Union_PayloadAtCorrectOffset`
9. ✅ `Union_SizeMatchesCalculatedLayout`
10. ✅ `UnionWithMultipleArms_AllAtPayloadOffset`
11. ✅ `UnionWithSmallDisc_PayloadPadded`
12. ✅ `UnionFieldOffsets_MatchLayout`
13. ✅ `GeneratedUnion_CompilesWithoutErrors`

**Part 3: Managed View Tests (7 tests)**
14. ✅ `ManagedView_IsRefStruct`
15. ✅ `ManagedView_HasPrimitiveProperties`
16. ✅ `ManagedView_FixedStringReturnsReadOnlySpan`
17. ✅ `ManagedView_GuidProperty`
18. ✅ `ManagedView_DateTimeProperty`
19. ✅ `ManagedView_CompilesWithoutErrors`
20. ✅ `ManagedView_CanAccessNativeData`

---

## 📊 Report Requirements

### Required Sections

1. **Executive Summary**
   - BATCH-06 issues fixed
   - Unions implemented
   - Managed views started
   - Test quality dramatically improved

2. **Implementation Details**
   - Pack=1 strategy chosen and why
   - Explicit layout for unions
   - Managed view ref struct approach

3. **Test Results**
   - All 76+ tests passing (56 previous + 20 new)
   - Show example of actual size/offset validation

4. **Developer Insights**

   **Q1:** Which padding strategy did you choose (explicit padding vs natural) and why?

   **Q2:** What was the trickiest part of union explicit layout?

   **Q3:** How do managed views handle fixed buffers safely?

   **Q4:** What performance implications exist for managed views over native?

5. **Code Quality Checklist**
   - [ ] Pack=1 padding issue resolved
   - [ ] Explicit padding fields emitted (if Pack=1)
   - [ ] Actual size/offset validation tests added
   - [ ] Union explicit layout implemented
   - [ ] Discriminator at offset 0
   - [ ] Payload at calculated offset
   - [ ] Managed view ref structs generated
   - [ ] Fixed string → ReadOnlySpan
   - [ ] Guid/DateTime properties working
   - [ ] 20+ tests passing
   - [ ] All previous tests still passing

---

## 🎯 Success Criteria

This batch is DONE when:

1. ✅ BATCH-06 Pack=1 issue fixed (explicit padding OR natural alignment)
2. ✅ Real layout validation tests using compilation
3. ✅ Union native types with [StructLayout(Explicit)]
4. ✅ [FieldOffset] used for discriminator and payload
5. ✅ Managed view ref structs for struct types
6. ✅ ReadOnlySpan<byte> for FixedString
7. ✅ Direct access for primitives
8. ✅ Minimum 20 tests passing (5 validation + 8 union + 7 managed)
9. ✅ All 56 previous tests still passing
10. ✅ Report submitted

---

## ⚠️ Common Pitfalls to Avoid

1. **Explicit padding index** - Track field index to name padding fields uniquely
2. **Union payload offset** - MUST use UnionLayoutCalculator, not guess
3. **FieldOffset precision** - Off-by-one errors will break everything
4. **Ref struct lifetime** - Managed views can't escape method scope
5. **Fixed buffer access** - Requires unsafe and fixed statement
6. **Compilation tests** - Need proper references (System.Runtime.InteropServices)
7. **Test actual behavior** - Don't just check string presence!

---

## 📚 Reference Materials

- **BATCH-06 Review:** `.dev-workstream/reviews/BATCH-06-REVIEW.md` (Issues to fix!)
- **Task Definitions:** `docs/FCDC-TASK-MASTER.md` (FCDC-009, FCDC-010)
- **Design:** `docs/FCDC-DETAILED-DESIGN.md` (§8, §9, §8.1)
- **Layout Calculators:** `tools/CycloneDDS.CodeGen/Layout/*.cs`
- **Explicit Layout Docs:** https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.layoutkind
- **Ref Struct Docs:** https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct

---

**Focus: FIX quality issues from BATCH-06, COMPLETE native types with unions, START managed views. This is a demanding batch - take your time to get it RIGHT, especially the tests!**
