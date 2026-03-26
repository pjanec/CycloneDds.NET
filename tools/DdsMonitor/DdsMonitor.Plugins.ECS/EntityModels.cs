using System;
using System.Collections.Generic;

namespace DdsMonitor.Plugins.ECS;

/// <summary>
/// Lifecycle state of a BDC domain entity.
/// </summary>
public enum EntityState
{
    /// <summary>Entity has a live Master descriptor.</summary>
    Alive,

    /// <summary>Entity has non-master descriptors but no Master descriptor.</summary>
    Zombie,

    /// <summary>Entity has no descriptors at all.</summary>
    Dead
}

/// <summary>
/// Uniquely identifies a single descriptor within an entity.
/// A descriptor is the combination of a DDS topic name and an optional PartId.
/// </summary>
public sealed record DescriptorIdentity(string TopicName, long? PartId);

/// <summary>
/// An immutable snapshot of a state-transition recorded by the entity journal.
/// </summary>
public sealed record EntityJournalRecord(DateTime Timestamp, EntityState NewState, string Description);

/// <summary>
/// Aggregates all descriptors that share the same EntityId into a single domain entity.
/// </summary>
public sealed class Entity
{
    private readonly object _lock = new();
    private EntityState _state = EntityState.Dead;

    /// <summary>Gets the domain entity identifier.</summary>
    public int EntityId { get; init; }

    /// <summary>Gets the latest sample per descriptor identity, keyed by <see cref="DescriptorIdentity"/>.</summary>
    public Dictionary<DescriptorIdentity, DdsMonitor.Engine.SampleData> Descriptors { get; } = new();

    /// <summary>Gets the ordered journal of state transitions.</summary>
    public List<EntityJournalRecord> Journal { get; } = new();

    /// <summary>Gets the current lifecycle state.</summary>
    public EntityState State
    {
        get { lock (_lock) { return _state; } }
    }

    /// <summary>Raised when the entity's <see cref="State"/> changes.</summary>
    public event Action? StateChanged;

    /// <summary>
    /// Recomputes the entity state from the current set of descriptors and records a
    /// journal entry if the state changes.
    /// </summary>
    /// <param name="masterTopicPattern">
    /// Regex string matched against <see cref="DescriptorIdentity.TopicName"/> to determine
    /// whether a descriptor is this entity's "Master".
    /// </param>
    internal void RecalculateState(System.Text.RegularExpressions.Regex masterRegex)
    {
        EntityState newState;
        bool hasMaster;

        lock (_lock)
        {
            if (Descriptors.Count == 0)
            {
                newState = EntityState.Dead;
            }
            else
            {
                hasMaster = false;
                foreach (var key in Descriptors.Keys)
                {
                    if (masterRegex.IsMatch(key.TopicName))
                    {
                        hasMaster = true;
                        break;
                    }
                }
                newState = hasMaster ? EntityState.Alive : EntityState.Zombie;
            }

            if (newState == _state)
                return;

            Journal.Add(new EntityJournalRecord(
                DateTime.UtcNow,
                newState,
                $"State transitioned from {_state} to {newState}"));

            _state = newState;
        }

        StateChanged?.Invoke();
    }
}
