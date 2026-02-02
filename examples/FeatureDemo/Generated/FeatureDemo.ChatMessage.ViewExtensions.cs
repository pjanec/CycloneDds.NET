using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace FeatureDemo
{
    public static class ChatMessageExtensions
    {
        public static ChatMessageView AsView(this DdsSample<ChatMessage> sample)
        {
            unsafe { return new ChatMessageView((ChatMessage_Native*)sample.NativePtr); }
        }

        public static System.Collections.Generic.List<ChatMessage> ReadCopied(this DdsReader<ChatMessage> reader, int maxSamples = 32)
        {
            using var samples = reader.Read(maxSamples);
            var result = new System.Collections.Generic.List<ChatMessage>(samples.Count);
            foreach (var sample in samples)
            {
                if (sample.IsValid)
                {
                    result.Add(sample.AsView().ToManaged());
                }
            }
            return result;
        }
    }
}
