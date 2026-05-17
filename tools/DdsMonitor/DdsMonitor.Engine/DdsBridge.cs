using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Channels;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Tracking;

namespace DdsMonitor.Engine;

/// <summary>
/// Coordinates DDS readers and writers for dynamic topic types.
/// Supports multiple concurrent <see cref="DdsParticipant"/> instances (ME1-T06)
/// and a shared global ordinal counter with pre-filter ordinal allocation (ME1-T07).
/// </summary>
public sealed class DdsBridge : IDdsBridge
{
    private const int EmptyCount = 0;

    private readonly object _sync = new();
    private readonly Dictionary<Type, IDynamicReader> _activeReaders = new();
    // D04: auxiliary readers per extra participant (index 0 = participant 1, index 1 = participant 2, ...)
    private readonly List<Dictionary<Type, IDynamicReader>> _auxReadersPerParticipant = new();
    // Tracks topic types explicitly unsubscribed by the user so that auto-subscribe
    // on panel recreations (minimize/restore) does not silently undo the user's choice.
    private readonly HashSet<Type> _explicitlyUnsubscribedTopicTypes = new();
    private readonly ChannelWriter<SampleData> _channelWriter;
    private readonly List<DdsParticipant> _participants = new();
    private readonly List<ParticipantConfig> _participantConfigs = new();
    private readonly ISampleStore? _sampleStore;
    private readonly IInstanceStore? _instanceStore;
    private readonly OrdinalCounter _ordinalCounter;
    private Func<SampleData, bool>? _filter;
    private bool _disposed;

    /// <inheritdoc />
    public event Action? ReadersChanged;

    // ─────────────────────────────────────────────────────────────────────────
    // Constructors
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Backward-compatible constructor: creates a single participant on domain 0.
    /// </summary>
    public DdsBridge(ChannelWriter<SampleData> channelWriter, string? initialPartition = null)
        : this(channelWriter, null, initialPartition, null, null, null)
    {
    }

