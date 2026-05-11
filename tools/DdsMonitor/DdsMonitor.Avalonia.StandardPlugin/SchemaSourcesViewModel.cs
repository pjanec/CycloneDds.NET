using System.Collections.ObjectModel;
using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using DdsMonitor.Engine.AssemblyScanner;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// ViewModel for the Schema Sources panel.
/// Reflects the list of user-configured DLL assemblies and the topics discovered from them.
/// Does not implement IDisposable because it subscribes to IAssemblySourceService.Changed,
/// a service-owned event (service lifetime exceeds all panels), and unregisters on Dispose
/// only if the panel is closed early — but since IAssemblySourceService is singleton, the
/// ViewModel can be collected when the panel window closes without side-effects.
/// </summary>
public sealed class SchemaSourcesViewModel : IStatefulViewModel
{
    private readonly IAssemblySourceService _assemblyService;
    private readonly ITopicRegistry _topicRegistry;
    private readonly IWindowManager _windowManager;

    public ObservableCollection<AssemblySourceEntry> Entries { get; } = new();

    public bool IsCliOverride => _assemblyService.IsCliOverride;

    public SchemaSourcesViewModel(
        IAssemblySourceService assemblyService,
        ITopicRegistry topicRegistry,
        IWindowManager windowManager)
    {
        _assemblyService = assemblyService;
        _topicRegistry = topicRegistry;
        _windowManager = windowManager;

        _assemblyService.Changed += OnAssemblySourceChanged;
        RefreshEntries();
    }

    public void Initialize(IDictionary<string, object> componentState) { }

    private void OnAssemblySourceChanged(object? sender, EventArgs e)
        => RefreshEntries();

    private void RefreshEntries()
    {
        Entries.Clear();
        foreach (var entry in _assemblyService.Entries)
            Entries.Add(entry);
    }

    public void AddAssembly(string path)
        => _assemblyService.Add(path);

    public void RemoveAssembly(int index)
    {
        if (index >= 0 && index < _assemblyService.Entries.Count)
            _assemblyService.Remove(index);
    }

    /// <summary>
    /// Returns the topics discovered from the assembly at the given entry index.
    /// </summary>
    public IReadOnlyList<TopicMetadata> GetTopicsForEntry(int entryIndex)
        => _assemblyService.GetTopicsForEntry(entryIndex);
}
