using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Interop;
using Microsoft.Extensions.Hosting;

namespace DdsMonitor.Engine;

public sealed class SelfSendService : BackgroundService
{
    private const int MinimumRateHz = 1;
    private const int SamplesPerPayload = 5;

    private readonly ChannelWriter<SampleData> _writer;
    private readonly ITopicRegistry _topicRegistry;
    private readonly DdsSettings _settings;
    private readonly Random _random = new();
    private long _nextOrdinal;

    public SelfSendService(
        ChannelWriter<SampleData> writer,
        ITopicRegistry topicRegistry,
        DdsSettings settings)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _topicRegistry = topicRegistry ?? throw new ArgumentNullException(nameof(topicRegistry));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.SelfSendEnabled)
        {
            return;
        }

        var topics = GetSelfSendTopics();
        if (topics.Count == 0)
        {
            return;
        }

        var rateHz = Math.Max(MinimumRateHz, _settings.SelfSendRateHz);
        var delay = TimeSpan.FromMilliseconds(1000d / rateHz);
        var keyCount = Math.Max(1, _settings.SelfSendKeyCount);
        var keyIndex = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var topic in topics)
            {
                var payload = CreatePayload(topic.TopicType, keyIndex);
                var sample = CreateSample(topic, payload);
                await _writer.WriteAsync(sample, stoppingToken);
            }

            keyIndex = (keyIndex + 1) % keyCount;
            await Task.Delay(delay, stoppingToken);
        }
    }

    private IReadOnlyList<TopicMetadata> GetSelfSendTopics()
    {
        var topics = new List<TopicMetadata>();

        foreach (var topicType in SelfSendTopics.TopicTypes)
        {
            var metadata = _topicRegistry.GetByType(topicType);
            if (metadata != null)
            {
                topics.Add(metadata);
            }
        }

        return topics;
    }

    private object CreatePayload(Type topicType, int keyIndex)
    {
        if (topicType == typeof(SelfTestSimple))
        {
            return CreateSimplePayload(keyIndex);
        }

        if (topicType == typeof(SelfTestPose))
        {
            return CreatePosePayload(keyIndex);
        }

        throw new InvalidOperationException($"Unsupported self-send topic type '{topicType.Name}'.");
    }

    private SelfTestSimple CreateSimplePayload(int keyIndex)
    {
        return new SelfTestSimple
        {
            Id = keyIndex + 1,
            Message = $"Self-send message {keyIndex + 1}",
            Value = Math.Round(_random.NextDouble() * 100, 2),
            Timestamp = DateTime.UtcNow
        };
    }

    private SelfTestPose CreatePosePayload(int keyIndex)
    {
        var samples = new float[SamplesPerPayload];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(_random.NextDouble() * 10);
        }

        return new SelfTestPose
        {
            Id = keyIndex + 1,
            Pose = new Pose
            {
                Position = new Vector3
                {
                    X = (float)(_random.NextDouble() * 100),
                    Y = (float)(_random.NextDouble() * 100),
                    Z = (float)(_random.NextDouble() * 100)
                },
                Velocity = new Vector3
                {
                    X = (float)(_random.NextDouble() * 5),
                    Y = (float)(_random.NextDouble() * 5),
                    Z = (float)(_random.NextDouble() * 5)
                }
            },
            Samples = samples,
            Level = (StatusLevel)(_random.Next(0, 3))
        };
    }

    private SampleData CreateSample(TopicMetadata metadata, object payload)
    {
        var now = DateTime.UtcNow;
        var info = new DdsApi.DdsSampleInfo
        {
            SampleState = DdsSampleState.NotRead,
            ViewState = DdsViewState.New,
            InstanceState = DdsInstanceState.Alive,
            ValidData = 1,
            SourceTimestamp = now.Ticks
        };

        return new SampleData
        {
            Ordinal = Interlocked.Increment(ref _nextOrdinal),
            Payload = payload,
            TopicMetadata = metadata,
            SampleInfo = info,
            Timestamp = now,
            SizeBytes = 0,
            Sender = new SenderIdentity
            {
                ProcessId = (uint)Environment.ProcessId,
                MachineName = Environment.MachineName
            }
        };
    }
}
