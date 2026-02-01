using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using CycloneDDS.Runtime;
using CycloneDDS.Core;
using CycloneDDS.Runtime.Interop;
using System.Reflection;

namespace CycloneDDS.Runtime.Tests
{
    public class AsyncTests : IDisposable
    {
        private DdsParticipant _participant;
        private string _topicName;

        public AsyncTests()
        {
            _participant = new DdsParticipant();
            _topicName = "AsynTest_" + Guid.NewGuid();
        }

        public void Dispose()
        {
            _participant?.Dispose();
        }

        [Fact]
        public async Task WaitDataAsync_CompletesWhenDataArrives()
        {
            using var reader = new DdsReader<TestMessage>(_participant, _topicName);
            using var writer = new DdsWriter<TestMessage>(_participant, _topicName);

            // Start waiting
            var waitTask = reader.WaitDataAsync();
            
            // Give it a moment to block
            await Task.Delay(100);
            Assert.False(waitTask.IsCompleted);

            // Write
            writer.Write(new TestMessage { Id = 1, Value = 100 });

            // Wait for completion
            var compl = await Task.WhenAny(waitTask, Task.Delay(2000));
            Assert.Equal(waitTask, compl);
            
            VerifySingleSample(reader);
        }

        private void VerifySingleSample(DdsReader<TestMessage> reader)
        {
            using var scope = reader.Take();
            Assert.Equal(1, scope.Count);
        }

        [Fact]
        public async Task WaitDataAsync_RespectsCancellation()
        {
             using var reader = new DdsReader<TestMessage>(_participant, _topicName);
             using var cts = new CancellationTokenSource(200);
             
             await Assert.ThrowsAsync<TaskCanceledException>(async () => 
                 await reader.WaitDataAsync(cts.Token));
        }

        [Fact]
        public void Polling_NoListener_NoOverhead()
        {
             using var reader = new DdsReader<TestMessage>(_participant, _topicName);
             
             // Use reflection to check _listener field
             var field = typeof(DdsReader<TestMessage>)
                 .GetField("_listener", BindingFlags.NonPublic | BindingFlags.Instance);
             
             IntPtr listener = (IntPtr)field.GetValue(reader);
             Assert.Equal(IntPtr.Zero, listener);
        }

        [Fact]
        public async Task DisposeWithListener_NoLeaks()
        {
             using (var reader = new DdsReader<TestMessage>(_participant, _topicName))
             {
                 var t = reader.WaitDataAsync(); 
                 // It created listener
             }
             // Should not crash on dispose
        }

        [Fact]
        public async Task StreamAsync_YieldsMultipleSamples()
        {
             // Use KeepAll to ensure we don't drop samples if Reader is slow/late
             var qos = DdsApi.dds_create_qos();
             DdsApi.dds_qset_history(qos, DdsApi.DDS_HISTORY_KEEP_ALL, 0);

             using var reader = new DdsReader<TestMessage>(_participant, _topicName, qos);
             using var writer = new DdsWriter<TestMessage>(_participant, _topicName, qos);
             DdsApi.dds_delete_qos(qos);
             
             // Write 3 samples
             for (int i=0; i<3; i++) writer.Write(new TestMessage { Id = i });

             int count = 0;
             using var cts = new CancellationTokenSource(5000); // 5s timeout

             // Consume
             try 
             {
                 await foreach (var msg in reader.StreamAsync(cts.Token))
                 {
                     count++;
                     if (count == 3) cts.Cancel(); 
                 }
             }
             catch(TaskCanceledException) {}
             
             Assert.Equal(3, count);
        }
    }
}
