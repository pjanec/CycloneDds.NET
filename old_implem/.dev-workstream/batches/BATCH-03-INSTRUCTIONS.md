# BATCH-03: Schema Validation Logic

**Batch Number:** BATCH-03  
**Tasks:** FCDC-006 (Schema Validation Logic)  
**Phase:** Phase 2 - Code Generator  
**Estimated Effort:** 4-5 days  
**Priority:** CRITICAL  
**Dependencies:** BATCH-02.2 (CLI Tool Infrastructure)

---

## 📋 Onboarding & Workflow

### Developer Instructions

This batch implements **comprehensive schema validation** for the CLI code generator. You will add validation rules that enforce:
- DDS appendable evolution constraints
- Type safety and consistency
- Union/enum correctness
- Bounded type limits
- Schema fingerprinting for break detection

### Required Reading (IN ORDER)

1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Task Definition:** `docs/FCDC-TASK-MASTER.md` → See FCDC-006
3. **Design Document:** `docs/FCDC-DETAILED-DESIGN.md` → §5.4 Schema Evolution Validation
4. **Previous Batches:** Review BATCH-02.1 and BATCH-02.2 for CLI tool architecture

### Source Code Location

- **Primary Work Area:** `tools/CycloneDDS.CodeGen/`
- **Test Project:** `tests/CycloneDDS.CodeGen.Tests/`
- **Models:** `tools/CycloneDDS.CodeGen/Models/` (existing)

### Report Submission

**When done, create:**  
`.dev-workstream/reports/BATCH-03-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/BATCH-03-QUESTIONS.md`

---

## 🎯 Objectives

Implement full schema validation in the CLI code generator to:

1. **Validate schema correctness** - Catch errors before code generation
2. **Enforce appendable evolution** - Prevent breaking changes
3. **Validate union/enum constraints** - Ensure discriminator uniqueness
4. **Check bounded type limits** - Validate FixedString sizes, sequence bounds
5. **Generate schema fingerprints** - Detect schema changes across builds
6. **Provide detailed diagnostics** - Clear error messages with fix suggestions

---

## ✅ Tasks

### Task 1: Implement Diagnostic System

**Files:** 
- `tools/CycloneDDS.CodeGen/Diagnostics/DiagnosticCode.cs` (NEW)
- `tools/CycloneDDS.CodeGen/Diagnostics/Diagnostic.cs` (NEW)
- `tools/CycloneDDS.CodeGen/Diagnostics/DiagnosticSeverity.cs` (NEW)

Create a diagnostic reporting system for validation errors:

```csharp
// DiagnosticSeverity.cs
namespace CycloneDDS.CodeGen.Diagnostics;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}
```

```csharp
// DiagnosticCode.cs
namespace CycloneDDS.CodeGen.Diagnostics;

public static class DiagnosticCode
{
    // Schema structure errors
    public const string MissingTopicAttribute = "FCDC1001";
    public const string MissingQosAttribute = "FCDC1002";
    public const string InvalidTopicName = "FCDC1003";
    public const string DuplicateTopicName = "FCDC1004";
    
    // Type validation errors
    public const string UnsupportedFieldType = "FCDC1010";
    public const string MissingKeyField = "FCDC1011";  // Warning
    public const string InvalidKeyFieldType = "FCDC1012";
    
    // Union validation errors
    public const string MissingDiscriminator = "FCDC1020";
    public const string MultipleDiscriminators = "FCDC1021";
    public const string DuplicateCaseValue = "FCDC1022";
    public const string MultipleDefaultCases = "FCDC1023";
    public const string InvalidDiscriminatorType = "FCDC1024";
    public const string UnusedEnumValue = "FCDC1025";  // Warning
    
    // Bounded type errors
    public const string ExcessiveBound = "FCDC1030";
    public const string InvalidBoundValue = "FCDC1031";
    
    // Evolution errors (breaking changes)
    public const string MemberRemoved = "FCDC2001";
    public const string MemberReordered = "FCDC2002";
    public const string MemberTypeChanged = "FCDC2003";
    public const string MemberInsertedNotAtEnd = "FCDC2004";
}
```

