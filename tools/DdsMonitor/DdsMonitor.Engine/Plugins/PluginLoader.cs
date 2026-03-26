using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Scans the configured plugin directories for DLLs containing <see cref="IMonitorPlugin"/>
/// implementations, loads them into isolated <see cref="AssemblyLoadContext"/> instances,
/// and manages the plugin lifecycle.
/// </summary>
/// <remarks>
/// Assembly isolation prevents plugin version conflicts.  Framework assemblies that must
/// share type identity with the host (e.g. <c>DdsMonitor.Engine</c>,
/// <c>Microsoft.Extensions.DependencyInjection.Abstractions</c>) are explicitly delegated
/// to the host's Default context so that interface assignments succeed across the boundary.
/// </remarks>
public sealed class PluginLoader
{
    /// <summary>
    /// Assembly short names that must be resolved from the Default (host) context
    /// to guarantee type identity between the host and any loaded plugin.
    /// </summary>
    private static readonly HashSet<string> SharedAssemblyNames = new(StringComparer.Ordinal)
    {
        "DdsMonitor.Engine",
        "CycloneDDS.Schema",
        "CycloneDDS.Runtime",
        "CycloneDDS.Core",
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.Logging",
        "netstandard",
        "System.Runtime",
    };

    private readonly string _pluginDirectory;
    private readonly ILogger<PluginLoader>? _logger;
    private readonly PluginConfigService? _configService;
    private readonly List<AssemblyLoadContext> _loadContexts = new();
    private readonly List<IMonitorPlugin> _plugins = new();
    private readonly List<DiscoveredPlugin> _discovered = new();

    /// <summary>
    /// Initialises a new <see cref="PluginLoader"/> that scans the <c>plugins</c>
    /// sub-folder of <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    /// <param name="configService">
    /// Optional service that tracks which plugins are enabled.  When <c>null</c> every
    /// discovered plugin is treated as enabled (default behaviour).
    /// </param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="pluginDirectory">
    /// Override the plugin directory path (intended for unit tests).  When <c>null</c>
    /// defaults to <c>AppContext.BaseDirectory/plugins</c>.
    /// </param>
    public PluginLoader(
        PluginConfigService? configService = null,
        ILogger<PluginLoader>? logger = null,
        string? pluginDirectory = null)
    {
        _pluginDirectory = pluginDirectory ?? Path.Combine(AppContext.BaseDirectory, "plugins");
        _configService = configService;
        _logger = logger;
    }

    /// <summary>Gets the plugin instances that were successfully loaded and enabled.</summary>
    public IReadOnlyList<IMonitorPlugin> LoadedPlugins => _plugins;

    /// <summary>
    /// Gets all <see cref="DiscoveredPlugin"/> entries found during the last
    /// <see cref="LoadPlugins"/> scan, including plugins that are disabled.
    /// </summary>
    public IReadOnlyList<DiscoveredPlugin> DiscoveredPlugins => _discovered;

    /// <summary>
    /// Scans all configured plugin directories, loads valid plugin DLLs, and records each
    /// in <see cref="DiscoveredPlugins"/>.  Calls <see cref="IMonitorPlugin.ConfigureServices"/>
    /// only for plugins that are enabled according to <see cref="PluginConfigService"/> (or
    /// all of them when no config service is supplied).
    /// Invalid or non-plugin DLLs are skipped gracefully.
    /// </summary>
    /// <param name="services">The host service collection (before the container is built).</param>
    public void LoadPlugins(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(_pluginDirectory) || !Directory.Exists(_pluginDirectory))
        {
            return;
        }

        foreach (var dllPath in Directory.EnumerateFiles(_pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            // EnumerateFiles returns paths relative to the process cwd when
            // `_pluginDirectory` is relative.  LoadFromAssemblyPath (and
            // AssemblyDependencyResolver) require an absolute path.
            TryLoadPluginFromFile(Path.GetFullPath(dllPath), services);
        }
    }

