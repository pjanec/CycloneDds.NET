using System;
using System.Collections.Concurrent;
using Avalonia.Controls;

namespace DdsMonitor.Avalonia.Core;

/// <summary>
/// Thread-safe implementation of <see cref="IAvaloniaViewRegistry"/>.
/// </summary>
public sealed class AvaloniaViewRegistry : IAvaloniaViewRegistry
{
    private readonly ConcurrentDictionary<Type, Func<object, Control>> _factories = new();

    /// <inheritdoc/>
    public void Register<TViewModel>(Func<TViewModel, Control> viewFactory)
    {
        if (viewFactory == null) throw new ArgumentNullException(nameof(viewFactory));
        _factories[typeof(TViewModel)] = vm => viewFactory((TViewModel)vm);
    }

    /// <inheritdoc/>
    public Control BuildView(object viewModel)
    {
        if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));

        if (_factories.TryGetValue(viewModel.GetType(), out var factory))
            return factory(viewModel);

        throw new InvalidOperationException(
            $"No Avalonia view registered for ViewModel type '{viewModel.GetType().FullName}'. " +
            $"Call IAvaloniaViewRegistry.Register<{viewModel.GetType().Name}>(...) first.");
    }
}
