# BATCH-02.1: Switch to CLI Code Generation Tool

**Batch Number:** BATCH-02.1 (Corrective - Architecture Change)  
**Parent Batch:** BATCH-02  
**Tasks:** FCDC-005 (revised approach)  
**Estimated Effort:** 3-4 hours  
**Priority:** HIGH (Unblocking)  
**Dependencies:** BATCH-02 (partial work can be reused)

---

## 📋 Onboarding & Workflow

### Background

**Problem:** Roslyn IIncrementalGenerator caching is complex and causing regeneration on every build despite no changes.

**Solution:** Switch to **CLI Tool approach** - a standard console app that reads .cs files, finds attributes, and writes generated .cs files to disk.

**Original Batch:** `.dev-workstream/batches/BATCH-02-INSTRUCTIONS.md`  
**This replaces:** The IIncrementalGenerator approach with a simpler, more debuggable CLI tool.

### Report Submission

**When done, create:**  
`.dev-workstream/reports/BATCH-02.1-REPORT.md`

---

## 🎯 Objectives

1. Create CLI console app for code generation
2. Reuse existing discovery logic from BATCH-02 (models, diagnostics)
3. Parse .cs files using Roslyn syntax trees
4. Find types with [DdsTopic], [DdsUnion], [DdsTypeMap]
5. Generate placeholder .Discovery.g.cs files to disk
6. Integrate into MSBuild (runs before compilation)
7. Test that it only regenerates when source files change

---

## ✅ Tasks

### Task 1: Create CLI Tool Project

**File:** `tools/CycloneDDS.CodeGen/CycloneDDS.CodeGen.csproj` (NEW)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\CycloneDDS.Schema\CycloneDDS.Schema.csproj" />
  </ItemGroup>
</Project>
```

**Note:** This is a regular console app (net8.0), NOT netstandard2.0.

---

### Task 2: Implement Main Entry Point

**File:** `tools/CycloneDDS.CodeGen/Program.cs` (NEW)

```csharp
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.IO;
using System.Linq;

namespace CycloneDDS.CodeGen;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: CycloneDDS.CodeGen <source-directory>");
            return 1;
        }

        var sourceDir = args[0];
        if (!Directory.Exists(sourceDir))
        {
            Console.Error.WriteLine($"Directory not found: {sourceDir}");
            return 1;
        }

        Console.WriteLine($"[CodeGen] Scanning: {sourceDir}");

        var generator = new CodeGenerator();
        var filesGenerated = generator.Generate(sourceDir);

        Console.WriteLine($"[CodeGen] Generated {filesGenerated} files");
        return 0;
    }
}
```

---

### Task 3: Implement Code Generator

**File:** `tools/CycloneDDS.CodeGen/CodeGenerator.cs` (NEW)

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CycloneDDS.CodeGen;

public class CodeGenerator
{
    public int Generate(string sourceDirectory)
    {
        int filesGenerated = 0;
        
        // Find all .cs files (exclude Generated/ folder)
        var csFiles = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("Generated") && !f.Contains("obj") && !f.Contains("bin"))
            .ToList();

        foreach (var file in csFiles)
        {
            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code, path: file);
            var root = tree.GetRoot();

            // Find types with [DdsTopic]
            var topicTypes = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Where(HasDdsTopicAttribute)
                .ToList();

            if (topicTypes.Any())
            {
                filesGenerated += GenerateForTopics(file, topicTypes);
            }

            // Find types with [DdsUnion]
            var unionTypes = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Where(HasDdsUnionAttribute)
                .ToList();

            if (unionTypes.Any())
            {
                filesGenerated += GenerateForUnions(file, unionTypes);
            }
        }

        return filesGenerated;
    }

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

    private int GenerateForTopics(string sourceFile, List<TypeDeclarationSyntax> types)
    {
        int count = 0;
        var sourceDir = Path.GetDirectoryName(sourceFile)!;
        var generatedDir = Path.Combine(sourceDir, "Generated");
        Directory.CreateDirectory(generatedDir);

        foreach (var type in types)
        {
            var typeName = type.Identifier.Text;
            var namespaceName = GetNamespace(type);
            var topicName = ExtractTopicName(type);

            var generatedCode = $@"// <auto-generated/>
// Generated from: {Path.GetFileName(sourceFile)}
// Topic: {topicName}

namespace {namespaceName}
{{
    partial class {typeName}
    {{
        // FCDC-005: Discovery placeholder (CLI tool)
        // TODO: Generate native types, managed views, marshallers in FCDC-009+
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

    private int GenerateForUnions(string sourceFile, List<TypeDeclarationSyntax> types)
    {
        // Similar to GenerateForTopics, but for unions
        // For now, just placeholder
        return 0;
    }

    private string GetNamespace(TypeDeclarationSyntax type)
    {
        var namespaceDecl = type.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceDecl != null)
            return namespaceDecl.Name.ToString();

        var fileScopedNs = type.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        if (fileScopedNs != null)
            return fileScopedNs.Name.ToString();

        return "Global";
    }

    private string ExtractTopicName(TypeDeclarationSyntax type)
    {
        var topicAttr = type.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString().Contains("DdsTopic"));

        if (topicAttr?.ArgumentList?.Arguments.Count > 0)
        {
            var arg = topicAttr.ArgumentList.Arguments[0];
            return arg.Expression.ToString().Trim('"');
        }

        return type.Identifier.Text;
    }
}
```

