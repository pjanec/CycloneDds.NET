using System;
using DdsMonitor.Engine.Ui;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for BATCH-23 features:
/// DMON-036 – ITypeDrawerRegistry / TypeDrawerRegistry
/// DMON-035 – DynamicForm payload field mapping and two-way binding
/// DMON-034 – SendSamplePanel payload instantiation and mutation logic
/// </summary>
public sealed class Batch23Tests
{
    // ──────────────────────────────────────────────────────────────────────────
    // DMON-036: TypeDrawerRegistry – primitive type registration
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    [InlineData(typeof(short))]
    [InlineData(typeof(byte))]
    [InlineData(typeof(uint))]
    [InlineData(typeof(ulong))]
    [InlineData(typeof(ushort))]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(char))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(Guid))]
    public void TypeDrawerRegistry_HasDrawer_ReturnsTrue_ForAllBuiltInPrimitives(Type type)
    {
        var registry = new TypeDrawerRegistry();

        Assert.True(registry.HasDrawer(type));
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(double))]
    public void TypeDrawerRegistry_GetDrawer_ReturnsNonNull_ForBuiltInPrimitives(Type type)
    {
        var registry = new TypeDrawerRegistry();

        var drawer = registry.GetDrawer(type);

        Assert.NotNull(drawer);
    }

    [Fact]
    public void TypeDrawerRegistry_GetDrawer_ReturnsNull_ForUnknownComplexType()
    {
        var registry = new TypeDrawerRegistry();

        var drawer = registry.GetDrawer(typeof(object));

        Assert.Null(drawer);
    }

    [Fact]
    public void TypeDrawerRegistry_HasDrawer_ReturnsFalse_ForUnknownComplexType()
    {
        var registry = new TypeDrawerRegistry();

        Assert.False(registry.HasDrawer(typeof(object)));
    }

    [Fact]
    public void TypeDrawerRegistry_GetDrawer_ReturnsDrawer_ForEnumType()
    {
        var registry = new TypeDrawerRegistry();

        var drawer = registry.GetDrawer(typeof(SampleStatus));

        Assert.NotNull(drawer);
    }

    [Fact]
    public void TypeDrawerRegistry_HasDrawer_ReturnsTrue_ForEnumType()
    {
        var registry = new TypeDrawerRegistry();

        Assert.True(registry.HasDrawer(typeof(SampleStatus)));
    }

    [Fact]
    public void TypeDrawerRegistry_GetDrawer_ReturnsDrawer_ForNullableInt()
    {
        var registry = new TypeDrawerRegistry();

        var drawer = registry.GetDrawer(typeof(int?));

        // Nullable<int> should forward to the int drawer.
        Assert.NotNull(drawer);
    }

    [Fact]
    public void TypeDrawerRegistry_HasDrawer_ReturnsTrue_ForNullableFloat()
    {
        var registry = new TypeDrawerRegistry();

        Assert.True(registry.HasDrawer(typeof(float?)));
    }

    [Fact]
    public void TypeDrawerRegistry_Register_OverridesDefaultDrawer()
    {
        var registry = new TypeDrawerRegistry();
        var customDrawer = (RenderFragment<DrawerContext>)(ctx => builder => { /* custom */ });

        registry.Register(typeof(int), customDrawer);

        Assert.Same(customDrawer, registry.GetDrawer(typeof(int)));
    }

    [Fact]
    public void TypeDrawerRegistry_EnumDrawer_IsCachedAfterFirstResolution()
    {
        var registry = new TypeDrawerRegistry();

        var first = registry.GetDrawer(typeof(SampleStatus));
        var second = registry.GetDrawer(typeof(SampleStatus));

        // Same instance: registry caches enum drawers.
        Assert.Same(first, second);
    }

    [Fact]
    public void TypeDrawerRegistry_Register_Throws_OnNullType()
    {
        var registry = new TypeDrawerRegistry();

        Assert.Throws<ArgumentNullException>(
            () => registry.Register(null!, ctx => builder => { }));
    }

    [Fact]
    public void TypeDrawerRegistry_GetDrawer_Throws_OnNullType()
    {
        var registry = new TypeDrawerRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.GetDrawer(null!));
    }

    [Fact]
    public void TypeDrawerRegistry_HasDrawer_Throws_OnNullType()
    {
        var registry = new TypeDrawerRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.HasDrawer(null!));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DrawerContext – construction and property access
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DrawerContext_Constructor_Throws_WhenLabelIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DrawerContext(null!, typeof(int), () => 0, _ => { }));
    }

    [Fact]
    public void DrawerContext_Constructor_Throws_WhenFieldTypeIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DrawerContext("Label", null!, () => 0, _ => { }));
    }

    [Fact]
    public void DrawerContext_Constructor_Throws_WhenValueGetterIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DrawerContext("Label", typeof(int), null!, _ => { }));
    }

    [Fact]
    public void DrawerContext_Constructor_Throws_WhenOnChangeIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DrawerContext("Label", typeof(int), () => 0, null!));
    }

    [Fact]
    public void DrawerContext_ValueGetter_ReturnsCurrentValue()
    {
        var value = 42;
        var ctx = new DrawerContext("F", typeof(int), () => value, _ => { });

        Assert.Equal(42, ctx.ValueGetter());
    }

    [Fact]
    public void DrawerContext_OnChange_MutatesState()
    {
        var captured = (object?)null;
        var ctx = new DrawerContext("F", typeof(int), () => 0, v => captured = v);

        ctx.OnChange(99);

        Assert.Equal(99, captured);
    }

    [Fact]
    public void DrawerContext_Receiver_IsNullByDefault()
    {
        var ctx = new DrawerContext("F", typeof(int), () => 0, _ => { });

        Assert.Null(ctx.Receiver);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DMON-034: Payload instantiation via Activator.CreateInstance
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Activator_CreateInstance_ProducesBoxedStruct_ForTopicType()
    {
        var meta = new TopicMetadata(typeof(SampleTopic));

        var payload = Activator.CreateInstance(meta.TopicType);

        Assert.NotNull(payload);
        Assert.IsType<SampleTopic>(payload);
    }

    [Fact]
    public void Activator_CreateInstance_ProducesDefaultValues_ForSimpleStruct()
    {
        var meta = new TopicMetadata(typeof(SampleTopic));

        var payload = Activator.CreateInstance(meta.TopicType)!;
        var sampleTopic = (SampleTopic)payload;

        Assert.Equal(0, sampleTopic.Id);
    }

    [Fact]
    public void Activator_CreateInstance_WorksForNestedStructType()
    {
        var meta = new TopicMetadata(typeof(OuterType));

        var payload = Activator.CreateInstance(meta.TopicType);

        Assert.NotNull(payload);
        Assert.IsType<OuterType>(payload);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DMON-035: FieldMetadata two-way binding on boxed struct
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FieldMetadata_Setter_MutatesBoxedStruct_ForTopLevelField()
    {
        var meta = new TopicMetadata(typeof(SampleTopic));
        var payload = Activator.CreateInstance(meta.TopicType)!;

        var idField = meta.AllFields.First(f => f.StructuredName == "Id");
        idField.Setter(payload, 99);

        Assert.Equal(99, idField.Getter(payload));
    }

    [Fact]
    public void FieldMetadata_Setter_MutatesNestedField_InBoxedStruct()
    {
        var meta = new TopicMetadata(typeof(OuterType));
        var payload = Activator.CreateInstance(meta.TopicType)!;

        var xField = meta.AllFields.First(f => f.StructuredName == "Position.X");
        xField.Setter(payload, 3.14);

        Assert.Equal(3.14, xField.Getter(payload));
    }

    [Fact]
    public void FieldMetadata_Setter_MutatesMultipleNestedFields_Independently()
    {
        var meta = new TopicMetadata(typeof(OuterType));
        var payload = Activator.CreateInstance(meta.TopicType)!;

        var xField = meta.AllFields.First(f => f.StructuredName == "Position.X");
        var yField = meta.AllFields.First(f => f.StructuredName == "Position.Y");
        var idField = meta.AllFields.First(f => f.StructuredName == "Id");

        xField.Setter(payload, 1.1);
        yField.Setter(payload, 2.2);
        idField.Setter(payload, 42);

        Assert.Equal(1.1, xField.Getter(payload));
        Assert.Equal(2.2, yField.Getter(payload));
        Assert.Equal(42, idField.Getter(payload));
    }

    [Fact]
    public void FieldMetadata_Setter_MutatesEnumField()
    {
        var meta = new TopicMetadata(typeof(StatusTopic));
        var payload = Activator.CreateInstance(meta.TopicType)!;

        var statusField = meta.AllFields.First(f => f.StructuredName == "Status");
        statusField.Setter(payload, SampleStatus.Active);

        Assert.Equal(SampleStatus.Active, statusField.Getter(payload));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DMON-035: DynamicForm – field filtering (only non-synthetic fields editable)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TopicMetadata_AllFields_ContainsSyntheticAndNonSyntheticFields()
    {
        var meta = new TopicMetadata(typeof(SampleTopic));

        var synthetic = meta.AllFields.Where(f => f.IsSynthetic).ToList();
        var editableFields = meta.AllFields.Where(f => !f.IsSynthetic).ToList();

        Assert.NotEmpty(synthetic);
        Assert.NotEmpty(editableFields);
    }

    [Fact]
    public void DynamicForm_EditableFields_ExcludesSyntheticFields()
    {
        var meta = new TopicMetadata(typeof(SampleTopic));

        // Simulate what DynamicForm's EditableFields property does.
        var editableFields = meta.AllFields.Where(f => !f.IsSynthetic).ToList();

        Assert.All(editableFields, f => Assert.False(f.IsSynthetic));
    }

    [Fact]
    public void DynamicForm_EditableFields_ContainsAllPayloadFields()
    {
        var meta = new TopicMetadata(typeof(OuterType));

        var editableFields = meta.AllFields.Where(f => !f.IsSynthetic).ToList();
        var names = editableFields.Select(f => f.StructuredName).ToList();

        // OuterType flattens to: Id, Position.X, Position.Y
        Assert.Contains("Id", names);
        Assert.Contains("Position.X", names);
        Assert.Contains("Position.Y", names);
    }

    [Fact]
    public void DynamicForm_EditableFields_DoesNotContainTimestampOrOrdinal()
    {
        var meta = new TopicMetadata(typeof(SampleTopic));

        var editableFields = meta.AllFields.Where(f => !f.IsSynthetic).ToList();
        var names = editableFields.Select(f => f.StructuredName).ToList();

        Assert.DoesNotContain("Timestamp", names);
        Assert.DoesNotContain("Ordinal", names);
    }

    [Fact]
    public void DynamicForm_GroupName_IsNullForTopLevelField()
    {
        // GetGroupName("Id") → null
        var groupName = GetGroupName("Id");

        Assert.Null(groupName);
    }

    [Fact]
    public void DynamicForm_GroupName_IsParentSegmentForNestedField()
    {
        // GetGroupName("Position.X") → "Position"
        var groupName = GetGroupName("Position.X");

        Assert.Equal("Position", groupName);
    }

    [Fact]
    public void DynamicForm_GroupName_UsesLastSegmentParent_ForDeeplyNested()
    {
        // GetGroupName("A.B.C") → "A.B"
        var groupName = GetGroupName("A.B.C");

        Assert.Equal("A.B", groupName);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static FieldMetadata? First(IEnumerable<FieldMetadata> fields, string name)
        => fields.FirstOrDefault(f => f.StructuredName == name);

    /// <summary>
    /// Mirrors the GetGroupName helper in DynamicForm.razor.
    /// </summary>
    private static string? GetGroupName(string structuredName)
    {
        var dot = structuredName.LastIndexOf('.');
        return dot < 0 ? null : structuredName[..dot];
    }
}
