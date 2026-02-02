using System;
using System.Threading;
using System.Threading.Tasks;
using FeatureDemo.Orchestration;
using FeatureDemo.Scenarios.SensorArray;
using Spectre.Console;

namespace FeatureDemo.Scenarios.SensorArray;

public class SensorArrayScenario : ScenarioBase
{
    public override string Name => "Sensor Array";
    public override string Description => "High-frequency Zero-Copy vs Managed performance demo";

    public SensorArrayScenario(DemoOrchestrator orchestrator) : base(orchestrator)
    {
    }

    public override async Task RunStandaloneAsync(CancellationToken ct)
    {
        PrintScenarioHeader();
        
        // In Standalone, we run both Publisher and Subscriber
        // Publisher in background
        var subParticipant = Orchestrator.GetSecondParticipant();
        using var publisher = new SensorPublisher(Orchestrator.GetParticipant());
        using var subscriber = new SensorSubscriber(subParticipant);
        
        // Start Publisher
        // Ask usage for rate? Default 1000
        int rate = 1000; // Fixed for simplicity or prompt?
        
        AnsiConsole.MarkupLine($"[green]Starting Publisher at {rate} Hz...[/]");
        var pubTask = publisher.StartPublishingAsync(rate, ct);

        // Start UI
        var ui = new SensorUI(subscriber);
        await ui.RunAsync(ct);
        
        publisher.Stop();
        try { await pubTask; } catch(OperationCanceledException){}
    }

    public override async Task RunPublisherAsync(CancellationToken ct)
    {
        PrintScenarioHeader();
        AnsiConsole.MarkupLine("[bold green]Mode: Publisher (Sensor Array)[/]");

        using var publisher = new SensorPublisher(Orchestrator.GetParticipant());
        
        // Interactive rate selection?
        var rate = AnsiConsole.Prompt(
            new TextPrompt<int>("Enter target publish rate (Hz):")
                .DefaultValue(1000)
                .Validate(r => r > 0 && r <= 20000 ? ValidationResult.Success() : ValidationResult.Error("Rate must be 1-20000")));

        // Start publishing
        AnsiConsole.MarkupLine($"Running publisher at {rate} Hz. Press Ctrl+C to stop.");
        
        await AnsiConsole.Status().StartAsync("Publishing...", async ctx => 
        {
             var task = publisher.StartPublishingAsync(rate, ct);
             
             // Update status with count
             while(!ct.IsCancellationRequested)
             {
                 ctx.Status($"Publishing... Sent: {publisher.MessagesSent:N0}");
                 await Task.Delay(500, ct);
             }
             try { await task; } catch(OperationCanceledException){}
        });
    }

    public override async Task RunSubscriberAsync(CancellationToken ct)
    {
        PrintScenarioHeader();
        AnsiConsole.MarkupLine("[bold blue]Mode: Subscriber (Visualization)[/]");

        using var subscriber = new SensorSubscriber(Orchestrator.GetParticipant());
        
        // The Subscriber logic is mainly the UI
        var ui = new SensorUI(subscriber);
        await ui.RunAsync(ct);
    }
}
