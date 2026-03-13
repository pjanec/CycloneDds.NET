using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DdsMonitor.Engine.Json;

/// <summary>
/// <see cref="JsonConverterFactory"/> that handles compiler-generated fixed-buffer value types
/// (i.e. the nested struct emitted for <c>public unsafe fixed T Name[N]</c>).
/// Those structs have exactly one public instance field named <c>FixedElementField</c>.
///
/// Without this converter <see cref="System.Text.Json"/> serializes them as
/// <c>{ "FixedElementField": 0 }</c>; with it they are emitted / consumed as a proper
/// JSON array of the element type such as <c>[0, 1, 2, …]</c>.
/// </summary>
public sealed class FixedBufferJsonConverterFactory : JsonConverterFactory
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly FixedBufferJsonConverterFactory Instance = new();

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsValueType)
        {
            return false;
        }

        var fields = typeToConvert.GetFields(BindingFlags.Public | BindingFlags.Instance);
        return fields.Length == 1 && fields[0].Name == "FixedElementField";
    }

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(FixedBufferJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>
/// Generic converter for a single fixed-buffer value type <typeparamref name="TBuffer"/>.
/// Serialises as a JSON array; deserialises by writing elements back via <see cref="Marshal"/>.
/// </summary>
/// <typeparam name="TBuffer">The compiler-generated fixed-buffer struct type.</typeparam>
internal sealed class FixedBufferJsonConverter<TBuffer> : JsonConverter<TBuffer>
    where TBuffer : struct
{
    private static readonly FieldInfo ElemField =
        typeof(TBuffer).GetFields(BindingFlags.Public | BindingFlags.Instance)[0];

    private static readonly Type ElementType = ElemField.FieldType;

    private static readonly int ElemSize = Marshal.SizeOf(ElementType);

    private static readonly int Count = Marshal.SizeOf<TBuffer>() / ElemSize;

    // ─────────────────────────────────────────────────────────────────────────
    // Write
    // ─────────────────────────────────────────────────────────────────────────

    public override void Write(Utf8JsonWriter writer, TBuffer value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        var boxed = (object)value;
        var handle = GCHandle.Alloc(boxed, GCHandleType.Pinned);
        try
        {
            var ptr = handle.AddrOfPinnedObject();
            for (int i = 0; i < Count; i++)
            {
                WriteElement(writer, TopicMetadata.ReadMarshalElement(ptr + (i * ElemSize), ElementType));
            }
        }
        finally
        {
            handle.Free();
        }

        writer.WriteEndArray();
    }

    private static void WriteElement(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case byte v:   writer.WriteNumberValue(v); break;
            case sbyte v:  writer.WriteNumberValue(v); break;
            case short v:  writer.WriteNumberValue(v); break;
            case ushort v: writer.WriteNumberValue(v); break;
            case int v:    writer.WriteNumberValue(v); break;
            case uint v:   writer.WriteNumberValue(v); break;
            case long v:   writer.WriteNumberValue(v); break;
            case ulong v:  writer.WriteNumberValue(v); break;
            case float v:  writer.WriteNumberValue(v); break;
            case double v: writer.WriteNumberValue(v); break;
            case bool v:   writer.WriteBooleanValue(v); break;
            case char v:   writer.WriteNumberValue((ushort)v); break;
            default:       writer.WriteNullValue(); break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Read
    // ─────────────────────────────────────────────────────────────────────────

    public override TBuffer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        TBuffer buf = default;

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            // Gracefully skip unexpected tokens (e.g. old exports that stored an object).
            reader.Skip();
            return buf;
        }

        var boxed = (object)buf;
        var handle = GCHandle.Alloc(boxed, GCHandleType.Pinned);
        try
        {
            var ptr = handle.AddrOfPinnedObject();
            int index = 0;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (index >= Count)
                {
                    // More elements in JSON than the buffer can hold; skip the remainder.
                    reader.Skip();
                    continue;
                }

                var element = ReadElement(ref reader, ElementType);
                if (element != null)
                {
                    TopicMetadata.WriteMarshalElement(ptr + (index * ElemSize), element, ElementType);
                }

                index++;
            }
        }
        finally
        {
            handle.Free();
        }

        return (TBuffer)boxed;
    }

    private static object? ReadElement(ref Utf8JsonReader reader, Type elementType)
    {
        try
        {
            if (elementType == typeof(bool))   return reader.GetBoolean();
            if (elementType == typeof(byte))   return reader.GetByte();
            if (elementType == typeof(sbyte))  return reader.GetSByte();
            if (elementType == typeof(short))  return reader.GetInt16();
            if (elementType == typeof(ushort)) return reader.GetUInt16();
            if (elementType == typeof(int))    return reader.GetInt32();
            if (elementType == typeof(uint))   return reader.GetUInt32();
            if (elementType == typeof(long))   return reader.GetInt64();
            if (elementType == typeof(ulong))  return reader.GetUInt64();
            if (elementType == typeof(float))  return reader.GetSingle();
            if (elementType == typeof(double)) return reader.GetDouble();
            if (elementType == typeof(char))   return (char)reader.GetUInt16();
        }
        catch
        {
            // Ignore malformed tokens; element stays at default.
        }

        return null;
    }
}
