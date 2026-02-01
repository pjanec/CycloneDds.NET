using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using CycloneDDS.Runtime;
using CycloneDDS.Core;
using CycloneDDS.Runtime.Interop;
using Probe; 

namespace CycloneDDS.Runtime.Tests
{
    public class LayoutProbeTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly DdsParticipant _participant;

        public LayoutProbeTests(ITestOutputHelper output)
        {
            _output = output;
            _participant = new DdsParticipant();
        }

        public void Dispose()
        {
            _participant.Dispose();
        }

        [Fact]
        public void ProbeBufferLayout()
        {
            var topicName = "SequenceProbeTopic";
            var writer = new DdsWriter<SequenceProbe>(_participant, topicName);
            var reader = new DdsReader<SequenceProbe>(_participant, topicName);

            // Wait for match
            for(int i=0; i<50; i++) {
                if(GetMatchCount(writer) > 0 && GetMatchCount(reader) > 0) break;
                System.Threading.Thread.Sleep(100);
            }
            
            var sample = new SequenceProbe();
            sample.P1 = 0x11111111;
            sample.Seq = new List<int> { 1, 2, 3 }; // Content doesn't matter much
            sample.P2 = 0x22222222;

            writer.Write(sample);

            // Wait for data
            System.Threading.Thread.Sleep(1000);

            // Read Raw
            IntPtr[] samples = new IntPtr[1];
            DdsApi.DdsSampleInfo[] infos = new DdsApi.DdsSampleInfo[1];
            
            // Get Native Handle using Reflection
            var handleField = typeof(DdsReader<SequenceProbe>).GetField("_readerHandle", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(handleField);
            var entityHandleObj = handleField.GetValue(reader); 
            Assert.NotNull(entityHandleObj);
            
            var nativeHandleProp = entityHandleObj.GetType().GetProperty("NativeHandle");
            Assert.NotNull(nativeHandleProp);
            
            var ddsEntity = (DdsApi.DdsEntity)nativeHandleProp.GetValue(entityHandleObj);
            int readerId = ddsEntity.Handle;

            int rc = 0;
            for (int i = 0; i < 50; i++)
            {
                rc = DdsApi.dds_take(readerId, samples, infos, (UIntPtr)1, 1);
                if (rc > 0) break;
                System.Threading.Thread.Sleep(100);
            }
            if (rc <= 0)
               throw new Exception($"dds_take returned {rc} samples. Writer count: {GetMatchCount(writer)}, Reader count: {GetMatchCount(reader)}");

            // Dump Memory
            IntPtr ptr = samples[0];
            _output.WriteLine($"Sample Ptr: {ptr.ToString("X")}");
            
            byte[] buffer = new byte[128];
            Marshal.Copy(ptr, buffer, 0, 128);
            
            _output.WriteLine("Hex Dump:");
            for(int i=0; i<buffer.Length; i+=16)
            {
                 string hex = BitConverter.ToString(buffer, i, Math.Min(16, buffer.Length - i)).Replace("-", " ");
                 string ascii = "";
                 for(int j=i; j < Math.Min(i+16, buffer.Length); j++) {
                     char c = (char)buffer[j];
                     ascii += (c >= 32 && c < 127) ? c : '.';
                 }
                 _output.WriteLine($"{i:X4}: {hex,-48} | {ascii}");
            }
            
            // Clean up loan
             DdsApi.dds_return_loan(readerId, samples, rc);
        }

        private int GetMatchCount<T>(DdsWriter<T> writer) where T:struct {
            DdsApi.DdsPublicationMatchedStatus status;
            // Need reflection or DdsApi manual call. DdsWriter doesn't expose Handle elegantly public
            var handleField = typeof(DdsWriter<T>).GetField("_writerHandle", BindingFlags.NonPublic | BindingFlags.Instance);
            var entityHandleObj = handleField.GetValue(writer);
            var nativeHandleProp = entityHandleObj.GetType().GetProperty("NativeHandle");
            var ddsEntity = (DdsApi.DdsEntity)nativeHandleProp.GetValue(entityHandleObj);
            
            DdsApi.dds_get_publication_matched_status(ddsEntity.Handle, out status);
            return (int)status.CurrentCount;
        }

        private int GetMatchCount<T>(DdsReader<T> reader) where T:struct {
            DdsApi.DdsSubscriptionMatchedStatus status;
            var handleField = typeof(DdsReader<T>).GetField("_readerHandle", BindingFlags.NonPublic | BindingFlags.Instance);
            var entityHandleObj = handleField.GetValue(reader);
            var nativeHandleProp = entityHandleObj.GetType().GetProperty("NativeHandle");
            var ddsEntity = (DdsApi.DdsEntity)nativeHandleProp.GetValue(entityHandleObj);
            
            DdsApi.dds_get_subscription_matched_status(ddsEntity.Handle, out status);
            return (int)status.CurrentCount;
        }
    }
}
