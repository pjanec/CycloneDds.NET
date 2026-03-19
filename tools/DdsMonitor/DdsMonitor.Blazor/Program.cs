using System;
using System.Diagnostics;
using System.Linq;
using DdsMonitor.Components;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Hosting;
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
    builder.Services.AddSingleton(lifecycleOptions);

    builder.Services.AddSingleton<BrowserTrackingCircuitHandler>();
    builder.Services.AddSingleton<CircuitHandler>(sp =>
        sp.GetRequiredService<BrowserTrackingCircuitHandler>());
    builder.Services.AddHostedService<BrowserLifecycleService>();

    // ── ME1-T10: HTTP-only on a dynamic local port ────────────────────────────────────
    var port = GetFreePort();
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

    // Start the host (Kestrel begins listening).
    await app.StartAsync();

    // ── ME1-T10: Open the system browser ─────────────────────────────────────────────
    var serverAddresses = app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>();
    var url = serverAddresses?.Addresses.FirstOrDefault()
              ?? $"http://127.0.0.1:{GetBoundPort(app)}";

    try
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
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