```csharp
// Diagnostic.cs
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CycloneDDS.CodeGen.Diagnostics;

public record Diagnostic
{
    public required string Code { get; init; }
    public required DiagnosticSeverity Severity { get; init; }
    public required string Message { get; init; }
    public string? SourceFile { get; init; }
    public int? Line { get; init; }
    public string? TypeName { get; init; }
    public string? FieldName { get; init; }
    
    public override string ToString()
    {
        var location = SourceFile != null && Line.HasValue 
            ? $"{SourceFile}({Line}): " 
            : TypeName != null 
                ? $"{TypeName}: " 
                : "";
                
        var severity = Severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            _ => "info"
        };
        
        return $"{location}{severity} {Code}: {Message}";
    }
}
```

---

### Task 2: Implement Schema Validator

**File:** `tools/CycloneDDS.CodeGen/Validation/SchemaValidator.cs` (NEW)

```csharp
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CycloneDDS.CodeGen.Models;
using CycloneDDS.CodeGen.Diagnostics;

namespace CycloneDDS.CodeGen.Validation;

public class SchemaValidator
{
    private readonly List<Diagnostic> _diagnostics = new();
    
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;
    public bool HasErrors => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    
    public void ValidateTopicType(TypeDeclarationSyntax type, string sourceFile)
    {
        ValidateTopicAttribute(type, sourceFile);
        ValidateQosAttribute(type, sourceFile);
        ValidateFields(type, sourceFile);
    }
    
    public void ValidateUnionType(TypeDeclarationSyntax type, string sourceFile)
    {
        ValidateDiscriminator(type, sourceFile);
        ValidateCases(type, sourceFile);
    }
    
    private void ValidateTopicAttribute(TypeDeclarationSyntax type, string sourceFile)
    {
        var topicAttr = type.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr =>
            {
                var name = attr.Name.ToString();
                return name is "DdsTopic" or "DdsTopicAttribute";
            });
            
        if (topicAttr == null)
        {
            AddError(DiagnosticCode.MissingTopicAttribute,
                $"Type '{type.Identifier}' must have [DdsTopic(...)] attribute",
                sourceFile, GetLineNumber(type), type.Identifier.Text);
            return;
        }
        
        // Validate topic name
        if (topicAttr.ArgumentList?.Arguments.Count > 0)
        {
            var topicNameArg = topicAttr.ArgumentList.Arguments[0];
            var topicName = topicNameArg.Expression.ToString().Trim('"');
            
            if (string.IsNullOrWhiteSpace(topicName))
            {
                AddError(DiagnosticCode.InvalidTopicName,
                    "Topic name cannot be empty or whitespace",
                    sourceFile, GetLineNumber(type), type.Identifier.Text);
            }
            
            // Topic name validation: alphanumeric + underscore, no spaces
            if (!System.Text.RegularExpressions.Regex.IsMatch(topicName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            {
                AddError(DiagnosticCode.InvalidTopicName,
                    $"Topic name '{topicName}' invalid. Must start with letter/underscore, contain only alphanumerics",
                    sourceFile, GetLineNumber(type), type.Identifier.Text);
            }
        }
    }
    
    private void ValidateQosAttribute(TypeDeclarationSyntax type, string sourceFile)
    {
        var qosAttr = type.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr =>
            {
                var name = attr.Name.ToString();
                return name is "DdsQos" or "DdsQosAttribute";
            });
            
        if (qosAttr == null)
        {
            AddWarning(DiagnosticCode.MissingQosAttribute,
                $"Type '{type.Identifier}' missing [DdsQos] attribute. Using defaults: Reliable, Volatile, KeepLast(1)",
                sourceFile, GetLineNumber(type), type.Identifier.Text);
        }
    }
    
    private void ValidateFields(TypeDeclarationSyntax type, string sourceFile)
    {
        var fields = type.Members.OfType<FieldDeclarationSyntax>().ToList();
        
        foreach (var field in fields)
        {
            ValidateFieldType(field, type, sourceFile);
        }
    }
    
    private void ValidateFieldType(FieldDeclarationSyntax field, TypeDeclarationSyntax parentType, string sourceFile)
    {
        var typeName = field.Declaration.Type.ToString();
        
        // Check for unsupported types
        var unsupportedTypes = new[] { "List<", "Dictionary<", "Hashtable", "ArrayList" };
        if (unsupportedTypes.Any(ut => typeName.Contains(ut)))
        {
            var fieldName = field.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "?";
            AddError(DiagnosticCode.UnsupportedFieldType,
                $"Field '{fieldName}' uses unsupported type '{typeName}'. Use T[] or BoundedSeq<T,N> instead",
                sourceFile, GetLineNumber(field), parentType.Identifier.Text, fieldName);
        }
    }
    
    private void ValidateDiscriminator(TypeDeclarationSyntax type, string sourceFile)
    {
        var discriminators = type.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr =>
                {
                    var name = attr.Name.ToString();
                    return name is "DdsDiscriminator" or "DdsDiscriminatorAttribute";
                }))
            .ToList();
            
        if (discriminators.Count == 0)
        {
            AddError(DiagnosticCode.MissingDiscriminator,
                $"Union type '{type.Identifier}' must have exactly one [DdsDiscriminator] field",
                sourceFile, GetLineNumber(type), type.Identifier.Text);
        }
        else if (discriminators.Count > 1)
        {
            AddError(DiagnosticCode.MultipleDiscriminators,
                $"Union type '{type.Identifier}' has {discriminators.Count} discriminators, expected exactly 1",
                sourceFile, GetLineNumber(type), type.Identifier.Text);
        }
    }
    
    private void ValidateCases(TypeDeclarationSyntax type, string sourceFile)
    {
        var caseFields = type.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr =>
                {
                    var name = attr.Name.ToString();
                    return name is "DdsCase" or "DdsCaseAttribute";
                }))
            .ToList();
            
        var caseValues = new HashSet<string>();
        
        foreach (var caseField in caseFields)
        {
            var caseAttr = caseField.AttributeLists
                .SelectMany(al => al.Attributes)
                .FirstOrDefault(attr =>
                {
                    var name = attr.Name.ToString();
                    return name is "DdsCase" or "DdsCaseAttribute";
                });
                
            if (caseAttr?.ArgumentList?.Arguments.Count > 0)
            {
                var caseValue = caseAttr.ArgumentList.Arguments[0].Expression.ToString();
                
                if (!caseValues.Add(caseValue))
                {
                    var fieldName = caseField.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "?";
                    AddError(DiagnosticCode.DuplicateCaseValue,
                        $"Union case value '{caseValue}' used multiple times in type '{type.Identifier}'",
                        sourceFile, GetLineNumber(caseField), type.Identifier.Text, fieldName);
                }
            }
        }
        
        // Check for multiple default cases
        var defaultCases = type.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr =>
                {
                    var name = attr.Name.ToString();
                    return name is "DdsDefaultCase" or "DdsDefaultCaseAttribute";
                }))
            .ToList();
            
        if (defaultCases.Count > 1)
        {
            AddError(DiagnosticCode.MultipleDefaultCases,
                $"Union type '{type.Identifier}' has {defaultCases.Count} default cases, expected at most 1",
                sourceFile, GetLineNumber(type), type.Identifier.Text);
        }
    }
    
    private void AddError(string code, string message, string? sourceFile = null, int? line = null, 
        string? typeName = null, string? fieldName = null)
    {
        _diagnostics.Add(new Diagnostic
        {
            Code = code,
            Severity = DiagnosticSeverity.Error,
            Message = message,
            SourceFile = sourceFile,
            Line = line,
            TypeName = typeName,
            FieldName = fieldName
        });
    }
    
    private void AddWarning(string code, string message, string? sourceFile = null, int? line = null, 
        string? typeName = null, string? fieldName = null)
    {
        _diagnostics.Add(new Diagnostic
        {
            Code = code,
            Severity = DiagnosticSeverity.Warning,
            Message = message,
            SourceFile = sourceFile,
            Line = line,
            TypeName = typeName,
            FieldName = fieldName
        });
    }
    
    private int GetLineNumber(SyntaxNode node)
    {
        return node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    }
}
```

