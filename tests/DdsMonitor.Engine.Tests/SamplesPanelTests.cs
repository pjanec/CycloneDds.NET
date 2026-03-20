using System;
using System.Collections.Generic;
using DdsMonitor.Engine;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class SamplesPanelTests
{
    [Fact]
    public void SamplesPanel_VirtualizeCallback_RequestsCorrectRange()
    {
        var view = new CapturingView();
        var provider = new SamplesPanelVirtualizer(view);

        provider.GetRange(120, 40);

        Assert.Equal(120, view.LastStartIndex);
        Assert.Equal(40, view.LastCount);
    }

    private sealed class CapturingView : ISampleView
    {
        public int LastStartIndex { get; private set; } = -1;

        public int LastCount { get; private set; } = -1;

        public int CurrentFilteredCount => 0;

        public event Action? OnViewRebuilt
        {
            add { }
            remove { }
        }

        public ReadOnlyMemory<SampleData> GetVirtualView(int startIndex, int count)
        {
            LastStartIndex = startIndex;
            LastCount = count;
            return ReadOnlyMemory<SampleData>.Empty;
        }

        public SampleData[] GetFilteredSnapshot() => Array.Empty<SampleData>();

        public void SetFilter(Func<SampleData, bool>? compiledFilterPredicate) { }

        public void SetSortSpec(FieldMetadata? field, SortDirection direction) { }

        public void Dispose() { }
    }
}
