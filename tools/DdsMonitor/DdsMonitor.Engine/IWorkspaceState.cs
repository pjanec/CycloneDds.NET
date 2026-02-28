namespace DdsMonitor.Engine;

/// <summary>
/// Tracks per-browser-tab workspace state.
/// </summary>
public interface IWorkspaceState
{
	/// <summary>
	/// Gets the workspace settings file path for the current user.
	/// </summary>
	string WorkspaceFilePath { get; }
}
