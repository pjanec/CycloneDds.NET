using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace DdsMonitor.Engine;

public enum FilterGroupOperator
{
    And,
    Or
}

public enum FilterComparisonOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    StartsWith,
    EndsWith,
    Contains
}

public abstract class FilterNode
{
    public bool IsNegated { get; set; }

    public string ToDynamicLinqString()
    {
        var core = BuildLinq();
        return IsNegated ? $"!({core})" : core;
    }

    protected abstract string BuildLinq();
}

public sealed class FilterGroupNode : FilterNode
{
    public FilterGroupOperator Operator { get; set; } = FilterGroupOperator.And;

    public List<FilterNode> Children { get; } = new();

    protected override string BuildLinq()
    {
        if (Children.Count == 0)
        {
            return Operator == FilterGroupOperator.And ? "true" : "false";
        }

        if (Children.Count == 1)
        {
            return Children[0].ToDynamicLinqString();
        }

        var joiner = Operator == FilterGroupOperator.And ? " and " : " or ";
        var builder = new StringBuilder();

        for (var i = 0; i < Children.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(joiner);
            }

            builder.Append(Children[i].ToDynamicLinqString());
        }

        return $"({builder})";
    }
}

public sealed class FilterConditionNode : FilterNode
{
    public string FieldPath { get; set; } = string.Empty;

    public FilterComparisonOperator Operator { get; set; } = FilterComparisonOperator.Equals;

    public string? ValueText { get; set; }

    public string? ValueTypeName { get; set; }

    [JsonIgnore]
    public Type? ValueType => string.IsNullOrWhiteSpace(ValueTypeName) ? null : Type.GetType(ValueTypeName);

    protected override string BuildLinq()
    {
        var formattedValue = FormatValue();

        return Operator switch
        {
            FilterComparisonOperator.StartsWith => $"{FieldPath}.StartsWith({formattedValue})",
            FilterComparisonOperator.EndsWith => $"{FieldPath}.EndsWith({formattedValue})",
            FilterComparisonOperator.Contains => $"{FieldPath}.Contains({formattedValue})",
            FilterComparisonOperator.NotEquals => $"{FieldPath} != {formattedValue}",
            FilterComparisonOperator.GreaterThan => $"{FieldPath} > {formattedValue}",
            FilterComparisonOperator.GreaterThanOrEqual => $"{FieldPath} >= {formattedValue}",
            FilterComparisonOperator.LessThan => $"{FieldPath} < {formattedValue}",
            FilterComparisonOperator.LessThanOrEqual => $"{FieldPath} <= {formattedValue}",
            _ => $"{FieldPath} == {formattedValue}"
        };
    }

    private string FormatValue()
    {
        if (ValueText == null)
        {
            return "null";
        }

        var valueType = ValueType;
        if (valueType == null)
        {
            return QuoteString(ValueText);
        }

        if (valueType == typeof(string))
        {
            return QuoteString(ValueText);
        }

        if (valueType == typeof(bool))
        {
            return bool.TryParse(ValueText, out var flag) ? flag.ToString().ToLowerInvariant() : "false";
        }

        if (valueType == typeof(DateTime))
        {
            return $"DateTime.Parse({QuoteString(ValueText)})";
        }

        if (valueType == typeof(DateTimeOffset))
        {
            return $"DateTimeOffset.Parse({QuoteString(ValueText)})";
        }

        if (valueType.IsEnum)
        {
            return $"{valueType.FullName}.{ValueText}";
        }

        if (TryFormatNumber(ValueText, out var formatted))
        {
            return formatted;
        }

        return QuoteString(ValueText);
    }

    private static bool TryFormatNumber(string valueText, out string formatted)
    {
        if (double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            formatted = number.ToString("0.################", CultureInfo.InvariantCulture);
            return true;
        }

        formatted = string.Empty;
        return false;
    }

    private static string QuoteString(string value)
    {
        var escaped = value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
