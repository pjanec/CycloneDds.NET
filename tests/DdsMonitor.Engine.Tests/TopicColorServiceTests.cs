using System;
using System.Collections.Generic;
using System.IO;
using DdsMonitor.Engine;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Unit tests for <see cref="TopicColorService.RegisterColorRule"/> and
/// <see cref="TopicColorService.GetEffectiveColor"/> (PLA1-P6-T03).
/// </summary>
public sealed class TopicColorServiceTests : IDisposable
{
    private readonly string _tempDir;

    public TopicColorServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private TopicColorService Create() =>
        new(new FakeWorkspaceState(_tempDir));

    // ── PLA1-P6-T03 tests ─────────────────────────────────────────────────

    [Fact]
    public void RegisterColorRule_OverridesAutoColor()
    {
        var service = Create();

        service.RegisterColorRule(name =>
            name == "ErrorTopic" ? "#FF0000" : null);

        var result = service.GetEffectiveColor("ErrorTopic");

        Assert.Equal("#FF0000", result);
    }

    [Fact]
    public void RegisterColorRule_ReturningNull_FallsBackToAutoPalette()
    {
        var service = Create();

        service.RegisterColorRule(_ => null);

        var result = service.GetEffectiveColor("NormalTopic");

        // Auto palette returns a CSS variable, not a specific hex value.
        Assert.StartsWith("var(--topic-color-", result, StringComparison.Ordinal);
    }

    [Fact]
    public void UserOverride_TakesPrecedenceOverRule()
    {
        var service = Create();

        // Rule would return #FF0000…
        service.RegisterColorRule(_ => "#FF0000");

        // …but user has explicitly overridden to #00FF00.
        service.SetUserColor("OverrideTopic", "#00FF00");

        var result = service.GetEffectiveColor("OverrideTopic");

        Assert.Equal("#00FF00", result);
    }

    [Fact]
    public void RegisterColorRule_TwoRules_FirstNull_SecondReturnsColor()
    {
        // DEBT-014: verify that a null result from the first rule is skipped
        // and the second rule's non-null value is used.
        var service = Create();

        service.RegisterColorRule(_ => null);           // first rule: always defer
        service.RegisterColorRule(_ => "#123456");      // second rule: always wins

        var result = service.GetEffectiveColor("AnyTopic");

        Assert.Equal("#123456", result);
    }

    // ── Workspace.json persistence tests ─────────────────────────────────

    [Fact]
    public void LoadsColorOverridesFromWorkspaceJson()
    {
        // Pre-populate workspace.json with a TopicColors section.
        var workspaceJson = "{\"PluginSettings\":{\"TopicColors\":{\"MyTopic\":\"#aabbcc\"}}}";
        File.WriteAllText(Path.Combine(_tempDir, "workspace.json"), workspaceJson);

        var service = Create();

        Assert.Equal("#aabbcc", service.GetUserColor("MyTopic"));
    }

    [Fact]
    public void MigratesFromLegacyTopicColorsFile()
    {
        // No workspace.json, but topic-colors.json exists (legacy).
        var legacyJson = "{\"LegacyTopic\":\"#112233\"}";
        File.WriteAllText(Path.Combine(_tempDir, "topic-colors.json"), legacyJson);

        var service = Create();

        Assert.Equal("#112233", service.GetUserColor("LegacyTopic"));
    }

    [Fact]
    public void SetUserColor_PublishesWorkspaceSaveRequestedEvent()
    {
        var broker = new SimpleEventBroker();
        var service = new TopicColorService(new FakeWorkspaceState(_tempDir), broker);

        var eventCount = 0;
        broker.Subscribe<WorkspaceSaveRequestedEvent>(_ => eventCount++);

        service.SetUserColor("Topic1", "#ff0000");

        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void WorkspaceSavingEvent_IncludesColorOverrides()
    {
        var broker = new SimpleEventBroker();
        var service = new TopicColorService(new FakeWorkspaceState(_tempDir), broker);
        service.SetUserColor("SavedTopic", "#cafeba");

        var bag = new Dictionary<string, object>(StringComparer.Ordinal);
        broker.Publish(new WorkspaceSavingEvent(bag));

        Assert.True(bag.ContainsKey("TopicColors"));
    }

    // ── Helper ────────────────────────────────────────────────────────────

    private sealed class FakeWorkspaceState : IWorkspaceState
    {
        public FakeWorkspaceState(string dir)
            => WorkspaceFilePath = Path.Combine(dir, "workspace.json");

        public string WorkspaceFilePath { get; }
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
