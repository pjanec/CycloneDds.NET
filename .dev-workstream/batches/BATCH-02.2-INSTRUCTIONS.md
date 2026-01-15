# BATCH-02.2: CLI Code Generator - Corrections

**Batch Number:** BATCH-02.2 (Corrective)  
**Parent Batch:** BATCH-02.1  
**Tasks:** FCDC-005 (corrections)  
**Estimated Effort:** 4-5 hours  
**Priority:** HIGH (Corrective)

---

## 📋 Onboarding & Workflow

### Background

This is a **corrective batch** addressing issues found in BATCH-02.1 review.

**Original Batch:** `.dev-workstream/batches/BATCH-02.1-INSTRUCTIONS.md`  
**Review with Issues:** `.dev-workstream/reviews/BATCH-02.1-REVIEW.md`

Please read both before starting.

### Report Submission

**When done, create:**  
`.dev-workstream/reports/BATCH-02.2-REPORT.md`

---

## 🎯 Objectives

This batch corrects 6 critical issues from BATCH-02.1:

1. **Issue 1: No Tests for CLI Tool**
   - **Why it's a problem:** Cannot verify correctness, no regression protection
   - **What needs to change:** Create test project with minimum 5 tests

2. **Issue 2: Incomplete Union Support**
   - **Why it's a problem:** Code discovers unions but doesn't generate anything
   - **What needs to change:** Implement union generation or remove discovery

3. **Issue 3: Attribute Detection Too Loose**
   - **Why it's a problem:** False positives on types like `MyDdsTopicHelper`
   - **What needs to change:** Use exact attribute name matching

4. **Issue 4: No Error Handling**
   - **Why it's a problem:** Tool crashes on I/O errors instead of reporting them
   - **What needs to change:** Add try-catch with clear error messages

5. **Issue 5: MSBuild Always Runs**
   - **Why it's a problem:** Inefficient, regenerates on every build
   - **What needs to change:** Add incremental build optimization

6. **Issue 6: Missing Report Details**
   - **Why it's a problem:** Future developers can't understand decisions made
   - **What needs to change:** Document circular dependency resolution

---

## ✅ Tasks

### Task 1: Create Test Project for CLI Tool

**File:** `tests/CycloneDDS.CodeGen.Tests/CycloneDDS.CodeGen.Tests.csproj` (NEW)

Create a new xUnit test project:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
    <PackageReference Include="xunit" Version="2.7.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.7">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj" />
  </ItemGroup>

</Project>
```

---

### Task 2: Implement CLI Tool Tests

**File:** `tests/CycloneDDS.CodeGen.Tests/CodeGeneratorTests.cs` (NEW)

Implement minimum 5 tests that verify actual file generation:

```csharp
using System.IO;
using Xunit;
using CycloneDDS.CodeGen;

namespace CycloneDDS.CodeGen.Tests;

public class CodeGeneratorTests : IDisposable
{
    private readonly string _testDir;

    public CodeGeneratorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "CodeGenTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void DiscoversSingleTopicType()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDir, "TestType.cs");
        File.WriteAllText(sourceFile, @"
using CycloneDDS.Schema;

namespace TestNamespace;

[DdsTopic(""TestTopic"")]
public partial class TestType
{
    public int Id { get; set; }
}
");

        // Act
        var generator = new CodeGenerator();
        var filesGenerated = generator.Generate(_testDir);

        // Assert
        Assert.Equal(1, filesGenerated);
        
        var generatedFile = Path.Combine(_testDir, "Generated", "TestType.Discovery.g.cs");
        Assert.True(File.Exists(generatedFile), $"Generated file not found: {generatedFile}");
        
        var content = File.ReadAllText(generatedFile);
        Assert.Contains("namespace TestNamespace", content);
        Assert.Contains("partial class TestType", content);
        Assert.Contains("Topic: TestTopic", content);
    }

    [Fact]
    public void DiscoversMultipleTopicTypes()
    {
        // Arrange - create file with 2 topic types
        var sourceFile = Path.Combine(_testDir, "Types.cs");
        File.WriteAllText(sourceFile, @"
using CycloneDDS.Schema;

[DdsTopic(""Topic1"")]
public partial class Type1 { }

[DdsTopic(""Topic2"")]
public partial class Type2 { }
");

        // Act
        var generator = new CodeGenerator();
        var filesGenerated = generator.Generate(_testDir);

        // Assert
        Assert.Equal(2, filesGenerated);
        Assert.True(File.Exists(Path.Combine(_testDir, "Generated", "Type1.Discovery.g.cs")));
        Assert.True(File.Exists(Path.Combine(_testDir, "Generated", "Type2.Discovery.g.cs")));
    }

    [Fact]
    public void DiscoversUnionType()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDir, "MyUnion.cs");
        File.WriteAllText(sourceFile, @"
using CycloneDDS.Schema;

[DdsUnion]
public partial class MyUnion { }
");

        // Act
        var generator = new CodeGenerator();
        var filesGenerated = generator.Generate(_testDir);

        // Assert
        Assert.Equal(1, filesGenerated);
        
        var generatedFile = Path.Combine(_testDir, "Generated", "MyUnion.Discovery.g.cs");
        Assert.True(File.Exists(generatedFile));
    }

