using Avalonia;
using Avalonia.Headless;

namespace DdsMonitor.Avalonia.Tests;

/// <summary>
/// Minimal Avalonia application for headless shell tests.
/// Provides the FluentTheme so ShellWindow can initialize its styles.
/// </summary>
public sealed class TestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());

    public override void Initialize()
    {
        Styles.Add(new global::Avalonia.Themes.Fluent.FluentTheme());
    }
}
