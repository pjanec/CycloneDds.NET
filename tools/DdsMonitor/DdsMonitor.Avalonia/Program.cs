using Avalonia;
using DdsMonitor.Avalonia;
using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Hosting;
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Register core engine services (DDS, plugins, IMenuRegistry, etc.)
builder.Services.AddDdsMonitorServices(builder.Configuration);

// Avalonia-specific singletons (registered AFTER engine so these override defaults)
builder.Services.AddSingleton<IToolbarRegistry, ToolbarRegistry>();
builder.Services.AddSingleton<IUserSettings, UserSettingsStore>();
builder.Services.AddSingleton<IAvaloniaViewRegistry, AvaloniaViewRegistry>();
builder.Services.AddSingleton<IAvaloniaTypeDrawerRegistry, AvaloniaTypeDrawerRegistry>();

// Override the scoped IWindowManager registered by the engine with an Avalonia singleton
builder.Services.AddSingleton<IWindowManager, AvaloniaWindowManager>();

// Persistence: debounce workspace saves triggered by WorkspaceSaveRequestedEvent
builder.Services.AddHostedService<AvaloniaWorkspacePersistenceService>();

var host = builder.Build();

var settings = host.Services.GetRequiredService<DdsSettings>();

if (settings.HeadlessMode != HeadlessMode.None)
{
    // Record / Replay mode: run without a UI window
    await host.RunAsync();
}
else
{
    // Interactive Avalonia desktop mode
    _ = host.StartAsync();

    // Initialize plugins after host starts, before showing the window
    var pluginLoader = host.Services.GetRequiredService<PluginLoader>();
    var monitorContext = host.Services.GetRequiredService<IMonitorContext>();
    pluginLoader.InitializePlugins(monitorContext);

    BuildAvaloniaApp(host.Services).StartWithClassicDesktopLifetime(args);
}

static AppBuilder BuildAvaloniaApp(IServiceProvider services) =>
    AppBuilder.Configure(() => new App(services))
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
