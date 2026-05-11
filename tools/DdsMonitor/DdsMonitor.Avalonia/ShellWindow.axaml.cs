using Avalonia.Controls;
using Avalonia.Interactivity;
using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Plugins;

namespace DdsMonitor.Avalonia;

public sealed partial class ShellWindow : Window
{
    private readonly IMenuRegistry _menuRegistry;
    private readonly IToolbarRegistry _toolbarRegistry;
    private readonly IDdsBridge _ddsBridge;

    public ShellWindow(
        IMenuRegistry menuRegistry,
        IToolbarRegistry toolbarRegistry,
        IDdsBridge ddsBridge)
    {
        _menuRegistry = menuRegistry;
        _toolbarRegistry = toolbarRegistry;
        _ddsBridge = ddsBridge;

        InitializeComponent();

        // Build menu now and subscribe for future changes
        RebuildMenu();
        _menuRegistry.Changed += RebuildMenu;

        // Build toolbar now and subscribe for future changes
        RebuildToolbar();
        _toolbarRegistry.Changed += RebuildToolbar;
    }

    // ── Menu ─────────────────────────────────────────────────────────────────

    private void RebuildMenu()
    {
        TopMenu.Items.Clear();

        // Built-in File menu
        var fileMenu = new MenuItem { Header = "File" };
        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => Close();
        fileMenu.Items.Add(exitItem);
        TopMenu.Items.Add(fileMenu);

        // Plugin-contributed menus
        foreach (var node in _menuRegistry.GetTopLevelMenus())
        {
            TopMenu.Items.Add(BuildMenuItem(node));
        }
    }

    private static MenuItem BuildMenuItem(MenuNode node)
    {
        var item = new MenuItem { Header = node.Label };

        if (node.IsLeaf)
        {
            item.Click += (_, _) =>
            {
                if (node.OnClickAsync is not null)
                {
                    _ = node.OnClickAsync();
                }
                else
                {
                    node.OnClick?.Invoke();
                }
            };
        }
        else
        {
            foreach (var child in node.Children)
            {
                item.Items.Add(BuildMenuItem(child));
            }
        }

        return item;
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private void RebuildToolbar()
    {
        Toolbar.Children.Clear();

        foreach (var entry in _toolbarRegistry.Entries)
        {
            var btn = new Button
            {
                Content = entry.Tooltip,
                Tag = entry.Id,
            };
            btn.Click += (_, _) => entry.Action();
            Toolbar.Children.Add(btn);
        }
    }

    // ── Transport controls ────────────────────────────────────────────────────

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        _ddsBridge.IsPaused = false;
    }

    private void OnPauseClick(object? sender, RoutedEventArgs e)
    {
        _ddsBridge.IsPaused = true;
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        _ddsBridge.ResetAll();
    }
}
