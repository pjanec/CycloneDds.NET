using System;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;

namespace FeatureDemo.Scenarios.StockTicker;

public class StockSubscriber : IDisposable
{
    private readonly DdsReader<StockTick> _reader;
    private long _totalReceived;
    private long _passedFilter;
    private Predicate<StockTick> _filter;
    private bool _running;
    
    public long TotalReceived => Interlocked.Read(ref _totalReceived);
    public long PassedFilter => Interlocked.Read(ref _passedFilter);

    public event Action<StockTick>? OnTickReceived;
    public event Action<StockTick>? OnRawTick;

    public StockSubscriber(DdsParticipant participant)
    {
        _reader = new DdsReader<StockTick>(participant, "StockTick");
        // Default filter: allow all
        _filter = _ => true;
    }

    public void SetFilter(string symbol, double minPrice)
    {
        // Reset counters when filter changes? Maybe not, better to keep running total.
        if (string.IsNullOrEmpty(symbol))
        {
            _filter = tick => tick.Price >= minPrice;
        }
        else
        {
            _filter = tick => tick.Symbol.ToString() == symbol && tick.Price >= minPrice;
        }
    }

    public async Task StartProcessingAsync(CancellationToken ct)
    {
        _running = true;
        while (!ct.IsCancellationRequested && _running)
        {
            try
            {
                bool hasData = false;
                {
                    using var loan = _reader.Take(32);
                    if (loan.Count > 0)
                    {
                        hasData = true;
                        foreach (var sample in loan)
                        {
                            if (!sample.IsValid) continue;

                            Interlocked.Increment(ref _totalReceived);
                            OnRawTick?.Invoke(sample.Data);

                            if (_filter(sample.Data))
                            {
                                Interlocked.Increment(ref _passedFilter);
                                OnTickReceived?.Invoke(sample.Data);
                            }
                        }
                    }
                }

                if (!hasData)
                {
                    await _reader.WaitDataAsync(ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                // In a real app we would log this
                await Task.Delay(100, ct);
            }
        }
    }

    public void Stop()
    {
        _running = false;
    }

    public void Dispose()
    {
        _reader?.Dispose();
    }
}
