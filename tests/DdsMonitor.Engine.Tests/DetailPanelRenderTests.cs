using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class DetailPanelRenderTests
{
    [Fact]
    public void DetailPanel_RenderNode_DoesNotRecurseInfinitely()
    {
        var detailPanelType = LoadDetailPanelType();
        var node = new CyclicNode();
        node.Next = node;

        var method = detailPanelType
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == "AnalyzeTraversal" &&
                                 candidate.GetParameters().Length == 1 &&
                                 candidate.ReturnType.Name == "TraversalSummary");

        var summary = method.Invoke(null, new object?[] { node });
        Assert.NotNull(summary);

        var summaryType = summary!.GetType();
        var hitCycle = (bool)summaryType.GetProperty("HitCycle")!.GetValue(summary)!;
        var nodeCount = (int)summaryType.GetProperty("NodeCount")!.GetValue(summary)!;
        var hitMaxDepth = (bool)summaryType.GetProperty("HitMaxDepth")!.GetValue(summary)!;

        Assert.True(hitCycle);
        Assert.True(nodeCount > 0);
        Assert.False(hitMaxDepth);
    }

    private static Type LoadDetailPanelType()
    {
        var assembly = LoadDdsMonitorAssembly();
        return assembly.GetType("DdsMonitor.Components.DetailPanel", throwOnError: true)!;
    }

    private static Assembly LoadDdsMonitorAssembly()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            var debugPath = Path.Combine(directory.FullName, "tools", "DdsMonitor", "bin", "Debug", "net8.0", "DdsMonitor.dll");
            if (File.Exists(debugPath))
            {
                return Assembly.LoadFrom(debugPath);
            }

            var releasePath = Path.Combine(directory.FullName, "tools", "DdsMonitor", "bin", "Release", "net8.0", "DdsMonitor.dll");
            if (File.Exists(releasePath))
            {
                return Assembly.LoadFrom(releasePath);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("DdsMonitor.dll was not found. Build the DdsMonitor project before running this test.");
    }

    private sealed class CyclicNode
    {
        public CyclicNode? Next { get; set; }
    }
}
