using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FeatureDemo.Scenarios.ChatRoom;
using FeatureDemo.Orchestration;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;

namespace FeatureDemo.Tests;

[TestFixture]
public class ChatRoomTests
{
    [Test]
    public void ChatRoomPublisher_SendMessage_DoesNotThrow()
    {
        using var orchestrator = new DemoOrchestrator(DemoMode.Standalone, domainId: 210);
        var participant = orchestrator.GetParticipant();
        
        using var publisher = new ChatRoomPublisher(participant);
        Assert.DoesNotThrow(() => publisher.SendMessage("Alice", "Hello World"));
    }

    [Test]
    public async Task ChatRoomSubscriber_ReceiveMessage_Success()
    {
        using var orchestrator = new DemoOrchestrator(DemoMode.Standalone, domainId: 211);
        var participant = orchestrator.GetParticipant();
        
        using var publisher = new ChatRoomPublisher(participant);
        using var subscriber = new ChatRoomSubscriber(participant);
        
        ChatMessage? received = null;
        var receiveTask = Task.Run(async () =>
        {
            await subscriber.WaitForMessagesAsync((msg, sender) =>
            {
                received = msg;
                return false; // Stop after first message
            }, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        });
        
        await Task.Delay(500); // Allow discovery
        publisher.SendMessage("Alice", "Test Message");
        
        await receiveTask;
        
        Assert.That(received, Is.Not.Null);
        Assert.That(received.Value.User.ToString(), Is.EqualTo("Alice"));
        Assert.That(received.Value.Content.ToString(), Contains.Substring("Test"));
    }

    [Test]
    public void ChatRoomUI_Render_DoesNotThrow()
    {
        var ui = new ChatRoomUI();
        var message = new ChatMessage
        {
            MessageId = 1,
            User = new FixedString32("Alice"),
            Content = new FixedString128("Hello"),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        
        // We can't easily assert console output, but we can check it doesn't crash
        Assert.DoesNotThrow(() => ui.DisplayMessage(message, "PC-001"));
    }
}
