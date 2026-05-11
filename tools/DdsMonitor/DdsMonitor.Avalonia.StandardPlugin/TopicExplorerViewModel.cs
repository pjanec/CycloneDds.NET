using System.Collections.ObjectModel;
using Avalonia.Threading;
using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Plugins;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// ViewModel for the Topic Explorer panel.
/// Shows all registered DDS topics and supports filtering hidden topics.
/// Implements <see cref="IDisposable"/> to release IEventBroker subscriptions when the panel closes.
/// </summary>
public sealed class TopicExplorerViewModel : IStatefulViewModel, IDisposable
{
    private readonly ITopicRegistry _topicRegistry;
    private readonly IContextMenuRegistry _contextMenuRegistry;
    private readonly IEventBroker _eventBroker;
    private readonly IUserSettings _userSettings;

    private readonly List<IDisposable> _brokerSubscriptions = new();
    private bool _showHidden;

    public ObservableCollection<TopicMetadata> Topics { get; } = new();

    public bool ShowHidden
    {
        get => _showHidden;
        set
        {
            if (_showHidden == value) return;
            _showHidden = value;
            _userSettings.Set("TopicExplorer", "ShowHidden", value);
            _ = _userSettings.SaveAsync();
            RefreshTopics();
        }
    }

    public TopicExplorerViewModel(
        ITopicRegistry topicRegistry,
        IContextMenuRegistry contextMenuRegistry,
        IEventBroker eventBroker,
        IUserSettings userSettings)
    {
        _topicRegistry = topicRegistry;
        _contextMenuRegistry = contextMenuRegistry;
        _eventBroker = eventBroker;
        _userSettings = userSettings;

        // Subscribe to registry changes via the standard C# event, dispatching to UI thread
        _topicRegistry.Changed += OnTopicRegistryChanged;

        // Subscribe to EventBroker events on the UI thread — tokens stored for disposal
        _brokerSubscriptions.Add(
            _eventBroker.SubscribeOnUiThread<SpawnPanelEvent>(OnSpawnPanelEvent));

        RefreshTopics();
    }

    public void Initialize(IDictionary<string, object> componentState)
    {
        _showHidden = _userSettings.Get("TopicExplorer", "ShowHidden", false);
        RefreshTopics();
    }

    private void OnTopicRegistryChanged()
    {
        if (Dispatcher.UIThread.CheckAccess())
            RefreshTopics();
        else
            Dispatcher.UIThread.Post(RefreshTopics, DispatcherPriority.Normal);
    }

    private void OnSpawnPanelEvent(SpawnPanelEvent ev)
    {
        // React to panel spawn events.
        // V1: no-op — subscription exists to demonstrate and enforce the disposal lifecycle.
    }

    private void RefreshTopics()
    {
        Topics.Clear();
        foreach (var topic in _topicRegistry.AllTopics)
        {
            if (!_showHidden && IsHidden(topic)) continue;
            Topics.Add(topic);
        }
    }

    private static bool IsHidden(TopicMetadata meta)
        => meta.ShortName.StartsWith('_') || meta.Namespace.Contains("Internal");

    public IEnumerable<ContextMenuItem> GetContextMenu(TopicMetadata meta)
        => _contextMenuRegistry.GetItems(meta);

    public void OpenSamplesViewer(TopicMetadata meta)
        => _eventBroker.Publish(new SpawnPanelEvent(
            "SamplesViewer",
            new Dictionary<string, object>(StringComparer.Ordinal) { ["TopicName"] = meta.TopicName }));

    public void Dispose()
    {
        _topicRegistry.Changed -= OnTopicRegistryChanged;

        foreach (var sub in _brokerSubscriptions)
            sub.Dispose();

        _brokerSubscriptions.Clear();
    }
}
