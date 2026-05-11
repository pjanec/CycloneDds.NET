using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>Code-behind for <see cref="DetailInspectorView"/>.</summary>
public sealed partial class DetailInspectorView : UserControl
{
    public DetailInspectorView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
