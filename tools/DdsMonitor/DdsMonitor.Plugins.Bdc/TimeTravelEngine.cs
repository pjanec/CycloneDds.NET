using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CycloneDDS.Runtime;
using DdsMonitor.Engine;

namespace DdsMonitor.Plugins.Bdc;

/// <summary>
/// Reconstructs the historical state of a BDC domain entity at a requested timestamp
/// by binary-searching the chronological <see cref="ISampleStore"/> ledger.
///
/// <para>
/// The algorithm per topic type:
/// <list type="number">
///   <item>Collect the set of unique CLR topic types present in the sample store.</item>
///   <item>
///     For each BDC-format type (namespace filter + EntityId field found via regex),
///     binary-search its <see cref="ITopicSamples.Samples"/> list to locate the
///     time-boundary index where <c>Timestamp &lt;= targetTime</c>.
///   </item>
///   <item>
///     Scan backwards from that boundary, collecting the <em>latest</em> sample before
///     <paramref name="targetTime"/> per unique PartId for the requested entity.
///   </item>
///   <item>Discard samples whose <c>SampleInfo.InstanceState</c> indicates disposal.</item>
/// </list>
/// </para>
/// </summary>
public sealed class TimeTravelEngine
{
    private static readonly HashSet<Type> ValidIntegerTypes = new()
    {
        typeof(sbyte), typeof(byte),
        typeof(short), typeof(ushort),
        typeof(int),   typeof(uint),
        typeof(long),  typeof(ulong)
    };

    private readonly ISampleStore _sampleStore;
    private readonly BdcSettings  _settings;