---

### Task 3: Implement Schema Fingerprinting

**File:** `tools/CycloneDDS.CodeGen/Validation/SchemaFingerprint.cs` (NEW)

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CycloneDDS.CodeGen.Validation;

public class SchemaFingerprint
{
    public string TypeName { get; }
    public string Hash { get; }
    public List<MemberInfo> Members { get; }
    
    public SchemaFingerprint(string typeName, string hash, List<MemberInfo> members)
    {
        TypeName = typeName;
        Hash = hash;
        Members = members;
    }
    
    public record MemberInfo(int Index, string Name, string Type);
    
    public static SchemaFingerprint Compute(TypeDeclarationSyntax type)
    {
        var typeName = type.Identifier.Text;
        var members = new List<MemberInfo>();
        
        var fields = type.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => !f.Modifiers.Any(m => m.Text == "const"))
            .ToList();
            
        for (int i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            var fieldType = field.Declaration.Type.ToString();
            var fieldName = field.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "?";
            
            members.Add(new MemberInfo(i, fieldName, fieldType));
        }
        
        // Compute hash from member order + names + types
        var sb = new StringBuilder();
        foreach (var member in members)
        {
            sb.Append($"{member.Index}:{member.Name}:{member.Type};");
        }
        
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        var hash = Convert.ToHexString(hashBytes);
        
