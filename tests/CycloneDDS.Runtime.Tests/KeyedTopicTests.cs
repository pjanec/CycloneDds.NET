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

        [Fact]
        public void CompositeKey_RoundTrip_Basic()
        {
            using var participant = new DdsParticipant(domainId: 0);
            string topicName = $"CompositeTopic_{Guid.NewGuid()}";
            
            using var writer = new DdsWriter<CompositeKeyMessage>(participant, topicName);
            using var reader = new DdsReader<CompositeKeyMessage, CompositeKeyMessage>(participant, topicName);
            
            var sample = new CompositeKeyMessage
            {
                SensorId = 10,
                LocationId = 20,
                Temperature = 25.5
            };
            
            writer.Write(sample);
            Thread.Sleep(100);
            
            using var scope = reader.Take();
            Assert.Equal(1, scope.Count);
            
            var received = scope[0];
            Assert.Equal(10, received.SensorId);
            Assert.Equal(20, received.LocationId);
            Assert.Equal(25.5, received.Temperature, precision: 2);
        }

        [Fact]
        public void CompositeKey_DistinctInstances_BothKeysMustMatch()
        {
            using var participant = new DdsParticipant(domainId: 0);
            string topicName = $"CompositeTopic_{Guid.NewGuid()}";
            
            using var writer = new DdsWriter<CompositeKeyMessage>(participant, topicName);
            using var reader = new DdsReader<CompositeKeyMessage, CompositeKeyMessage>(participant, topicName);
            
            // Write 4 samples - 4 distinct instances because composite key (SensorId, LocationId)
            writer.Write(new CompositeKeyMessage { SensorId = 1, LocationId = 1, Temperature = 10.0 });
            writer.Write(new CompositeKeyMessage { SensorId = 1, LocationId = 2, Temperature = 20.0 }); // Different location
            writer.Write(new CompositeKeyMessage { SensorId = 2, LocationId = 1, Temperature = 30.0 }); // Different sensor
            writer.Write(new CompositeKeyMessage { SensorId = 2, LocationId = 2, Temperature = 40.0 }); // Both different
            Thread.Sleep(100);
            
            using var scope = reader.Take();
            Assert.Equal(4, scope.Count); // 4 distinct instances
            
            var samples = new System.Collections.Generic.List<CompositeKeyMessage>();
            foreach(var s in scope) samples.Add(s);
            
            // Verify all 4 combinations present
            Assert.Contains(samples, s => s.SensorId == 1 && s.LocationId == 1 && s.Temperature == 10.0);
            Assert.Contains(samples, s => s.SensorId == 1 && s.LocationId == 2 && s.Temperature == 20.0);
            Assert.Contains(samples, s => s.SensorId == 2 && s.LocationId == 1 && s.Temperature == 30.0);
            Assert.Contains(samples, s => s.SensorId == 2 && s.LocationId == 2 && s.Temperature == 40.0);
        }

        [Fact]
        public void CompositeKey_SameInstance_RequiresBothKeysEqual()
        {
            using var participant = new DdsParticipant(domainId: 0);
            string topicName = $"CompositeTopic_{Guid.NewGuid()}";
            
            using var writer = new DdsWriter<CompositeKeyMessage>(participant, topicName);
            using var reader = new DdsReader<CompositeKeyMessage, CompositeKeyMessage>(participant, topicName);
            
            // Write same instance (1, 1) twice with different data
            writer.Write(new CompositeKeyMessage { SensorId = 1, LocationId = 1, Temperature = 10.0 });
            writer.Write(new CompositeKeyMessage { SensorId = 1, LocationId = 1, Temperature = 15.0 }); // Update
            Thread.Sleep(100);
            
            using var scope = reader.Take();
            // Default QoS is KeepLast(1), so we expect only the latest sample for the same instance.
            Assert.Equal(1, scope.Count); 
            
            var received = scope[0];
            Assert.Equal(1, received.SensorId);
            Assert.Equal(1, received.LocationId);
            Assert.Equal(15.0, received.Temperature, precision: 2);
        }
        [Fact]
        public void NestedKey_RoundTrip_Basic()
        {
            using var participant = new DdsParticipant(domainId: 0);
            string topicName = $"NestedTopic_{Guid.NewGuid()}";
            
            using var writer = new DdsWriter<NestedKeyMessage>(participant, topicName);
            using var reader = new DdsReader<NestedKeyMessage, NestedKeyMessage>(participant, topicName);
            
            var sample = new NestedKeyMessage
            {
                InnerId = 1,
                Data = "Payload"
            };
            
            writer.Write(sample);
            Thread.Sleep(100);
            
            using var scope = reader.Take();
            Assert.Equal(1, scope.Count);
            
            var received = scope[0];
            Assert.Equal(1, received.InnerId);
            Assert.Equal("Payload", received.Data);
        }

        [Fact]
        public void NestedKey_DifferentInstances_DifferentInnerKeys()
        {
            using var participant = new DdsParticipant(domainId: 0);
            string topicName = $"NestedTopic_{Guid.NewGuid()}";
            
            using var writer = new DdsWriter<NestedKeyMessage>(participant, topicName);
            using var reader = new DdsReader<NestedKeyMessage, NestedKeyMessage>(participant, topicName);
            
            // Instance 1
            writer.Write(new NestedKeyMessage { 
                InnerId = 1, 
                Data = "Data1" 
            });

            // Instance 2
            writer.Write(new NestedKeyMessage { 
                InnerId = 2, 
                Data = "Data2" 
            });
            
            Thread.Sleep(100);
            
            using var scope = reader.Take();
            Assert.Equal(2, scope.Count);
            
            var samples = new System.Collections.Generic.List<NestedKeyMessage>();
            foreach(var s in scope) samples.Add(s);
             
            Assert.Contains(samples, s => s.InnerId == 1);
            Assert.Contains(samples, s => s.InnerId == 2);
        }

        [Fact]
        public void NestedKey_SameInstance_UpdatesData()
        {
             using var participant = new DdsParticipant(domainId: 0);
            string topicName = $"NestedTopic_{Guid.NewGuid()}";
            
            using var writer = new DdsWriter<NestedKeyMessage>(participant, topicName);
            using var reader = new DdsReader<NestedKeyMessage, NestedKeyMessage>(participant, topicName);
            
            // Write, then Update
            writer.Write(new NestedKeyMessage { InnerId = 99, Data = "Initial" });
            writer.Write(new NestedKeyMessage { InnerId = 99, Data = "Updated" });
            
            Thread.Sleep(100);
            
            using var scope = reader.Take();
            // Default QoS KeepLast(1) => 1 sample
            Assert.Equal(1, scope.Count);
            
Assert.Equal("Updated", scope[0].Data);
            Assert.Equal(99, scope[0].InnerId);
        }

        [Fact]
        public void StringKey_RoundTrip_Basic()
        {
            using var participant = new DdsParticipant(domainId: 0);
            string topicName = $"StringKeyTopic_{Guid.NewGuid()}";

            using var writer = new DdsWriter<StringKeyMessage>(participant, topicName);
            using var reader = new DdsReader<StringKeyMessage, StringKeyMessage>(participant, topicName);

            var sample = new StringKeyMessage
            {
                KeyId = "Device_A",
                Message = "Hello World"
            };

            writer.Write(sample);
            Thread.Sleep(100);

            using var scope = reader.Take();
            Assert.Equal(1, scope.Count);

            var received = scope[0];
            Assert.Equal("Device_A", received.KeyId);
            Assert.Equal("Hello World", received.Message);
        }

        [Fact]
        public void StringKey_DifferentInstances()
        {
            using var participant = new DdsParticipant(domainId: 0);
            string topicName = $"StringKeyTopic_{Guid.NewGuid()}";

            using var writer = new DdsWriter<StringKeyMessage>(participant, topicName);
            using var reader = new DdsReader<StringKeyMessage, StringKeyMessage>(participant, topicName);

            writer.Write(new StringKeyMessage { KeyId = "Id_1", Message = "Msg1" });
            writer.Write(new StringKeyMessage { KeyId = "Id_2", Message = "Msg2" });
            Thread.Sleep(100);

            using var scope = reader.Take();
            Assert.Equal(2, scope.Count);
        }

        [Fact]
        public void MixedKey_RoundTrip_Basic()
        {
            using var participant = new DdsParticipant(domainId: 0);
            string topicName = $"MixedKeyTopic_{Guid.NewGuid()}";

            using var writer = new DdsWriter<MixedKeyMessage>(participant, topicName);
            using var reader = new DdsReader<MixedKeyMessage, MixedKeyMessage>(participant, topicName);

            var sample = new MixedKeyMessage
            {
                Id = 1,
                Name = "Alpha",
                Data = "Data1"
            };

            writer.Write(sample);
            Thread.Sleep(100);

            using var scope = reader.Take();
            Assert.Equal(1, scope.Count);

            var received = scope[0];
            Assert.Equal(1, received.Id);
            Assert.Equal("Alpha", received.Name);
            Assert.Equal("Data1", received.Data);
        }

        [Fact]
        public void MixedKey_DifferentInstances()
        {
            using var participant = new DdsParticipant(domainId: 0);
            string topicName = $"MixedKeyTopic_{Guid.NewGuid()}";

            using var writer = new DdsWriter<MixedKeyMessage>(participant, topicName);
            using var reader = new DdsReader<MixedKeyMessage, MixedKeyMessage>(participant, topicName);

            // Same Id, different Name => Different Instance
            writer.Write(new MixedKeyMessage { Id = 10, Name = "A", Data = "D1" });
            writer.Write(new MixedKeyMessage { Id = 10, Name = "B", Data = "D2" });
            
            // Different Id, same Name => Different Instance
            writer.Write(new MixedKeyMessage { Id = 11, Name = "A", Data = "D3" });

            Thread.Sleep(100);

            using var scope = reader.Take();
            Assert.Equal(3, scope.Count);
        }

        [Fact]
        public void KeyLast_RoundTrip_Basic()
        {
            using var participant = new DdsParticipant(domainId: 0);
            string topicName = $"KeyLastTopic_{Guid.NewGuid()}";

            using var writer = new DdsWriter<KeyLastMessage>(participant, topicName);
            using var reader = new DdsReader<KeyLastMessage, KeyLastMessage>(participant, topicName);

            var sample = new KeyLastMessage
            {
                Data = "SomeData",
                Id = 999
            };

            writer.Write(sample);
            Thread.Sleep(100);

            using var scope = reader.Take();
            Assert.Equal(1, scope.Count);

            var received = scope[0];
            Assert.Equal(999, received.Id);
            Assert.Equal("SomeData", received.Data);
        }
    }
}
