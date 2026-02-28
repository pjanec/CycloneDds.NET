using System;
using System.Threading.Channels;
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

        var topicRegistry = new TopicRegistry();
        var discovery = new TopicDiscoveryService(topicRegistry);
        discovery.Discover(settings.PluginDirectories);
        services.AddSingleton<ITopicRegistry>(topicRegistry);

        services.AddSingleton<IDdsBridge, DdsBridge>();
        services.AddSingleton<ISampleStore, SampleStore>();
        services.AddSingleton<IInstanceStore, InstanceStore>();
        services.AddSingleton<IEventBroker, EventBroker>();
        services.AddSingleton<IFilterCompiler, FilterCompiler>();

        services.AddSingleton(sp => Channel.CreateUnbounded<SampleData>());
        services.AddSingleton(sp => sp.GetRequiredService<Channel<SampleData>>().Reader);
        services.AddSingleton(sp => sp.GetRequiredService<Channel<SampleData>>().Writer);

        services.AddScoped<IWindowManager, WindowManager>();
        services.AddScoped<IWorkspaceState, WorkspaceState>();

        services.AddHostedService<DdsIngestionService>();

        return services;
    }
}
