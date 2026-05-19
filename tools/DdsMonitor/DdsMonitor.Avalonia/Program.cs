using System;
using Avalonia;
using Avalonia.Logging;
using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Hosting;
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DdsMonitor.Avalonia;

internal sealed class Program
{
    // Avalonia requires an STA thread on Windows. Top-level async statements 
    // force MTA, causing PlatformNotSupportedException.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => 
        {
            Console.WriteLine($"[FATAL] AppDomain Unhandled: {e.ExceptionObject}");
        };
    
        TaskScheduler.UnobservedTaskException += (s, e) => 
        {
            Console.WriteLine($"[FATAL] Task Unobserved: {e.Exception}");
        };


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
            // Record / Replay mode: run synchronously to block the main thread.
            host.Run();
        }
        else
        {
            // Force all diagnostic traces into a text file
            System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener("avalonia-startup-crash.log"));
            System.Diagnostics.Trace.AutoFlush = true;

            // 1. Build and initialize Avalonia platform subsystems FIRST (injects Win32)
            var appBuilder = BuildAvaloniaApp(host.Services)
                .LogToTrace( global::Avalonia.Logging.LogEventLevel.Verbose); // <-- Crank to Verbose

            // 2. NOW it is safe to start the Generic Host.
            // When AvaloniaWorkspacePersistenceService resolves AvaloniaWindowManager 
            // and touches the Window class, Avalonia is already fully hooked into the OS.
             _ = host.StartAsync();

            // Initialize plugins after host starts, before showing the window
            var pluginLoader = host.Services.GetRequiredService<PluginLoader>();
            var monitorContext = host.Services.GetRequiredService<IMonitorContext>();
            pluginLoader.InitializePlugins(monitorContext);

            BuildAvaloniaApp(host.Services).StartWithClassicDesktopLifetime(args);
        }
    }

    private static AppBuilder BuildAvaloniaApp(IServiceProvider services) =>
        AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
