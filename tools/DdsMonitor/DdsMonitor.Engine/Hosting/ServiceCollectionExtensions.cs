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

        services.AddSingleton<IDdsBridge, DdsBridge>();
        services.AddSingleton<ISampleStore, SampleStore>();
        services.AddSingleton<IInstanceStore, InstanceStore>();
        services.AddSingleton<IEventBroker, EventBroker>();
        services.AddSingleton<IFilterCompiler, FilterCompiler>();

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
