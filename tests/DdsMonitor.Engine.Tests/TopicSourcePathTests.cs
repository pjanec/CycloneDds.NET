using System;
using System.IO;
using System.Linq;
using DdsMonitor.Engine.AssemblyScanner;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// <c>--AppSettings:TopicSources:0=./bin/...</c>: a relative topic source is taken from the current
/// directory. It used to load nothing, silently, because the assembly loader needs an absolute path.
/// </summary>
public sealed class TopicSourcePathTests
{
    [Fact]
    public void RelativeTopicSource_LoadsTheTopicTypes()
    {
        var assembly = typeof(TopicSourcePathTests).Assembly.Location;
        var relative = Path.GetRelativePath(Environment.CurrentDirectory, assembly);
        Assert.False(Path.IsPathRooted(relative));

        var registry = new TopicRegistry();
        var sources = new AssemblySourceService(registry, new TopicDiscoveryService(registry),
            new AppSettings { TopicSources = new[] { relative } });

        var entry = Assert.Single(sources.Entries);
        Assert.Null(entry.LoadError);
        Assert.True(entry.TopicCount > 0, "no topic types loaded from the relative path");
        Assert.Contains(registry.AllTopics, t => t.TopicName == "SumoLikeWireFrame");
    }
}
