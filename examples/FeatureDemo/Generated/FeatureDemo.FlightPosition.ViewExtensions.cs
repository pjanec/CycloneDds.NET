using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace FeatureDemo
{
    public static class FlightPositionExtensions
    {
        public static FlightPositionView AsView(this DdsSample<FlightPosition> sample)
        {
            unsafe { return new FlightPositionView((FlightPosition_Native*)sample.NativePtr); }
        }

        public static System.Collections.Generic.List<FlightPosition> ReadCopied(this DdsReader<FlightPosition> reader, int maxSamples = 32)
        {
            using var samples = reader.Read(maxSamples);
            var result = new System.Collections.Generic.List<FlightPosition>(samples.Count);
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
