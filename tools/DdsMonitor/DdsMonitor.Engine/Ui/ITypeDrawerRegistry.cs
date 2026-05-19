using System;

namespace DdsMonitor.Engine.Ui;

/// <summary>
/// Provides a registry mapping CLR types to UI-agnostic drawer factories
/// that produce an appropriate user-input control for editing a value of that type.
/// The concrete control type is determined by the host UI layer that registers the factory.
/// </summary>
public interface ITypeDrawerRegistry
{
    /// <summary>
    /// Registers a drawer factory for the given <paramref name="type"/>.
    /// Replaces any previously registered drawer for that type.
    /// </summary>
    /// <param name="type">The CLR type to handle.</param>
    /// <param name="drawer">
    /// A factory that accepts a <see cref="DrawerContext"/> and returns the UI control object
    /// The returned object is interpreted by the host UI layer.
    /// </param>
    void Register(Type type, Func<DrawerContext, object?> drawer);

    /// <summary>
    /// Returns the drawer factory registered for the given <paramref name="type"/>,
    /// or <c>null</c> if no drawer has been registered.
    /// </summary>
    Func<DrawerContext, object?>? GetDrawer(Type type);

    /// <summary>
    /// Returns <c>true</c> when a drawer is registered for <paramref name="type"/>.
    /// </summary>
    bool HasDrawer(Type type);
}
