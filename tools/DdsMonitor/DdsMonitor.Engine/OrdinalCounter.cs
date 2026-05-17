using System.Threading;

namespace DdsMonitor.Engine;

/// <summary>
/// Thread-safe monotonically increasing counter that provides globally unique
/// ordinal values across all <see cref="DdsMonitor.Engine.DdsBridge"/> participants.
/// </summary>
public sealed class OrdinalCounter
{
    private long _value;

    /// <summary>Atomically increments the counter and returns the new value.</summary>
    public long Increment() => Interlocked.Increment(ref _value);

    /// <summary>Atomically resets the counter to zero.</summary>
    public void Reset() => Interlocked.Exchange(ref _value, 0L);

    /// <summary>Gets the current value without modifying it.</summary>
    public long Current => Interlocked.Read(ref _value);
}
