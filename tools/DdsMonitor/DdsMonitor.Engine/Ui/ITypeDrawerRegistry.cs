using System;
using Microsoft.AspNetCore.Components;

namespace DdsMonitor.Engine.Ui;

/// <summary>
/// Provides a registry mapping CLR types to Blazor <see cref="RenderFragment{TValue}"/> builders
/// that render an appropriate user-input control for editing a value of that type.
/// </summary>
public interface ITypeDrawerRegistry
{
    /// <summary>
    /// Registers a drawer factory for the given <paramref name="type"/>.
    /// Replaces any previously registered drawer for that type.
    /// </summary>
    /// <param name="type">The CLR type to handle.</param>
    /// <param name="drawer">
    /// A <see cref="RenderFragment{DrawerContext}"/> that produces an interactive input
    /// element bound to the values provided in <see cref="DrawerContext"/>.
    /// </param>
    void Register(Type type, RenderFragment<DrawerContext> drawer);

    /// <summary>
    /// Returns the drawer factory registered for the given <paramref name="type"/>,
    /// or <c>null</c> if no drawer has been registered.
    /// </summary>
    RenderFragment<DrawerContext>? GetDrawer(Type type);

    /// <summary>
    /// Returns <c>true</c> when a drawer is registered for <paramref name="type"/>.
    /// </summary>
    bool HasDrawer(Type type);
}
