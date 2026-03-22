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

    private readonly string[] _pluginDirectories;
    private readonly ILogger<PluginLoader>? _logger;
    private readonly List<AssemblyLoadContext> _loadContexts = new();
    private readonly List<IMonitorPlugin> _plugins = new();

    /// <summary>
    /// Initialises a new <see cref="PluginLoader"/> with directories taken from
    /// <see cref="DdsSettings.PluginDirectories"/>.
    /// </summary>
    public PluginLoader(DdsSettings settings, ILogger<PluginLoader>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _pluginDirectories = settings.PluginDirectories ?? Array.Empty<string>();
        _logger = logger;
    }

    /// <summary>Gets the plugin instances that were successfully loaded.</summary>
    public IReadOnlyList<IMonitorPlugin> LoadedPlugins => _plugins;

    /// <summary>
    /// Scans all configured plugin directories, loads valid plugin DLLs, and calls
    /// <see cref="IMonitorPlugin.ConfigureServices"/> on each discovered plugin.
    /// Invalid or non-plugin DLLs are skipped gracefully.
    /// </summary>
    /// <param name="services">The host service collection (before the container is built).</param>
    public void LoadPlugins(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var directory in _pluginDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                continue;
            }

            foreach (var dllPath in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                TryLoadPluginFromFile(dllPath, services);
            }
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
        var loadContext = new PluginAssemblyLoadContext(dllPath);
        var assembly = loadContext.LoadFromAssemblyPath(dllPath);

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
            _plugins.Add(plugin);

            try
            {
                plugin.ConfigureServices(services);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "PluginLoader: plugin '{Name}' threw during ConfigureServices().", plugin.Name);
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
