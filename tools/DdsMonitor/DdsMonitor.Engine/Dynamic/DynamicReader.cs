using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime;

namespace DdsMonitor.Engine;

/// <summary>
/// Configuration record injected into <see cref="DynamicReader{T}"/> by the bridge so that
/// each reader can stamp its samples with the correct participant metadata and share a
/// single global ordinal counter across all participants.
/// </summary>
public sealed class DynamicReaderConfig
{
    /// <summary>Shared ordinal counter across all participants. When null the reader uses its own per-instance counter.</summary>
    public OrdinalCounter? OrdinalCounter { get; init; }

    /// <summary>Pre-compiled filter predicate. Null means accept all samples.</summary>
    public Func<SampleData, bool>? Filter { get; init; }

    /// <summary>DDS domain identifier of the owning participant.</summary>
    public uint DomainId { get; init; }

    /// <summary>Partition name the owning participant listens on.</summary>
    public string PartitionName { get; init; } = string.Empty;

    /// <summary>Zero-based index of the owning participant within <see cref="IDdsBridge.Participants"/>.</summary>
    public int ParticipantIndex { get; init; }
}

/// <summary>
/// Generic DDS reader wrapper that publishes samples through a non-generic interface.
/// </summary>
public sealed class DynamicReader<T> : IDynamicReader
    where T : new()
{
    private const int DefaultMaxSamples = 32;
    private const int UnknownSizeBytes = 0;

    private delegate int GetNativeSizeDelegate(in T sample);
    private static readonly GetNativeSizeDelegate? _nativeSizer;

    static DynamicReader()
    {
        var method = typeof(T).GetMethod(
            "GetNativeSize",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(T).MakeByRefType() },
            null);
        if (method != null)
        {
            _nativeSizer = (GetNativeSizeDelegate)Delegate.CreateDelegate(typeof(GetNativeSizeDelegate), method);
        }
    }

    private readonly DdsParticipant _participant;
    private readonly string? _initialPartition;
    private readonly DynamicReaderConfig? _config;
    private readonly object _sync = new();
    private DdsReader<T>? _reader;
    private CancellationTokenSource? _cancellation;
    private Task? _readTask;

    // Per-instance fallback ordinal used when no shared OrdinalCounter is provided.
    private long _nextOrdinal;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicReader{T}"/> class.
    /// </summary>
    public DynamicReader(DdsParticipant participant, TopicMetadata topicMetadata, string? partition = null)
        : this(participant, topicMetadata, partition, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicReader{T}"/> class with
    /// bridge-supplied configuration for ordinal sharing and participant stamping.
    /// </summary>
    public DynamicReader(DdsParticipant participant, TopicMetadata topicMetadata, string? partition, DynamicReaderConfig? config)
    {
        _participant = participant ?? throw new ArgumentNullException(nameof(participant));
        TopicMetadata = topicMetadata ?? throw new ArgumentNullException(nameof(topicMetadata));
        _initialPartition = partition;
        _config = config;
    }

    /// <inheritdoc />
    public Type TopicType => typeof(T);

    /// <inheritdoc />
    public TopicMetadata TopicMetadata { get; }

    /// <inheritdoc />
    public event Action<SampleData>? OnSampleReceived;

    /// <inheritdoc />
    public void Start(string? partition)
    {
        lock (_sync)
        {
            StopInternal();

            var activePartition = partition ?? _initialPartition;
            _reader = new DdsReader<T>(_participant, TopicMetadata.TopicName, partition: activePartition);
            _cancellation = new CancellationTokenSource();

            var reader = _reader;
            var token = _cancellation.Token;
            // explicitly push the task to a background thread so it never captures the UI context to begin with
            // otherwise the UI freezes when the topic subscription check box is clicked
            _readTask = Task.Run(() => ReadLoopAsync(reader, token));
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        lock (_sync)
        {
            StopInternal();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
    }

    private void StopInternal()
    {
        var reader = _reader;
        var cancellation = _cancellation;
        var task = _readTask;

        _reader = null;
        _cancellation = null;
        _readTask = null;

        if (cancellation != null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }

        if (task != null)
        {
            try
            {
                task.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
        }

        reader?.Dispose();
    }

    private async Task ReadLoopAsync(DdsReader<T> reader, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var hasData = await reader.WaitDataAsync(cancellationToken).ConfigureAwait(false);
                if (hasData)
                {
                    // Loop synchronously until the reader queue is completely empty,
                    // avoiding a redundant native peek (HasData/WaitDataAsync) between batches.
                    bool moreData = true;
                    while (moreData && !cancellationToken.IsCancellationRequested)
                    {
                        moreData = DrainReader(reader);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    // Synchronous helper: DdsLoan<T> is a ref struct and cannot be declared
    // inside an async method on C# < 13, so the Take/Emit work lives here.
    // Returns true if samples were taken (there may be more); false when the queue is empty.
    private bool DrainReader(DdsReader<T> reader)
    {
        using var loan = reader.Take(DefaultMaxSamples);
        if (loan.Count == 0)
        {
            return false; // Queue is empty.
        }

        foreach (var sample in loan)
        {
            EmitSample(sample);
        }

        return true; // Took a full batch; there may still be more.
    }

    private void EmitSample(DdsSample<T> sample)
    {
        var payload = sample.Data;
        var estimatedSize = _nativeSizer != null && payload != null ? _nativeSizer(payload) : UnknownSizeBytes;

        // ME1-T07: Build a temporary sample (ordinal = 0) for filter evaluation.
        // The ordinal counter is only incremented for samples that pass the filter,
        // ensuring that filtered-out samples consume no ordinal slots.
        var tempSample = new SampleData
        {
            Ordinal = 0,
            Payload = payload!,
            TopicMetadata = TopicMetadata,
            SampleInfo = sample.Info,
            Timestamp = DateTime.UtcNow,
            SizeBytes = estimatedSize,
            DomainId = _config?.DomainId ?? 0,
            PartitionName = _config?.PartitionName ?? string.Empty,
            ParticipantIndex = _config?.ParticipantIndex ?? 0
        };

        // Apply startup filter before allocating an ordinal.
        var filter = _config?.Filter;
        if (filter != null && !filter(tempSample))
        {
            return; // Reject without incrementing the ordinal.
        }

        // Allocate the ordinal: use the shared counter if available, otherwise per-reader.
        var ordinal = _config?.OrdinalCounter != null
            ? _config.OrdinalCounter.Increment()
            : Interlocked.Increment(ref _nextOrdinal);

        var sampleData = tempSample with { Ordinal = ordinal };

        OnSampleReceived?.Invoke(sampleData);
    }
}
