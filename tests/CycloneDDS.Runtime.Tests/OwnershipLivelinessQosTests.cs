using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests
{
    [DdsTopic("OwnedMsg")]
    [DdsQos(Ownership = DdsOwnershipKind.Exclusive, AutodisposeUnregisteredInstances = false)]
    public partial struct OwnedMsg
    {
        [DdsId(0), DdsKey]
        public int Id;
        [DdsId(1)]
        public int Value;
    }

    [DdsTopic("ManualLivelinessMsg")]
    // AutodisposeUnregisteredInstances = false: Cyclone treats a writer that loses liveliness
    // like one that unregisters, so with the default the instance would read NotAliveDisposed.
    [DdsQos(LivelinessKind = DdsLivelinessKind.ManualByTopic, LivelinessLeaseDurationMs = 300,
            AutodisposeUnregisteredInstances = false)]
    public partial struct ManualLivelinessMsg
    {
        [DdsId(0), DdsKey]
        public int Id;
        [DdsId(1)]
        public int Value;
    }

    [DdsTopic("DeadlineMsg")]
    [DdsQos(DeadlineMs = 100)]
    public partial struct DeadlineMsg
    {
        [DdsId(0), DdsKey]
        public int Id;
        [DdsId(1)]
        public int Value;
    }

    /// <summary>
    /// Ownership, liveliness and deadline QoS, from [DdsQos] and from
    /// DdsWriterOptions / DdsReaderOptions.
    /// </summary>
    public class OwnershipLivelinessQosTests
    {
        private static string UniqueTopic(string prefix) => prefix + "_" + Guid.NewGuid().ToString("N");

        [Fact]
        public void ExclusiveOwnership_StrongerWriterWins_WeakerTakesOverAfterUnregister()
        {
            using var participant = new DdsParticipant();
            string topic = UniqueTopic("Owned");
            using var reader = new DdsReader<OwnedMsg>(participant, topic);
            using var backup = new DdsWriter<OwnedMsg>(participant, topic, new DdsWriterOptions { OwnershipStrength = 10 });
            using var primary = new DdsWriter<OwnedMsg>(participant, topic, new DdsWriterOptions { OwnershipStrength = 20 });

            var received = new List<int>();

            backup.Write(new OwnedMsg { Id = 1, Value = 1 });   // only writer so far: owns it
            received.AddRange(TakeValues(reader, expectAtLeast: 1));
            primary.Write(new OwnedMsg { Id = 1, Value = 2 });  // stronger: takes over
            received.AddRange(TakeValues(reader, expectAtLeast: 1));
            backup.Write(new OwnedMsg { Id = 1, Value = 3 });   // weaker: not the owner, dropped
            received.AddRange(TakeValues(reader, expectAtLeast: 0));

            // The owner hands over: unregister without disposing (AutodisposeUnregisteredInstances = false).
            primary.UnregisterInstance(new OwnedMsg { Id = 1 });
            backup.Write(new OwnedMsg { Id = 1, Value = 4 });
            received.AddRange(TakeValues(reader, expectAtLeast: 1));

            Assert.Equal(new[] { 1, 2, 4 }, received);
        }

        [Fact]
        public void ManualLiveliness_NotAsserted_ReaderSeesWriterGone()
        {
            using var participant = new DdsParticipant();
            string topic = UniqueTopic("Liveliness");
            using var reader = new DdsReader<ManualLivelinessMsg>(participant, topic);
            int notAliveEvents = 0;
            reader.LivelinessChanged += (_, s) => { if (s.NotAliveCountChange > 0) Interlocked.Increment(ref notAliveEvents); };
            using var writer = new DdsWriter<ManualLivelinessMsg>(participant, topic);

            writer.Write(new ManualLivelinessMsg { Id = 1, Value = 1 });
            Assert.Equal(DdsInstanceState.Alive, TakeLastState(reader));

            // No writes and no asserts for well over the 300 ms lease.
            Assert.True(WaitUntil(() => reader.LivelinessChangedStatus.NotAliveCount == 1, TimeSpan.FromSeconds(3)),
                "writer never lost liveliness");
            Assert.Equal(DdsInstanceState.NotAliveNoWriters, TakeLastState(reader));
            Assert.True(WaitUntil(() => Volatile.Read(ref notAliveEvents) > 0, TimeSpan.FromSeconds(1)), "LivelinessChanged not raised");
        }

        [Fact]
        public void ManualLiveliness_Asserted_WriterStaysAlive()
        {
            using var participant = new DdsParticipant();
            string topic = UniqueTopic("LivelinessAsserted");
            using var reader = new DdsReader<ManualLivelinessMsg>(participant, topic);
            using var writer = new DdsWriter<ManualLivelinessMsg>(participant, topic);

            writer.Write(new ManualLivelinessMsg { Id = 1, Value = 1 });
            for (int i = 0; i < 20; i++)   // ~1 s, more than three leases
            {
                writer.AssertLiveliness();
                Thread.Sleep(50);
            }

            var status = reader.LivelinessChangedStatus;
            Assert.Equal(1u, status.AliveCount);
            Assert.Equal(0u, status.NotAliveCount);
        }

        [Fact]
        public void Options_Liveliness_OverrideAttribute()
        {
            using var participant = new DdsParticipant();
            string topic = UniqueTopic("LivelinessOverride");
            // The type says manual-by-topic with a 300 ms lease; both sides override it to
            // automatic with an infinite lease, so the silent writer stays alive.
            var automatic = new DdsReaderOptions { LivelinessKind = DdsLivelinessKind.Automatic, LivelinessLeaseDuration = Timeout.InfiniteTimeSpan };
            using var reader = new DdsReader<ManualLivelinessMsg>(participant, topic, automatic);
            using var writer = new DdsWriter<ManualLivelinessMsg>(participant, topic,
                new DdsWriterOptions { LivelinessKind = DdsLivelinessKind.Automatic, LivelinessLeaseDuration = Timeout.InfiniteTimeSpan });

            writer.Write(new ManualLivelinessMsg { Id = 1, Value = 1 });
            Thread.Sleep(1000);   // more than three of the attribute's leases

            var status = reader.LivelinessChangedStatus;
            Assert.Equal(1u, status.AliveCount);
            Assert.Equal(0u, status.NotAliveCount);
            Assert.Equal(0u, writer.LivelinessLostStatus.TotalCount);
        }

        [Fact]
        public void Deadline_InstanceNotUpdated_ReaderAndWriterReportMissed()
        {
            using var participant = new DdsParticipant();
            string topic = UniqueTopic("Deadline");
            using var reader = new DdsReader<DeadlineMsg>(participant, topic);
            int readerEvents = 0;
            reader.RequestedDeadlineMissed += (_, _) => Interlocked.Increment(ref readerEvents);
            using var writer = new DdsWriter<DeadlineMsg>(participant, topic);

            writer.Write(new DeadlineMsg { Id = 1, Value = 1 });

            // No update for several 100 ms deadline periods.
            Assert.True(WaitUntil(() => reader.RequestedDeadlineMissedStatus.TotalCount > 0, TimeSpan.FromSeconds(3)),
                "reader never reported a missed deadline");
            Assert.True(WaitUntil(() => writer.OfferedDeadlineMissedStatus.TotalCount > 0, TimeSpan.FromSeconds(3)),
                "writer never reported a missed deadline");
            Assert.True(WaitUntil(() => Volatile.Read(ref readerEvents) > 0, TimeSpan.FromSeconds(1)), "RequestedDeadlineMissed not raised");
        }

        [Fact]
        public void Deadline_UpdatedInTime_NothingMissed()
        {
            using var participant = new DdsParticipant();
            string topic = UniqueTopic("DeadlineMet");
            using var reader = new DdsReader<DeadlineMsg>(participant, topic,
                new DdsReaderOptions { Deadline = TimeSpan.FromMilliseconds(500) });
            using var writer = new DdsWriter<DeadlineMsg>(participant, topic,
                new DdsWriterOptions { Deadline = TimeSpan.FromMilliseconds(500) });

            for (int i = 0; i < 10; i++)   // ~0.5 s of updates every 50 ms
            {
                writer.Write(new DeadlineMsg { Id = 1, Value = i });
                Thread.Sleep(50);
            }

            Assert.Equal(0u, reader.RequestedDeadlineMissedStatus.TotalCount);
        }

        /// <summary>
        /// Why there is no autopurge option: Cyclone drops a reader's instance by itself once
        /// all its samples are taken and no writer is left (disposed or not).
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void InstanceFreed_OnceTakenAndNoWriterLeft(bool disposeFirst)
        {
            using var participant = new DdsParticipant();
            string topic = UniqueTopic("InstanceFreed");
            using var reader = new DdsReader<LifecycleNoAutodisposeMsg>(participant, topic);
            using var writer = new DdsWriter<LifecycleNoAutodisposeMsg>(participant, topic);

            var sample = new LifecycleNoAutodisposeMsg { Id = 1, Value = 1 };
            writer.Write(sample);
            Assert.True(WaitUntil(() => !reader.LookupInstance(sample).IsNil, TimeSpan.FromSeconds(2)));
            if (disposeFirst) writer.DisposeInstance(sample);
            using (reader.Take()) { }
            Assert.False(reader.LookupInstance(sample).IsNil); // the writer is still registered

            writer.UnregisterInstance(sample);

            Assert.True(WaitUntil(() =>
            {
                using (reader.Take()) { }
                return reader.LookupInstance(sample).IsNil;
            }, TimeSpan.FromSeconds(2)), "instance was not freed");
        }

        [Fact]
        public void WriterOptions_AutodisposeFalse_OverridesAttributeDefault()
        {
            using var participant = new DdsParticipant();
            string topic = UniqueTopic("AutodisposeOverride");
            using var reader = new DdsReader<LifecycleDefaultMsg>(participant, topic);
            using var writer = new DdsWriter<LifecycleDefaultMsg>(participant, topic,
                new DdsWriterOptions { AutodisposeUnregisteredInstances = false });

            var sample = new LifecycleDefaultMsg { Id = 1, Value = 10 };
            writer.Write(sample);
            Assert.Equal(DdsInstanceState.Alive, TakeLastState(reader));

            writer.UnregisterInstance(sample);
            Assert.Equal(DdsInstanceState.NotAliveNoWriters, TakeLastState(reader));
        }

        [Fact]
        public void Options_Null_Throws()
        {
            using var participant = new DdsParticipant();
            Assert.Throws<ArgumentNullException>(() => new DdsWriter<OwnedMsg>(participant, "x", (DdsWriterOptions)null!));
            Assert.Throws<ArgumentNullException>(() => new DdsReader<OwnedMsg>(participant, "x", (DdsReaderOptions)null!));
        }

        private static List<int> TakeValues(DdsReader<OwnedMsg> reader, int expectAtLeast)
        {
            var values = new List<int>();
            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(expectAtLeast > 0 ? 2000 : 200);
            while (DateTime.UtcNow < deadline)
            {
                using (var scope = reader.Take())
                {
                    for (int i = 0; i < scope.Count; i++)
                        if (scope.Infos[i].ValidData != 0) values.Add(scope[i].Value);
                }
                if (expectAtLeast > 0 && values.Count >= expectAtLeast) break;
                Thread.Sleep(20);
            }
            return values;
        }

        private static DdsInstanceState TakeLastState<T>(DdsReader<T> reader) where T : new()
        {
            for (int i = 0; i < 100; i++)
            {
                using var scope = reader.Take();
                if (scope.Count > 0) return scope.Infos[scope.Count - 1].InstanceState;
                Thread.Sleep(20);
            }
            throw new TimeoutException("no sample received");
        }

        private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
        {
            var end = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < end)
            {
                if (condition()) return true;
                Thread.Sleep(20);
            }
            return condition();
        }
    }
}
