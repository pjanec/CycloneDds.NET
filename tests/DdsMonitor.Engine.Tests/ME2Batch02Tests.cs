using System;
using System.Linq;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Interop;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for ME2-BATCH-02: Advanced Filtering and Dynamic Payload Control.
/// Tasks covered: ME2-T08, ME2-T09 (filter expression logic), ME2-T10, ME2-T15.
/// </summary>
public sealed class ME2Batch02Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T08: Expose Non-Payload Fields to Filter and Column Picker
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// FilterCompiler compiles a Sample.Topic == "..." expression successfully.
    /// </summary>
    [Fact]
    public void FilterCompiler_SampleTopicExpression_Compiles()
    {
        var compiler = new FilterCompiler();
        var metadata = new TopicMetadata(typeof(SampleTopic));

        var result = compiler.Compile("Sample.Topic == \"SampleTopic\"", metadata);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.NotNull(result.Predicate);
    }

    /// <summary>
    /// Sample.Topic filter returns true for a matching topic and false for a non-matching one.
    /// </summary>
    [Fact]
    public void FilterCompiler_SampleTopicFilter_FiltersCorrectly()
    {
        var compiler = new FilterCompiler();
        var metaA = new TopicMetadata(typeof(SampleTopic));
        var metaB = new TopicMetadata(typeof(SimpleType));

        // "SampleTopic" is the CLR type name / ShortName of SampleTopic.
        var result = compiler.Compile($"Sample.Topic == \"{typeof(SampleTopic).Name}\"", metaA);

        Assert.True(result.IsValid, result.ErrorMessage);
        var predicate = Assert.IsType<Func<SampleData, bool>>(result.Predicate);

        var matching = CreateSample(metaA, 1, new SampleTopic { Id = 1 });
        var notMatching = CreateSample(metaB, 2, new SimpleType());

        Assert.True(predicate(matching));
        Assert.False(predicate(notMatching));
    }

    /// <summary>
    /// Sample.Topic != "..." exclusion filter works correctly (basis for ExcludeTopicFromFilter).
    /// </summary>
    [Fact]
    public void FilterCompiler_SampleTopicExclusion_FiltersCorrectly()
    {
        var compiler = new FilterCompiler();
        var metaA = new TopicMetadata(typeof(SampleTopic));
        var metaB = new TopicMetadata(typeof(SimpleType));

        var result = compiler.Compile($"Sample.Topic != \"{typeof(SampleTopic).Name}\"", null);

        Assert.True(result.IsValid, result.ErrorMessage);
        var predicate = Assert.IsType<Func<SampleData, bool>>(result.Predicate);

        var excluded = CreateSample(metaA, 1, new SampleTopic { Id = 1 });
        var included = CreateSample(metaB, 2, new SimpleType());

        Assert.False(predicate(excluded));
        Assert.True(predicate(included));
    }

    /// <summary>
    /// FilterCompiler compiles a Sample.InstanceState expression successfully.
    /// </summary>
    [Fact]
    public void FilterCompiler_SampleInstanceStateExpression_Compiles()
    {
        var compiler = new FilterCompiler();
        var metadata = new TopicMetadata(typeof(SampleTopic));

        // DdsInstanceState.Alive = 16
        var result = compiler.Compile("Sample.InstanceState == 16", metadata);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.NotNull(result.Predicate);
    }

    /// <summary>
    /// Sample.InstanceState filter correctly separates Alive from non-Alive samples.
    /// </summary>
    [Fact]
    public void FilterCompiler_SampleInstanceStateFilter_FiltersCorrectly()
    {
        var compiler = new FilterCompiler();
        var metadata = new TopicMetadata(typeof(SampleTopic));

        // DdsInstanceState.Alive = 16
        var result = compiler.Compile("Sample.InstanceState == 16", metadata);

        Assert.True(result.IsValid, result.ErrorMessage);
        var predicate = Assert.IsType<Func<SampleData, bool>>(result.Predicate);

        var alive = CreateSampleWithState(metadata, 1, new SampleTopic { Id = 1 }, DdsInstanceState.Alive);
        var disposed = CreateSampleWithState(metadata, 2, new SampleTopic { Id = 2 }, DdsInstanceState.NotAliveDisposed);

        Assert.True(predicate(alive));
        Assert.False(predicate(disposed));
    }

    /// <summary>
    /// Existing Payload.Field expressions still compile and evaluate correctly
    /// after the regex was updated to also match Sample.
    /// </summary>
    [Fact]
    public void FilterCompiler_ExistingPayloadExpression_StillWorks()
    {
        var compiler = new FilterCompiler();
        var metadata = new TopicMetadata(typeof(SampleTopic));

        var result = compiler.Compile("Payload.Id > 5", metadata);

        Assert.True(result.IsValid, result.ErrorMessage);
        var predicate = Assert.IsType<Func<SampleData, bool>>(result.Predicate);

        var matching = CreateSample(metadata, 1, new SampleTopic { Id = 10 });
        var notMatching = CreateSample(metadata, 2, new SampleTopic { Id = 3 });

        Assert.True(predicate(matching));
        Assert.False(predicate(notMatching));
    }

    /// <summary>
    /// TopicMetadata.AllFields contains "Topic" as a wrapper synthetic field after T08.
    /// </summary>
    [Fact]
    public void TopicMetadata_AllFields_ContainsTopicWrapperField()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));

        var topicField = metadata.AllFields.FirstOrDefault(f => f.StructuredName == "Topic");

        Assert.NotNull(topicField);
        Assert.True(topicField.IsSynthetic);
        Assert.True(topicField.IsWrapperField);
        Assert.Equal(typeof(string), topicField.ValueType);
    }

    /// <summary>
    /// TopicMetadata.AllFields contains "InstanceState" as a wrapper synthetic field after T08.
    /// </summary>
    [Fact]
    public void TopicMetadata_AllFields_ContainsInstanceStateWrapperField()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));

        var isField = metadata.AllFields.FirstOrDefault(f => f.StructuredName == "InstanceState");

        Assert.NotNull(isField);
        Assert.True(isField.IsSynthetic);
        Assert.True(isField.IsWrapperField);
        Assert.Equal(typeof(DdsInstanceState), isField.ValueType);
    }

    /// <summary>
    /// The Topic getter returns the correct ShortName from the SampleData.
    /// </summary>
    [Fact]
    public void TopicMetadata_TopicField_Getter_ReturnsSampleShortName()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var topicField = metadata.AllFields.First(f => f.StructuredName == "Topic");

        var sample = CreateSample(metadata, 1, new SampleTopic { Id = 42 });
        var result = topicField.Getter(sample);

        Assert.Equal(typeof(SampleTopic).Name, result);
    }

    /// <summary>
    /// The InstanceState getter returns the correct instance state from the SampleData.
    /// </summary>
    [Fact]
    public void TopicMetadata_InstanceStateField_Getter_ReturnsCorrectState()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var stateField = metadata.AllFields.First(f => f.StructuredName == "InstanceState");

        var sample = CreateSampleWithState(metadata, 1, new SampleTopic { Id = 1 }, DdsInstanceState.NotAliveDisposed);
        var result = stateField.Getter(sample);

        Assert.Equal(DdsInstanceState.NotAliveDisposed, result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T08: FieldPickerFilter prefix matching
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Searching "Sample." in FieldPickerFilter matches only wrapper fields.
    /// </summary>
    [Fact]
    public void FieldPickerFilter_SamplePrefix_MatchesOnlyWrapperFields()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var allFields = metadata.AllFields;

        var results = FieldPickerFilter.FilterFields(allFields, "Sample.").ToList();

        Assert.NotEmpty(results);
        Assert.All(results, f => Assert.True(f.IsWrapperField));
        Assert.Contains(results, f => f.StructuredName == "Topic");
        Assert.Contains(results, f => f.StructuredName == "InstanceState");
        Assert.Contains(results, f => f.StructuredName == "Timestamp");
        Assert.Contains(results, f => f.StructuredName == "Ordinal");
    }

    /// <summary>
    /// Searching "Payload." in FieldPickerFilter matches only non-wrapper fields.
    /// </summary>
    [Fact]
    public void FieldPickerFilter_PayloadPrefix_MatchesNonWrapperFields()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var allFields = metadata.AllFields;

        var results = FieldPickerFilter.FilterFields(allFields, "Payload.").ToList();

        Assert.NotEmpty(results);
        Assert.All(results, f => Assert.False(f.IsWrapperField));
        Assert.Contains(results, f => f.StructuredName == "Id");
    }

    /// <summary>
    /// Searching "Sample.Topic" matches the Topic wrapper field precisely.
    /// </summary>
    [Fact]
    public void FieldPickerFilter_SampleTopicQuery_MatchesTopicField()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var allFields = metadata.AllFields;

        var results = FieldPickerFilter.FilterFields(allFields, "Sample.Topic").ToList();

        Assert.Single(results);
        Assert.Equal("Topic", results[0].StructuredName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T10: TopicMetadata field count includes all synthetic non-Ordinal fields
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// After T08 + T10, last two AllFields entries are still Delay [ms] and Size [B]
    /// (non-wrapper non-filterable synthetic fields appended at the end).
    /// </summary>
    [Fact]
    public void TopicMetadata_SyntheticFields_DelayAndSizeAreLastEntries()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));

        var delayField = metadata.AllFields[^2];
        var sizeField = metadata.AllFields[^1];

        Assert.Equal("Delay [ms]", delayField.StructuredName);
        Assert.Equal("Size [B]", sizeField.StructuredName);
        Assert.True(delayField.IsSynthetic);
        Assert.True(sizeField.IsSynthetic);
    }

    /// <summary>
    /// All default metadata column names are present as synthetic fields in AllFields.
    /// This is required for T10's InitializeColumns to correctly default-populate _selectedColumns.
    /// </summary>
    [Fact]
    public void TopicMetadata_AllDefaultColumnNames_PresentAsSyntheticFields()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var defaultKeys = new[] { "Topic", "Timestamp", "Size [B]", "Delay [ms]" };

        foreach (var key in defaultKeys)
        {
            var field = metadata.AllFields.FirstOrDefault(f => f.StructuredName == key);
            Assert.True(field != null, $"Expected synthetic field '{key}' not found in AllFields.");
            Assert.True(field!.IsSynthetic, $"Field '{key}' should be synthetic.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T09: Sample.Topic != filter (ExcludeTopicFromFilter expression pattern)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Compound exclusion filter (as generated by ExcludeTopicFromFilter called twice)
    /// correctly excludes both topics while keeping others.
    /// </summary>
    [Fact]
    public void FilterCompiler_CompoundTopicExclusion_ExcludesBothTopics()
    {
        var compiler = new FilterCompiler();
        var metaA = new TopicMetadata(typeof(SampleTopic));
        var metaB = new TopicMetadata(typeof(SimpleType));
        var metaC = new TopicMetadata(typeof(MockTopic));

        // Simulates two calls to ExcludeTopicFromFilter:
        var filter = $"(Sample.Topic != \"{metaA.ShortName}\") AND Sample.Topic != \"{metaB.ShortName}\"";
        var result = compiler.Compile(filter, null);

        Assert.True(result.IsValid, result.ErrorMessage);
        var predicate = Assert.IsType<Func<SampleData, bool>>(result.Predicate);

        var sampleA = CreateSample(metaA, 1, new SampleTopic { Id = 1 });
        var sampleB = CreateSample(metaB, 2, new SimpleType());
        var sampleC = CreateSample(metaC, 3, new MockTopic { Id = 99 });

        Assert.False(predicate(sampleA));
        Assert.False(predicate(sampleB));
        Assert.True(predicate(sampleC));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static SampleData CreateSample(TopicMetadata metadata, long ordinal, object payload)
    {
        return new SampleData
        {
            Ordinal = ordinal,
            Payload = payload,
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo
            {
                SourceTimestamp = 0,
                InstanceState = DdsInstanceState.Alive
            },
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SizeBytes = 0
        };
    }

    private static SampleData CreateSampleWithState(TopicMetadata metadata, long ordinal, object payload, DdsInstanceState instanceState)
    {
        return new SampleData
        {
            Ordinal = ordinal,
            Payload = payload,
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo
            {
                SourceTimestamp = 0,
                InstanceState = instanceState
            },
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SizeBytes = 0
        };
    }
}
