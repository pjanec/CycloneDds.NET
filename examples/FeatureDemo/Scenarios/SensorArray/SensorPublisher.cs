using System;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;

namespace FeatureDemo.Scenarios.SensorArray;

public class SensorPublisher : IDisposable
{
    private readonly DdsWriter<SensorData> _writer;
    private bool _running;
    private long _messagesSent;

    public long MessagesSent => Interlocked.Read(ref _messagesSent);

    public SensorPublisher(DdsParticipant participant)
    {
        // Topic name matches the one in Schema.cs [DdsTopic("SensorData")]
        _writer = new DdsWriter<SensorData>(participant, "SensorData");
    }


    public async Task StartPublishingAsync(int targetRateHz, CancellationToken ct)
    {
        _running = true;
        var period = TimeSpan.FromSeconds(1.0 / targetRateHz);
        var nextTime = DateTime.UtcNow;

        int sensorId = 1;
        var fixedLocation = new FixedString32("Factory-A");
        
        while (!ct.IsCancellationRequested && _running)
        {
            var data = new SensorData
            {
                SensorId = sensorId,
                Value = Math.Sin(DateTime.UtcNow.Ticks * 0.0001),
                Location = fixedLocation, 
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            
            _writer.Write(data);
            Interlocked.Increment(ref _messagesSent);

            sensorId++;
            if (sensorId > 10) sensorId = 1;

            // Rate limiting
            nextTime += period;
            var delay = nextTime - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                 if (targetRateHz > 64) 
                 {
                     // Busy wait for high freq
                     while (DateTime.UtcNow < nextTime && !ct.IsCancellationRequested) 
                     { 
                        // Spin
                     } 
                 }
                 else 
                 {
                    await Task.Delay(delay, ct);
                 }
            }
            else
            {
                // We are behind, just yield every now and then
                if (_messagesSent % 100 == 0)
                     await Task.Yield();
            }
        }
    }

    public void Stop()
    {
        _running = false;
    }

    public void Dispose()
    {
        _writer?.Dispose();
    }
}
