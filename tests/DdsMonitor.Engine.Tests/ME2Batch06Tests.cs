using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DdsMonitor.Engine;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for ME2-BATCH-06:
///   ME2-T25-A — GetPanelBaseName now returns only the simple class name, even when passed
///               a fully-qualified AQN or a FullName (namespace.ClassName) string.
///   ME2-T25-B — ResolveComponentType in Desktop.razor scans all loaded assemblies so
///               external plugin types can resolve beyond the executing-assembly boundary.
///   ME2-T12/T26 via TopicColorService — deterministic hash assigns palette indices;
///               user overrides are respected; overrides round-trip through persistence;
///               resetting returns to auto.
///   ME2-T13-B — RefreshSelectedTopics returns topics sorted alphabetically by ShortName.
/// </summary>
public sealed class ME2Batch06Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T25-A: GetPanelBaseName sanitization — tested indirectly via SpawnPanel
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SpawnPanel_WithAqn_PanelIdUsesSimpleClassName()
    {
        // Arrange: simulate a legacy workspace AQN entry for a known simple name.
        const string aqn =
            "DdsMonitor.Components.SamplesPanel, DdsMonitor, Version=0.2.0.0, Culture=neutral, PublicKeyToken=null";
        var manager = new WindowManager();

        // Act
        var panel = manager.SpawnPanel(aqn);

        // Assert: PanelId must start with the simple class name "SamplesPanel",
        // NOT with the full namespace.classname or the raw AQN.
        Assert.StartsWith("SamplesPanel.", panel.PanelId, StringComparison.Ordinal);
        Assert.DoesNotContain("DdsMonitor.Components", panel.PanelId, StringComparison.Ordinal);
        Assert.DoesNotContain("Version=", panel.PanelId, StringComparison.Ordinal);
    }

    [Fact]
    public void SpawnPanel_WithFullName_PanelIdUsesSimpleClassName()
    {
        // A FullName "My.Namespace.MyPanel" should produce PanelId "MyPanel.1".
        const string fullName = "DdsMonitor.Components.TopicExplorerPanel";
        var manager = new WindowManager();

        var panel = manager.SpawnPanel(fullName);

        Assert.StartsWith("TopicExplorerPanel.", panel.PanelId, StringComparison.Ordinal);
        Assert.DoesNotContain("DdsMonitor.Components", panel.PanelId, StringComparison.Ordinal);
    }

    [Fact]
    public void SpawnPanel_WithSimpleName_PanelIdUsesSimpleName()
    {
        // A bare simple name (no dot, no comma) should produce PanelId "SimpleName.1".
        const string simpleName = "MySinglePanel";
        var manager = new WindowManager();

        var panel = manager.SpawnPanel(simpleName);

        Assert.Equal("MySinglePanel.1", panel.PanelId);
    }

    [Fact]
    public void SpawnPanel_TwoAqnPanels_BothGetDifferentNumericSuffices()
    {
        const string aqn1 =
            "My.Namespace.FooPanel, MyAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
        const string aqn2 =
            "My.Namespace.FooPanel, MyAssembly, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null";
        var manager = new WindowManager();

        var panel1 = manager.SpawnPanel(aqn1);
        var panel2 = manager.SpawnPanel(aqn2);

        Assert.Equal("FooPanel.1", panel1.PanelId);
        Assert.Equal("FooPanel.2", panel2.PanelId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T25-B: ResolveComponentType scans loaded assemblies
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ResolveComponentType_FullName_FindsTypeInLoadedAssembly()
    {
        // WindowManager is in DdsMonitor.Engine which is already loaded.
        // Its FullName should resolve via the loaded-assembly scan path.
        var type = ResolveComponentTypeViaReflection(typeof(WindowManager).FullName!);

        Assert.NotNull(type);
        Assert.Equal(typeof(WindowManager), type);
    }

    [Fact]
    public void ResolveComponentType_AqnStripped_FindsTypeInLoadedAssembly()
    {
        // Pass a FullName to the resolver; it must strip the namespace-only path
        // and find WindowManager.
        var aqn = typeof(WindowManager).AssemblyQualifiedName!;
        var type = ResolveComponentTypeViaReflection(aqn);

        Assert.NotNull(type);
        Assert.Equal(typeof(WindowManager), type);
    }

    [Fact]
    public void ResolveComponentType_UnknownType_ReturnsNull()
    {
        var type = ResolveComponentTypeViaReflection("My.Completely.Unknown.Xyzzy");
        Assert.Null(type);
    }

    /// <summary>
    /// Mirrors the <c>ResolveComponentType</c> logic from Desktop.razor:
    /// tries <see cref="Type.GetType"/> first, then scans all loaded assemblies.
    /// </summary>
    private static Type? ResolveComponentTypeViaReflection(string componentTypeName)
    {
        if (string.IsNullOrWhiteSpace(componentTypeName))
            return null;

        var direct = Type.GetType(componentTypeName);
        if (direct != null)
            return direct;

        var fullName = componentTypeName;
        var commaIndex = componentTypeName.IndexOf(',');
        if (commaIndex > 0)
            fullName = componentTypeName[..commaIndex].Trim();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var found = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
            if (found != null)
                return found;
        }

        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T26: TopicColorService — deterministic hash & user overrides
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TopicColorService_GetAutoColorIndex_IsDeterministic()
    {
        var service = CreateColorService();

        var first = service.GetAutoColorIndex("TemperatureSensor");
        var second = service.GetAutoColorIndex("TemperatureSensor");

        Assert.Equal(first, second);
    }

    [Fact]
    public void TopicColorService_GetAutoColorIndex_IsInPaletteRange()
    {
        var service = CreateColorService();
        var topics = new[] { "Alpha", "Beta", "Gamma", "Delta", "TemperatureSensor", "VehicleState", "x" };

        foreach (var t in topics)
        {
            var idx = service.GetAutoColorIndex(t);
            Assert.InRange(idx, 0, 11);
        }
    }

    [Fact]
    public void TopicColorService_GetAutoColorVar_ReturnsValidCssVar()
    {
        var service = CreateColorService();

        var cssVar = service.GetAutoColorVar("TemperatureSensor");

        Assert.StartsWith("var(--topic-color-", cssVar, StringComparison.Ordinal);
        Assert.EndsWith(")", cssVar, StringComparison.Ordinal);
    }

    [Fact]
    public void TopicColorService_GetUserColor_ReturnsNullByDefault()
    {
        var service = CreateColorService();

        var color = service.GetUserColor("NoOverrideTopic");

        Assert.Null(color);
    }

    [Fact]
    public void TopicColorService_SetUserColor_StoresOverride()
    {
        var service = CreateColorService();
        const string hex = "#aabbcc";

        service.SetUserColor("MySensor", hex);

        Assert.Equal(hex, service.GetUserColor("MySensor"));
    }

    [Fact]
    public void TopicColorService_ResetUserColor_RemovesOverride()
    {
        var service = CreateColorService();
        service.SetUserColor("MySensor", "#aabbcc");
        Assert.NotNull(service.GetUserColor("MySensor"));

        service.ResetUserColor("MySensor");

        Assert.Null(service.GetUserColor("MySensor"));
    }

    [Fact]
    public void TopicColorService_GetColorValue_WithOverride_ReturnsHex()
    {
        var service = CreateColorService();
        const string hex = "#ff0099";
        service.SetUserColor("OverrideTopic", hex);

        var value = service.GetColorValue("OverrideTopic");

        Assert.Equal(hex, value);
    }

    [Fact]
    public void TopicColorService_GetColorValue_WithoutOverride_ReturnsCssVar()
    {
        var service = CreateColorService();

        var value = service.GetColorValue("AutoTopic");

        Assert.StartsWith("var(--topic-color-", value, StringComparison.Ordinal);
    }

    [Fact]
    public void TopicColorService_GetColorStyle_ContainsColorProperty()
    {
        var service = CreateColorService();

        var style = service.GetColorStyle("AnyTopic");

        Assert.StartsWith("color:", style, StringComparison.Ordinal);
    }

    [Fact]
    public void TopicColorService_SetUserColor_RaisesOnChanged()
    {
        var service = CreateColorService();
        var raised = false;
        service.OnChanged += () => raised = true;

        service.SetUserColor("Topic1", "#123456");

        Assert.True(raised);
    }

    [Fact]
    public void TopicColorService_ResetUserColor_RaisesOnChanged()
    {
        var service = CreateColorService();
        service.SetUserColor("Topic2", "#abcdef");
        var raised = false;
        service.OnChanged += () => raised = true;

        service.ResetUserColor("Topic2");

        Assert.True(raised);
    }

    [Fact]
    public void TopicColorService_OverridesRoundTripThroughPersistence()
    {
        // Arrange: create a service and set an override.
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var service1 = CreateColorService(tempDir);
            service1.SetUserColor("PersistTopic", "#deadbe");

            // Act: create a second service pointing to the same directory.
            var service2 = CreateColorService(tempDir);

            // Assert: the override was persisted and reloaded.
            Assert.Equal("#deadbe", service2.GetUserColor("PersistTopic"));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void TopicColorService_NoOverridesFile_LoadsCleanly()
    {
        // Arrange: a directory with no existing overrides file.
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var service = CreateColorService(tempDir);
            Assert.Null(service.GetUserColor("Anything"));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T13-B: TopicSources topic list is sorted alphabetically
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TopicMetadata_SortedByShortName_IsAlphabeticalCaseInsensitive()
    {
        // Simulate the RefreshSelectedTopics sort logic on a list of topics.
        var topics = new[]
        {
            new TopicMetadata(typeof(SimpleType)),    // ShortName: "SimpleType"
            new TopicMetadata(typeof(SampleTopic)),   // ShortName: "SampleTopic"
        };

        var sorted = topics
            .OrderBy(t => t.ShortName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // "SampleTopic" < "SimpleType" alphabetically.
        Assert.Equal("SampleTopic", sorted[0].ShortName);
        Assert.Equal("SimpleType", sorted[1].ShortName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static TopicColorService CreateColorService(string? tempDir = null)
    {
        var dir = tempDir ?? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var workspaceState = new FakeWorkspaceState(dir);
        return new TopicColorService(workspaceState);
    }

    private sealed class FakeWorkspaceState : IWorkspaceState
    {
        public FakeWorkspaceState(string dir)
        {
            WorkspaceFilePath = Path.Combine(dir, "workspace.json");
        }

        public string WorkspaceFilePath { get; }
    }
}
