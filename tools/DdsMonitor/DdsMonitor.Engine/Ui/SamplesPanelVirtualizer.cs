using System;

namespace DdsMonitor.Engine;

/// <summary>
/// Provides sample ranges for virtualized grids by delegating to an <see cref="ISampleView"/>.
/// </summary>
public sealed class SamplesPanelVirtualizer
{
    private readonly ISampleView _sampleView;

    /// <summary>
    /// Initializes a new instance of the <see cref="SamplesPanelVirtualizer"/> class.
    /// </summary>
    public SamplesPanelVirtualizer(ISampleView sampleView)
    {
        _sampleView = sampleView ?? throw new ArgumentNullException(nameof(sampleView));
    }

    /// <summary>
    /// Gets the total number of samples in the current filtered view.
    /// </summary>
    public int TotalCount => _sampleView.CurrentFilteredCount;

    /// <summary>
    /// Requests a virtualized slice from the sample view.
    /// </summary>
    public ReadOnlyMemory<SampleData> GetRange(int startIndex, int count)
    {
        return _sampleView.GetVirtualView(startIndex, count);
    }
}
