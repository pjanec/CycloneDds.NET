using System;
using System.IO;

namespace DdsMonitor.Engine;

/// <summary>
/// Default implementation of <see cref="IWorkspaceState"/>.
/// </summary>
public sealed class WorkspaceState : IWorkspaceState
{
	private const string WorkspaceFileName = "ddsmon-settings.json";

	public WorkspaceState()
	{
		var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		var workspaceDir = Path.Combine(appData, "DdsMonitor");
		Directory.CreateDirectory(workspaceDir);
		WorkspaceFilePath = Path.Combine(workspaceDir, WorkspaceFileName);
	}

	/// <inheritdoc />
	public string WorkspaceFilePath { get; }
}
