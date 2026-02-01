using System;
using System.Threading;
using Xunit;
using CycloneDDS.Runtime;
using CycloneDDS.Core;

namespace CycloneDDS.Runtime.Tests
{
    public class StringTests
    {
        [Fact]
        public void Xcdr2_String_RoundTrip_Works()
        {
            using var participant = new DdsParticipant(0);
            
            // Topic name
            string topicName = "StringRoundtripTopic";

            using var writer = new DdsWriter<StringMessage>(participant, topicName);
            using var reader = new DdsReader<StringMessage>(participant, topicName);
            
            // Wait for discovery (match)
            Thread.Sleep(2000);
            
            var sent = new StringMessage { Id = 100, Msg = "Hello World XCDR2" };
            writer.Write(sent);
            
            // Wait for data
            Thread.Sleep(1000);
            
            using var samples = reader.Take();
            
            bool found = false;
            for (int i = 0; i < samples.Count; i++)
            {
               if (samples.Infos[i].ValidData != 0)
               {
                   Assert.Equal(100, samples[i].Id);
                   Assert.Equal("Hello World XCDR2", samples[i].Msg);
                   found = true;
               }
            }
            
            Assert.True(found, "Did not receive valid data");
        }
    }
}