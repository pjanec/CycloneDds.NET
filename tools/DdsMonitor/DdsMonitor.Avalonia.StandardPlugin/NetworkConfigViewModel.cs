using System.Collections.ObjectModel;
using DdsMonitor.Engine;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// ViewModel for the Network Configuration panel.
/// Allows adding, removing, and applying DDS participant configurations.
/// </summary>
public sealed class NetworkConfigViewModel
{
    private readonly IDdsBridge _ddsBridge;
    private readonly IEventBroker _eventBroker;
    private string? _applyError;

    /// <summary>Gets the editable list of participant configurations.</summary>
    public ObservableCollection<ParticipantConfigViewModel> Participants { get; } = new();

    /// <summary>Gets the last apply error, or <c>null</c> if the last apply succeeded.</summary>
    public string? ApplyError
    {
        get => _applyError;
        private set => _applyError = value;
    }

    public NetworkConfigViewModel(IDdsBridge ddsBridge, IEventBroker eventBroker)
    {
        _ddsBridge = ddsBridge ?? throw new ArgumentNullException(nameof(ddsBridge));
        _eventBroker = eventBroker ?? throw new ArgumentNullException(nameof(eventBroker));

        // Load existing participants
        foreach (var cfg in ddsBridge.ParticipantConfigs)
        {
            Participants.Add(new ParticipantConfigViewModel
            {
                DomainId = (int)cfg.DomainId,
                PartitionName = cfg.PartitionName,
            });
        }
    }

    /// <summary>Adds a new participant row with default values.</summary>
    public void AddRow()
    {
        Participants.Add(new ParticipantConfigViewModel { DomainId = 0, PartitionName = string.Empty });
    }

    /// <summary>Removes the participant row at the specified index.</summary>
    public void RemoveRow(int index)
    {
        if (index >= 0 && index < Participants.Count)
            Participants.RemoveAt(index);
    }

    /// <summary>Applies all participant configurations to the DDS bridge and publishes a change event.</summary>
    public void Apply()
    {
        try
        {
            // No-op guard: if the current list already matches the bridge state, skip all calls.
            var bridgeConfigs = _ddsBridge.ParticipantConfigs;
            if (bridgeConfigs.Count == Participants.Count &&
                bridgeConfigs.Zip(Participants).All(pair =>
                    pair.First.DomainId == (uint)pair.Second.DomainId &&
                    pair.First.PartitionName == pair.Second.PartitionName))
            {
                ApplyError = null;
                return;
            }

            ApplyError = null;
            foreach (var p in Participants)
                _ddsBridge.AddParticipant((uint)p.DomainId, p.PartitionName);

            _eventBroker.Publish(new ParticipantsChangedEvent(_ddsBridge.ParticipantConfigs));
        }
        catch (Exception ex)
        {
            ApplyError = $"Failed to apply: {ex.Message}";
        }
    }
}
