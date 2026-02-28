using System;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime;

namespace DdsMonitor.Engine;

/// <summary>
/// Generic DDS reader wrapper that publishes samples through a non-generic interface.
/// </summary>
public sealed class DynamicReader<T> : IDynamicReader
    where T : new()
{
    private const int DefaultMaxSamples = 32;
    private const int UnknownSizeBytes = 0;

    private readonly DdsParticipant _participant;
    private readonly string? _initialPartition;
    private readonly object _sync = new();
    private DdsReader<T>? _reader;
    private CancellationTokenSource? _cancellation;
    private Task? _readTask;
    private long _nextOrdinal;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicReader{T}"/> class.
    /// </summary>
    public DynamicReader(DdsParticipant participant, TopicMetadata topicMetadata, string? partition = null)
    {
        _participant = participant ?? throw new ArgumentNullException(nameof(participant));
        TopicMetadata = topicMetadata ?? throw new ArgumentNullException(nameof(topicMetadata));
        _initialPartition = partition;
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
            _readTask = Task.Run(() => ReadLoop(_reader, _cancellation.Token));
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

    private void ReadLoop(DdsReader<T> reader, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var hasData = reader.WaitDataAsync(cancellationToken).GetAwaiter().GetResult();
                if (!hasData)
                {
                    continue;
                }

                using var loan = reader.Take(DefaultMaxSamples);
                if (loan.Count == 0)
                {
                    continue;
                }

                foreach (var sample in loan)
                {
                    if (!sample.IsValid)
                    {
                        continue;
                    }

                    EmitSample(sample);
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

    private void EmitSample(DdsSample<T> sample)
    {
        var payload = sample.Data;
        var sampleData = new SampleData
        {
            Ordinal = Interlocked.Increment(ref _nextOrdinal),
            Payload = payload!,
            TopicMetadata = TopicMetadata,
            SampleInfo = sample.Info,
            Timestamp = DateTime.UtcNow,
            SizeBytes = UnknownSizeBytes
        };

        OnSampleReceived?.Invoke(sampleData);
    }
}
