using System;

namespace DdsMonitor.Engine;

/// <summary>
/// Provides sample ranges for virtualized grids.
/// </summary>
public sealed class SamplesPanelVirtualizer
{
    private readonly ISampleStore _sampleStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="SamplesPanelVirtualizer"/> class.
    /// </summary>
    public SamplesPanelVirtualizer(ISampleStore sampleStore)
    {
        _sampleStore = sampleStore ?? throw new ArgumentNullException(nameof(sampleStore));
    }

    /// <summary>
    /// Gets the total number of samples in the current filtered view.
    /// </summary>
    public int TotalCount => _sampleStore.CurrentFilteredCount;

    /// <summary>
    /// Requests a virtualized slice from the sample store.
    /// </summary>
    public ReadOnlyMemory<SampleData> GetRange(int startIndex, int count)
    {
        return _sampleStore.GetVirtualView(startIndex, count);
    }
}
