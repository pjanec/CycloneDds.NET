using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DdsMonitor.Engine.AssemblyScanner;

/// <summary>
/// Manages the persistent list of user-configured external DLL assemblies and
/// scans them for DDS topic types on startup and whenever a new path is added.
///
/// Persistence is handled externally via <see cref="AssemblySourcePersistenceService"/>,
/// which hooks into the workspace save/load events so all settings are stored in the
/// single <c>workspace.json</c> file.  On first run a one-time migration from the
/// legacy <c>assembly-sources.json</c> sidecar file is performed automatically.
/// </summary>
public sealed class AssemblySourceService : IAssemblySourceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string LegacyConfigFileName = "assembly-sources.json";
    private const string WorkspaceFileName = "workspace.json";
    private const string WorkspaceKey = "AssemblySources";

    private readonly ITopicRegistry _topicRegistry;
    private readonly TopicDiscoveryService _discoveryService;
    private readonly object _sync = new();
    private readonly List<AssemblySourceEntry> _entries = new();
    private readonly string[]? _cliPaths; // non-null ⇒ CLI override mode

    // Maps entry index → list of topic types owned by that entry.
    private readonly List<List<TopicMetadata>> _entryTopics = new();

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public bool IsCliOverride => _cliPaths != null;

    public AssemblySourceService(ITopicRegistry topicRegistry, TopicDiscoveryService discoveryService)
        : this(topicRegistry, discoveryService, appSettings: null) { }

    /// <summary>
    /// Constructs the service.  When <paramref name="appSettings"/> contains a non-empty
    /// <see cref="AppSettings.TopicSources"/> list, the service operates in CLI-override mode:
    /// those paths are scanned instead of persisted paths, and any changes made
    /// during the session are never written back.
    /// </summary>
    public AssemblySourceService(ITopicRegistry topicRegistry, TopicDiscoveryService discoveryService, AppSettings? appSettings)
    {
        _topicRegistry = topicRegistry ?? throw new ArgumentNullException(nameof(topicRegistry));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));

        if (appSettings?.TopicSources is { Length: > 0 } overridePaths)
        {
            _cliPaths = overridePaths;
            LoadFromPaths(_cliPaths);
        }
        else
        {
            // Determine workspace file path using the same logic as WorkspaceState.
            var workspaceFilePath = ComputeWorkspaceFilePath(appSettings);
            var initialPaths = ReadPathsFromWorkspace(workspaceFilePath)
                               ?? ReadPathsFromLegacy(workspaceFilePath)
                               ?? Array.Empty<string>();
            LoadFromPaths(initialPaths);
        }
    }

    /// <summary>
    /// Internal constructor that accepts an explicit legacy config file path for testing.
    /// CLI override is not active when using this constructor.
    /// </summary>
    internal AssemblySourceService(ITopicRegistry topicRegistry, TopicDiscoveryService discoveryService, string configFilePath)
    {
        _topicRegistry = topicRegistry ?? throw new ArgumentNullException(nameof(topicRegistry));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        if (configFilePath == null) throw new ArgumentNullException(nameof(configFilePath));

        // For the test constructor: read from the provided file as a plain JSON path list.
        var paths = ReadPathsFromPlainFile(configFilePath) ?? Array.Empty<string>();
        LoadFromPaths(paths);
    }

    /// <inheritdoc />
    public IReadOnlyList<AssemblySourceEntry> Entries
    {
        get
        {
            lock (_sync)
            {
                return _entries.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public void Add(string dllPath)
    {
        if (string.IsNullOrWhiteSpace(dllPath))
        {
            return;
        }

        dllPath = Path.GetFullPath(dllPath);

        lock (_sync)
        {
            foreach (var e in _entries)
            {
                if (string.Equals(e.Path, dllPath, StringComparison.OrdinalIgnoreCase))
                {
                    return; // Already present.
                }
            }

            var entry = new AssemblySourceEntry { Path = dllPath };
            var topics = ScanEntry(entry);
            _entries.Add(entry);
            _entryTopics.Add(topics);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Remove(int index)
    {
        lock (_sync)
        {
            if (index < 0 || index >= _entries.Count)
            {
                return;
            }

            _entries.RemoveAt(index);
            _entryTopics.RemoveAt(index);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void MoveUp(int index)
    {
        lock (_sync)
        {
            if (index <= 0 || index >= _entries.Count)
            {
                return;
            }

            Swap(_entries, index, index - 1);
            Swap(_entryTopics, index, index - 1);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void MoveDown(int index)
    {
        lock (_sync)
        {
            if (index < 0 || index >= _entries.Count - 1)
            {
                return;
            }

            Swap(_entries, index, index + 1);
            Swap(_entryTopics, index, index + 1);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public IReadOnlyList<TopicMetadata> GetTopicsForEntry(int entryIndex)
    {
        lock (_sync)
        {
            if (entryIndex < 0 || entryIndex >= _entryTopics.Count)
            {
                return Array.Empty<TopicMetadata>();
            }

            return _entryTopics[entryIndex].ToArray();
        }
    }

    /// <summary>
    /// Replaces the current entry list with the supplied paths and re-scans.
    /// Called by <see cref="AssemblySourcePersistenceService"/> when a workspace is loaded.
    /// </summary>
    internal void Reload(IEnumerable<string> paths)
    {
        lock (_sync)
        {
            _entries.Clear();
            _entryTopics.Clear();
        }
        LoadFromPaths(paths);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Returns the current list of paths for serialisation into the workspace file.
    /// </summary>
    internal IReadOnlyList<string> GetPaths()
    {
        lock (_sync)
        {
            return _entries.Select(e => e.Path).ToArray();
        }
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private void LoadFromPaths(IEnumerable<string> paths)
    {
        lock (_sync)
        {
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var entry = new AssemblySourceEntry { Path = path };
                var topics = ScanEntry(entry);
                _entries.Add(entry);
                _entryTopics.Add(topics);
            }
        }
    }

    private static string[] ReadPathsFromWorkspace(string workspaceFilePath)
    {
        try
        {
            if (!File.Exists(workspaceFilePath)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(workspaceFilePath));
            if (!doc.RootElement.TryGetProperty("PluginSettings", out var ps)) return null;
            if (!ps.TryGetProperty(WorkspaceKey, out var srcEl)) return null;
            var list = srcEl.Deserialize<List<string>>(JsonOptions);
            return list is { Count: > 0 } ? list.ToArray() : null;
        }
        catch { return null; }
    }

    private string[] ReadPathsFromLegacy(string workspaceFilePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(workspaceFilePath) ?? string.Empty;
            var legacyPath = Path.Combine(dir, LegacyConfigFileName);
            return ReadPathsFromPlainFile(legacyPath);
        }
        catch { return null; }
    }

    private static string[] ReadPathsFromPlainFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            var json = File.ReadAllText(filePath);
            var list = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            return list is { Count: > 0 } ? list.ToArray() : null;
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

    /// <summary>
    /// Scans a single entry via the discovery service.
    /// Must be called while holding <see cref="_sync"/>.
    /// </summary>
    private List<TopicMetadata> ScanEntry(AssemblySourceEntry entry)
    {
        try
        {
            List<TopicMetadata> found;

            if (Directory.Exists(entry.Path))
            {
                found = new List<TopicMetadata>();
                var files = Directory.EnumerateFiles(entry.Path, "*.dll")
                    .Concat(Directory.EnumerateFiles(entry.Path, "*.exe"));
                foreach (var file in files)
                {
                    try
                    {
                        found.AddRange(_discoveryService.DiscoverFromFileDetailed(file));
                    }
                    catch
                    {
                        // Skip non-loadable files silently.
                    }
                }
            }
            else
            {
                found = new List<TopicMetadata>(_discoveryService.DiscoverFromFileDetailed(entry.Path));
            }

            entry.TopicCount = found.Count;
            entry.LoadError = null;
            return found;
        }
        catch (Exception ex)
        {
            entry.TopicCount = 0;
            entry.LoadError = ex.Message;
            return new List<TopicMetadata>();
        }
    }

    private static void Swap<T>(List<T> list, int a, int b)
    {
        (list[a], list[b]) = (list[b], list[a]);
    }
}


/// <summary>
/// Manages the persistent list of user-configured external DLL assemblies and
/// scans them for DDS topic types on startup and whenever a new path is added.
/// </summary>