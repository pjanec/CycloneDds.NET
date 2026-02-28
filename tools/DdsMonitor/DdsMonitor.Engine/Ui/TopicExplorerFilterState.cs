using System;

namespace DdsMonitor.Engine;

/// <summary>
/// Represents a tri-state filter value.
/// </summary>
public enum TriStateFilter
{
    /// <summary>
    /// The filter does not affect the results.
    /// </summary>
    Ignore,

    /// <summary>
    /// The filter requires the condition to be true.
    /// </summary>
    Include,

    /// <summary>
    /// The filter requires the condition to be false.
    /// </summary>
    Exclude
}

/// <summary>
/// Identifies the topic explorer filter toggles.
/// </summary>
public enum TopicFilterKind
{
    /// <summary>
    /// Samples have been received for the topic.
    /// </summary>
    Received,

    /// <summary>
    /// The topic is currently subscribed.
    /// </summary>
    Subscribed,

    /// <summary>
    /// The topic is keyed.
    /// </summary>
    Keyed,

    /// <summary>
    /// The topic has alive instances.
    /// </summary>
    Alive
}

/// <summary>
/// Holds tri-state filter values for the topic explorer.
/// </summary>
public sealed class TopicExplorerFilterState
{
    /// <summary>
    /// Gets the received filter state.
    /// </summary>
    public TriStateFilter Received { get; private set; } = TriStateFilter.Ignore;

    /// <summary>
    /// Gets the subscribed filter state.
    /// </summary>
    public TriStateFilter Subscribed { get; private set; } = TriStateFilter.Ignore;

    /// <summary>
    /// Gets the keyed filter state.
    /// </summary>
    public TriStateFilter Keyed { get; private set; } = TriStateFilter.Ignore;

    /// <summary>
    /// Gets the alive filter state.
    /// </summary>
    public TriStateFilter Alive { get; private set; } = TriStateFilter.Ignore;

    /// <summary>
    /// Advances the specified filter through Ignore -> Include -> Exclude -> Ignore.
    /// </summary>
    public void Cycle(TopicFilterKind kind)
    {
        switch (kind)
        {
            case TopicFilterKind.Received:
                Received = Next(Received);
                break;
            case TopicFilterKind.Subscribed:
                Subscribed = Next(Subscribed);
                break;
            case TopicFilterKind.Keyed:
                Keyed = Next(Keyed);
                break;
            case TopicFilterKind.Alive:
                Alive = Next(Alive);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown filter kind.");
        }
    }

    private static TriStateFilter Next(TriStateFilter current)
    {
        return current switch
        {
            TriStateFilter.Ignore => TriStateFilter.Include,
            TriStateFilter.Include => TriStateFilter.Exclude,
            _ => TriStateFilter.Ignore
        };
    }
}
