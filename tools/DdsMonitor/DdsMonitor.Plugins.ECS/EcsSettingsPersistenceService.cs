using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DdsMonitor.Engine;
using Microsoft.Extensions.Hosting;

namespace DdsMonitor.Plugins.ECS;

/// <summary>
/// Singleton hosted service that persists <see cref="EcsSettings"/> by hooking the
/// workspace save/load events published by <see cref="IEventBroker"/>.
///
/// <para>
/// On <see cref="WorkspaceLoadedEvent"/> the ECS section is read from
/// <c>PluginSettings["ECS"]</c>.  If the section is absent a one-time migration is
/// attempted from the legacy <c>ecs-settings.json</c> file; that file is deleted after
/// a successful migration.
/// </para>
/// <para>
/// On <see cref="WorkspaceSavingEvent"/> the current settings are written to
/// <c>PluginSettings["ECS"]</c> so they are persisted with the workspace JSON.
/// </para>
/// </summary>
public sealed class EcsSettingsPersistenceService : IHostedService, IDisposable
{
    private const string WorkspaceKey = "ECS";
    private const string LegacyFileName = "ecs-settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
    };

    private readonly EcsSettings _settings;
    private readonly IEventBroker _eventBroker;
    private readonly string _legacyFilePath;

    private IDisposable? _savingSub;
    private IDisposable? _loadedSub;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    public EcsSettingsPersistenceService(EcsSettings settings, IEventBroker eventBroker)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _eventBroker = eventBroker ?? throw new ArgumentNullException(nameof(eventBroker));

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "DdsMonitor");
        Directory.CreateDirectory(folder);
        _legacyFilePath = Path.Combine(folder, LegacyFileName);
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
            var dto = new EcsSettingsDto
            {
                NamespacePrefix    = _settings.NamespacePrefix,
                EntityIdPattern    = _settings.EntityIdPattern,
                PartIdPattern      = _settings.PartIdPattern,
                MasterTopicPattern = _settings.MasterTopicPattern,
            };
            e.PluginSettings[WorkspaceKey] = dto;
        }
        catch
        {
            // Ignore persistence errors – they must not crash the save path.
        }
    }

    private void OnWorkspaceLoaded(WorkspaceLoadedEvent e)
    {
        if (e.PluginSettings.TryGetValue(WorkspaceKey, out var raw))
        {
            ApplyFromObject(raw);
        }
        else
        {
            TryMigrateFromLegacy();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ApplyFromObject(object raw)
    {
        try
        {
            EcsSettingsDto? dto;
            if (raw is JsonElement elem)
            {
                dto = elem.Deserialize<EcsSettingsDto>(JsonOptions);
            }
            else
            {
                var json = JsonSerializer.Serialize(raw, JsonOptions);
                dto = JsonSerializer.Deserialize<EcsSettingsDto>(json, JsonOptions);
            }

            if (dto is null) return;
            ApplyDto(dto);
        }
        catch
        {
            // Ignore deserialisation errors.
        }
    }

    private void TryMigrateFromLegacy()
    {
        if (!File.Exists(_legacyFilePath))
            return;

        try
        {
            var json = File.ReadAllText(_legacyFilePath);
            var dto  = JsonSerializer.Deserialize<EcsSettingsDto>(json, JsonOptions);
            if (dto is not null)
            {
                ApplyDto(dto);
                // Delete legacy file after successful migration.
                File.Delete(_legacyFilePath);
            }
        }
        catch
        {
            // Ignore migration errors – the plugin continues with default settings.
        }
    }

    private void ApplyDto(EcsSettingsDto dto)
    {
        if (dto.NamespacePrefix    is not null) _settings.NamespacePrefix    = dto.NamespacePrefix;
        if (dto.EntityIdPattern    is not null) _settings.EntityIdPattern    = dto.EntityIdPattern;
        if (dto.PartIdPattern      is not null) _settings.PartIdPattern      = dto.PartIdPattern;
        if (dto.MasterTopicPattern is not null) _settings.MasterTopicPattern = dto.MasterTopicPattern;
    }

    // ── DTO ───────────────────────────────────────────────────────────────────

    private sealed class EcsSettingsDto
    {
        public string? NamespacePrefix    { get; set; }
        public string? EntityIdPattern    { get; set; }
        public string? PartIdPattern      { get; set; }
        public string? MasterTopicPattern { get; set; }
    }
}
