using System;
using Xunit;
using CycloneDDS.Runtime;

namespace CycloneDDS.Runtime.Tests
{
    public class AutoDiscoveryTests : IDisposable
    {
        private DdsParticipant _participant;

        public AutoDiscoveryTests()
        {
            _participant = new DdsParticipant();
        }

        public void Dispose()
        {
            _participant.Dispose();
        }

        [Fact]
        public void GetDescriptorOps_ValidType_ReturnsOps()
        {
            var ops = DdsTypeSupport.GetDescriptorOps<TestMessage>();
            Assert.NotNull(ops);
            Assert.NotEmpty(ops);
        }

        [Fact]
        public void GetDescriptorOps_InvalidType_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => DdsTypeSupport.GetDescriptorOps<int>());
        }

        [Fact]
        public void TopicCache_SameName_ReturnsSameHandle()
        {
            var topic1 = _participant.GetOrRegisterTopic<TestMessage>("CachedTopic");
            var topic2 = _participant.GetOrRegisterTopic<TestMessage>("CachedTopic");
            
            Assert.Equal(topic1.Handle, topic2.Handle);
        }

        [Fact]
        public void TopicCache_DifferentNames_CreatesSeparateTopics()
        {
            var topic1 = _participant.GetOrRegisterTopic<TestMessage>("Topic1");
            var topic2 = _participant.GetOrRegisterTopic<TestMessage>("Topic2");
            
            Assert.NotEqual(topic1.Handle, topic2.Handle);
        }

        [Fact]
        public void AutoDiscovery_ValidType_Succeeds()
        {
            // Should succeed without manual descriptor
            using var writer = new DdsWriter<TestMessage>(_participant, "AutoDiscTopic");
            Assert.NotNull(writer);
        }

        [Fact]
        public void AutoDiscovery_InvalidType_Throws()
        {
            // int has no GetDescriptorOps
            Assert.Throws<InvalidOperationException>(() => new DdsWriter<int>(_participant, "InvalidTopic"));
        }
    }
}
