using System;
using DdsMonitor.Engine;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Unit tests for <see cref="FilterMacroRegistry"/> (PLA1-P6-T08).
/// </summary>
public sealed class FilterMacroRegistryTests
{
    [Fact]
    public void GetMacros_WhenEmpty_ReturnsEmptyDictionary()
    {
        var registry = new FilterMacroRegistry();

        var result = registry.GetMacros();

        Assert.Empty(result);
    }

    [Fact]
    public void RegisterMacro_ThenGetMacros_ContainsMacro()
    {
        var registry = new FilterMacroRegistry();
        registry.RegisterMacro("Square", args => Convert.ToDouble(args[0]) * Convert.ToDouble(args[0]));

        var macros = registry.GetMacros();

        Assert.True(macros.ContainsKey("Square"));
    }

    [Fact]
    public void RegisterMacro_SameName_ReplacesExistingImplementation()
    {
        var registry = new FilterMacroRegistry();
        registry.RegisterMacro("Fn", _ => (object?)1);
        registry.RegisterMacro("Fn", _ => (object?)2);

        var macros = registry.GetMacros();
        var result = macros["Fn"](Array.Empty<object?>());

        Assert.Equal((object?)2, result);
        Assert.Single(macros);
    }

    [Fact]
    public void RegisterMacro_MultipleMacros_AllPresentInGetMacros()
    {
        var registry = new FilterMacroRegistry();
        registry.RegisterMacro("A", _ => null);
        registry.RegisterMacro("B", _ => null);
        registry.RegisterMacro("C", _ => null);

        var macros = registry.GetMacros();

        Assert.Equal(3, macros.Count);
        Assert.True(macros.ContainsKey("A"));
        Assert.True(macros.ContainsKey("B"));
        Assert.True(macros.ContainsKey("C"));
    }
}
