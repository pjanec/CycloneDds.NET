using NUnit.Framework;
using CycloneDDS.Runtime;
using FeatureDemo;
using FeatureDemo.Scenarios.StockTicker;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace FeatureDemo.Tests;

public class StockSubscriberTests
{
    [Test]
    public async Task StockSubscriber_FiltersMessages()
    {
        using var participant = new DdsParticipant(60); 
        using var publisher = new StockPublisher(participant);
        using var subscriber = new StockSubscriber(participant);
        
        var receivedTicks = new ConcurrentBag<StockTick>();
        subscriber.OnTickReceived += (tick) => receivedTicks.Add(tick);
        
        // Set filter to only accept AAPL
        subscriber.SetFilter("AAPL", 0);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(10000);

        var pubTask = publisher.StartPublishingAsync(50, cts.Token);
        var subTask = subscriber.StartProcessingAsync(cts.Token);
        
        try
        {
            // Wait for some data
            while (!cts.Token.IsCancellationRequested && receivedTicks.Count < 20)
            {
                await Task.Delay(100, cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            publisher.Stop();
            subscriber.Stop();
            // Allow tasks to finish gracefully
            cts.Cancel(); 
            try { await Task.WhenAll(pubTask, subTask); } catch (OperationCanceledException) { }
        }

        Assert.That(receivedTicks.Count, Is.GreaterThan(0), "Should have received AAPL messages");
        
        var nonAapl = receivedTicks.Where(t => t.Symbol.ToString() != "AAPL").ToList();
        Assert.That(nonAapl, Is.Empty, $"Should NOT have received non-AAPL messages. Found: {string.Join(", ", nonAapl.Select(t => t.Symbol.ToString()))}");
        
        // Verify stats
        // We publish 4 symbols (AAPL, MSFT, GOOG, TSLA). We filter for AAPL.
        // So PassedFilter should be roughly 25% of TotalReceived.
        Assert.That(subscriber.PassedFilter, Is.GreaterThan(0));
        Assert.That(subscriber.TotalReceived, Is.GreaterThan(subscriber.PassedFilter), 
            $"TotalReceived ({subscriber.TotalReceived}) should be greater than PassedFilter ({subscriber.PassedFilter}) because we are filtering out 3/4 symbols.");
    }
}
