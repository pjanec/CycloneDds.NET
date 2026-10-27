using System;
using System.Threading;
using Xunit;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests
{
    [DdsTopic("LifecycleDefaultMsg")]
    [DdsQos]
    public partial struct LifecycleDefaultMsg
    {
        [DdsId(0), DdsKey]
        public int Id;
        [DdsId(1)]
        public int Value;
    }

    [DdsTopic("LifecycleNoAutodisposeMsg")]
    [DdsQos(AutodisposeUnregisteredInstances = false)]
    public partial struct LifecycleNoAutodisposeMsg
    {
        [DdsId(0), DdsKey]
        public int Id;
        [DdsId(1)]
        public int Value;
    }

    /// <summary>
    /// [DdsQos(AutodisposeUnregisteredInstances)] maps to the writer data lifecycle QoS.
    /// </summary>
    public class WriterDataLifecycleTests
    {
        [Fact]
        public void AutodisposeFalse_Unregister_ReaderSeesNotAliveNoWriters()
        {
            using var participant = new DdsParticipant();
            string topic = "LifecycleNoAutodispose_" + Guid.NewGuid().ToString("N");
            using var writer = new DdsWriter<LifecycleNoAutodisposeMsg>(participant, topic);
            using var reader = new DdsReader<LifecycleNoAutodisposeMsg>(participant, topic);

            var sample = new LifecycleNoAutodisposeMsg { Id = 1, Value = 10 };
            writer.Write(sample);
            Assert.Equal(DdsInstanceState.Alive, TakeFirstState(reader));

            writer.UnregisterInstance(sample);
            Assert.Equal(DdsInstanceState.NotAliveNoWriters, TakeFirstState(reader));
        }

        [Fact]
        public void AutodisposeDefault_Unregister_ReaderSeesNotAliveDisposed()
        {
            using var participant = new DdsParticipant();
            string topic = "LifecycleDefault_" + Guid.NewGuid().ToString("N");
            using var writer = new DdsWriter<LifecycleDefaultMsg>(participant, topic);
            using var reader = new DdsReader<LifecycleDefaultMsg>(participant, topic);

            var sample = new LifecycleDefaultMsg { Id = 1, Value = 10 };
            writer.Write(sample);
            Assert.Equal(DdsInstanceState.Alive, TakeFirstState(reader));

            writer.UnregisterInstance(sample);
            Assert.Equal(DdsInstanceState.NotAliveDisposed, TakeFirstState(reader));
        }

        [Fact]
        public void AutodisposeAttribute_DefaultsToTrue()
        {
            Assert.True(new DdsQosAttribute().AutodisposeUnregisteredInstances);
        }

        private static DdsInstanceState TakeFirstState<T>(DdsReader<T> reader) where T : new()
        {
            for (int i = 0; i < 100; i++)
            {
                using var scope = reader.Take();
                if (scope.Count > 0) return scope.Infos[0].InstanceState;
                Thread.Sleep(20);
            }
            throw new TimeoutException("no sample received");
        }
    }
}
