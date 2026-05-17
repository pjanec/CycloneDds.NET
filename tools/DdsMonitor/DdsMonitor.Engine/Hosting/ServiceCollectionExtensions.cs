using System;
using System.Threading.Channels;
using DdsMonitor.Engine.AssemblyScanner;
using DdsMonitor.Engine.Export;
using DdsMonitor.Engine.Import;
using DdsMonitor.Engine.Plugins;
using DdsMonitor.Engine.Replay;
using DdsMonitor.Engine.Ui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        IConfiguration configuration,
        ILoggerFactory? loggerFactory = null)
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
        var appSettings = configuration.GetSection(AppSettings.SectionName).Get<AppSettings>() ?? new AppSettings();

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
        services.AddSingleton(appSettings);

        // Runtime developer/debug settings (live-togglable from the UI).
        // Initialise from DdsSettings so that CLI flags like
        // --DdsSettings:SelfSendEnabled=true --DdsSettings:SelfSendRateHz=1000
        // take effect immediately on startup without needing the UI.
        services.AddSingleton<DevelSettings>(sp =>
        {
            var s = sp.GetRequiredService<DdsSettings>();
            var devel = new DevelSettings();
            devel.SelfSendEnabled = s.SelfSendEnabled;
            if (s.SelfSendRateHz > 0)
            {
                devel.SelfSendRateHz = s.SelfSendRateHz;
            }

            return devel;
        });

        // Performance counters for monitoring the ingestion hot path.
        services.AddSingleton<PerfCounters>();

        var topicRegistry = new TopicRegistry();
        var discoveryService = new TopicDiscoveryService(topicRegistry);
        discoveryService.Discover(Path.Combine(AppContext.BaseDirectory, "plugins"));

        // Eagerly instantiate to ensure saved dynamic DLLs are loaded immediately on startup.
        // Pass appSettings so CLI TopicSources overrides are honoured without modifying the file.
        var assemblySourceService = new AssemblySourceService(topicRegistry, discoveryService, appSettings);

        services.AddSingleton<ITopicRegistry>(topicRegistry);
        services.AddSingleton(discoveryService);
        services.AddSingleton<IAssemblySourceService>(assemblySourceService);
        services.AddHostedService<AssemblySourcePersistenceService>();

        // Register self-send topic types eagerly so they are present in the registry
        // before any hosted service (e.g. SelfSendService) or pre-startup exclusion
        // bootstrap runs.  SelfSendService.ExecuteAsync() previously did this, but
        // that was too late when SelfSendEnabled=true causes an immediate subscription.
        SelfSendTopics.Register(topicRegistry);

        // ── Phase 5: Plugin infrastructure ───────────────────────────────────────────
        // Create plugin singletons eagerly (before the container is built) so that
        // PluginLoader.LoadPlugins() can call ConfigureServices on each plugin while
        // the IServiceCollection is still open for registration.
        var menuRegistry = new MenuRegistry();
        var panelRegistry = new PluginPanelRegistry();
        var pluginConfigService = new PluginConfigService(loggerFactory, appSettings);
        var pluginLoader = new PluginLoader(pluginConfigService);
        pluginLoader.LoadPlugins(services);

        services.AddSingleton<IMenuRegistry>(menuRegistry);
        services.AddSingleton(panelRegistry);
        services.AddSingleton(pluginLoader);
        services.AddSingleton(pluginConfigService);
        services.AddHostedService<PluginConfigPersistenceService>();
        services.AddSingleton<IContextMenuRegistry, ContextMenuRegistry>();
        services.AddSingleton<ISampleViewRegistry, SampleViewRegistry>();
        services.AddSingleton<IMonitorContext>(sp => new MonitorContext(sp));

        // Phase 6: Plugin-accessible UI registries ──────────────────────────────────────
        // Registered here (Engine layer) so that GetFeature<IValueFormatterRegistry>() and
        // GetFeature<ITypeDrawerRegistry>() work for plugins without coupling to the Blazor host.
        // Program.cs registers these too for the interactive UI; duplicate singletons are harmless
        // because DI returns the last registration and both registrations use the same concrete types.
        services.AddSingleton<IValueFormatterRegistry, ValueFormatterRegistry>();
        services.AddSingleton<ITypeDrawerRegistry, TypeDrawerRegistry>();

        // IExportFormatRegistry: allows plugins to contribute custom export formats.
        services.AddSingleton<IExportFormatRegistry, ExportFormatRegistry>();

        // ITooltipProviderRegistry: allows plugins to contribute custom tooltip HTML (P6-T06).
        services.AddSingleton<ITooltipProviderRegistry, TooltipProviderRegistry>();

        // IFilterMacroRegistry: allows plugins to register custom filter macro functions (P6-T08).
        services.AddSingleton<IFilterMacroRegistry, FilterMacroRegistry>();

        // TopicColorService: registered as singleton so all plugin code (incl. FeatureDemoPlugin
        // running in the root scope at Initialize time) can resolve it via GetFeature<TopicColorService>()
        // (DEBT-018). Uses the same WorkspaceState factory path as IWorkspaceState above.
        //
        // DEBT-022 design note: workspace path is immutable at runtime.
        // WorkspaceState.WorkspaceFilePath is computed once in its constructor from AppSettings,
        // which is bound from IConfiguration at host build time and never mutated afterward.
        // Therefore the singleton TopicColorService and scoped IWorkspaceState instances always
        // resolve to the same workspace directory — no desync is possible without a host restart.
        services.AddSingleton<TopicColorService>(sp =>
            new TopicColorService(
                new WorkspaceState(sp.GetService<AppSettings>()),
                sp.GetService<IEventBroker>()));

        // SelfSendService is always registered; it stays dormant until DevelSettings.SelfSendEnabled is set.
        services.AddHostedService<SelfSendService>();
        // Persists DevelSettings.SelfSendEnabled to the workspace file so the toggle
        // state survives restarts.
        services.AddHostedService<SelfSendPersistenceService>();

        // Shared ordinal counter (ME1-T07).
        services.AddSingleton<OrdinalCounter>();

        services.AddSingleton<ISampleStore, SampleStore>();
        services.AddSingleton<IInstanceStore, InstanceStore>();
        services.AddSingleton<IEventBroker, EventBroker>();
        services.AddSingleton<IFilterCompiler>(sp =>
            new FilterCompiler(sp.GetService<IFilterMacroRegistry>()));

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
        services.AddScoped<IWorkspaceState>(sp =>
            new WorkspaceState(sp.GetService<AppSettings>()));

        // ME1-T11: In Record-headless mode HeadlessRunnerService consumes the channel directly
        // so DdsIngestionService must not run (two readers on the same channel are not supported).
        if (settings.HeadlessMode != HeadlessMode.Record)
        {
            services.AddHostedService<DdsIngestionService>();
        }

        // ME1-T11: HeadlessRunnerService is only needed when headless mode is active.
        // When HeadlessMode == None the Blazor UI drives execution and IHostApplicationLifetime
        // may not be registered (e.g. in unit-test DI containers), so we skip registration.
        if (settings.HeadlessMode != HeadlessMode.None)
        {
            services.AddHostedService<HeadlessRunnerService>();
        }

        return services;
    }
}
