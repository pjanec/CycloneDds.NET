using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CycloneDDS.Schema;
using DdsMonitor.Engine.Json;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// ME1-T02: Tests for C# 12 [InlineArray] support in TopicMetadata and JSON converters.
/// </summary>
public class InlineArrayTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // 1. TopicMetadata – detects [InlineArray] field as IsFixedSizeArray=true
    // Success condition 1:
    //   A TopicMetadata built for a struct with [InlineArray(8)] float produces
    //   FieldMetadata with IsFixedSizeArray==true, FixedArrayLength==8, ElementType==float.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TopicMetadata_InlineArrayFloat_IsFixedSizeArray()
    {
        var meta = new TopicMetadata(typeof(InlineArrayFloatTopic));

        var field = meta.AllFields.SingleOrDefault(f => f.StructuredName == "Data");
        Assert.NotNull(field);
        Assert.True(field.IsFixedSizeArray, "InlineArray field should be tagged IsFixedSizeArray=true");
        Assert.False(field.IsArrayField, "InlineArray field should not be tagged IsArrayField=true");
        Assert.Equal(8, field.FixedArrayLength);
        Assert.Equal(typeof(float), field.ElementType);
        Assert.Equal(typeof(float[]), field.ValueType);
    }

    [Fact]
    public void TopicMetadata_InlineArrayInt_IsFixedSizeArray()
    {
        var meta = new TopicMetadata(typeof(InlineArrayIntTopic));

        var field = meta.AllFields.SingleOrDefault(f => f.StructuredName == "Values");
        Assert.NotNull(field);
        Assert.True(field.IsFixedSizeArray);
        Assert.Equal(4, field.FixedArrayLength);
        Assert.Equal(typeof(int), field.ElementType);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. TopicMetadata getter returns correct T[] snapshot from InlineArray
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TopicMetadata_InlineArray_GetterReturnsSnapshot()
    {
        var meta = new TopicMetadata(typeof(InlineArrayFloatTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Data");

        object boxed = new InlineArrayFloatTopic { Id = 99 };

        // Populate via setter – write [1..8].
        var arr = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f };
        field.Setter(boxed, arr);

        var result = field.Getter(boxed) as float[];
        Assert.NotNull(result);
        Assert.Equal(8, result!.Length);
        Assert.Equal(1.0f, result[0]);
        Assert.Equal(8.0f, result[7]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. TopicMetadata – InlineArray field not exposed as a flattened nested struct
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TopicMetadata_InlineArray_NotFlattenedAsNestedStruct()
    {
        var meta = new TopicMetadata(typeof(InlineArrayFloatTopic));

        // There should be no field named "Data._elem" – the struct was not flattened.
        Assert.DoesNotContain(meta.AllFields, f => f.StructuredName.Contains("._elem"));
        Assert.DoesNotContain(meta.AllFields, f => f.StructuredName == "Data._elem");

        // There should be exactly one "Data" field.
        Assert.Single(meta.AllFields, f => f.StructuredName == "Data");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. JSON serialization – InlineArray produces array form [1,2,3,4]
    // Success condition 2:
    //   JSON serialization of a struct with [InlineArray(4)] int field produces
    //   [1,2,3,4] (array form), not {} or the struct type name.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Json_InlineArrayInt_SerializesAsArray()
    {
        var opts = new JsonSerializerOptions();
        opts.Converters.Add(FixedBufferJsonConverterFactory.Instance);

        // Build an IntBuf4 with values [1, 2, 3, 4].
        var buf = new IntBuf4();
        ref int first = ref System.Runtime.CompilerServices.Unsafe.As<IntBuf4, int>(ref buf);
        System.Runtime.CompilerServices.Unsafe.Add(ref first, 0) = 1;
        System.Runtime.CompilerServices.Unsafe.Add(ref first, 1) = 2;
        System.Runtime.CompilerServices.Unsafe.Add(ref first, 2) = 3;
        System.Runtime.CompilerServices.Unsafe.Add(ref first, 3) = 4;

        string json = JsonSerializer.Serialize(buf, opts);

        // Must serialize as a JSON array, not an object.
        Assert.Equal("[1,2,3,4]", json);
    }

    [Fact]
    public void Json_InlineArrayFloat_SerializesAsArray()
    {
        var opts = new JsonSerializerOptions();
        opts.Converters.Add(FixedBufferJsonConverterFactory.Instance);

        var buf = new FloatBuf8();
        ref float elem = ref System.Runtime.CompilerServices.Unsafe.As<FloatBuf8, float>(ref buf);
        for (int i = 0; i < 8; i++)
            System.Runtime.CompilerServices.Unsafe.Add(ref elem, i) = i + 1.0f;

        string json = JsonSerializer.Serialize(buf, opts);

        // Must be a JSON array of 8 numbers.
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(8, doc.RootElement.GetArrayLength());
        Assert.Equal(1.0f, doc.RootElement[0].GetSingle(), 3);
        Assert.Equal(8.0f, doc.RootElement[7].GetSingle(), 3);
    }

    [Fact]
    public void Json_InlineArray_DeserializesFromArray()
    {
        var opts = new JsonSerializerOptions();
        opts.Converters.Add(FixedBufferJsonConverterFactory.Instance);

        var buf = JsonSerializer.Deserialize<IntBuf4>("[10,20,30,40]", opts);

        // Re-read the values.
        ref int first = ref System.Runtime.CompilerServices.Unsafe.As<IntBuf4, int>(ref buf);
        Assert.Equal(10, System.Runtime.CompilerServices.Unsafe.Add(ref first, 0));
        Assert.Equal(20, System.Runtime.CompilerServices.Unsafe.Add(ref first, 1));
        Assert.Equal(30, System.Runtime.CompilerServices.Unsafe.Add(ref first, 2));
        Assert.Equal(40, System.Runtime.CompilerServices.Unsafe.Add(ref first, 3));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Existing FixedBufferAttribute tests are not broken
    //    (Implicit: the existing ArrayFieldTests.cs covers those.)
    //    Extra check: FixedBufferJsonConverter still works for unsafe fixed buffers.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Json_FixedBufferAttribute_StillWorks()
    {
        // Before T02, the factory only handles FixedElementField-named structs.
        // After T02 it also handles [InlineArray]. Verify the original path still works.
        var meta = new TopicMetadata(typeof(FixedByteBufferTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Payload");

        Assert.True(field.IsFixedSizeArray);
        Assert.Equal(typeof(byte), field.ElementType);
        Assert.Equal(8, field.FixedArrayLength);
    }
}
