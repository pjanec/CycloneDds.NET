using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Linq;
using DdsMonitor.Engine.AssemblyScanner;
using DdsMonitor.Engine.Recording;
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
    /// <summary>Exit code when the run itself failed (bad arguments, unexpected error).</summary>
    public const int ExitCodeError = 2;

    /// <summary>Exit code of a recording during which at least one topic could not be read.</summary>
    public const int ExitCodeTopicFailed = 3;

    private readonly DdsSettings _settings;
    private readonly AppSettings _appSettings;
    private readonly ITopicRegistry _topicRegistry;
    private readonly IDdsBridge _ddsBridge;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<HeadlessRunnerService> _logger;
    private readonly ChannelReader<SampleData>? _channelReader;
    private readonly IAssemblySourceService? _assemblySources;

    public HeadlessRunnerService(
        DdsSettings settings,
        AppSettings appSettings,
        ITopicRegistry topicRegistry,
        IDdsBridge ddsBridge,
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        ILogger<HeadlessRunnerService> logger,
        ChannelReader<SampleData>? channelReader = null,
        IAssemblySourceService? assemblySources = null)
    {
        _assemblySources = assemblySources;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _topicRegistry = topicRegistry ?? throw new ArgumentNullException(nameof(topicRegistry));
        _ddsBridge = ddsBridge ?? throw new ArgumentNullException(nameof(ddsBridge));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _channelReader = channelReader;
    }

    /// <summary>
    /// The process exit code the headless run asks for: 0 on success, <see cref="ExitCodeError"/>
    /// or <see cref="ExitCodeTopicFailed"/>. Applied by the host program after shutdown.
    /// </summary>
    public int ExitCode { get; private set; }

    /// <summary>The outcome of the last recording, or null if none ran.</summary>
    public RecordResult? RecordResult { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_settings.HeadlessMode == HeadlessMode.None)
            return;

        // Apply saved/CLI topic exclusions and subscribe to (non-excluded) topics.
        // We apply this read-only: headless mode never writes back to the workspace file.
        using (var scope = _scopeFactory.CreateScope())
        {
            var workspaceState = scope.ServiceProvider.GetService<IWorkspaceState>();
            TopicSubscriptionBootstrapper.Apply(
                _ddsBridge,
                _topicRegistry,
                _appSettings,
                workspaceState?.WorkspaceFilePath);
        }

        // A topic source that yields nothing usually means a wrong path: say so, since nothing
        // of those topics would be recorded.
        foreach (var source in _assemblySources?.Entries ?? Array.Empty<AssemblySourceEntry>())
        {
            if (source.LoadError != null)
                _logger.LogWarning("Topic source {Path} could not be loaded: {Error}", source.Path, source.LoadError);
            else if (source.TopicCount == 0)
                _logger.LogWarning("Topic source {Path} contains no [DdsTopic] types (missing, or not a build output with topic types).", source.Path);
        }

        foreach (var topic in _topicRegistry.AllTopics)
        {
            if (!_ddsBridge.ExplicitlyUnsubscribedTopicTypes.Contains(topic.TopicType))
                _ddsBridge.TrySubscribe(topic, out _, out _);
        }

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
            ExitCode = ExitCodeError;
        }
        finally
        {
            // Stop the host only once it has finished starting: a recording can end during start-up
            // (a sample cap met by durable history the moment the reader matches), and stopping
            // then cancels the start-up, which the host reports as an unhandled exception.
            // A registration on an already-started host runs at once.
            _lifetime.ApplicationStarted.Register(_lifetime.StopApplication);
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
            ExitCode = ExitCodeError;
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.HeadlessFilePath))
        {
            _logger.LogError("HeadlessRunnerService: HeadlessFilePath must be set for Record mode.");
            ExitCode = ExitCodeError;
            return;
        }

        var subscribed = _topicRegistry.AllTopics
            .Where(t => !_ddsBridge.ExplicitlyUnsubscribedTopicTypes.Contains(t.TopicType))
            .ToList();
        var recorder = new HeadlessRecorder(_settings, _ddsBridge, subscribed, _channelReader, _logger);
        var result = await recorder.RunAsync(stoppingToken);
        RecordResult = result;
        if (result.FailedTopics > 0)
            ExitCode = ExitCodeTopicFailed;
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
            ExitCode = ExitCodeError;
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
}
