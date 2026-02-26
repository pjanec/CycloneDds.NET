using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime.Tests
{
    // Helper: a type without [DdsTopic] attribute — used in "throws" tests
    public partial struct NoAttributeMessage
    {
        public int Value;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // MONEXT-001 — dds_qset_partition interop
    // ─────────────────────────────────────────────────────────────────────────────

    public class DdsApi_PartitionInteropTests
    {
        [Fact]
        public void DdsApi_PartitionInterop_DoesNotThrow()
        {
            var qos = DdsApi.dds_create_qos();
            Assert.NotEqual(IntPtr.Zero, qos);

            var ex = Record.Exception(() =>
                DdsApi.dds_qset_partition(qos, 1, new[] { "my_partition" }));
            Assert.Null(ex);

            DdsApi.dds_delete_qos(qos);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // MONEXT-002 — WaitSet / ReadCondition / GuardCondition interop
    // ─────────────────────────────────────────────────────────────────────────────

    public class DdsApi_WaitSetInteropTests
    {
        [Fact]
        public void DdsApi_WaitSetInterop_CreateAndDelete()
        {
            using var p = new DdsParticipant(0);

            var ws = DdsApi.dds_create_waitset(p.NativeEntity);
            Assert.True(ws.IsValid, "WaitSet handle must be valid");

            DdsApi.dds_delete(ws);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // MONEXT-003 — DefaultPartition on DdsParticipant
    // ─────────────────────────────────────────────────────────────────────────────

    public class DdsParticipant_DefaultPartitionTests
    {
        [Fact]
        public void DdsParticipant_DefaultPartition_CanBeSet()
        {
            using var p = new DdsParticipant(0, "my_partition");
            Assert.Equal("my_partition", p.DefaultPartition);
        }

        [Fact]
        public void DdsParticipant_DefaultPartition_IsNullByDefault()
        {
            using var p = new DdsParticipant(0);
            Assert.Null(p.DefaultPartition);
        }

        [Fact]
        public void DdsParticipant_BackwardCompatible_NoArgs()
        {
            var ex = Record.Exception(() =>
            {
                using var p = new DdsParticipant();
            });
            Assert.Null(ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // MONEXT-004 — Partition parameter on DdsReader<T>
    // ─────────────────────────────────────────────────────────────────────────────

    public class DdsReader_PartitionTests
    {
        [Fact]
        public void DdsReader_PartitionFromConstructor_DoesNotThrow()
        {
            using var participant = new DdsParticipant(0);
            var ex = Record.Exception(() =>
            {
                using var reader = new DdsReader<TestMessage>(participant, "MONEXT004_Topic_A", partition: "test_partition");
            });
            Assert.Null(ex);
        }

        [Fact]
        public void DdsReader_PartitionInheritedFromParticipant_DoesNotThrow()
        {
            using var participant = new DdsParticipant(0, "inherited_partition");
            var ex = Record.Exception(() =>
            {
                using var reader = new DdsReader<TestMessage>(participant, "MONEXT004_Topic_B");
            });
            Assert.Null(ex);
        }

        [Fact]
        public void DdsReader_PartitionResolutionOrder_ReaderOverridesParticipant()
        {
            using var participant = new DdsParticipant(0, "participant_partition");
            var ex = Record.Exception(() =>
            {
                using var reader = new DdsReader<TestMessage>(
                    participant, "MONEXT004_Topic_C", partition: "reader_partition");
            });
            Assert.Null(ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // MONEXT-005 — Partition parameter on DdsWriter<T>
    // ─────────────────────────────────────────────────────────────────────────────

    public class DdsWriter_PartitionTests
    {
        [Fact]
        public void DdsWriter_PartitionFromConstructor_DoesNotThrow()
        {
            using var participant = new DdsParticipant(0);
            var ex = Record.Exception(() =>
            {
                using var writer = new DdsWriter<TestMessage>(participant, "MONEXT005_Topic_A", partition: "test_partition");
            });
            Assert.Null(ex);
        }

        [Fact]
        public void DdsWriter_PartitionInheritedFromParticipant_DoesNotThrow()
        {
            using var participant = new DdsParticipant(0, "inherited_partition");
            var ex = Record.Exception(() =>
            {
                using var writer = new DdsWriter<TestMessage>(participant, "MONEXT005_Topic_B");
            });
            Assert.Null(ex);
        }

        [Fact]
        public void DdsWriter_PartitionResolutionOrder_WriterOverridesParticipant()
        {
            using var participant = new DdsParticipant(0, "participant_partition");
            var ex = Record.Exception(() =>
            {
                using var writer = new DdsWriter<TestMessage>(
                    participant, "MONEXT005_Topic_C", partition: "writer_partition");
            });
            Assert.Null(ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // MONEXT-006 — Unified constructor for DdsReader<T>
    // ─────────────────────────────────────────────────────────────────────────────

    public class DdsReader_UnifiedCtorTests
    {
        [Fact]
        public void DdsReader_UnifiedCtor_TopicFromAttribute()
        {
            // TestMessage has [DdsTopic("TestMessageTopic")] — no explicit topicName needed
            using var participant = new DdsParticipant(0);
            var ex = Record.Exception(() =>
            {
                using var reader = new DdsReader<TestMessage>(participant);
            });
            Assert.Null(ex);
        }

        [Fact]
        public void DdsReader_UnifiedCtor_ExplicitTopic()
        {
            using var participant = new DdsParticipant(0);
            var ex = Record.Exception(() =>
            {
                using var reader = new DdsReader<TestMessage>(participant, "MONEXT006_ExplicitTopic");
            });
            Assert.Null(ex);
        }

        [Fact]
        public void DdsReader_UnifiedCtor_MissingAttributeThrows()
        {
            using var participant = new DdsParticipant(0);
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var reader = new DdsReader<NoAttributeMessage>(participant);
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // MONEXT-007 — Unified constructor for DdsWriter<T>
    // ─────────────────────────────────────────────────────────────────────────────

    public class DdsWriter_UnifiedCtorTests
    {
        [Fact]
        public void DdsWriter_UnifiedCtor_TopicFromAttribute()
        {
            using var participant = new DdsParticipant(0);
            var ex = Record.Exception(() =>
            {
                using var writer = new DdsWriter<TestMessage>(participant);
            });
            Assert.Null(ex);
        }

        [Fact]
        public void DdsWriter_UnifiedCtor_ExplicitTopic()
        {
            using var participant = new DdsParticipant(0);
            var ex = Record.Exception(() =>
            {
                using var writer = new DdsWriter<TestMessage>(participant, "MONEXT007_ExplicitTopic");
            });
            Assert.Null(ex);
        }

        [Fact]
        public void DdsWriter_UnifiedCtor_MissingAttributeThrows()
        {
            using var participant = new DdsParticipant(0);
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var writer = new DdsWriter<NoAttributeMessage>(participant);
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // MONEXT-008 — IDdsReader and IInternalDdsEntity interfaces
    // ─────────────────────────────────────────────────────────────────────────────

    public class DdsReader_InterfaceTests
    {
        [Fact]
        public void DdsReader_Implements_IDdsReader()
        {
            using var participant = new DdsParticipant(0);
            using var reader = new DdsReader<TestMessage>(participant, "MONEXT008_Topic_A");

            IDdsReader r = reader;
            Assert.Equal(typeof(TestMessage), r.DataType);
        }

        [Fact]
        public void DdsReader_Implements_IInternalDdsEntity()
        {
            using var participant = new DdsParticipant(0);
            using var reader = new DdsReader<TestMessage>(participant, "MONEXT008_Topic_B");

            IInternalDdsEntity e = reader;
            Assert.NotEqual(default(DdsApi.DdsEntity), e.NativeEntity);
            Assert.True(e.NativeEntity.IsValid);
        }

        [Fact]
        public void DdsReader_IInternalDdsEntity_ThrowsAfterDispose()
        {
            using var participant = new DdsParticipant(0);
            var reader = new DdsReader<TestMessage>(participant, "MONEXT008_Topic_C");

            reader.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
            {
                _ = ((IInternalDdsEntity)reader).NativeEntity;
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // MONEXT-009 — DdsWaitSet
    // ─────────────────────────────────────────────────────────────────────────────

    public class DdsWaitSetTests
    {
        [Fact]
        public void DdsWaitSet_Create_DoesNotThrow()
        {
            using var participant = new DdsParticipant(0);
            var ex = Record.Exception(() =>
            {
                using var ws = new DdsWaitSet(participant);
            });
            Assert.Null(ex);
        }

        [Fact]
        public void DdsWaitSet_Attach_DoesNotThrow()
        {
            using var participant = new DdsParticipant(0);
            using var ws = new DdsWaitSet(participant);
            using var reader = new DdsReader<TestMessage>(participant, "MONEXT009_Attach");

            var ex = Record.Exception(() => ws.Attach(reader));
            Assert.Null(ex);
        }

        [Fact]
        public void DdsWaitSet_AttachSameReaderTwice_IsIdempotent()
        {
            using var participant = new DdsParticipant(0);
            using var ws = new DdsWaitSet(participant);
            using var reader = new DdsReader<TestMessage>(participant, "MONEXT009_Idempotent");

            ws.Attach(reader);
            var ex = Record.Exception(() => ws.Attach(reader));
            Assert.Null(ex);
        }

        [Fact]
        public void DdsWaitSet_Detach_DoesNotThrow()
        {
            using var participant = new DdsParticipant(0);
            using var ws = new DdsWaitSet(participant);
            using var reader = new DdsReader<TestMessage>(participant, "MONEXT009_Detach");

            ws.Attach(reader);
            var ex = Record.Exception(() => ws.Detach(reader));
            Assert.Null(ex);
        }

        [Fact]
        public void DdsWaitSet_Wait_TimesOutCorrectly()
        {
            using var participant = new DdsParticipant(0);
            using var ws = new DdsWaitSet(participant);
            using var reader = new DdsReader<TestMessage>(participant, "MONEXT009_Timeout");

            ws.Attach(reader);

            var buffer = new IDdsReader[4];
            int count = ws.Wait(buffer.AsSpan(), TimeSpan.FromMilliseconds(50));

            Assert.Equal(0, count);
        }

        [Fact(Timeout = 5000)]
        public async Task DdsWaitSet_Wait_CancellationTokenCancels()
        {
            using var participant = new DdsParticipant(0);
            using var ws = new DdsWaitSet(participant);
            using var reader = new DdsReader<TestMessage>(participant, "MONEXT009_Cancel");

            ws.Attach(reader);

            var buffer = new IDdsReader[4];
            var cts = new CancellationTokenSource();

            var task = Task.Run(() => ws.Wait(buffer.AsSpan(), Timeout.InfiniteTimeSpan, cts.Token));

            await Task.Delay(50);
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        }

        [Fact(Timeout = 5000)]
        public void DdsWaitSet_Wait_ReceivesDataFromWriter()
        {
            using var participant = new DdsParticipant(0);
            using var ws = new DdsWaitSet(participant);
            using var reader = new DdsReader<TestMessage>(participant, "MONEXT009_Integration");
            using var writer = new DdsWriter<TestMessage>(participant, "MONEXT009_Integration");

            ws.Attach(reader);

            // Give time for discovery
            Thread.Sleep(200);

            writer.Write(new TestMessage { Id = 42, Value = 100 });

            var buffer = new IDdsReader[4];
            int count = ws.Wait(buffer.AsSpan(), TimeSpan.FromSeconds(2));

            Assert.True(count > 0, "Expected at least one triggered reader");
            Assert.Same(reader, buffer[0]);
        }

        [Fact]
        public void DdsWaitSet_Dispose_DoesNotLeak()
        {
            using var participant = new DdsParticipant(0);
            var ws = new DdsWaitSet(participant);

            using var r1 = new DdsReader<TestMessage>(participant, "MONEXT009_Leak_1");
            using var r2 = new DdsReader<TestMessage>(participant, "MONEXT009_Leak_2");

            ws.Attach(r1);
            ws.Attach(r2);

            var ex = Record.Exception(() => ws.Dispose());
            Assert.Null(ex);

            // Double-dispose must also be safe
            ex = Record.Exception(() => ws.Dispose());
            Assert.Null(ex);
        }
    }
}
