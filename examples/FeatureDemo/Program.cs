using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FeatureDemo.Orchestration;
using FeatureDemo.Scenarios;
using FeatureDemo.Scenarios.ChatRoom;
using FeatureDemo.UI;
using Spectre.Console;

namespace FeatureDemo;

/// <summary>
/// Entry point for the FeatureDemo application.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            var mode = ParseArguments(args);

            if (mode == DemoMode.Interactive)
            {
                mode = PromptForMode();
            }

            await RunDemoAsync(mode);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// Parses command-line arguments to determine demo mode.
    /// </summary>
    public static DemoMode ParseArguments(string[] args)
    {
        if (args.Length == 0)
        {
            return DemoMode.Interactive;
        }

        // Look for --mode argument
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--mode" && i + 1 < args.Length)
            {
                var modeStr = args[i + 1].ToLowerInvariant();
                return modeStr switch
                {
                    "standalone" => DemoMode.Standalone,
                    "master" => DemoMode.Master,
                    "slave" => DemoMode.Slave,
                    "autonomous" => DemoMode.Autonomous,
                    _ => throw new ArgumentException($"Unknown mode: {args[i + 1]}")
                };
            }
        }

        return DemoMode.Interactive;
    }

    /// <summary>
    /// Prompts user to select a demo mode interactively.
    /// </summary>
    private static DemoMode PromptForMode()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("FastCycloneDDS")
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[bold]Feature Demo Application[/]");
        AnsiConsole.WriteLine();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Select Mode:[/]")
                .AddChoices(
                    "Standalone (Run all in this process)",
                    "Master (Control Node)",
                    "Slave (Subscriber Node)",
                    "Autonomous Demo (Auto-run all)"));

        return choice switch
        {
            "Standalone (Run all in this process)" => DemoMode.Standalone,
            "Master (Control Node)" => DemoMode.Master,
            "Slave (Subscriber Node)" => DemoMode.Slave,
            "Autonomous Demo (Auto-run all)" => DemoMode.Autonomous,
            _ => DemoMode.Standalone
        };
    }

    /// <summary>
    /// Runs the demo in the specified mode.
    /// </summary>
    private static async Task RunDemoAsync(DemoMode mode)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[green]Starting in {mode} mode...[/]");
        AnsiConsole.WriteLine();

        using var orchestrator = new DemoOrchestrator(mode);

        if (mode == DemoMode.Autonomous)
        {
            var runner = new AutonomousDemoRunner(orchestrator);
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => {
                e.Cancel = true;
                cts.Cancel();
            };
            await runner.RunAsync(cts.Token);
            return;
        }

        // Initialize Scenarios
        var scenarios = new List<IDemoScenario>
        {
            new ChatRoomScenario(orchestrator),
            new FeatureDemo.Scenarios.SensorArray.SensorArrayScenario(orchestrator),
            new FeatureDemo.Scenarios.StockTicker.StockTickerScenario(orchestrator),
            new FeatureDemo.Scenarios.FlightRadar.FlightRadarScenario(orchestrator),
            new FeatureDemo.Scenarios.BlackBox.BlackBoxScenario(orchestrator)
        };

        if (mode == DemoMode.Slave)
        {
            await RunSlaveLoopAsync(orchestrator, scenarios);
        }
        else
        {
            // Master/Standalone/Interactive Logic: Show Menu
            var menu = new MainMenu(orchestrator, scenarios);
            await menu.ShowAsync();
        }
    }

    private static async Task RunSlaveLoopAsync(DemoOrchestrator orchestrator, List<IDemoScenario> scenarios)
    {
        var control = orchestrator.GetControlChannel();
        using var cts = new CancellationTokenSource();
        
        Console.CancelKeyPress += (s, e) => {
            e.Cancel = true;
            cts.Cancel();
        };

        // Send initial handshake so Master knows we exist
        await control.SendHandshakeAsync();
        
        AnsiConsole.MarkupLine("[cyan]Slave Node Ready. Waiting for commands from Master...[/]");

        try 
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var cmd = await control.WaitForCommandAsync(cts.Token);
                
                if (cmd.Command == ControlCommand.StartScenario)
                {
                    var scenarioId = cmd.ScenarioId;
                    if (scenarioId > 0 && scenarioId <= scenarios.Count)
                    {
                        var scenario = scenarios[scenarioId - 1];
                        AnsiConsole.MarkupLine($"[green]Received Start Command for: {scenario.Name}[/]");
                        
                        await orchestrator.RunScenarioAsync(scenario, scenarioId);
                        
                        AnsiConsole.MarkupLine("[cyan]Scenario finished. Waiting for next command...[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Received Unknown Scenario ID: {scenarioId}[/]");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Slave loop stopped.[/]");
        }
    }
}
