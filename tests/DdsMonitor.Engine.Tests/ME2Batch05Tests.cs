using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CycloneDDS.Schema;
using DdsMonitor.Engine;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for ME2-BATCH-05:
///   ME2-T23 — Union struct arm expansion: GetComplexFields must return non-empty
///             fields for complex struct union arms so DynamicForm can render an
///             expandable nested form instead of falling back to ToString().
///   ME2-T24 — AddArrayElement InvalidCastException: the "add element" path must build
///             a properly-typed List&lt;T&gt; when the setter expects a generic list,
///             avoiding the System.Single[] → List&lt;Single&gt; invalid-cast crash.
/// </summary>
public sealed class ME2Batch05Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T23: Union struct arm must have expandable complex fields
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetComplexFields_ForStructArmPayload_ReturnsThreeScalarFields()
    {
        var fields = TopicMetadata.GetComplexFields(typeof(StructArmPayload));

        Assert.Equal(3, fields.Count);
        var names = fields.Select(f => f.StructuredName).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "X", "Y", "Z" }, names);
    }

    [Fact]
    public void GetComplexFields_ForStructArmUnion_StructArmField_HasDependentDiscriminatorPath()
    {
        var fields = TopicMetadata.GetComplexFields(typeof(StructArmUnion));

        var structArm = fields.SingleOrDefault(f => f.StructuredName == "StructArm");
        Assert.NotNull(structArm);

        // Must be a union arm tied to the discriminator.
        Assert.NotNull(structArm!.DependentDiscriminatorPath);

        // The arm's ValueType is the complex struct.
        Assert.Equal(typeof(StructArmPayload), structArm.ValueType);
    }

    [Fact]
    public void GetComplexFields_ForStructArmUnion_StructArmType_HasExpandableChildren()
    {
        // This validating the key condition the ME2-T23 template fix relies on:
        // TopicMetadata.GetComplexFields(structArm.ValueType).Count > 0 → renders sub-form.
        var unionFields = TopicMetadata.GetComplexFields(typeof(StructArmUnion));
        var structArm = unionFields.Single(f => f.StructuredName == "StructArm");

        var innerFields = TopicMetadata.GetComplexFields(structArm.ValueType);

        Assert.NotEmpty(innerFields);
        Assert.True(innerFields.Count >= 1,
            "GetComplexFields on the struct arm type must return fields so DynamicForm " +
            "can render an expandable sub-form instead of falling back to ToString().");
    }

    [Fact]
    public void GetComplexFields_ForStructArmUnion_ScalarArmField_HasNoExpandableChildren()
    {
        // Scalar arm (int Kind) should have an empty GetComplexFields result —
        // those remain in the unsupported/drawer path.
        var unionFields = TopicMetadata.GetComplexFields(typeof(StructArmUnion));
        var scalarArm = unionFields.SingleOrDefault(f => f.StructuredName == "ScalarArm");

        Assert.NotNull(scalarArm);

        // int is a leaf type — no complex children.
        var innerFields = TopicMetadata.GetComplexFields(scalarArm!.ValueType);
        Assert.Empty(innerFields);
    }

    [Fact]
    public void GetComplexFields_ForStructArmUnion_ContainsDiscriminator()
    {
        var fields = TopicMetadata.GetComplexFields(typeof(StructArmUnion));

        var discriminator = fields.SingleOrDefault(f => f.IsDiscriminatorField);
        Assert.NotNull(discriminator);
        Assert.Equal("Kind", discriminator!.StructuredName);
    }

    [Fact]
    public void StructArmUnion_ActiveStructArm_CanBeReadViaGetter()
    {
        // Prove that the struct arm value can be retrieved — prerequisite for the
        // DynamicForm expansion to work at runtime.
        var fields = TopicMetadata.GetComplexFields(typeof(StructArmUnion));
        var structArm = fields.Single(f => f.StructuredName == "StructArm");

        var union = new StructArmUnion { Kind = 1, StructArm = new StructArmPayload { X = 1f, Y = 2f, Z = 3f } };
        var obj = (object)union;

        var retrieved = structArm.Getter(obj);

        Assert.NotNull(retrieved);
        Assert.IsType<StructArmPayload>(retrieved);
        var payload = (StructArmPayload)retrieved!;
        Assert.Equal(1f, payload.X);
        Assert.Equal(2f, payload.Y);
        Assert.Equal(3f, payload.Z);
    }

    [Fact]
    public void StructArmUnion_ActiveStructArm_CanBeWrittenViaSetter()
    {
        // Prove that the struct arm setter works — the ME2-T23 OnPayloadMutated callback
        // calls capturedField.Setter(Payload!, mutatedValue) to write the updated struct back.
        var fields = TopicMetadata.GetComplexFields(typeof(StructArmUnion));
        var structArm = fields.Single(f => f.StructuredName == "StructArm");

        var union = new StructArmUnion { Kind = 1, StructArm = new StructArmPayload { X = 0f, Y = 0f, Z = 0f } };
        var obj = (object)union;

        var newPayload = new StructArmPayload { X = 9f, Y = 8f, Z = 7f };
        structArm.Setter(obj, newPayload);

        var retrieved = (StructArmPayload)structArm.Getter(obj)!;
        Assert.Equal(9f, retrieved.X);
        Assert.Equal(8f, retrieved.Y);
        Assert.Equal(7f, retrieved.Z);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T24: AddArrayElement must not cast T[] into List<T> setter
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FloatListSequenceTopic_ItemsField_HasListValueType()
    {
        // Precondition: the field's ValueType is List<float> (generic list),
        // which is the trigger condition for the ME2-T24 fix path.
        var meta = new TopicMetadata(typeof(FloatListSequenceTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Items");

        Assert.Equal(typeof(List<float>), field.ValueType);
        Assert.Equal(typeof(float), field.ElementType);
        Assert.True(field.IsArrayField);
    }

    [Fact]
    public void FloatListSequenceTopic_ItemsField_InitialValueIsNull()
    {
        // When a payload is default-constructed, List<T> fields are null.
        // This is the exact condition that triggered the pre-fix crash:
        // null → else branch → Array.CreateInstance → setter(float[]) → InvalidCastException.
        var meta = new TopicMetadata(typeof(FloatListSequenceTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Items");

        var payload = new FloatListSequenceTopic();

        Assert.Null(field.Getter(payload));
    }

    [Fact]
    public void FloatListSequenceTopic_Setter_Rejects_FloatArray_Throws()
    {
        // Regression guard: setting a float[] on a List<float> property must throw.
        // This is the exact cast failure that ME2-T24 prevents.
        var meta = new TopicMetadata(typeof(FloatListSequenceTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Items");

        var payload = new FloatListSequenceTopic();
        var arr = new float[] { 1f, 2f };

        Assert.Throws<InvalidCastException>(() => field.Setter(payload, arr));
    }

    [Fact]
    public void FloatListSequenceTopic_AddElement_ViaListPath_Succeeds()
    {
        // Simulate the ME2-T24 fix: when raw == null and field.ValueType is List<T>,
        // create a List<T>, copy existing items from an IEnumerable (none here),
        // append the default element, then call Setter — must not throw.
        var meta = new TopicMetadata(typeof(FloatListSequenceTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Items");

        var payload = new FloatListSequenceTopic();
        var raw = field.Getter(payload); // null

        // ME2-T24 fix path:
        var listType = typeof(List<>).MakeGenericType(field.ElementType!);
        var newList = (IList)Activator.CreateInstance(listType)!;
        if (raw is IEnumerable existing)
        {
            foreach (var item in existing)
                newList.Add(item);
        }
        newList.Add(0.0f); // default element

        // Must not throw InvalidCastException (was the pre-fix crash).
        field.Setter(payload, newList);

        var result = field.Getter(payload);
        Assert.IsType<List<float>>(result);
        var resultList = (List<float>)result!;
        Assert.Single(resultList);
        Assert.Equal(0.0f, resultList[0]);
    }

    [Fact]
    public void FloatListSequenceTopic_AddMultipleElements_ViaListPath_Accumulates()
    {
        // Simulate adding two elements sequentially — confirms the fix path
        // correctly seeds the new list from the existing list on each call.
        var meta = new TopicMetadata(typeof(FloatListSequenceTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "Items");

        var payload = new FloatListSequenceTopic();

        // First add: raw == null → build List<float> with [0.0].
        var list1 = new List<float>();
        list1.Add(10f);
        field.Setter(payload, list1);

        // Second add: raw == List<float>{ 10f } → IList not fixed → first branch hits.
        // This matches the mutableList path (raw is IList && !IsFixedSize).
        var raw2 = (IList)field.Getter(payload)!;
        Assert.False(raw2.IsFixedSize, "List<float> must not be fixed-size.");
        raw2.Add(20f);
        field.Setter(payload, raw2);

        var result = (List<float>)field.Getter(payload)!;
        Assert.Equal(2, result.Count);
        Assert.Equal(10f, result[0]);
        Assert.Equal(20f, result[1]);
    }

    [Fact]
    public void SelfTestPose_SamplesField_IsListOfFloat_AndNullInitially()
    {
        // SelfTestPose.Samples (used in the QA report) must behave like the fix expects:
        // ValueType = List<float>, ElementType = float, default value null.
        var meta = new TopicMetadata(typeof(SelfTestPose));
        var field = meta.AllFields.Single(f => f.StructuredName == "Samples");

        Assert.Equal(typeof(List<float>), field.ValueType);
        Assert.Equal(typeof(float), field.ElementType);
        Assert.True(field.IsArrayField);

        var payload = new SelfTestPose();
        Assert.Null(field.Getter(payload));
    }

    [Fact]
    public void SelfTestPose_SamplesField_Setter_Accepts_ListOfFloat()
    {
        // Verify that setting List<float> on SelfTestPose.Samples works correctly —
        // this is the expected result of the ME2-T24 fix path.
        var meta = new TopicMetadata(typeof(SelfTestPose));
        var field = meta.AllFields.Single(f => f.StructuredName == "Samples");

        var payload = new SelfTestPose();
        var list = new List<float> { 1.1f, 2.2f, 3.3f };

        field.Setter(payload, list); // must not throw

        var result = (List<float>)field.Getter(payload)!;
        Assert.Equal(3, result.Count);
        Assert.Equal(1.1f, result[0], precision: 3);
    }
}
