using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DdsMonitor.Engine;

namespace DdsMonitor.Plugins.Bdc;

/// <summary>
/// Singleton background service that ingests <see cref="IInstanceStore.OnInstanceChanged"/>
/// events and aggregates them into domain <see cref="Entity"/> objects keyed by their
/// numeric EntityId.
///
/// <para>
/// Key fields are discovered via regex matching against <see cref="FieldMetadata.StructuredName"/>
/// (DMON-061). Only standard 32-bit or 64-bit integer value types are accepted; topics
/// with non-integer key fields matching the configured patterns are silently rejected
/// (DMON-062).
/// </para>
/// </summary>
public sealed class EntityStore : IObserver<InstanceTransitionEvent>, IDisposable
{
    // ── Integer types accepted as EntityId / PartId ───────────────────────────
    private static readonly HashSet<Type> ValidIntegerTypes = new()
    {
        typeof(sbyte), typeof(byte),
        typeof(short), typeof(ushort),
        typeof(int),   typeof(uint),
        typeof(long),  typeof(ulong)
    };

    private readonly IInstanceStore _instanceStore;
    private readonly BdcSettings _settings;
    private readonly object _sync = new();
    private readonly Dictionary<int, Entity> _entities = new();
    private IDisposable? _subscription;

    // Compiled regex cache — rebuilt whenever settings change.
    private Regex _entityIdRegex;
    private Regex _partIdRegex;
    private Regex _masterRegex;

    /// <summary>
    /// Initializes the store and subscribes to <see cref="IInstanceStore.OnInstanceChanged"/>.
    /// </summary>
    public EntityStore(IInstanceStore instanceStore, BdcSettings settings)
    {
        _instanceStore = instanceStore ?? throw new ArgumentNullException(nameof(instanceStore));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _entityIdRegex = BuildRegex(_settings.EntityIdPattern);
        _partIdRegex   = BuildRegex(_settings.PartIdPattern);
        _masterRegex   = BuildRegex(_settings.MasterTopicPattern);

        _subscription = instanceStore.OnInstanceChanged.Subscribe(this);
        instanceStore.Cleared += OnCleared;
        settings.SettingsChanged += OnSettingsChanged;
    }

    /// <summary>Raised on the calling thread whenever the entity collection changes.</summary>
    public event Action? Changed;

    /// <summary>
    /// Gets a point-in-time snapshot of all known entities (Alive, Zombie, or Dead).
    /// </summary>
    public IReadOnlyDictionary<int, Entity> Entities
    {
        get
        {
            lock (_sync)
            {
                return new Dictionary<int, Entity>(_entities);
            }
        }
    }

    // ── IObserver<InstanceTransitionEvent> ───────────────────────────────────

    void IObserver<InstanceTransitionEvent>.OnNext(InstanceTransitionEvent evt)
        => ProcessEvent(evt);

    void IObserver<InstanceTransitionEvent>.OnError(Exception error) { /* ignore */ }

    void IObserver<InstanceTransitionEvent>.OnCompleted() { /* ignore */ }

    // ── Core aggregation ──────────────────────────────────────────────────────

