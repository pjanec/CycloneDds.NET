using System;
using Avalonia.Threading;
using DdsMonitor.Engine;

namespace DdsMonitor.Avalonia.Core;

/// <summary>
/// Extension methods for <see cref="IEventBroker"/> that marshal handlers onto the
/// Avalonia UI thread.
/// </summary>
public static class IEventBrokerExtensions
{
    /// <summary>
    /// Subscribes to events of type <typeparamref name="TEvent"/> and invokes
    /// <paramref name="handler"/> on <see cref="Dispatcher.UIThread"/>.
    /// </summary>
    /// <param name="broker">The event broker to subscribe to.</param>
    /// <param name="handler">The handler to invoke on the UI thread.</param>
    /// <param name="dispatcher">
    /// The Avalonia dispatcher. Defaults to <see cref="Dispatcher.UIThread"/> when
    /// <c>null</c>.
    /// </param>
    /// <returns>A disposable token; dispose to unsubscribe.</returns>
    public static IDisposable SubscribeOnUiThread<TEvent>(
        this IEventBroker broker,
        Action<TEvent> handler,
        Dispatcher? dispatcher = null)
    {
        if (broker == null) throw new ArgumentNullException(nameof(broker));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var d = dispatcher ?? Dispatcher.UIThread;

        return broker.Subscribe<TEvent>(ev =>
        {
            if (d.CheckAccess())
                handler(ev);
            else
                d.Post(() => handler(ev), DispatcherPriority.Normal);
        });
    }
}
