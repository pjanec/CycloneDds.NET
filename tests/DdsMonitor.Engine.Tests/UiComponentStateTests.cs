using System;
using System.Collections.Generic;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Tests.Robotics;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class UiComponentStateTests
{
    [Fact]
    public void TopicExplorerPanel_TriStateFilter_CyclesCorrectly()
    {
        var state = new TopicExplorerFilterState();

        Assert.Equal(TriStateFilter.Ignore, state.Received);

        state.Cycle(TopicFilterKind.Received);
        Assert.Equal(TriStateFilter.Include, state.Received);

        state.Cycle(TopicFilterKind.Received);
        Assert.Equal(TriStateFilter.Exclude, state.Received);

        state.Cycle(TopicFilterKind.Received);
        Assert.Equal(TriStateFilter.Ignore, state.Received);
    }

    [Fact]
    public void TopicPicker_FiltersOnKeystroke()
    {
        var matching = new TopicMetadata(typeof(RobotTopic));
        var nonMatching = new TopicMetadata(typeof(OtherTopic));
        var topics = new List<TopicMetadata>();

        const int TotalTopics = 100;
        for (var i = 0; i < TotalTopics; i++)
        {
            topics.Add(i % 2 == 0 ? matching : nonMatching);
        }

        var results = TopicPickerFilter.FilterTopics(topics, "rob");

        Assert.All(results, topic => Assert.Contains("rob", topic.ShortName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TopicPicker_MatchesBothNameAndNamespace()
    {
        var match = new TopicMetadata(typeof(NavigationTopic));
        var miss = new TopicMetadata(typeof(OtherTopic));

        var results = TopicPickerFilter.FilterTopics(new[] { match, miss }, "robotics");

        Assert.Contains(results, topic => topic == match);
        Assert.DoesNotContain(results, topic => topic == miss);
    }

    [Fact]
    public void ColumnPicker_AddField_MovesToSelected()
    {
        var field = CreateField("Payload.Id");
        var state = new ColumnPickerState(new[] { field }, Array.Empty<FieldMetadata>());

        state.AddField(field);

        Assert.DoesNotContain(field, state.AvailableFields);
        Assert.Contains(field, state.SelectedFields);
    }

    [Fact]
    public void ColumnPicker_RemoveField_MovesToAvailable()
    {
        var field = CreateField("Payload.Id");
        var state = new ColumnPickerState(Array.Empty<FieldMetadata>(), new[] { field });

        state.RemoveField(field);

        Assert.DoesNotContain(field, state.SelectedFields);
        Assert.Contains(field, state.AvailableFields);
    }

    [Fact]
    public void ColumnPicker_Apply_ReturnsSelectedOrder()
    {
        var first = CreateField("Payload.Id");
        var second = CreateField("Payload.Value");
        var third = CreateField("Header.Timestamp");

        var state = new ColumnPickerState(new[] { first, second, third }, Array.Empty<FieldMetadata>());

        state.AddField(first);
        state.AddField(second);
        state.AddField(third);

        var order = state.GetSelectedOrder();

        Assert.Equal(new[] { first, second, third }, order);
    }

    private static FieldMetadata CreateField(string name)
    {
        return new FieldMetadata(
            name,
            name,
            typeof(int),
            _ => 0,
            (_, __) => { },
            false);
    }
}
