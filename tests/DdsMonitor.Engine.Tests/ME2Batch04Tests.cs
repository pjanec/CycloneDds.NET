using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CycloneDDS.Schema;
using DdsMonitor.Engine;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for ME2-BATCH-04:
///   ME2-T22-A — IsUnionArmVisible O(N^2) elimination via precomputed Dictionary cache.
///   ME2-T22-B — GetUnionInfo reflection cycle fix via ConcurrentDictionary.
///   ME2-T21   — IsOptional flag on FieldMetadata from [DdsOptional] or Nullable&lt;T&gt;.
///   Task 4    — GetComplexFields for nested DynamicForm rendering.
/// </summary>
public sealed class ME2Batch04Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T21: IsOptional flag on FieldMetadata
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FieldMetadata_IsOptional_FalseByDefault_ForPlainString()
    {
        // SelfTestSimple.Message is a plain string (no [DdsOptional]) – must NOT be optional
        var meta = new TopicMetadata(typeof(SelfTestSimple));
        var messageField = meta.AllFields.SingleOrDefault(f => f.StructuredName == "Message");

        Assert.NotNull(messageField);
        Assert.False(messageField!.IsOptional,
            "Plain string reference type without [DdsOptional] must not be marked optional.");
    }

    [Fact]
    public void FieldMetadata_IsOptional_FalseByDefault_ForPrimitiveField()
    {
        // SelfTestSimple.Id is int – must NOT be optional
        var meta = new TopicMetadata(typeof(SelfTestSimple));
        var idField = meta.AllFields.SingleOrDefault(f => f.StructuredName == "Id");

        Assert.NotNull(idField);
        Assert.False(idField!.IsOptional);
    }

    [Fact]
    public void FieldMetadata_IsOptional_True_ForDdsOptionalAnnotatedField()
    {
        // OptionalFieldTopic.OptionalInt is [DdsOptional] int? – must be optional
        var meta = new TopicMetadata(typeof(OptionalFieldTopic));
        var field = meta.AllFields.SingleOrDefault(f => f.StructuredName == "OptionalInt");

        Assert.NotNull(field);
        Assert.True(field!.IsOptional,
            "[DdsOptional] int? field must be marked optional.");
    }

    [Fact]
    public void FieldMetadata_IsOptional_True_ForNullableValueType()
    {
        // OptionalFieldTopic.NullableDouble is double? (Nullable<T>) without [DdsOptional]
        var meta = new TopicMetadata(typeof(OptionalFieldTopic));
        var field = meta.AllFields.SingleOrDefault(f => f.StructuredName == "NullableDouble");

        Assert.NotNull(field);
        Assert.True(field!.IsOptional,
            "Nullable<T> field must be marked optional even without [DdsOptional].");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T22-A: Precomputed discriminator cache for IsUnionArmVisible
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The TopicMetadata for SelfTestPose must include discriminator fields
    /// so the precomputed cache in DynamicForm can do O(1) lookups.
    /// </summary>
    [Fact]
    public void TopicMetadata_UnionType_DiscriminatorField_HasCorrectStructuredName()
    {
        var meta = new TopicMetadata(typeof(SelfTestPose));

        var discriminator = meta.AllFields.SingleOrDefault(
            f => f.IsDiscriminatorField && f.StructuredName == "UnionValue.level");

        Assert.NotNull(discriminator);
    }

    /// <summary>
    /// All union arm fields must have DependentDiscriminatorPath matching
    /// the discriminator's structured name — the data that populates the
    /// O(1) discriminator lookup dict in DynamicForm.
    /// </summary>
    [Fact]
    public void TopicMetadata_UnionArmFields_HaveConsistentDiscriminatorPath()
    {
        var meta = new TopicMetadata(typeof(SelfTestPose));

        var armFields = meta.AllFields
            .Where(f =>
                !f.IsDiscriminatorField &&
                f.DependentDiscriminatorPath != null)
            .ToList();

        Assert.NotEmpty(armFields);
        // All arms in the same union must point to the same discriminator path.
        var distinctPaths = armFields.Select(f => f.DependentDiscriminatorPath).Distinct().ToList();
        Assert.Single(distinctPaths);
        Assert.Equal("UnionValue.level", distinctPaths[0]);
    }

    /// <summary>
    /// The precomputed cache should correctly handle default union arms.
    /// Default arm must have IsDefaultUnionCase = true and no ActiveWhenDiscriminatorValue.
    /// </summary>
    [Fact]
    public void TopicMetadata_DefaultUnionArm_IsDefaultUnionCase_And_NoExplicitCaseValue()
    {
        var meta = new TopicMetadata(typeof(SelfTestPose));

        var defaultArm = meta.AllFields.SingleOrDefault(
            f => f.IsDefaultUnionCase && f.StructuredName == "UnionValue.DefaultMessage");

        Assert.NotNull(defaultArm);
        Assert.True(defaultArm!.IsDefaultUnionCase);
        Assert.Null(defaultArm.ActiveWhenDiscriminatorValue);
        Assert.NotNull(defaultArm.DependentDiscriminatorPath);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Task 4: GetComplexFields for nested DynamicForm rendering
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetComplexFields_ReturnsFieldsForNestedStruct()
    {
        // Pose has Position (Vector3) and Velocity (Vector3), each with X, Y, Z.
        var fields = TopicMetadata.GetComplexFields(typeof(Pose));

        Assert.NotEmpty(fields);
        var names = fields.Select(f => f.StructuredName).ToList();
        Assert.Contains("Position.X", names);
        Assert.Contains("Position.Y", names);
        Assert.Contains("Position.Z", names);
        Assert.Contains("Velocity.X", names);
    }

    [Fact]
    public void GetComplexFields_ReturnsCachedResult_SameReference()
    {
        // Calling twice for the same type must return exactly the same list instance (cache hit).
        var result1 = TopicMetadata.GetComplexFields(typeof(Vector3));
        var result2 = TopicMetadata.GetComplexFields(typeof(Vector3));

        Assert.Same(result1, result2);
    }

    [Fact]
    public void GetComplexFields_ForUnionType_ReturnsDiscriminatorAndArms()
    {
        var fields = TopicMetadata.GetComplexFields(typeof(TestingUnion));

        Assert.NotEmpty(fields);
        var discriminator = fields.SingleOrDefault(f => f.IsDiscriminatorField);
        Assert.NotNull(discriminator);

        var arms = fields.Where(f => f.DependentDiscriminatorPath != null).ToList();
        Assert.NotEmpty(arms);
    }

    [Fact]
    public void GetComplexFields_ForVector3_ReturnsThreeScalarFields()
    {
        var fields = TopicMetadata.GetComplexFields(typeof(Vector3));

        Assert.Equal(3, fields.Count);
        var names = fields.Select(f => f.StructuredName).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "X", "Y", "Z" }, names);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T21: FieldMetadata constructor — IsOptional is passable
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FieldMetadata_Constructor_IsOptionalDefaultIsFalse()
    {
        var field = new FieldMetadata(
            structuredName: "Test",
            displayName: "Test",
            valueType: typeof(string),
            getter: _ => null,
            setter: (_, __) => { },
            isSynthetic: false);

        Assert.False(field.IsOptional);
    }

    [Fact]
    public void FieldMetadata_Constructor_IsOptionalCanBeSetToTrue()
    {
        var field = new FieldMetadata(
            structuredName: "Test",
            displayName: "Test",
            valueType: typeof(int?),
            getter: _ => null,
            setter: (_, __) => { },
            isSynthetic: false,
            isOptional: true);

        Assert.True(field.IsOptional);
    }
}
