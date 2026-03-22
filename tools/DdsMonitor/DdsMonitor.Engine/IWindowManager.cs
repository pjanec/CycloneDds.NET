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
    /// Raised when the active panel list changes.
    /// </summary>
    event Action? PanelsChanged;

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
    /// Un-minimizes and un-hides the specified panel, brings it to the front,
    /// and notifies listeners via <see cref="PanelsChanged"/>.
    /// </summary>
    void ShowPanel(string panelId);

    /// <summary>
    /// Clears all active panels.
    /// </summary>
    void ClearPanels();

    /// <summary>
    /// Registers a component type for lookup by name.
    /// </summary>
    void RegisterPanelType(string typeName, Type blazorComponentType);

    /// <summary>
    /// Returns a read-only snapshot of all panel types registered on this instance via
    /// <see cref="RegisterPanelType"/>.
    /// </summary>
    IReadOnlyDictionary<string, Type> RegisteredPanelTypes { get; }

    /// <summary>
    /// Saves the current workspace to disk.
    /// </summary>
    void SaveWorkspace(string filePath);

    /// <summary>
    /// Serializes the current workspace to JSON.
    /// </summary>
    string SaveWorkspaceToJson();

    /// <summary>
    /// Loads a workspace from disk.
    /// </summary>
    void LoadWorkspace(string filePath);

    /// <summary>
    /// Loads a workspace from JSON.
    /// </summary>
    void LoadWorkspaceFromJson(string json);
}
