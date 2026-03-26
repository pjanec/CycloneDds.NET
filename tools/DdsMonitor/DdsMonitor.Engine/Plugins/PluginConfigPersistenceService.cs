using System;
using System.Threading;
using System.Threading.Tasks;
using DdsMonitor.Engine;
using Microsoft.Extensions.Hosting;

namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Singleton hosted service that wires <see cref="PluginConfigService"/> into the
/// workspace save/load event infrastructure.
///
/// <para>
/// On <see cref="WorkspaceSavingEvent"/> the <c>PluginSettings["EnabledPlugins"]</c>
/// key is populated with the current enabled-plugin set so it is persisted together with
/// all other settings in <c>workspace.json</c>.
/// </para>
/// <para>
/// On <see cref="WorkspaceLoadedEvent"/> the enabled-plugin set is refreshed from the
/// loaded workspace so that loading a different workspace file also switches the active
/// plugin selection.
/// </para>
/// </summary>
public sealed class PluginConfigPersistenceService : IHostedService, IDisposable
{
    private readonly PluginConfigService _pluginConfigService;
    private readonly IEventBroker _eventBroker;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    public PluginConfigPersistenceService(PluginConfigService pluginConfigService, IEventBroker eventBroker)
    {
        _pluginConfigService = pluginConfigService ?? throw new ArgumentNullException(nameof(pluginConfigService));
        _eventBroker = eventBroker ?? throw new ArgumentNullException(nameof(eventBroker));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _pluginConfigService.SetupPersistence(_eventBroker);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose() { }
}
