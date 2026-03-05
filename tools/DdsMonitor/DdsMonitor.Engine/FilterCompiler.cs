using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Reflection;
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

            // When topicMeta is null, dynamic fields were created via CreateDynamicField().
            // Wrap the predicate in a safe try/catch so missing properties return false.
            var isDynamic = topicMeta == null;

            if (isDynamic)
            {
                // Postpone type-specific compilation to first evaluation against each payload type.
                var lazyPredicate = BuildDynamicNullMetaPredicate(expression);
                return new FilterResult(true, lazyPredicate, null);
            }

            var parameters = new List<ParameterExpression> { parameter };
            for (var i = 0; i < payloadFields.Count; i++)
            {
                // Enum fields are promoted to int so that integer literals emitted by
                // FilterConditionNode.FormatValue() can be compared without Dynamic LINQ
                // needing to resolve the fully-qualified enum type name.
                var paramType = payloadFields[i].ValueType.IsEnum ? typeof(int) : payloadFields[i].ValueType;
                parameters.Add(Expression.Parameter(paramType, GetPayloadParameterName(i)));
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
                    // Dynamic mode: create a reflection-based accessor for unknown payload fields.
                    field = CreateDynamicField(fieldPath);
                }
                else
                {
                    field = topicMeta.AllFields.FirstOrDefault(f =>
                        string.Equals(f.StructuredName, fieldPath, StringComparison.Ordinal));

                    if (field == null)
                    {
                        throw new InvalidOperationException($"Unknown payload field '{fieldPath}'.");
                    }
                }

                fieldMap[fieldPath] = field;
                fields.Add(field);
            }

            return GetPayloadParameterName(fields.IndexOf(field));
        });

        return (rewritten, fields);
    }

    /// <summary>
    /// Creates a <see cref="FieldMetadata"/> that accesses a named property path via reflection at runtime.
    /// Used when <see cref="TopicMetadata"/> is unavailable (e.g., All-Topics mode).
    /// </summary>
    private static FieldMetadata CreateDynamicField(string fieldPath)
    {
        return new FieldMetadata(
            fieldPath,
            fieldPath,
            typeof(object),
            payload => GetPropertyByPath(payload, fieldPath),
            (_, __) => { },
            isSynthetic: false);
    }

    /// <summary>
    /// Traverses a dot-separated property path on an object using reflection.
    /// Returns <c>null</c> if any segment does not exist.
    /// </summary>
    private static object? GetPropertyByPath(object? obj, string path)
    {
        if (obj == null)
        {
            return null;
        }

        var current = obj;
        foreach (var part in path.Split('.'))
        {
            if (current == null)
            {
                return null;
            }

            var prop = current.GetType().GetProperty(
                part,
                BindingFlags.Public | BindingFlags.Instance);

            if (prop == null)
            {
                return null;
            }

            current = prop.GetValue(current);
        }

        return current;
    }

    private static string GetPayloadParameterName(int index) => $"field{index}";

    private static object? GetFieldValue(SampleData sample, FieldMetadata field)
    {
        var target = field.IsSynthetic ? sample : sample.Payload;
        var value = field.Getter(target!);
        // Enum values are promoted to int to match the Dynamic LINQ parameter type.
        if (value != null && value.GetType().IsEnum)
        {
            return Convert.ToInt32(value);
        }

        return value;
    }

    /// <summary>
    /// Builds a predicate that defers payload-type-specific compilation to the first
    /// time each payload type is encountered. This handles null <see cref="TopicMetadata"/>
    /// by discovering field types at runtime from the actual payload object type.
    /// </summary>
    private static Func<SampleData, bool> BuildDynamicNullMetaPredicate(string expression)
    {
        var cache = new Dictionary<Type, Func<SampleData, bool>>();
        var syncLock = new object();

        return sample =>
        {
            var payloadType = sample.Payload?.GetType();
            if (payloadType == null)
            {
                return false;
            }

            Func<SampleData, bool>? typedPredicate;
            lock (syncLock)
            {
                if (!cache.TryGetValue(payloadType, out typedPredicate))
                {
                    typedPredicate = CompileForPayloadType(expression, payloadType);
                    cache[payloadType] = typedPredicate;
                }
            }

            return typedPredicate(sample);
        };
    }

    /// <summary>
    /// Compiles a filter expression for a specific payload type using its <see cref="TopicMetadata"/>.
    /// Falls back to returning <c>false</c> for all samples if compilation or metadata fails.
    /// </summary>
    private static Func<SampleData, bool> CompileForPayloadType(string expression, Type payloadType)
    {
        try
        {
            var typedMeta = new TopicMetadata(payloadType);
            var result = new FilterCompiler().Compile(expression, typedMeta);
            if (result.IsValid && result.Predicate != null)
            {
                return result.Predicate;
            }
        }
        catch
        {
            // TopicMetadata construction failed (e.g., missing [DdsTopic] attribute)
            // or compilation failed for this type. Fall through to the safe default.
        }

        // Property doesn't exist on this type or compilation failed → no match.
        return _ => false;
    }

}
