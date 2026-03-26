using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Export;
using DdsMonitor.Engine.Plugins;
using DdsMonitor.Engine.Ui;
using DdsMonitor.Plugins.FeatureDemo.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DdsMonitor.Plugins.FeatureDemo;

/// <summary>
/// "Kitchen Sink" demo plugin that exercises every extension point defined by the
/// DdsMonitor Plugin API (Design §10).  Each <see cref="IMonitorContext.GetFeature{TFeature}"/>
/// call is guarded with a null check so the plugin degrades gracefully when loaded into
/// a host that only provides a subset of features.
/// </summary>
public sealed class FeatureDemoPlugin : IMonitorPlugin
{
    /// <inheritdoc />
    public string Name => "Feature Demo";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Register the background sample counter as both a singleton (so the
        // DemoDashboardPanel can inject it) and as a hosted service so the host
        // starts it automatically on application startup.
        services.AddSingleton<DemoBackgroundProcessor>();
        services.AddHostedService(sp => sp.GetRequiredService<DemoBackgroundProcessor>());
    }

    /// <inheritdoc />
    public void Initialize(IMonitorContext context)
    {
        // ── IMenuRegistry: add a "Plugins/Demo → Show Dashboard" item ────────────
        context.GetFeature<IMenuRegistry>()?.AddMenuItem(
            "Plugins/Demo",
            "Show Dashboard",
            () => { /* Panel is spawned by the host shell via PanelRegistry */ });

        // ── PluginPanelRegistry: register DemoDashboardPanel ─────────────────────
        context.GetFeature<PluginPanelRegistry>()?.RegisterPanelType(
            "Demo Dashboard",
            typeof(DemoDashboardPanel));

        // ── IContextMenuRegistry: inject "Log to Console" on SampleData rows ─────
        context.GetFeature<IContextMenuRegistry>()?.RegisterProvider<SampleData>(
            sample => new[]
            {
                new ContextMenuItem(
                    "Log to Console (Demo)",
                    null,
                    () => { Console.WriteLine($"[FeatureDemo] Sample #{sample.Ordinal}: {sample.Payload}"); return Task.CompletedTask; })
            });

        // ── ISampleViewRegistry: replace tree view for DemoPayload with custom panel ─
        context.GetFeature<ISampleViewRegistry>()?.Register(
            typeof(DemoPayload),
            sd => builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "demo-payload-view");
                builder.OpenElement(2, "h4");
                builder.AddContent(3, "Demo Payload Viewer");
                builder.CloseElement();
                if (sd.Payload is DemoPayload dp)
                {
                    builder.OpenElement(4, "dl");
                    builder.OpenElement(5, "dt");
                    builder.AddContent(6, "ID");
                    builder.CloseElement();
                    builder.OpenElement(7, "dd");
                    builder.AddContent(8, dp.Id.ToString());
                    builder.CloseElement();
                    builder.OpenElement(9, "dt");
                    builder.AddContent(10, "Label");
                    builder.CloseElement();
                    builder.OpenElement(11, "dd");
                    builder.AddContent(12, dp.Label);
                    builder.CloseElement();
                    builder.OpenElement(13, "dt");
                    builder.AddContent(14, "Location");
                    builder.CloseElement();
                    builder.OpenElement(15, "dd");
                    builder.AddContent(16, dp.Location.ToString());
                    builder.CloseElement();
                    builder.CloseElement(); // dl
                }
                builder.CloseElement(); // div
            });

        // ── IValueFormatterRegistry: register geo-coordinate formatter ────────────
        context.GetFeature<IValueFormatterRegistry>()?.Register(new DemoGeoFormatter());

        // ── IExportFormatRegistry: add "Export as CSV (Demo)" ─────────────────────
        context.GetFeature<IExportFormatRegistry>()?.RegisterFormat(
            "Export as CSV (Demo)",
            async (samples, path, ct) =>
            {
                var lines = samples.Select(s => $"{s.Ordinal},{s.TopicMetadata.TopicName}");
                await File.WriteAllLinesAsync(path, lines, ct);
            });

        // ── ITooltipProviderRegistry: mock "sensor gauge" tooltip for DemoPayload ─
        context.GetFeature<ITooltipProviderRegistry>()?.RegisterProvider(
            (type, value) =>
            {
                if (type != typeof(DemoPayload))
                    return null;

                var dp = value as DemoPayload;
                return $"<div class=\"demo-tooltip\">" +
                       $"<strong>Sensor Gauge</strong><br/>" +
                       $"ID: {dp?.Id}<br/>" +
                       $"Label: {System.Net.WebUtility.HtmlEncode(dp?.Label ?? string.Empty)}" +
                       $"</div>";
            });

        // ── TopicColorService: color any topic containing "DEMO" in red (§10.2) ────
        context.GetFeature<TopicColorService>()?.RegisterColorRule(
            name => name.Contains("DEMO", StringComparison.OrdinalIgnoreCase) ? "#FF0000" : null);

        // ── IEventBroker: workspace save / load ───────────────────────────────────
        var broker = context.GetFeature<IEventBroker>();
        if (broker != null)
        {
            broker.Subscribe<WorkspaceSavingEvent>(e =>
                e.PluginSettings["FeatureDemo"] = new { DemoMode = true });

            broker.Subscribe<WorkspaceLoadedEvent>(e =>
            {
                // Restore demo mode from workspace settings when present.
                // The value is ignored in this stub; a real plugin would update state here.
                _ = e.PluginSettings.TryGetValue("FeatureDemo", out _);
            });
        }
    }
}
