using System;
using Xunit;
using CycloneDDS.Runtime;

namespace CycloneDDS.Runtime.Tests
{
    public class DdsReaderTests
    {
        [Fact]
        public void CreateReader_Success()
        {
            using var participant = new DdsParticipant(0);
            using var reader = new DdsReader<TestMessage, TestMessage>(participant, "TestTopic_Unique1");
            
            Assert.NotNull(reader);
        }

        [Fact]
        public void Take_NoData_ReturnsEmptyScope()
        {
            using var participant = new DdsParticipant(0);
            using var reader = new DdsReader<TestMessage, TestMessage>(participant, "TestTopic_Unique2");
            
            using var scope = reader.Take();
            
            Assert.Equal(0, scope.Count);
        }

        [Fact]
        public void Dispose_Idempotent()
        {
            using var participant = new DdsParticipant(0);
            var reader = new DdsReader<TestMessage, TestMessage>(participant, "TestTopic_Unique3");
            
            reader.Dispose();
            reader.Dispose();
        }

        [Fact]
        public void Take_AfterDispose_Throws()
        {
            using var participant = new DdsParticipant(0);
            var reader = new DdsReader<TestMessage, TestMessage>(participant, "TestTopic_Unique4");
            
            reader.Dispose();
            
            Assert.Throws<ObjectDisposedException>(() => reader.Take());
        }
    }
}
