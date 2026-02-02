using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FeatureDemo.Orchestration;
using FeatureDemo.Scenarios;
using Spectre.Console;

namespace FeatureDemo.UI;

public class MainMenu
{
    private readonly DemoOrchestrator _orchestrator;
    private readonly List<IDemoScenario> _scenarios;

    public MainMenu(DemoOrchestrator orchestrator, IEnumerable<IDemoScenario> scenarios)
    {
        _orchestrator = orchestrator;
        _scenarios = new List<IDemoScenario>(scenarios);
    }

    public async Task ShowAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();
            new DiagnosticHeader(_orchestrator.Mode, _orchestrator.DomainId).Render();
            
            AnsiConsole.Write(
                new FigletText("Feature Demo")
                    .Color(Color.Blue));

            var choices = new List<string>();
            foreach(var s in _scenarios)
            {
                choices.Add(s.Name);
            }
            choices.Add("Autonomous Demo");
            choices.Add("Exit");

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Select scenario:[/]")
                    .AddChoices(choices));

            if (selection == "Exit")
            {
                break;
            }

            if (selection == "Autonomous Demo")
            {
                 var runner = new AutonomousDemoRunner(_orchestrator);
                 using var cts = new System.Threading.CancellationTokenSource();
                 
                 // How to handle cancellation in menu?
                 // If user presses Ctrl+C, it might kill process. 
                 // We can rely on Runner to break loop on cancellation.
                 // But we need to feed it a token.
                 // If we want to return to menu, we need user input to cancel.
                 // But AnsiConsole has no easy "Press key to cancel task".
                 // Let's assume user interrupts with Ctrl+C which exits program.
                 
                 AnsiConsole.MarkupLine("[yellow]Press Ctrl+C to exit autonomous mode (will exit app).[/]");
                 
                 await runner.RunAsync(cts.Token);
                 continue;
            }

            var scenario = _scenarios.Find(s => s.Name == selection);
            if (scenario != null)
            {
                 int scenarioId = _scenarios.IndexOf(scenario) + 1;
                 // We rely on DemoOrchestrator to run it.
                 // Orchestrator handles Master/Slave/Standalone logic.
                 await _orchestrator.RunScenarioAsync(scenario, scenarioId);
                 
                 AnsiConsole.WriteLine();
                 AnsiConsole.MarkupLine("[dim]Press any key to return to menu...[/]");
                 Console.ReadKey(true);
            }
        }
    }
}
