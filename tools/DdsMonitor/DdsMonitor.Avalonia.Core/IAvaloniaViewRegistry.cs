using System;
using Avalonia.Controls;

namespace DdsMonitor.Avalonia.Core;

/// <summary>
/// Maps ViewModel instances to Avalonia <see cref="Control"/> instances.
/// </summary>
public interface IAvaloniaViewRegistry
{
    /// <summary>
    /// Registers a factory that creates a <see cref="Control"/> for instances of
    /// <typeparamref name="TViewModel"/>.
    /// </summary>
    void Register<TViewModel>(Func<TViewModel, Control> viewFactory);

    /// <summary>
    /// Builds a <see cref="Control"/> for the supplied <paramref name="viewModel"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no factory is registered for the runtime type of <paramref name="viewModel"/>.
    /// </exception>
    Control BuildView(object viewModel);
}
