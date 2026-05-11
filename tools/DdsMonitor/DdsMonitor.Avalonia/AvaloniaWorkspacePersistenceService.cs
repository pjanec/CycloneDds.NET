using DdsMonitor.Engine;
using Microsoft.Extensions.Hosting;

namespace DdsMonitor.Avalonia;

/// <summary>
/// BackgroundService that listens for <see cref="WorkspaceSaveRequestedEvent"/> and
/// debounces workspace saves to disk. Also performs a best-effort final flush when
/// the application is stopping.
/// </summary>
internal sealed class AvaloniaWorkspacePersistenceService : BackgroundService
{
    private readonly IWindowManager _windowManager;
    private readonly IWorkspaceState _workspaceState;
    private readonly IDisposable _subscription;
    private readonly TimeSpan _debounceDelay;
    private CancellationTokenSource? _debounce;

    public AvaloniaWorkspacePersistenceService(
        IEventBroker broker,
        IWindowManager windowManager,
        IWorkspaceState workspaceState,
        IHostApplicationLifetime lifetime,
        TimeSpan? debounceDelay = null)
    {
        _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
        _debounceDelay = debounceDelay ?? TimeSpan.FromSeconds(1.5);

        // Subscribe immediately so events are captured as soon as the service is constructed.
        _subscription = broker.Subscribe<WorkspaceSaveRequestedEvent>(_ => RequestSave());

        // Register a best-effort final flush on application shutdown.
        lifetime.ApplicationStopping.Register(FlushSync);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            // Normal shutdown — nothing to do here; FlushSync is handled by ApplicationStopping.
        }
    }

    /// <summary>
    /// Schedules a debounced save: cancels any pending save and starts a new timer.
    /// </summary>
    internal void RequestSave()
    {
        var old = Interlocked.Exchange(ref _debounce, null);
        old?.Cancel();
        old?.Dispose();

        var cts = new CancellationTokenSource();
        _debounce = cts;

        _ = Task.Delay(_debounceDelay, cts.Token).ContinueWith(
            _ => FlushSync(),
            CancellationToken.None,
            TaskContinuationOptions.NotOnCanceled,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Immediately persists the current workspace to disk. Best-effort — never throws.
    /// </summary>
    internal void FlushSync()
    {
        try
        {
            var path = _workspaceState.WorkspaceFilePath;
            if (!string.IsNullOrWhiteSpace(path))
                _windowManager.SaveWorkspace(path);
        }
        catch
        {
            // Persistence failures must not crash the application.
        }
    }

    public override void Dispose()
    {
        _subscription.Dispose();

        var cts = Interlocked.Exchange(ref _debounce, null);
        cts?.Cancel();
        cts?.Dispose();

        base.Dispose();
    }
}