        return new SchemaFingerprint(typeName, hash, members);
    }
    
    public static EvolutionResult CompareForEvolution(SchemaFingerprint old, SchemaFingerprint @new)
    {
        var result = new EvolutionResult();
        
        if (old.TypeName != @new.TypeName)
        {
            result.AddError($"Type name changed from '{old.TypeName}' to '{@new.TypeName}'");
            return result;
        }
        
        // Check for removed members
        for (int i = 0; i < old.Members.Count; i++)
        {
            var oldMember = old.Members[i];
            var newMember = i < @new.Members.Count ? @new.Members[i] : null;
            
            if (newMember == null)
            {
                result.AddError($"Member '{oldMember.Name}' at index {i} was removed");
                continue;
            }
            
            if (oldMember.Name != newMember.Name)
            {
                result.AddError($"Member at index {i} renamed from '{oldMember.Name}' to '{newMember.Name}' or reordered");
            }
            
            if (oldMember.Type != newMember.Type)
            {
                result.AddError($"Member '{oldMember.Name}' type changed from '{oldMember.Type}' to '{newMember.Type}'");
            }
        }
        
        // Check for inserted members (not at end)
        if (@new.Members.Count > old.Members.Count)
        {
            result.MembersAdded = @new.Members.Count - old.Members.Count;
            result.AddInfo($"{result.MembersAdded} new member(s) added (appendable-safe)");
        }
        
        return result;
    }
}

public class EvolutionResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Info { get; } = new();
    public int MembersAdded { get; set; }
    
    public bool HasBreakingChanges => Errors.Any();
    
    public void AddError(string message) => Errors.Add(message);
    public void AddWarning(string message) => Warnings.Add(message);
    public void AddInfo(string message) => Info.Add(message);
}
```

---

### Task 4: Persist and Compare Fingerprints

**File:** `tools/CycloneDDS.CodeGen/Validation/FingerprintStore.cs` (NEW)

```csharp
using System.Text.Json;

