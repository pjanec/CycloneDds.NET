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

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsIngestionService"/> class.
    /// </summary>
    public DdsIngestionService(
        ChannelReader<SampleData> channelReader,
        ISampleStore sampleStore,
        IInstanceStore instanceStore)
    {
        _channelReader = channelReader ?? throw new ArgumentNullException(nameof(channelReader));
        _sampleStore = sampleStore ?? throw new ArgumentNullException(nameof(sampleStore));
        _instanceStore = instanceStore ?? throw new ArgumentNullException(nameof(instanceStore));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var sample in _channelReader.ReadAllAsync(stoppingToken))
        {
            _sampleStore.Append(sample);

            if (sample.TopicMetadata.IsKeyed)
            {
                _instanceStore.ProcessSample(sample);
            }
        }
    }
}
