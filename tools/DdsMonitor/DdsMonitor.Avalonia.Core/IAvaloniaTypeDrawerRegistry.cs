using System;
using Avalonia.Controls;

namespace DdsMonitor.Avalonia.Core;

/// <summary>
/// Maps CLR types to Avalonia <see cref="Control"/> factories for in-place field editing.
/// </summary>
public interface IAvaloniaTypeDrawerRegistry
{
    /// <summary>
    /// Registers a factory that produces a <see cref="Control"/> for the specified type.
    /// The factory result <b>must</b> be an Avalonia <see cref="Control"/>; if it is not,
    /// <see cref="Build"/> will throw <see cref="InvalidCastException"/>.
    /// </summary>
    void Register(Type type, Func<AvaloniaDrawerContext, object> factory);

    /// <summary>
    /// Builds the editing control for the given context.
    /// </summary>
    /// <returns>A non-null <see cref="Control"/>.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the registered factory returned an object that is not an Avalonia
    /// <see cref="Control"/>.
    /// </exception>
    Control Build(AvaloniaDrawerContext ctx);
}
