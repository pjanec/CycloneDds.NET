using System;
using System.Diagnostics;
using System.Linq;
using DdsMonitor.Components;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Hosting;
using DdsMonitor.Engine.Plugins;
using DdsMonitor.Engine.Ui;
using DdsMonitor.Services;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// ── Read DdsSettings early so we can branch registration based on HeadlessMode ──────
var headlessModeStr = builder.Configuration.GetSection("DdsSettings")["HeadlessMode"] ?? "None";
var isHeadless = !string.Equals(headlessModeStr, "None", StringComparison.OrdinalIgnoreCase);

builder.Services.AddDdsMonitorServices(builder.Configuration);
// DevelSettings is registered inside AddDdsMonitorServices

if (!isHeadless)
{
    // ── Register Blazor + lifecycle services (UI mode only) ──────────────────────────
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddSingleton<ITypeDrawerRegistry, TypeDrawerRegistry>();
    builder.Services.AddSingleton<IValueFormatterRegistry, ValueFormatterRegistry>();
    builder.Services.AddSingleton<TooltipService>();
    builder.Services.AddSingleton<ContextMenuService>();
    builder.Services.AddScoped<DdsMonitor.Engine.TopicColorService>();
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
            var userDataDir = Path.Combine(Path.GetTempPath(), "ddsmon-spa-profile");
            var browserArgs = $"--app=\"{url}\" --user-data-dir=\"{userDataDir}\"";
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
    await app.RunAsync();
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
