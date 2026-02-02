using NUnit.Framework;
using CycloneDDS.Runtime;
using FeatureDemo;
using FeatureDemo.Scenarios.FlightRadar;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FeatureDemo.Tests;

public class FlightRadarTests
{
    [Test]
    public async Task FlightRadar_KeyedLookupworks()
    {
        using var participant = new DdsParticipant(70);
        using var publisher = new FlightPublisher(participant);
        using var subscriber = new FlightSubscriber(participant);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(10000);

        // Start publishing vigorously
        var pubTask = publisher.StartPublishingAsync(50, cts.Token);
        
        try
        {
            // Wait for data to accumulate and discovery
            // We need enough time for at least 10 samples (at 50Hz this is 0.2s)
            // Discovery might take 1s
            await Task.Delay(3000, cts.Token);
            
            // Look up "BA-123"
            var history = subscriber.GetHistoryForFlight("BA-123");
            
            TestContext.Out.WriteLine($"BA-123 History Count: {history.Count}");
            Assert.That(history.Count, Is.GreaterThan(0), "Should receive history for BA-123");
            Assert.That(history.Count, Is.LessThanOrEqualTo(10), "Should respect History KeepLast(10)");
            
            // Verify all returned samples are BA-123
            foreach (var item in history)
            {
                Assert.That(item.FlightId.ToString(), Is.EqualTo("BA-123"));
            }
            
            // Look up "LH-456"
            var history2 = subscriber.GetHistoryForFlight("LH-456");
             TestContext.Out.WriteLine($"LH-456 History Count: {history2.Count}");
             Assert.That(history2.Count, Is.GreaterThan(0));
             foreach (var item in history2)
            {
                Assert.That(item.FlightId.ToString(), Is.EqualTo("LH-456"));
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            publisher.Stop();
            // allow pub task to exit
            try { await pubTask; } catch {}
        }
    }
}
