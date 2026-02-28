using System.Collections.Generic;

namespace DdsMonitor.Engine;

/// <summary>
/// Represents the persistent state of a desktop panel.
/// </summary>
public sealed class PanelState
{
    /// <summary>
    /// Gets or sets the panel identifier.
    /// </summary>
    public string PanelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the panel title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the component type name to render inside the panel.
    /// </summary>
    public string ComponentTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the panel X position.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Gets or sets the panel Y position.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Gets or sets the panel width.
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Gets or sets the panel height.
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// Gets or sets the panel Z-index.
    /// </summary>
    public int ZIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the panel is minimized.
    /// </summary>
    public bool IsMinimized { get; set; }

    /// <summary>
    /// Gets or sets the component-specific state payload.
    /// </summary>
    public Dictionary<string, object> ComponentState { get; set; } = new(StringComparer.Ordinal);
}
