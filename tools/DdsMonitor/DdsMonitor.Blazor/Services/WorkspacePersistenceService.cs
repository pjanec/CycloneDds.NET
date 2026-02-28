using System;
using System.IO;
using DdsMonitor.Engine;

namespace DdsMonitor.Services;

/// <summary>
/// Handles debounced workspace persistence and import/export.
/// </summary>
public sealed class WorkspacePersistenceService : IDisposable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(2);

    private readonly IWindowManager _windowManager;
    private readonly IWorkspaceState _workspaceState;
    private readonly DebouncedAction _debouncer;

    public WorkspacePersistenceService(IWindowManager windowManager, IWorkspaceState workspaceState)
    {
        _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
        _debouncer = new DebouncedAction(SaveDelay, SaveNow);
    }

    public void RequestSave()
    {
        _debouncer.Trigger();
    }

    public void SaveNow()
    {
        var filePath = _workspaceState.WorkspaceFilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            _windowManager.SaveWorkspace(filePath);
        }
        catch
        {
            // Ignore persistence errors to avoid breaking the UI loop.
        }
    }

    public void LoadDefault()
    {
        var filePath = _workspaceState.WorkspaceFilePath;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        _windowManager.LoadWorkspace(filePath);
    }

    public string ExportWorkspaceJson()
    {
        return _windowManager.SaveWorkspaceToJson();
    }

    public void ImportWorkspaceJson(string json)
    {
        _windowManager.LoadWorkspaceFromJson(json);
    }

    public void Dispose()
    {
        _debouncer.Dispose();
    }
}
