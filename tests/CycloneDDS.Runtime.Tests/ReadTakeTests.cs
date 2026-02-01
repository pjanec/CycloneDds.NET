using System;
using System.Threading;
using Xunit;
using CycloneDDS.Runtime;

namespace CycloneDDS.Runtime.Tests
{
    public class ReadTakeTests : IDisposable
    {
        private DdsParticipant _participant;
        private DdsWriter<TestMessage> _writer;
        private DdsReader<TestMessage> _reader;
        private const string TopicName = "ReadTakeTopic";

        public ReadTakeTests()
        {
            _participant = new DdsParticipant();
            _writer = new DdsWriter<TestMessage>(_participant, TopicName);
            _reader = new DdsReader<TestMessage>(_participant, TopicName);
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _participant?.Dispose();
        }

        [Fact]
        public void NonDestructiveRead_DoesNotRemoveSamples()
        {
            var msg = new TestMessage { Id = 1, Value = 100 };
            _writer.Write(msg);
            
            Thread.Sleep(200);

            // 1. Read (should see data)
            using var view1 = _reader.Read();
            Assert.True(view1.Count > 0, "First Read should return samples");
            Assert.Equal(1, view1[0].Id);
            
            // State check: First read might show NotRead if we haven't touched it, 
            // but the act of reading sets it to Read for SUBSEQUENT access? 
            // Actually, the returned SampleInfo reflects the state AT THE MOMENT of access.
            Assert.Equal(DdsSampleState.NotRead, view1.Infos[0].SampleState); 

            // 2. Read again (should still see data because it was READ, not TAKEN)
            using var view2 = _reader.Read();
            Assert.True(view2.Count > 0, "Second Read should return samples");
            Assert.Equal(1, view2[0].Id);
            
            // State should be READ now
            Assert.Equal(DdsSampleState.Read, view2.Infos[0].SampleState);
        }

        [Fact]
        public void Take_RemovesSamples()
        {
            var msg = new TestMessage { Id = 2, Value = 200 };
            _writer.Write(msg);
            Thread.Sleep(200);

            // 1. Take (should see data)
            using var view1 = _reader.Take();
            Assert.True(view1.Count > 0, "Take should return samples");
            
            // 2. Take again (should be empty as data was removed)
            using var view2 = _reader.Take();
            Assert.Equal(0, view2.Count);
        }
        
        [Fact]
        public void Masks_FilterByState()
        {
             var msg = new TestMessage { Id = 3, Value = 300 };
            _writer.Write(msg);
            Thread.Sleep(200);

            // 1. Read to mark as READ
            using var view1 = _reader.Read();
            Assert.True(view1.Count > 0);
            
            // 2. Read with mask NOT_READ (should be empty because it is now READ)
            using var view2 = _reader.Read(32, DdsSampleState.NotRead, DdsViewState.AnyViewState, DdsInstanceState.AnyInstanceState);
            Assert.Equal(0, view2.Count);
            
             // 3. Read with mask READ (should see it)
            using var view3 = _reader.Read(32, DdsSampleState.Read, DdsViewState.AnyViewState, DdsInstanceState.AnyInstanceState);
            Assert.True(view3.Count > 0);
        }
    }
}
