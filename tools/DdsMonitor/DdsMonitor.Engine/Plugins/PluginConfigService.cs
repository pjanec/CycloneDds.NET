using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Provides the enabled-plugin set used by <see cref="PluginLoader"/> during startup.
///
/// <para>
/// On construction the service reads the persisted enabled-plugin set from the
/// <c>PluginSettings["EnabledPlugins"]</c> section of the workspace JSON file.
/// When that section is absent a one-time migration from the legacy
/// <c>enabled-plugins.json</c> sidecar file is attempted.
/// </para>
/// <para>
/// Ongoing persistence is handled externally by <see cref="PluginConfigPersistenceService"/>,
/// which subscribes to <see cref="WorkspaceSavingEvent"/> and <see cref="WorkspaceLoadedEvent"/>
/// so that the enabled-plugin set is stored together with all other user settings in the
/// single <c>workspace.json</c> file.
/// </para>
/// </summary>
public sealed class PluginConfigService
{
    private const string WorkspaceKey = "EnabledPlugins";
    private const string WorkspaceFileName = "workspace.json";
    private const string LegacyFileName = "enabled-plugins.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILogger<PluginConfigService>? _logger;
    private IEventBroker? _eventBroker;
    private IDisposable? _savingSub;
    private IDisposable? _loadedSub;

    /// <summary>
    /// Whether the workspace file (or legacy file) contained an enabled-plugin list at
    /// initialisation time.
    /// When <see langword="false"/>, <see cref="PluginLoader"/> enables all discovered plugins
    /// (first-run / upgrade semantics).
    /// </summary>
    public bool HadConfigFileAtInitialization { get; private set; }

    /// <summary>Gets the set of plugin names that are currently enabled.</summary>
    public HashSet<string> EnabledPlugins { get; private set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Initialises the service using the default per-user workspace location and loads
    /// the saved enabled-plugin set.
    /// </summary>
    public PluginConfigService(ILoggerFactory? loggerFactory = null, AppSettings? appSettings = null)
    {
        _logger = loggerFactory?.CreateLogger<PluginConfigService>();
        var workspaceFilePath = ComputeWorkspaceFilePath(appSettings);
        Initialize(workspaceFilePath);
    }

    /// <summary>
    /// Initialises the service with an explicit workspace JSON file path (test seam).
    /// </summary>
    internal PluginConfigService(string workspaceFilePath, ILogger<PluginConfigService>? logger = null)
    {
        _logger = logger;
        if (workspaceFilePath == null) throw new ArgumentNullException(nameof(workspaceFilePath));
        Initialize(workspaceFilePath);
    }

    /// <summary>
    /// Wires up <see cref="WorkspaceSavingEvent"/> / <see cref="WorkspaceLoadedEvent"/> subscriptions.
    /// Called by <see cref="PluginConfigPersistenceService"/> after the DI container is built.
    /// </summary>
    internal void SetupPersistence(IEventBroker eventBroker)
    {
        _eventBroker = eventBroker ?? throw new ArgumentNullException(nameof(eventBroker));
        _savingSub = eventBroker.Subscribe<WorkspaceSavingEvent>(OnWorkspaceSaving);
        _loadedSub = eventBroker.Subscribe<WorkspaceLoadedEvent>(OnWorkspaceLoaded);
    }

    /// <summary>
    /// Updates <see cref="EnabledPlugins"/> in-memory and requests a workspace save so the
    /// new state is persisted to <c>workspace.json</c> via the debounce mechanism.
    /// </summary>
    public void Save(HashSet<string> enabledPlugins)
    {
        ArgumentNullException.ThrowIfNull(enabledPlugins);
        EnabledPlugins = enabledPlugins;
        HadConfigFileAtInitialization = true;
        _eventBroker?.Publish(new WorkspaceSaveRequestedEvent());
    }

    // -- Event handlers --------------------------------------------------------

    private void OnWorkspaceSaving(WorkspaceSavingEvent e)
    {
        // Only persist the enabled-plugin set when it has been explicitly managed
        // (e.g. via the Plugin Manager UI).  On first-run / upgrade the flag is false
        // and EnabledPlugins is empty, meaning "all discovered plugins are enabled by
        // default".  Writing an empty list here would disable every plugin on the next
        // startup, so we skip the key entirely and let the auto-enable logic re-apply.
        if (!HadConfigFileAtInitialization)
            return;

        try
        {
            e.PluginSettings[WorkspaceKey] = new HashSet<string>(EnabledPlugins, StringComparer.Ordinal);
        }
        catch { }
    }

    private void OnWorkspaceLoaded(WorkspaceLoadedEvent e)
    {
        if (!e.PluginSettings.TryGetValue(WorkspaceKey, out var raw))
            return;

        var set = ParseEnabledPlugins(raw);
        if (set != null && set.Count > 0)
        {
            EnabledPlugins = set;
            HadConfigFileAtInitialization = true;
        }
        // An empty list is treated as "first-run / all enabled" — do not mark
        // HadConfigFileAtInitialization=true so the auto-enable logic stays active.
    }

    // -- Startup helpers -------------------------------------------------------

    private void Initialize(string workspaceFilePath)
    {
        var (set, had) = ReadFromWorkspace(workspaceFilePath);
        if (had)
        {
            HadConfigFileAtInitialization = true;
            EnabledPlugins = set;
            return;
        }

        // Fall back to legacy file.
        var legacyPath = Path.Combine(
            Path.GetDirectoryName(workspaceFilePath) ?? string.Empty,
            LegacyFileName);

        var (legacySet, legacyHad) = ReadFromLegacy(legacyPath);
        HadConfigFileAtInitialization = legacyHad;
        EnabledPlugins = legacySet;
    }

    private (HashSet<string> Set, bool Had) ReadFromWorkspace(string workspaceFilePath)
    {
        try
        {
            if (!File.Exists(workspaceFilePath))
                return (new HashSet<string>(StringComparer.Ordinal), false);

            using var doc = JsonDocument.Parse(File.ReadAllText(workspaceFilePath));
            if (!doc.RootElement.TryGetProperty("PluginSettings", out var ps))
                return (new HashSet<string>(StringComparer.Ordinal), false);
            if (!ps.TryGetProperty(WorkspaceKey, out var epEl))
                return (new HashSet<string>(StringComparer.Ordinal), false);

            var set = epEl.Deserialize<HashSet<string>>(JsonOptions)
                      ?? new HashSet<string>(StringComparer.Ordinal);

            // An empty list is treated as "first-run / all enabled" rather than
            // "user explicitly disabled every plugin".  The old code incorrectly wrote
            // an empty list on the first workspace save; we must not honour that as an
            // intentional "disable all" action.  Any non-trivial explicit configuration
            // produced by the Plugin Manager UI will contain at least one name.
            if (set.Count == 0)
                return (new HashSet<string>(StringComparer.Ordinal), false);

            return (new HashSet<string>(set, StringComparer.Ordinal), true);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "PluginConfigService: workspace file '{Path}' could not be parsed; treating as missing -- all plugins will be enabled.",
                workspaceFilePath);
            return (new HashSet<string>(StringComparer.Ordinal), false);
        }
    }

