using DdsMonitor.Engine.Hosting;
using Xunit;

namespace DdsMonitor.Engine.Tests;

// A dotnet-tool install keeps the DDS Security plugins beside the assemblies, not beside the launcher; Windows only
// finds them through PATH, so the monitor must put its own folder there (once).
public class NativeSearchPathTests
{
    [Fact]
    public void AddsTheDirectoryInFront()
    {
        Assert.Equal(@"C:\tool;C:\a;C:\b", NativeSearchPath.WithDirectory(@"C:\a;C:\b", @"C:\tool", ';'));
    }

    [Fact]
    public void EmptyPathBecomesTheDirectory()
    {
        Assert.Equal(@"C:\tool", NativeSearchPath.WithDirectory(null, @"C:\tool\", ';'));
        Assert.Equal(@"C:\tool", NativeSearchPath.WithDirectory("", @"C:\tool", ';'));
    }

    [Fact]
    public void DoesNotAddTheDirectoryTwice_IgnoringCaseAndTrailingSlash()
    {
        var path = @"C:\a;c:\TOOL\;C:\b";
        Assert.Same(path, NativeSearchPath.WithDirectory(path, @"C:\tool", ';'));
    }
}
