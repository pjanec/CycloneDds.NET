using System;

namespace DdsMonitor.Engine;

/// <summary>
/// Singleton service that allows the main layout to hand a clone-and-send request
/// off to the SendSamplePanel, regardless of whether the panel is already open
/// or is freshly spawned.
/// </summary>
public sealed class CloneRequestService
{
    /// <summary>
    /// The most recently posted clone request, or <c>null</c> if none is pending.
    /// </summary>
    public (TopicMetadata Meta, object Payload)? Pending { get; private set; }

    /// <summary>
    /// Fired whenever a new request is posted via <see cref="SetRequest"/>.
    /// Subscribe in a component's <c>OnInitialized</c> to handle requests while
    /// the component is alive.
    /// </summary>
    public event Action? RequestAvailable;

    /// <summary>Posts a new clone request and notifies any live subscribers.</summary>
    public void SetRequest(TopicMetadata meta, object payload)
    {
        Pending = (meta, payload);
        RequestAvailable?.Invoke();
    }

    /// <summary>
    /// Atomically reads and clears the pending request.
    /// Returns <c>null</c> if no request is pending.
    /// </summary>
    public (TopicMetadata Meta, object Payload)? TakeRequest()
    {
        var r = Pending;
        Pending = null;
        return r;
    }
}
