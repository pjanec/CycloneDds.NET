using System;
using System.IO;

namespace DdsMonitor.Engine;

/// <summary>
/// Default implementation of <see cref="IWorkspaceState"/>.
/// </summary>
public sealed class WorkspaceState : IWorkspaceState
{
	private const string WorkspaceFileName = "workspace.json";

	public WorkspaceState() : this(null) { }

	public WorkspaceState(AppSettings? appSettings)
	{
		// Explicit workspace file takes highest priority.
		if (!string.IsNullOrWhiteSpace(appSettings?.WorkspaceFile))
		{
			var dir = Path.GetDirectoryName(appSettings.WorkspaceFile);
			if (!string.IsNullOrWhiteSpace(dir))
			{
				Directory.CreateDirectory(dir);
			}
			WorkspaceFilePath = appSettings.WorkspaceFile;
			return;
		}

		// Explicit config folder overrides the default %APPDATA% location.
		string workspaceDir;
		if (!string.IsNullOrWhiteSpace(appSettings?.ConfigFolder))
		{
			workspaceDir = appSettings.ConfigFolder;
		}
		else
		{
			var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			workspaceDir = Path.Combine(appData, "DdsMonitor");
		}

		Directory.CreateDirectory(workspaceDir);
		WorkspaceFilePath = Path.Combine(workspaceDir, WorkspaceFileName);
	}

	/// <inheritdoc />
	public string WorkspaceFilePath { get; }
}

