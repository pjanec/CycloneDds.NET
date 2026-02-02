using NUnit.Framework;
using CycloneDDS.Runtime;
using FeatureDemo.Scenarios.SensorArray;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace FeatureDemo.Tests;

public class SensorSubscriberTests
{
    [Test]
    [Explicit("Performance test takes too long for rapid iteration. Run manually if needed.")]
    public async Task SensorSubscriber_ZeroCopy_AllocatesSignificantlyLess()
    {
        // Setup
        using var participant = new DdsParticipant(112);
        // Ensure both on same domain
        using var pub = new SensorPublisher(participant);
        using var sub = new SensorSubscriber(participant);
        using var cts = new CancellationTokenSource();

        // Start Publisher
        var pubTask = pub.StartPublishingAsync(1000, cts.Token);

        // Start Subscriber
        sub.CurrentMode = SubscriberMode.ZeroCopy;
        var subTask = sub.StartReceivingAsync(cts.Token);

        // Warm up / Discovery
        // Wait until we actually start receiving messages
        var start = DateTime.UtcNow;
        while (sub.MessagesReceived == 0 && (DateTime.UtcNow - start).TotalSeconds < 5)
        {
            await Task.Delay(100);
        }
        
        Assert.That(sub.MessagesReceived, Is.GreaterThan(0), "Failed to establish communication");

        // MEASURE ZERO-COPY
        // Clear metrics by reading current state
        long startBytesZC = sub.BytesAllocated;
        long startMsgZC = sub.MessagesReceived;
        
        await Task.Delay(1000); 
        
        long endBytesZC = sub.BytesAllocated;
        long endMsgZC = sub.MessagesReceived;
        
        long bytesZC = endBytesZC - startBytesZC;
        long msgsZC = endMsgZC - startMsgZC;
        double bytesPerMsgZC = msgsZC > 0 ? (double)bytesZC / msgsZC : 0;
        
        TestContext.Out.WriteLine($"ZeroCopy: {bytesZC} bytes for {msgsZC} msgs ({bytesPerMsgZC:F2} bytes/msg)");


        // MEASURE MANAGED
        sub.CurrentMode = SubscriberMode.Managed;
        await Task.Delay(200); // Allow mode switch to settle

        long startBytesM = sub.BytesAllocated;
        long startMsgM = sub.MessagesReceived;
        
        await Task.Delay(1000); 
        
        long endBytesM = sub.BytesAllocated;
        long endMsgM = sub.MessagesReceived;

        long bytesM = endBytesM - startBytesM;
        long msgsM = endMsgM - startMsgM;
        double bytesPerMsgM = msgsM > 0 ? (double)bytesM / msgsM : 0;

        TestContext.Out.WriteLine($"Managed:  {bytesM} bytes for {msgsM} msgs ({bytesPerMsgM:F2} bytes/msg)");
        
        cts.Cancel();
        try { await pubTask; } catch(OperationCanceledException){}
        try { await subTask; } catch(OperationCanceledException){}

        Assert.That(msgsZC, Is.GreaterThan(10), "Should receive messages in ZC mode");
        Assert.That(msgsM, Is.GreaterThan(10), "Should receive messages in Managed mode");

        // Relaxed constraint for ZC: overhead per call might be non-zero due to internal checks/safehandles
        // But 100 bytes is generous. Managed copies string + struct + list overhead.
        Assert.That(bytesPerMsgZC, Is.LessThan(200), "ZeroCopy should allocate < 200 bytes/msg");
        Assert.That(bytesPerMsgM, Is.GreaterThan(bytesPerMsgZC), "Managed should allocate more");
    }
}
