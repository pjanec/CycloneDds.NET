using System;
using System.Collections.Generic;
using System.Linq;
using CycloneDDS.Schema.Formatting;
using DdsMonitor.Engine.Ui;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for the Tier 1 <see cref="IValueFormatterRegistry"/> and <see cref="ValueFormatterRegistry"/>.
/// </summary>
public sealed class ValueFormatterRegistryTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class FakeFormatter : IValueFormatter
    {
        public string DisplayName { get; }
        private readonly float _score;

        public FakeFormatter(string displayName, float score)
        {
            DisplayName = displayName;
            _score = score;
        }

        public float GetScore(Type type, object? value) => _score;
        public string FormatText(object? value) => value?.ToString() ?? string.Empty;
        public IEnumerable<FormattedToken> FormatTokens(object? value)
        {
            yield return new FormattedToken(FormatText(value), TokenType.Number);
        }
    }

    private sealed class TypeSpecificFormatter : IValueFormatter
    {
        private readonly Type _targetType;
        private readonly float _score;
        public string DisplayName => "TypeSpecific";

        public TypeSpecificFormatter(Type targetType, float score)
        {
            _targetType = targetType;
            _score = score;
        }

        public float GetScore(Type type, object? value) => type == _targetType ? _score : 0f;
        public string FormatText(object? value) => $"<{value}>";
        public IEnumerable<FormattedToken> FormatTokens(object? value)
        {
            yield return new FormattedToken(FormatText(value), TokenType.Keyword);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Registration
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Register_NullFormatter_ThrowsArgumentNullException()
    {
        var registry = new ValueFormatterRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    [Fact]
    public void Register_Formatter_IsReturned_ByGetFormatters()
    {
        var registry = new ValueFormatterRegistry();
        var formatter = new FakeFormatter("Test", score: 0.5f);
        registry.Register(formatter);

        var results = registry.GetFormatters(typeof(int), 42);

        Assert.Contains(formatter, results);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetFormatters
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetFormatters_ReturnsEmpty_WhenNoFormattersRegistered()
    {
        var registry = new ValueFormatterRegistry();
        var results = registry.GetFormatters(typeof(int), 42);
        Assert.Empty(results);
    }

    [Fact]
    public void GetFormatters_ExcludesFormatters_WithZeroScore()
    {
        var registry = new ValueFormatterRegistry();
        registry.Register(new FakeFormatter("Zero", score: 0f));
        registry.Register(new FakeFormatter("Negative", score: -0.1f));

        var results = registry.GetFormatters(typeof(int), 42);

        Assert.Empty(results);
    }

    [Fact]
    public void GetFormatters_IncludesFormatters_WithPositiveScore()
    {
        var registry = new ValueFormatterRegistry();
        registry.Register(new FakeFormatter("Low", score: 0.3f));
        registry.Register(new FakeFormatter("High", score: 0.9f));

        var results = registry.GetFormatters(typeof(int), 42);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void GetFormatters_ReturnsResultsOrderedByDescendingScore()
    {
        var registry = new ValueFormatterRegistry();
        registry.Register(new FakeFormatter("A", score: 0.3f));
        registry.Register(new FakeFormatter("B", score: 0.9f));
        registry.Register(new FakeFormatter("C", score: 0.6f));

        var results = registry.GetFormatters(typeof(int), 42);

        var scores = results.Select(f => f.GetScore(typeof(int), 42)).ToList();
        for (int i = 0; i < scores.Count - 1; i++)
        {
            Assert.True(scores[i] >= scores[i + 1], "Results should be ordered by descending score.");
        }
    }

    [Fact]
    public void GetFormatters_FiltersOutFormatters_NotApplicableToType()
    {
        var registry = new ValueFormatterRegistry();
        registry.Register(new TypeSpecificFormatter(typeof(string), score: 0.9f));

        var results = registry.GetFormatters(typeof(int), 42);

        Assert.Empty(results);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetAutoFormatter
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetAutoFormatter_ReturnsNull_WhenNoFormattersRegistered()
    {
        var registry = new ValueFormatterRegistry();
        var result = registry.GetAutoFormatter(typeof(int), 42);
        Assert.Null(result);
    }

    [Fact]
    public void GetAutoFormatter_ReturnsNull_WhenBestScoreBelow0Point8()
    {
        var registry = new ValueFormatterRegistry();
        registry.Register(new FakeFormatter("Low", score: 0.5f));
        registry.Register(new FakeFormatter("Medium", score: 0.79f));

        var result = registry.GetAutoFormatter(typeof(int), 42);

        Assert.Null(result);
    }

    [Fact]
    public void GetAutoFormatter_ReturnsFormatter_WhenScoreAtThreshold()
    {
        var registry = new ValueFormatterRegistry();
        var autoFmt = new FakeFormatter("Auto", score: 0.8f);
        registry.Register(autoFmt);

        var result = registry.GetAutoFormatter(typeof(int), 42);

        Assert.Same(autoFmt, result);
    }

    [Fact]
    public void GetAutoFormatter_ReturnsHighestScoringAutoFormatter()
    {
        var registry = new ValueFormatterRegistry();
        registry.Register(new FakeFormatter("Good", score: 0.85f));
        var best = new FakeFormatter("Best", score: 0.95f);
        registry.Register(best);
        registry.Register(new FakeFormatter("JustThreshold", score: 0.8f));

        var result = registry.GetAutoFormatter(typeof(int), 42);

        Assert.Same(best, result);
    }
}

/// <summary>
/// Tests for the <see cref="FormattedToken"/> struct and <see cref="TokenType"/> enum.
/// </summary>
public sealed class FormattedTokenTests
{
    [Fact]
    public void FormattedToken_StoresTextAndType()
    {
        var token = new FormattedToken("hello", TokenType.String);
        Assert.Equal("hello", token.Text);
        Assert.Equal(TokenType.String, token.Type);
    }

    [Fact]
    public void TokenType_HasExpectedMembers()
    {
        var names = Enum.GetNames(typeof(TokenType));
        Assert.Contains("Number",      names);
        Assert.Contains("String",      names);
        Assert.Contains("Boolean",     names);
        Assert.Contains("Enum",        names);
        Assert.Contains("Keyword",     names);
        Assert.Contains("Punctuation", names);
        Assert.Contains("Null",        names);
        Assert.Contains("Default",     names);
    }

    [Fact]
    public void TokenType_Default_HasZeroValue()
    {
        Assert.Equal(0, (int)TokenType.Default);
    }
}
