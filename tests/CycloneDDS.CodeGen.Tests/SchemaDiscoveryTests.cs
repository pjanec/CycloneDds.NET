using System;
using System.IO;
using System.Linq;
using Xunit;
using CycloneDDS.CodeGen;

namespace CycloneDDS.CodeGen.Tests
{
    public class SchemaDiscoveryTests : IDisposable
    {
        private readonly string _tempDir;

        public SchemaDiscoveryTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch {}
        }
        
        private string CreateFile(string content)
        {
            var path = Path.Combine(_tempDir, "Tests_" + Guid.NewGuid().ToString("N") + ".cs");
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void Discovery_DdsStruct_Found()
        {
            CreateFile(@"
using CycloneDDS.Schema;
namespace Test
{
    [DdsStruct]
    public struct SafePoint
    {
        public double X, Y;
    }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);
            
            var type = types.FirstOrDefault(t => t.Name == "SafePoint");
            Assert.NotNull(type);
            Assert.True(type.IsStruct);
            Assert.False(type.IsTopic);
        }

        [Fact]
        public void Discovery_DdsTopic_StillWorks()
        {
            CreateFile(@"
using CycloneDDS.Schema;
namespace Test
{
    [DdsTopic(""T1"")]
    public struct T1
    {
        [DdsKey] public int Id;
    }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);
            
            var type = types.FirstOrDefault(t => t.Name == "T1");
            Assert.NotNull(type);
            Assert.True(type.IsTopic);
            Assert.False(type.IsStruct);
        }

        [Fact]
        public void Discovery_Mixed_FindsBoth()
        {
            CreateFile(@"
using CycloneDDS.Schema;
namespace Test
{
    [DdsTopic(""T1"")]
    public struct T1
    {
        public int Id;
        public Nested1 N;
    }
    
    [DdsStruct]
    public struct Nested1
    {
        public int Val;
    }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);
            
            Assert.Equal(2, types.Count);
            Assert.Contains(types, t => t.Name == "T1" && t.IsTopic);
            Assert.Contains(types, t => t.Name == "Nested1" && t.IsStruct);
        }
    }
}

