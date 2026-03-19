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
/// </summary>
public sealed class AssemblySourceService : IAssemblySourceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string ConfigFileName = "assembly-sources.json";

    private readonly string _configFilePath;
    private readonly ITopicRegistry _topicRegistry;
    private readonly TopicDiscoveryService _discoveryService;
    private readonly object _sync = new();
    private readonly List<AssemblySourceEntry> _entries = new();

    // Maps entry index → list of topic types owned by that entry.
    private readonly List<List<TopicMetadata>> _entryTopics = new();

    /// <inheritdoc />
    public event EventHandler? Changed;

    public AssemblySourceService(ITopicRegistry topicRegistry, TopicDiscoveryService discoveryService)
    {
        _topicRegistry = topicRegistry ?? throw new ArgumentNullException(nameof(topicRegistry));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "DdsMonitor");
        Directory.CreateDirectory(dir);
        _configFilePath = Path.Combine(dir, ConfigFileName);

        LoadAndScanAll();
    }

    /// <summary>
    /// Internal constructor that accepts an explicit config file path for testing.
    /// </summary>
    internal AssemblySourceService(ITopicRegistry topicRegistry, TopicDiscoveryService discoveryService, string configFilePath)
    {
        _topicRegistry = topicRegistry ?? throw new ArgumentNullException(nameof(topicRegistry));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _configFilePath = configFilePath ?? throw new ArgumentNullException(nameof(configFilePath));

        LoadAndScanAll();
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

        Persist();
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

        Persist();
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

        Persist();
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

        Persist();
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

    // ── Private helpers ─────────────────────────────────────────────────────

    private void LoadAndScanAll()
    {
        List<string> paths;
        try
        {
            if (!File.Exists(_configFilePath))
            {
                return;
            }

            var json = File.ReadAllText(_configFilePath);
            paths = JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
        }
        catch
        {
            return;
        }

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

    /// <summary>
    /// Scans a single entry via the discovery service, populates its <see cref="AssemblySourceEntry.TopicCount"/>
    /// and <see cref="AssemblySourceEntry.LoadError"/>, and returns the list of topic types found in the assembly.
    /// For topics that were already registered (e.g. via SelfSendService), the existing registry entry is
    /// returned so the detail panel always shows what topics the DLL contains.
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

    private void Persist()
    {
        try
        {
            List<string> paths;
            lock (_sync)
            {
                paths = new List<string>(_entries.Count);
                foreach (var e in _entries)
                {
                    paths.Add(e.Path);
                }
            }

            var json = JsonSerializer.Serialize(paths, JsonOptions);
            File.WriteAllText(_configFilePath, json);
        }
        catch
        {
            // Ignore persistence failures.
        }
    }

    private static void Swap<T>(List<T> list, int a, int b)
    {
        (list[a], list[b]) = (list[b], list[a]);
    }
}
