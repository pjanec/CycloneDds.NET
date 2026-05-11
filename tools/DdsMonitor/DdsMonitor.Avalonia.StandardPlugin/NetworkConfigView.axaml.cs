using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DdsMonitor.Avalonia.StandardPlugin;

public partial class NetworkConfigView : UserControl
{
    public NetworkConfigView()
    {
        InitializeComponent();
    }

    private void OnAddRowClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NetworkConfigViewModel vm)
            vm.AddRow();
    }

    private void OnRemoveSelectedClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NetworkConfigViewModel vm)
        {
            var selectedIndex = ParticipantList.SelectedIndex;
            if (selectedIndex >= 0)
                vm.RemoveRow(selectedIndex);
        }
    }

    private void OnApplyClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NetworkConfigViewModel vm)
            vm.Apply();
    }
}
