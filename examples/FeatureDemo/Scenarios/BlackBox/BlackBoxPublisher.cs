using System;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;

namespace FeatureDemo.Scenarios.BlackBox;

public class BlackBoxPublisher : IDisposable
{
    private readonly DdsWriter<SystemLog> _writer;
    private long _logIdCounter;

    public BlackBoxPublisher(DdsParticipant participant)
    {
        // Topic "SystemLog" with Durability = TransientLocal (defined in Schema.cs)
        _writer = new DdsWriter<SystemLog>(participant, "SystemLog");
    }

    public void PublishCriticalLogs()
    {
        var logs = new[]
        {
            (LogLevel.Info, "System", "Boot sequence initiated"),
            (LogLevel.Info, "System", "Kernel loaded"),
            (LogLevel.Warning, "Memory", "Heap fragmentation detected at 15%"),
            (LogLevel.Info, "Network", "Interface eth0 up"),
            (LogLevel.Error, "Database", "Connection timeout on db-primary"),
            (LogLevel.Critical, "Security", "Unauthorized access attempt on port 22"),
            (LogLevel.Info, "System", "Safe mode active")
        };

        foreach (var (level, comp, msg) in logs)
        {
            var logEntry = new SystemLog
            {
                LogId = ++_logIdCounter,
                Level = level,
                Component = new FixedString64(comp),
                Message = new FixedString128(msg),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            
            _writer.Write(logEntry);
            
            // Small delay just so they don't have identical timestamps if that matters,
            // but for transient local they are stored in history cache.
            Thread.Sleep(10); 
        }
    }

    public void Dispose()
    {
        _writer?.Dispose();
    }
}
