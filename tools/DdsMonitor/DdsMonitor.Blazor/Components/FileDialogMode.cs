namespace DdsMonitor.Components;

/// <summary>
/// Controls the behavior of the <see cref="FileDialog"/> component.
/// </summary>
public enum FileDialogMode
{
    /// <summary>
    /// The user selects an existing file to open / load.
    /// </summary>
    Open,

    /// <summary>
    /// The user nominates a path where a file will be written.
    /// </summary>
    Save
}
