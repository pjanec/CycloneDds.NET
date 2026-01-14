using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using Xunit;
using CycloneDDS.Generator;
using CycloneDDS.Schema;
using System.Reflection;
using System.Linq;
using System;
using System.Collections.Generic;

namespace CycloneDDS.Generator.Tests
{
    public class DiscoveryTests
    {
        [Fact]
        public void DiscoversSingleTopicType()
        {
            var source = @"
using CycloneDDS.Schema;

[DdsTopic(""TestTopic"")]
public partial class TestType { }
";
            var (compilation, diagnostics) = RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains(diagnostics, d => d.Id == "FCDC0001" && d.GetMessage().Contains("TestTopic"));
            Assert.Contains(compilation.SyntaxTrees, t => t.FilePath.EndsWith("TestType.Discovery.g.cs"));
        }

        [Fact]
        public void DiscoversMultipleTopicTypes()
        {
            var source = @"
using CycloneDDS.Schema;

[DdsTopic(""Topic1"")]
public partial class Type1 { }

[DdsTopic(""Topic2"")]
public partial class Type2 { }
";
            var (compilation, diagnostics) = RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Equal(2, diagnostics.Count(d => d.Id == "FCDC0001"));
            Assert.Contains(compilation.SyntaxTrees, t => t.FilePath.EndsWith("Type1.Discovery.g.cs"));
            Assert.Contains(compilation.SyntaxTrees, t => t.FilePath.EndsWith("Type2.Discovery.g.cs"));
        }

        [Fact]
        public void DiscoversUnionType()
        {
            var source = @"
using CycloneDDS.Schema;

[DdsUnion]
public partial class MyUnion { }
";
            var (compilation, diagnostics) = RunGenerator(source);

            Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            // No specific diagnostic for Union discovery, check file existence
            Assert.Contains(compilation.SyntaxTrees, t => t.FilePath.EndsWith("MyUnion.Discovery.g.cs"));
        }

        [Fact]
        public void ReportsErrorForMissingTopicName()
        {
            var source = @"
using CycloneDDS.Schema;

[DdsTopic("""")]
public partial class InvalidType { }
";
            var (compilation, diagnostics) = RunGenerator(source);

            Assert.Contains(diagnostics, d => d.Id == "FCDC0002" && d.Severity == DiagnosticSeverity.Error);
        }

        [Fact]
        public void IncrementalGen_UnrelatedChange_DoesNotRegenerate()
        {
            var source = @"
using CycloneDDS.Schema;

[DdsTopic(""CachedTopic"")]
public partial class CachedType { }
";
            var generator = new FcdcGenerator();
            // Enable incremental tracking to verifying caching
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                new [] { generator.AsSourceGenerator() }, 
                driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
                
            var compilation = CreateCompilation(source);

            // Run 1
            driver = driver.RunGenerators(compilation);
            var result1 = driver.GetRunResult();
            var tree1 = result1.GeneratedTrees.Single(t => t.FilePath.EndsWith("CachedType.Discovery.g.cs"));

            // Run 2: Modify compilation by adding a comment (irrelevant to generator logic, but changes syntax tree)
            // Ideally we change a file that is not the one with attribute, but here we only have one file.
            // Even if we change the file, if we change something irrelevant to the specific node, granular caching should work?
            // Actually, ForAttributeWithMetadataName is very smart. If the attribute or class sig doesn't change, it shouldn't re-run transform.
            
            var newSyntaxTree = CSharpSyntaxTree.ParseText(source + "\n// Comment");
            var newCompilation = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.First(), newSyntaxTree);

            driver = driver.RunGenerators(newCompilation);
            var result2 = driver.GetRunResult();
            var tree2 = result2.GeneratedTrees.Single(t => t.FilePath.EndsWith("CachedType.Discovery.g.cs"));

            Assert.Same(tree1, tree2);
        }
        
        [Fact]
        public void DiscoversTypeMap()
        {
            var source = @"
using CycloneDDS.Schema;

[assembly: DdsTypeMap(typeof(MappedType), DdsWire.Guid16)]

public class MappedType { }
";
             var (compilation, diagnostics) = RunGenerator(source);
             Assert.Contains(compilation.SyntaxTrees, t => t.FilePath.EndsWith("GlobalTypeMaps.Discovery.g.cs"));
        }
        
        [Fact]
        public void NoSchemas_NoOp()
        {
            var source = @"
public class PlainType { }
";
            var (compilation, diagnostics) = RunGenerator(source);
            Assert.Empty(diagnostics);
            Assert.Empty(compilation.SyntaxTrees.Where(t => t.FilePath.EndsWith("Discovery.g.cs")));
        }


        private static Compilation CreateCompilation(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Concat(new[] {
                    MetadataReference.CreateFromFile(typeof(DdsTopicAttribute).Assembly.Location)
                });

            return CSharpCompilation.Create(
                "TestAssembly",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        private static (Compilation compilation, ImmutableArray<Diagnostic> diagnostics) RunGenerator(string source)
        {
            var compilation = CreateCompilation(source);
            var generator = new FcdcGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
            return (outputCompilation, diagnostics);
        }
    }
}
