using System;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;
using FeatureDemo; // For Extensions

namespace FeatureDemo.Scenarios.SensorArray;

public enum SubscriberMode
{
    Managed,
    ZeroCopy
}

public class SensorSubscriber : IDisposable
{
    private readonly DdsReader<SensorData> _reader;
    private long _messagesReceived;
    private long _bytesAllocated; // Accumulator
    
    public long MessagesReceived => Interlocked.Read(ref _messagesReceived);
    public long BytesAllocated => Interlocked.Read(ref _bytesAllocated);
    
    public SubscriberMode CurrentMode { get; set; } = SubscriberMode.ZeroCopy;

    public SensorSubscriber(DdsParticipant participant)
    {
        _reader = new DdsReader<SensorData>(participant, "SensorData");
    }

    public async Task StartReceivingAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                long startAlloc = GC.GetAllocatedBytesForCurrentThread();
                int processed = 0;

                if (CurrentMode == SubscriberMode.ZeroCopy)
                {
                    processed = ProcessZeroCopy();
                }
                else
                {
                    processed = ProcessManaged();
                }

                long endAlloc = GC.GetAllocatedBytesForCurrentThread();
                long diff = endAlloc - startAlloc;
                
                if (processed > 0)
                {
                    Interlocked.Add(ref _messagesReceived, processed);
                    Interlocked.Add(ref _bytesAllocated, diff);
                }
                
                if (processed == 0)
                {
                    await Task.Delay(10, ct); 
                    // Use larger delay when idle to avoid spin
                }
                else if (processed < 10)
                {
                     // If low data rate, small delay
                     await Task.Delay(1, ct);
                }
                // If high data rate (processed many), loop immediately
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                await Task.Delay(100, ct);
            }
        }
    }
    
    // We want to verify ZERO allocations (or near zero) in this hot path
    // excluding the overhead of polling itself if empty.
    private int ProcessZeroCopy()
    {
        int count = 0;
        // Take handles standard IEnumerable returns which allocates the enumerator
        // But CycloneDDS binding should be optimized.
        // Let's assume the binding returns a struct enumerator or a disposable that limits allocs.
        using var samples = _reader.Take(200); 
        
        if (samples.Length == 0) return 0;

        foreach (var sample in samples)
        {
             if (sample.IsValid)
             {
                 var view = sample.AsView();
                 // Access data to verify readability
                 double v = view.Value;
                 int id = view.SensorId;
                 count++;
             }
        }
        return count;
    }

    private int ProcessManaged()
    {
        int count = 0;
        using var samples = _reader.Take(200);
        
        if (samples.Length == 0) return 0;

        // Simulate typical managed usage where users materializes a List of objects/structs
        var list = new System.Collections.Generic.List<SensorData>(samples.Length);

        foreach (var sample in samples)
        {
            if (sample.IsValid)
            {
                // Accessing .Data copies the struct from native memory to stack/managed memory
                var data = sample.Data;
                // Simulating usage of managed strings 
                string loc = data.Location.ToString(); 
                list.Add(data); 
                count++;
            }
        }
        return count;
    }

    public void Dispose()
    {
        _reader?.Dispose();
    }
}