    [Fact]
    public void HandlesFileScopedNamespace()
    {
        // Arrange - file-scoped namespace (C# 10+)
        var sourceFile = Path.Combine(_testDir, "Modern.cs");
        File.WriteAllText(sourceFile, @"
using CycloneDDS.Schema;

namespace My.Namespace;

[DdsTopic(""ModernTopic"")]
public partial class ModernType { }
");

        // Act
        var generator = new CodeGenerator();
        var filesGenerated = generator.Generate(_testDir);

        // Assert
        Assert.Equal(1, filesGenerated);
        var content = File.ReadAllText(Path.Combine(_testDir, "Generated", "ModernType.Discovery.g.cs"));
        Assert.Contains("namespace My.Namespace", content);
    }

    [Fact]
    public void NoAttributesGeneratesNothing()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDir, "PlainType.cs");
        File.WriteAllText(sourceFile, @"
public class PlainType { }
");

        // Act
        var generator = new CodeGenerator();
        var filesGenerated = generator.Generate(_testDir);

        // Assert
        Assert.Equal(0, filesGenerated);
    }

    [Fact]
    public void IgnoresFalsePositiveAttributes()
    {
        // Arrange - type with similar but not exact attribute name
        var sourceFile = Path.Combine(_testDir, "FalsePositive.cs");
        File.WriteAllText(sourceFile, @"
// This should NOT be discovered (DdsTopicHelper != DdsTopic)
[DdsTopicHelper]
public partial class FalsePositive { }

public class DdsTopicHelperAttribute : System.Attribute { }
");

        // Act
        var generator = new CodeGenerator();
        var filesGenerated = generator.Generate(_testDir);

        // Assert - should generate 0 files (after fix to attribute matching)
        Assert.Equal(0, filesGenerated);
    }
}
```

**Test Quality Requirements:**
- Each test must create actual files on disk and verify content
- Tests must clean up after themselves (IDisposable pattern)
- Tests must verify namespace extraction works for both styles
- Tests must verify exact attribute matching (no false positives)

---

### Task 3: Fix Attribute Detection

**File:** `tools/CycloneDDS.CodeGen/CodeGenerator.cs` (MODIFY)

**Current Code (Lines 54-65):**
```csharp
private bool HasDdsTopicAttribute(TypeDeclarationSyntax type)
{
    return type.AttributeLists
        .SelectMany(al => al.Attributes)
        .Any(attr => attr.Name.ToString().Contains("DdsTopic"));
}

private bool HasDdsUnionAttribute(TypeDeclarationSyntax type)
{
    return type.AttributeLists
        .SelectMany(al => al.Attributes)
        .Any(attr => attr.Name.ToString().Contains("DdsUnion"));
}
```

**Required Change:**
```csharp
private bool HasDdsTopicAttribute(TypeDeclarationSyntax type)
{
    return type.AttributeLists
        .SelectMany(al => al.Attributes)
        .Any(attr => 
        {
            var name = attr.Name.ToString();
            return name is "DdsTopic" or "DdsTopicAttribute";
        });
}

private bool HasDdsUnionAttribute(TypeDeclarationSyntax type)
{
    return type.AttributeLists
        .SelectMany(al => al.Attributes)
        .Any(attr => 
        {
            var name = attr.Name.ToString();
            return name is "DdsUnion" or "DdsUnionAttribute";
        });
}
```

**Why This Matters:** Prevents false positives on types like `MyDdsTopicHelper`, `DdsTopicFactory`, etc.

---

### Task 4: Implement Union Generation

**File:** `tools/CycloneDDS.CodeGen/CodeGenerator.cs` (MODIFY)

**Current Code (Lines 104-108):**
```csharp
private int GenerateForUnions(string sourceFile, List<TypeDeclarationSyntax> types)
{
    // Similar to GenerateForTopics, but for unions
    // For now, just placeholder
    return 0;
}
```

**Required Implementation:**
```csharp
private int GenerateForUnions(string sourceFile, List<TypeDeclarationSyntax> types)
{
    int count = 0;
    var sourceDir = Path.GetDirectoryName(sourceFile)!;
    var generatedDir = Path.Combine(sourceDir, "Generated");
    Directory.CreateDirectory(generatedDir);

    foreach (var type in types)
    {
        var typeName = type.Identifier.Text;
        var namespaceName = GetNamespace(type);

        var generatedCode = $@"// <auto-generated/>
// Generated from: {Path.GetFileName(sourceFile)}
// Union type

namespace {namespaceName}
{{
    partial class {typeName}
    {{
        // FCDC-005: Union discovery placeholder (CLI tool)
        // TODO: Generate union discriminator, case handling in FCDC-027
    }}
}}
";

        var outputFile = Path.Combine(generatedDir, $"{typeName}.Discovery.g.cs");
        File.WriteAllText(outputFile, generatedCode);
        Console.WriteLine($"[CodeGen]   Generated: {outputFile}");
        count++;
    }

    return count;
}
```

---

### Task 5: Add Error Handling

**File:** `tools/CycloneDDS.CodeGen/CodeGenerator.cs` (MODIFY)

Add try-catch blocks around file operations:

**In `Generate` method (around line 22-48):**
```csharp
foreach (var file in csFiles)
{
    try
    {
        var code = File.ReadAllText(file);
        var tree = CSharpSyntaxTree.ParseText(code, path: file);
        var root = tree.GetRoot();

        // ... existing discovery logic ...
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"[CodeGen] ERROR: Failed to read {file}: {ex.Message}");
        // Continue processing other files
        continue;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[CodeGen] ERROR: Unexpected error processing {file}: {ex.Message}");
        throw;
    }
}
```

**In `GenerateForTopics` and `GenerateForUnions` (around file write):**
```csharp
try
{
    var outputFile = Path.Combine(generatedDir, $"{typeName}.Discovery.g.cs");
    File.WriteAllText(outputFile, generatedCode);
    Console.WriteLine($"[CodeGen]   Generated: {outputFile}");
    count++;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"[CodeGen] ERROR: Failed to write {outputFile}: {ex.Message}");
    // Continue with other types
}
```

**Why This Matters:** Tool should gracefully handle I/O errors (readonly folders, locked files, permission issues) instead of crashing.

---

### Task 6: Add Incremental Build Support

**File:** `src/CycloneDDS.Schema/CycloneDDS.Schema.csproj` (MODIFY)

**Current Code (Lines 20-22):**
```xml
<Target Name="RunCodeGeneration" BeforeTargets="BeforeBuild">
  <Exec Command="dotnet run --project $(MSBuildThisFileDirectory)..\..\tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj -- $(MSBuildProjectDirectory)" />
