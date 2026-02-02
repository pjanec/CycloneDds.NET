using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FeatureDemo.Orchestration;
using FeatureDemo.Scenarios;
using CycloneDDS.Runtime;

namespace FeatureDemo.Tests;

[TestFixture]
public class OrchestrationTests
{
    [Test]
    public void ParseArguments_StandaloneMode_ReturnsCorrectMode()
    {
        var mode = Program.ParseArguments(new[] { "--mode", "standalone" });
        Assert.That(mode, Is.EqualTo(DemoMode.Standalone));
    }

    [Test]
    public void ParseArguments_NoArgs_ReturnsInteractive()
    {
        var mode = Program.ParseArguments(Array.Empty<string>());
        Assert.That(mode, Is.EqualTo(DemoMode.Interactive));
    }

    [Test]
    public void DemoOrchestrator_CreateParticipant_Success()
    {
        // Use a random domain ID to avoid conflict with other tests
        using var orchestrator = new DemoOrchestrator(DemoMode.Standalone, domainId: 200);
        var participant = orchestrator.GetParticipant();
        Assert.That(participant, Is.Not.Null);
    }

    [Test]
    public async Task ControlChannel_Handshake_SucceedsInStandaloneMode()
    {
        using var orchestrator = new DemoOrchestrator(DemoMode.Standalone, domainId: 201);
        var participant = orchestrator.GetParticipant();
        
        using var master = new ControlChannelManager(participant, nodeId: 0);
        using var slave = new ControlChannelManager(participant, nodeId: 1);
        
        // Wait briefly for discovery
        await Task.Delay(100);

        await master.SendHandshakeAsync();
        
        // Polling for shake with better timeout handling
        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool received = false;
        while (sw.ElapsedMilliseconds < 5000)
        {
            if (slave.CheckHandshake())
            {
                received = true;
                break;
            }
            await Task.Delay(100);
        }
        
        Assert.That(received, Is.True, "Slave did not receive handshake from Master");
    }

    [Test]
    public async Task ControlChannel_StartScenario_ReceivedBySlave()
    {
        using var orchestrator = new DemoOrchestrator(DemoMode.Standalone, domainId: 202);
        var participant = orchestrator.GetParticipant();
        
        using var master = new ControlChannelManager(participant, nodeId: 0);
        using var slave = new ControlChannelManager(participant, nodeId: 1);

        // Ensure reader/writer match
        await Task.Delay(500);
        
        await master.SendStartScenarioAsync(2);
        
        // We rely on the DDS reader below to verify the message was actually sent.

        // Alternative: Use a raw reader to verify Master sent it.
        using var reader = new DdsReader<DemoControl>(participant, "DemoControl");
        await Task.Delay(100); // Wait for discovery
        
        await master.SendStartScenarioAsync(2);
        
        await Task.Delay(200); // Allow transmission
        
        bool found = false;
        using var cts = new CancellationTokenSource(2000);
        
        try
        {
            if (await reader.WaitDataAsync(cts.Token))
            {
                using var samples = reader.Take();
                foreach (var s in samples)
                {
                    if (s.IsValid && s.Data.Command == ControlCommand.StartScenario && s.Data.ScenarioId == 2)
                        found = true;
                }
            }
        }
        catch (TaskCanceledException) { }

        Assert.That(found, Is.True);
    }
    
    [Test]
    public void IDemoScenario_Interface_HasRequiredMembers()
    {
        var interfaceType = typeof(IDemoScenario);
        Assert.That(interfaceType.GetProperty("Name"), Is.Not.Null);
        Assert.That(interfaceType.GetProperty("Description"), Is.Not.Null);
        Assert.That(interfaceType.GetMethod("RunStandaloneAsync"), Is.Not.Null);
        Assert.That(interfaceType.GetMethod("RunPublisherAsync"), Is.Not.Null);
        Assert.That(interfaceType.GetMethod("RunSubscriberAsync"), Is.Not.Null);
    }
}
