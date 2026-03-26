using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using DdsMonitor.Engine;
using Xunit;

namespace DdsMonitor.Plugins.ECS.Tests;

/// <summary>
/// Tests for <see cref="EcsSettingsPersistenceService"/> workspace settings integration
/// (PLA1-P4-T04).
/// </summary>
public sealed class EcsSettingsPersistenceServiceTests
{
    private static EcsSettingsPersistenceService Create(EcsSettings settings, IEventBroker broker)
        => new(settings, broker);

    // ── Save via WorkspaceSavingEvent ─────────────────────────────────────────

    [Fact]
    public async Task OnWorkspaceSaving_WritesEcsSectionToPluginBag()
    {
        var broker = new EventBroker();
        var settings = new EcsSettings { NamespacePrefix = "com.example" };
        using var svc = Create(settings, broker);
        await svc.StartAsync(default);

        var bag = new Dictionary<string, object>(System.StringComparer.Ordinal);
        broker.Publish(new WorkspaceSavingEvent(bag));

        Assert.True(bag.ContainsKey("ECS"), "Plugin bag should contain 'ECS' key");
    }

    // ── Load via WorkspaceLoadedEvent ─────────────────────────────────────────

    [Fact]
    public async Task OnWorkspaceLoaded_RestoresSettingsFromPluginBag()
    {
        var broker = new EventBroker();
        var settings = new EcsSettings();
        using var svc = Create(settings, broker);
        await svc.StartAsync(default);

        // Simulate what WindowManager publishes: a JsonElement-typed value.
        var dto = new { NamespacePrefix = "restored.ns" };
        var json = JsonSerializer.Serialize(dto);
        using var doc = JsonDocument.Parse(json);
        var bag = new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["ECS"] = doc.RootElement.Clone()
        };
        broker.Publish(new WorkspaceLoadedEvent(bag));

        Assert.Equal("restored.ns", settings.NamespacePrefix);
    }

    [Fact]
    public async Task OnWorkspaceLoaded_WithEmptyBag_DoesNotThrow()
    {
        var broker = new EventBroker();
        var settings = new EcsSettings();
        using var svc = Create(settings, broker);
        await svc.StartAsync(default);

        var ex = Record.Exception(() =>
            broker.Publish(new WorkspaceLoadedEvent(
                new Dictionary<string, object>(System.StringComparer.Ordinal))));

        Assert.Null(ex);
    }

    // ── Save / load round-trip ────────────────────────────────────────────────

    [Fact]
    public async Task SaveLoadRoundTrip_ViaEventBroker()
    {
        var broker = new EventBroker();
        var settings = new EcsSettings
        {
            NamespacePrefix    = "test.ns",
            EntityIdPattern    = ".*Entity",
            PartIdPattern      = ".*Part",
            MasterTopicPattern = ".*Master",
        };
        using var svc = Create(settings, broker);
        await svc.StartAsync(default);

        // Simulate save.
        var bag = new Dictionary<string, object>(System.StringComparer.Ordinal);
        broker.Publish(new WorkspaceSavingEvent(bag));

        // Serialise the bag and deserialise it (mimics what WindowManager / STJ does).
        var roundTrippedJson = JsonSerializer.Serialize(bag);
        using var roundTrippedDoc = JsonDocument.Parse(roundTrippedJson);
        var ecsProp = roundTrippedDoc.RootElement.GetProperty("ECS");
        var loadBag = new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["ECS"] = ecsProp.Clone()
        };

        // Reset settings and simulate load.
        settings.NamespacePrefix    = string.Empty;
        settings.EntityIdPattern    = string.Empty;
        settings.PartIdPattern      = string.Empty;
        settings.MasterTopicPattern = string.Empty;
        broker.Publish(new WorkspaceLoadedEvent(loadBag));

        Assert.Equal("test.ns",   settings.NamespacePrefix);
        Assert.Equal(".*Entity",  settings.EntityIdPattern);
        Assert.Equal(".*Part",    settings.PartIdPattern);
        Assert.Equal(".*Master",  settings.MasterTopicPattern);
    }
}
