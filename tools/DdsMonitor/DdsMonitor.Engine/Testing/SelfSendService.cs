using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace DdsMonitor.Engine;

public sealed class SelfSendService : BackgroundService
{
    private const int MinimumRateHz = 1;
    private const int SamplesPerPayload = 5;

    private readonly IDdsBridge _bridge;
    private readonly ITopicRegistry _topicRegistry;
    private readonly DdsSettings _settings;
    private readonly Random _random = new();

    public SelfSendService(
        IDdsBridge bridge,
        ITopicRegistry topicRegistry,
        DdsSettings settings)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
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

        // Subscribe first so DynamicReaders exist and are wired to the ingestion channel.
        foreach (var topic in topics)
        {
            _bridge.Subscribe(topic);
        }

        // Create DDS writers – samples flow through the real DDS middleware
        // and are received by the DynamicReaders created above.
        var writers = new List<IDynamicWriter>(topics.Count);
        foreach (var topic in topics)
        {
            writers.Add(_bridge.GetWriter(topic));
        }

        var rateHz = Math.Max(MinimumRateHz, _settings.SelfSendRateHz);
        var delay = TimeSpan.FromMilliseconds(1000d / rateHz);
        var keyCount = Math.Max(1, _settings.SelfSendKeyCount);
        var keyIndex = 0;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                for (var i = 0; i < topics.Count; i++)
                {
                    var payload = CreatePayload(topics[i].TopicType, keyIndex);
                    writers[i].Write(payload);
                }

                keyIndex = (keyIndex + 1) % keyCount;
                await Task.Delay(delay, stoppingToken);
            }
        }
        finally
        {
            foreach (var writer in writers)
            {
                writer.Dispose();
            }
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
        var samples = new System.Collections.Generic.List<float>(SamplesPerPayload);
        var angle = _random.NextDouble() * Math.PI * 2;
        var baseZ = (float)(_random.NextDouble() * 10);
        for (var i = 0; i < SamplesPerPayload; i++)
        {
            var offset = Math.Sin(angle + (i * 0.5));
            samples.Add(baseZ + (float)offset);
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
}
