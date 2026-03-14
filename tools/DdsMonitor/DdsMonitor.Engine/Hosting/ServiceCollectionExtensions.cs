using System;
using System.Threading.Channels;
using DdsMonitor.Engine.AssemblyScanner;
using DdsMonitor.Engine.Export;
using DdsMonitor.Engine.Import;
using DdsMonitor.Engine.Replay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Engine.Hosting;

/// <summary>
/// Registers DDS Monitor services for the application host.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds DDS Monitor services to the provided service collection.
    /// </summary>
    public static IServiceCollection AddDdsMonitorServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        var settings = configuration.GetSection(DdsSettings.SectionName).Get<DdsSettings>() ?? new DdsSettings();

        // Backward compat: if DomainId was changed from default but Participants is still the
        // default single-entry list (DomainId=0), migrate the legacy DomainId value into
        // Participants[0] so that --DdsSettings:DomainId=3 continues to work.
        if (settings.DomainId != DdsSettings.DefaultDomainId
            && settings.Participants.Count == 1
            && settings.Participants[0].DomainId == 0)
        {
            settings.Participants[0] = new ParticipantConfig
            {
                DomainId = (uint)settings.DomainId,
                PartitionName = settings.Participants[0].PartitionName
            };
        }

        services.AddSingleton(settings);

        // Runtime developer/debug settings (live-togglable from the UI).
        services.AddSingleton<DevelSettings>();

        var topicRegistry = new TopicRegistry();
        var discoveryService = new TopicDiscoveryService(topicRegistry);
        discoveryService.Discover(settings.PluginDirectories);

        // Eagerly instantiate to ensure saved dynamic DLLs are loaded immediately on startup.
        var assemblySourceService = new AssemblySourceService(topicRegistry, discoveryService);

        services.AddSingleton<ITopicRegistry>(topicRegistry);
        services.AddSingleton(discoveryService);
        services.AddSingleton<IAssemblySourceService>(assemblySourceService);

        // SelfSendService is always registered; it stays dormant until DevelSettings.SelfSendEnabled is set.
        services.AddHostedService<SelfSendService>();

        // Shared ordinal counter (ME1-T07).
        services.AddSingleton<OrdinalCounter>();

        services.AddSingleton<ISampleStore, SampleStore>();
        services.AddSingleton<IInstanceStore, InstanceStore>();
        services.AddSingleton<IEventBroker, EventBroker>();
        services.AddSingleton<IFilterCompiler, FilterCompiler>();

        // DdsBridge with full multi-participant and ordinal wiring (ME1-T06, ME1-T07).
        services.AddSingleton<IDdsBridge>(sp =>
        {
            var s = sp.GetRequiredService<DdsSettings>();
            var writer = sp.GetRequiredService<ChannelWriter<SampleData>>();
            var sampleStore = sp.GetRequiredService<ISampleStore>();
            var instanceStore = sp.GetRequiredService<IInstanceStore>();
            var ordinal = sp.GetRequiredService<OrdinalCounter>();

            var bridge = new DdsBridge(writer, s.Participants, null, sampleStore, instanceStore, ordinal);

            // Apply startup filter expression if configured (ME1-T07).
            if (!string.IsNullOrWhiteSpace(s.FilterExpression))
            {
                var compiler = sp.GetRequiredService<IFilterCompiler>();
                var result = compiler.Compile(s.FilterExpression, null);
                if (result.IsValid && result.Predicate != null)
                    bridge.SetFilter(result.Predicate);
            }

            return bridge;
        });

        services.AddSingleton(sp => Channel.CreateUnbounded<SampleData>());
        services.AddSingleton(sp => sp.GetRequiredService<Channel<SampleData>>().Reader);
        services.AddSingleton(sp => sp.GetRequiredService<Channel<SampleData>>().Writer);

        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IImportService, ImportService>();
        services.AddScoped<IReplayEngine, ReplayEngine>();

        services.AddScoped<IWindowManager, WindowManager>();
        services.AddScoped<IWorkspaceState, WorkspaceState>();

        services.AddHostedService<DdsIngestionService>();

        return services;
    }
}
