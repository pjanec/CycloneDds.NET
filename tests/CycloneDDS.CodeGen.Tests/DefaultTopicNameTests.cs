using System;
using System.IO;
using Xunit;
using CycloneDDS.CodeGen;

namespace CycloneDDS.CodeGen.Tests
{
    /// <summary>
    /// ME1-T03: Tests for default topic name derived from namespace.
    /// Covers both the CodeGen (SchemaDiscovery + IdlEmitter) and schema attribute layer.
    /// </summary>
    public class DefaultTopicNameTests : IDisposable
    {
        private readonly string _tempDir;

        public DefaultTopicNameTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "CG_T03_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        private void CreateFile(string content)
        {
            var path = Path.Combine(_tempDir, "Src_" + Guid.NewGuid().ToString("N") + ".cs");
            File.WriteAllText(path, content);
        }

        // ─────────────────────────────────────────────────────────────────────
        // SchemaDiscovery – resolves topic name to fallback when no argument
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void SchemaDiscovery_DdsTopicNoArg_TopicNameFallbackFromNamespace()
        {
            // ME1-T03 success condition: IDL output for a type with [DdsTopic] (no name)
            // includes the topic declaration using the namespace-underscore form.
            CreateFile(@"
using CycloneDDS.Schema;
namespace My.Ns
{
    [DdsTopic]
    public struct Item
    {
        public int Value;
    }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);

            var t = Assert.Single(types, x => x.Name == "Item");
            Assert.NotNull(t.TopicName);
            // Dots replaced with underscores: "My.Ns.Item" -> "My_Ns_Item"
            Assert.Equal("My_Ns_Item", t.TopicName);
        }

        [Fact]
        public void SchemaDiscovery_DdsTopicExplicitArg_TopicNamePreserved()
        {
            CreateFile(@"
using CycloneDDS.Schema;
namespace My.Ns
{
    [DdsTopic(""ExplicitName"")]
    public struct Telemetry
    {
        public float Value;
    }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);

            var t = Assert.Single(types, x => x.Name == "Telemetry");
            Assert.Equal("ExplicitName", t.TopicName);
        }

        [Fact]
        public void SchemaDiscovery_DdsTopicNoArg_GlobalNamespace_FallsBackToTypeName()
        {
            // Type in global namespace: just the class name.
            CreateFile(@"
using CycloneDDS.Schema;
[DdsTopic]
public struct GlobalItem
{
    public int X;
}
");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);

            var t = Assert.Single(types, x => x.Name == "GlobalItem");
            // No namespace – fallback should be "GlobalItem" (with no underscores needed).
            Assert.NotNull(t.TopicName);
            Assert.Equal("GlobalItem", t.TopicName);
        }

        // ─────────────────────────────────────────────────────────────────────
        // IdlEmitter – @topic annotation includes the resolved topic name
        // Success condition 4:
        //   IDL output for My.Ns.Item with [DdsTopic] includes @topic(name="My_Ns_Item")
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void IdlEmitter_DdsTopicNoArg_EmitsTopicNameAnnotation()
        {
            // Build a TypeInfo representing a type with no explicit name.
            var registry = new GlobalTypeRegistry();
            var type = new TypeInfo
            {
                Name = "Item",
                Namespace = "My.Ns",
                IsTopic = true,
                IsStruct = true,
                TopicName = "My_Ns_Item" // as set by SchemaDiscovery fallback
            };
            registry.RegisterLocal(type, "src.cs", "File1", "My::Ns");

            var outputDir = Path.Combine(_tempDir, "idl");
            Directory.CreateDirectory(outputDir);
            new IdlEmitter().EmitIdlFiles(registry, outputDir);

            var content = File.ReadAllText(Path.Combine(outputDir, "File1.idl"));

            // The @topic annotation must include the resolved name.
            Assert.Contains("@topic(name=\"My_Ns_Item\")", content);
        }

        [Fact]
        public void IdlEmitter_DdsTopicExplicitName_EmitsExplicitTopicName()
        {
            var registry = new GlobalTypeRegistry();
            var type = new TypeInfo
            {
                Name = "Telemetry",
                Namespace = "My.Ns",
                IsTopic = true,
                IsStruct = true,
                TopicName = "ExplicitName"
            };
            registry.RegisterLocal(type, "src.cs", "File1", "My::Ns");

            var outputDir = Path.Combine(_tempDir, "idl2");
            Directory.CreateDirectory(outputDir);
            new IdlEmitter().EmitIdlFiles(registry, outputDir);

            var content = File.ReadAllText(Path.Combine(outputDir, "File1.idl"));
            Assert.Contains("@topic(name=\"ExplicitName\")", content);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Full Roslyn discovery + IDL generation roundtrip
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void FullRoundtrip_DdsTopicNoArg_IdlContainsNamespacedTopicName()
        {
            // ME1-T03 success condition 4 (integration):
            // IDL for a type My.Ns.Item with [DdsTopic] (no name) includes topic using My_Ns_Item.
            CreateFile(@"
using CycloneDDS.Schema;
namespace My.Ns
{
    [DdsTopic]
    public struct Item
    {
        public int Value;
    }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);
            var registry = new GlobalTypeRegistry();
            foreach (var t in types)
            {
                var file = discovery.GetIdlFileName(t, t.SourceFile);
                var module = discovery.GetIdlModule(t);
                registry.RegisterLocal(t, t.SourceFile, file, module);
            }

            var outputDir = Path.Combine(_tempDir, "roundtrip_idl");
            Directory.CreateDirectory(outputDir);
            new IdlEmitter().EmitIdlFiles(registry, outputDir);

            var idlFiles = System.IO.Directory.GetFiles(outputDir, "*.idl");
            Assert.NotEmpty(idlFiles);
            var allContent = string.Concat(System.Array.ConvertAll(idlFiles, System.IO.File.ReadAllText));
            Assert.Contains("My_Ns_Item", allContent);
            Assert.Contains("@topic", allContent);
        }
    }
}
