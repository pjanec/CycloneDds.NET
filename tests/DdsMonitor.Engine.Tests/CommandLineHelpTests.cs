using System;
using System.Linq;
using System.Reflection;
using DdsMonitor.Engine.Hosting;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// <c>ddsmonitor --help</c> is the reference an AI agent (or a person) has with the installed tool
/// alone: every command-line setting must be in it.
/// </summary>
public sealed class CommandLineHelpTests
{
    [Theory]
    [InlineData(typeof(DdsSettings), DdsSettings.SectionName)]
    [InlineData(typeof(AppSettings), AppSettings.SectionName)]
    [InlineData(typeof(BrowserLifecycleOptions), "BrowserLifecycle")]
    public void Help_NamesEverySetting(Type settings, string section)
    {
        var help = CommandLineHelp.Text("1.0");
        var missing = settings.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Select(p => $"--{section}:{p.Name}")
            .Where(arg => !help.Contains(arg + "=", StringComparison.Ordinal) && !help.Contains(arg + ":", StringComparison.Ordinal))
            .ToList();

        Assert.True(missing.Count == 0, "missing from --help: " + string.Join(", ", missing));
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    public void HelpArguments_AreRecognised(string arg) =>
        Assert.True(CommandLineHelp.IsHelpRequest(new[] { "--DdsSettings:DomainId=3", arg }));

    [Fact]
    public void OrdinaryArguments_DoNotAskForHelp() =>
        Assert.False(CommandLineHelp.IsHelpRequest(new[] { "--DdsSettings:HeadlessMode=Record", "--NoBrowser", "true" }));
}
