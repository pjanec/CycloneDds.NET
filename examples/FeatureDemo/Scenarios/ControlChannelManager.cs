using System;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using Spectre.Console;

namespace FeatureDemo.Scenarios;

/// <summary>
/// Manages the control channel for orchestrating demos between Master and Slave nodes.
/// Handles handshake, scenario synchronization, and command distribution.
/// </summary>
public class ControlChannelManager : IDisposable
{
    private readonly DdsParticipant _participant;
    private readonly byte _nodeId;
    private readonly DdsWriter<DemoControl> _writer;
    private readonly DdsReader<DemoControl> _reader;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the ControlChannelManager.
    /// </summary>
    /// <param name="participant">The DDS participant to use.</param>
    /// <param name="nodeId">Node identifier (0 = Master, 1 = Slave).</param>
    public ControlChannelManager(DdsParticipant participant, byte nodeId)
    {
        _participant = participant ?? throw new ArgumentNullException(nameof(participant));
        _nodeId = nodeId;

        _writer = new DdsWriter<DemoControl>(_participant, "DemoControl");
        _reader = new DdsReader<DemoControl>(_participant, "DemoControl");
    }

    /// <summary>
    /// Sends a handshake message to discover peers.
    /// </summary>
    public async Task SendHandshakeAsync()
    {
        var control = new DemoControl
        {
            NodeId = _nodeId,
            Command = ControlCommand.Handshake,
            ScenarioId = 0,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        _writer.Write(control);
        await Task.Delay(10); // Small delay to ensure message propagates
    }

    /// <summary>
    /// Waits for a handshake message from a peer node.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for peer.</param>
    /// <returns>True if peer discovered, false if timeout.</returns>
    public async Task<bool> WaitForPeerHandshakeAsync(TimeSpan timeout)
    {
        var endTime = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < endTime)
        {
            var samples = _reader.Take();
            try
            {
                foreach (var sample in samples)
                {
                    if (sample.Data.NodeId != _nodeId && 
                        sample.Data.Command == ControlCommand.Handshake)
                    {
                        return true;
                    }
                }
            }
            finally
            {
                samples.Dispose();
            }

            await Task.Delay(100);
        }

        return false;
    }

    /// <summary>
    /// Checks if a handshake message has been received (non-blocking).
    /// </summary>
    /// <returns>True if handshake received.</returns>
    public bool CheckHandshake()
    {
        var samples = _reader.Take();
        try
        {
            foreach (var sample in samples)
            {
                if (sample.Data.NodeId != _nodeId && 
                    sample.Data.Command == ControlCommand.Handshake)
                {
                    return true;
                }
            }
        }
        finally
        {
            samples.Dispose();
        }
        return false;
    }

    /// <summary>
    /// Sends a command to start a specific scenario.
    /// </summary>
    /// <param name="scenarioId">The scenario ID to start (1-5).</param>
    public async Task SendStartScenarioAsync(int scenarioId)
    {
        var control = new DemoControl
        {
            NodeId = _nodeId,
            Command = ControlCommand.StartScenario,
            ScenarioId = scenarioId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        _writer.Write(control);
        await Task.Delay(10); // Small delay to ensure message propagates
    }

    /// <summary>
    /// Sends a command to stop the current scenario.
    /// </summary>
    public async Task SendStopScenarioAsync()
    {
        var control = new DemoControl
        {
            NodeId = _nodeId,
            Command = ControlCommand.StopScenario,
            ScenarioId = 0,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        _writer.Write(control);
        await Task.Delay(10); // Small delay to ensure message propagates
    }

    /// <summary>
    /// Sends an acknowledgment message.
    /// </summary>
    public async Task SendAckAsync()
    {
        var control = new DemoControl
        {
            NodeId = _nodeId,
            Command = ControlCommand.Ack,
            ScenarioId = 0,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        _writer.Write(control);
        await Task.Delay(10); // Small delay to ensure message propagates
    }

    /// <summary>
    /// Waits for a control command from peer.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The received control message.</returns>
    public async Task<DemoControl> WaitForCommandAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var samples = _reader.Take();
            try
            {
                foreach (var sample in samples)
                {
                    // Ignore messages from self
                    if (sample.Data.NodeId != _nodeId)
                    {
                        return sample.Data;
                    }
                }
            }
            finally
            {
                samples.Dispose();
            }

            await Task.Delay(100, ct);
        }

        throw new OperationCanceledException();
    }

    /// <summary>
    /// Waits for peer connection with diagnostic display.
    /// </summary>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <returns>True if connected, false if timeout.</returns>
    public async Task<bool> WaitForPeerWithDiagnosticsAsync(TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;
        var endTime = startTime + timeout;

        return await AnsiConsole.Status()
            .StartAsync("⏳ Waiting for peer connection...", async ctx =>
            {
                while (DateTime.UtcNow < endTime)
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    ctx.Status($"⏳ Waiting for peer... ({elapsed.TotalSeconds:F0}s elapsed)");

                    var samples = _reader.Take();
                    bool found = false;
                    try
                    {
                        foreach (var sample in samples)
                        {
                            if (sample.Data.NodeId != _nodeId && 
                                sample.Data.Command == ControlCommand.Handshake)
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                    finally
                    {
                        samples.Dispose();
                    }

                    if (found)
                    {
                        ctx.Status("✓ Peer discovered!");
                        await Task.Delay(500); // Show success briefly
                        return true;
                    }

                    await Task.Delay(100);
                }

                ctx.Status("✗ Timeout - no peer found");
                await Task.Delay(1000); // Show failure briefly
                return false;
            });
    }

    /// <summary>
    /// Disposes of resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _writer?.Dispose();
        _reader?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
