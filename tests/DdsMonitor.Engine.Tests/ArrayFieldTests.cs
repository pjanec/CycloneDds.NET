using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CycloneDDS.Schema;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for array and fixed-size buffer support in DdsMonitor TopicMetadata
/// and FieldMetadata.
/// </summary>
public unsafe class ArrayFieldTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // 1. FieldMetadata model – new properties
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FieldMetadata_DefaultArrayProperties_AreFalseAndNull()
    {
        var meta = new FieldMetadata(
            "X", "X", typeof(int),
            _ => null,
            (_, __) => { },
            isSynthetic: false);

        Assert.False(meta.IsArrayField);
        Assert.False(meta.IsFixedSizeArray);
        Assert.Null(meta.ElementType);
        Assert.Equal(-1, meta.FixedArrayLength);
    }

    [Fact]
    public void FieldMetadata_IsArrayField_IsSet()
    {
        var meta = new FieldMetadata(
            "Values", "Values", typeof(int[]),
            _ => null, (_, __) => { },
            isSynthetic: false,
            isArrayField: true,
            elementType: typeof(int));

        Assert.True(meta.IsArrayField);
        Assert.False(meta.IsFixedSizeArray);
        Assert.Equal(typeof(int), meta.ElementType);
        Assert.Equal(-1, meta.FixedArrayLength);
    }

    [Fact]
    public void FieldMetadata_IsFixedSizeArray_IsSet()
    {
        var meta = new FieldMetadata(
            "Payload", "Payload", typeof(byte[]),
            _ => null, (_, __) => { },
            isSynthetic: false,
            isFixedSizeArray: true,
            elementType: typeof(byte),
            fixedArrayLength: 8);

        Assert.False(meta.IsArrayField);
        Assert.True(meta.IsFixedSizeArray);
        Assert.Equal(typeof(byte), meta.ElementType);
        Assert.Equal(8, meta.FixedArrayLength);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. TopicMetadata – dynamic array (T[]) field discovery
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TopicMetadata_IntArray_DiscoveredAsArrayField()
    {
        var meta = new TopicMetadata(typeof(IntArrayTopic));

        var field = meta.AllFields.SingleOrDefault(f => f.StructuredName == "Values");
        Assert.NotNull(field);
        Assert.True(field.IsArrayField);
        Assert.False(field.IsFixedSizeArray);
        Assert.Equal(typeof(int), field.ElementType);
        Assert.Equal(typeof(int[]), field.ValueType);
    }

    [Fact]
    public void TopicMetadata_FloatList_DiscoveredAsArrayField()
    {
        var meta = new TopicMetadata(typeof(FloatListTopic));

        var field = meta.AllFields.SingleOrDefault(f => f.StructuredName == "Samples");
        Assert.NotNull(field);
        Assert.True(field.IsArrayField);
        Assert.False(field.IsFixedSizeArray);
        Assert.Equal(typeof(float), field.ElementType);
        Assert.Equal(typeof(List<float>), field.ValueType);
    }

    [Fact]
    public void TopicMetadata_ScalarField_IsNotArrayField()
    {
        var meta = new TopicMetadata(typeof(IntArrayTopic));

        var idField = meta.AllFields.SingleOrDefault(f => f.StructuredName == "Id");
        Assert.NotNull(idField);
        Assert.False(idField.IsArrayField);
        Assert.False(idField.IsFixedSizeArray);
        Assert.Null(idField.ElementType);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. TopicMetadata – fixed-size C# buffer field discovery
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TopicMetadata_FixedByteBuffer_DiscoveredAsFixedSizeArray()
    {
        var meta = new TopicMetadata(typeof(FixedByteBufferTopic));

        var field = meta.AllFields.SingleOrDefault(f => f.StructuredName == "Payload");
        Assert.NotNull(field);
        Assert.True(field.IsFixedSizeArray);
        Assert.False(field.IsArrayField);
        Assert.Equal(typeof(byte), field.ElementType);
        Assert.Equal(typeof(byte[]), field.ValueType);
        Assert.Equal(8, field.FixedArrayLength);
    }

    [Fact]
    public void TopicMetadata_FixedIntBuffer_DiscoveredAsFixedSizeArray()
    {
        var meta = new TopicMetadata(typeof(FixedIntBufferTopic));

        var field = meta.AllFields.SingleOrDefault(f => f.StructuredName == "Readings");
        Assert.NotNull(field);
        Assert.True(field.IsFixedSizeArray);
        Assert.Equal(typeof(int), field.ElementType);
        Assert.Equal(typeof(int[]), field.ValueType);
        Assert.Equal(4, field.FixedArrayLength);
    }

    [Fact]
    public void TopicMetadata_FixedBuffer_NotExposedAsCompilerGeneratedStruct()
    {
        // The compiler-generated <Payload>e__FixedBuffer struct must NOT appear
        // as a separate flattened FieldMetadata entry.
        var meta = new TopicMetadata(typeof(FixedByteBufferTopic));

        var payloadField = meta.AllFields.SingleOrDefault(f => f.StructuredName == "Payload");
        Assert.NotNull(payloadField);
        Assert.Equal(typeof(byte[]), payloadField.ValueType);

        // No "Payload.FixedElementField" or similar residue.
        var spurious = meta.AllFields.Where(f => f.StructuredName.StartsWith("Payload."));
        Assert.Empty(spurious);
    }

    [Fact]
    public void TopicMetadata_NestedFixedBuffer_DiscoveredWithDotPrefix()
    {
        var meta = new TopicMetadata(typeof(NestedFixedBufferTopic));

        // The nested struct's fixed buffer is exposed as "Sensor.Data".
        var field = meta.AllFields.SingleOrDefault(f => f.StructuredName == "Sensor.Data");
        Assert.NotNull(field);
        Assert.True(field.IsFixedSizeArray);
        Assert.Equal(typeof(byte), field.ElementType);
        Assert.Equal(4, field.FixedArrayLength);

        // Scalar nested field is still present.
        Assert.Contains(meta.AllFields, f => f.StructuredName == "Sensor.Channel");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. FieldMetadata getter – fixed-size buffer round-trip (read)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FixedBuffer_Getter_ReturnsCorrectBytes()
    {
        var meta = new TopicMetadata(typeof(FixedByteBufferTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Payload");

        // Write to a local stack variable first (valid in unsafe context),
        // then box – the heap copy carries the initialised bytes.
        var local = new FixedByteBufferTopic { Id = 1 };
        for (byte i = 0; i < 8; i++) local.Payload[i] = (byte)(10 + i);
        object payload = local;

        var arr = Assert.IsType<byte[]>(field.Getter(payload));

        Assert.Equal(8, arr.Length);
        for (int i = 0; i < 8; i++)
        {
            Assert.Equal((byte)(10 + i), arr[i]);
        }
    }

    [Fact]
    public void FixedBuffer_IntGetter_ReturnsCorrectInts()
    {
        var meta = new TopicMetadata(typeof(FixedIntBufferTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Readings");

        var local = new FixedIntBufferTopic { Id = 99 };
        local.Readings[0] = 100;
        local.Readings[1] = 200;
        local.Readings[2] = 300;
        local.Readings[3] = 400;
        object payload = local;

        var arr = Assert.IsType<int[]>(field.Getter(payload));

        Assert.Equal(4, arr.Length);
        Assert.Equal(100, arr[0]);
        Assert.Equal(200, arr[1]);
        Assert.Equal(300, arr[2]);
        Assert.Equal(400, arr[3]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. FieldMetadata setter – fixed-size buffer round-trip (write)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FixedBuffer_Setter_WritesCorrectBytes()
    {
        var meta = new TopicMetadata(typeof(FixedByteBufferTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Payload");

        object payload = new FixedByteBufferTopic { Id = 7 };
        var src = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0x01, 0x02, 0x03, 0x04 };

        field.Setter(payload, src);

        // Verify written values via the getter (avoids unsafe boxed-object pointer issues).
        var result = Assert.IsType<byte[]>(field.Getter(payload));
        Assert.Equal(src, result);
    }

    [Fact]
    public void FixedBuffer_IntSetter_WritesCorrectInts()
    {
        var meta = new TopicMetadata(typeof(FixedIntBufferTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Readings");

        object payload = new FixedIntBufferTopic { Id = 5 };
        field.Setter(payload, new int[] { 10, 20, 30, 40 });

        var result = Assert.IsType<int[]>(field.Getter(payload));
        Assert.Equal(4, result.Length);
        Assert.Equal(10, result[0]);
        Assert.Equal(20, result[1]);
        Assert.Equal(30, result[2]);
        Assert.Equal(40, result[3]);
    }

    [Fact]
    public unsafe void FixedBuffer_GetterThenSetter_Roundtrips()
    {
        var meta = new TopicMetadata(typeof(FixedByteBufferTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Payload");

        object payload = new FixedByteBufferTopic { Id = 3 };

        // Write via setter.
        var original = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        field.Setter(payload, original);

        // Read back via getter.
        var result = Assert.IsType<byte[]>(field.Getter(payload));
        Assert.Equal(original, result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. FieldMetadata getter/setter – nested fixed buffer
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NestedFixedBuffer_Getter_ReturnsCorrectBytes()
    {
        var meta = new TopicMetadata(typeof(NestedFixedBufferTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Sensor.Data");

        // Write to local stack variables first, then box.
        var sensor = new NestedSensorData { Channel = 5 };
        sensor.Data[0] = 0x10;
        sensor.Data[1] = 0x20;
        sensor.Data[2] = 0x30;
        sensor.Data[3] = 0x40;
        object payload = new NestedFixedBufferTopic { Id = 1, Sensor = sensor };

        var result = Assert.IsType<byte[]>(field.Getter(payload));
        Assert.Equal(4, result.Length);
        Assert.Equal(0x10, result[0]);
        Assert.Equal(0x20, result[1]);
        Assert.Equal(0x30, result[2]);
        Assert.Equal(0x40, result[3]);
    }

    [Fact]
    public unsafe void NestedFixedBuffer_Setter_WritesBytes_AndPreservesOtherFields()
    {
        var meta = new TopicMetadata(typeof(NestedFixedBufferTopic));
        var dataField = meta.AllFields.Single(f => f.StructuredName == "Sensor.Data");
        var chanField = meta.AllFields.Single(f => f.StructuredName == "Sensor.Channel");

        object payload = new NestedFixedBufferTopic { Id = 42, Sensor = new NestedSensorData { Channel = 7 } };
        dataField.Setter(payload, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

        // Channel must still be intact.
        Assert.Equal((short)7, chanField.Getter(payload));

        // Read back the bytes via getter.
        var result = Assert.IsType<byte[]>(dataField.Getter(payload));
        Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. Dynamic array (T[]) getter and setter
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DynamicArray_Getter_ReturnsCurrentArray()
    {
        var meta = new TopicMetadata(typeof(IntArrayTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Values");

        object payload = new IntArrayTopic { Id = 1, Values = new[] { 10, 20, 30 } };
        var result = Assert.IsType<int[]>(field.Getter(payload));

        Assert.Equal(3, result.Length);
        Assert.Equal(10, result[0]);
        Assert.Equal(20, result[1]);
        Assert.Equal(30, result[2]);
    }

    [Fact]
    public void DynamicArray_Setter_ReplacesArray()
    {
        var meta = new TopicMetadata(typeof(IntArrayTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Values");

        object payload = new IntArrayTopic { Id = 2, Values = Array.Empty<int>() };
        field.Setter(payload, new int[] { 100, 200 });

        var result = Assert.IsType<int[]>(field.Getter(payload));
        Assert.Equal(2, result.Length);
        Assert.Equal(100, result[0]);
        Assert.Equal(200, result[1]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. TopicMetadata.ReadMarshalElement / WriteMarshalElement correctness
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ReadMarshalElement_AllSupportedTypes()
    {
        // byte
        unsafe
        {
            byte value = 0xBE;
            Assert.Equal((byte)0xBE, TopicMetadata.ReadMarshalElement((IntPtr)(&value), typeof(byte)));
        }

        // int
        unsafe
        {
            int value = 12345678;
            Assert.Equal(12345678, TopicMetadata.ReadMarshalElement((IntPtr)(&value), typeof(int)));
        }

        // float
        unsafe
        {
            float value = 3.14f;
            var result = Assert.IsType<float>(TopicMetadata.ReadMarshalElement((IntPtr)(&value), typeof(float)));
            Assert.InRange(result, 3.13f, 3.15f);
        }

        // bool – true
        unsafe
        {
            byte value = 1;
            Assert.Equal(true, TopicMetadata.ReadMarshalElement((IntPtr)(&value), typeof(bool)));
        }

        // bool – false
        unsafe
        {
            byte value = 0;
            Assert.Equal(false, TopicMetadata.ReadMarshalElement((IntPtr)(&value), typeof(bool)));
        }
    }

    [Fact]
    public void WriteMarshalElement_AndReadBack_Roundtrips()
    {
        // int
        unsafe
        {
            int buf = 0;
            TopicMetadata.WriteMarshalElement((IntPtr)(&buf), 999, typeof(int));
            Assert.Equal(999, buf);
        }

        // byte
        unsafe
        {
            byte buf = 0;
            TopicMetadata.WriteMarshalElement((IntPtr)(&buf), (byte)200, typeof(byte));
            Assert.Equal((byte)200, buf);
        }

        // double
        unsafe
        {
            double buf = 0;
            TopicMetadata.WriteMarshalElement((IntPtr)(&buf), 2.718, typeof(double));
            Assert.InRange(buf, 2.717, 2.719);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9. AllFields does not duplicate array entries
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TopicMetadata_MixedArrayTopic_AllFieldsCount()
    {
        var meta = new TopicMetadata(typeof(MixedArrayTopic));

        // Expected non-synthetic fields: Id (int), DynamicValues (int[]), FixedFloats (float[])
        var nonSynthetic = meta.AllFields.Where(f => !f.IsSynthetic).ToList();
        Assert.Equal(3, nonSynthetic.Count);

        Assert.Contains(nonSynthetic, f => f.StructuredName == "Id" && !f.IsArrayField && !f.IsFixedSizeArray);
        Assert.Contains(nonSynthetic, f => f.StructuredName == "DynamicValues" && f.IsArrayField);
        Assert.Contains(nonSynthetic, f => f.StructuredName == "FixedFloats" && f.IsFixedSizeArray && f.FixedArrayLength == 3);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10. Zero-initialised fixed buffer getter returns correct defaults
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FixedBuffer_ZeroInitialised_GetterReturnsAllZeros()
    {
        var meta = new TopicMetadata(typeof(FixedByteBufferTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Payload");

        object payload = new FixedByteBufferTopic(); // default-initialised

        var result = Assert.IsType<byte[]>(field.Getter(payload));
        Assert.Equal(8, result.Length);
        Assert.All(result, b => Assert.Equal(0, b));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11. Setter with a shorter array only writes available elements
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FixedBuffer_SetterWithShortArray_WritesOnlyProvidedElements()
    {
        var meta = new TopicMetadata(typeof(FixedByteBufferTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Payload");

        object payload = new FixedByteBufferTopic();

        // Pre-fill with 0xFF via setter.
        field.Setter(payload, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });

        // Overwrite only the first 3 elements.
        field.Setter(payload, new byte[] { 0x01, 0x02, 0x03 });

        var result = Assert.IsType<byte[]>(field.Getter(payload));
        Assert.Equal(0x01, result[0]);
        Assert.Equal(0x02, result[1]);
        Assert.Equal(0x03, result[2]);
        // Remaining elements must be untouched (0xFF).
        Assert.Equal(0xFF, result[3]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 12. FixedSizeArray is not tagged as IsArrayField (no add/remove allowed)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FixedBuffer_IsNotTaggedAsIsArrayField()
    {
        var meta = new TopicMetadata(typeof(FixedByteBufferTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Payload");

        Assert.True(field.IsFixedSizeArray);
        Assert.False(field.IsArrayField, "Fixed buffers cannot change length; IsArrayField must be false.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 13. Dynamic array field – null array does not throw in getter
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DynamicArray_NullValue_GetterReturnsNull()
    {
        var meta = new TopicMetadata(typeof(IntArrayTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Values");

        // Values is a reference-type field; default struct has null array.
        object payload = new IntArrayTopic { Id = 1 }; // Values == null

        var result = field.Getter(payload); // should not throw
        Assert.Null(result);
    }
}
