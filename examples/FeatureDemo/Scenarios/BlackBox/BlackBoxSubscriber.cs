using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;

namespace FeatureDemo.Scenarios.BlackBox;

public class BlackBoxSubscriber : IDisposable
{
    private readonly DdsReader<SystemLog> _reader;

    public BlackBoxSubscriber(DdsParticipant participant)
    {
        // "SystemLog" matches schema which has TransientLocal.
        // The Reader MUST also be TransientLocal (or Volatile, but for late-joining to work 
        // with a TransientLocal Writer, the Reader needs to request TransientLocal or Volatile? 
        // DDS Spec: Reader Durability must be <= Writer Durability.
        // Writer is TransientLocal. Reader defaults to Volatile?
        // Actually to receive historical data, the Reader usually needs TransientLocal too so the DCPS layer knows to ask for history.
        // Let's check Schema.cs for what the reader will pick up. The schema defines the topic QoS.
        // The DdsReader constructor uses Topic QoS by default.
        _reader = new DdsReader<SystemLog>(participant, "SystemLog");
    }

    public async Task<List<SystemLog>> WaitForHistoricalLogsAsync(int expectedCount, CancellationToken ct)
    {
        var logs = new List<SystemLog>();
        
        // We expect the data to be delivered shortly after matching because it is already in the writer's history.
        // However, discovery takes a moment.
        
        try
        {
            while (logs.Count < expectedCount && !ct.IsCancellationRequested)
            {
                // Poll or wait
                if (logs.Count == 0)
                {
                    // Wait for first data
                    await _reader.WaitDataAsync(ct);
                }

                bool gotData = false;
                {
                    using var loan = _reader.Take(100); // Take all available
                    foreach (var sample in loan)
                    {
                        if (sample.IsValid)
                        {
                            logs.Add(sample.Data);
                            gotData = true;
                        }
                    }
                }

                if (logs.Count < expectedCount)
                {
                    await Task.Delay(100, ct);
                }
            }
        }
        catch (OperationCanceledException) { }

        // Sort by ID to ensure order (though KeepAll should preserve order)
        logs.Sort((a, b) => a.LogId.CompareTo(b.LogId));

        return logs;
    }

    public void Dispose()
    {
        _reader?.Dispose();
    }
}