    private (HashSet<string> Set, bool Had) ReadFromLegacy(string legacyFilePath)
    {
        try
        {
            if (!File.Exists(legacyFilePath))
                return (new HashSet<string>(StringComparer.Ordinal), false);

            var json = File.ReadAllText(legacyFilePath);
            var set = JsonSerializer.Deserialize<HashSet<string>>(json, JsonOptions)
                      ?? new HashSet<string>(StringComparer.Ordinal);
            return (new HashSet<string>(set, StringComparer.Ordinal), true);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "PluginConfigService: '{Path}' could not be parsed; treating as missing -- all plugins will be enabled.",
                legacyFilePath);
            return (new HashSet<string>(StringComparer.Ordinal), false);
        }
    }

    private static HashSet<string>? ParseEnabledPlugins(object raw)
    {
        try
        {
            HashSet<string>? result;
            if (raw is JsonElement elem)
                result = elem.Deserialize<HashSet<string>>(JsonOptions);
            else
            {
                var json = JsonSerializer.Serialize(raw, JsonOptions);
                result = JsonSerializer.Deserialize<HashSet<string>>(json, JsonOptions);
            }

            return result == null ? null : new HashSet<string>(result, StringComparer.Ordinal);
        }
        catch { return null; }
    }

    private static string ComputeWorkspaceFilePath(AppSettings? appSettings)
    {
        if (!string.IsNullOrWhiteSpace(appSettings?.WorkspaceFile))
            return appSettings.WorkspaceFile;

        string workspaceDir;
        if (!string.IsNullOrWhiteSpace(appSettings?.ConfigFolder))
            workspaceDir = appSettings.ConfigFolder;
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            workspaceDir = Path.Combine(appData, "DdsMonitor");
        }

        Directory.CreateDirectory(workspaceDir);
        return Path.Combine(workspaceDir, WorkspaceFileName);
    }
}