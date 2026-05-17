using System;
using System.Collections.Generic;
using System.Threading;

namespace DdsMonitor.Engine;

/// <summary>
/// Thread-safe event broker implementation.
/// </summary>
public sealed class EventBroker : IEventBroker
{
    private readonly object _sync = new();
    private readonly Dictionary<Type, List<IEventSubscription>> _subscriptions = new();

    /// <inheritdoc />
    public void Publish<TEvent>(TEvent eventMessage)
    {
        if (eventMessage == null)
        {
            throw new ArgumentNullException(nameof(eventMessage));
        }

        List<IEventSubscription> subscribers;
        lock (_sync)
        {
            if (!_subscriptions.TryGetValue(typeof(TEvent), out var list) || list.Count == 0)
            {
                return;
            }

            subscribers = new List<IEventSubscription>(list);
        }

        foreach (var subscriber in subscribers)
        {
            subscriber.Invoke(eventMessage);
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var subscription = new EventSubscription<TEvent>(this, handler);

        lock (_sync)
        {
            if (!_subscriptions.TryGetValue(typeof(TEvent), out var list))
            {
                list = new List<IEventSubscription>();
                _subscriptions[typeof(TEvent)] = list;
            }

            list.Add(subscription);
        }

        return subscription;
    }

    private void Unsubscribe(Type eventType, IEventSubscription subscription)
    {
        lock (_sync)
        {
            if (!_subscriptions.TryGetValue(eventType, out var list))
            {
                return;
            }

            list.Remove(subscription);
            if (list.Count == 0)
            {
                _subscriptions.Remove(eventType);
            }
        }
    }

    private interface IEventSubscription
    {
        void Invoke(object message);
    }

    private sealed class EventSubscription<TEvent> : IEventSubscription, IDisposable
    {
        private readonly EventBroker _owner;
        private Action<TEvent>? _handler;

        public EventSubscription(EventBroker owner, Action<TEvent> handler)
        {
            _owner = owner;
            _handler = handler;
        }

        public void Invoke(object message)
        {
            var handler = Volatile.Read(ref _handler);
            if (handler == null)
            {
                return;
            }

            handler((TEvent)message);
        }

        public void Dispose()
        {
            var handler = Interlocked.Exchange(ref _handler, null);
            if (handler == null)
            {
                return;
            }

            _owner.Unsubscribe(typeof(TEvent), this);
        }
    }
}
