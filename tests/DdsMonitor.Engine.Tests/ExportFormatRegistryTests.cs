using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DdsMonitor.Engine.Export;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Unit tests for <see cref="ExportFormatRegistry"/> (PLA1-P6-T04).
/// </summary>
public sealed class ExportFormatRegistryTests
{
    private static Task NoopExport(IReadOnlyList<SampleData> _, string __, CancellationToken ___) =>
        Task.CompletedTask;

    [Fact]
    public void RegisterFormat_AppearInGetFormats()
    {
        var registry = new ExportFormatRegistry();

        registry.RegisterFormat("CSV", NoopExport);

        var formats = registry.GetFormats();
        Assert.Single(formats);
        Assert.Equal("CSV", formats[0].Label);
    }

    [Fact]
    public void RegisterFormat_MultipleFormats_AccumulateInOrder()
    {
        var registry = new ExportFormatRegistry();

        registry.RegisterFormat("CSV", NoopExport);
        registry.RegisterFormat("TSV", NoopExport);

        var formats = registry.GetFormats();
        Assert.Equal(2, formats.Count);
        Assert.Equal("CSV", formats[0].Label);
        Assert.Equal("TSV", formats[1].Label);
    }

    [Fact]
    public void GetFormats_WhenEmpty_ReturnsEmptyList()
    {
        var registry = new ExportFormatRegistry();

        var formats = registry.GetFormats();

        Assert.Empty(formats);
    }
}
