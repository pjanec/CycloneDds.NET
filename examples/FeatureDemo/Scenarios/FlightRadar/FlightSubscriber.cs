using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;

namespace FeatureDemo.Scenarios.FlightRadar;

public class FlightSubscriber : IDisposable
{
    private readonly DdsReader<FlightPosition> _reader;

    public FlightSubscriber(DdsParticipant participant)
    {
        _reader = new DdsReader<FlightPosition>(participant, "FlightPosition");
    }

    /// <summary>
    /// Look up a specific flight and return its history using ReadInstance.
    /// This demonstrates O(1) keyed access without iterating all samples.
    /// </summary>
    public List<FlightPosition> GetHistoryForFlight(string flightId)
    {
        var result = new List<FlightPosition>();
        
        // 1. Create a key sample
        var keySample = new FlightPosition
        {
            FlightId = new FixedString32(flightId)
        };

        try
        {
            // 2. Lookup instance handle
            // This is efficient because DDS maintains an index of keys
            var handle = _reader.LookupInstance(in keySample);
            
            // Check if handle is valid (0 is usually nil)
            if (handle.Value == 0) return result;

            // 3. Read history for just this instance
            // History depth is 10 in QoS, so we ask for up to 10
            using var loan = _reader.ReadInstance(handle, maxSamples: 10);
            
            foreach (var sample in loan)
            {
                if (sample.IsValid)
                {
                    result.Add(sample.Data); // struct copy
                }
            }
        }
        catch (Exception)
        {
            // Instance might not exist or other error
        }

        return result;
    }

    // Helper to just drain data so the reader cache updates?
    // Actually, ReadInstance works on the Reader's cache. If we never Read/Take from the reader, 
    // the history keeps growing until resource limits or History QoS overwrites old samples.
    // Since History = KeepLast(10), it will maintain last 10 samples automatically.
    // So we don't strictly need a background loop calling Read() if we only care about polling specific instances.
    // However, if we want to discover NEW instances, we might need to process notification or just poll LookupInstance.

    public void Dispose()
    {
        _reader?.Dispose();
    }
}