    /// <summary>
    /// Full constructor used by the DI container.
    /// </summary>
    public DdsBridge(
        ChannelWriter<SampleData> channelWriter,
        IReadOnlyList<ParticipantConfig>? participants,
        string? initialPartition,
        ISampleStore? sampleStore,
        IInstanceStore? instanceStore,
        OrdinalCounter? ordinalCounter)
    {
        _channelWriter = channelWriter ?? throw new ArgumentNullException(nameof(channelWriter));
        _sampleStore = sampleStore;
        _instanceStore = instanceStore;
        _ordinalCounter = ordinalCounter ?? new OrdinalCounter();

        // Build the participant list from config.
        var configs = (participants != null && participants.Count > 0)
            ? participants
            : new[] { new ParticipantConfig { DomainId = 0, PartitionName = initialPartition ?? string.Empty } };

        foreach (var cfg in configs)
        {
            _participantConfigs.Add(cfg);
            var participant = new DdsParticipant(cfg.DomainId);
            // Enable receiver-only sender tracking so DynamicReaders can populate
            // SampleData.Sender from the __FcdcSenderIdentity topic.
            participant.EnableSenderMonitoring();
            _participants.Add(participant);
            // Participants beyond index 0 each get an aux-reader slot.
            if (_participants.Count > 1)
                _auxReadersPerParticipant.Add(new Dictionary<Type, IDynamicReader>());
        }

        // Use the first participant's partition as the current partition for backward compat.
        CurrentPartition = _participantConfigs.Count > 0 ? _participantConfigs[0].PartitionName : initialPartition;
        if (string.IsNullOrEmpty(CurrentPartition))
            CurrentPartition = initialPartition;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IDdsBridge properties
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public DdsParticipant Participant => _participants.Count > 0 ? _participants[0] : throw new InvalidOperationException("No participants.");

    /// <inheritdoc />
    public IReadOnlyList<DdsParticipant> Participants
    {
        get { lock (_sync) { return _participants.AsReadOnly(); } }
    }

    /// <inheritdoc />
    public IReadOnlyList<ParticipantConfig> ParticipantConfigs
    {
        get { lock (_sync) { return _participantConfigs.AsReadOnly(); } }
    }

    /// <inheritdoc />
    public string? CurrentPartition { get; private set; }

    /// <inheritdoc />
    public bool IsPaused { get; set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<Type, IDynamicReader> ActiveReaders
    {
        get
        {
            lock (_sync)
            {
                return new Dictionary<Type, IDynamicReader>(_activeReaders);
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlySet<Type> ExplicitlyUnsubscribedTopicTypes
    {
        get
        {
            lock (_sync)
            {
                return new HashSet<Type>(_explicitlyUnsubscribedTopicTypes);
            }
        }
    }

    /// <inheritdoc />
    public void InitializeExplicitlyUnsubscribed(IEnumerable<Type> types)
    {
        if (types == null) throw new ArgumentNullException(nameof(types));

        lock (_sync)
        {
            foreach (var t in types)
            {
                // Only add to the excluded set if there is no active reader for this type.
                if (!_activeReaders.ContainsKey(t))
                    _explicitlyUnsubscribedTopicTypes.Add(t);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Subscription management
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public IDynamicReader Subscribe(TopicMetadata meta)
    {
        return TrySubscribe(meta, out var reader, out var errorMessage)
            ? reader!
            : new NullDynamicReader(meta, errorMessage);
    }

    /// <inheritdoc />
    public bool TrySubscribe(TopicMetadata meta, out IDynamicReader? reader, out string? errorMessage)
    {
        if (meta == null)
        {
            throw new ArgumentNullException(nameof(meta));
        }

        lock (_sync)
        {
            ThrowIfDisposed();

            reader = null;
            errorMessage = null;

            // A manual subscribe call always clears the explicit-unsubscribe marker,
            // even if the topic is already active, so the user can re-check a topic
            // that was previously unchecked and have auto-subscribe honour it again.
            _explicitlyUnsubscribedTopicTypes.Remove(meta.TopicType);

            if (_activeReaders.TryGetValue(meta.TopicType, out var existing))
            {
                reader = existing;
                return true;
            }

            try
            {
                // Primary reader (participant 0) is returned to callers.
                reader = CreateAndWireReader(meta, 0);
                reader.Start(CurrentPartition);
                _activeReaders[meta.TopicType] = reader;

                // Create additional readers for participants 1..N (their samples flow
                // into the same channel but are not exposed via ActiveReaders).
                // D04: store them in _auxReadersPerParticipant for later hot-wiring.
                for (var i = 1; i < _participants.Count; i++)
                {
                    var aux = CreateAndWireReader(meta, i);
                    aux.Start(_participantConfigs[i].PartitionName);
                    if (i - 1 < _auxReadersPerParticipant.Count)
                        _auxReadersPerParticipant[i - 1][meta.TopicType] = aux;
                }

                ReadersChanged?.Invoke();
                return true;
            }
            catch (Exception ex) when (TryGetDescriptorError(ex, out var message))
            {
                reader?.Dispose();
                reader = null;
                errorMessage = message;
                return false;
            }
        }
    }

    /// <inheritdoc />
    public void Unsubscribe(TopicMetadata meta)
    {
        if (meta == null)
        {
            throw new ArgumentNullException(nameof(meta));
        }

        lock (_sync)
        {
            ThrowIfDisposed();

            if (!_activeReaders.TryGetValue(meta.TopicType, out var reader))
            {
                return;
            }

            _activeReaders.Remove(meta.TopicType);
            // Record explicit unsubscription so auto-subscribe does not re-add it
            // when the Topics panel is recreated after minimize/restore.
            _explicitlyUnsubscribedTopicTypes.Add(meta.TopicType);
            reader.Dispose();

            // D04: also dispose auxiliary readers for this topic.
            foreach (var auxMap in _auxReadersPerParticipant)
            {
                if (auxMap.TryGetValue(meta.TopicType, out var auxReader))
                {
                    auxMap.Remove(meta.TopicType);
                    auxReader.Dispose();
                }
            }

            ReadersChanged?.Invoke();
        }
    }

    /// <inheritdoc />
    public IDynamicWriter GetWriter(TopicMetadata meta)
    {
        if (meta == null)
        {
            throw new ArgumentNullException(nameof(meta));
        }

        lock (_sync)
        {
            ThrowIfDisposed();
            return CreateWriter(meta);
        }
    }

    /// <inheritdoc />
    public void ChangePartition(string? newPartition)
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            if (_activeReaders.Count == EmptyCount)
            {
                CurrentPartition = newPartition;
                return;
            }

            var metas = new List<TopicMetadata>(_activeReaders.Count);
            foreach (var reader in _activeReaders.Values)
            {
                metas.Add(reader.TopicMetadata);
                reader.Dispose();
            }

            _activeReaders.Clear();

            // D04: also dispose all auxiliary readers before re-subscribing
            foreach (var auxMap in _auxReadersPerParticipant)
            {
                foreach (var auxReader in auxMap.Values)
                    auxReader.Dispose();
                auxMap.Clear();
            }

            CurrentPartition = newPartition;

            foreach (var meta in metas)
            {
                var reader = CreateAndWireReader(meta, 0);
                reader.Start(CurrentPartition);
                _activeReaders[meta.TopicType] = reader;

                // Recreate aux readers for all extra participants
                for (var i = 1; i < _participants.Count; i++)
                {
                    var aux = CreateAndWireReader(meta, i);
                    aux.Start(_participantConfigs[i].PartitionName);
                    if (i - 1 < _auxReadersPerParticipant.Count)
                        _auxReadersPerParticipant[i - 1][meta.TopicType] = aux;
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Multi-participant management (ME1-T06)
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void AddParticipant(uint domainId, string partitionName)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            var cfg = new ParticipantConfig { DomainId = domainId, PartitionName = partitionName };
            _participantConfigs.Add(cfg);
            var newParticipant = new DdsParticipant(domainId);
            newParticipant.EnableSenderMonitoring();
            _participants.Add(newParticipant);

            // D04: Each new participant beyond index 0 gets an aux-reader slot.
            var auxMap = new Dictionary<Type, IDynamicReader>();
            _auxReadersPerParticipant.Add(auxMap);

            // D04: Hot-wire all existing active subscriptions into the new participant.
            var newParticipantIndex = _participants.Count - 1;
            foreach (var kv in _activeReaders)
            {
                var meta = kv.Value.TopicMetadata;
                try
                {
                    var aux = CreateAndWireReader(meta, newParticipantIndex);
                    aux.Start(cfg.PartitionName);
                    auxMap[meta.TopicType] = aux;
                }
                catch
                {
                    // Swallow — new participant is best-effort; existing reads are unaffected.
                }
            }
        }
    }

    /// <inheritdoc />
    public void RemoveParticipant(int participantIndex)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (participantIndex < 0 || participantIndex >= _participants.Count)
                throw new ArgumentOutOfRangeException(nameof(participantIndex));

            // D04: dispose and remove aux readers for this participant (only for index >= 1).
            var auxIndex = participantIndex - 1;
            if (auxIndex >= 0 && auxIndex < _auxReadersPerParticipant.Count)
            {
                foreach (var auxReader in _auxReadersPerParticipant[auxIndex].Values)
                    auxReader.Dispose();
                _auxReadersPerParticipant.RemoveAt(auxIndex);
            }

            _participants[participantIndex].Dispose();
            _participants.RemoveAt(participantIndex);
            _participantConfigs.RemoveAt(participantIndex);
        }
    }

    /// <inheritdoc />
    public void ResetAll()
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            // Reset shared state: clear ordinal counter and data stores.
            // Readers are intentionally preserved so active subscriptions survive.
            _ordinalCounter.Reset();
            _sampleStore?.Clear();
            _instanceStore?.Clear();
        }

        // Trigger a compacting GC on a background thread so the freed LOH backing
        // arrays and SampleData objects are returned to the OS immediately.
        // This is appropriate here because Reset is an explicit user action that
        // should result in a visible drop in Task Manager / process monitors.
        // Running on a background thread avoids blocking the Blazor circuit.
        _ = Task.Run(static () =>
        {
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
                System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Startup filter (ME1-T07)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the startup filter predicate applied to every incoming sample before the
    /// global ordinal counter is incremented.  Samples not matching the predicate are
    /// silently dropped.  Pass <c>null</c> to accept all samples.
    /// </summary>
    public void SetFilter(Func<SampleData, bool>? filter)
    {
        lock (_sync) { _filter = filter; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Disposal
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var reader in _activeReaders.Values)
            {
                reader.Dispose();
            }

            _activeReaders.Clear();

            // D04: dispose all auxiliary readers
            foreach (var auxMap in _auxReadersPerParticipant)
            {
                foreach (var auxReader in auxMap.Values)
                    auxReader.Dispose();
            }
            _auxReadersPerParticipant.Clear();

            foreach (var participant in _participants)
            {
                participant.Dispose();
            }

            _participants.Clear();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private IDynamicReader CreateReader(TopicMetadata meta, int participantIndex)
    {
        var participant = _participants[participantIndex];
        var cfg = participantIndex < _participantConfigs.Count
            ? _participantConfigs[participantIndex]
            : new ParticipantConfig();

        var readerConfig = new DynamicReaderConfig
        {
            OrdinalCounter = _ordinalCounter,
            Filter = _filter,
            DomainId = cfg.DomainId,
            PartitionName = cfg.PartitionName,
            ParticipantIndex = participantIndex,
            SenderRegistry = participant.SenderRegistry
        };

        var partition = string.IsNullOrEmpty(cfg.PartitionName) ? CurrentPartition : cfg.PartitionName;

        var readerType = typeof(DynamicReader<>).MakeGenericType(meta.TopicType);
        var instance = Activator.CreateInstance(readerType, participant, meta, partition, readerConfig);

        if (instance == null)
        {
            throw new InvalidOperationException($"Unable to create reader for '{meta.TopicType.Name}'.");
        }

        return (IDynamicReader)instance;
    }

    private IDynamicReader CreateAndWireReader(TopicMetadata meta, int participantIndex)
    {
        var reader = CreateReader(meta, participantIndex);
        reader.OnSampleReceived += sample =>
        {
            if (!IsPaused)
                _channelWriter.TryWrite(sample);
        };
        return reader;
    }

    private IDynamicWriter CreateWriter(TopicMetadata meta)
    {
        var writerType = typeof(DynamicWriter<>).MakeGenericType(meta.TopicType);
        var instance = Activator.CreateInstance(writerType, Participant, meta, CurrentPartition);

        if (instance == null)
        {
            throw new InvalidOperationException($"Unable to create writer for '{meta.TopicType.Name}'.");
        }

        return (IDynamicWriter)instance;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DdsBridge));
        }
    }

    private static bool TryGetDescriptorError(Exception exception, out string? message)
    {
        if (exception is TargetInvocationException invocationException && invocationException.InnerException != null)
        {
            return TryGetDescriptorError(invocationException.InnerException, out message);
        }

        if (exception is InvalidOperationException invalidOperationException)
        {
            message = invalidOperationException.Message;
            return true;
        }

        message = null;
        return false;
    }
}
