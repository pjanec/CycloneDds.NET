using System;
using System.IO;
using System.Linq;
using Xunit;
using CycloneDDS.CodeGen;

namespace CycloneDDS.CodeGen.Tests
{
    public class GeneratorTests : IDisposable
    {
        private readonly string _tempDir;

        public GeneratorTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private string CreateFile(string filename, string content)
        {
            var path = Path.Combine(_tempDir, filename);
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void Program_Main_ReturnsError_WhenArgsMissing()
        {
            var result = Program.Main(new string[0]);
            Assert.Equal(1, result);
        }

        [Fact]
        public void SchemaDiscovery_Throws_WhenDirectoryNotFound()
        {
            var discovery = new SchemaDiscovery();
            Assert.Throws<DirectoryNotFoundException>(() => discovery.DiscoverTopics("nonexistent"));
        }

        [Fact]
        public void SchemaDiscovery_FindsTypes_WithDdsTopic()
        {
            CreateFile("TestTopic.cs", @"
using CycloneDDS.Schema;
namespace MyNamespace {
    [DdsTopic(""MyTopic"")]
    public struct MyTopicStruct { }
}");

            var discovery = new SchemaDiscovery();
            var topics = discovery.DiscoverTopics(_tempDir);

            Assert.Single(topics);
            Assert.Equal("MyTopicStruct", topics[0].Name);
            Assert.Equal("MyNamespace", topics[0].Namespace);
            Assert.Equal("MyNamespace.MyTopicStruct", topics[0].FullName);
        }

        [Fact]
        public void SchemaDiscovery_IgnoresTypes_WithoutDdsTopic()
        {
            CreateFile("NotATopic.cs", @"
namespace MyNamespace {
    public struct NotATopic { }
}");

            var discovery = new SchemaDiscovery();
            var topics = discovery.DiscoverTopics(_tempDir);

            Assert.Empty(topics);
        }

        [Fact]
        public void SchemaDiscovery_Handles_NestedNamespaces()
        {
            CreateFile("NestedTopic.cs", @"
using CycloneDDS.Schema;
namespace A.B {
    [DdsTopic(""Nested"")]
    public class NestedTopic { }
}");

            var discovery = new SchemaDiscovery();
            var topics = discovery.DiscoverTopics(_tempDir);

            Assert.Single(topics);
            Assert.Equal("A.B", topics[0].Namespace);
        }

        [Fact]
        public void SchemaDiscovery_Handles_FileScopedNamespaces()
        {
            CreateFile("FileScopedTopic.cs", @"
using CycloneDDS.Schema;
namespace A.B.C;
[DdsTopic(""FileScoped"")]
public class FileScopedTopic { }
");

            var discovery = new SchemaDiscovery();
            var topics = discovery.DiscoverTopics(_tempDir);

            Assert.Single(topics);
            Assert.Equal("A.B.C", topics[0].Namespace);
        }

        [Fact]
        public void CodeGenerator_GeneratesOutput()
        {
             CreateFile("GenTopic.cs", @"
using CycloneDDS.Schema;
[DdsTopic(""Gen"")]
public struct GenTopic { }
");
            var outputDir = Path.Combine(_tempDir, "Output");
            var generator = new CodeGenerator();
            
            generator.Generate(_tempDir, outputDir);

            Assert.True(Directory.Exists(outputDir));
            Assert.True(File.Exists(Path.Combine(outputDir, "GenTopic.idl")));
        }

        [Fact]
        public void Program_Main_RunsEndToEnd()
        {
             CreateFile("MainTopic.cs", @"
using CycloneDDS.Schema;
[DdsTopic(""Main"")]
public struct MainTopic { }
");
            var outputDir = Path.Combine(_tempDir, "MainOutput");
            
            var result = Program.Main(new string[] { _tempDir, outputDir });
            
            Assert.Equal(0, result);
            Assert.True(File.Exists(Path.Combine(outputDir, "MainTopic.idl")));
        }

        [Fact]
        public void SchemaDiscovery_Handles_NestedClasses()
        {
            CreateFile("NestedClassTopic.cs", @"
using CycloneDDS.Schema;
namespace MyNamespace {
    public class Outer {
        [DdsTopic(""NestedClass"")]
        public class Inner { }
    }
}");

            var discovery = new SchemaDiscovery();
            var topics = discovery.DiscoverTopics(_tempDir);

            Assert.Single(topics);
            Assert.Equal("Inner", topics[0].Name);
            Assert.Equal("MyNamespace", topics[0].Namespace);
        }

        [Fact]
        public void SchemaDiscovery_Handles_MultipleTopicsInFile()
        {
            CreateFile("MultipleTopics.cs", @"
using CycloneDDS.Schema;
namespace MyNamespace {
    [DdsTopic(""Topic1"")]
    public struct Topic1 { }

    [DdsTopic(""Topic2"")]
    public struct Topic2 { }
}");

            var discovery = new SchemaDiscovery();
            var topics = discovery.DiscoverTopics(_tempDir);

            Assert.Equal(2, topics.Count);
            Assert.Contains(topics, t => t.Name == "Topic1");
            Assert.Contains(topics, t => t.Name == "Topic2");
        }

        [Fact]
        public void CodeGenerator_CombinesOutputs_AndCleansLegacyFiles()
        {
            CreateFile("CombinedTopic.cs", @"
using CycloneDDS.Schema;
[DdsTopic(""Combined"")]
public struct CombinedTopic { }
");
            var outputDir = Path.Combine(_tempDir, "CombinedOutput");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, "CombinedTopic.Serializer.cs"), "stale");
            File.WriteAllText(Path.Combine(outputDir, "CombinedTopic.View.cs"), "stale");
            File.WriteAllText(Path.Combine(outputDir, "CombinedTopic.ViewExtensions.cs"), "stale");
            File.WriteAllText(Path.Combine(outputDir, "CombinedTopic.Descriptor.cs"), "stale");

            var generator = new CodeGenerator();
            generator.Generate(_tempDir, outputDir);

            Assert.True(File.Exists(Path.Combine(outputDir, "CombinedTopic.g.cs")));
            Assert.False(File.Exists(Path.Combine(outputDir, "CombinedTopic.Serializer.cs")));
            Assert.False(File.Exists(Path.Combine(outputDir, "CombinedTopic.View.cs")));
            Assert.False(File.Exists(Path.Combine(outputDir, "CombinedTopic.ViewExtensions.cs")));
            Assert.False(File.Exists(Path.Combine(outputDir, "CombinedTopic.Descriptor.cs")));
        }
    }
}

