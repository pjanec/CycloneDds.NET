using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>Code-behind for <see cref="SamplesViewerView"/>.</summary>
public sealed partial class SamplesViewerView : UserControl
{
    public SamplesViewerView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
