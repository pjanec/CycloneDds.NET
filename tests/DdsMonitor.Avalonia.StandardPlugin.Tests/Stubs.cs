using System.Collections.ObjectModel;
using Avalonia.Controls;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;
using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using DdsMonitor.Engine.AssemblyScanner;
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Avalonia.StandardPlugin.Tests;

// ── Stubs ─────────────────────────────────────────────────────────────────────

internal sealed class StubMenuRegistry : IMenuRegistry
{
    private readonly List<(string Path, string Label, Action? Action)> _items = new();
    public event Action? Changed;

    public void AddMenuItem(string menuPath, string label, Action onClick)
        => _items.Add((menuPath, label, onClick));

    public void AddMenuItem(string menuPath, string label, Func<Task> onClickAsync)
        => _items.Add((menuPath, label, null));

    public IReadOnlyList<MenuNode> GetTopLevelMenus() => Array.Empty<MenuNode>();

    public IReadOnlyList<(string Path, string Label, Action? Action)> Items => _items;

    public void InvokeItem(string label)
    {
        var item = _items.FirstOrDefault(i => i.Label == label);
        item.Action?.Invoke();
    }
}

internal sealed class StubToolbarRegistry : IToolbarRegistry
{
    private readonly List<ToolbarEntry> _entries = new();
    public event Action? Changed;

    public IReadOnlyList<ToolbarEntry> Entries => _entries;

    public void Register(string id, Action onClick, string? iconKey = null, string tooltip = "")
    {
        _entries.Add(new ToolbarEntry(id, onClick, iconKey, tooltip));
        Changed?.Invoke();
    }
}

internal sealed class StubWindowManager : IWindowManager
{
    public List<(string TypeName, Dictionary<string, object>? State)> SpawnCalls { get; } = new();

    public event Action<PanelState>? PanelClosed;
    public event Action? PanelsChanged;
    public IReadOnlyList<PanelState> ActivePanels => Array.Empty<PanelState>();
    public IReadOnlyList<string> ExcludedTopics => Array.Empty<string>();

    public PanelState SpawnPanel(string componentTypeName, Dictionary<string, object>? initialState = null)
    {
        SpawnCalls.Add((componentTypeName, initialState));
        return new PanelState { PanelId = componentTypeName, ComponentTypeName = componentTypeName };
    }

    public void ClosePanel(string panelId) { }
    public void BringToFront(string panelId) { }
    public void ShowPanel(string panelId) { }
    public void ClearPanels() { }
    public void SetExcludedTopics(IEnumerable<string> topicTypeNames) { }
    public void RegisterPanelType(string typeName, Type viewModelType) { _registeredPanelTypes[typeName] = viewModelType; }
    private readonly Dictionary<string, Type> _registeredPanelTypes = new();
    public IReadOnlyDictionary<string, Type> RegisteredPanelTypes => _registeredPanelTypes;
    public void SaveWorkspace(string filePath) { }
    public string SaveWorkspaceToJson() => "[]";
    public void LoadWorkspace(string filePath) { }
    public void LoadWorkspaceFromJson(string json) { }
}

internal sealed class StubAvaloniaViewRegistry : IAvaloniaViewRegistry
{
    public void Register<TViewModel>(Func<TViewModel, Control> viewFactory) { }
    public Control BuildView(object viewModel) => throw new NotSupportedException();
}

internal sealed class StubMonitorContext : IMonitorContext
{
    private readonly Dictionary<Type, object> _features = new();

    public void Register<T>(T feature) where T : class
        => _features[typeof(T)] = feature;

    public TFeature? GetFeature<TFeature>() where TFeature : class
    {
        _features.TryGetValue(typeof(TFeature), out var f);
        return f as TFeature;
    }
}

internal sealed class StubAssemblySourceService : IAssemblySourceService
{
    private readonly List<AssemblySourceEntry> _entries = new();
    public event EventHandler? Changed;

    public bool IsCliOverride { get; set; }
    public IReadOnlyList<AssemblySourceEntry> Entries => _entries;