namespace CycloneDDS.CodeGen.Validation;

public class FingerprintStore
{
    private readonly string _storeFilePath;
    private Dictionary<string, SchemaFingerprint> _fingerprints = new();
    
    public FingerprintStore(string sourceDirectory)
    {
        // Store in Generated/ folder
        var generatedDir = Path.Combine(sourceDirectory, "Generated");
        Directory.CreateDirectory(generatedDir);
        _storeFilePath = Path.Combine(generatedDir, ".schema-fingerprints.json");
        
        Load();
    }
    
    public void Load()
    {
        if (!File.Exists(_storeFilePath))
        {
            _fingerprints = new();
            return;
        }
        
        try
        {
            var json = File.ReadAllText(_storeFilePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, FingerprintData>>(json);
            
            if (data != null)
            {
                _fingerprints = data.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new SchemaFingerprint(
                        kvp.Value.TypeName,
                        kvp.Value.Hash,
                        kvp.Value.Members.Select(m => new SchemaFingerprint.MemberInfo(m.Index, m.Name, m.Type)).ToList()
                    )
                );
            }
        }
        catch
        {
            // Corrupted file, start fresh
            _fingerprints = new();
        }
    }
    
    public void Save()
    {
        var data = _fingerprints.ToDictionary(
            kvp => kvp.Key,
            kvp => new FingerprintData
            {
                TypeName = kvp.Value.TypeName,
                Hash = kvp.Value.Hash,
                Members = kvp.Value.Members.Select(m => new MemberData 
                { 
                    Index = m.Index, 
                    Name = m.Name, 
                    Type = m.Type 
                }).ToList()
            }
        );
        
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_storeFilePath, json);
    }
    
    public SchemaFingerprint? GetPrevious(string typeName)
    {
        _fingerprints.TryGetValue(typeName, out var fingerprint);
        return fingerprint;
    }
    
    public void Update(string typeName, SchemaFingerprint fingerprint)
    {
        _fingerprints[typeName] = fingerprint;
    }
    
    private class FingerprintData
    {
        public string TypeName { get; set; } = "";
        public string Hash { get; set; } = "";
        public List<MemberData> Members { get; set; } = new();
    }
    
    private class MemberData
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
    }
}
```

---

### Task 5: Integrate Validation into CodeGenerator

**File:** `tools/CycloneDDS.CodeGen/CodeGenerator.cs` (MODIFY)

Add validation before generation:

```csharp
using CycloneDDS.CodeGen.Validation;
using CycloneDDS.CodeGen.Diagnostics;

