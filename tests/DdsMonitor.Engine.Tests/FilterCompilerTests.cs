using System;
using CycloneDDS.Runtime.Interop;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class FilterCompilerTests
{
    [Fact]
    public void FilterCompiler_SimpleExpression_Compiles()
    {
        var compiler = new FilterCompiler();

        var result = compiler.Compile("Ordinal > 50", null);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Predicate);
    }

    [Fact]
    public void FilterCompiler_Predicate_FiltersCorrectly()
    {
        var compiler = new FilterCompiler();
        var metadata = new TopicMetadata(typeof(SampleTopic));

        var result = compiler.Compile("Ordinal > 50", metadata);

        Assert.True(result.IsValid);
        var predicate = Assert.IsType<Func<SampleData, bool>>(result.Predicate);

        var match = CreateSample(metadata, 100, new SampleTopic { Id = 1 });
        var miss = CreateSample(metadata, 10, new SampleTopic { Id = 2 });

        Assert.True(predicate(match));
        Assert.False(predicate(miss));
    }

    [Fact]
    public void FilterCompiler_InvalidExpression_ReturnsError()
    {
        var compiler = new FilterCompiler();

        var result = compiler.Compile(") )) garbage (((", null);

        Assert.False(result.IsValid);
        Assert.True(!string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void FilterCompiler_PayloadFieldAccess_Works()
    {
        var compiler = new FilterCompiler();
        var metadata = new TopicMetadata(typeof(SampleTopic));

        var result = compiler.Compile("Payload.Id == 42", metadata);

        Assert.True(result.IsValid);
        var predicate = Assert.IsType<Func<SampleData, bool>>(result.Predicate);

        var match = CreateSample(metadata, 1, new SampleTopic { Id = 42 });
        var miss = CreateSample(metadata, 2, new SampleTopic { Id = 7 });

        Assert.True(predicate(match));
        Assert.False(predicate(miss));
    }

    private static SampleData CreateSample(TopicMetadata metadata, long ordinal, SampleTopic payload)
    {
        return new SampleData
        {
            Ordinal = ordinal,
            Payload = payload,
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = 0 },
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SizeBytes = 0
        };
    }
}
