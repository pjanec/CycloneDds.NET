# Task FCDC-005: Generator Infrastructure

**ID:** FCDC-005  
**Title:** Roslyn Source Generator Infrastructure  
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Phase:** 2 - Roslyn Source Generator  
**Estimated Effort:** 3-5 days  
**Dependencies:** FCDC-001 (Schema Attributes), FCDC-003 (Global Type Map Registry)  

---

## Overview

Set up the foundational Roslyn IIncrementalGenerator infrastructure for the FastCycloneDDS source generator. This includes discovery of annotated schema types, collection of global type mappings, diagnostic reporting, and the basic framework for the multi-phase code generation pipeline.

**Design Reference:** [Detailed Design §5.1 Roslyn Source Generator Flow](../docs/FCDC-DETAILED-DESIGN.md#51-roslyn-source-generator-flow), [§3.1 Component Design - Deliverables](../docs/FCDC-DETAILED-DESIGN.md#31-deliverables)

---

## Objectives

1. Create `CycloneDDS.Generator` project (class library targeting netstandard2.0)
2. Implement IIncrementalGenerator with proper pipeline setup
3. Implement schema type discovery (find types with [DdsTopic])
4. Implement union type discovery (find types with [DdsUnion])
5. Implement global type map discovery (assembly-level [DdsTypeMap] attributes)
6. Establish diagnostic reporting system with error codes
7. Set up source generation context and output infrastructure
8. Create internal models for discovered schema types

---

## Acceptance Criteria

- [ ] Generator project targets netstandard2.0 (Roslyn requirement)
- [ ] References correct NuGet packages (Microsoft.CodeAnalysis.CSharp, Microsoft.CodeAnalysis.Analyzers)
- [ ] Implements IIncrementalGenerator interface
- [ ] ForAttributeWithMetadataName pipeline finds [DdsTopic] types correctly
- [ ] ForAttributeWithMetadataName pipeline finds [DdsUnion] types correctly
- [ ] Assembly attribute provider collects [DdsTypeMap] attributes
- [ ] Diagnostic descriptors defined with unique IDs (FCDC0001-FCDC9999 range)
- [ ] Schema types are collected into internal model objects (SchemaTopicType, SchemaUnionType)
- [ ] Generator handles multiple schema types in a single compilation
- [ ] Generator handles no schema types gracefully (no-op)
- [ ] Output is deterministic (same input → same output)
- [ ] Generator emits placeholder comment for discovered types (proves discovery works)
- [ ] Incremental generation caching is correct (unchanged inputs → no recomputation)

---

## Implementation Details

### Project File (CycloneDDS.Generator.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <IsRoslynComponent>true</IsRoslynComponent>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.4" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\CycloneDDS.Schema\CycloneDDS.Schema.csproj" />
  </ItemGroup>
</Project>
```

### File Structure

```
src/CycloneDDS.Generator/
├── CycloneDDS.Generator.csproj
├── FcdcGenerator.cs               # Main IIncrementalGenerator
├── Discovery/
│   ├── TopicTypeDiscovery.cs      # Pipeline for [DdsTopic] types
│   ├── UnionTypeDiscovery.cs      # Pipeline for [DdsUnion] types
│   └── TypeMapDiscovery.cs        # Pipeline for [DdsTypeMap] assembly attributes
├── Models/
│   ├── SchemaTopicType.cs         # Internal model for discovered topic types
│   ├── SchemaUnionType.cs         # Internal model for discovered union types
│   ├── SchemaField.cs             # Internal model for fields
│   └── GlobalTypeMapping.cs       # Internal model for type mappings
├── Diagnostics/
│   ├── DiagnosticDescriptors.cs   # All diagnostic definitions
│   └── DiagnosticIds.cs           # Constant diagnostic IDs
└── Utilities/
    ├── SymbolExtensions.cs        # Extension methods for ISymbol helpers
    └── GeneratorContext.cs        # Shared context for generation state
```

### Main Generator Implementation (FcdcGenerator.cs)

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bagira.CycloneDDS.Generator
{
    [Generator]
    public class FcdcGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Register post-initialization output (e.g., shared utility files)
            context.RegisterPostInitializationOutput(ctx =>
            {
                // Can add common generated code here if needed
            });

            // Pipeline 1: Discover [DdsTopic] types
            var topicTypes = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: "Bagira.CycloneDDS.Schema.DdsTopicAttribute",
                    predicate: static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                    transform: static (ctx, _) => TransformTopicType(ctx))
                .Where(static type => type is not null);

            // Pipeline 2: Discover [DdsUnion] types
            var unionTypes = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: "Bagira.CycloneDDS.Schema.DdsUnionAttribute",
                    predicate: static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                    transform: static (ctx, _) => TransformUnionType(ctx))
                .Where(static type => type is not null);

            // Pipeline 3: Discover [DdsTypeMap] assembly attributes
            var typeMappings = context.CompilationProvider
                .Select(static (compilation, _) => CollectTypeMappings(compilation));

            // Combine all discovery results
            var allInput = topicTypes
                .Collect()
                .Combine(unionTypes.Collect())
                .Combine(typeMappings);

            // Register main generation phase
            context.RegisterSourceOutput(allInput, static (spc, source) =>
            {
                var (topicAndUnion, globalMappings) = source;
                var (topics, unions) = topicAndUnion;

                GenerateCode(spc, topics, unions, globalMappings);
            });
        }

        private static SchemaTopicType? TransformTopicType(GeneratorAttributeSyntaxContext context)
        {
            var symbol = context.TargetSymbol as INamedTypeSymbol;
            if (symbol is null)
                return null;

            // Extract topic name from [DdsTopic("...")] attribute
            var topicAttr = context.Attributes[0]; // ForAttributeWithMetadataName guarantees this exists
            if (topicAttr.ConstructorArguments.Length == 0)
                return null;

            var topicName = topicAttr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrWhiteSpace(topicName))
                return null;

            // Create internal model
            return new SchemaTopicType
            {
                Symbol = symbol,
                TopicName = topicName,
                // TODO: Extract QoS, key fields, etc.
            };
        }

        private static SchemaUnionType? TransformUnionType(GeneratorAttributeSyntaxContext context)
        {
            var symbol = context.TargetSymbol as INamedTypeSymbol;
            if (symbol is null)
                return null;

            // Validate union has discriminator and arms
            // TODO: Full validation in FCDC-006

            return new SchemaUnionType
            {
                Symbol = symbol,
                // TODO: Extract discriminator and arms
            };
        }

        private static ImmutableArray<GlobalTypeMapping> CollectTypeMappings(Compilation compilation)
        {
            var builder = ImmutableArray.CreateBuilder<GlobalTypeMapping>();

            foreach (var attr in compilation.Assembly.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() != "Bagira.CycloneDDS.Schema.DdsTypeMapAttribute")
                    continue;

                // Extract source type and wire kind
                // TODO: Parse DdsWire enum value

                builder.Add(new GlobalTypeMapping
                {
                    // ...
                });
            }

            return builder.ToImmutable();
        }

        private static void GenerateCode(
            SourceProductionContext context,
            ImmutableArray<SchemaTopicType> topics,
            ImmutableArray<SchemaUnionType> unions,
            ImmutableArray<GlobalTypeMapping> typeMappings)
        {
            // For now, just emit a placeholder to prove discovery works
            foreach (var topic in topics)
            {
                var code = $@"
// Auto-generated for topic: {topic.TopicName}
// Type: {topic.Symbol.ToDisplayString()}

namespace {topic.Symbol.ContainingNamespace.ToDisplayString()}
{{
    // TODO: Generate native types, managed views, marshallers
    partial class {topic.Symbol.Name}
    {{
        // Placeholder: FCDC-005 discovery test
    }}
}}
";
                context.AddSource($"{topic.Symbol.Name}.Discovery.g.cs", code);
            }

            // Report diagnostic for each discovered type
            foreach (var topic in topics)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TopicDiscovered,
                    Location.None,
                    topic.TopicName,
                    topic.Symbol.ToDisplayString()));
            }
        }
    }
}
```

### Internal Models (Models/SchemaTopicType.cs)

```csharp
using Microsoft.CodeAnalysis;

namespace Bagira.CycloneDDS.Generator.Models
{
    internal sealed class SchemaTopicType
    {
        public required INamedTypeSymbol Symbol { get; init; }
        public required string TopicName { get; init; }

        // Will be populated in FCDC-006 validation phase
        public DdsQosSettings? Qos { get; set; }
        public ImmutableArray<SchemaField> Fields { get; set; }
        public ImmutableArray<int> KeyFieldIndices { get; set; }
        public string? TypeName { get; set; } // Override via [DdsTypeName]
    }

    internal sealed class DdsQosSettings
    {
        public DdsReliability Reliability { get; set; }
        public DdsDurability Durability { get; set; }
        public DdsHistoryKind HistoryKind { get; set; }
        public int HistoryDepth { get; set; }
    }

    internal sealed class SchemaField
    {
        public required IFieldSymbol Symbol { get; init; }
        public required string Name { get; init; }
        public required ITypeSymbol Type { get; init; }
        public bool IsKey { get; set; }
        public bool IsOptional { get; set; }
        public int? Bound { get; set; }
        public int? Id { get; set; } // Explicit member ID
    }
}
```

### Diagnostics (Diagnostics/DiagnosticDescriptors.cs)

```csharp
using Microsoft.CodeAnalysis;

namespace Bagira.CycloneDDS.Generator.Diagnostics
{
    internal static class DiagnosticDescriptors
    {
        private const string Category = "CycloneDDS.Generator";

        public static readonly DiagnosticDescriptor TopicDiscovered = new(
            id: Diagnostic Ids.TopicDiscovered,
            title: "DDS Topic Discovered",
            messageFormat: "Discovered topic '{0}' for type '{1}'",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor TopicNameMissing = new(
            id: DiagnosticIds.TopicNameMissing,
            title: "Topic Name Missing",
            messageFormat: "Type '{0}' has [DdsTopic] but topic name is null or empty",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        // More diagnostics will be added in FCDC-006
    }

    internal static class DiagnosticIds
    {
        public const string TopicDiscovered = "FCDC0001";
        public const string TopicNameMissing = "FCDC0002";
        // Reserve FCDC0001-FCDC0999 for discovery/infrastructure
        // Reserve FCDC1000-FCDC1999 for validation errors
        // Reserve FCDC2000-FCDC2999 for generation errors
    }
}
```

---

## Testing Requirements

### Integration Tests (FCDC-005-Tests project)

1. **Discovery Tests**
   - Generate test schema with [DdsTopic] and verify topic is discovered
   - Generate test schema with [DdsUnion] and verify union is discovered
   - Generate multiple schemas and verify all are discovered
   - Generate schema with no attributes and verify no-op behavior

2. **Incremental Generation Tests**
   - Verify changing unrelated file doesn't cause regeneration (cached)
   - Verify changing schema file causes regeneration
   - Verify adding new schema type triggers generation for that type only

3. **Diagnostic Tests**
   - Verify TopicDiscovered diagnostic is emitted for each topic
   - Verify TopicNameMissing diagnostic is emitted for invalid topic name

4. **Edge Cases**
   - Empty compilation (no types)
   - Compilation with syntax errors (generator should not crash)
   - Schema types in different namespaces
   - Nested types (not supported, should warn/error in FCDC-006)

### Test Infrastructure

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Bagira.CycloneDDS.Generator.Tests
{
    public class DiscoveryTests
    {
        [Fact]
        public void DiscoversSingleTopicType()
        {
            var source = @"
using Bagira.CycloneDDS.Schema;

[DdsTopic(""TestTopic"")]
public partial class TestType
{
}
";
            var (compilation, diagnostics) = RunGenerator(source);

            Assert.Contains(diagnostics, d => d.Id == "FCDC0001" && d.GetMessage().Contains("TestTopic"));
            Assert.Single(compilation.SyntaxTrees.Where(t => t.FilePath.EndsWith(".Discovery.g.cs")));
        }

        private static (Compilation compilation, ImmutableArray<Diagnostic> diagnostics) RunGenerator(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Concat(new[] {
                    MetadataReference.CreateFromFile(typeof(DdsTopicAttribute).Assembly.Location)
                });

            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new FcdcGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            return (outputCompilation, diagnostics);
        }
    }
}
```

---

## Documentation Requirements

- [ ] XML documentation on FcdcGenerator class
- [ ] README.md explaining generator architecture
- [ ] Diagnostic ID reference document (list all IDs with descriptions)

---

## Definition of Done

- All acceptance criteria met
- Integration tests pass
- Generator can be consumed by test project via project reference
- Discovery pipelines correctly identify all annotated types
- Diagnostics are reported correctly
- Incremental generation is verified to work
- Code review approved
