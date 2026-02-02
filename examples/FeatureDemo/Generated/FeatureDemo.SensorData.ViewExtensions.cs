using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace FeatureDemo
{
    public static class SensorDataExtensions
    {
        public static SensorDataView AsView(this DdsSample<SensorData> sample)
        {
            unsafe { return new SensorDataView((SensorData_Native*)sample.NativePtr); }
        }

        public static System.Collections.Generic.List<SensorData> ReadCopied(this DdsReader<SensorData> reader, int maxSamples = 32)
        {
            using var samples = reader.Read(maxSamples);
            var result = new System.Collections.Generic.List<SensorData>(samples.Count);
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
