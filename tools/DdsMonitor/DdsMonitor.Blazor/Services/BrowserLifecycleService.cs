using System;
using System.Threading;
using System.Threading.Tasks;
using DdsMonitor.Engine.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DdsMonitor.Services;

/// <summary>
/// Background service that shuts down the application when no browser connects within
/// <see cref="BrowserLifecycleOptions.ConnectTimeout"/> seconds, or when all connected
/// tabs close and none reconnect within <see cref="BrowserLifecycleOptions.DisconnectTimeout"/>
/// seconds (ME1-T10).
/// </summary>
public sealed class BrowserLifecycleService : BackgroundService
{
    private readonly BrowserTrackingCircuitHandler _tracker;
    private readonly BrowserLifecycleOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<BrowserLifecycleService> _logger;

    private volatile bool _everConnected;
    private CancellationTokenSource? _disconnectCts;

    public BrowserLifecycleService(
        BrowserTrackingCircuitHandler tracker,
        BrowserLifecycleOptions options,
        IHostApplicationLifetime lifetime,
        ILogger<BrowserLifecycleService> logger)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // When KeepAlive is set (NoBrowser mode) the app should not shut down due to
        // browser connectivity events – just stay alive until the host is stopped.
        if (_options.KeepAlive)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
            return;
        }

        var firstConnectTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnConnectionChanged(bool isConnected)
        {
            if (isConnected)
            {
                if (!_everConnected)
                {
                    _everConnected = true;
                    _logger.LogInformation("Browser connected.");
                    firstConnectTcs.TrySetResult();
                }

                // Cancel any pending disconnect timer on reconnect.
                var dcts = Interlocked.Exchange(ref _disconnectCts, null);
                dcts?.Cancel();
                dcts?.Dispose();
            }
            else
            {
                // All tabs disconnected – start disconnect timer.
                _logger.LogInformation("All browser tabs disconnected. Waiting {Timeout}s before shutdown.", _options.DisconnectTimeout);
                var dcts = new CancellationTokenSource();
                var prev = Interlocked.Exchange(ref _disconnectCts, dcts);
                prev?.Cancel();
                prev?.Dispose();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(_options.DisconnectTimeout), dcts.Token);
                        _logger.LogInformation("Disconnect timeout elapsed. Stopping application.");
                        _lifetime.StopApplication();
                    }
                    catch (OperationCanceledException)
                    {
                        // Reconnect cancelled the timer – do nothing.
                    }
                    finally
                    {
                        dcts.Dispose();
                    }
                }, CancellationToken.None);
            }
        }

        _tracker.ConnectionChanged += OnConnectionChanged;

        try
        {
            // Wait for the first browser connection.  If ConnectTimeout elapses first, shut down.
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            connectCts.CancelAfter(TimeSpan.FromSeconds(_options.ConnectTimeout));

            try
            {
                await firstConnectTcs.Task.WaitAsync(connectCts.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("No browser connected within {Timeout}s. Stopping application.", _options.ConnectTimeout);
                _lifetime.StopApplication();
            }

            // Keep running until the host stops (disconnect timer will also stop it).
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            _tracker.ConnectionChanged -= OnConnectionChanged;

            var dcts = Interlocked.Exchange(ref _disconnectCts, null);
            dcts?.Cancel();
            dcts?.Dispose();
        }
    }
}
