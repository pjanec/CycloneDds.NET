using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(DdsMonitor.Avalonia.StandardPlugin.Tests.StandardPluginTestApp))]

namespace DdsMonitor.Avalonia.StandardPlugin.Tests;

/// <summary>
/// Minimal Avalonia application for headless StandardPlugin tests.
/// </summary>
public sealed class StandardPluginTestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<StandardPluginTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
