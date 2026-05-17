using System;
using System.Threading;
using System.Threading.Tasks;
using DdsMonitor.Engine;
using Microsoft.Extensions.Hosting;

namespace DdsMonitor.Plugins.FeatureDemo;

/// <summary>
/// Demo background service that periodically polls <see cref="ISampleStore.TotalCount"/>
/// and exposes a live <see cref="ProcessedCount"/> for the <c>DemoDashboardPanel</c> to display.
/// Registered by <see cref="FeatureDemoPlugin"/> via <c>ConfigureServices</c>.
/// </summary>
public sealed class DemoBackgroundProcessor : IHostedService, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly ISampleStore _sampleStore;
    private Timer? _timer;
    private int _processedCount;

    /// <summary>
    /// Gets the total number of samples observed in the store since the last poll tick.
    /// Updated approximately once per second; thread-safe.
    /// </summary>
    public int ProcessedCount => _processedCount;

    /// <summary>
    /// Raised on the timer thread each time <see cref="ProcessedCount"/> changes.
    /// Consumers should marshal to the UI thread before calling <c>StateHasChanged</c>.
    /// </summary>
    public event Action? OnUpdated;

    /// <summary>
    /// Initialises the processor with the shared sample store.
    /// </summary>
    public DemoBackgroundProcessor(ISampleStore sampleStore)
    {
        _sampleStore = sampleStore ?? throw new ArgumentNullException(nameof(sampleStore));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(Tick, null, TimeSpan.Zero, PollInterval);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _timer?.Dispose();
    }

    private void Tick(object? state)
    {
        _processedCount = _sampleStore.TotalCount;
        OnUpdated?.Invoke();
    }
}