public class CodeGenerator
{
    public int Generate(string sourceDirectory)
    {
        int filesGenerated = 0;
        var validator = new SchemaValidator();
        var fingerprintStore = new FingerprintStore(sourceDirectory);
        var evolutionErrors = new List<string>();
        
        // ... existing file discovery ...
        
        foreach (var file in csFiles)
        {
            try
            {
                var code = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(code, path: file);
                var root = tree.GetRoot();

                // Find and validate topics
                var topicTypes = root.DescendantNodes()
                    .OfType<TypeDeclarationSyntax>()
                    .Where(HasDdsTopicAttribute)
                    .ToList();

                foreach (var type in topicTypes)
                {
                    // Validate schema
                    validator.ValidateTopicType(type, file);
                    
                    // Check evolution
                    var currentFingerprint = SchemaFingerprint.Compute(type);
                    var previousFingerprint = fingerprintStore.GetPrevious(type.Identifier.Text);
                    
                    if (previousFingerprint != null)
                    {
                        var evolutionResult = SchemaFingerprint.CompareForEvolution(previousFingerprint, currentFingerprint);
                        
                        if (evolutionResult.HasBreakingChanges)
                        {
                            foreach (var error in evolutionResult.Errors)
                            {
                                evolutionErrors.Add($"{type.Identifier.Text}: {error}");
                                validator.Diagnostics.Add(new Diagnostic
                                {
                                    Code = DiagnosticCode.MemberTypeChanged,
                                    Severity = DiagnosticSeverity.Error,
                                    Message = error,
                                    SourceFile = file,
                                    TypeName = type.Identifier.Text
                                });
                            }
                        }
                    }
                    
                    fingerprintStore.Update(type.Identifier.Text, currentFingerprint);
                }

                if (topicTypes.Any())
                {
                    filesGenerated += GenerateForTopics(file, topicTypes);
                }

                // Similar for unions...
                var unionTypes = root.DescendantNodes()
                    .OfType<TypeDeclarationSyntax>()
                    .Where(HasDdsUnionAttribute)
                    .ToList();
                    
                foreach (var type in unionTypes)
                {
                    validator.ValidateUnionType(type, file);
                }

                if (unionTypes.Any())
                {
                    filesGenerated += GenerateForUnions(file, unionTypes);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // ... existing error handling ...
            }
        }
        
        // Report all diagnostics
        foreach (var diagnostic in validator.Diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
                Console.Error.WriteLine(diagnostic);
            else
                Console.WriteLine(diagnostic);
        }
        
        // Save fingerprints
        fingerprintStore.Save();
        
        // Return error code if validation failed
        if (validator.HasErrors)
        {
            Console.Error.WriteLine($"\n[CodeGen] Validation failed with {validator.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error)} error(s)");
            return -1;  // Signal error to build system
        }

        return filesGenerated;
    }
    
    // ... rest of existing code ...
}
```

---

### Task 6: Update Program.cs for Error Handling

**File:** `tools/CycloneDDS.CodeGen/Program.cs` (MODIFY)

```csharp
static int Main(string[] args)
{
    // ... existing arg validation ...

    var generator = new CodeGenerator();
    var result = generator.Generate(sourceDir);
    
    if (result < 0)
    {
        Console.Error.WriteLine("[CodeGen] Code generation failed due to validation errors");
        return 1;  // Non-zero exit code signals build failure
    }

    Console.WriteLine($"[CodeGen] Generated {result} files");
    return 0;
}
```

---

## 🧪 Testing Requirements

### Test Project: `tests/CycloneDDS.CodeGen.Tests/ValidationTests.cs` (NEW)

Create comprehensive validation tests:

**Minimum 15 tests required:**

1. ✅ `TopicWithoutAttribute_ReportsError`
2. ✅ `EmptyTopicName_ReportsError`
3. ✅ `InvalidTopicName_ReportsError`
4. ✅ `MissingQoS_ReportsWarning`
5. ✅ `UnsupportedFieldType_ReportsError`
6. ✅ `UnionWithoutDiscriminator_ReportsError`
7. ✅ `UnionWithMultipleDiscriminators_ReportsError`
8. ✅ `DuplicateUnionCase_ReportsError`
9. ✅ `MultipleDefaultCases_ReportsError`
10. ✅ `ValidTopicSchema_PassesValidation`
11. ✅ `ValidUnionSchema_PassesValidation`
12. ✅ `MemberAdded_AllowedAppendable`
13. ✅ `MemberRemoved_ReportsEvolutionError`
14. ✅ `MemberReordered_ReportsEvolutionError`
15. ✅ `MemberTypeChanged_ReportsEvolutionError`

### Example Test Implementation:

```csharp
using Xunit;
using CycloneDDS.CodeGen.Validation;
using Microsoft.CodeAnalysis.CSharp;

namespace CycloneDDS.CodeGen.Tests;

public class ValidationTests
{
    [Fact]
    public void TopicWithoutAttribute_ReportsError()
    {
        var source = @"
public partial class MyType 
{ 
    public int Id; 
}";
        
        var tree = CSharpSyntaxTree.ParseText(source);
        var type = tree.GetRoot().DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .First();
            
        var validator = new SchemaValidator();
        validator.ValidateTopicType(type, "test.cs");
        
        Assert.True(validator.HasErrors);
        Assert.Contains(validator.Diagnostics, d => d.Code == DiagnosticCode.MissingTopicAttribute);
    }
    
