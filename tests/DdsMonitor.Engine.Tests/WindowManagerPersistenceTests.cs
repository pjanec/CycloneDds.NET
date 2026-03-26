using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for <see cref="WindowManager"/> workspace save/load event integration
/// (PLA1-P4-T03).
/// </summary>
public sealed class WindowManagerPersistenceTests
{
    // ── Save ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Save_PublishesWorkspaceSavingEvent()
    {
        var broker = new EventBroker();
        var manager = new WindowManager(broker);
        WorkspaceSavingEvent? received = null;
        using var sub = broker.Subscribe<WorkspaceSavingEvent>(e => received = e);

        manager.SaveWorkspaceToJson();

        Assert.NotNull(received);
        Assert.NotNull(received!.PluginSettings);
    }

    [Fact]
    public void Save_IncludesPluginDataInJson()
    {
        var broker = new EventBroker();
        var manager = new WindowManager(broker);
        using var sub = broker.Subscribe<WorkspaceSavingEvent>(e => e.PluginSettings["Test"] = 42);

        var json = manager.SaveWorkspaceToJson();

        Assert.Contains("\"PluginSettings\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Test\"", json, StringComparison.Ordinal);
        Assert.Contains("42", json, StringComparison.Ordinal);
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Load_PublishesWorkspaceLoadedEvent()
    {
        var broker = new EventBroker();
        var manager = new WindowManager(broker);
        const string json = "{\"Panels\":[],\"PluginSettings\":{\"Key\":\"Val\"}}";
        WorkspaceLoadedEvent? received = null;
        using var sub = broker.Subscribe<WorkspaceLoadedEvent>(e => received = e);

        manager.LoadWorkspaceFromJson(json);

        Assert.NotNull(received);
        Assert.True(received!.PluginSettings.ContainsKey("Key"));
        // System.Text.Json deserializes object values as JsonElement.
        var val = received.PluginSettings["Key"];
        var strVal = val is System.Text.Json.JsonElement elem ? elem.GetString() : val?.ToString();
        Assert.Equal("Val", strVal);
    }

    [Fact]
    public void Load_WithNoPluginSettings_PublishesEmptyDictionary()
    {
        var broker = new EventBroker();
        var manager = new WindowManager(broker);
        const string json = "{\"Panels\":[]}";
        WorkspaceLoadedEvent? received = null;
        using var sub = broker.Subscribe<WorkspaceLoadedEvent>(e => received = e);

        manager.LoadWorkspaceFromJson(json);

        Assert.NotNull(received);
        Assert.Empty(received!.PluginSettings);
    }
}
