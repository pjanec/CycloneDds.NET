using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CycloneDDS.Schema;

namespace DdsMonitor.Engine.Json;

/// <summary>
/// ME1-C08: A <see cref="JsonConverterFactory"/> that handles <c>[DdsUnion]</c> types by
/// serializing only the discriminator field and the currently active case arm.
/// Inactive union arms are omitted from the JSON output to keep exported payloads compact
/// and free of data-less fields.
/// </summary>
public sealed class DdsUnionJsonConverterFactory : JsonConverterFactory
{
    /// <summary>Singleton instance.</summary>
    public static readonly DdsUnionJsonConverterFactory Instance = new();

    private DdsUnionJsonConverterFactory() { }

    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.GetCustomAttribute<DdsUnionAttribute>() != null;

    /// <inheritdoc/>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(DdsUnionJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>
/// Generic JSON converter for a single <c>[DdsUnion]</c> type <typeparamref name="T"/>.
/// On write, only the discriminator and the active case arm are included.
/// On read, standard reflection-based deserialization is used (all fields are accepted).
/// </summary>
internal sealed class DdsUnionJsonConverter<T> : JsonConverter<T>
{
    private static readonly Type _type = typeof(T);

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Strip this converter from options to avoid infinite recursion, then delegate.
        var stripped = StripSelf(options);
        return JsonSerializer.Deserialize<T>(ref reader, stripped);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        // Locate the discriminator field.
        MemberInfo? discMember = null;
        foreach (var m in GetMembers(_type))
        {
            if (m.GetCustomAttribute<DdsDiscriminatorAttribute>() != null)
            {
                discMember = m;
                break;
            }
        }

        if (discMember == null)
        {
            // Not a properly annotated union — fall back to default serialization.
            var stripped = StripSelf(options);
            JsonSerializer.Serialize(writer, value, _type, stripped);
            return;
        }

        object? discValue = GetMemberValue(discMember, value);

        // Determine the active arm: find a [DdsCase] arm whose value matches the discriminator,
        // or fall back to the [DdsDefaultCase] arm when no explicit match is found.
        MemberInfo? activeArm = null;
        bool anyExplicitMatch = false;

        foreach (var m in GetMembers(_type))
        {
            if (m == discMember) continue;
            var caseAttr = m.GetCustomAttribute<DdsCaseAttribute>();
            if (caseAttr != null && UnionValuesEqual(caseAttr.Value, discValue))
            {
                activeArm = m;
                anyExplicitMatch = true;
                break;
            }
        }

        if (!anyExplicitMatch)
        {
            foreach (var m in GetMembers(_type))
            {
                if (m.GetCustomAttribute<DdsDefaultCaseAttribute>() != null)
                {
                    activeArm = m;
                    break;
                }
            }
        }

        // Write JSON: only discriminator + active arm.
        writer.WriteStartObject();

        writer.WritePropertyName(discMember.Name);
        JsonSerializer.Serialize(writer, discValue, GetMemberType(discMember), options);

        if (activeArm != null)
        {
            var armValue = GetMemberValue(activeArm, value);
            writer.WritePropertyName(activeArm.Name);
            JsonSerializer.Serialize(writer, armValue, GetMemberType(activeArm), options);
        }

        writer.WriteEndObject();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static JsonSerializerOptions StripSelf(JsonSerializerOptions options)
    {
        var stripped = new JsonSerializerOptions(options);
        for (int i = stripped.Converters.Count - 1; i >= 0; i--)
        {
            if (stripped.Converters[i] is DdsUnionJsonConverterFactory)
            {
                stripped.Converters.RemoveAt(i);
                break;
            }
        }
        return stripped;
    }

    private static System.Collections.Generic.IEnumerable<MemberInfo> GetMembers(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var f in type.GetFields(flags))
            yield return f;
        foreach (var p in type.GetProperties(flags))
            if (p.GetMethod != null && p.GetIndexParameters().Length == 0)
                yield return p;
    }

    private static object? GetMemberValue(MemberInfo member, object target)
        => member switch
        {
            FieldInfo f => f.GetValue(target),
            PropertyInfo p => p.GetValue(target),
            _ => null
        };

    private static Type GetMemberType(MemberInfo member)
        => member switch
        {
            FieldInfo f => f.FieldType,
            PropertyInfo p => p.PropertyType,
            _ => typeof(object)
        };

    private static bool UnionValuesEqual(object a, object? b)
    {
        if (b == null) return false;
        if (a.Equals(b)) return true;
        try { return Convert.ToInt64(a) == Convert.ToInt64(b); }
        catch { return false; }
    }
}
