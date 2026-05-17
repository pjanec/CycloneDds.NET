using System.Collections.Generic;
using DdsMonitor.Engine;

namespace DdsMonitor.Plugins.ECS;

/// <summary>
/// Represents the reconstructed state of a BDC domain entity at a specific point in time.
/// </summary>
/// <param name="EntityId">The domain entity identifier.</param>
/// <param name="TargetTime">The timestamp at which the state was reconstructed.</param>
/// <param name="EntityState">Lifecycle state inferred from the found descriptors.</param>
/// <param name="Descriptors">
/// Descriptor samples that were alive at <paramref name="TargetTime"/>, keyed by
/// <see cref="DescriptorIdentity"/>.  An empty dictionary indicates the entity was
/// dead (no descriptors found).
/// </param>
public sealed record EntityHistoricalState(
    int EntityId,
    System.DateTime TargetTime,
    EntityState EntityState,
    IReadOnlyDictionary<DescriptorIdentity, SampleData> Descriptors);
