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
                .Where(static type => type is not null)
                .Select(static (type, _) => type!)
                .Collect();

            // Pipeline 2: Discover [DdsUnion] types
            var unionTypes = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: "CycloneDDS.Schema.DdsUnionAttribute",
                    predicate: static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                    transform: static (ctx, _) => TransformUnionType(ctx))
                .Where(static type => type is not null)
                .Select(static (type, _) => type!)
                .Collect();

            // Pipeline 3: Discover [DdsTypeMap] assembly attributes
            var typeMappings = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: "CycloneDDS.Schema.DdsTypeMapAttribute",
                    predicate: static (node, _) => true, 
                    transform: static (ctx, _) => TransformTypeMap(ctx))
                .Where(static map => map is not null)
                .Select(static (map, _) => map!)
                .Collect();
                
            var allInput = topicTypes
                .Combine(unionTypes)
                .Combine(typeMappings)
                .Select(static (source, _) => new GenerationInput
                {
                    Topics = source.Left.Left,
                    Unions = source.Left.Right,
                    Mappings = source.Right
                });

            context.RegisterSourceOutput(allInput, static (spc, input) =>
            {
                GenerateCode(spc, input.Topics, input.Unions, input.Mappings);
            });
        }
        
        private static SchemaTopicType? TransformTopicType(GeneratorAttributeSyntaxContext context)
        {
            var symbol = context.TargetSymbol as INamedTypeSymbol;
            if (symbol is null) return null;

            var topicAttr = context.Attributes.FirstOrDefault();
            if (topicAttr == null) return null;

            string topicName = "";
            if (topicAttr.ConstructorArguments.Length > 0)
            {
                topicName = topicAttr.ConstructorArguments[0].Value as string ?? "";
            }

            // Handle Global Namespace
            string ns = symbol.ContainingNamespace.IsGlobalNamespace 
                ? string.Empty 
                : symbol.ContainingNamespace.ToDisplayString();

            return new SchemaTopicType
            {
                Namespace = ns,
                TypeName = symbol.Name,
                TopicName = topicName,
                DefinitionName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                
                // No need to manually init arrays if you set default in record, 
                // but if you have data, cast it:
                // Fields = fields.ToImmutableArray() 
            };
        }

        private static SchemaUnionType? TransformUnionType(GeneratorAttributeSyntaxContext context)
        {
            var symbol = context.TargetSymbol as INamedTypeSymbol;
            if (symbol is null)
                return null;

            return new SchemaUnionType
            {
                Namespace = symbol.ContainingNamespace.ToDisplayString(),
                TypeName = symbol.Name,
                DefinitionName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
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
                        SourceTypeName = sourceType.Name,
                        WireKind = wireKind,
                        DefinitionName = sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
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
                        Location.None,
                        topic.TypeName));
                    continue; 
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TopicDiscovered,
                    Location.None,
                    topic.TopicName,
                    topic.DefinitionName));

                var nsDeclaration = string.IsNullOrEmpty(topic.Namespace) 
                    ? "" 
                    : $"namespace {topic.Namespace}";
                
                var openingBrace = string.IsNullOrEmpty(topic.Namespace) ? "" : "{";
                var closingBrace = string.IsNullOrEmpty(topic.Namespace) ? "" : "}";

                var code = $@"// Auto-generated for topic: {topic.TopicName}
// Type: {topic.DefinitionName}

{nsDeclaration}
{openingBrace}
    partial class {topic.TypeName}
    {{
        // FCDC-005: Discovery placeholder
    }}
{closingBrace}";
                context.AddSource($"{topic.TypeName}.Discovery.g.cs", code);
            }

            foreach (var union in unions)
            {
                 var code = $@"// Auto-generated for union: {union.TypeName}
namespace {union.Namespace}
{{
    partial class {union.TypeName}
    {{
        // FCDC-005: Discovery placeholder
    }}
}}";
                 context.AddSource($"{union.TypeName}.Discovery.g.cs", code);
            }
            
            if (typeMappings.Any())
            {
                var mapLines = string.Join("\n", typeMappings.Select(m => $"// Map: {m.SourceTypeName} -> {m.WireKind}"));
                context.AddSource("GlobalTypeMaps.Discovery.g.cs", $@"// Auto-generated type maps
{mapLines}
");
            }
        }
    }
}
