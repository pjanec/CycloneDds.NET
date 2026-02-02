using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace FeatureDemo
{
    public static class StockTickExtensions
    {
        public static StockTickView AsView(this DdsSample<StockTick> sample)
        {
            unsafe { return new StockTickView((StockTick_Native*)sample.NativePtr); }
        }

        public static System.Collections.Generic.List<StockTick> ReadCopied(this DdsReader<StockTick> reader, int maxSamples = 32)
        {
            using var samples = reader.Read(maxSamples);
            var result = new System.Collections.Generic.List<StockTick>(samples.Count);
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