    /// <summary>
    /// Initialises the engine with the shared sample store and plugin settings.
    /// </summary>
    public TimeTravelEngine(ISampleStore sampleStore, BdcSettings settings)
    {
        _sampleStore = sampleStore ?? throw new ArgumentNullException(nameof(sampleStore));
        _settings    = settings    ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Reconstructs the entity state at <paramref name="targetTime"/> by
    /// binary-searching all descriptor samples for the most recent alive sample
    /// at or before that timestamp.
    /// </summary>
    /// <param name="entityId">Target BDC entity identifier.</param>
    /// <param name="targetTime">The point in time to reconstruct.</param>
    /// <returns>
    /// An <see cref="EntityHistoricalState"/> whose <see cref="EntityHistoricalState.Descriptors"/>
    /// contains the descriptors alive at <paramref name="targetTime"/>.  The collection is
    /// empty when the entity was dead at that time.
    /// </returns>
    public EntityHistoricalState GetHistoricalState(int entityId, DateTime targetTime)
    {
        var entityIdRx = BuildRegex(_settings.EntityIdPattern);
        var partIdRx   = BuildRegex(_settings.PartIdPattern);
        var masterRx   = BuildRegex(_settings.MasterTopicPattern);

        // Collect unique topic metadata from all samples (avoids repeated scanning).
        var seenMeta = new Dictionary<Type, TopicMetadata>();
        foreach (var sample in _sampleStore.AllSamples)
            seenMeta.TryAdd(sample.TopicMetadata.TopicType, sample.TopicMetadata);

        var descriptors = new Dictionary<DescriptorIdentity, SampleData>();

        foreach (var (topicType, meta) in seenMeta)
        {
            // Namespace prefix filter (BDC topic names start with the configured prefix).
            if (!string.IsNullOrEmpty(_settings.NamespacePrefix) &&
                !meta.TopicName.StartsWith(_settings.NamespacePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            // Locate EntityId key field via regex.
            if (!EntityStore.TryFindKeyField(meta, entityIdRx, out int entityFieldIdx))
                continue;

            var entityField = meta.KeyFields[entityFieldIdx];
            if (!ValidIntegerTypes.Contains(entityField.ValueType))
                continue;

            // Locate optional PartId key field.
            FieldMetadata? partField = null;
            if (EntityStore.TryFindKeyField(meta, partIdRx, out int partFieldIdx, skipIndex: entityFieldIdx))
            {
                var candidate = meta.KeyFields[partFieldIdx];
                if (ValidIntegerTypes.Contains(candidate.ValueType))
                    partField = candidate;
            }

            // Binary-search + backwards-scan for the latest sample per PartId.
            var topicSamples = _sampleStore.GetTopicSamples(topicType);
            var latestPerPart = FindLatestBeforeTime(topicSamples.Samples, targetTime, entityId, entityField, partField);

            foreach (var (partId, sample) in latestPerPart)
            {
                // Exclude disposal / no-writers samples.
                if (!IsAliveSample(sample.SampleInfo.InstanceState))
                    continue;

                var descId = new DescriptorIdentity(meta.TopicName, partId);
                descriptors[descId] = sample;
            }        }

        // Derive entity state from found descriptors.
        var entityState = DeriveEntityState(descriptors.Keys, masterRx);
        return new EntityHistoricalState(entityId, targetTime, entityState, descriptors);
    }

    // ── Core binary-search helper ─────────────────────────────────────────────

    /// <summary>
    /// Binary-searches <paramref name="samples"/> for the rightmost index at or before
    /// <paramref name="targetTime"/>, then scans backwards to collect the latest sample
    /// per unique PartId (or <c>null</c> when the topic is not multi-instance) for the
    /// specified <paramref name="entityId"/>.
    /// </summary>
    /// <returns>
    /// A list of <c>(PartId, Sample)</c> tuples where <c>PartId</c> is <c>null</c> for
    /// topics that have no PartId field.
    /// </returns>
    public static List<(long? PartId, SampleData Sample)> FindLatestBeforeTime(
        IReadOnlyList<SampleData> samples,
        DateTime targetTime,
        int entityId,
        FieldMetadata entityField,
        FieldMetadata? partField)
    {
        // Step 1: binary search for the rightmost sample at or before targetTime.
        int lo = 0, hi = samples.Count - 1, timeBoundary = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (samples[mid].Timestamp <= targetTime)
            {
                timeBoundary = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        if (timeBoundary < 0)
            return new List<(long?, SampleData)>();

        // Step 2: scan backwards; keep the first (= latest) sample per PartId.
        // Use long.MinValue as an internal sentinel for "topic has no PartId field"
        // to avoid Dictionary<long?,V> null-key ArgumentNullException in .NET generics.
        const long noPartId = long.MinValue;
        var resultMap = new Dictionary<long, SampleData>();

        for (int i = timeBoundary; i >= 0; i--)
        {
            var s   = samples[i];
            var eid = (int)ExtractLong(entityField.Getter(s.Payload));
            if (eid != entityId)
                continue;

            long partKey = partField is not null
                ? ExtractLong(partField.Getter(s.Payload))
                : noPartId;

            if (!resultMap.ContainsKey(partKey))
                resultMap[partKey] = s;
        }

        // Translate back to (long?, SampleData): null PartId for topics without a PartId field.
        bool hasPartField = partField is not null;
        var output = new List<(long?, SampleData)>(resultMap.Count);
        foreach (var (k, v) in resultMap)
            output.Add((hasPartField ? (long?)k : null, v));

        return output;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when <paramref name="instanceState"/> indicates an alive
    /// instance (mirrors <c>InstanceStore.MapInstanceState</c> semantics: anything
    /// other than an explicit <em>Disposed</em> or <em>NoWriters</em> is alive).
    /// </summary>
    public static bool IsAliveSample(DdsInstanceState instanceState) =>
        instanceState != DdsInstanceState.NotAliveDisposed &&
        instanceState != DdsInstanceState.NotAliveNoWriters;

    private static EntityState DeriveEntityState(
        IEnumerable<DescriptorIdentity> keys,
        Regex masterRx)
    {
        bool any = false;
        foreach (var k in keys)
        {
            any = true;
            if (masterRx.IsMatch(k.TopicName))
                return EntityState.Alive;
        }
        return any ? EntityState.Zombie : EntityState.Dead;
    }

    private static long ExtractLong(object? value) =>
        value switch
        {
            sbyte  sb => sb,
            byte   b  => b,
            short  s  => s,
            ushort us => us,
            int    i  => i,
            uint   u  => u,
            long   l  => l,
            ulong  ul => (long)ul,
            _ => Convert.ToInt64(value)
        };

    private static Regex BuildRegex(string pattern)
    {
        try
        {
            return new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException)
        {
            return new Regex(@"(?!)", RegexOptions.Compiled);
        }
    }
}
