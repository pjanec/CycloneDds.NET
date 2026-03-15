using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DdsMonitor.Engine.Json;
using DdsMonitor.Engine.Replay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DdsMonitor.Engine;

/// <summary>
/// Hosted service that implements headless record and replay modes (ME1-T11).
///
/// <list type="bullet">
///   <item><description>
///     <c>HeadlessMode.None</c> — returns immediately; the Blazor UI drives execution instead.
///   </description></item>
///   <item><description>
///     <c>HeadlessMode.Record</c> — consumes <see cref="ChannelReader{SampleData}"/> and
///     streams incoming samples as a JSON array to <see cref="DdsSettings.HeadlessFilePath"/>
///     until cancellation (Ctrl+C).
///   </description></item>
///   <item><description>
///     <c>HeadlessMode.Replay</c> — loads a JSON file via <see cref="IReplayEngine"/>,
///     applies the optional <see cref="DdsSettings.FilterExpression"/>, replays at
///     <see cref="DdsSettings.ReplayRate"/> speed to the live DDS network, then exits.
///   </description></item>
/// </list>
/// </summary>
public sealed class HeadlessRunnerService : BackgroundService
{
    private const int FileBufferSize = 65_536;
    private const int FlushEveryN = 200;

    private readonly DdsSettings _settings;
    private readonly ChannelReader<SampleData>? _channelReader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<HeadlessRunnerService> _logger;

    public HeadlessRunnerService(
        DdsSettings settings,
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        ILogger<HeadlessRunnerService> logger,
        ChannelReader<SampleData>? channelReader = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _channelReader = channelReader;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_settings.HeadlessMode == HeadlessMode.None)
            return;

        try
        {
            switch (_settings.HeadlessMode)
            {
                case HeadlessMode.Record:
                    await RunRecordAsync(stoppingToken);
                    break;

                case HeadlessMode.Replay:
                    await RunReplayAsync(stoppingToken);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Headless runner encountered an unexpected error.");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Record mode
    // ─────────────────────────────────────────────────────────────────────────

    private async Task RunRecordAsync(CancellationToken stoppingToken)
    {
        if (_channelReader == null)
        {
            _logger.LogError("HeadlessRunnerService: Record mode requires a ChannelReader<SampleData>.");
            return;
        }

        var filePath = _settings.HeadlessFilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogError("HeadlessRunnerService: HeadlessFilePath must be set for Record mode.");
            return;
        }

        _logger.LogInformation("Headless Record → {FilePath}", filePath);

        await using var fileStream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            FileBufferSize,
            useAsync: true);

        await using var jsonWriter = new Utf8JsonWriter(fileStream, new JsonWriterOptions { Indented = true });

        jsonWriter.WriteStartArray();

        long count = 0;

        await foreach (var sample in _channelReader.ReadAllAsync(stoppingToken))
        {
            WriteRecord(jsonWriter, sample);
            count++;

            Console.WriteLine($"[RECORD] Ordinal={sample.Ordinal} Topic={sample.TopicMetadata.ShortName}");

            if (count % FlushEveryN == 0)
                await jsonWriter.FlushAsync(stoppingToken);
        }

        jsonWriter.WriteEndArray();
        await jsonWriter.FlushAsync(CancellationToken.None);

        _logger.LogInformation("Headless Record complete – {Count} samples written to {FilePath}.", count, filePath);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Replay mode
    // ─────────────────────────────────────────────────────────────────────────

    private async Task RunReplayAsync(CancellationToken stoppingToken)
    {
        var filePath = _settings.HeadlessFilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogError("HeadlessRunnerService: HeadlessFilePath must be set for Replay mode.");
            return;
        }

        _logger.LogInformation("Headless Replay ← {FilePath}", filePath);

        // IReplayEngine is scoped; create a scope for the duration of the replay.
        using var scope = _scopeFactory.CreateScope();
        var replayEngine = scope.ServiceProvider.GetRequiredService<IReplayEngine>();
        var filterCompiler = scope.ServiceProvider.GetService<IFilterCompiler>();

        await replayEngine.LoadAsync(filePath, stoppingToken);

        // Apply optional filter expression.
        if (!string.IsNullOrWhiteSpace(_settings.FilterExpression) && filterCompiler != null)
        {
            var result = filterCompiler.Compile(_settings.FilterExpression, null);
            if (result.IsValid && result.Predicate != null)
            {
                replayEngine.SetFilter(result.Predicate);
                _logger.LogInformation("Replay filter applied: {Expr}", _settings.FilterExpression);
            }
            else
            {
                _logger.LogWarning("Replay filter expression is invalid: {Expr}", _settings.FilterExpression);
            }
        }

        replayEngine.SpeedMultiplier = _settings.ReplayRate > 0 ? _settings.ReplayRate : 1.0;

        _logger.LogInformation("Replaying {Count} samples at {Speed}× speed.",
            replayEngine.FilteredTotalCount, replayEngine.SpeedMultiplier);

        // Play to the live DDS network.
        replayEngine.Play(ReplayTarget.DdsNetwork);

        // Poll until playback is done or cancellation is requested.
        while (!stoppingToken.IsCancellationRequested)
        {
            if (replayEngine.Status != ReplayStatus.Playing)
                break;

            await Task.Delay(50, stoppingToken);

            Console.WriteLine($"[REPLAY] {replayEngine.CurrentIndex}/{replayEngine.FilteredTotalCount}");
        }

        _logger.LogInformation("Headless Replay complete.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // JSON writer helpers (same schema as ExportService)
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions PayloadOptions = DdsJsonOptions.Export;

    private static void WriteRecord(Utf8JsonWriter writer, SampleData sample)
    {
        writer.WriteStartObject();

        writer.WriteNumber("Ordinal", sample.Ordinal);
        writer.WriteString("TopicTypeName", sample.TopicMetadata.TopicType.FullName);
        writer.WriteString("Timestamp", sample.Timestamp.ToUniversalTime());
        writer.WriteNumber("DomainId", sample.DomainId);
        writer.WriteString("PartitionName", sample.PartitionName);

        if (sample.Sender != null)
        {
            writer.WriteStartObject("Sender");
            writer.WriteNumber("ProcessId", sample.Sender.ProcessId);
            if (sample.Sender.MachineName != null)
                writer.WriteString("MachineName", sample.Sender.MachineName);
            if (sample.Sender.IpAddress != null)
                writer.WriteString("IpAddress", sample.Sender.IpAddress);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull("Sender");
        }

        writer.WriteString("InstanceState", sample.SampleInfo.InstanceState.ToString());

        writer.WritePropertyName("Payload");
        var payloadElement = JsonSerializer.SerializeToElement(
            sample.Payload,
            sample.Payload.GetType(),
            PayloadOptions);
        payloadElement.WriteTo(writer);

        writer.WriteEndObject();
    }
}
