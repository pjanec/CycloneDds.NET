using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DdsMonitor.Engine.Plugins;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for MON-BATCH-28:
///   DMON-041 — Plugin loading infrastructure (PluginLoader, IMonitorPlugin, IMonitorContext)
///   DMON-042 — Plugin panel registration (IWindowManager.RegisteredPanelTypes)
///   DMON-043 — Plugin menu registration (IMenuRegistry / MenuRegistry hierarchical tree)
/// </summary>
public sealed class Batch28Tests : IDisposable
{
    private readonly string _tempDir;

    public Batch28Tests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DMON-043: MenuRegistry — hierarchical menu tree
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MenuRegistry_AddItem_Sync_AppearsAtTopLevel()
    {
        // Adding a path-less item places it directly in the root.
        var registry = new MenuRegistry();
        var invoked = false;

        registry.AddMenuItem(string.Empty, "Root Action", () => { invoked = true; });

        var roots = registry.GetTopLevelMenus();
        Assert.Single(roots);
        Assert.Equal("Root Action", roots[0].Label);
        Assert.True(roots[0].IsLeaf);

        // Verify the callback is stored and invoked correctly.
        roots[0].OnClick!();
        Assert.True(invoked);
    }

    [Fact]
    public async Task MenuRegistry_AddItem_Async_AppearsAtTopLevel()
    {
        var registry = new MenuRegistry();
        var invoked = false;

        registry.AddMenuItem(string.Empty, "Async Action", async () =>
        {
            await Task.Yield();
            invoked = true;
        });

        var roots = registry.GetTopLevelMenus();
        Assert.Single(roots);
        Assert.Equal("Async Action", roots[0].Label);
        Assert.True(roots[0].IsLeaf);
        Assert.NotNull(roots[0].OnClickAsync);

        // Invoke the async callback and verify the side-effect.
        await roots[0].OnClickAsync!();
        Assert.True(invoked);
    }

    [Fact]
    public void MenuRegistry_AddItem_NestedPath_CreatesCorrectHierarchy()
    {
        // "Plugins/ECS/Show Entities" → three levels: Plugins → ECS → Show Entities (leaf)
        var registry = new MenuRegistry();

        registry.AddMenuItem("Plugins/ECS", "Show Entities", () => { });

        var roots = registry.GetTopLevelMenus();
        Assert.Single(roots);

        var plugins = roots[0];
        Assert.Equal("Plugins", plugins.Label);
        Assert.False(plugins.IsLeaf);
        Assert.Single(plugins.Children);

        var ecs = plugins.Children[0];
        Assert.Equal("ECS", ecs.Label);
        Assert.False(ecs.IsLeaf);
        Assert.Single(ecs.Children);

        var leaf = ecs.Children[0];
        Assert.Equal("Show Entities", leaf.Label);
        Assert.True(leaf.IsLeaf);
    }

    [Fact]
    public void MenuRegistry_AddItems_SamePath_SharedBranchNode()
    {
        // Two items under the same path must share the same branch node, not duplicate it.
        var registry = new MenuRegistry();

        registry.AddMenuItem("Plugins/Tools", "Action A", () => { });
        registry.AddMenuItem("Plugins/Tools", "Action B", () => { });

        var roots = registry.GetTopLevelMenus();
        Assert.Single(roots); // only one "Plugins" root

        var plugins = roots[0];
        Assert.Single(plugins.Children); // only one "Tools" branch

        var tools = plugins.Children[0];
        Assert.Equal(2, tools.Children.Count); // two leaf items
        Assert.Equal("Action A", tools.Children[0].Label);
        Assert.Equal("Action B", tools.Children[1].Label);
    }