    public void Add(string dllPath)
    {
        _entries.Add(new AssemblySourceEntry { Path = dllPath });
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(int index)
    {
        _entries.RemoveAt(index);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MoveUp(int index) { }
    public void MoveDown(int index) { }
    public IReadOnlyList<TopicMetadata> GetTopicsForEntry(int entryIndex) => Array.Empty<TopicMetadata>();

    public void FireChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

internal sealed class StubTopicRegistry : ITopicRegistry
{
    private readonly List<TopicMetadata> _topics = new();
    public event Action? Changed;

    public IReadOnlyList<TopicMetadata> AllTopics => _topics;

    public TopicMetadata? GetByType(Type topicType) => _topics.FirstOrDefault(t => t.TopicType == topicType);
    public TopicMetadata? GetByName(string topicName) => _topics.FirstOrDefault(t => t.TopicName == topicName);

    public void Register(TopicMetadata meta)
    {
        _topics.Add(meta);
        Changed?.Invoke();
    }

    public void FireChanged()
    {
        Changed?.Invoke();
    }
}

internal sealed class StubContextMenuRegistry : IContextMenuRegistry
{
    private readonly ContextMenuRegistry _inner = new();
    public List<string> RegisteredProviderTypes { get; } = new();

    public void RegisterProvider<TContext>(Func<TContext, IEnumerable<ContextMenuItem>> provider)
    {
        RegisteredProviderTypes.Add(typeof(TContext).Name);
        _inner.RegisterProvider(provider);
    }

    public IEnumerable<ContextMenuItem> GetItems<TContext>(TContext context)
        => _inner.GetItems(context);
}

internal sealed class StubEventBroker : IEventBroker
{
    public void Publish<TEvent>(TEvent eventMessage) { }
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) => new NoopDisposable();
    private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
}

/// <summary>
/// Tracks active subscription count so tests can verify disposal.
/// </summary>
internal sealed class TrackingEventBroker : IEventBroker
{
    private readonly EventBroker _inner = new();
    private int _activeCount;

    public int ActiveSubscriptionCount => _activeCount;

    public void Publish<TEvent>(TEvent eventMessage)
        => _inner.Publish(eventMessage);

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
    {
        Interlocked.Increment(ref _activeCount);
        var token = _inner.Subscribe(handler);
        return new TrackedToken(token, this);
    }

    internal void Decrement() => Interlocked.Decrement(ref _activeCount);

    private sealed class TrackedToken : IDisposable
    {
        private readonly IDisposable _inner;
        private readonly TrackingEventBroker _broker;
        private bool _disposed;

        public TrackedToken(IDisposable inner, TrackingEventBroker broker)
        {
            _inner = inner;
            _broker = broker;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _inner.Dispose();
            _broker.Decrement();
        }
    }
}

internal sealed class StubUserSettings : IUserSettings
{
    private readonly Dictionary<string, Dictionary<string, object>> _data = new();

    public T Get<T>(string section, string key, T defaultValue)
    {
        if (_data.TryGetValue(section, out var sec) && sec.TryGetValue(key, out var val) && val is T typed)
            return typed;
        return defaultValue;
    }

    public void Set<T>(string section, string key, T value)
    {
        if (!_data.TryGetValue(section, out var sec))
            _data[section] = sec = new();
        sec[key] = value!;
    }

    public Task SaveAsync() => Task.CompletedTask;
}

internal sealed class StubDynamicWriter : IDynamicWriter
{
    public Type TopicType => typeof(HeartbeatSample);
    public int WriteCount { get; private set; }

    public void Write(object payload) => WriteCount++;
    public void DisposeInstance(object payload) { }
    public void Dispose() { }
}

internal sealed class StubDdsBridge : IDdsBridge
{
    public StubDynamicWriter Writer { get; } = new();

    public bool IsPaused { get; set; }
    public CycloneDDS.Runtime.DdsParticipant Participant => throw new NotSupportedException();
    public IReadOnlyList<CycloneDDS.Runtime.DdsParticipant> Participants => Array.Empty<CycloneDDS.Runtime.DdsParticipant>();
    public IReadOnlyList<ParticipantConfig> ParticipantConfigs => Array.Empty<ParticipantConfig>();
    public string? CurrentPartition => null;
    public IReadOnlySet<Type> ExplicitlyUnsubscribedTopicTypes => new HashSet<Type>();
    public IReadOnlyDictionary<Type, IDynamicReader> ActiveReaders => new Dictionary<Type, IDynamicReader>();
    public event Action? ReadersChanged { add { } remove { } }

    public IDynamicReader Subscribe(TopicMetadata meta) => throw new NotSupportedException();
    public bool TrySubscribe(TopicMetadata meta, out IDynamicReader? reader, out string? error)
    { reader = null; error = "stub"; return false; }
    public void Unsubscribe(TopicMetadata meta) { }
    public IDynamicWriter GetWriter(TopicMetadata meta) => Writer;
    public void ChangePartition(string? newPartition) { }
    public void InitializeExplicitlyUnsubscribed(IEnumerable<Type> types) { }
    public void AddParticipant(uint domainId, string partitionName) { }
    public void RemoveParticipant(int participantIndex) { }
    public void SetFilter(Func<SampleData, bool>? predicate) { }
    public void Dispose() { }

    public void ResetAll() { }
}

// ── DT-003: Hidden sample type for IsHidden filter coverage ────────────────────

[DdsTopic]
internal struct _HiddenSample
{
    [DdsKey] public int Id;
    public int Value;
}

// ── StubSampleView ────────────────────────────────────────────────────────────

internal sealed class StubSampleView : ISampleView
{
    public int CurrentFilteredCount { get; set; } = 0;
    public bool Disposed { get; private set; }
    public bool SetFilterCalled { get; private set; }
    public Func<SampleData, bool>? LastFilter { get; private set; }
    public event Action? OnViewRebuilt;

    public void TriggerViewRebuilt() => OnViewRebuilt?.Invoke();

    public void SetFilter(Func<SampleData, bool>? predicate)
    {
        SetFilterCalled = true;
        LastFilter = predicate;
    }

    public void SetSortSpec(FieldMetadata? field, SortDirection direction) { }

    public ReadOnlyMemory<SampleData> GetVirtualView(int startIndex, int count)
        => ReadOnlyMemory<SampleData>.Empty;

    public SampleData[] GetFilteredSnapshot() => Array.Empty<SampleData>();

    public void Dispose() => Disposed = true;
}

// ── StubFilterCompiler ────────────────────────────────────────────────────────

internal sealed class StubFilterCompiler : IFilterCompiler
{
    public bool NextResultIsValid { get; set; } = true;
    public string? NextErrorMessage { get; set; }
    public FilterResult? LastResult { get; private set; }

    public FilterResult Compile(string expression, TopicMetadata? topicMeta)
    {
        var result = NextResultIsValid
            ? new FilterResult(true, _ => true, null)
            : new FilterResult(false, null, NextErrorMessage ?? "Compile error");
        LastResult = result;
        return result;
    }

    public FilterResult Compile(string expression, TopicMetadata? topicMeta, IReadOnlyList<object?>? paramValues)
        => Compile(expression, topicMeta);
}
