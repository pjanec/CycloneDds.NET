using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using CycloneDDS.Schema;
using DdsMonitor.Engine.AssemblyScanner;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for ME2-BATCH-07:
///   ME2-T27 — TopicColorService DI registration fix: TopicColorService is now Scoped
///             (accepts IWorkspaceState per constructor) — FakeWorkspaceState still works.
///   ME2-T14 — Folder-based assembly scanning: AssemblyPath on TopicMetadata;
///             ScanEntry handles directory paths; non-loadable files silently skipped;
///             single-file entries continue to work.
/// </summary>
public sealed class ME2Batch07Tests : IDisposable
{
    private readonly string _tempDir;

    public ME2Batch07Tests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T27: TopicColorService — constructor still works with IWorkspaceState
    // (verifies that the Scoped registration pattern is valid)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TopicColorService_ConstructsWithWorkspaceState_NoException()
    {
        // After the T27 fix, TopicColorService still accepts IWorkspaceState so the
        // existing FakeWorkspaceState test pattern continues to work — only the DI
        // registration lifetime changed (Singleton → Scoped).
        var exception = Record.Exception(() => CreateColorService());
        Assert.Null(exception);
    }

    [Fact]
    public void TopicColorService_AfterFix_UserOverridesStillPersist()
    {
        var service1 = CreateColorService(_tempDir);
        service1.SetUserColor("FixedTopic", "#cafeba");

        var service2 = CreateColorService(_tempDir);

        Assert.Equal("#cafeba", service2.GetUserColor("FixedTopic"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T14: TopicMetadata.AssemblyPath
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TopicMetadata_AssemblyPath_IsNotEmpty()
    {
        var metadata = new TopicMetadata(typeof(SimpleType));

        Assert.False(string.IsNullOrEmpty(metadata.AssemblyPath));
    }

    [Fact]
    public void TopicMetadata_AssemblyPath_MatchesAssemblyLocation()
    {
        var metadata = new TopicMetadata(typeof(SimpleType));

        var expected = typeof(SimpleType).Assembly.Location;
        Assert.Equal(expected, metadata.AssemblyPath, StringComparer.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T14: ScanEntry — folder scanning via AssemblySourceService
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ScanEntry_Directory_AggregatesTopicsFromMultipleDlls()
    {
        // Arrange: compile two DLLs into the same folder.
        var dllDir = Path.Combine(_tempDir, "scan_multi");
        Directory.CreateDirectory(dllDir);

        CompileAssembly("PluginA",
            "using CycloneDDS.Schema; namespace PA { [DdsTopic(\"TopicA\")] public class TopicA { public int Id; } }",
            dllDir);
        CompileAssembly("PluginB",
            "using CycloneDDS.Schema; namespace PB { [DdsTopic(\"TopicB\")] public class TopicB { public int Id; } }",
            dllDir);

        var registry = new TopicRegistry();
        var discoveryService = new TopicDiscoveryService(registry);
        var configPath = Path.Combine(_tempDir, "assembly-sources.json");
        var service = new AssemblySourceService(registry, discoveryService, configPath);

        // Act
        service.Add(dllDir);

        // Assert: both topics from both DLLs appear.
        var topics = service.GetTopicsForEntry(0);
        var topicNames = topics.Select(t => t.TopicName).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("TopicA", topicNames);
        Assert.Contains("TopicB", topicNames);
    }

    [Fact]
    public void ScanEntry_Directory_NonLoadableDllDoesNotThrow()
    {
        // Arrange: one valid DLL and one garbage (non-loadable) file in the same directory.
        var dllDir = Path.Combine(_tempDir, "scan_skip");
        Directory.CreateDirectory(dllDir);

        CompileAssembly("GoodPlugin",
            "using CycloneDDS.Schema; namespace GP { [DdsTopic(\"GoodTopic\")] public class GoodTopic { public int Id; } }",
            dllDir);
        // Write a corrupt/non-PE DLL file.
        File.WriteAllText(Path.Combine(dllDir, "BadPlugin.dll"), "not a real dll");

        var registry = new TopicRegistry();
        var discoveryService = new TopicDiscoveryService(registry);
        var configPath = Path.Combine(_tempDir, "assembly-sources-skip.json");
        var service = new AssemblySourceService(registry, discoveryService, configPath);

        // Act — must not throw.
        var exception = Record.Exception(() => service.Add(dllDir));
        Assert.Null(exception);

        // The good DLL's topic is still returned.
        var topics = service.GetTopicsForEntry(0);
        Assert.Contains(topics, t => t.TopicName == "GoodTopic");
    }

    [Fact]
    public void ScanEntry_SingleFile_ContinuesToWork()
    {
        // Confirm the existing single-file path is unaffected by the directory branch.
        var dllDir = Path.Combine(_tempDir, "scan_single");
        Directory.CreateDirectory(dllDir);

        var dllPath = CompileAssembly("SinglePlugin",
            "using CycloneDDS.Schema; namespace SP { [DdsTopic(\"SingleTopic\")] public class SingleTopic { public int Id; } }",
            dllDir);

        var registry = new TopicRegistry();
        var discoveryService = new TopicDiscoveryService(registry);
        var configPath = Path.Combine(_tempDir, "assembly-sources-single.json");
        var service = new AssemblySourceService(registry, discoveryService, configPath);

        service.Add(dllPath);

        var topics = service.GetTopicsForEntry(0);
        Assert.Single(topics);
        Assert.Equal("SingleTopic", topics[0].TopicName);
    }

    [Fact]
    public void ScanEntry_Directory_TopicsHaveNonEmptyAssemblyPath()
    {
        var dllDir = Path.Combine(_tempDir, "scan_asmpath");
        Directory.CreateDirectory(dllDir);

        CompileAssembly("PathPlugin",
            "using CycloneDDS.Schema; namespace PP { [DdsTopic(\"PathTopic\")] public class PathTopic { public int Id; } }",
            dllDir);

        var registry = new TopicRegistry();
        var discoveryService = new TopicDiscoveryService(registry);
        var configPath = Path.Combine(_tempDir, "assembly-sources-path.json");
        var service = new AssemblySourceService(registry, discoveryService, configPath);

        service.Add(dllDir);

        var topics = service.GetTopicsForEntry(0);
        Assert.All(topics, t => Assert.False(string.IsNullOrEmpty(t.AssemblyPath)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static TopicColorService CreateColorService(string? tempDir = null)
    {
        var dir = tempDir ?? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var workspaceState = new FakeWorkspaceState(dir);
        return new TopicColorService(workspaceState);
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
                result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"Failed to compile '{assemblyName}': {diagnostics}");
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

    private sealed class FakeWorkspaceState : IWorkspaceState
    {
        public FakeWorkspaceState(string dir)
        {
            WorkspaceFilePath = Path.Combine(dir, "workspace.json");
        }

        public string WorkspaceFilePath { get; }
    }
}
