using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using CycloneDDS.Schema;

namespace DdsMonitor.Engine.Json;

/// <summary>
/// <see cref="JsonConverterFactory"/> that handles all CycloneDDS FixedStringN types
/// (FixedString32, FixedString64, FixedString128, FixedString256).
/// Serialises as a JSON string using <c>ToString()</c>; deserialises from a JSON string.
/// Without this converter the default serializer emits only <c>{"Length": N}</c>
/// because the fixed <c>_buffer</c> field is not visible to reflection.
/// </summary>
public sealed class FixedStringJsonConverterFactory : JsonConverterFactory
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly FixedStringJsonConverterFactory Instance = new();

    private static readonly Type[] FixedStringTypes =
    {
        typeof(FixedString32),
        typeof(FixedString64),
        typeof(FixedString128),
        typeof(FixedString256),
    };

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        foreach (var t in FixedStringTypes)
        {
            if (typeToConvert == t) return true;
        }
        return false;
    }

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(FixedString32))  return new FixedStringConverter<FixedString32>(s => new FixedString32(s));
        if (typeToConvert == typeof(FixedString64))  return new FixedStringConverter<FixedString64>(s => new FixedString64(s));
        if (typeToConvert == typeof(FixedString128)) return new FixedStringConverter<FixedString128>(s => new FixedString128(s));
        if (typeToConvert == typeof(FixedString256)) return new FixedStringConverter<FixedString256>(s => new FixedString256(s));
        return null;
    }
}

/// <summary>
/// Generic JSON converter for a single FixedString value type.
/// Reads and writes as a plain JSON string.
/// </summary>
internal sealed class FixedStringConverter<T> : JsonConverter<T>
    where T : struct
{
    private readonly Func<string, T> _fromString;

    internal FixedStringConverter(Func<string, T> fromString)
    {
        _fromString = fromString;
    }

    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString() ?? string.Empty;
        try
        {
            return _fromString(text);
        }
        catch (ArgumentException)
        {
            // String too long or invalid – return a default (empty) value.
            return default;
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
