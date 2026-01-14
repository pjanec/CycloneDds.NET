using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using CycloneDDS.Generator.Models;
using CycloneDDS.Generator.Diagnostics;
using CycloneDDS.Generator.Utilities;
using CycloneDDS.Schema;

namespace CycloneDDS.Generator
{
    [Generator]
    public class FcdcGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Pipeline 1: Discover [DdsTopic] types
            var topicTypes = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: "CycloneDDS.Schema.DdsTopicAttribute",
                    predicate: static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                    transform: static (ctx, _) => TransformTopicType(ctx))
                .Where(static type => type is not null);

            // Pipeline 2: Discover [DdsUnion] types
            var unionTypes = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: "CycloneDDS.Schema.DdsUnionAttribute",
                    predicate: static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                    transform: static (ctx, _) => TransformUnionType(ctx))
                .Where(static type => type is not null);

            // Pipeline 3: Discover [DdsTypeMap] assembly attributes
            var typeMappings = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: "CycloneDDS.Schema.DdsTypeMapAttribute",
                    predicate: static (node, _) => true, 
                    transform: static (ctx, _) => TransformTypeMap(ctx))
                .Where(static map => map is not null)
                .Collect(); 
                
            var allInput = topicTypes.Collect()
                .Combine(unionTypes.Collect())
                .Combine(typeMappings);

            context.RegisterSourceOutput(allInput, static (spc, source) =>
            {
                var (topicAndUnion, globalMappings) = source;
                var (topics, unions) = topicAndUnion;

                GenerateCode(spc, topics!, unions!, globalMappings!);
            });
        }
        
        private static SchemaTopicType? TransformTopicType(GeneratorAttributeSyntaxContext context)
        {
            var symbol = context.TargetSymbol as INamedTypeSymbol;
            if (symbol is null)
                return null;

            var topicAttr = context.Attributes.FirstOrDefault();
            if (topicAttr == null) return null;

            string topicName = "";
            if (topicAttr.ConstructorArguments.Length > 0)
            {
                topicName = topicAttr.ConstructorArguments[0].Value as string ?? "";
            }

            return new SchemaTopicType
            {
                Symbol = symbol,
                TopicName = topicName
            };
        }

        private static SchemaUnionType? TransformUnionType(GeneratorAttributeSyntaxContext context)
        {
            var symbol = context.TargetSymbol as INamedTypeSymbol;
            if (symbol is null)
                return null;

            return new SchemaUnionType
            {
                Symbol = symbol
            };
        }
        
        private static GlobalTypeMapping? TransformTypeMap(GeneratorAttributeSyntaxContext context)
        {
            var attr = context.Attributes.FirstOrDefault();
            if (attr == null) return null;

            if (attr.ConstructorArguments.Length >= 2)
            {
                var sourceType = attr.ConstructorArguments[0].Value as INamedTypeSymbol;
                var wireKindVal = attr.ConstructorArguments[1].Value;
                
                // Be lenient with parsing to facilitate discovery (validation later in FCDC-006)
                if (sourceType != null)
                {
                    DdsWire wireKind = DdsWire.Guid16; // Default
                    if (wireKindVal is int val) wireKind = (DdsWire)val;
                    else if (wireKindVal is DdsWire w) wireKind = w;
                    
                    return new GlobalTypeMapping
                    {
                        SourceType = sourceType,
                        WireKind = wireKind
                    };
                }
            }
            return null;
        }

        private static void GenerateCode(
            SourceProductionContext context,
            ImmutableArray<SchemaTopicType> topics,
            ImmutableArray<SchemaUnionType> unions,
            ImmutableArray<GlobalTypeMapping> typeMappings)
        {
            foreach (var topic in topics)
            {
                if (string.IsNullOrWhiteSpace(topic.TopicName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.TopicNameMissing,
                        topic.Symbol.Locations.FirstOrDefault(),
                        topic.Symbol.Name));
                    continue; 
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TopicDiscovered,
                    Location.None,
                    topic.TopicName,
                    topic.Symbol.ToDisplayString()));

                var code = $@"// Auto-generated for topic: {topic.TopicName}
// Type: {topic.Symbol.ToDisplayString()}

namespace {topic.Symbol.ContainingNamespace}
{{
    partial class {topic.Symbol.Name}
    {{
        // FCDC-005: Discovery placeholder
    }}
}}";
                context.AddSource($"{topic.Symbol.Name}.Discovery.g.cs", code);
            }

            foreach (var union in unions)
            {
                 var code = $@"// Auto-generated for union: {union.Symbol.Name}
namespace {union.Symbol.ContainingNamespace}
{{
    partial class {union.Symbol.Name}
    {{
        // FCDC-005: Discovery placeholder
    }}
}}";
                 context.AddSource($"{union.Symbol.Name}.Discovery.g.cs", code);
            }
            
            if (typeMappings.Any())
            {
                var mapLines = string.Join("\n", typeMappings.Select(m => $"// Map: {m.SourceType.Name} -> {m.WireKind}"));
                context.AddSource("GlobalTypeMaps.Discovery.g.cs", $@"// Auto-generated type maps
{mapLines}
");
            }
        }
    }
}
