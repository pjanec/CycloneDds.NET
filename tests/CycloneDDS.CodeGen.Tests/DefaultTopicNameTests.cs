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
        // IdlEmitter – @topic annotation is plain (no name parameter)
        // Success condition 4 (updated for ME1-C03/D06):
        //   IDL output always emits @topic without a name= parameter
        //   to avoid idlc warnings about ignored parameters.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void IdlEmitter_DdsTopicNoArg_EmitsPlainTopicAnnotation()
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

            // Plain @topic must be present; name=" must NOT appear (D06 fix).
            Assert.Contains("@topic", content);
            Assert.DoesNotContain("@topic(", content);
        }

        [Fact]
        public void IdlEmitter_DdsTopicExplicitName_EmitsPlainTopicAnnotation()
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
            // D06: plain @topic regardless of name
            Assert.Contains("@topic", content);
            Assert.DoesNotContain("@topic(", content);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Full Roslyn discovery + IDL generation roundtrip
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void FullRoundtrip_DdsTopicNoArg_IdlContainsPlainTopicAnnotation()
        {
            // ME1-T03 / ME1-C03 integration: IDL for a type My.Ns.Item with [DdsTopic]
            // must contain plain @topic (no name= parameter, per D06 fix).
            // The resolved TopicName "My_Ns_Item" is used at runtime but NOT embedded in IDL.
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

            // SchemaDiscovery must still resolve topic name correctly for runtime use.
            var t = Assert.Single(types, x => x.Name == "Item");
            Assert.Equal("My_Ns_Item", t.TopicName);

            var registry = new GlobalTypeRegistry();
            foreach (var typ in types)
            {
                var file = discovery.GetIdlFileName(typ, typ.SourceFile);
                var module = discovery.GetIdlModule(typ);
                registry.RegisterLocal(typ, typ.SourceFile, file, module);
            }

            var outputDir = Path.Combine(_tempDir, "roundtrip_idl");
            Directory.CreateDirectory(outputDir);
            new IdlEmitter().EmitIdlFiles(registry, outputDir);

            var idlFiles = System.IO.Directory.GetFiles(outputDir, "*.idl");
            Assert.NotEmpty(idlFiles);
            var allContent = string.Concat(System.Array.ConvertAll(idlFiles, System.IO.File.ReadAllText));

            // D06: plain @topic must be present; name= must NOT appear.
            Assert.Contains("@topic", allContent);
            Assert.DoesNotContain("@topic(", allContent);
        }
    }
}
