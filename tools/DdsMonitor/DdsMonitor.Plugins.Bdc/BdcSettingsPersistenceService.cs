using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DdsMonitor.Engine;
using Microsoft.Extensions.Hosting;

namespace DdsMonitor.Plugins.Bdc;

/// <summary>
/// Singleton hosted service that persists <see cref="BdcSettings"/> to a JSON file
/// (<c>bdc-settings.json</c>) stored alongside the main <c>workspace.json</c>.
///
/// <para>
/// On application startup (<see cref="StartAsync"/>) the saved file is loaded back and
/// applied to the in-memory <see cref="BdcSettings"/> singleton.  Whenever the user
/// modifies a setting, <see cref="BdcSettings.SettingsChanged"/> triggers a 2-second
/// debounced write so persistence is transparent and non-blocking.
/// </para>
/// </summary>
public sealed class BdcSettingsPersistenceService : IHostedService, IDisposable
{
    private const string FileName = "bdc-settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly BdcSettings _settings;
    private readonly string _filePath;
    private readonly DebouncedAction _debouncer;

    /// <summary>
    /// Initialises the service.  The settings file is stored next to
    /// <see cref="IWorkspaceState.WorkspaceFilePath"/>.
    /// </summary>
    public BdcSettingsPersistenceService(BdcSettings settings, IWorkspaceState workspaceState)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        var folder = Path.GetDirectoryName(workspaceState.WorkspaceFilePath) ?? ".";
        _filePath = Path.Combine(folder, FileName);
        _debouncer = new DebouncedAction(TimeSpan.FromSeconds(2), SaveNow);
        _settings.SettingsChanged += OnSettingsChanged;
    }

    /// <inheritdoc />
    /// <remarks>Loads previously saved settings from disk before the UI starts.</remarks>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Load();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        _settings.SettingsChanged -= OnSettingsChanged;
        _debouncer.Dispose();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void OnSettingsChanged() => _debouncer.Trigger();

    private void SaveNow()
    {
        try
        {
            var dto = new BdcSettingsDto
            {
                NamespacePrefix    = _settings.NamespacePrefix,
                EntityIdPattern    = _settings.EntityIdPattern,
                PartIdPattern      = _settings.PartIdPattern,
                MasterTopicPattern = _settings.MasterTopicPattern,
            };
            File.WriteAllText(_filePath, JsonSerializer.Serialize(dto, JsonOptions));
        }
        catch
        {
            // Ignore persistence errors – they must not crash the UI loop.
        }
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
            return;

        try
        {
            var json = File.ReadAllText(_filePath);
            var dto  = JsonSerializer.Deserialize<BdcSettingsDto>(json, JsonOptions);
            if (dto is null)
                return;

            // Apply only non-null values so defaults are preserved when a field is absent.
            if (dto.NamespacePrefix    is not null) _settings.NamespacePrefix    = dto.NamespacePrefix;
            if (dto.EntityIdPattern    is not null) _settings.EntityIdPattern    = dto.EntityIdPattern;
            if (dto.PartIdPattern      is not null) _settings.PartIdPattern      = dto.PartIdPattern;
            if (dto.MasterTopicPattern is not null) _settings.MasterTopicPattern = dto.MasterTopicPattern;
        }
        catch
        {
            // Ignore load errors – the plugin continues with default settings.
        }
    }

    // ── DTO ───────────────────────────────────────────────────────────────────

    private sealed class BdcSettingsDto
    {
        public string? NamespacePrefix    { get; set; }
        public string? EntityIdPattern    { get; set; }
        public string? PartIdPattern      { get; set; }
        public string? MasterTopicPattern { get; set; }
    }
}
