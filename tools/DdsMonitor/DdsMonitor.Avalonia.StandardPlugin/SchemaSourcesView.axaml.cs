using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// Code-behind for <see cref="SchemaSourcesView"/>.
/// </summary>
public sealed partial class SchemaSourcesView : UserControl
{
    public SchemaSourcesView()
    {
        InitializeComponent();
    }

    private async void OnAddClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SchemaSourcesViewModel vm) return;

        var dialog = new OpenFileDialog
        {
            Title = "Select DDS Schema DLL",
            Filters = [new FileDialogFilter { Name = "DLL assemblies", Extensions = { "dll" } }],
            AllowMultiple = false,
        };

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window window)
        {
            var result = await dialog.ShowAsync(window);
            if (result is { Length: > 0 })
                vm.AddAssembly(result[0]);
        }
    }

    private void OnRemoveClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SchemaSourcesViewModel vm) return;

        var listBox = this.FindControl<ListBox>("EntriesList");
        if (listBox?.SelectedIndex >= 0)
            vm.RemoveAssembly(listBox.SelectedIndex);
    }
}
