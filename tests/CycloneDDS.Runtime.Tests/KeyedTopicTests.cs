using System;
using System.Linq;
using System.Threading;
using Xunit;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Tests.KeyedMessages;

namespace CycloneDDS.Runtime.Tests
{
    public class KeyedTopicTests
    {
        [Fact]
        public void SingleKey_RoundTrip_Basic()
        {
            // Arrange
            using var participant = new DdsParticipant(domainId: 0);
            string topicName = $"SingleKeyTopic_{Guid.NewGuid()}";
            
            using var writer = new DdsWriter<SingleKeyMessage>(participant, topicName);
            using var reader = new DdsReader<SingleKeyMessage, SingleKeyMessage>(participant, topicName);
            
            var sample = new SingleKeyMessage
            {
                DeviceId = 42,
                Value = 100,
                Timestamp = 123456789L
            };
            
            // Act
            writer.Write(sample);
            Thread.Sleep(100); // Wait for propagation
            
            // Assert
            using var scope = reader.Take();
            Assert.Equal(1, scope.Count);
            
            var received = scope[0];
            Assert.Equal(42, received.DeviceId);
            Assert.Equal(100, received.Value);
            Assert.Equal(123456789L, received.Timestamp);
        }

        [Fact]
        public void SingleKey_MultipleInstances_IndependentDelivery()
        {
            // Arrange
            using var participant = new DdsParticipant(domainId: 0);
            string topicName = $"SingleKeyTopic_{Guid.NewGuid()}";
            
            using var writer = new DdsWriter<SingleKeyMessage>(participant, topicName);
            using var reader = new DdsReader<SingleKeyMessage, SingleKeyMessage>(participant, topicName);
            
            // Act - Write 3 different instances (different DeviceIds)
            writer.Write(new SingleKeyMessage { DeviceId = 1, Value = 100 });
            writer.Write(new SingleKeyMessage { DeviceId = 2, Value = 200 });
            writer.Write(new SingleKeyMessage { DeviceId = 3, Value = 300 });
            Thread.Sleep(100);
            
            // Assert - All 3 instances received
            using var scope = reader.Take();
            Assert.Equal(3, scope.Count);
            
            // Verify distinct instances (distinct DeviceIds)
            var samples = new System.Collections.Generic.List<SingleKeyMessage>();
            foreach(var s in scope) samples.Add(s);
            
            var deviceIds = samples.Select(s => s.DeviceId).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { 1, 2, 3 }, deviceIds);
            
            // Verify values match keys
            Assert.Equal(100, samples.First(s => s.DeviceId == 1).Value);
            Assert.Equal(200, samples.First(s => s.DeviceId == 2).Value);
            Assert.Equal(300, samples.First(s => s.DeviceId == 3).Value);
        }

        [Fact]
        public void SingleKey_SameInstance_UpdatesData()
        {
            // Arrange
            using var participant = new DdsParticipant(domainId: 0);
            string topicName = $"SingleKeyTopic_{Guid.NewGuid()}";
            
            using var writer = new DdsWriter<SingleKeyMessage>(participant, topicName);
            using var reader = new DdsReader<SingleKeyMessage, SingleKeyMessage>(participant, topicName);
            
            // Wait for discovery
            for (int i = 0; i < 20; i++)
            {
                if (reader.CurrentStatus.CurrentCount > 0) break;
                Thread.Sleep(50);
            }

            // Act - Write same instance (DeviceId=5) twice with different values
            writer.Write(new SingleKeyMessage { DeviceId = 5, Value = 100, Timestamp = 1000 });
            writer.Write(new SingleKeyMessage { DeviceId = 5, Value = 200, Timestamp = 2000 });
            Thread.Sleep(100);
            
            // Assert - Default QoS is KeepLast(1), so we expect only the latest sample for the same instance.
            using var scope = reader.Take();
            Assert.Equal(1, scope.Count);
            
            var samples = new System.Collections.Generic.List<SingleKeyMessage>();
            foreach(var s in scope) samples.Add(s);

            Assert.All(samples, s => Assert.Equal(5, s.DeviceId));
            Assert.Equal(200, samples[0].Value);
        }
    }
}
