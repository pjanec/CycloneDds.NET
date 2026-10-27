using System;
using System.IO;
using System.Linq;
using CycloneDDS.CodeGen;
using CycloneDDS.Schema;
using DdsMonitor.Engine;
using Xunit;

namespace TopicNameConsistency.Ns
{
    // Mirrors TopicNameConsistencyTests.Source, compiled into the test assembly so the
    // runtime rule and DdsMonitor can be asked about the very same types.
    [DdsTopic]
    public struct TopLevel
    {
        public int Value;
    }

    public class Outer
    {
        [DdsTopic]
        public struct Inner
        {
            public int Value;
        }
    }
}

namespace CycloneDDS.CodeGen.Tests
{
    /// <summary>
    /// The generator (SchemaDiscovery), the runtime (DdsTopicName, used by DdsWriter/DdsReader)
    /// and DdsMonitor (TopicMetadata) must spell the default topic name the same way.
    /// </summary>
    public class TopicNameConsistencyTests : IDisposable
    {
        private const string Source = @"
using CycloneDDS.Schema;
namespace TopicNameConsistency.Ns
{
    [DdsTopic]
    public struct TopLevel
    {
        public int Value;
    }

    public class Outer
    {
        [DdsTopic]
        public struct Inner
        {
            public int Value;
        }
    }
}";

        private readonly string _tempDir;

        public TopicNameConsistencyTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "CG_TopicName_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            File.WriteAllText(Path.Combine(_tempDir, "Types.cs"), Source);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        [Theory]
        [InlineData(typeof(TopicNameConsistency.Ns.TopLevel), "TopLevel", "TopicNameConsistency_Ns_TopLevel")]
        [InlineData(typeof(TopicNameConsistency.Ns.Outer.Inner), "Inner", "TopicNameConsistency_Ns_Outer_Inner")]
        public void Generator_Runtime_DdsMonitor_AgreeOnDefaultName(Type type, string simpleName, string expected)
        {
            var generated = new SchemaDiscovery().DiscoverTopics(_tempDir).Single(t => t.Name == simpleName);

            Assert.Equal(expected, generated.TopicName);
            Assert.Equal(expected, DdsTopicName.Of(type));
            Assert.Equal(expected, new TopicMetadata(type).TopicName);
        }
    }
}
