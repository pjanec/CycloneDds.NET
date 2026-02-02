using System;
using System.Threading;
using System.Threading.Tasks;
using FeatureDemo.Scenarios;
using FeatureDemo.Scenarios.ChatRoom;
using FeatureDemo.Scenarios.SensorArray;
using FeatureDemo.Scenarios.StockTicker;
using FeatureDemo.Scenarios.FlightRadar;
using FeatureDemo.Scenarios.BlackBox;
using Spectre.Console;

namespace FeatureDemo.Orchestration;

public class AutonomousDemoRunner
{
    private readonly DemoOrchestrator _orchestrator;

    public AutonomousDemoRunner(DemoOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var scenarios = new ScenarioBase[]
        {
            new ChatRoomScenario(_orchestrator),
            new SensorArrayScenario(_orchestrator),
            new StockTickerScenario(_orchestrator),
            new FlightRadarScenario(_orchestrator),
            new BlackBoxScenario(_orchestrator)
        };

        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("Autonomous Mode").Color(Color.Green));
        AnsiConsole.MarkupLine("[yellow]Running all scenarios in a loop... (Press Ctrl+C to exit)[/]");
        await Task.Delay(2000, ct);

        while (!ct.IsCancellationRequested)
        {
            foreach (var scenario in scenarios)
            {
                if (ct.IsCancellationRequested) break;

                AnsiConsole.Clear();
                AnsiConsole.Write(new Rule($"[bold cyan]{scenario.Name}[/]").RuleStyle("grey"));
                AnsiConsole.MarkupLine($"[dim]{scenario.Description}[/]");
                AnsiConsole.WriteLine();
                
                // Countdown
                for (int i = 3; i > 0; i--)
                {
                    AnsiConsole.Markup($"Starting in {i}... \r");
                    await Task.Delay(1000, ct);
                }
                AnsiConsole.WriteLine("                   "); // Clear line

                // Run the scenario in Standalone mode for a fixed duration
                // We use a linked TS to enforce timeout
                using var scenarioCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                scenarioCts.CancelAfter(TimeSpan.FromSeconds(15)); // Run each for 15 seconds

                try
                {
                    await scenario.RunStandaloneAsync(scenarioCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected when time runs out
                }
                catch (Exception ex)
                {
                    AnsiConsole.WriteException(ex);
                    AnsiConsole.MarkupLine("[red]Scenario failed. Continuing to next...[/]");
                    await Task.Delay(2000, ct);
                }
            }
        }
    }
}