---

### Task 4: Integrate into MSBuild

**File:** `src/CycloneDDS.Schema/CycloneDDS.Schema.csproj` (MODIFY)

Add this target to run code generation before build:

```xml
<Target Name="RunCodeGeneration" BeforeTargets="BeforeBuild">
  <Exec Command="dotnet run --project $(MSBuildThisFileDirectory)..\..\tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj -- $(MSBuildProjectDirectory)" />
</Target>
```

**Alternative (if you want it opt-in):**
```xml
<Target Name="RunCodeGeneration" BeforeTargets="BeforeBuild" Condition="'$(RunCodeGen)' == 'true'">
  <Exec Command="dotnet run --project $(MSBuildThisFileDirectory)..\..\tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj -- $(MSBuildProjectDirectory)" />
</Target>
```

Then run with: `dotnet build /p:RunCodeGen=true`

---

### Task 5: Reuse Models from BATCH-02

**Action:** Move (or copy) the model classes you created in BATCH-02 to the CLI tool:

**From:** `src/CycloneDDS.Generator/Models/*.cs`  
**To:** `tools/CycloneDDS.CodeGen/Models/*.cs`

These models (SchemaTopicType, SchemaUnionType, etc.) can be reused for semantic analysis in future batches.

**For now:** The CLI tool uses syntax-only analysis (no semantic model), so models are optional.

---

## 🧪 Testing Requirements

**Manual Testing (for now):**

1. Create a test .cs file with [DdsTopic]:
```csharp
using CycloneDDS.Schema;

namespace TestNamespace;

[DdsTopic("TestTopic")]
public partial class TestType
{
    public int Id { get; set; }
}
```

2. Run the tool:
```bash
dotnet run --project tools/CycloneDDS.CodeGen/CycloneDDS.CodeGen.csproj -- src/CycloneDDS.Schema
```

3. Verify:
   - `Generated/TestType.Discovery.g.cs` created
   - Contains correct namespace and partial class
   - Running again doesn't regenerate if no changes (file timestamp check)

**Integration Test (optional):**
- Create a test project that references the tool
- Add MSBuild target
- Verify `dotnet build` runs tool and generates files

---

## 📊 Report Requirements

### Required Sections

1. **Executive Summary**
   - Why we switched from Roslyn generator to CLI tool

2. **Implementation Summary**
   - Files created
   - How discovery works (syntax tree walking)
   - MSBuild integration approach

3. **Developer Insights**

   **Q1:** What were the specific issues with the Roslyn IIncrementalGenerator that led to this change?

   **Q2:** How does the CLI tool approach compare in terms of complexity and debuggability?

   **Q3:** What are the trade-offs of this approach vs. Roslyn generators?

   **Q4:** How would you handle incremental builds (only regenerate changed files)?

   **Q5:** What challenges remain for future batches (validation, full code generation)?

4. **Build Instructions**
   - How to run the tool manually
   - How to integrate into build

5. **Code Quality Checklist**

---

## 🎯 Success Criteria

- [ ] CLI tool project created and compiles
- [ ] Tool discovers [DdsTopic] types via syntax analysis
- [ ] Generates .Discovery.g.cs files to Generated/ folder
- [ ] MSBuild integration works (runs before build)
- [ ] Manual testing confirms generation works
- [ ] Report documents rationale and trade-offs

---

## ⚠️ Key Differences from BATCH-02

| Aspect | Roslyn Generator (BATCH-02) | CLI Tool (BATCH-02.1) |
|--------|----------------------------|----------------------|
| **Target Framework** | netstandard2.0 | net8.0 |
| **Runs** | Inside compiler | Before build (MSBuild) |
| **Output** | In-memory (virtual files) | Physical .cs files on disk |
| **Debugging** | Complex (attach to compiler) | Simple (F5 in VS) |
| **Caching** | Incremental generator API | File timestamps |
| **IntelliSense** | Live updates | After build only |

---

## 📚 Reference Materials

- **Original Batch:** `.dev-workstream/batches/BATCH-02-INSTRUCTIONS.md`
- **Roslyn Syntax API:** [Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/syntax-analysis)
- **MSBuild Targets:** [Microsoft Docs](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-targets)

---

## 🔄 Next Steps

After approval:
1. Archive BATCH-02 work (keep models for reuse)
2. BATCH-03: Schema Validation Logic (FCDC-006) - will use CLI tool
3. Future batches will extend the CLI tool, not Roslyn generator

**This is a pragmatic pivot. Simpler is better.** 🚀
