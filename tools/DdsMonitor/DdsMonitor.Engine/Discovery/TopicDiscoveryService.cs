using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using CycloneDDS.Schema;

namespace DdsMonitor.Engine;

/// <summary>
/// Scans assemblies for DDS topic types and registers their metadata.
/// </summary>
public sealed class TopicDiscoveryService
{
    private const string DllPattern = "*.dll";
    private const string SchemaAssemblyName = "CycloneDDS.Schema";

    private readonly ITopicRegistry _registry;
    private readonly List<AssemblyLoadContext> _loadContexts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TopicDiscoveryService"/> class.
    /// </summary>
    public TopicDiscoveryService(ITopicRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Scans the provided directories for topic assemblies.
    /// </summary>
    public void Discover(IEnumerable<string> directories)
    {
        if (directories == null)
        {
            throw new ArgumentNullException(nameof(directories));
        }

        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                continue;
            }

            foreach (var dllPath in Directory.EnumerateFiles(directory, DllPattern, SearchOption.TopDirectoryOnly))
            {
                if (!processed.Add(dllPath))
                {
                    continue;
                }

                TryDiscoverFromAssembly(dllPath);
            }
        }
    }

    /// <summary>
    /// Scans a single directory for topic assemblies.
    /// </summary>
    public void Discover(string directory)
    {
        Discover(new[] { directory });
    }

    private void TryDiscoverFromAssembly(string dllPath)
    {
        try
        {
            var loadContext = new CollectiblePluginLoadContext(dllPath);
            var assembly = loadContext.LoadFromAssemblyPath(dllPath);

            _loadContexts.Add(loadContext);

            foreach (var type in assembly.ExportedTypes)
            {
                if (type.GetCustomAttribute<DdsTopicAttribute>() == null)
                {
                    continue;
                }

                var metadata = new TopicMetadata(type);
                _registry.Register(metadata);
            }
        }
        catch
        {
            // Ignore unreadable or incompatible assemblies.
        }
    }

    private sealed class CollectiblePluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public CollectiblePluginLoadContext(string mainAssemblyPath)
            : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (string.Equals(assemblyName.Name, SchemaAssemblyName, StringComparison.Ordinal))
            {
                return AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
            }

            var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (resolvedPath == null)
            {
                return null;
            }

            return LoadFromAssemblyPath(resolvedPath);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var resolvedPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (resolvedPath == null)
            {
                return IntPtr.Zero;
            }

            return LoadUnmanagedDllFromPath(resolvedPath);
        }
    }
}
