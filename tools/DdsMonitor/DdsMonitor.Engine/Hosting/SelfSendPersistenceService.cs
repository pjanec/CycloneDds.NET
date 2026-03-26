using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace DdsMonitor.Engine.Hosting;

/// <summary>
/// Hosted service that persists the <see cref="DevelSettings.SelfSendEnabled"/> state
/// by hooking the workspace save/load events published by <see cref="IEventBroker"/>.
///
/// <para>
/// On <see cref="WorkspaceSavingEvent"/> the current <c>SelfSendEnabled</c> value is
/// written to <c>PluginSettings["SelfSend"]</c>.
/// </para>
/// <para>
/// On <see cref="WorkspaceLoadedEvent"/> the stored value is read and applied to
/// <see cref="DevelSettings"/> so that the self-send toggle is restored to its last
/// saved state.  When the key is absent (fresh workspace) self-send remains <c>false</c>.
/// </para>
/// </summary>
public sealed class SelfSendPersistenceService : IHostedService, IDisposable
{
    private const string WorkspaceKey = "SelfSend";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
    };

    private readonly DevelSettings _develSettings;
    private readonly IEventBroker _eventBroker;

    private IDisposable? _savingSub;
    private IDisposable? _loadedSub;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    public SelfSendPersistenceService(DevelSettings develSettings, IEventBroker eventBroker)
    {
        _develSettings = develSettings ?? throw new ArgumentNullException(nameof(develSettings));
        _eventBroker = eventBroker ?? throw new ArgumentNullException(nameof(eventBroker));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _savingSub = _eventBroker.Subscribe<WorkspaceSavingEvent>(OnWorkspaceSaving);
        _loadedSub = _eventBroker.Subscribe<WorkspaceLoadedEvent>(OnWorkspaceLoaded);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        _savingSub?.Dispose();
        _loadedSub?.Dispose();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnWorkspaceSaving(WorkspaceSavingEvent e)
    {
        try
        {
            e.PluginSettings[WorkspaceKey] = new { Enabled = _develSettings.SelfSendEnabled };
        }
        catch { }
    }

    private void OnWorkspaceLoaded(WorkspaceLoadedEvent e)
    {
        if (!e.PluginSettings.TryGetValue(WorkspaceKey, out var raw))
        {
            // Key absent → no saved state; leave self-send at its current (default off) state.
            return;
        }

        try
        {
            bool? enabled = null;

            if (raw is JsonElement elem)
            {
                if (elem.ValueKind == JsonValueKind.Object &&
                    elem.TryGetProperty("Enabled", out var ep))
                {
                    enabled = ep.GetBoolean();
                }
                else if (elem.ValueKind == JsonValueKind.True)
                {
                    enabled = true;
                }
                else if (elem.ValueKind == JsonValueKind.False)
                {
                    enabled = false;
                }
            }

            if (enabled.HasValue)
            {
                _develSettings.SelfSendEnabled = enabled.Value;
            }
        }
        catch { }
    }
}