    [Fact]
    public void MenuRegistry_SyncCallback_IsInvokedWhenCalled()
    {
        var registry = new MenuRegistry();
        var callCount = 0;

        registry.AddMenuItem("Plugins", "Counter", () => callCount++);

        var leaf = registry.GetTopLevelMenus()[0].Children[0];
        leaf.OnClick!();
        leaf.OnClick!();

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task MenuRegistry_AsyncCallback_IsInvokedWhenCalled()
    {
        var registry = new MenuRegistry();
        var callCount = 0;

        registry.AddMenuItem("Plugins", "AsyncCounter", async () =>
        {
            await Task.Yield();
            callCount++;
        });

        var leaf = registry.GetTopLevelMenus()[0].Children[0];
        await leaf.OnClickAsync!();
        await leaf.OnClickAsync!();

        Assert.Equal(2, callCount);
    }

    [Fact]
    public void MenuRegistry_Changed_FiredOnAdd()
    {
        var registry = new MenuRegistry();
        var changeCount = 0;

        registry.Changed += () => changeCount++;

        registry.AddMenuItem("Test", "Item1", () => { });
        registry.AddMenuItem("Test", "Item2", () => { });

        Assert.Equal(2, changeCount);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DMON-042: WindowManager — RegisteredPanelTypes + SpawnRegisteredPlugin
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void WindowManager_RegisterPanelType_AppearsInRegisteredPanelTypes()
    {
        var manager = new WindowManager();

        manager.RegisterPanelType("MyPlugin", typeof(FakePanelComponent));

        var registered = manager.RegisteredPanelTypes;
        Assert.Single(registered);
        Assert.True(registered.ContainsKey("MyPlugin"));
        Assert.Equal(typeof(FakePanelComponent), registered["MyPlugin"]);
    }

    [Fact]
    public void WindowManager_RegisterMultiplePanelTypes_AllAppearInRegisteredPanelTypes()
    {
        var manager = new WindowManager();

        manager.RegisterPanelType("Panel A", typeof(FakePanelComponent));
        manager.RegisterPanelType("Panel B", typeof(FakePanelComponent2));

        var registered = manager.RegisteredPanelTypes;
        Assert.Equal(2, registered.Count);
        Assert.Equal(typeof(FakePanelComponent), registered["Panel A"]);
        Assert.Equal(typeof(FakePanelComponent2), registered["Panel B"]);
    }

    [Fact]
    public void WindowManager_SpawnRegisteredPluginPanel_CreatesPanel()
    {
        var manager = new WindowManager();
        manager.RegisterPanelType("MyPlugin", typeof(FakePanelComponent));

        var panel = manager.SpawnPanel("MyPlugin");

        Assert.Single(manager.ActivePanels);
        // The spawned panel's ComponentTypeName is the full name of the registered type.
        Assert.Contains(nameof(FakePanelComponent), manager.ActivePanels[0].ComponentTypeName);
    }

    [Fact]
    public void WindowManager_RegisteredPanelTypes_IsSnapshot_NotLiveView()
    {
        // Mutating the dictionary after RegisterPanelType must not affect the original.
        var manager = new WindowManager();
        manager.RegisterPanelType("A", typeof(FakePanelComponent));

        // Take a snapshot.
        var snapshot = manager.RegisteredPanelTypes;

        // Register another type after snapshot was taken.
        manager.RegisterPanelType("B", typeof(FakePanelComponent2));

        // Snapshot must still have only "A".
        Assert.Single(snapshot);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DMON-041: PluginPanelRegistry
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PluginPanelRegistry_RegisterPanelType_AppearsInRegisteredTypes()
    {
        var registry = new PluginPanelRegistry();
        var changed = false;
        registry.Changed += () => changed = true;

        registry.RegisterPanelType("MyPanel", typeof(FakePanelComponent));

        var types = registry.RegisteredTypes;
        Assert.Single(types);
        Assert.Equal(typeof(FakePanelComponent), types["MyPanel"]);
        Assert.True(changed);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DMON-041: PluginLoader — bad DLL skipped gracefully
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PluginLoader_BadDll_SkipsGracefully_NoException()
    {
        // Write a file with a .dll extension but with garbage bytes.
        var badDllPath = Path.Combine(_tempDir, "corrupt.dll");
        File.WriteAllBytes(badDllPath, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01 });

        var settings = new DdsSettings { PluginDirectories = new[] { _tempDir } };
        var services = new ServiceCollection();

        var loader = new PluginLoader(settings);

        // Must not throw.
        var ex = Record.Exception(() => loader.LoadPlugins(services));

        Assert.Null(ex);
        Assert.Empty(loader.LoadedPlugins);
    }

    [Fact]
    public void PluginLoader_EmptyDirectory_LoadsNothingGracefully()
    {
        // Pointing at an empty directory must not crash.
        var settings = new DdsSettings { PluginDirectories = new[] { _tempDir } };
        var loader = new PluginLoader(settings);
        var services = new ServiceCollection();

        var ex = Record.Exception(() => loader.LoadPlugins(services));

        Assert.Null(ex);
        Assert.Empty(loader.LoadedPlugins);
    }

    [Fact]
    public void PluginLoader_MissingDirectory_SkipsGracefully()
    {
        // A directory that doesn't exist must be silently ignored.
        var settings = new DdsSettings { PluginDirectories = new[] { @"C:\this\does\not\exist\abc123" } };
        var loader = new PluginLoader(settings);
        var services = new ServiceCollection();

        var ex = Record.Exception(() => loader.LoadPlugins(services));

        Assert.Null(ex);
        Assert.Empty(loader.LoadedPlugins);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DMON-041: PluginLoader — loading a real compiled plugin
    // Uses Roslyn to compile a minimal IMonitorPlugin implementation in temp dir.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PluginLoader_ValidPlugin_ConfigureServicesCalled()
    {
        // Compile a plugin that registers a marker service in ConfigureServices.
        const string pluginSource = @"
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;
using System;

public sealed class ConfigTestPlugin : IMonitorPlugin
{
    public string Name => ""ConfigTestPlugin"";
    public string Version => ""1.0.0"";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<string>(""plugin-configured"");
    }

    public void Initialize(IMonitorContext context) { }
}";

        var dllPath = CompilePlugin("ConfigTestPlugin", pluginSource, _tempDir);

        var settings = new DdsSettings { PluginDirectories = Array.Empty<string>() };
        var loader = new PluginLoader(settings);
        var services = new ServiceCollection();

        var loaded = loader.TryLoadPluginFromFile(dllPath, services);

        Assert.Equal(1, loaded);
        Assert.Single(loader.LoadedPlugins);
        Assert.Equal("ConfigTestPlugin", loader.LoadedPlugins[0].Name);
        Assert.Equal("1.0.0", loader.LoadedPlugins[0].Version);

        // Verify ConfigureServices was called by checking the service descriptor.
        Assert.Contains(services, sd =>
            sd.ServiceType == typeof(string) &&
            sd.ImplementationInstance is "plugin-configured");
    }

    [Fact]
    public void PluginLoader_ValidPlugin_InitializeReceivesContext()
    {
        // Compile a plugin that registers a menu item during Initialize so we can
        // verify the IMonitorContext was properly passed in.
        const string pluginSource = @"
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;
using System;

public sealed class InitTestPlugin : IMonitorPlugin
{
    public string Name => ""InitTestPlugin"";
    public string Version => ""2.0.0"";

    public void ConfigureServices(IServiceCollection services) { }

    public void Initialize(IMonitorContext context)
    {
        context.MenuRegistry.AddMenuItem(""Plugins/InitTest"", ""WasHere"", () => { });
    }
}";

        var dllPath = CompilePlugin("InitTestPlugin", pluginSource, _tempDir);

        var settings = new DdsSettings { PluginDirectories = Array.Empty<string>() };
        var loader = new PluginLoader(settings);
        var services = new ServiceCollection();
        loader.TryLoadPluginFromFile(dllPath, services);

        // Build a minimal context with real registries.
        var menuRegistry = new MenuRegistry();
        var panelRegistry = new PluginPanelRegistry();
        var context = new MonitorContext(menuRegistry, panelRegistry);

        loader.InitializePlugins(context);

        // The plugin should have added a menu item under "Plugins/InitTest".
        var roots = menuRegistry.GetTopLevelMenus();
        Assert.Single(roots);
        Assert.Equal("Plugins", roots[0].Label);
        var initTest = roots[0].Children.FirstOrDefault(n => n.Label == "InitTest");
        Assert.NotNull(initTest);
        Assert.Single(initTest!.Children);
        Assert.Equal("WasHere", initTest.Children[0].Label);
    }

    [Fact]
    public void PluginLoader_AssemblyLoadContext_TypeIdentityPreserved()
    {
        // Type-identity test: IMonitorPlugin loaded from the plugin ALC must be
        // the same interface type as the one in the Default context (DdsMonitor.Engine).
        // If the ALC did NOT share DdsMonitor.Engine, IsAssignableFrom would return false
        // and the plugin would not be discovered.
        const string pluginSource = @"
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;

public sealed class IsolationPlugin : IMonitorPlugin
{
    public string Name => ""IsolationPlugin"";
    public string Version => ""1.0"";
    public void ConfigureServices(IServiceCollection services) { }
    public void Initialize(IMonitorContext context) { }
}";

        var dllPath = CompilePlugin("IsolationPlugin", pluginSource, _tempDir);

        var settings = new DdsSettings { PluginDirectories = Array.Empty<string>() };
        var loader = new PluginLoader(settings);
        var services = new ServiceCollection();

        loader.TryLoadPluginFromFile(dllPath, services);

        // The loaded plugin instance must be assignable to the host's IMonitorPlugin interface.
        Assert.Single(loader.LoadedPlugins);
        Assert.IsAssignableFrom<IMonitorPlugin>(loader.LoadedPlugins[0]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles a minimal plugin assembly from C# source using Roslyn and writes it to
    /// <paramref name="outputDirectory"/>, then returns the full path to the resulting DLL.
    /// </summary>
    private static string CompilePlugin(string assemblyName, string source, string outputDirectory)
    {
        // Collect all assemblies already loaded in the current AppDomain as references.
        // This ensures that transitive dependencies (e.g. System.ComponentModel, netstandard,
        // Microsoft.Extensions.*) are available without enumerating them manually.
        var loadedRefs = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
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

    // ── Dummy Blazor component types used as stand-ins in panel tests ─────────

    private sealed class FakePanelComponent { }
    private sealed class FakePanelComponent2 { }
}