    private void ProcessEvent(InstanceTransitionEvent evt)
    {
        var instance = evt.Instance;
        var meta = instance.TopicMetadata;

        // 1. Topic name prefix filter (the BDC "namespace" is the DDS topic name prefix,
        //    e.g. "company.BDC." — not the CLR type namespace stored in meta.Namespace).
        if (!string.IsNullOrEmpty(_settings.NamespacePrefix) &&
            !meta.TopicName.StartsWith(_settings.NamespacePrefix, StringComparison.Ordinal))
        {
            return;
        }

        // 2. Locate EntityId key field via regex (DMON-061).
        Regex entityIdRx, partIdRx, masterRx;
        lock (_sync)
        {
            entityIdRx = _entityIdRegex;
            partIdRx   = _partIdRegex;
            masterRx   = _masterRegex;
        }

        if (!TryFindKeyField(meta, entityIdRx, out int entityFieldIndex))
            return;  // No EntityId field found — not a BDC descriptor.

        // 3. Validate integer type (DMON-062).
        var entityFieldMeta = meta.KeyFields[entityFieldIndex];
        if (!ValidIntegerTypes.Contains(entityFieldMeta.ValueType))
            return;  // Non-integer EntityId type — reject this topic.

        // 4. Locate optional PartId key field.
        long? partIdValue = null;
        if (TryFindKeyField(meta, partIdRx, out int partFieldIndex, skipIndex: entityFieldIndex))
        {
            var partFieldMeta = meta.KeyFields[partFieldIndex];
            if (!ValidIntegerTypes.Contains(partFieldMeta.ValueType))
                return;  // Non-integer PartId type — reject this topic.

            partIdValue = ExtractLong(instance.Key.Values[partFieldIndex]);
        }

        // 5. Extract EntityId value.
        int entityId = (int)ExtractLong(instance.Key.Values[entityFieldIndex]);

        // 6. Build descriptor identity.
        var descriptorId = new DescriptorIdentity(meta.TopicName, partIdValue);

        // 7. Apply transition.
        Entity? entity;
        bool changed;
        lock (_sync)
        {
            if (!_entities.TryGetValue(entityId, out entity))
            {
                entity = new Entity { EntityId = entityId };
                _entities[entityId] = entity;
            }

            changed = ApplyTransition(entity, descriptorId, evt);
        }

        if (changed)
        {
            entity.RecalculateState(masterRx);
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Applies the instance transition to the entity's descriptor map.
    /// Returns <c>true</c> if the descriptor set actually changed.
    /// </summary>
    private static bool ApplyTransition(Entity entity, DescriptorIdentity id, InstanceTransitionEvent evt)
    {
        switch (evt.Kind)
        {
            case TransitionKind.Added:
            case TransitionKind.Updated:
                // The instance is alive — upsert its latest sample.
                if (evt.Instance.State == InstanceState.Alive)
                {
                    entity.Descriptors[id] = evt.Sample;
                    return true;
                }
                // Disposed / NoWriters from an Added/Updated event — treat as removal.
                goto case TransitionKind.Removed;

            case TransitionKind.Removed:
                return entity.Descriptors.Remove(id);

            default:
                return false;
        }
    }

    // ── Settings hot-reload ───────────────────────────────────────────────────

    private void OnSettingsChanged()
    {
        Regex newEntityIdRx = BuildRegex(_settings.EntityIdPattern);
        Regex newPartIdRx   = BuildRegex(_settings.PartIdPattern);
        Regex newMasterRx   = BuildRegex(_settings.MasterTopicPattern);

        lock (_sync)
        {
            _entityIdRegex = newEntityIdRx;
            _partIdRegex   = newPartIdRx;
            _masterRegex   = newMasterRx;

            // Drop all aggregated entities; the caller must re-ingest from the store.
            _entities.Clear();
        }

        Changed?.Invoke();
    }

    private void OnCleared()
    {
        lock (_sync) { _entities.Clear(); }
        Changed?.Invoke();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Searches <see cref="TopicMetadata.KeyFields"/> for the first field whose
    /// <see cref="FieldMetadata.StructuredName"/> matches <paramref name="pattern"/>,
    /// optionally skipping a previously-matched index.
    /// </summary>
    public static bool TryFindKeyField(
        TopicMetadata meta,
        Regex pattern,
        out int fieldIndex,
        int skipIndex = -1)
    {
        for (int i = 0; i < meta.KeyFields.Count; i++)
        {
            if (i == skipIndex) continue;
            if (pattern.IsMatch(meta.KeyFields[i].StructuredName))
            {
                fieldIndex = i;
                return true;
            }
        }

        fieldIndex = -1;
        return false;
    }

    /// <summary>
    /// Returns whether <paramref name="type"/> is a valid integer key type (DMON-062).
    /// </summary>
    public static bool IsValidIntegerType(Type type) => ValidIntegerTypes.Contains(type);

    private static long ExtractLong(object value) =>
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
            // Fallback: treat invalid pattern as "match nothing".
            return new Regex(@"(?!)", RegexOptions.Compiled);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _settings.SettingsChanged -= OnSettingsChanged;
        _instanceStore.Cleared    -= OnCleared;
        _subscription?.Dispose();
        _subscription = null;
    }
}
