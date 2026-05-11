using System.Collections.ObjectModel;
using Avalonia.Threading;
using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// ViewModel for the Samples Viewer panel.
/// Shows a virtualized, filterable list of DDS samples from the global <see cref="ISampleStore"/>.
/// Implements <see cref="IDisposable"/> to release the background <see cref="ISampleView"/> worker.
/// </summary>
public sealed class SamplesViewerViewModel : IStatefulViewModel, IDisposable
{
    private readonly IFilterCompiler _filterCompiler;
    private readonly IEventBroker? _eventBroker;
    private readonly ISampleStore? _store;
    private ISampleView? _view;
    private TopicMetadata? _meta;
    private IDictionary<string, object>? _state;
    private string _filterText = "";
    private string? _filterError;
    private int _filteredCount;

    /// <summary>Gets the current sample rows (updated on the UI thread after each view rebuild).</summary>
    public ObservableCollection<SampleRowViewModel> SampleRows { get; } = new();

    /// <summary>Gets or sets the filter expression text. Invalid expressions set <see cref="FilterError"/>.</summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            // Always apply when clearing (empty) so SetFilter(null) is always called on clear,
            // even if the text was already empty. Skip only when setting the same non-empty value.
            if (_filterText == value && !string.IsNullOrEmpty(value)) return;
            _filterText = value;
            ApplyFilter(value);
        }
    }

    /// <summary>Gets the last filter compile error, or <c>null</c> if the filter is valid.</summary>
    public string? FilterError
    {
        get => _filterError;
        private set => _filterError = value;
    }

    /// <summary>Gets the count of samples that pass the current filter.</summary>
    public int FilteredCount
    {
        get => _filteredCount;
        private set => _filteredCount = value;
    }

    /// <summary>
    /// Creates a new <see cref="SamplesViewerViewModel"/>.
    /// </summary>
    /// <param name="filterCompiler">Compiles user filter expressions.</param>
    /// <param name="store">The global sample store; used to create a per-view <see cref="ISampleView"/>.</param>
    /// <param name="view">Pre-created view (for testing); if non-null, <paramref name="store"/> is ignored.</param>
    /// <param name="meta">Topic metadata; resolved from component state in <see cref="Initialize"/>.</param>
    public SamplesViewerViewModel(
        IFilterCompiler filterCompiler,
        ISampleStore? store = null,
        ISampleView? view = null,
        TopicMetadata? meta = null,
        IEventBroker? eventBroker = null)
    {
        _filterCompiler = filterCompiler ?? throw new ArgumentNullException(nameof(filterCompiler));
        _store = store;
        _view = view;
        _meta = meta;
        _eventBroker = eventBroker;
    }

    /// <inheritdoc />
    public void Initialize(IDictionary<string, object> componentState)
    {
        _state = componentState;

        // Restore FilterText (handle both native string and JsonElement from JSON deserialization)
        if (componentState.TryGetValue("FilterText", out var ft))
        {
            if (ft is string ftStr)
                _filterText = ftStr;
            else if (ft is System.Text.Json.JsonElement je &&
                     je.ValueKind == System.Text.Json.JsonValueKind.String)
                _filterText = je.GetString() ?? "";
        }

        if (_meta != null)
            componentState["TopicName"] = _meta.TopicName;

        if (_view != null)
        {
            // Pre-injected view (test scenario): subscribe without creating a new SampleView.
            _view.OnViewRebuilt += OnViewRebuilt;
        }
        else if (_meta != null && _store != null)
        {
            StartView(_meta);
        }

        // Apply restored filter after view is wired up
        if (!string.IsNullOrEmpty(_filterText))
            ApplyFilter(_filterText);
    }

    private void StartView(TopicMetadata meta)
    {
        _view = new SampleView(_store!);
        _view.OnViewRebuilt += OnViewRebuilt;
    }

    /// <summary>
    /// Called by the background view worker thread — must NOT touch UI state directly.
    /// Dispatches the count update to the UI thread.
    /// </summary>
    private void OnViewRebuilt()
    {
        var count = _view?.CurrentFilteredCount ?? 0;
        Dispatcher.UIThread.Post(() =>
        {
            FilteredCount = count;
            RefreshSampleRows();
        }, DispatcherPriority.Normal);
    }

    private void RefreshSampleRows()
    {
        SampleRows.Clear();
        if (_view == null) return;
        var slice = _view.GetVirtualView(0, 200);
        foreach (var s in slice.Span)
            SampleRows.Add(new SampleRowViewModel(s));
    }

    private void ApplyFilter(string expression)
    {
        if (_view == null) return;

        if (string.IsNullOrEmpty(expression))
        {
            _view.SetFilter(null);
            _filterError = null;
            return;
        }

        var result = _filterCompiler.Compile(expression, _meta);
        if (result.IsValid)
        {
            _view.SetFilter(result.Predicate);
            _filterError = null;
            if (_state != null)
            {
                _state["FilterText"] = expression;
                _eventBroker?.Publish(new WorkspaceSaveRequestedEvent());
            }
        }
        else
        {
            _filterError = result.ErrorMessage;
            // Do NOT call SetFilter on invalid expression — leave current filter in place.
        }
    }

    /// <summary>Returns a virtual slice of current filtered samples.</summary>
    public ReadOnlyMemory<SampleData> GetVirtualSlice(int start, int count)
        => _view?.GetVirtualView(start, count) ?? ReadOnlyMemory<SampleData>.Empty;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_view != null)
        {
            _view.OnViewRebuilt -= OnViewRebuilt;
            _view.Dispose();
        }
    }
}

/// <summary>Row ViewModel for a single sample shown in the <see cref="SamplesViewerView"/>.</summary>
public sealed class SampleRowViewModel
{
    public string TopicName { get; }
    public string Timestamp { get; }

    public SampleRowViewModel(SampleData sample)
    {
        TopicName = sample.TopicMetadata?.ShortName ?? "?";
        Timestamp = sample.Timestamp.ToString("O");
    }
}