</Target>
```

**Option 1: Make it Opt-In (Recommended for Now):**
```xml
<Target Name="RunCodeGeneration" BeforeTargets="BeforeBuild" Condition="'$(RunCodeGen)' == 'true'">
  <Exec Command="dotnet run --project $(MSBuildThisFileDirectory)..\..\tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj -- $(MSBuildProjectDirectory)" />
</Target>
```

Then developers can run: `dotnet build /p:RunCodeGen=true` when they change schema types.

**Option 2: Add Inputs/Outputs for Incremental Check:**
```xml
<Target Name="RunCodeGeneration" 
        BeforeTargets="BeforeBuild"
        Inputs="@(Compile)"
        Outputs="Generated\*.g.cs">
  <Exec Command="dotnet run --project $(MSBuildThisFileDirectory)..\..\tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj -- $(MSBuildProjectDirectory)" />
</Target>
```

**Choose Option 1 for this batch** (simpler, more explicit control).

**Add documentation comment:**
```xml
<!-- Code Generation: Run with /p:RunCodeGen=true to regenerate DDS bindings -->
<Target Name="RunCodeGeneration" BeforeTargets="BeforeBuild" Condition="'$(RunCodeGen)' == 'true'">
  <Exec Command="dotnet run --project $(MSBuildThisFileDirectory)..\..\tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj -- $(MSBuildProjectDirectory)" />
</Target>
```

---

### Task 7: Update Documentation

**File:** `tools/CycloneDDS.CodeGen/README.md` (NEW)

Create a README documenting the CLI tool:

```markdown
# CycloneDDS.CodeGen - CLI Code Generator

## Purpose

Standalone CLI tool that discovers DDS schema types (marked with `[DdsTopic]`, `[DdsUnion]`, etc.) and generates supporting code.

