using System;
using System.Threading;
using System.Threading.Tasks;
using FeatureDemo.Orchestration;
using Spectre.Console;

namespace FeatureDemo.Scenarios.ChatRoom;

public class ChatRoomScenario : ScenarioBase
{
    public override string Name => "Chat Room";
    public override string Description => "Basic Pub/Sub with reliable delivery";

    public ChatRoomScenario(DemoOrchestrator orchestrator) : base(orchestrator)
    {
    }

    public override async Task RunStandaloneAsync(CancellationToken ct)
    {
        PrintScenarioHeader();
        AnsiConsole.MarkupLine("[yellow]Running in Standalone Mode (Single Process)[/]");
        AnsiConsole.MarkupLine("Messages you type will be received by the subscriber running in background.");
        AnsiConsole.WriteLine();

        // 1. Setup Subscriber with Secondary Participant
        var subParticipant = Orchestrator.GetSecondParticipant();
        using var subscriber = new ChatRoomSubscriber(subParticipant);
        var ui = new ChatRoomUI("App User");

        var subscriberTask = Task.Run(() => subscriber.WaitForMessagesAsync((msg, sender) =>
        {
            if (msg.HasValue)
            {
                // UI is not thread safe, so use a lock if needed, or AnsiConsole synchronized methods
                // Spectre.Console is generally thread-safe for writing but layout might break
                // For this demo we'll just write.
                ui.DisplayMessage(msg.Value, sender);
            }
            return true;
        }, ct), ct);

        // 2. Setup Publisher with Primary Participant
        var pubParticipant = Orchestrator.GetParticipant();
        using var publisher = new ChatRoomPublisher(pubParticipant);

        // 3. User Input Loop
        await RunInputLoopAsync(publisher, "Master", ct);

        // Cleanup
        await subscriberTask; 
        // Note: subscriberTask only finishes when ct is cancelled, causing WaitForMessagesAsync to return
    }

    public override async Task RunPublisherAsync(CancellationToken ct)
    {
        PrintScenarioHeader();
        AnsiConsole.MarkupLine("[green]Running as Publisher (Master)[/]");
        AnsiConsole.MarkupLine("Type messages to send to the Subscriber node.");
        AnsiConsole.WriteLine();

        var participant = Orchestrator.GetParticipant();
        using var publisher = new ChatRoomPublisher(participant);

        await RunInputLoopAsync(publisher, "Master", ct);
    }

    public override async Task RunSubscriberAsync(CancellationToken ct)
    {
        PrintScenarioHeader();
        AnsiConsole.MarkupLine("[blue]Running as Subscriber (Slave)[/]");
        AnsiConsole.MarkupLine("Waiting for messages from Master...");
        AnsiConsole.WriteLine();

        var participant = Orchestrator.GetParticipant();
        using var subscriber = new ChatRoomSubscriber(participant);
        var ui = new ChatRoomUI("Slave");

        await subscriber.WaitForMessagesAsync((msg, sender) =>
        {
            if (msg.HasValue)
            {
                ui.DisplayMessage(msg.Value, sender);
            }
            return true;
        }, ct);
    }

    private async Task RunInputLoopAsync(ChatRoomPublisher publisher, string username, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // We use Task.Run to allow cancellation of the input wait if possible, 
            // though Console.ReadLine is hard to cancel. 
            // We'll just check token before asking.
            
            if (ct.IsCancellationRequested) break;

            AnsiConsole.Markup("[grey]Type a message (or 'exit' to quit):[/] ");
            var input = await Task.Run(Console.ReadLine, ct);

            if (input == "exit" || input == null)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(input))
            {
                publisher.SendMessage(username, input);
            }
        }
    }
}
