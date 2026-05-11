namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// Editable row view model for a single DDS participant configuration.
/// Used in the Network Configuration panel.
/// </summary>
public sealed class ParticipantConfigViewModel
{
    /// <summary>Gets or sets the DDS domain identifier.</summary>
    public int DomainId { get; set; }

    /// <summary>Gets or sets the optional partition name.</summary>
    public string PartitionName { get; set; } = string.Empty;
}
