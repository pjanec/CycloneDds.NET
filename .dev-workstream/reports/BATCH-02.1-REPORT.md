# BATCH-02.1 Report: Switch to CLI Code Generation Tool

## 1. Executive Summary
We have successfully replaced the Roslyn `IIncrementalGenerator` with a standalone CLI tool (`CycloneDDS.CodeGen`) for code generation. This change addresses the complexity and caching issues associated with the Roslyn generator, providing a more predictable and debuggable workflow. The CLI tool is integrated into the MSBuild process and runs before compilation.

## 2. Implementation Summary
- **New Project:** `tools/CycloneDDS.CodeGen/CycloneDDS.CodeGen.csproj` (NET 8.0 Console App).
- **Generator Logic:** `CodeGenerator.cs` uses `Microsoft.CodeAnalysis.CSharp` to parse syntax trees and identify `[DdsTopic]` and `[DdsUnion]` attributes.
- **Models:** Ported existing models (e.g., `SchemaTopicType`, `SchemaField`) to `tools/CycloneDDS.CodeGen/Models` for future semantic analysis.
- **MSBuild Integration:** Added a `RunCodeGeneration` target to `CycloneDDS.Schema.csproj` that executes the CLI tool before the build.
- **Dependency Management:** Decoupled the CLI tool from `CycloneDDS.Schema` by defining necessary enums locally to avoid circular dependencies during the build.

## 3. Developer Insights

**Q1: What were the specific issues with the Roslyn IIncrementalGenerator that led to this change?**
The `IIncrementalGenerator` was complex to debug and often triggered unnecessary regenerations or failed to update when expected due to caching behaviors. It also ran inside the compiler process, making it hard to inspect the state.

**Q2: How does the CLI tool approach compare in terms of complexity and debuggability?**
The CLI tool is significantly simpler. It's a standard console application that can be run and debugged independently (F5 in VS). We have full control over when it runs and what it outputs.

**Q3: What are the trade-offs of this approach vs. Roslyn generators?**
- **Pros:** Simplicity, debuggability, explicit control, physical files on disk (easier to inspect).
- **Cons:** No live IntelliSense updates (requires a build to see generated types), slightly slower build time (process startup), need to manage file I/O manually.

**Q4: How would you handle incremental builds (only regenerate changed files)?**
Currently, the tool overwrites files. To support incremental builds, we can:
1. Check file timestamps of source vs. generated files.
2. Calculate a hash of the input file/content and compare it with a stored hash in the generated file header.
3. Only write the file if the content has actually changed.

**Q5: What challenges remain for future batches (validation, full code generation)?**
- **Validation:** We need to implement semantic checks (e.g., duplicate topic names) which might require more than just syntax analysis (symbol resolution).
- **Type Resolution:** If we need to resolve types across assemblies, we might need to load the project/solution, which adds complexity. For now, syntax analysis is sufficient for local types.

## 4. Build Instructions
The code generation is automatically triggered when building `CycloneDDS.Schema`.

**Manual Run:**
```bash
dotnet run --project tools/CycloneDDS.CodeGen/CycloneDDS.CodeGen.csproj -- src/CycloneDDS.Schema
```

**Integration:**
The `CycloneDDS.Schema.csproj` contains:
```xml
<Target Name="RunCodeGeneration" BeforeTargets="BeforeBuild">
    <Exec Command="dotnet run --project ... -- $(MSBuildProjectDirectory)" />
</Target>
```

## 5. Code Quality Checklist
- [x] CLI tool project created and compiles
- [x] Tool discovers [DdsTopic] types via syntax analysis
- [x] Generates .Discovery.g.cs files to Generated/ folder
- [x] MSBuild integration works (runs before build)
- [x] Circular dependencies resolved
