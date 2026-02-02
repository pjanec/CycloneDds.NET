using NUnit.Framework;
using CycloneDDS.Runtime;
using FeatureDemo;
using FeatureDemo.Scenarios.StockTicker;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

namespace FeatureDemo.Tests;

public class StockPublisherTests
{
    [Test]
    public async Task StockPublisher_PublishesMultipleSymbols()
    {
        // Use separate domain for isolation
        using var participant = new DdsParticipant(222);
        using var publisher = new StockPublisher(participant);
        
        // Create a subscriber to verify. Topic name "StockTick" matches StockPublisher.
        using var subscriber = new DdsReader<StockTick>(participant, "StockTick");
        
        var receivedSymbols = new HashSet<string>();
        var cts = new CancellationTokenSource();
        cts.CancelAfter(10000); // 10 sec max timeout

        // Start publisher
        var pubTask = publisher.StartPublishingAsync(50, cts.Token);
        
        try
        {
            // Give some time for discovery
            await Task.Delay(1000, cts.Token);

            while (!cts.Token.IsCancellationRequested && receivedSymbols.Count < 4)
            {
                // Drain queue
                using (var samples = subscriber.Take(10))
                {
                    foreach (var sample in samples)
                    {
                        if (sample.IsValid)
                        {
                            receivedSymbols.Add(sample.Data.Symbol.ToString());
                        }
                    }
                }

                if (receivedSymbols.Count >= 4) break;
                
                await Task.Delay(50, cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            publisher.Stop();
            try { await pubTask; } catch (OperationCanceledException) { }
        }

        Assert.That(receivedSymbols, Contains.Item("AAPL"), "Should contain AAPL");
        Assert.That(receivedSymbols, Contains.Item("MSFT"), "Should contain MSFT");
        Assert.That(receivedSymbols, Contains.Item("GOOG"), "Should contain GOOG");
        Assert.That(receivedSymbols, Contains.Item("TSLA"), "Should contain TSLA");
    }
}
