namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Allows plugins to register items in the application's top menu bar.
/// Menus are described as a hierarchical tree using slash-delimited paths.
/// </summary>
public interface IMenuRegistry
{
    /// <summary>
    /// Adds a synchronous menu item.
    /// </summary>
    /// <param name="menuPath">
    /// Slash-delimited path describing the parent hierarchy, e.g. <c>"Plugins/ECS"</c>.
    /// Use an empty string or <c>null</c> to place the item at the top level.
    /// </param>
    /// <param name="label">Display label for the leaf menu item.</param>
    /// <param name="onClick">Callback invoked when the item is clicked.</param>
    void AddMenuItem(string menuPath, string label, Action onClick);

    /// <summary>
    /// Adds an asynchronous menu item.
    /// </summary>
    /// <param name="menuPath">
    /// Slash-delimited path describing the parent hierarchy, e.g. <c>"Plugins/ECS"</c>.
    /// Use an empty string or <c>null</c> to place the item at the top level.
    /// </param>
    /// <param name="label">Display label for the leaf menu item.</param>
    /// <param name="onClickAsync">Async callback invoked when the item is clicked.</param>
    void AddMenuItem(string menuPath, string label, Func<Task> onClickAsync);

    /// <summary>Gets the top-level menu nodes, each potentially containing child nodes.</summary>
    IReadOnlyList<MenuNode> GetTopLevelMenus();

    /// <summary>Raised when the menu tree changes.</summary>
    event Action? Changed;
}
