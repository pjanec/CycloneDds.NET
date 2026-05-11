using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DdsMonitor.Avalonia.StandardPlugin;

public partial class SendSampleView : UserControl
{
    public SendSampleView()
    {
        InitializeComponent();
    }

    private void OnSendClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SendSampleViewModel vm)
            vm.Send();
    }
}
