using System;
using System.Threading;
using System.Threading.Tasks;
using FeatureDemo.Orchestration;
using Spectre.Console;

namespace FeatureDemo.Scenarios;

/// <summary>
/// Base class for demo scenarios, providing common utilities.
/// </summary>
public abstract class ScenarioBase : IDemoScenario
{
    protected readonly DemoOrchestrator Orchestrator;

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract string Description { get; }

    protected ScenarioBase(DemoOrchestrator orchestrator)
    {
        Orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    /// <inheritdoc/>
    public abstract Task RunStandaloneAsync(CancellationToken ct);

    /// <inheritdoc/>
    public abstract Task RunPublisherAsync(CancellationToken ct);

    /// <inheritdoc/>
    public abstract Task RunSubscriberAsync(CancellationToken ct);

    /// <inheritdoc/>
    public virtual void DisplayInstructions()
    {
        AnsiConsole.MarkupLine($"[bold cyan]{Name}[/]");
        AnsiConsole.MarkupLine($"[grey]{Description}[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Helper to print a header within a scenario.
    /// </summary>
    protected void PrintScenarioHeader()
    {
        var rule = new Rule($"[yellow]{Name}[/]");
        rule.Justification = Justify.Left;
        AnsiConsole.Write(rule);
        AnsiConsole.MarkupLine($"[italic]{Description}[/]");
        AnsiConsole.WriteLine();
    }
}
