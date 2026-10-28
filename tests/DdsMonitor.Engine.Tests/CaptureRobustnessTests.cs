using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// One topic that cannot be read must not cost the whole capture: the failure is reported when it
/// happens, the reader stops cleanly, and a headless recording always ends as valid JSON.
/// </summary>
public sealed class CaptureRobustnessTests
{
    [Fact(Timeout = 30000)]
    public async Task DynamicReader_ReadFailure_IsReportedWhenItHappens_AndStopDoesNotThrow()
    {
        var metadata = new TopicMetadata(typeof(KeyedType));
        var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var config = new DynamicReaderConfig { OnReadError = (_, ex) => reported.TrySetResult(ex) };

        using var participant = new DdsParticipant();
        using var reader = new DynamicReader<KeyedType>(participant, metadata, null, config);
        using var writer = new DynamicWriter<KeyedType>(participant, metadata);
        reader.OnSampleReceived += _ => throw new InvalidOperationException("boom");
        reader.Start(null);
        await Task.Delay(300);

        writer.Write(new KeyedType { Id = 1 });

        var completed = await Task.WhenAny(reported.Task, Task.Delay(10000));
        Assert.True(ReferenceEquals(reported.Task, completed), "the read failure was not reported while the reader was running");
        Assert.Equal("boom", (await reported.Task).Message);
        Assert.Same(await reported.Task, reader.ReadError);

        // Before: Stop rethrew the faulted task's exception, which at shutdown killed ddsmonitor.
        reader.Stop();
    }

    [Fact(Timeout = 30000)]
    public async Task HeadlessRecord_StoppedByCtrlC_WritesAClosedJsonArray()
    {
        var path = Path.Combine(Path.GetTempPath(), "ddsmon-record-" + Guid.NewGuid().ToString("N") + ".json");
        var channel = Channel.CreateUnbounded<SampleData>();
        var settings = new DdsSettings
        {
            HeadlessMode = HeadlessMode.Record,
            RecordFormat = RecordFormat.Json,
            HeadlessFilePath = path
        };
        var metadata = new TopicMetadata(typeof(SumoLikeWireFrame));

        try
        {
            using var bridge = new DdsBridge(channel.Writer);
            using var provider = new ServiceCollection().BuildServiceProvider();
            var service = new HeadlessRunnerService(
                settings,
                new AppSettings(),
                new TopicRegistry(),
                bridge,
                provider.GetRequiredService<IServiceScopeFactory>(),
                new TestLifetime(),
                NullLogger<HeadlessRunnerService>.Instance,
                channel.Reader);

            await service.StartAsync(CancellationToken.None);
            for (int i = 0; i < 3; i++)
            {
                channel.Writer.TryWrite(new SampleData
                {
                    Ordinal = i + 1,
                    Payload = Activator.CreateInstance<SumoLikeWireFrame>(),
                    TopicMetadata = metadata,
                    Timestamp = DateTime.UtcNow
                });
            }
            await WaitUntil(() => File.Exists(path) && new FileInfo(path).Length > 0 && channel.Reader.Count == 0);

            // What Ctrl+C does: the host stops the service while the channel is still open.
            await service.StopAsync(CancellationToken.None);

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(3, doc.RootElement.GetArrayLength());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 100 && !condition(); i++)
            await Task.Delay(50);
        await Task.Delay(200);
    }

    private sealed class TestLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
}
