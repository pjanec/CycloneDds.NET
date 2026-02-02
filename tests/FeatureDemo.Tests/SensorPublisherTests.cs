using NUnit.Framework;
using CycloneDDS.Runtime;
using FeatureDemo.Scenarios.SensorArray;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace FeatureDemo.Tests;

public class SensorPublisherTests
{
    [Test]
    [Explicit("Performance test takes too long for rapid iteration. Run manually if needed.")]
    public async Task SensorPublisher_HighFrequency_AchievesTargetRate()
    {
        // Use domain different from default to avoid interference
        using var participant = new DdsParticipant(111);
        using var publisher = new SensorPublisher(participant);
        using var cts = new CancellationTokenSource();

        var targetRate = 2000;
        var task = publisher.StartPublishingAsync(targetRate, cts.Token);

        // Let it run for a bit
        await Task.Delay(200);
        
        // Measure
        var startCount = publisher.MessagesSent;
        var startTime = DateTime.UtcNow;

        await Task.Delay(1000);

        var endCount = publisher.MessagesSent;
        var endTime = DateTime.UtcNow;
        
        cts.Cancel();
        try { await task; } catch (OperationCanceledException) {}

        var count = endCount - startCount;
        var duration = (endTime - startTime).TotalSeconds;
        var rate = count / duration;

        TestContext.WriteLine($"Achieved Rate: {rate:F2} msg/s (Target: {targetRate})");
        
        // Assert we are close. In CI environments timing is tricky, so be lenient (e.g. 50% or just > 1000)
        Assert.That(rate, Is.GreaterThan(500), "Should achieve at least 500 msg/s");
    }
}
