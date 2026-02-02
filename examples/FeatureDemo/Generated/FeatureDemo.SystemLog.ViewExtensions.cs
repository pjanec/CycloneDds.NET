using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace FeatureDemo
{
    public static class SystemLogExtensions
    {
        public static SystemLogView AsView(this DdsSample<SystemLog> sample)
        {
            unsafe { return new SystemLogView((SystemLog_Native*)sample.NativePtr); }
        }

        public static System.Collections.Generic.List<SystemLog> ReadCopied(this DdsReader<SystemLog> reader, int maxSamples = 32)
        {
            using var samples = reader.Read(maxSamples);
            var result = new System.Collections.Generic.List<SystemLog>(samples.Count);
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
