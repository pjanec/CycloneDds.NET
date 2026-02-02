using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace FeatureDemo
{
    public static class DemoControlExtensions
    {
        public static DemoControlView AsView(this DdsSample<DemoControl> sample)
        {
            unsafe { return new DemoControlView((DemoControl_Native*)sample.NativePtr); }
        }

        public static System.Collections.Generic.List<DemoControl> ReadCopied(this DdsReader<DemoControl> reader, int maxSamples = 32)
        {
            using var samples = reader.Read(maxSamples);
            var result = new System.Collections.Generic.List<DemoControl>(samples.Count);
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
