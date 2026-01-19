using System;
using Xunit;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Tests;

namespace CycloneDDS.Runtime.Tests
{
    public class DdsWriterTests
    {
        [Fact]
        public void CreateWriter_Success()
        {
            using var participant = new DdsParticipant(0);
            
            using var writer = new DdsWriter<TestMessage>(participant, "TestTopic_Unique1");
        }

        [Fact]
        public void Write_SingleSample_Success()
        {
            using var participant = new DdsParticipant(0);
            using var writer = new DdsWriter<TestMessage>(participant, "TestTopic_Unique2");
            
            var data = new TestMessage { Id = 1, Value = 123 };
            writer.Write(data);
        }
        
        [Fact]
        public void Dispose_Idempotent()
        {
            using var participant = new DdsParticipant(0);
            var writer = new DdsWriter<TestMessage>(participant, "TestTopic_Unique3");
            
            writer.Dispose();
            writer.Dispose();
        }

        [Fact]
        public void Write_AfterDispose_Throws()
        {
            using var participant = new DdsParticipant(0);
            var writer = new DdsWriter<TestMessage>(participant, "TestTopic_Unique4");
            
            writer.Dispose();
            
            Assert.Throws<ObjectDisposedException>(() => writer.Write(new TestMessage()));
        }
    }
}
