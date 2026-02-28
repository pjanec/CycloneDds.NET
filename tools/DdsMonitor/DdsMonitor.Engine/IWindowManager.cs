using System;
using System.Collections.Generic;

namespace DdsMonitor.Engine;

/// <summary>
/// Manages desktop panels and their persistence.
/// </summary>
public interface IWindowManager
{
    /// <summary>
    /// Raised when a panel is closed.
    /// </summary>
    event Action<PanelState>? PanelClosed;

    /// <summary>
    /// Gets the active panels.
    /// </summary>
    IReadOnlyList<PanelState> ActivePanels { get; }

    /// <summary>
    /// Spawns a new panel using the provided component type name.
    /// </summary>
    PanelState SpawnPanel(string componentTypeName, Dictionary<string, object>? initialState = null);

    /// <summary>
    /// Closes a panel.
    /// </summary>
    void ClosePanel(string panelId);

    /// <summary>
    /// Brings the specified panel to the front.
    /// </summary>
    void BringToFront(string panelId);

    /// <summary>
    /// Registers a component type for lookup by name.
    /// </summary>
    void RegisterPanelType(string typeName, Type blazorComponentType);

    /// <summary>
    /// Saves the current workspace to disk.
    /// </summary>
    void SaveWorkspace(string filePath);

    /// <summary>
    /// Loads a workspace from disk.
    /// </summary>
    void LoadWorkspace(string filePath);
}