    /// <summary>
    /// Attempts to load <see cref="IMonitorPlugin"/> types from the specified DLL path,
    /// calling <see cref="IMonitorPlugin.ConfigureServices"/> on each.
    /// Errors are logged and suppressed — the host is never crashed by a bad plugin DLL.
    /// </summary>
    /// <returns>The number of plugin instances loaded from the file.</returns>
    public int TryLoadPluginFromFile(string dllPath, IServiceCollection services)
    {
        if (string.IsNullOrWhiteSpace(dllPath))
        {
            return 0;
        }

        try
        {
            return LoadPluginFromFileCore(dllPath, services);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "PluginLoader: failed to load '{Path}'. Skipping.", dllPath);
            return 0;
        }
    }

    /// <summary>
    /// Calls <see cref="IMonitorPlugin.Initialize"/> on every loaded plugin.
    /// Should be called after the application host has fully started.
    /// Exceptions thrown by individual plugins are caught and logged so that one
    /// misbehaving plugin cannot prevent others from initialising.
    /// </summary>
    /// <param name="context">The monitor context provided to each plugin.</param>
    public void InitializePlugins(IMonitorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var plugin in _plugins)
        {
            try
            {
                plugin.Initialize(context);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "PluginLoader: plugin '{Name}' threw during Initialize().", plugin.Name);
            }
        }
    }

    private int LoadPluginFromFileCore(string dllPath, IServiceCollection services)
    {
        // Ensure the path is absolute; AssemblyLoadContext.LoadFromAssemblyPath
        // and AssemblyDependencyResolver both require a rooted path.
        var absolutePath = Path.GetFullPath(dllPath);
        var loadContext = new PluginAssemblyLoadContext(absolutePath);
        var assembly = loadContext.LoadFromAssemblyPath(absolutePath);

        _loadContexts.Add(loadContext);

        var count = 0;

        foreach (var type in GetExportedTypesSafe(assembly))
        {
            if (!type.IsClass || type.IsAbstract)
            {
                continue;
            }

            if (!typeof(IMonitorPlugin).IsAssignableFrom(type))
            {
                continue;
            }

            var plugin = (IMonitorPlugin)Activator.CreateInstance(type)!;

            // Determine enabled state: when no config service is present every plugin is
            // enabled (backward-compatible default for headless / test environments).
            // Otherwise only plugins explicitly listed in EnabledPlugins are activated.
            // Plugins are disabled by default on first run; the user enables them via
            // the Plugin Manager UI, after which the list is persisted to the workspace.
            var isEnabled = _configService == null
                || _configService.EnabledPlugins.Contains(plugin.Name);

            _discovered.Add(new DiscoveredPlugin(plugin, absolutePath, isEnabled));

            if (isEnabled)
            {
                _plugins.Add(plugin);

                try
                {
                    plugin.ConfigureServices(services);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "PluginLoader: plugin '{Name}' threw during ConfigureServices().", plugin.Name);
                }
            }

            count++;
        }

        return count;
    }

    private static IEnumerable<Type> GetExportedTypesSafe(Assembly assembly)
    {
        try
        {
            return assembly.ExportedTypes;
        }
        catch
        {
            return Enumerable.Empty<Type>();
        }
    }

    // ── Nested load context ─────────────────────────────────────────────────

    private sealed class PluginAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginAssemblyLoadContext(string mainAssemblyPath)
            : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Delegate shared assemblies to the Default context so that type identities
            // (e.g. IMonitorPlugin, IServiceCollection) remain equal on both sides of
            // the assembly load context boundary.
            if (SharedAssemblyNames.Contains(assemblyName.Name ?? string.Empty))
            {
                return Default.LoadFromAssemblyName(assemblyName);
            }

            var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);

            if (resolvedPath != null)
            {
                return LoadFromAssemblyPath(resolvedPath);
            }

            // Fall through to Default context resolution.
            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var resolvedPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

            if (resolvedPath != null)
            {
                return LoadUnmanagedDllFromPath(resolvedPath);
            }

            return IntPtr.Zero;
        }
    }
}
