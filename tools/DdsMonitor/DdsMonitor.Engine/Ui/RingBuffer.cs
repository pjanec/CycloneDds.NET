using System;
using System.Threading;

namespace DdsMonitor.Engine.Ui;

/// <summary>
/// A fixed-size circular buffer that tracks integer values over time.
/// Thread-safe for concurrent increment and flush operations.
/// </summary>
public sealed class RingBuffer
{
    private readonly int[] _slots;
    private int _currentIndex;
    private int _currentCount;

    /// <summary>
    /// Initialises a new <see cref="RingBuffer"/> with the given number of slots.
    /// </summary>
    public RingBuffer(int capacity = 10)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        _slots = new int[capacity];
    }

    /// <summary>
    /// Gets the total number of history slots.
    /// </summary>
    public int Capacity => _slots.Length;

    /// <summary>
    /// Atomically increments the counter for the current time slot.
    /// Call this for each incoming message.
    /// </summary>
    public void Increment()
    {
        Interlocked.Increment(ref _currentCount);
    }

    /// <summary>
    /// Atomically adds <paramref name="delta"/> to the counter for the current time slot.
    /// Use this to record multiple events at once without looping.
    /// </summary>
    public void Add(int delta)
    {
        if (delta > 0)
        {
            Interlocked.Add(ref _currentCount, delta);
        }
    }

    /// <summary>
    /// Commits the current slot count to the ring, advances to the next slot, and
    /// resets the counter.  Call this once per refresh interval (e.g. every second).
    /// </summary>
    public void Flush()
    {
        var committed = Interlocked.Exchange(ref _currentCount, 0);

        lock (_slots)
        {
            _slots[_currentIndex] = committed;
            _currentIndex = (_currentIndex + 1) % _slots.Length;
        }
    }

    /// <summary>
    /// Returns a snapshot of the history values in chronological order
    /// (oldest first, newest last).  The last element is the most recently
    /// flushed value; the current (not-yet-flushed) count is NOT included.
    /// </summary>
    public int[] GetSnapshot()
    {
        int[] snapshot;
        int startIndex;

        lock (_slots)
        {
            snapshot = (int[])_slots.Clone();
            startIndex = _currentIndex;
        }

        // Rotate so that oldest slot comes first.
        var result = new int[snapshot.Length];
        for (var i = 0; i < snapshot.Length; i++)
        {
            result[i] = snapshot[(startIndex + i) % snapshot.Length];
        }

        return result;
    }
}
