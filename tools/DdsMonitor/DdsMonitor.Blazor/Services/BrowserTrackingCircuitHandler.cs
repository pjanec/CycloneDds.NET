using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace DdsMonitor.Services;

/// <summary>
/// Blazor <see cref="CircuitHandler"/> that tracks the number of active browser circuits
/// and raises <see cref="ConnectionChanged"/> whenever the first circuit connects or all
/// circuits disconnect (ME1-T10).
/// </summary>
public sealed class BrowserTrackingCircuitHandler : CircuitHandler
{
    private int _activeCount;

    /// <summary>
    /// Raised with <c>true</c> when the first circuit connects, and with <c>false</c>
    /// when all circuits have disconnected.
    /// </summary>
    public event Action<bool>? ConnectionChanged;

    /// <inheritdoc />
    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _activeCount) == 1)
        {
            ConnectionChanged?.Invoke(true);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (Interlocked.Decrement(ref _activeCount) == 0)
        {
            ConnectionChanged?.Invoke(false);
        }
        return Task.CompletedTask;
    }
}
