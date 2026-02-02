using System;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using FeatureDemo.Scenarios;
using Spectre.Console;

namespace FeatureDemo.Orchestration;

/// <summary>
/// Orchestrates the demo application, managing DDS participants and lifecycle.
/// </summary>
public class DemoOrchestrator : IDisposable
{
    private DdsParticipant? _participant;
    private DdsParticipant? _secondParticipant; // For standalone mode
    private ControlChannelManager? _controlChannel;
    private bool _disposed;

    /// <summary>
    /// Gets the current demo mode.
    /// </summary>
    public DemoMode Mode { get; }

    /// <summary>
    /// Gets the domain ID for DDS communication.
    /// </summary>
    public uint DomainId { get; }

    /// <summary>
    /// Initializes a new instance of the DemoOrchestrator.
    /// </summary>
    /// <param name="mode">The demo mode to operate in.</param>
    /// <param name="domainId">The DDS domain ID (default: 0).</param>
    public DemoOrchestrator(DemoMode mode, uint domainId = 0)
    {
        Mode = mode;
        DomainId = domainId;
    }

    /// <summary>
    /// Gets the primary DDS participant (creates if not exists).
    /// </summary>
    public DdsParticipant GetParticipant()
    {
        if (_participant == null)
        {
            _participant = new DdsParticipant(DomainId);
        }
        return _participant;
    }

    /// <summary>
    /// Gets the secondary DDS participant for standalone mode (creates if not exists).
    /// Only valid in Standalone mode.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if not in Standalone mode.</exception>
    public DdsParticipant GetSecondParticipant()
    {
        if (Mode != DemoMode.Standalone && Mode != DemoMode.Interactive)
        {
            throw new InvalidOperationException(
                "Secondary participant is only available in Standalone or Interactive mode.");
        }

        if (_secondParticipant == null)
        {
            _secondParticipant = new DdsParticipant(DomainId);
        }
        return _secondParticipant;
    }

    /// <summary>
    /// Gets the control channel manager, initializing it if necessary.
    /// </summary>
    public ControlChannelManager GetControlChannel()
    {
        if (_controlChannel == null)
        {
             byte nodeId = Mode == DemoMode.Master ? (byte)0 : (byte)1;
             // In standalone, node ID doesn't matter much for internal coordination but we'll use 0
             if (Mode == DemoMode.Standalone || Mode == DemoMode.Interactive) nodeId = 0;

             // Ensure participant is created
             var participant = GetParticipant();
             _controlChannel = new ControlChannelManager(participant, nodeId);
        }
        return _controlChannel;
    }

    /// <summary>
    /// Runs a specific scenario based on the orchestration mode.
    /// </summary>
    /// <param name="scenario">The scenario to run.</param>
    /// <param name="scenarioId">The ID of the scenario (used for Master/Slave coordination).</param>
    public async Task RunScenarioAsync(IDemoScenario scenario, int scenarioId = 0)
    {
        using var cts = new CancellationTokenSource();
        
        // Handle Ctrl+C to cancel
        Console.CancelKeyPress += (s, e) => {
            e.Cancel = true;
            cts.Cancel();
        };

        try 
        {
            scenario.DisplayInstructions();

            switch (Mode)
            {
                case DemoMode.Standalone:
                case DemoMode.Interactive:
                    AnsiConsole.MarkupLine("[yellow]Running in Standalone Mode...[/]");
                    await scenario.RunStandaloneAsync(cts.Token);
                    break;
                    
                case DemoMode.Master:
                    await RunAsMasterAsync(scenario, scenarioId, cts.Token);
                    break;
                    
                case DemoMode.Slave:
                    await RunAsSlaveAsync(scenario, cts.Token);
                    break;
                    
                case DemoMode.Autonomous:
                    AnsiConsole.MarkupLine("[yellow]Running in Autonomous Mode...[/]");
                     // TODO: Autonomous logic? for now use standalone
                    await scenario.RunStandaloneAsync(cts.Token);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("\n[yellow]Scenario cancelled by user.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
        }
    }

    private async Task RunAsMasterAsync(IDemoScenario scenario, int scenarioId, CancellationToken ct)
    {
        var control = GetControlChannel();
        
        await AnsiConsole.Status().StartAsync("Waiting for Client...", async ctx => 
        {
            ctx.Spinner(Spinner.Known.Dots);
            // Wait for handshake to ensure slave is online
            if(await control.WaitForPeerHandshakeAsync(TimeSpan.FromSeconds(30)))
            {
               ctx.Status("Client connected!");
               await Task.Delay(500);
            }
            else
            {
               AnsiConsole.MarkupLine("[red]Warning: No client failed to handshake. Starting anyway...[/]");
            }
        });

        AnsiConsole.MarkupLine($"[green]Starting Publisher for Scenario {scenarioId}...[/]");
        
        // Tell Slave to start this scenario
        await control.SendStartScenarioAsync(scenarioId);
        
        await scenario.RunPublisherAsync(ct);
        
        // When done (if ever), stop
        await control.SendStopScenarioAsync();
    }
    
    private async Task RunAsSlaveAsync(IDemoScenario scenario, CancellationToken ct)
    {
        var control = GetControlChannel();
        
        // In Slave mode, we are likely triggered BY the StartScenario command, 
        // so we can assume we are good to go, or we can do a quick handshake check.
        
        AnsiConsole.MarkupLine("[green]Starting Subscriber...[/]");
        await scenario.RunSubscriberAsync(ct);
    }

    /// <summary>
    /// Disposes of all DDS participants and resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _controlChannel?.Dispose();
        _controlChannel = null;

        _participant?.Dispose();
        _participant = null;

        _secondParticipant?.Dispose();
        _secondParticipant = null;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
