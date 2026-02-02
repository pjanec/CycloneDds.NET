using System;
using System.Threading;
using System.Threading.Tasks;
using FeatureDemo.Orchestration;
using CycloneDDS.Runtime;
using Spectre.Console;
using System.Collections.Generic;

namespace FeatureDemo.Scenarios.BlackBox;

public class BlackBoxScenario : ScenarioBase
{
    public override string Name => "Black Box (Durability/QoS)";
    public override string Description => "Demonstrates TransientLocal durability with late-joining subscriber.";

    public BlackBoxScenario(DemoOrchestrator orchestrator) : base(orchestrator)
    {
    }

    public override async Task RunStandaloneAsync(CancellationToken ct)
    {
        PrintScenarioHeader();
        AnsiConsole.MarkupLine("[yellow]This scenario demonstrates Durability: TransientLocal + KeepAll.[/]");
        AnsiConsole.MarkupLine("We will publish data [bold]BEFORE[/] creating the subscriber.");
        AnsiConsole.MarkupLine("The subscriber should receive [bold]ALL[/] historical logs.");
        AnsiConsole.WriteLine();

        using var participant = new DdsParticipant();
        
        // Step 1: Create Publisher
        using var publisher = new BlackBoxPublisher(participant);
        AnsiConsole.MarkupLine("[green]Step 1: Publisher created (TransientLocal).[/]");
        
        AnsiConsole.MarkupLine("Publishing 7 critical system logs...");
        publisher.PublishCriticalLogs();
        AnsiConsole.MarkupLine("[green]Logs published to Writer History.[/]");
        
        // Step 2: Wait
        AnsiConsole.MarkupLine("[yellow]Step 2: Waiting 2 seconds (Simulating delay before subscriber join)...[/]");
        await Task.Delay(2000, ct);

        // Step 3: Create Subscriber
        AnsiConsole.MarkupLine("[green]Step 3: Creating Late-Joining Subscriber (TransientLocal).[/]");
        using var subscriber = new BlackBoxSubscriber(participant);
        
        // Step 4: Verify
        AnsiConsole.MarkupLine("[bold]Waiting for historical data...[/]");
        
        try
        {
            var logs = await subscriber.WaitForHistoricalLogsAsync(7, ct);
            
            AnsiConsole.MarkupLine($"[green]Received {logs.Count} logs from history![/]");
            
            var ui = new BlackBoxUI();
            ui.DisplayLogs(logs);
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[red]Operation cancelled.[/]");
        }
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Press [blue]Enter[/] to finish...");
        if (!ct.IsCancellationRequested)
        {
             try { await Task.Run(() => Console.ReadLine(), ct); } catch {}
        }
    }

    public override async Task RunPublisherAsync(CancellationToken ct)
    {
         // For Master/Slave mode
         // Master publishes data then waits.
         PrintScenarioHeader();
         AnsiConsole.MarkupLine("[yellow]Master Node: Publishing Logs...[/]");
         
         using var participant = new DdsParticipant();
         using var publisher = new BlackBoxPublisher(participant);
         
         publisher.PublishCriticalLogs();
         AnsiConsole.MarkupLine("[green]Logs published! Waiting for Slaves to read history...[/]");
         
         // Keep alive so TransientLocal history is available
         try { await Task.Delay(-1, ct); } catch {}
    }

    public override async Task RunSubscriberAsync(CancellationToken ct)
    {
         // Slave node
         PrintScenarioHeader();
         AnsiConsole.MarkupLine("[yellow]Slave Node: Joining Late...[/]");
         
         using var participant = new DdsParticipant();
         using var subscriber = new BlackBoxSubscriber(participant);
         
         try
         {
             var logs = await subscriber.WaitForHistoricalLogsAsync(7, ct);
             
             var ui = new BlackBoxUI();
             ui.DisplayLogs(logs);
             
             AnsiConsole.MarkupLine("[green]Press Ctrl+C to exit.[/]");
             await Task.Delay(-1, ct);
         }
         catch (OperationCanceledException) {}
    }
}
