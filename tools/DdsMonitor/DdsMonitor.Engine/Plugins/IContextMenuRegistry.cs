namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Registry for plugin-contributed context menu items.
/// Plugins call <see cref="RegisterProvider{TContext}"/> during initialization to supply
/// additional items; host UI panels call <see cref="GetItems{TContext}"/> when building a
/// right-click menu.
/// </summary>
public interface IContextMenuRegistry
{
    /// <summary>
    /// Registers a function that yields additional context menu items whenever a right-click
    /// event carries a context payload of type <typeparamref name="TContext"/>.
    /// Multiple providers may be registered for the same context type; all are invoked.
    /// </summary>
    /// <typeparam name="TContext">The context payload type this provider handles.</typeparam>
    /// <param name="provider">A function that receives the clicked item and returns menu items.</param>
    void RegisterProvider<TContext>(Func<TContext, IEnumerable<ContextMenuItem>> provider);

    /// <summary>
    /// Called by UI panels to collect all plugin-injected menu items for the given context.
    /// If a provider throws, the exception is logged and the remaining providers still run.
    /// Returns an empty enumerable when no providers are registered for <typeparamref name="TContext"/>.
    /// </summary>
    /// <typeparam name="TContext">The context payload type.</typeparam>
    /// <param name="context">The context payload for which to collect items.</param>
    IEnumerable<ContextMenuItem> GetItems<TContext>(TContext context);
}
