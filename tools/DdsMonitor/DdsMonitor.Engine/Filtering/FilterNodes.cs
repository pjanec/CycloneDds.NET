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

    /// <summary>
    /// Builds the Dynamic LINQ expression string.  For string-method operators
    /// (<see cref="FilterComparisonOperator.StartsWith"/> etc.) the values are appended to
    /// <paramref name="paramValues"/> and the expression references them as <c>@N</c>
    /// parameters, keeping the generated string safe for all comparison operators.
    /// </summary>
    public string ToDynamicLinqString(IList<object?> paramValues)
    {
        var core = BuildLinq(paramValues);
        return IsNegated ? $"!({core})" : core;
    }

    /// <summary>
    /// Backward-compatible overload that discards collected parameter values.
    /// Suitable for non-string-method expressions (all comparison operators other
    /// than <c>StartsWith</c>, <c>EndsWith</c>, <c>Contains</c>) where values are
    /// embedded directly into the expression string.
    /// </summary>
    public string ToDynamicLinqString()
    {
        return ToDynamicLinqString(new List<object?>());
    }

    protected abstract string BuildLinq(IList<object?> paramValues);
}

public sealed class FilterGroupNode : FilterNode
{
    public FilterGroupOperator Operator { get; set; } = FilterGroupOperator.And;

    public List<FilterNode> Children { get; } = new();

    protected override string BuildLinq(IList<object?> paramValues)
    {
        if (Children.Count == 0)
        {
            return Operator == FilterGroupOperator.And ? "true" : "false";
        }

        if (Children.Count == 1)
        {
            return Children[0].ToDynamicLinqString(paramValues);
        }

        var joiner = Operator == FilterGroupOperator.And ? " and " : " or ";
        var builder = new StringBuilder();

        for (var i = 0; i < Children.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(joiner);
            }

            builder.Append(Children[i].ToDynamicLinqString(paramValues));
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

    protected override string BuildLinq(IList<object?> paramValues)
    {
        var formattedValue = FormatValue(paramValues);

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

    private string FormatValue(IList<object?> paramValues)
    {
        // String-method operators use @N parameter syntax so that the value is
        // passed as a strongly-typed argument to ParseLambda, avoiding inline
        // string escaping issues and providing clean expression output.
        if (Operator is FilterComparisonOperator.StartsWith
                     or FilterComparisonOperator.EndsWith
                     or FilterComparisonOperator.Contains)
        {
            var idx = paramValues.Count;
            paramValues.Add(ValueText ?? string.Empty);
            return $"@{idx}";
        }

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
            // Dynamic LINQ cannot resolve arbitrary namespaced enum literals such as
            // "Company.DDS.Level.Ok" because it has no reference to that assembly.
            // Instead we emit the underlying integer value which is always resolvable
            // when compared to an enum-typed Dynamic LINQ parameter.
            try
            {
                var parsed = Enum.Parse(valueType, ValueText, ignoreCase: true);
                return Convert.ToInt32(parsed).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return "0";
            }
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
