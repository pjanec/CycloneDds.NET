namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// A node in the application's hierarchical plugin menu tree.
/// Leaf nodes carry click callbacks; branch nodes carry children.
/// </summary>
public sealed class MenuNode
{
    private readonly List<MenuNode> _children = new();

    /// <summary>Initialises a new menu node with the given display label.</summary>
    public MenuNode(string label)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
    }

    /// <summary>Gets the display label.</summary>
    public string Label { get; }

    /// <summary>Gets the child nodes. Empty for leaf nodes.</summary>
    public IReadOnlyList<MenuNode> Children => _children;

    /// <summary>Gets or sets the synchronous click handler (leaf nodes only).</summary>
    public Action? OnClick { get; set; }

    /// <summary>Gets or sets the asynchronous click handler (leaf nodes only).</summary>
    public Func<Task>? OnClickAsync { get; set; }

    /// <summary>
    /// Gets a value indicating whether this is a clickable leaf node
    /// (i.e. it has at least one click handler).
    /// </summary>
    public bool IsLeaf => OnClick != null || OnClickAsync != null;

    /// <summary>
    /// Returns the child with the given label, creating it if it does not exist.
    /// </summary>
    internal MenuNode GetOrAddChild(string label)
    {
        foreach (var child in _children)
        {
            if (string.Equals(child.Label, label, StringComparison.Ordinal))
            {
                return child;
            }
        }

        var node = new MenuNode(label);
        _children.Add(node);
        return node;
    }

    /// <summary>Appends a direct child node.</summary>
    internal void AddChild(MenuNode node) => _children.Add(node);
}
