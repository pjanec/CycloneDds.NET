using System.Linq;
using DdsMonitor.Engine.Ui;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class SparklineTests
{
    [Fact]
    public void RingBuffer_GetSnapshot_ReturnsZerosInitially()
    {
        var buffer = new RingBuffer(10);

        var snapshot = buffer.GetSnapshot();

        Assert.Equal(10, snapshot.Length);
        Assert.All(snapshot, v => Assert.Equal(0, v));
    }

    [Fact]
    public void RingBuffer_IncrementAndFlush_RecordsCorrectFrequency()
    {
        var buffer = new RingBuffer(10);

        // First second: 50 samples
        for (var i = 0; i < 50; i++)
        {
            buffer.Increment();
        }

        buffer.Flush();

        // Second second: 30 samples
        for (var i = 0; i < 30; i++)
        {
            buffer.Increment();
        }

        buffer.Flush();

        var snapshot = buffer.GetSnapshot();

        // The two most-recent slots should be 50 and 30.
        Assert.Equal(50, snapshot[snapshot.Length - 2]);
        Assert.Equal(30, snapshot[snapshot.Length - 1]);

        // All older slots should still be zero.
        var older = snapshot.Take(snapshot.Length - 2).ToArray();
        Assert.All(older, v => Assert.Equal(0, v));
    }

    [Fact]
    public void RingBuffer_WrapsAround_OldestSlotOverwritten()
    {
        var buffer = new RingBuffer(4);

        // Fill all 4 slots: 1, 2, 3, 4
        for (var value = 1; value <= 4; value++)
        {
            for (var i = 0; i < value; i++)
            {
                buffer.Increment();
            }

            buffer.Flush();
        }

        // Add a 5th flush — should overwrite slot 0 (oldest = 1)
        for (var i = 0; i < 10; i++)
        {
            buffer.Increment();
        }

        buffer.Flush();

        // After wrap-around the snapshot (oldest-first) should be [2, 3, 4, 10]
        var snapshot = buffer.GetSnapshot();
        Assert.Equal(4, snapshot.Length);
        Assert.Equal(2, snapshot[0]);
        Assert.Equal(3, snapshot[1]);
        Assert.Equal(4, snapshot[2]);
        Assert.Equal(10, snapshot[3]);
    }

    [Fact]
    public void RingBuffer_MultipleFlushes_WithNoIncrements_AdvancesSlots()
    {
        var buffer = new RingBuffer(4);

        buffer.Increment();
        buffer.Flush(); // index 0: slot[0] = 1, _currentIndex advances to 1

        buffer.Flush(); // index 1: slot[1] = 0, _currentIndex advances to 2
        buffer.Flush(); // index 2: slot[2] = 0, _currentIndex advances to 3

        // After 3 flushes _currentIndex == 3, so GetSnapshot rotates starting at slot[3].
        // Oldest-first order: slot[3], slot[0], slot[1], slot[2]  →  [0, 1, 0, 0]
        var snapshot = buffer.GetSnapshot();
        Assert.Equal(0, snapshot[0]); // slot[3] — unwritten
        Assert.Equal(1, snapshot[1]); // slot[0] — first flush
        Assert.Equal(0, snapshot[2]); // slot[1] — second flush
        Assert.Equal(0, snapshot[3]); // slot[2] — third flush
    }
}
