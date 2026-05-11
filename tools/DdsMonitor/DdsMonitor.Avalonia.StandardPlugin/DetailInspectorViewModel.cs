using System.Collections.ObjectModel;
using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Plugins;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// ViewModel for the Detail Inspector panel.
/// Subscribes to <see cref="SampleSelectedEvent"/> (when linked) and displays field values
/// for the currently selected sample.
/// </summary>
public sealed class DetailInspectorViewModel : IStatefulViewModel, IDisposable
{
    private readonly IEventBroker _broker;
    private IDictionary<string, object>? _state;
    private IDisposable? _sampleSubscription;
    private bool _isLinked = true;
    private string? _sourcePanelId;
    private SampleData? _currentSample;

    public ObservableCollection<FieldInspectorItemViewModel> FieldTree { get; } = new();

    public bool IsLinked
    {
        get => _isLinked;
        set
        {
            if (_isLinked == value) return;
            _isLinked = value;
            if (_state != null) _state["IsLinked"] = value;

            if (!value)
            {
                // Unlink: dispose subscription
                _sampleSubscription?.Dispose();
                _sampleSubscription = null;
            }
            else
            {
                // Re-link: re-subscribe if panel id is known
                SubscribeIfLinked();
            }
        }
    }

    public string? SourcePanelId
    {
        get => _sourcePanelId;
        set
        {
            if (_sourcePanelId == value) return;
            _sourcePanelId = value;
            if (_state != null) _state["SourcePanelId"] = value ?? "";
        }
    }

    public SampleData? CurrentSample
    {
        get => _currentSample;
        private set => _currentSample = value;
    }

    // ── Sample info display properties ────────────────────────────────────────

    public string WriteTimestamp
        => _currentSample != null
            ? _currentSample.SampleInfo.SourceTimestamp.ToString()
            : "-";

    public string ReceptionTimestamp
        => _currentSample != null ? _currentSample.Timestamp.ToString("O") : "-";

    public string GenerationRank
        => _currentSample != null ? _currentSample.SampleInfo.GenerationRank.ToString() : "-";

    public DetailInspectorViewModel(IEventBroker broker, ISampleViewRegistry? sampleViewRegistry = null)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
    }

    /// <inheritdoc />
    public void Initialize(IDictionary<string, object> componentState)
    {
        _state = componentState;

        _isLinked = componentState.TryGetValue("IsLinked", out var v) && v is bool b ? b : true;

        _sourcePanelId = componentState.TryGetValue("SourcePanelId", out var v2) && v2 is string s && !string.IsNullOrEmpty(s)
            ? s
            : null;

        SubscribeIfLinked();
    }

    private void SubscribeIfLinked()
    {
        if (_isLinked && _sourcePanelId != null)
        {
            var panelId = _sourcePanelId;
            _sampleSubscription = _broker.SubscribeOnUiThread<SampleSelectedEvent>(ev =>
            {
                if (_isLinked && ev.SourcePanelId == panelId)
                    OnSampleReceived(ev);
            });
        }
    }

    /// <summary>
    /// Called on the UI thread (via <see cref="IEventBrokerExtensions.SubscribeOnUiThread{TEvent}"/>).
    /// <see cref="FieldMetadata.Getter"/> is invoked here — UI-thread only.
    /// </summary>
    private void OnSampleReceived(SampleSelectedEvent ev)
    {
        _currentSample = ev.Sample;
        RebuildFieldTree(ev.Sample);
    }

    /// <summary>
    /// Rebuilds the flat field tree from the given sample.
    /// Must only be called on the UI thread — <see cref="FieldMetadata.Getter"/> is invoked here.
    /// </summary>
    private void RebuildFieldTree(SampleData? sample)
    {
        FieldTree.Clear();

        if (sample == null || sample.Payload == null || sample.TopicMetadata?.AllFields == null)
            return;

        foreach (var field in sample.TopicMetadata.AllFields)
        {
            string valueText;
            try
            {
                var val = field.Getter(sample.Payload);
                valueText = val?.ToString() ?? "<null>";
            }
            catch
            {
                valueText = "<error>";
            }

            FieldTree.Add(new FieldInspectorItemViewModel
            {
                Name = field.DisplayName,
                ValueText = valueText,
                IsNested = field.StructuredName.Contains('.'),
                Depth = field.StructuredName.Count(c => c == '.'),
            });
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _sampleSubscription?.Dispose();
        _sampleSubscription = null;
    }
}

