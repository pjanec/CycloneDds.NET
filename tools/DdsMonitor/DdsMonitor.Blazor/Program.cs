using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using DdsMonitor.Components;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Hosting;
using DdsMonitor.Engine.Plugins;
using DdsMonitor.Engine.Recording;
using DdsMonitor.Engine.Ui;
using DdsMonitor.Services;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

if (CommandLineHelp.IsHelpRequest(args) || CommandLineHelp.IsVersionRequest(args))
{
    var version = typeof(HeadlessRunnerService).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
    Console.WriteLine(CommandLineHelp.IsHelpRequest(args) ? CommandLineHelp.Text(version) : version);
    return;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// ── Read DdsSettings early so we can branch registration based on HeadlessMode ──────
var headlessModeStr = builder.Configuration.GetSection("DdsSettings")["HeadlessMode"] ?? "None";
var isHeadless = !string.Equals(headlessModeStr, "None", StringComparison.OrdinalIgnoreCase);

// Recording to stdout (HeadlessFilePath=-): stdout carries the capture only, so every log line goes to stderr.
var captureToStdout = isHeadless
    && builder.Configuration.GetSection("DdsSettings")["HeadlessFilePath"] == HeadlessRecorder.StdoutPath;
if (captureToStdout)
    builder.Services.Configure<ConsoleLoggerOptions>(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// A quiet recording (the default RecordConsole) logs warnings and errors only, from every logger,
// so the host's startup and shutdown chatter stays out too. --Logging:LogLevel:Default=Information
// brings it back.
var isRecord = string.Equals(headlessModeStr, nameof(HeadlessMode.Record), StringComparison.OrdinalIgnoreCase);
var recordConsole = Enum.TryParse<RecordConsoleOutput>(
    builder.Configuration.GetSection("DdsSettings")["RecordConsole"], ignoreCase: true, out var parsedConsole)
    ? parsedConsole
    : new DdsSettings().RecordConsole;
if (isRecord && recordConsole == RecordConsoleOutput.Quiet)
    builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Bootstrap logger factory: used by PluginConfigService during startup (before the host
// DI container is built) so that corrupt enabled-plugins.json emits a Warning to the
// console rather than silently falling through.  The factory is disposed after
// AddDdsMonitorServices returns because PluginConfigService only reads the file during
// its constructor; no logging is needed after that startup phase.
using var startupLoggers = LoggerFactory.Create(b =>
    b.SetMinimumLevel(LogLevel.Warning).AddConsole(o =>
    {
        if (captureToStdout) o.LogToStandardErrorThreshold = LogLevel.Trace;
    }));
builder.Services.AddDdsMonitorServices(builder.Configuration, startupLoggers);
// DevelSettings is registered inside AddDdsMonitorServices

if (!isHeadless)
{
    // ── Register Blazor + lifecycle services (UI mode only) ──────────────────────────
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddSingleton<TooltipService>();
    builder.Services.AddSingleton<ContextMenuService>();
    // TopicColorService is registered as singleton in AddDdsMonitorServices (DEBT-018).
    builder.Services.AddSingleton<CloneRequestService>();
    builder.Services.AddScoped<WorkspacePersistenceService>();

    // ── ME1-T10: Browser lifecycle services ──────────────────────────────────────────
    var lifecycleOptions = builder.Configuration
        .GetSection("BrowserLifecycle")
        .Get<BrowserLifecycleOptions>() ?? new BrowserLifecycleOptions();

    // When NoBrowser is enabled the app should keep running even if the browser
    // disconnects or is never opened – do not apply connect/disconnect timeouts.
    {
        var noBrowserViaAppSettings = builder.Configuration.GetSection(AppSettings.SectionName)
            .GetValue<bool>("NoBrowser");
        var noBrowserViaTopLevel = builder.Configuration.GetValue<bool>("NoBrowser");
        if (noBrowserViaAppSettings || noBrowserViaTopLevel)
            lifecycleOptions.KeepAlive = true;
    }

    builder.Services.AddSingleton(lifecycleOptions);

    builder.Services.AddSingleton<BrowserTrackingCircuitHandler>();
    builder.Services.AddSingleton<CircuitHandler>(sp =>
        sp.GetRequiredService<BrowserTrackingCircuitHandler>());
    builder.Services.AddHostedService<BrowserLifecycleService>();

    // ── ME1-T10: HTTP-only on a configured or dynamic local port ─────────────────────
    var portStr = builder.Configuration["BrowserPort"];
    var port = int.TryParse(portStr, out var specifiedPort) && specifiedPort is > 0 and <= 65535
        ? specifiedPort
        : GetFreePort();
    builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
}
else
{
    // Headless mode serves nothing, but the web host still starts Kestrel, which would
    // otherwise fall back to its default http://localhost:5000 — so a second headless
    // instance (or anything else on 5000) made the host fail to start. Port 0 lets the
    // OS pick a free one.
    builder.WebHost.UseUrls("http://127.0.0.1:0");
}

var app = builder.Build();

if (!isHeadless)
{
    // ── Normal interactive UI mode ────────────────────────────────────────────────────
    app.UseStaticFiles();
    app.UseAntiforgery();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // Pre-apply workspace and CLI exclusions BEFORE hosted services start.
    // This ensures SelfSendService (and any other background service that calls
    // IDdsBridge.Subscribe) respects the saved and/or CLI-driven exclusion set even
    // when DdsSettings.SelfSendEnabled=true causes an immediate subscription attempt.
    {
        var bridge = app.Services.GetRequiredService<IDdsBridge>();
        var registry = app.Services.GetRequiredService<ITopicRegistry>();
        var appSettingsPre = app.Services.GetRequiredService<AppSettings>();
        var wsFilePath = new WorkspaceState(appSettingsPre).WorkspaceFilePath;
        TopicSubscriptionBootstrapper.Apply(bridge, registry, appSettingsPre, wsFilePath);
    }

    // Start the host (Kestrel begins listening).
    await app.StartAsync();

    // ── Phase 5: Initialise plugins after the host has started ───────────────────────
    var pluginLoader = app.Services.GetService<PluginLoader>();
    if (pluginLoader != null)
    {
        var monitorContext = app.Services.GetRequiredService<IMonitorContext>();
        pluginLoader.InitializePlugins(monitorContext);
    }

    // ── ME1-T10: Open the system browser ─────────────────────────────────────────────
    var serverAddresses = app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>();
    var url = serverAddresses?.Addresses.FirstOrDefault()
              ?? $"http://127.0.0.1:{GetBoundPort(app)}";

    try
    {
        var appSettings = app.Services.GetService<AppSettings>();

        // Also allow a top-level CLI flag `--NoBrowser true` for backward compatibility.
        var configNoBrowser = false;
        try
        {
            var raw = builder.Configuration["NoBrowser"];
            if (!string.IsNullOrWhiteSpace(raw))
                configNoBrowser = bool.TryParse(raw, out var b) && b;
        }
        catch { }

        if ((appSettings == null || !appSettings.NoBrowser) && !configNoBrowser)
        {
            // Launch Chrome/Edge in app mode with an isolated profile so the app opens
            // in its own window rather than a tab in an existing browser instance.
            // Use a per-user location (%LOCALAPPDATA%\DdsMonitor\spa-profile) so that
            // each user gets their own browser profile instead of sharing the system temp folder.
            var spaProfileDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DdsMonitor", "spa-profile");
            var browserArgs = $"--app=\"{url}\" --user-data-dir=\"{spaProfileDir}\"";
            bool browserOpened = false;

            try
            {
                Process.Start(new ProcessStartInfo("chrome", browserArgs) { UseShellExecute = true });
                browserOpened = true;
            }
            catch { /* Chrome not found or failed to start */ }

            if (!browserOpened)
            {
                try
                {
                    Process.Start(new ProcessStartInfo("msedge", browserArgs) { UseShellExecute = true });
                    browserOpened = true;
                }
                catch { /* Edge not found or failed to start */ }
            }

            if (!browserOpened)
            {
                // Fallback: open with the default system browser (no app-mode flags).
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[ddsmon] Could not open browser: {ex.Message}");
    }

    // Wait until the host shuts down (driven by BrowserLifecycleService or Ctrl+C).
    await app.WaitForShutdownAsync();
}
else
{
    // ── Headless mode: start hosted services only; no HTTP listener ───────────────────
    // Resolved up front: RunAsync disposes the service provider on its way out.
    var runner = app.Services.GetServices<IHostedService>().OfType<HeadlessRunnerService>().FirstOrDefault();

    await app.RunAsync();

    // 0 = success, 2 = the run failed, 3 = a recording lost at least one topic (see the log / summary file).
    if (runner != null)
        Environment.ExitCode = runner.ExitCode;
}

// ── Helpers ───────────────────────────────────────────────────────────────────────────

static int GetFreePort()
{
    using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
    listener.Start();
    var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static int GetBoundPort(WebApplication application)
{
    try
    {
        var feature = application.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>();
        var addr = feature?.Addresses.FirstOrDefault();
        if (addr != null && Uri.TryCreate(addr, UriKind.Absolute, out var uri))
            return uri.Port;
    }
    catch { }
    return 0;
}
