using NUnit.Framework;
using CycloneDDS.Runtime;
using FeatureDemo;
using FeatureDemo.Scenarios.BlackBox;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Linq;

namespace FeatureDemo.Tests;

public class BlackBoxTests
{
    [Test]
    public async Task BlackBox_LateJoiner_ReceivesHistory()
    {
        using var participant = new DdsParticipant(80);
        
        // 1. Create Publisher and publish data
        using (var publisher = new BlackBoxPublisher(participant))
        {
            publisher.PublishCriticalLogs();
            // Ensure data is in the writer history
            // Actually, we need to keep the publisher alive for TransientLocal (it's not Persistent).
            // TransientLocal means "As long as the Writer is alive, late joiners get data".
            
            // Wait a moment for data to be written to history
            await Task.Delay(500);

            // 2. Create Subscribers AFTER publishing
            using var subscriber = new BlackBoxSubscriber(participant);
            var cts = new CancellationTokenSource();
            cts.CancelAfter(5000);

            // 3. Verify subscriber gets the "old" data
            var logs = await subscriber.WaitForHistoricalLogsAsync(7, cts.Token);
            
            Assert.That(logs.Count, Is.EqualTo(7), "Should receive all 7 historical logs");
            Assert.That(logs.First().Message.ToString(), Is.EqualTo("Boot sequence initiated"));
            Assert.That(logs.Last().Message.ToString(), Is.EqualTo("Safe mode active"));
        }
    }
}
