using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DdsMonitor.Avalonia.Core;

namespace DdsMonitor.Avalonia;

/// <summary>
/// Delegates to <see cref="IAvaloniaViewRegistry"/> to resolve a <see cref="Control"/>
/// for a given ViewModel, following Avalonia's ViewLocator pattern.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    private readonly IAvaloniaViewRegistry _viewRegistry;

    public ViewLocator(IAvaloniaViewRegistry viewRegistry)
    {
        _viewRegistry = viewRegistry;
    }

    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        try
        {
            return _viewRegistry.BuildView(param);
        }
        catch (InvalidOperationException)
        {
            return new TextBlock { Text = $"No view registered for: {param.GetType().Name}" };
        }
    }

    public bool Match(object? data) => data is not null;
}
