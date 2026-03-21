using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace DdsMonitor.Engine;

/// <summary>
/// Background worker that ingests samples from a channel into the stores.
/// </summary>
public sealed class DdsIngestionService : BackgroundService
{
    private readonly ChannelReader<SampleData> _channelReader;
    private readonly ISampleStore _sampleStore;
    private readonly IInstanceStore _instanceStore;
    private readonly PerfCounters _perfCounters;

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsIngestionService"/> class.
    /// </summary>
    public DdsIngestionService(
        ChannelReader<SampleData> channelReader,
        ISampleStore sampleStore,
        IInstanceStore instanceStore,
        PerfCounters perfCounters)
    {
        _channelReader = channelReader ?? throw new ArgumentNullException(nameof(channelReader));
        _sampleStore = sampleStore ?? throw new ArgumentNullException(nameof(sampleStore));
        _instanceStore = instanceStore ?? throw new ArgumentNullException(nameof(instanceStore));
        _perfCounters = perfCounters ?? throw new ArgumentNullException(nameof(perfCounters));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // WaitToReadAsync yields to the thread pool only once per batch.
        // The inner TryRead loop processes all queued samples synchronously,
        // eliminating per-sample async state machine overhead under high load.
        while (await _channelReader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
        {
            while (_channelReader.TryRead(out var sample))
            {
                _sampleStore.Append(sample);
                _perfCounters.IncrementSamplesIngested(sample.SizeBytes);

                if (sample.TopicMetadata.IsKeyed)
                {
                    _instanceStore.ProcessSample(sample);
                    _perfCounters.IncrementInstanceStoreOps();
                }
            }
        }
    }
}
