using System;
using System.IO;
using DdsMonitor.Engine;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for the AppSettings-aware WorkspaceState constructor introduced by the
/// "layout file and config folder" improvements.
/// </summary>
public sealed class WorkspaceStateTests : IDisposable
{
    private readonly string _tempDir;

    public WorkspaceStateTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DdsMonWorkspaceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // ─── Default behaviour (no AppSettings) ───────────────────────────────

    [Fact]
    public void WorkspaceState_Default_UsesAppDataLocation()
    {
        var state = new WorkspaceState();

        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DdsMonitor",
            "workspace.json");

        Assert.Equal(expected, state.WorkspaceFilePath, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkspaceState_NullAppSettings_UsesAppDataLocation()
    {
        var state = new WorkspaceState(null);

        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DdsMonitor",
            "workspace.json");

        Assert.Equal(expected, state.WorkspaceFilePath, StringComparer.OrdinalIgnoreCase);
    }

    // ─── ConfigFolder override ─────────────────────────────────────────────

    [Fact]
    public void WorkspaceState_ConfigFolder_UsesSpecifiedFolder()
    {
        var configDir = Path.Combine(_tempDir, "cfg");
        var appSettings = new AppSettings { ConfigFolder = configDir };

        var state = new WorkspaceState(appSettings);

        Assert.Equal(
            Path.Combine(configDir, "workspace.json"),
            state.WorkspaceFilePath,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkspaceState_ConfigFolder_CreatesDirectoryIfMissing()
    {
        var configDir = Path.Combine(_tempDir, "new_cfg_dir");
        Assert.False(Directory.Exists(configDir));

        var appSettings = new AppSettings { ConfigFolder = configDir };
        _ = new WorkspaceState(appSettings);

        Assert.True(Directory.Exists(configDir));
    }

    // ─── WorkspaceFile override ───────────────────────────────────────────

    [Fact]
    public void WorkspaceState_WorkspaceFile_UsesExplicitFilePath()
    {
        var filePath = Path.Combine(_tempDir, "custom_layout.json");
        var appSettings = new AppSettings { WorkspaceFile = filePath };

        var state = new WorkspaceState(appSettings);

        Assert.Equal(filePath, state.WorkspaceFilePath, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkspaceState_WorkspaceFile_TakesPriorityOverConfigFolder()
    {
        var filePath = Path.Combine(_tempDir, "explicit.json");
        var configDir = Path.Combine(_tempDir, "should_not_use");

        var appSettings = new AppSettings
        {
            WorkspaceFile = filePath,
            ConfigFolder = configDir
        };

        var state = new WorkspaceState(appSettings);

        // WorkspaceFile takes highest priority.
        Assert.Equal(filePath, state.WorkspaceFilePath, StringComparer.OrdinalIgnoreCase);
        // The config folder must NOT have been created.
        Assert.False(Directory.Exists(configDir));
    }

    [Fact]
    public void WorkspaceState_WorkspaceFile_CreatesParentDirectoryIfMissing()
    {
        var subDir = Path.Combine(_tempDir, "nested", "layout");
        var filePath = Path.Combine(subDir, "workspace.json");
        Assert.False(Directory.Exists(subDir));

        var appSettings = new AppSettings { WorkspaceFile = filePath };
        _ = new WorkspaceState(appSettings);

        Assert.True(Directory.Exists(subDir));
    }
}