    [Fact]
    public void MemberRemoved_ReportsEvolutionError()
    {
        var oldSource = @"
[DdsTopic(""Test"")]
public partial class MyType 
{ 
    public int Id; 
    public string Name;
}";
        
        var newSource = @"
[DdsTopic(""Test"")]
public partial class MyType 
{ 
    public int Id; 
}";
        
        var oldTree = CSharpSyntaxTree.ParseText(oldSource);
        var newTree = CSharpSyntaxTree.ParseText(newSource);
        
        var oldType = oldTree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();
        var newType = newTree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();
        
        var oldFingerprint = SchemaFingerprint.Compute(oldType);
        var newFingerprint = SchemaFingerprint.Compute(newType);
        
        var result = SchemaFingerprint.CompareForEvolution(oldFingerprint, newFingerprint);
        
        Assert.True(result.HasBreakingChanges);
        Assert.Contains(result.Errors, e => e.Contains("removed"));
    }
    
    // ... implement remaining 13 tests ...
}
```

---

## 📊 Report Requirements

### Required Sections

1. **Executive Summary**
   - Validation capabilities implemented
   - Number of diagnostic codes defined
   - Evolution detection approach

2. **Implementation Details**
   - How fingerprint computation works
   - How validation integrates with generation
   - Error reporting format

3. **Test Results**
   - All 15+ tests passing
   - Example validation output

4. **Developer Insights**

   **Q1:** What was the most complex validation rule to implement? Why?

   **Q2:** How would you extend the fingerprinting system to handle nested types or generic types?

   **Q3:** What additional validation rules would improve schema quality?

   **Q4:** How did you handle line number extraction from Roslyn syntax trees?

5. **Code Quality Checklist**
   - [ ] All 15+ validation tests passing
   - [ ] Diagnostic system implemented
   - [ ] Schema fingerprinting working
   - [ ] Evolution detection working
   - [ ] Integration with CodeGenerator.cs complete
   - [ ] Build fails on validation errors
   - [ ] Clear error messages withfix suggestions

---

## 🎯 Success Criteria

This batch is DONE when:

1. ✅ Diagnostic system implemented (codes, severity, messages)
2. ✅ SchemaValidator validates topics and unions
3. ✅ SchemaFingerprint computes and compares schemas
4. ✅ FingerprintStore persists across builds
5. ✅ CodeGenerator.cs integrates validation
6. ✅ Build fails with exit code 1 on validation errors
7. ✅ Minimum 15 validation tests passing
8. ✅ Breaking evolution changes detected and reported
9. ✅ Report submitted

---

## ⚠️ Common Pitfalls to Avoid

1. **Don't skip evolution testing** - This is critical for production safety
2. **Don't use generic error messages** - Be specific about what's wrong and how to fix it
3. **Don't forget line numbers** - Use `node.GetLocation().GetLineSpan()` for accurate reporting
4. **Don't silently ignore validation errors** - Must fail the build
5. **Validate before generating** - Don't generate code for invalid schemas

---

## 📚 Reference Materials

- **Task Definition:** `docs/FCDC-TASK-MASTER.md` (FCDC-006)
- **Design:** `docs/FCDC-DETAILED-DESIGN.md` (§5.4 Schema Evolution Validation)
- **Previous Batch:** `.dev-workstream/batches/BATCH-02.2-INSTRUCTIONS.md`
- **Roslyn Syntax API:** https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/work-with-syntax

---

**Focus on quality. Validation is the foundation that prevents runtime errors.**
