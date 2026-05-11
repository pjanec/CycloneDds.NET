using DdsMonitor.Engine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// Hosted service that periodically publishes <see cref="HeartbeatSample"/> instances
/// to prove the DDS write pipeline end-to-end.
/// </summary>
public class DummyGeneratorService : IHostedService, IDisposable
{
    private readonly ITopicRegistry _topicRegistry;
    private readonly IDdsBridge _ddsBridge;
    private readonly ILogger<DummyGeneratorService>? _logger;

    private readonly bool _enabledAtStartup;
    private readonly int _publishRateMs;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _publishing;
    private readonly object _toggleLock = new();

    public DummyGeneratorService(
        ITopicRegistry topicRegistry,
        IDdsBridge ddsBridge,
        IConfiguration configuration,
        ILogger<DummyGeneratorService>? logger = null)
    {
        _topicRegistry = topicRegistry;
        _ddsBridge = ddsBridge;
        _logger = logger;

        _enabledAtStartup = configuration.GetValue("GeneratorPlugin:Enabled", false);
        _publishRateMs = configuration.GetValue("GeneratorPlugin:PublishRateMs", 100);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_enabledAtStartup)
        {
            StartPublishing();
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_toggleLock)
        {
            _cts?.Cancel();
        }

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }
    }

    /// <summary>
    /// Toggles publishing on or off at runtime (thread-safe).
    /// </summary>
    public void TogglePublishing()
    {
        lock (_toggleLock)
        {
            if (_publishing)
                StopPublishing();
            else
                StartPublishing();
        }
    }

    private void StartPublishing()
    {
        _publishing = true;
        _cts = new CancellationTokenSource();
        _loopTask = RunLoopAsync(_cts.Token);
    }

    private void StopPublishing()
    {
        _publishing = false;
        _cts?.Cancel();
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        IDynamicWriter? writer = null;

        try
        {
            var meta = new TopicMetadata(typeof(HeartbeatSample));
            _topicRegistry.Register(meta);
            writer = _ddsBridge.GetWriter(meta);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "DummyGeneratorService: failed to acquire writer — publishing disabled.");
            _publishing = false;
            return;
        }

        int sequence = 0;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var sample = new HeartbeatSample
                {
                    Id = 1,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Sequence = sequence++,
                };

                try
                {
                    writer.Write(sample);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "DummyGeneratorService: write failed.");
                }

                if (_publishRateMs > 0)
                    await Task.Delay(_publishRateMs, ct).ConfigureAwait(false);
                else
                    await Task.Yield();
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            writer?.Dispose();
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
