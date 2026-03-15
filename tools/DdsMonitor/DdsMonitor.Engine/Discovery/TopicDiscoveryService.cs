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
    private const string RuntimeAssemblyName = "CycloneDDS.Runtime";
    private const string CoreAssemblyName = "CycloneDDS.Core";

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

    /// <summary>
    /// Scans a single assembly file and registers the topic types found within it.
    /// Returns the number of topic types successfully registered, or throws if the
    /// assembly cannot be loaded.
    /// </summary>
    public int DiscoverFromFile(string dllPath)
    {
        if (string.IsNullOrWhiteSpace(dllPath))
        {
            throw new ArgumentException("DLL path must not be empty.", nameof(dllPath));
        }

        return LoadAndScanAssembly(dllPath);
    }

    /// <summary>
    /// Scans a single assembly file, registers any new topic types, and returns the
    /// full list of DDS topic types found in the assembly (including types that were
    /// already registered under the same topic name).  Throws if the assembly cannot
    /// be loaded.
    /// </summary>
    public IReadOnlyList<TopicMetadata> DiscoverFromFileDetailed(string dllPath)
    {
        if (string.IsNullOrWhiteSpace(dllPath))
        {
            throw new ArgumentException("DLL path must not be empty.", nameof(dllPath));
        }

        return LoadAndScanAssemblyDetailed(dllPath);
    }

    private void TryDiscoverFromAssembly(string dllPath)
    {
        try
        {
            LoadAndScanAssembly(dllPath);
        }
        catch
        {
            // Ignore unreadable or incompatible assemblies.
        }
    }

    private int LoadAndScanAssembly(string dllPath)
    {
        return LoadAndScanAssemblyDetailed(dllPath).Count;
    }

    /// <summary>
    /// Loads an assembly, registers new topic types, and returns all DDS topic types
    /// found in the assembly.  For topic types that were already registered (detected by
    /// topic name), the existing <see cref="TopicMetadata"/> from the registry is returned
    /// so the caller always gets a fully resolved entry.
    /// </summary>
    private IReadOnlyList<TopicMetadata> LoadAndScanAssemblyDetailed(string dllPath)
    {
        var loadContext = new CollectiblePluginLoadContext(dllPath);
        var assembly = loadContext.LoadFromAssemblyPath(dllPath);

        _loadContexts.Add(loadContext);

        var found = new List<TopicMetadata>();
        foreach (var type in assembly.ExportedTypes)
        {
            if (type.GetCustomAttribute<DdsTopicAttribute>() == null)
            {
                continue;
            }

            var metadata = new TopicMetadata(type);
            _registry.Register(metadata);

            // Prefer the version already in the registry (registered first wins), so that
            // callers always receive a consistent TopicMetadata regardless of whether this
            // assembly introduced the topic or it was already known.
            var registered = _registry.GetByName(metadata.TopicName) ?? metadata;
            found.Add(registered);
        }

        return found;
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
            if (string.Equals(assemblyName.Name, SchemaAssemblyName, StringComparison.Ordinal) ||
                string.Equals(assemblyName.Name, RuntimeAssemblyName, StringComparison.Ordinal) ||
                string.Equals(assemblyName.Name, CoreAssemblyName, StringComparison.Ordinal))
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
