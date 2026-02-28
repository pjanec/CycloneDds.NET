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
        var store = new CapturingSampleStore();
        var provider = new SamplesPanelVirtualizer(store);

        provider.GetRange(120, 40);

        Assert.Equal(120, store.LastStartIndex);
        Assert.Equal(40, store.LastCount);
    }

    private sealed class CapturingSampleStore : ISampleStore
    {
        public int LastStartIndex { get; private set; } = -1;

        public int LastCount { get; private set; } = -1;

        public IReadOnlyList<SampleData> AllSamples => Array.Empty<SampleData>();

        public int CurrentFilteredCount => 0;

        public event Action? OnViewRebuilt
        {
            add { }
            remove { }
        }

        public ITopicSamples GetTopicSamples(Type topicType) => throw new NotSupportedException();

        public void Append(SampleData sample) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public ReadOnlyMemory<SampleData> GetVirtualView(int startIndex, int count)
        {
            LastStartIndex = startIndex;
            LastCount = count;
            return ReadOnlyMemory<SampleData>.Empty;
        }

        public void SetFilter(Func<SampleData, bool>? compiledFilterPredicate) => throw new NotSupportedException();

        public void SetSortSpec(FieldMetadata? field, SortDirection direction) => throw new NotSupportedException();
    }
}
