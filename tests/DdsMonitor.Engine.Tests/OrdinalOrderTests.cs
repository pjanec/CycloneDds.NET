using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;
using Xunit;

namespace DdsMonitor.Engine.Tests;

[DdsTopic("OrdinalOrderA")] public partial struct OrdinalOrderA { [DdsKey] public int Id; public int Seq; }
[DdsTopic("OrdinalOrderB")] public partial struct OrdinalOrderB { [DdsKey] public int Id; public int Seq; }
[DdsTopic("OrdinalOrderC")] public partial struct OrdinalOrderC { [DdsKey] public int Id; public int Seq; }
[DdsTopic("OrdinalOrderD")] public partial struct OrdinalOrderD { [DdsKey] public int Id; public int Seq; }

/// <summary>
/// Samples leave the bridge in ordinal order, even when several topics' reader threads deliver at once.
/// A capture reader that remembers the last ordinal it saw (<c>select(.Ordinal &gt; N)</c>) relies on it.
/// </summary>
public sealed class OrdinalOrderTests
{
    [Fact(Timeout = 60000)]
    public async Task ConcurrentTopics_ReachTheChannel_InOrdinalOrder()
    {
        const int perTopic = 3000;
        uint domain = (uint)Random.Shared.Next(100, 230);
        var channel = Channel.CreateUnbounded<SampleData>();
        using var bridge = new DdsBridge(channel.Writer, new[] { new ParticipantConfig { DomainId = domain } }, null, null, null, null);
        foreach (var type in new[] { typeof(OrdinalOrderA), typeof(OrdinalOrderB), typeof(OrdinalOrderC), typeof(OrdinalOrderD) })
            Assert.True(bridge.TrySubscribe(new TopicMetadata(type), out _, out _));

        using var participant = new DdsParticipant(domain);
        await Task.Delay(500);
        await Task.WhenAll(
            Task.Run(() => Publish<OrdinalOrderA>(participant, perTopic, (id, s) => new OrdinalOrderA { Id = id, Seq = s })),
            Task.Run(() => Publish<OrdinalOrderB>(participant, perTopic, (id, s) => new OrdinalOrderB { Id = id, Seq = s })),
            Task.Run(() => Publish<OrdinalOrderC>(participant, perTopic, (id, s) => new OrdinalOrderC { Id = id, Seq = s })),
            Task.Run(() => Publish<OrdinalOrderD>(participant, perTopic, (id, s) => new OrdinalOrderD { Id = id, Seq = s })));

        var ordinals = new List<long>();
        using var quiet = new CancellationTokenSource();
        while (true)
        {
            quiet.CancelAfter(TimeSpan.FromSeconds(1));
            try
            {
                ordinals.Add((await channel.Reader.ReadAsync(quiet.Token)).Ordinal);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            quiet.TryReset();
        }

        Assert.True(ordinals.Count > perTopic, $"only {ordinals.Count} samples arrived");
        var outOfOrder = ordinals.Zip(ordinals.Skip(1)).Count(p => p.Second <= p.First);
        Assert.True(outOfOrder == 0, $"{outOfOrder} of {ordinals.Count} samples arrived after a higher ordinal");
    }

    private static void Publish<T>(DdsParticipant participant, int count, Func<int, int, T> make) where T : struct
    {
        using var writer = new DdsWriter<T>(participant);
        for (int i = 0; i < count; i++)
            writer.Write(make(i, i));
    }
}
