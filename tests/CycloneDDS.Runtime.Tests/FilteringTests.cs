using System;
using System.Threading;
using Xunit;
using CycloneDDS.Runtime;
using CycloneDDS.Core;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime.Tests
{
    public class FilteringTests : IDisposable
    {
        private DdsParticipant _participant;
        private string _topicName;

        public FilteringTests()
        {
            _participant = new DdsParticipant();
            _topicName = "FiltTest_" + Guid.NewGuid();
        }

        public void Dispose()
        {
            _participant?.Dispose();
        }

        [Fact]
        public void Filter_Applied_OnlyMatchingSamples()
        {
             // Use History to ensure writer doesn't overwrite
             var qos = DdsApi.dds_create_qos();
             DdsApi.dds_qset_history(qos, DdsApi.DDS_HISTORY_KEEP_ALL, 0);

             using var reader = new DdsReader<TestMessage>(_participant, _topicName, qos);
             using var writer = new DdsWriter<TestMessage>(_participant, _topicName, qos);
             DdsApi.dds_delete_qos(qos);

             // Write 1, 5, 10
             writer.Write(new TestMessage { Id = 1, Value = 1 });
             writer.Write(new TestMessage { Id = 2, Value = 5 });
             writer.Write(new TestMessage { Id = 3, Value = 10 });
             
             // Sleep to propagate
             Thread.Sleep(1000);

             // Filter > 3
             reader.SetFilter(v => v.Value > 3);
             
             using var scope = reader.Take();
             
             // Iteration should yield 5 and 10 only (2 samples)
             // But scope.Count matches underlying native take count (3).
             // We must verify iteration count.
             
             int count = 0;
             foreach (var item in scope)
             {
                 count++;
                 Assert.True(item.Value > 3);
             }
             Assert.Equal(2, count);
        }

        [Fact]
        public void Filter_UpdatedAtRuntime_NewFilterApplied()
        {
             var qos = DdsApi.dds_create_qos();
             DdsApi.dds_qset_history(qos, DdsApi.DDS_HISTORY_KEEP_ALL, 0);

             using var reader = new DdsReader<TestMessage>(_participant, _topicName, qos);
             using var writer = new DdsWriter<TestMessage>(_participant, _topicName, qos);
             DdsApi.dds_delete_qos(qos);

             // Batch 1
             writer.Write(new TestMessage { Value = 1 });
             writer.Write(new TestMessage { Value = 5 });
             writer.Write(new TestMessage { Value = 10 });
             Thread.Sleep(200);

             // Filter > 5 -> Expect 10
             reader.SetFilter(v => v.Value > 5);
             using (var scope = reader.Take())
             {
                 int c = 0;
                 foreach(var x in scope) { c++; Assert.Equal(10, x.Value); }
                 Assert.Equal(1, c);
             }

             // Batch 2
             writer.Write(new TestMessage { Value = 1 });
             writer.Write(new TestMessage { Value = 5 });
             writer.Write(new TestMessage { Value = 10 });
             Thread.Sleep(200);

             // Filter < 8 -> Expect 1, 5 (from new batch)
             reader.SetFilter(v => v.Value < 8);
             using (var scope = reader.Take())
             {
                 int c = 0;
                 foreach(var x in scope) 
                 { 
                     c++; 
                     Assert.True(x.Value < 8);
                 }
                 Assert.Equal(2, c);
             }
        }

        [Fact]
        public void Filter_Null_AllSamplesReturned()
        {
             var qos = DdsApi.dds_create_qos();
             DdsApi.dds_qset_history(qos, DdsApi.DDS_HISTORY_KEEP_ALL, 0);

             using var reader = new DdsReader<TestMessage>(_participant, _topicName, qos);
             using var writer = new DdsWriter<TestMessage>(_participant, _topicName, qos);
             DdsApi.dds_delete_qos(qos);

             // Set Filter "False"
             reader.SetFilter(v => false);
             
             writer.Write(new TestMessage { Value = 1 });
             writer.Write(new TestMessage { Value = 2 });
             writer.Write(new TestMessage { Value = 3 });
             Thread.Sleep(500);

             // Verify empty
             using (var scope = reader.Read()) // Read maintains data
             {
                 int c = 0;
                 foreach(var x in scope) c++;
                 Assert.Equal(0, c);
             }

             // Set null
             reader.SetFilter(null);
             
             using (var scope = reader.Take())
             {
                 int c = 0;
                 foreach(var x in scope) c++;
                 Assert.Equal(3, c);
             }
        }
    }
}
