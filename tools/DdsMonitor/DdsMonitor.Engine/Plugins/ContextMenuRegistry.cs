using Microsoft.Extensions.Logging;

namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Thread-safe implementation of <see cref="IContextMenuRegistry"/>.
/// Providers are stored internally as delegates keyed by context type.
/// A provider that throws during <see cref="GetItems{TContext}"/> is caught, logged,
/// and does not prevent remaining providers from running.
/// </summary>
public sealed class ContextMenuRegistry : IContextMenuRegistry
{
    private readonly Dictionary<Type, List<Delegate>> _providers = new();
    private readonly object _sync = new();
    private readonly ILogger<ContextMenuRegistry>? _logger;

    /// <summary>
    /// Initialises a new <see cref="ContextMenuRegistry"/>.
    /// </summary>
    /// <param name="logger">Optional logger for provider error reporting.</param>
    public ContextMenuRegistry(ILogger<ContextMenuRegistry>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void RegisterProvider<TContext>(Func<TContext, IEnumerable<ContextMenuItem>> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        lock (_sync)
        {
            if (!_providers.TryGetValue(typeof(TContext), out var list))
            {
                list = new List<Delegate>();
                _providers[typeof(TContext)] = list;
            }

            list.Add(provider);
        }
    }

    /// <inheritdoc />
    public IEnumerable<ContextMenuItem> GetItems<TContext>(TContext context)
    {
        List<Delegate> snapshot;

        lock (_sync)
        {
            if (!_providers.TryGetValue(typeof(TContext), out var list))
                return Enumerable.Empty<ContextMenuItem>();

            snapshot = new List<Delegate>(list);
        }

        var results = new List<ContextMenuItem>();

        foreach (var del in snapshot)
        {
            try
            {
                var provider = (Func<TContext, IEnumerable<ContextMenuItem>>)del;
                results.AddRange(provider(context));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "A context menu provider threw an exception and was skipped.");
            }
        }

        return results;
    }
}
