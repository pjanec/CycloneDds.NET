using System;

namespace DdsMonitor.Engine;

/// <summary>
/// Publishes and subscribes to application events.
/// </summary>
public interface IEventBroker
{
    /// <summary>
    /// Publishes an event message to all subscribers.
    /// </summary>
    void Publish<TEvent>(TEvent eventMessage);

    /// <summary>
    /// Subscribes to a specific event type.
    /// </summary>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler);
}
