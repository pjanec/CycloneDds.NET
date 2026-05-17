using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DdsMonitor.Engine;
using DdsMonitor.Engine.AssemblyScanner;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Unit tests for <see cref="AssemblySourcePersistenceService"/> — ensures that
/// assembly source paths round-trip through workspace save/load events.
/// </summary>
public sealed class AssemblySourcePersistenceServiceTests : IDisposable
{
    private readonly string _tempDir;

    public AssemblySourcePersistenceServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task StartAsync_SubscribesToWorkspaceEvents()
    {
        var (service, broker, _) = CreateService();

        await service.StartAsync(CancellationToken.None);

        // After start, subscribing to WorkspaceSavingEvent should be wired.
        var bag = new Dictionary<string, object>(StringComparer.Ordinal);
        broker.Publish(new WorkspaceSavingEvent(bag)); // should not throw

        service.Dispose();
    }

    [Fact]
    public async Task WorkspaceSavingEvent_WritesCurrentPaths()
    {
        var (service, broker, assemblySource) = CreateService();
        await service.StartAsync(CancellationToken.None);

        // Add a path to the service.
        var fakePath = Path.Combine(_tempDir, "fake.dll");
        File.WriteAllText(fakePath, string.Empty); // create empty file so path is "valid"
        assemblySource.Add(fakePath);

        // Simulate workspace save.
        var bag = new Dictionary<string, object>(StringComparer.Ordinal);
        broker.Publish(new WorkspaceSavingEvent(bag));

        Assert.True(bag.ContainsKey("AssemblySources"));
        var paths = bag["AssemblySources"] as List<string>;
        Assert.NotNull(paths);

        service.Dispose();
    }

    [Fact]
    public async Task WorkspaceLoadedEvent_ReloadsFromPaths()
    {
        var (service, broker, assemblySource) = CreateService();
        await service.StartAsync(CancellationToken.None);

        // Initially empty.
        Assert.Empty(assemblySource.Entries);

        // Simulate workspace load with an empty list (no real DLLs needed for path reload).
        var loadedSettings = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["AssemblySources"] = new List<string> { "/nonexistent/path1.dll" }
        };
        broker.Publish(new WorkspaceLoadedEvent(loadedSettings));

        // After reload, the entry was added (even if the DLL doesn't exist, it will appear
        // with a load error, but the entry is still created).
        Assert.Equal(1, assemblySource.Entries.Count);

        service.Dispose();
    }

    [Fact]
    public async Task AssemblySourceChanged_PublishesWorkspaceSaveRequestedEvent()
    {
        var (service, broker, assemblySource) = CreateService();
        await service.StartAsync(CancellationToken.None);

        var saveRequestCount = 0;
        broker.Subscribe<WorkspaceSaveRequestedEvent>(_ => saveRequestCount++);

        // Trigger Add (which fires Changed event).
        var fakePath = Path.Combine(_tempDir, "trigger.dll");
        File.WriteAllText(fakePath, string.Empty);
        assemblySource.Add(fakePath);

        Assert.Equal(1, saveRequestCount);

        service.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private (AssemblySourcePersistenceService Service, SimpleEventBroker Broker, AssemblySourceService AssemblySource) CreateService()
    {
        var registry = new TopicRegistry();
        var discoveryService = new TopicDiscoveryService(registry);
        var assemblySource = new AssemblySourceService(registry, discoveryService, configFilePath: Path.Combine(_tempDir, "assembly-sources.json"));
        var broker = new SimpleEventBroker();
        var service = new AssemblySourcePersistenceService(assemblySource, broker);
        return (service, broker, assemblySource);
    }

    private sealed class SimpleEventBroker : IEventBroker
    {
        private readonly List<(Type EventType, Delegate Handler)> _handlers = new();

        public void Publish<TEvent>(TEvent eventMessage)
        {
            foreach (var (type, handler) in _handlers)
                if (type == typeof(TEvent)) ((Action<TEvent>)handler)(eventMessage);
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        {
            var entry = (typeof(TEvent), (Delegate)handler);
            _handlers.Add(entry);
            return new Unsubscriber(() => _handlers.Remove(entry));
        }

        private sealed class Unsubscriber : IDisposable
        {
            private readonly Action _action;
            public Unsubscriber(Action action) => _action = action;
            public void Dispose() => _action();
        }
    }
}
