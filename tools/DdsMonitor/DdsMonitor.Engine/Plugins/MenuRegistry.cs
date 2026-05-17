namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Default thread-safe, in-memory implementation of <see cref="IMenuRegistry"/>.
/// Menu items are organised into a hierarchical tree built from slash-delimited paths.
/// </summary>
public sealed class MenuRegistry : IMenuRegistry
{
    private readonly List<MenuNode> _roots = new();
    private readonly object _sync = new();

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public void AddMenuItem(string menuPath, string label, Action onClick)
    {
        ArgumentNullException.ThrowIfNull(onClick);
        AddItemCore(menuPath, label, onClick, null);
    }

    /// <inheritdoc />
    public void AddMenuItem(string menuPath, string label, Func<Task> onClickAsync)
    {
        ArgumentNullException.ThrowIfNull(onClickAsync);
        AddItemCore(menuPath, label, null, onClickAsync);
    }

    /// <inheritdoc />
    public IReadOnlyList<MenuNode> GetTopLevelMenus()
    {
        lock (_sync)
        {
            return _roots.ToArray();
        }
    }

    private void AddItemCore(string? menuPath, string label, Action? onClick, Func<Task>? onClickAsync)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Label must not be empty.", nameof(label));
        }

        lock (_sync)
        {
            var segments = string.IsNullOrEmpty(menuPath)
                ? Array.Empty<string>()
                : menuPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            var leaf = new MenuNode(label)
            {
                OnClick = onClick,
                OnClickAsync = onClickAsync
            };

            if (segments.Length == 0)
            {
                _roots.Add(leaf);
            }
            else
            {
                var current = GetOrAddRoot(segments[0]);

                for (var i = 1; i < segments.Length; i++)
                {
                    current = current.GetOrAddChild(segments[i]);
                }

                current.AddChild(leaf);
            }
        }

        Changed?.Invoke();
    }

    private MenuNode GetOrAddRoot(string label)
    {
        foreach (var node in _roots)
        {
            if (string.Equals(node.Label, label, StringComparison.Ordinal))
            {
                return node;
            }
        }

        var newNode = new MenuNode(label);
        _roots.Add(newNode);
        return newNode;
    }
}
