using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace DdsMonitor.Engine;

public sealed class SelfSendService : BackgroundService
{
    private const int MinimumRateHz = 1;
    private const int MaxTickRateHz = 100;  // Windows timer resolution limit (~15ms); cap tick rate, increase batch size instead
    private const int SamplesPerPayload = 5;
    private const int IdleCheckMs = 500;

    private readonly IDdsBridge _bridge;
    private readonly ITopicRegistry _topicRegistry;
    private readonly DdsSettings _settings;
    private readonly DevelSettings _develSettings;
    private readonly Random _random = new();

    public SelfSendService(
        IDdsBridge bridge,
        ITopicRegistry topicRegistry,
        DdsSettings settings,
        DevelSettings develSettings)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _topicRegistry = topicRegistry ?? throw new ArgumentNullException(nameof(topicRegistry));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _develSettings = develSettings ?? throw new ArgumentNullException(nameof(develSettings));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<IDynamicWriter>? writers = null;
        bool wasEnabled = false;

        // Register self-send topic metadata unconditionally so they appear in
        // the Send Sample combo and topic explorer immediately on startup,
        // even before the user enables self-sending.
        SelfSendTopics.Register(_topicRegistry);

        var keyCount = Math.Max(1, _settings.SelfSendKeyCount);
        var keyIndex = 0;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var isEnabled = _develSettings.SelfSendEnabled;

                if (isEnabled && !wasEnabled)
                {
                    // Turning on: subscribe to DDS and create writers.
                    var topics = GetSelfSendTopics();
                    if (topics.Count > 0)
                    {
                        foreach (var topic in topics)
                        {
                            _bridge.Subscribe(topic);
                        }

                        writers = new List<IDynamicWriter>(topics.Count);
                        foreach (var topic in topics)
                        {
                            writers.Add(_bridge.GetWriter(topic));
                        }
                    }
                }
                else if (!isEnabled && wasEnabled)
                {
                    // Turning off: dispose writers.
                    DisposeWriters(ref writers);
                }

                wasEnabled = isEnabled;

                if (isEnabled && writers != null && writers.Count > 0)
                {
                    // Compute current rate from DevelSettings (live-adjustable).
                    var rateHz = Math.Max(MinimumRateHz, _develSettings.SelfSendRateHz);

                    // For high rates, keep tick rate at ≤100 Hz and batch multiple samples per tick.
                    // tickRateHz = min(rateHz, 100),  batchSize = rateHz / tickRateHz
                    var tickRateHz = Math.Min(rateHz, MaxTickRateHz);
                    var batchSize = Math.Max(1, rateHz / tickRateHz);
                    var delayMs = 1000 / tickRateHz;

                    var topics = GetSelfSendTopics();
                    for (var b = 0; b < batchSize; b++)
                    {
                        for (var i = 0; i < Math.Min(topics.Count, writers.Count); i++)
                        {
                            var payload = CreatePayload(topics[i].TopicType, keyIndex);
                            writers[i].Write(payload);
                        }

                        keyIndex = (keyIndex + 1) % keyCount;
                    }

                    await Task.Delay(delayMs, stoppingToken);
                }
                else
                {
                    await Task.Delay(IdleCheckMs, stoppingToken);
                }
            }
        }
        finally
        {
            DisposeWriters(ref writers);
        }
    }

    private static void DisposeWriters(ref List<IDynamicWriter>? writers)
    {
        if (writers == null)
        {
            return;
        }

        foreach (var writer in writers)
        {
            writer.Dispose();
        }

        writers = null;
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
