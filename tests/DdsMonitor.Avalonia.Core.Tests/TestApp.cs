using Avalonia;
using Avalonia.Headless;


namespace DdsMonitor.Avalonia.Core.Tests;

/// <summary>
/// Minimal Avalonia application for headless test fixture.
/// </summary>
public sealed class TestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
