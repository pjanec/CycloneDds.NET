using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DdsMonitor.Engine.Plugins;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Engine.Tests.Plugins;

/// <summary>
/// Unit tests for the two-phase <see cref="PluginLoader"/> behaviour (PLA1-P5-T02).
/// </summary>
public sealed class PluginLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public PluginLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // ── PLA1-P5-T02 tests ──────────────────────────────────────────────────

    [Fact]
    public void LoadPlugins_PopulatesDiscoveredPlugins()
    {
        const string source = @"
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

public sealed class DiscoverTestPlugin : IMonitorPlugin
{
    public string Name => ""DiscoverTestPlugin"";
    public string Version => ""1.0.0"";
    public void ConfigureServices(IServiceCollection services) { }
    public void Initialize(IMonitorContext context) { }
}";

        CompilePlugin("DiscoverTestPlugin", source, _tempDir);

        var loader = new PluginLoader(pluginDirectory: _tempDir);
        loader.LoadPlugins(new ServiceCollection());

        Assert.Single(loader.DiscoveredPlugins);
        Assert.Equal("DiscoverTestPlugin", loader.DiscoveredPlugins[0].Instance.Name);
    }

    [Fact]
    public void LoadPlugins_DisabledPlugin_DoesNotCallConfigureServices()
    {
        const string source = @"
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

public sealed class DisabledPlugin : IMonitorPlugin
{
    public string Name => ""DisabledPlugin"";
    public string Version => ""1.0.0"";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<string>(""disabled-plugin-configured"");
    }

    public void Initialize(IMonitorContext context) { }
}";

        var configFilePath = Path.Combine(_tempDir, "enabled-plugins.json");
        CompilePlugin("DisabledPlugin", source, _tempDir);

        // Save a config that does NOT include "DisabledPlugin" — the plugin is disabled.
        var configSvc = new PluginConfigService(configFilePath);
        configSvc.Save(new HashSet<string>(StringComparer.Ordinal));

        var loader = new PluginLoader(configSvc, pluginDirectory: _tempDir);
        var services = new ServiceCollection();
        loader.LoadPlugins(services);

        // Plugin is discovered but disabled.
        Assert.Single(loader.DiscoveredPlugins);
        Assert.False(loader.DiscoveredPlugins[0].IsEnabled);

        // ConfigureServices must NOT have been called for a disabled plugin.
        Assert.DoesNotContain(services, sd =>
            sd.ServiceType == typeof(string) &&
            sd.ImplementationInstance is "disabled-plugin-configured");
    }

    [Fact]
    public void LoadPlugins_EnabledPlugin_CallsConfigureServices()
    {
        const string source = @"
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

public sealed class EnabledPlugin : IMonitorPlugin
{
    public string Name => ""EnabledPlugin"";
    public string Version => ""1.0.0"";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<string>(""enabled-plugin-configured"");
    }

    public void Initialize(IMonitorContext context) { }
}";

        var configFilePath = Path.Combine(_tempDir, "enabled-plugins.json");
        CompilePlugin("EnabledPlugin", source, _tempDir);

        // Save a config that includes "EnabledPlugin".
        var configSvc = new PluginConfigService(configFilePath);
        configSvc.Save(new HashSet<string>(StringComparer.Ordinal) { "EnabledPlugin" });

        var loader = new PluginLoader(configSvc, pluginDirectory: _tempDir);
        var services = new ServiceCollection();
        loader.LoadPlugins(services);

        // Plugin is discovered and enabled.
        Assert.Single(loader.DiscoveredPlugins);
        Assert.True(loader.DiscoveredPlugins[0].IsEnabled);

        // ConfigureServices must have been called exactly once.
        Assert.Contains(services, sd =>
            sd.ServiceType == typeof(string) &&
            sd.ImplementationInstance is "enabled-plugin-configured");
    }

    [Fact]
    public void LoadPlugins_MalformedDll_IsSkipped()
    {
        // Write a plain text file with a .dll extension — not a valid PE image.
        var fakeDllPath = Path.Combine(_tempDir, "Malformed.dll");
        File.WriteAllText(fakeDllPath, "this is not a DLL");

        var loader = new PluginLoader(pluginDirectory: _tempDir);
        var ex = Record.Exception(() => loader.LoadPlugins(new ServiceCollection()));

        Assert.Null(ex);
        // The malformed file must not appear in DiscoveredPlugins.
        Assert.Empty(loader.DiscoveredPlugins);
    }

    [Fact]
    public void LoadPlugins_WhenConfigFileMissing_DisablesAllDiscoveredPlugins()
    {
        const string source = @"
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

public sealed class MissingCfgPlugin : IMonitorPlugin
{
    public string Name => ""MissingCfgPlugin"";
    public string Version => ""1.0.0"";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<string>(""missing-cfg-configured"");
    }

    public void Initialize(IMonitorContext context) { }
}";

        CompilePlugin("MissingCfgPlugin", source, _tempDir);

        var configPath = Path.Combine(_tempDir, "no-such-config-yet.json");
        var configSvc = new PluginConfigService(configPath);
        Assert.False(configSvc.HadConfigFileAtInitialization);

        var loader = new PluginLoader(configSvc, pluginDirectory: _tempDir);
        var services = new ServiceCollection();
        loader.LoadPlugins(services);

        // Plugins are disabled by default when no config file exists.
        // The user must explicitly enable them via the Plugin Manager UI.
        Assert.Single(loader.DiscoveredPlugins);
        Assert.False(loader.DiscoveredPlugins[0].IsEnabled);
        Assert.DoesNotContain(services, sd =>
            sd.ServiceType == typeof(string) &&
            sd.ImplementationInstance is "missing-cfg-configured");
    }

    // ── PLA1-DEBT-010: Corrupt config file is treated like a missing file ──

    [Fact]
    public void LoadPlugins_WhenConfigFileCorrupt_DisablesAllDiscoveredPlugins()
    {
        const string source = @"
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

public sealed class CorruptCfgPlugin : IMonitorPlugin
{
    public string Name => ""CorruptCfgPlugin"";
    public string Version => ""1.0.0"";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<string>(""corrupt-cfg-configured"");
    }

    public void Initialize(IMonitorContext context) { }
}";

        var configFilePath = Path.Combine(_tempDir, "corrupt.json");
        File.WriteAllText(configFilePath, "{ this is not valid json !!!");

        CompilePlugin("CorruptCfgPlugin", source, _tempDir);

        var configSvc = new PluginConfigService(configFilePath);

        // DEBT-010: corrupt file must be treated like a missing file.
        Assert.False(configSvc.HadConfigFileAtInitialization);

        var loader = new PluginLoader(configSvc, pluginDirectory: _tempDir);
        var services = new ServiceCollection();
        loader.LoadPlugins(services);

        // Plugin must be discovered but disabled (disabled-by-default semantics for corrupt config).
        Assert.Single(loader.DiscoveredPlugins);
        Assert.False(loader.DiscoveredPlugins[0].IsEnabled);
        Assert.DoesNotContain(services, sd =>
            sd.ServiceType == typeof(string) &&
            sd.ImplementationInstance is "corrupt-cfg-configured");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string CompilePlugin(string assemblyName, string source, string outputDirectory)
    {
        var tempPath = Path.GetTempPath();
        var loadedRefs = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)
                        && !a.Location.StartsWith(tempPath, StringComparison.OrdinalIgnoreCase))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList<MetadataReference>();

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source) },
            loadedRefs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var dllPath = Path.Combine(outputDirectory, assemblyName + ".dll");

        using var stream = new FileStream(dllPath, FileMode.Create, FileAccess.Write);
        var result = compilation.Emit(stream);

        if (!result.Success)
        {
            var errors = string.Join(Environment.NewLine,
                result.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()));

            throw new InvalidOperationException($"Plugin compilation failed:{Environment.NewLine}{errors}");
        }

        return dllPath;
    }
}
