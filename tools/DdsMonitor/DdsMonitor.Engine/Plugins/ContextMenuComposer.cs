namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Pure helper that combines host-defined default context menu items with
/// plugin-contributed items retrieved from an <see cref="IContextMenuRegistry"/>.
/// The composition rule is: defaults first, then — only when plugin items are present —
/// a separator, then the plugin items.
/// </summary>
public static class ContextMenuComposer
{
    private static readonly ContextMenuItem Separator =
        new("─────────────────", null, () => Task.CompletedTask);

    /// <summary>
    /// Returns a new list that starts with <paramref name="defaultItems"/>, appends a
    /// separator, and then appends any plugin-supplied items for <paramref name="context"/>.
    /// The separator is omitted when the registry returns no items.
    /// </summary>
    /// <typeparam name="TContext">Context type used to look up registered providers.</typeparam>
    /// <param name="defaultItems">Ordered host-defined items (shown first, never reordered).</param>
    /// <param name="registry">Registry to query for plugin items.</param>
    /// <param name="context">Value passed to every registered provider for this context type.</param>
    /// <returns>Combined list ready to pass to <c>ContextMenuService.Show</c>.</returns>
    public static List<ContextMenuItem> Compose<TContext>(
        IEnumerable<ContextMenuItem> defaultItems,
        IContextMenuRegistry registry,
        TContext context)
    {
        var result = new List<ContextMenuItem>(defaultItems);
        var pluginItems = registry.GetItems<TContext>(context).ToList();
        if (pluginItems.Count > 0)
        {
            result.Add(Separator);
            result.AddRange(pluginItems);
        }

        return result;
    }
}
