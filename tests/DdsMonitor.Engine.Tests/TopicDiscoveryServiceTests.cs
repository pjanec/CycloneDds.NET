using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using CycloneDDS.Schema;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class TopicDiscoveryServiceTests : IDisposable
{
    private readonly string _tempDir;

    public TopicDiscoveryServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
        }
    }

    [Fact]
    public void TopicDiscoveryService_FindsTopicInAssembly()
    {
        CompileAssembly(
            "TestTopicPlugin",
            "using CycloneDDS.Schema; namespace TestPlugin { [DdsTopic(\"TestTopic\")] public class SampleTopic { public int Id { get; set; } } }",
            _tempDir);

        var registry = new TopicRegistry();
        var service = new TopicDiscoveryService(registry);

        service.Discover(_tempDir);

        var metadata = Assert.Single(registry.AllTopics);
        Assert.Equal("TestTopic", metadata.TopicName);
    }

    [Fact]
    public void TopicDiscoveryService_IgnoresDllsWithoutTopics()
    {
        CompileAssembly(
            "NoTopicPlugin",
            "namespace TestPlugin { public class NoTopic { public int Id { get; set; } } }",
            _tempDir);

        var registry = new TopicRegistry();
        var service = new TopicDiscoveryService(registry);

        service.Discover(_tempDir);

        Assert.Empty(registry.AllTopics);
    }

    [Fact]
    public void TopicDiscoveryService_IsolatesAssemblyLoadContext()
    {
        CompileAssembly(
            "IsolatedTopicPlugin",
            "using CycloneDDS.Schema; namespace TestPlugin { [DdsTopic(\"IsoTopic\")] public class IsolatedTopic { public int Id { get; set; } } }",
            _tempDir);

        var registry = new TopicRegistry();
        var service = new TopicDiscoveryService(registry);

        service.Discover(_tempDir);

        var metadata = Assert.Single(registry.AllTopics);
        var assembly = metadata.TopicType.Assembly;
        var loadContext = AssemblyLoadContext.GetLoadContext(assembly);

        Assert.NotNull(loadContext);
        Assert.NotSame(AssemblyLoadContext.Default, loadContext);
        Assert.True(loadContext!.IsCollectible);
        Assert.DoesNotContain(AssemblyLoadContext.Default.Assemblies, candidate =>
            string.Equals(candidate.FullName, assembly.FullName, StringComparison.Ordinal));
    }

    private static string CompileAssembly(string assemblyName, string source, string outputDirectory)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var references = CreateReferences();

        var compilation = CSharpCompilation.Create(assemblyName)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddReferences(references)
            .AddSyntaxTrees(tree);

        var dllPath = Path.Combine(outputDirectory, assemblyName + ".dll");
        var result = compilation.Emit(dllPath);

        if (!result.Success)
        {
            var diagnostics = string.Join(
                Environment.NewLine,
                result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"Failed to compile test assembly '{assemblyName}': {diagnostics}");
        }

        return dllPath;
    }

    private static IReadOnlyList<MetadataReference> CreateReferences()
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(DdsTopicAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
        };

        var systemRuntime = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName("System.Runtime"));
        references.Add(MetadataReference.CreateFromFile(systemRuntime.Location));

        return references;
    }
}
