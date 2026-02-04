using System;
using System.Collections.Generic;
using Xunit;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime.Tests
{
    public class ManagedCopyTests : IDisposable
    {
        private readonly DdsParticipant _participant;
        private readonly DdsWriter<TestMessage> _writer;
        private readonly DdsReader<TestMessage> _reader;

        public ManagedCopyTests()
        {
            try 
            {
                _participant = new DdsParticipant();
                var topicName = "ManagedCopyTests_Topic_" + Guid.NewGuid();
                
                var qos = DdsApi.dds_create_qos();
                DdsApi.dds_qset_history(qos, DdsApi.DDS_HISTORY_KEEP_ALL, 0);

                _writer = new DdsWriter<TestMessage>(_participant, topicName, qos);
                _reader = new DdsReader<TestMessage>(_participant, topicName, qos);
                
                DdsApi.dds_delete_qos(qos);
            }
            catch(Exception)
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _participant?.Dispose();
        }


    }
}
