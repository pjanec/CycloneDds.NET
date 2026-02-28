using System;
using System.Threading;
using System.Threading.Tasks;

namespace DdsMonitor.Engine;

/// <summary>
/// Debounces repeated triggers to a single action.
/// </summary>
public sealed class DebouncedAction : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly Action _action;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DebouncedAction"/> class.
    /// </summary>
    public DebouncedAction(TimeSpan delay, Action action)
    {
        _delay = delay;
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    /// <summary>
    /// Schedules the action to run after the debounce delay.
    /// </summary>
    public void Trigger()
    {
        ThrowIfDisposed();

        _cts?.Cancel();
        _cts?.Dispose();

        var cts = new CancellationTokenSource();
        _cts = cts;

        _ = RunAsync(cts.Token);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(_delay, token).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (!token.IsCancellationRequested)
        {
            _action();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DebouncedAction));
        }
    }
}
