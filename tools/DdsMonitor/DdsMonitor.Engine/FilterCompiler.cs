using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace DdsMonitor.Engine;

/// <summary>
/// Compiles user filter expressions into predicates using Dynamic LINQ.
/// </summary>
public sealed class FilterCompiler : IFilterCompiler
{
    private static readonly Regex PayloadFieldRegex = new(
        "\\bPayload\\.([A-Za-z_][A-Za-z0-9_]*(?:\\.[A-Za-z_][A-Za-z0-9_]*)*)",
        RegexOptions.Compiled);

    /// <inheritdoc />
    public FilterResult Compile(string expression, TopicMetadata? topicMeta)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return new FilterResult(false, null, "Expression is required.");
        }

        try
        {
            var config = new ParsingConfig
            {
                ConvertObjectToSupportComparison = true
            };
            var parameter = Expression.Parameter(typeof(SampleData), "it");
            var (rewritten, payloadFields) = PrepareExpression(expression, topicMeta);

            if (payloadFields.Count == 0)
            {
                var lambda = DynamicExpressionParser.ParseLambda(
                    config,
                    new[] { parameter },
                    typeof(bool),
                    rewritten);

                return new FilterResult(true, (Func<SampleData, bool>)lambda.Compile(), null);
            }

            if (topicMeta == null)
            {
                return new FilterResult(false, null, "Topic metadata is required for payload field filters.");
            }

            var parameters = new List<ParameterExpression> { parameter };
            for (var i = 0; i < payloadFields.Count; i++)
            {
                parameters.Add(Expression.Parameter(payloadFields[i].ValueType, GetPayloadParameterName(i)));
            }

            var payloadLambda = DynamicExpressionParser.ParseLambda(
                config,
                parameters.ToArray(),
                typeof(bool),
                rewritten);

            var compiled = payloadLambda.Compile();
            Func<SampleData, bool> predicate = sample =>
            {
                var args = new object?[payloadFields.Count + 1];
                args[0] = sample;

                for (var i = 0; i < payloadFields.Count; i++)
                {
                    args[i + 1] = GetFieldValue(sample, payloadFields[i]);
                }

                return (bool)compiled.DynamicInvoke(args)!;
            };

            return new FilterResult(true, predicate, null);
        }
        catch (Exception ex)
        {
            return new FilterResult(false, null, ex.Message);
        }
    }

    private static (string Expression, List<FieldMetadata> Fields) PrepareExpression(
        string expression,
        TopicMetadata? topicMeta)
    {
        var fields = new List<FieldMetadata>();
        var fieldMap = new Dictionary<string, FieldMetadata>(StringComparer.Ordinal);

        var rewritten = PayloadFieldRegex.Replace(expression, match =>
        {
            var fieldPath = match.Groups[1].Value;
            if (!fieldMap.TryGetValue(fieldPath, out var field))
            {
                if (topicMeta == null)
                {
                    throw new InvalidOperationException("Topic metadata is required for payload field filters.");
                }

                field = topicMeta.AllFields.FirstOrDefault(f =>
                    string.Equals(f.StructuredName, fieldPath, StringComparison.Ordinal));

                if (field == null)
                {
                    throw new InvalidOperationException($"Unknown payload field '{fieldPath}'.");
                }

                fieldMap[fieldPath] = field;
                fields.Add(field);
            }

            return GetPayloadParameterName(fields.IndexOf(field));
        });

        return (rewritten, fields);
    }

    private static string GetPayloadParameterName(int index) => $"field{index}";

    private static object? GetFieldValue(SampleData sample, FieldMetadata field)
    {
        var target = field.IsSynthetic ? sample : sample.Payload;
        return field.Getter(target!);
    }

}
