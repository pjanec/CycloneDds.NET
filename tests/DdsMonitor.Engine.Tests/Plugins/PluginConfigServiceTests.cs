using System;
using System.Collections.Generic;
using System.IO;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.Logging;

namespace DdsMonitor.Engine.Tests.Plugins;

/// <summary>
/// Unit tests for <see cref="PluginConfigService"/> (PLA1-P5-T01, DEBT-015).
/// </summary>
public sealed class PluginConfigServiceTests : IDisposable
{
    private readonly string _tempDir;

    public PluginConfigServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Load_WhenFileAbsent_ReturnsEmptySet()
    {
        // Pass a path to a workspace.json that does not exist.
        var workspacePath = Path.Combine(_tempDir, "workspace.json");
        var svc = new PluginConfigService(workspacePath);

        var result = svc.EnabledPlugins;

        Assert.Empty(result);
        Assert.False(svc.HadConfigFileAtInitialization);
    }

    [Fact]
    public void Load_WhenFileCorrupt_ReturnsEmptySet()
    {
        // Write a corrupt workspace.json.
        var workspacePath = Path.Combine(_tempDir, "workspace.json");
        File.WriteAllText(workspacePath, "{ this is not valid json !!!");

        var svc = new PluginConfigService(workspacePath);

        var result = svc.EnabledPlugins;

        Assert.Empty(result);
        // DEBT-010: corrupt file is treated like a missing file — all plugins enabled.
        Assert.False(svc.HadConfigFileAtInitialization);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        // Pre-populate a workspace.json with enabled plugins in the expected format.
        var workspacePath = Path.Combine(_tempDir, "workspace.json");
        var workspaceJson = "{\"PluginSettings\":{\"EnabledPlugins\":[\"PluginA\",\"PluginB\"]}}";
        File.WriteAllText(workspacePath, workspaceJson);

        var svc = new PluginConfigService(workspacePath);

        Assert.True(svc.HadConfigFileAtInitialization);
        Assert.Equal(2, svc.EnabledPlugins.Count);
        Assert.Contains("PluginA", svc.EnabledPlugins);
        Assert.Contains("PluginB", svc.EnabledPlugins);

        // Save updates the in-memory state.
        svc.Save(new HashSet<string>(StringComparer.Ordinal) { "PluginA", "PluginB", "PluginC" });
        Assert.True(svc.HadConfigFileAtInitialization);
        Assert.Equal(3, svc.EnabledPlugins.Count);
        Assert.Contains("PluginC", svc.EnabledPlugins);
    }

    // ── DEBT-015: warning logged when file is corrupt ───────────────────

    [Fact]
    public void Load_WhenFileCorrupt_LogsWarning()
    {
        var workspacePath = Path.Combine(_tempDir, "workspace-corrupt-warn.json");
        File.WriteAllText(workspacePath, "NOT_VALID_JSON!!!");

        var logger = new FakeLogger<PluginConfigService>();
        var svc = new PluginConfigService(workspacePath, logger);

        // Corrupt file should be silently handled but a warning logged.
        Assert.Empty(svc.EnabledPlugins);
        Assert.False(svc.HadConfigFileAtInitialization);
        Assert.Single(logger.Warnings);
        Assert.Contains("could not be parsed", logger.Warnings[0], StringComparison.OrdinalIgnoreCase);
    }

    // ── Migration from legacy enabled-plugins.json ───────────────────────

    [Fact]
    public void Load_MigratesFromLegacyEnabledPluginsFile()
    {
        // Write a legacy enabled-plugins.json (plain JSON array of strings).
        var legacyFilePath = Path.Combine(_tempDir, "enabled-plugins.json");
        File.WriteAllText(legacyFilePath, "[\"LegacyPlugin1\",\"LegacyPlugin2\"]");

        // workspace.json does NOT exist — service should fall back to legacy file.
        var workspacePath = Path.Combine(_tempDir, "workspace.json");

        var svc = new PluginConfigService(workspacePath);

        Assert.True(svc.HadConfigFileAtInitialization);
        Assert.Equal(2, svc.EnabledPlugins.Count);
        Assert.Contains("LegacyPlugin1", svc.EnabledPlugins);
        Assert.Contains("LegacyPlugin2", svc.EnabledPlugins);
    }

    [Fact]
    public void Save_UpdatesInMemoryStateAndSetsHadConfig()
    {
        var workspacePath = Path.Combine(_tempDir, "workspace.json");
        var svc = new PluginConfigService(workspacePath);

        Assert.False(svc.HadConfigFileAtInitialization);

        svc.Save(new HashSet<string>(StringComparer.Ordinal) { "MyPlugin" });

        Assert.True(svc.HadConfigFileAtInitialization);
        Assert.Contains("MyPlugin", svc.EnabledPlugins);
    }

    [Fact]
    public void Save_PublishesWorkspaceSaveRequestedEvent_WhenBrokerAvailable()
    {
        var workspacePath = Path.Combine(_tempDir, "workspace.json");
        var svc = new PluginConfigService(workspacePath);

        var broker = new SimpleEventBroker();
        svc.SetupPersistence(broker);

        var eventFired = false;
        broker.Subscribe<WorkspaceSaveRequestedEvent>(_ => eventFired = true);

        svc.Save(new HashSet<string>(StringComparer.Ordinal) { "Plugin1" });

        Assert.True(eventFired);
    }

    [Fact]
    public void WorkspaceSavingEvent_IncludesEnabledPlugins()
    {
        var workspacePath = Path.Combine(_tempDir, "workspace.json");
        var svc = new PluginConfigService(workspacePath);
        svc.Save(new HashSet<string>(StringComparer.Ordinal) { "APlugin", "BPlugin" });

        var broker = new SimpleEventBroker();
        svc.SetupPersistence(broker);

        var bag = new Dictionary<string, object>(StringComparer.Ordinal);
        broker.Publish(new WorkspaceSavingEvent(bag));

        Assert.True(bag.ContainsKey("EnabledPlugins"));
        var saved = bag["EnabledPlugins"] as HashSet<string>;
        Assert.NotNull(saved);
        Assert.Contains("APlugin", saved!);
        Assert.Contains("BPlugin", saved);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal <see cref="ILogger{T}"/> that collects warning messages for assertion.
    /// Used because the Engine.Tests project has no mocking library.
    /// </summary>
    private sealed class FakeLogger<T> : ILogger<T>
    {
        public readonly List<string> Warnings = new();

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        bool ILogger.IsEnabled(LogLevel logLevel) => true;

        void ILogger.Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
                Warnings.Add(formatter(state, exception));
        }
    }

    /// <summary>
    /// Minimal synchronous <see cref="IEventBroker"/> for unit tests.
    /// </summary>
    private sealed class SimpleEventBroker : IEventBroker
    {
        private readonly List<(Type EventType, Delegate Handler)> _handlers = new();

        public void Publish<TEvent>(TEvent eventMessage)
        {
            foreach (var (type, handler) in _handlers)
            {
                if (type == typeof(TEvent))
                    ((Action<TEvent>)handler)(eventMessage);
            }
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
