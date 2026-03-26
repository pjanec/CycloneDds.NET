using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DdsMonitor.Engine;
using Microsoft.Extensions.Hosting;

namespace DdsMonitor.Engine.AssemblyScanner;

/// <summary>
/// Singleton hosted service that persists the <see cref="AssemblySourceService"/> entry
/// list by hooking the workspace save/load events published by <see cref="IEventBroker"/>.
///
/// <para>
/// On <see cref="WorkspaceLoadedEvent"/> the assembly source paths are read from
/// <c>PluginSettings["AssemblySources"]</c> and the service is reloaded.  If the key is
/// absent a one-time migration from the legacy <c>assembly-sources.json</c> sidecar file
/// is already handled by <see cref="AssemblySourceService"/>'s constructor.
/// </para>
/// <para>
/// On <see cref="WorkspaceSavingEvent"/> the current path list is written to
/// <c>PluginSettings["AssemblySources"]</c> so it is persisted with the workspace JSON.
/// </para>
/// <para>
/// Whenever the assembly source list changes a <see cref="WorkspaceSaveRequestedEvent"/> is
/// published so the debounced workspace save is triggered automatically.
/// </para>
/// </summary>
public sealed class AssemblySourcePersistenceService : IHostedService, IDisposable
{
    private const string WorkspaceKey = "AssemblySources";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
    };

    private readonly AssemblySourceService _assemblySourceService;
    private readonly IEventBroker _eventBroker;

    private IDisposable? _savingSub;
    private IDisposable? _loadedSub;

    /// <summary>
    /// Initialises the service.  <paramref name="assemblySourceService"/> must be the
    /// concrete <see cref="AssemblySourceService"/> instance registered in DI.
    /// </summary>
    public AssemblySourcePersistenceService(IAssemblySourceService assemblySourceService, IEventBroker eventBroker)
    {
        _assemblySourceService = (AssemblySourceService)(assemblySourceService
            ?? throw new ArgumentNullException(nameof(assemblySourceService)));
        _eventBroker = eventBroker ?? throw new ArgumentNullException(nameof(eventBroker));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _savingSub = _eventBroker.Subscribe<WorkspaceSavingEvent>(OnWorkspaceSaving);
        _loadedSub = _eventBroker.Subscribe<WorkspaceLoadedEvent>(OnWorkspaceLoaded);
        _assemblySourceService.Changed += OnAssemblySourcesChanged;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        _assemblySourceService.Changed -= OnAssemblySourcesChanged;
        _savingSub?.Dispose();
        _loadedSub?.Dispose();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnWorkspaceSaving(WorkspaceSavingEvent e)
    {
        try
        {
            e.PluginSettings[WorkspaceKey] = new List<string>(_assemblySourceService.GetPaths());
        }
        catch { }
    }

    private void OnWorkspaceLoaded(WorkspaceLoadedEvent e)
    {
        if (!e.PluginSettings.TryGetValue(WorkspaceKey, out var raw))
            return;

        try
        {
            List<string>? paths;
            if (raw is System.Text.Json.JsonElement elem)
                paths = elem.Deserialize<List<string>>(JsonOptions);
            else
            {
                var json = JsonSerializer.Serialize(raw, JsonOptions);
                paths = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            }

            if (paths != null)
                _assemblySourceService.Reload(paths);
        }
        catch { }
    }

    private void OnAssemblySourcesChanged(object? sender, EventArgs e)
    {
        try
        {
            _eventBroker.Publish(new WorkspaceSaveRequestedEvent());
        }
        catch { }
    }
}
