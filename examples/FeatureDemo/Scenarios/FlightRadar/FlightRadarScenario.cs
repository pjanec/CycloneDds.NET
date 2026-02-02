using System;
using System.Threading;
using System.Threading.Tasks;
using FeatureDemo.Orchestration;
using CycloneDDS.Runtime;
using Spectre.Console;

namespace FeatureDemo.Scenarios.FlightRadar;

public class FlightRadarScenario : ScenarioBase
{
    public override string Name => "Flight Radar (Keyed Instances)";
    public override string Description => "Demonstrates keyed topic instances and lookup API.";

    public FlightRadarScenario(DemoOrchestrator orchestrator) : base(orchestrator)
    {
    }

    public override async Task RunStandaloneAsync(CancellationToken ct)
    {
        PrintScenarioHeader();
        
        using var participant = new DdsParticipant(); 
        using var publisher = new FlightPublisher(participant);
        using var subscriber = new FlightSubscriber(participant);
        
        // Start publishing
        var pubTask = publisher.StartPublishingAsync(20, ct); 
        
        // Simple Console UI loop
        try {
             while(!ct.IsCancellationRequested)
             {
                 await Task.Delay(1000, ct);
                 AnsiConsole.Clear();
                 PrintScenarioHeader();
                 
                 var flights = new[] { "BA-123", "LH-456", "AF-789", "UA-101", "KL-202" };
                 var table = new Table().Border(TableBorder.Rounded).Title("Flight Radar (Instance Lookup)");
                 table.AddColumn("Flight ID");
                 table.AddColumn("Lat");
                 table.AddColumn("Lon");
                 table.AddColumn("Alt");
                 table.AddColumn("History Samples");
                 
                 foreach(var fid in flights)
                 {
                     var hist = subscriber.GetHistoryForFlight(fid);
                     if (hist.Count > 0)
                     {
                         var latest = hist[hist.Count - 1]; // Last is latest usually
                         table.AddRow(
                             fid, 
                             latest.Latitude.ToString("F4"), 
                             latest.Longitude.ToString("F4"), 
                             latest.Altitude.ToString("F0"),
                             hist.Count.ToString()
                         );
                     }
                     else
                     {
                         table.AddRow(fid, "-", "-", "-", "0");
                     }
                 }
                 
                 AnsiConsole.Write(table);
                 AnsiConsole.MarkupLine("[dim]Press Ctrl+C to exit[/]");
             }
        }
        catch (OperationCanceledException) {}
        finally
        {
            publisher.Stop();
            try { await pubTask; } catch {}
        }
    }

    public override async Task RunPublisherAsync(CancellationToken ct)
    {
        PrintScenarioHeader();
        using var participant = new DdsParticipant();
        using var publisher = new FlightPublisher(participant);
        
        AnsiConsole.MarkupLine("[green]Publishing Flight Data...[/]");
        await publisher.StartPublishingAsync(20, ct);
    }

    public override async Task RunSubscriberAsync(CancellationToken ct)
    {
        PrintScenarioHeader();
        using var participant = new DdsParticipant();
        using var subscriber = new FlightSubscriber(participant);
         
        try {
             while(!ct.IsCancellationRequested)
             {
                 await Task.Delay(1000, ct);
                 AnsiConsole.Clear();
                 PrintScenarioHeader();
                 
                 var flights = new[] { "BA-123", "LH-456", "AF-789", "UA-101", "KL-202" };
                 var table = new Table().Border(TableBorder.Rounded).Title("Flight Radar (Instance Lookup)");
                 table.AddColumn("Flight ID");
                 table.AddColumn("Lat");
                 table.AddColumn("Lon");
                 table.AddColumn("Alt");
                 table.AddColumn("History Samples");
                 
                 foreach(var fid in flights)
                 {
                     var hist = subscriber.GetHistoryForFlight(fid);
                     if (hist.Count > 0)
                     {
                         var latest = hist[hist.Count - 1];
                         table.AddRow(
                             fid, 
                             latest.Latitude.ToString("F4"), 
                             latest.Longitude.ToString("F4"), 
                             latest.Altitude.ToString("F0"),
                             hist.Count.ToString()
                         );
                     }
                     else
                     {
                         table.AddRow(fid, "-", "-", "-", "0");
                     }
                 }
                 
                 AnsiConsole.Write(table);
             }
        }
        catch (OperationCanceledException) {}
    }
}
