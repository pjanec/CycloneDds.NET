using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DdsMonitor.Engine.Plugins;

namespace DdsMonitor.Engine.Tests.Plugins;

/// <summary>
/// Regression tests for ContextMenuComposer (PLA1-DEBT-004).
/// Verifies the "defaults first, optional separator, then plugin items" ordering contract.
/// </summary>
public sealed class ContextMenuComposerTests
{
    private static ContextMenuItem Item(string label) =>
        new(label, null, () => Task.CompletedTask);

    private static List<ContextMenuItem> DefaultItems() =>
        new() { Item("Default A"), Item("Default B") };

    // ── No plugin items ────────────────────────────────────────────────────────

    [Fact]
    public void Compose_WhenNoPluginItems_ReturnsOnlyDefaults()
    {
        var registry = new ContextMenuRegistry();
        var defaults = DefaultItems();

        var result = ContextMenuComposer.Compose(defaults, registry, (SampleData)null!);

        Assert.Equal(2, result.Count);
        Assert.Equal("Default A", result[0].Label);
        Assert.Equal("Default B", result[1].Label);
    }

    [Fact]
    public void Compose_WhenNoPluginItems_DoesNotAddSeparator()
    {
        var registry = new ContextMenuRegistry();

        var result = ContextMenuComposer.Compose(DefaultItems(), registry, (SampleData)null!);

        Assert.DoesNotContain(result, item => item.Label.StartsWith('─'));
    }

    // ── Plugin items present ───────────────────────────────────────────────────

    [Fact]
    public void Compose_DefaultItemsAppearFirst()
    {
        var registry = new ContextMenuRegistry();
        registry.RegisterProvider<SampleData>(_ => new[] { Item("Plugin X") });

        var result = ContextMenuComposer.Compose(DefaultItems(), registry, (SampleData)null!);

        Assert.Equal("Default A", result[0].Label);
        Assert.Equal("Default B", result[1].Label);
    }

    [Fact]
    public void Compose_SeparatorAppearsBeforePluginItems()
    {
        var registry = new ContextMenuRegistry();
        registry.RegisterProvider<SampleData>(_ => new[] { Item("Plugin X") });

        var result = ContextMenuComposer.Compose(DefaultItems(), registry, (SampleData)null!);

        // Separator must appear directly after the defaults
        Assert.True(result[2].Label.StartsWith('─'), "Third item must be the separator.");
        Assert.Equal("Plugin X", result[3].Label);
    }

    [Fact]
    public void Compose_PluginItemsAppearAfterSeparator()
    {
        var registry = new ContextMenuRegistry();
        registry.RegisterProvider<SampleData>(_ => new[] { Item("P1"), Item("P2") });

        var result = ContextMenuComposer.Compose(DefaultItems(), registry, (SampleData)null!);

        var separatorIndex = result.FindIndex(i => i.Label.StartsWith('─'));
        var pluginLabels = result.Skip(separatorIndex + 1).Select(i => i.Label).ToList();
        Assert.Equal(new[] { "P1", "P2" }, pluginLabels);
    }

    [Fact]
    public void Compose_MultipleProviders_AllItemsAfterSeparator()
    {
        var registry = new ContextMenuRegistry();
        registry.RegisterProvider<SampleData>(_ => new[] { Item("P1") });
        registry.RegisterProvider<SampleData>(_ => new[] { Item("P2"), Item("P3") });

        var result = ContextMenuComposer.Compose(DefaultItems(), registry, (SampleData)null!);

        // 2 defaults + separator + 3 plugin items = 6
        Assert.Equal(6, result.Count);
        var separatorIndex = result.FindIndex(i => i.Label.StartsWith('─'));
        Assert.Equal(2, separatorIndex); // separator right after defaults
    }

    // ── Guard: plugin items are NEVER prepended ─────────────────────────────────

    [Fact]
    public void Compose_PluginItems_AreNeverPrependedBeforeDefaults()
    {
        var registry = new ContextMenuRegistry();
        registry.RegisterProvider<SampleData>(_ => new[] { Item("Plugin First?") });
        var defaults = DefaultItems();

        var result = ContextMenuComposer.Compose(defaults, registry, (SampleData)null!);

        // Plugin item must NOT appear at index 0 or 1
        Assert.NotEqual("Plugin First?", result[0].Label);
        Assert.NotEqual("Plugin First?", result[1].Label);
    }

    // ── Exactly one separator ───────────────────────────────────────────────────

    [Fact]
    public void Compose_WithPluginItems_HasExactlyOneSeparator()
    {
        var registry = new ContextMenuRegistry();
        registry.RegisterProvider<SampleData>(_ => new[] { Item("P1") });
        registry.RegisterProvider<SampleData>(_ => new[] { Item("P2") });

        var result = ContextMenuComposer.Compose(DefaultItems(), registry, (SampleData)null!);

        Assert.Equal(1, result.Count(i => i.Label.StartsWith('─')));
    }
}
