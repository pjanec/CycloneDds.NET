using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class WindowManagerTests
{
    [Fact]
    public void WindowManager_SpawnPanel_AssignsUniqueId()
    {
        var manager = new WindowManager();

        var first = manager.SpawnPanel("SamplesPanel");
        var second = manager.SpawnPanel("SamplesPanel");

        Assert.NotEqual(first.PanelId, second.PanelId);
        Assert.EndsWith(".1", first.PanelId, StringComparison.Ordinal);
        Assert.EndsWith(".2", second.PanelId, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowManager_ClosePanel_RemovesFromList()
    {
        var manager = new WindowManager();

        var panel = manager.SpawnPanel("SamplesPanel");
        manager.ClosePanel(panel.PanelId);

        Assert.DoesNotContain(manager.ActivePanels, entry => entry.PanelId == panel.PanelId);
    }

    [Fact]
    public void WindowManager_BringToFront_SetsHighestZIndex()
    {
        var manager = new WindowManager();

        var first = manager.SpawnPanel("SamplesPanel");
        var second = manager.SpawnPanel("SamplesPanel");
        var third = manager.SpawnPanel("SamplesPanel");

        manager.BringToFront(first.PanelId);

        var highest = manager.ActivePanels.Max(panel => panel.ZIndex);
        Assert.Equal(highest, first.ZIndex);
        Assert.True(first.ZIndex >= second.ZIndex);
        Assert.True(first.ZIndex >= third.ZIndex);
    }

    [Fact]
    public void WindowManager_SaveAndLoad_RoundTrips()
    {
        var manager = new WindowManager();

        var first = manager.SpawnPanel("SamplesPanel");
        first.Title = "First";
        first.X = 10;
        first.Y = 20;
        first.Width = 300;
        first.Height = 200;
        first.ZIndex = 5;
        first.IsMinimized = true;

        var second = manager.SpawnPanel("SamplesPanel");
        second.Title = "Second";
        second.X = 50;
        second.Y = 60;
        second.Width = 320;
        second.Height = 240;
        second.ZIndex = 6;
        second.IsMinimized = false;

        var path = Path.GetTempFileName();

        try
        {
            manager.SaveWorkspace(path);

            manager.LoadWorkspace(path);

            Assert.Equal(2, manager.ActivePanels.Count);

            var loadedFirst = Assert.Single(manager.ActivePanels, panel => panel.PanelId == first.PanelId);
            Assert.Equal(first.Title, loadedFirst.Title);
            Assert.Equal(first.X, loadedFirst.X);
            Assert.Equal(first.Y, loadedFirst.Y);
            Assert.Equal(first.Width, loadedFirst.Width);
            Assert.Equal(first.Height, loadedFirst.Height);
            Assert.Equal(first.ZIndex, loadedFirst.ZIndex);
            Assert.Equal(first.IsMinimized, loadedFirst.IsMinimized);

            var loadedSecond = Assert.Single(manager.ActivePanels, panel => panel.PanelId == second.PanelId);
            Assert.Equal(second.Title, loadedSecond.Title);
            Assert.Equal(second.X, loadedSecond.X);
            Assert.Equal(second.Y, loadedSecond.Y);
            Assert.Equal(second.Width, loadedSecond.Width);
            Assert.Equal(second.Height, loadedSecond.Height);
            Assert.Equal(second.ZIndex, loadedSecond.ZIndex);
            Assert.Equal(second.IsMinimized, loadedSecond.IsMinimized);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
