using System;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;

namespace FeatureDemo.Scenarios.StockTicker;

public class StockPublisher : IDisposable
{
    private readonly DdsWriter<StockTick> _writer;
    private bool _running;
    private long _messagesSent;
    private readonly Random _random = new Random();
    private readonly string[] _symbols = new[] { "AAPL", "MSFT", "GOOG", "TSLA" };
    private readonly double[] _prices;

    public long MessagesSent => Interlocked.Read(ref _messagesSent);

    public StockPublisher(DdsParticipant participant)
    {
        _writer = new DdsWriter<StockTick>(participant, "StockTick");
        _prices = new double[_symbols.Length];
        for (int i = 0; i < _symbols.Length; i++)
        {
            _prices[i] = 100.0 + _random.NextDouble() * 50.0;
        }
    }

    public async Task StartPublishingAsync(int targetRateHz, CancellationToken ct)
    {
        _running = true;
        var period = TimeSpan.FromSeconds(1.0 / targetRateHz);
        var nextTime = DateTime.UtcNow;
        long tickId = 0;

        while (!ct.IsCancellationRequested && _running)
        {
            for (int i = 0; i < _symbols.Length; i++)
            {
                // Update price with random walk
                double change = (_random.NextDouble() - 0.5) * 1.0; // +/- 0.5
                _prices[i] += change;
                if (_prices[i] < 1.0) _prices[i] = 1.0; // Minimum price

                var data = new StockTick
                {
                    TickId = ++tickId,
                    Symbol = new FixedString32(_symbols[i]),
                    Price = _prices[i],
                    Volume = _random.Next(1, 100),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                _writer.Write(data);
                Interlocked.Increment(ref _messagesSent);
            }

            // Rate limiting
            nextTime += period;
            var delay = nextTime - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct);
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