## Architecture Decision: Why CLI Tool?

**Original Approach:** Roslyn `IIncrementalGenerator` (BATCH-02)  
**Problem:** Complex caching behavior, difficult to debug, regeneration issues  
**Solution:** CLI tool with explicit file I/O

## Circular Dependency Resolution

**Issue:** The tool needs to reference schema attributes (`[DdsTopic]`) from `CycloneDDS.Schema`, but `CycloneDDS.Schema` needs to build *before* the tool runs.

**Solution:** 
1. Tool uses **syntax-only analysis** (no semantic model required)
2. Attribute names matched as strings (`"DdsTopic"`) 
3. No `ProjectReference` to `CycloneDDS.Schema` in tool project
4. Enums copied locally to `Models/Enums.cs` to avoid dependency

This allows the tool to run before `CycloneDDS.Schema` compiles.

## Usage

**Manual Run:**
```bash
dotnet run --project tools/CycloneDDS.CodeGen/CycloneDDS.CodeGen.csproj -- src/CycloneDDS.Schema
```

**MSBuild Integration:**
```bash
dotnet build src/CycloneDDS.Schema /p:RunCodeGen=true
```

## Generated Files

- `Generated/{TypeName}.Discovery.g.cs` - Placeholder partial class
- Future batches will extend this to generate native types, marshallers, etc.

## Testing

Run tests:
```bash
dotnet test tests/CycloneDDS.CodeGen.Tests
```

## Future Work

- FCDC-006: Add semantic validation (duplicate topic names, invalid types)
- FCDC-007+: Generate IDL, native types, marshallers
```

---

## 🧪 Testing Requirements

**All Tests Must Pass:**
```bash
dotnet test tests/CycloneDDS.CodeGen.Tests
```

**Minimum 6 tests required:**
1. ✅ Single topic discovery
2. ✅ Multiple topics discovery
3. ✅ Union discovery
4. ✅ File-scoped namespace handling
5. ✅ No-op for plain types
6. ✅ False positive rejection (exact attribute matching)

**Quality Standard:**
- Tests must verify actual file generation (not in-memory)
- Tests must verify file content correctness
- Tests must clean up after themselves
- Tests must be deterministic (no flaky tests)

---

## 📊 Report Requirements

Use template: `.dev-workstream/templates/BATCH-REPORT-TEMPLATE.md`

### Required Sections

1. **Executive Summary**
   - What issues were fixed
   - Current test coverage

2. **Implementation Details**
   - How circular dependency was resolved (with examples)
   - Why opt-in MSBuild target was chosen
   - Error handling strategy

3. **Test Results**
   - All 6+ tests passing
   - Screenshot or output of `dotnet test`

4. **Developer Insights**

   **Q1:** What was the most challenging issue to fix? Why?

   **Q2:** How would you improve the incremental build mechanism in the future?

   **Q3:** What edge cases did you discover during testing that weren't in the spec?

5. **Code Quality Checklist**
   - [ ] All 6 tests passing
   - [ ] Attribute matching uses exact match
   - [ ] Union generation implemented
   - [ ] Error handling added
   - [ ] MSBuild target made opt-in
   - [ ] README.md created

---

## 🎯 Success Criteria

This batch is DONE when:

1. ✅ Test project created (`tests/CycloneDDS.CodeGen.Tests`)
2. ✅ Minimum 6 tests implemented and **all passing**
3. ✅ Attribute matching fixed (exact match, no false positives)
4. ✅ Union generation implemented
5. ✅ Error handling added (try-catch on I/O)
6. ✅ MSBuild target made opt-in with documentation
7. ✅ README.md created documenting architecture decisions
8. ✅ Report submitted with test results
9. ✅ All BATCH-02.1 tests still pass (no regressions)

---

## ⚠️ Common Pitfalls to Avoid

1. **Don't skip error handling tests** - Create read-only folder test
2. **Don't use `Contains` for attribute matching** - Must be exact match
3. **Don't forget to test file-scoped namespaces** - Modern C# syntax
4. **Don't leave union generation as stub** - Must actually generate file
5. **Ensure test cleanup** - Use `IDisposable` to delete temp directories

---

## 📚 Reference Materials

- **Review:** `.dev-workstream/reviews/BATCH-02.1-REVIEW.md`
- **Original Batch:** `.dev-workstream/batches/BATCH-02.1-INSTRUCTIONS.md`
- **xUnit Docs:** https://xunit.net/
- **Roslyn Syntax API:** https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/syntax-analysis

---

**This is a corrective batch. Focus on quality over speed. Comprehensive tests are mandatory.**
