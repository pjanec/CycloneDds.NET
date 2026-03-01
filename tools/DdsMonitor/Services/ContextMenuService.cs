using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DdsMonitor.Services;

/// <summary>
/// Tracks context menu state for the global portal.
/// </summary>
public sealed class ContextMenuService
{
    /// <summary>
    /// Raised when the context menu state changes.
    /// </summary>
    public event Action? OnChanged;

    /// <summary>
    /// Gets the current context menu state.
    /// </summary>
    public ContextMenuState? Current { get; private set; }

    /// <summary>
    /// Shows a context menu.
    /// </summary>
    public void Show(ContextMenuState state)
    {
        Current = state ?? throw new ArgumentNullException(nameof(state));
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Hides the current context menu.
    /// </summary>
    public void Hide()
    {
        if (Current == null)
        {
            return;
        }

        Current = null;
        OnChanged?.Invoke();
    }
}

/// <summary>
/// Defines a context menu instance.
/// </summary>
public sealed record ContextMenuState(IReadOnlyList<ContextMenuItem> Items, double X, double Y);

/// <summary>
/// Defines a context menu item.
/// </summary>
public sealed record ContextMenuItem(string Label, string? Icon, Func<Task> Action);
