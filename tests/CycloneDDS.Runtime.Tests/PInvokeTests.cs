using System;
using Xunit;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime.Tests
{
    public class PInvokeTests
    {
        [Fact]
        public void CanLoadLibrary()
        {
            try 
            {
               // This triggers loading the DLL
               var p = DdsApi.dds_create_participant(0, IntPtr.Zero, IntPtr.Zero);
               if (p.IsValid)
               {
                   DdsApi.dds_delete(p);
               }
            }
            catch (DllNotFoundException ex)
            {
                Assert.Fail($"Could not load ddsc.dll: {ex.Message}");
            }
        }
        
        [Fact]
        public void CreateParticipant_ReturnsValidHandle()
        {
            // Use domain 0 which is default
            var p = DdsApi.dds_create_participant(0, IntPtr.Zero, IntPtr.Zero);
            
            Assert.True(p.IsValid, "Participant handle should be valid");
            Assert.True(p.Handle != IntPtr.Zero);
            
            var ret = DdsApi.dds_delete(p);
            Assert.Equal(0, ret); // DDS_RETCODE_OK
        }
        
        [Fact]
        public void DdsEntityHandle_DisposesCorrectly()
        {
            var p = DdsApi.dds_create_participant(0, IntPtr.Zero, IntPtr.Zero);
            Assert.True(p.IsValid);
            
            var rawHandle = p;

            using (var handle = new DdsEntityHandle(p))
            {
                Assert.Equal(rawHandle.Handle, handle.NativeHandle.Handle);
            }
            
            // At this point, entity should be deleted.
            // Trying to delete it again should return error (DDS_RETCODE_ALREADY_DELETED = -4 or similar)
            // Note: dds_delete returns int return code.
            
            var ret = DdsApi.dds_delete(rawHandle);
            // We expect it to NOT be OK (0)
            Assert.NotEqual(0, ret); 
        }

        [Fact]
        public void CreateTopic_SignatureTest()
        {
            var p = DdsApi.dds_create_participant(0, IntPtr.Zero, IntPtr.Zero);
            Assert.True(p.IsValid);
            
            // Pass minimal invalid args to check for crash
            // If signature is wrong (e.g. alignment), this might crash.
            // If signature is right, it returns BadParameter (handle < 0)
            var topic = DdsApi.dds_create_topic(p, IntPtr.Zero, null, IntPtr.Zero, IntPtr.Zero);
            
            // Expected: Invalid, but NO CRASH.
            Assert.False(topic.IsValid);
            
            DdsApi.dds_delete(p);
        }

        [Fact]
        public void CreateTopic_Success()
        {
            var p = DdsApi.dds_create_participant(0, IntPtr.Zero, IntPtr.Zero);
            Assert.True(p.IsValid);
            
            using var desc = new DescriptorContainer(TestMessage.GetDescriptorOps(), 8, 4, 16);
            var topic = DdsApi.dds_create_topic(p, desc.Ptr, "MockTopicPInvoke", IntPtr.Zero, IntPtr.Zero);
            
            // If this fails, then topic creation is broken (signature or logic)
            Assert.True(topic.IsValid, $"Topic handle invalid: {topic.Handle}");
            
            if (topic.IsValid) DdsApi.dds_delete(topic);
            DdsApi.dds_delete(p);
        }
    }
}
