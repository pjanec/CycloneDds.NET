using System;

namespace DdsMonitor.Services;

/// <summary>
/// Tracks tooltip content for the global portal.
/// </summary>
public sealed class TooltipService
{
    /// <summary>
    /// Raised when the tooltip state changes.
    /// </summary>
    public event Action? OnChanged;

    /// <summary>
    /// Gets the current tooltip state.
    /// </summary>
    public TooltipState? Current { get; private set; }

    /// <summary>
    /// Shows a tooltip.
    /// </summary>
    public void Show(TooltipState state)
    {
        Current = state ?? throw new ArgumentNullException(nameof(state));
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Hides the current tooltip.
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
/// Holds tooltip content and positioning.
/// An optional <see cref="ContextType"/>/<see cref="ContextValue"/> pair allows
/// <see cref="DdsMonitor.Engine.Ui.ITooltipProviderRegistry"/> to override the
/// default <see cref="Html"/> with plugin-supplied content (P6-T07).
/// </summary>
public sealed record TooltipState(
    string Html,
    double X,
    double Y,
    Type? ContextType = null,
    object? ContextValue = null);
