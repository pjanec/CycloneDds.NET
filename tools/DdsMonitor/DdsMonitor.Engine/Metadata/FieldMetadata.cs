using System;

namespace DdsMonitor.Engine;

/// <summary>
/// Describes a flattened topic field and its compiled accessors.
/// </summary>
public sealed class FieldMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldMetadata"/> class.
    /// </summary>
    /// <param name="structuredName">Dot-separated structured field name.</param>
    /// <param name="displayName">Display name shown in the UI.</param>
    /// <param name="valueType">CLR type of the field value.</param>
    /// <param name="getter">Compiled getter delegate.</param>
    /// <param name="setter">Compiled setter delegate.</param>
    /// <param name="isSynthetic">True for computed/synthetic fields.</param>
    /// <param name="isWrapperField">True for top-level SampleData wrapper properties.</param>
    /// <param name="isArrayField">
    /// True when the field holds a dynamic array or list whose length can change at runtime
    /// (e.g. <c>T[]</c>, <c>List&lt;T&gt;</c> representing DDS sequences).
    /// The exposed value type is the concrete collection type; <see cref="ElementType"/> holds the element type.
    /// </param>
    /// <param name="isFixedSizeArray">
    /// True when the field represents a C# fixed-size buffer (<c>public unsafe fixed T Name[N]</c>)
    /// or any other fixed-length inline array.  The value type is <c>T[]</c> (elements are copied on
    /// each access); add/remove operations are not permitted.
    /// </param>
    /// <param name="elementType">Element type for array fields; <c>null</c> for scalar fields.</param>
    /// <param name="fixedArrayLength">
    /// Number of elements for fixed-size array fields; <c>-1</c> for scalar or dynamic fields.
    /// </param>
    /// <param name="dependentDiscriminatorPath">
    /// For a union arm field, the dot-separated path of the discriminator field that governs
    /// whether this arm is active.  <c>null</c> for non-union fields.
    /// </param>
    /// <param name="activeWhenDiscriminatorValue">
    /// The discriminator value (from <c>[DdsCase(value)]</c>) for which this arm is active.
    /// <c>null</c> for non-arm fields and for the <c>[DdsDefaultCase]</c> arm.
    /// </param>
    /// <param name="isDefaultUnionCase">
    /// <c>true</c> when this arm is decorated with <c>[DdsDefaultCase]</c> and is shown
    /// when no explicit case matches the current discriminator value.
    /// </param>
    /// <param name="isDiscriminatorField">
    /// <c>true</c> when this field carries the <c>[DdsDiscriminator]</c> attribute.
    /// </param>
    /// <param name="isOptional">
    /// <c>true</c> when this field is explicitly annotated with <c>[DdsOptional]</c>
    /// or declared as a <c>Nullable&lt;T&gt;</c> type.  Only optional fields show a
    /// null/include checkbox in the Send Sample panel.
    /// </param>
    public FieldMetadata(
        string structuredName,
        string displayName,
        Type valueType,
        Func<object, object?> getter,
        Action<object, object?> setter,
        bool isSynthetic,
        bool isWrapperField = false,
        bool isArrayField = false,
        bool isFixedSizeArray = false,
        Type? elementType = null,
        int fixedArrayLength = -1,
        string? dependentDiscriminatorPath = null,
        object? activeWhenDiscriminatorValue = null,
        bool isDefaultUnionCase = false,
        bool isDiscriminatorField = false,
        bool isOptional = false)
    {
        StructuredName = structuredName ?? throw new ArgumentNullException(nameof(structuredName));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        Getter = getter ?? throw new ArgumentNullException(nameof(getter));
        Setter = setter ?? throw new ArgumentNullException(nameof(setter));
        IsSynthetic = isSynthetic;
        IsWrapperField = isWrapperField;
        IsArrayField = isArrayField;
        IsFixedSizeArray = isFixedSizeArray;
        ElementType = elementType;
        FixedArrayLength = fixedArrayLength;
        DependentDiscriminatorPath = dependentDiscriminatorPath;
        ActiveWhenDiscriminatorValue = activeWhenDiscriminatorValue;
        IsDefaultUnionCase = isDefaultUnionCase;
        IsDiscriminatorField = isDiscriminatorField;
        IsOptional = isOptional;
    }

    /// <summary>
    /// Gets the dot-separated structured field name (e.g. Position.X).
    /// </summary>
    public string StructuredName { get; }

    /// <summary>
    /// Gets the display name for the field.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the field value type.
    /// </summary>
    public Type ValueType { get; }

    /// <summary>
    /// Gets the compiled getter delegate.
    /// </summary>
    public Func<object, object?> Getter { get; }

    /// <summary>
    /// Gets the compiled setter delegate.
    /// </summary>
    public Action<object, object?> Setter { get; }

    /// <summary>
    /// Gets a value indicating whether this field is synthetic (computed).
    /// </summary>
    public bool IsSynthetic { get; }

    /// <summary>
    /// Gets a value indicating whether this synthetic field is a top-level <see cref="SampleData"/>
    /// wrapper property (e.g. Timestamp, Ordinal) rather than a payload-derived display metric.
    /// Wrapper fields are exposed in the filter builder UI in addition to normal payload fields.
    /// </summary>
    public bool IsWrapperField { get; }

    /// <summary>
    /// Gets a value indicating whether this field holds a dynamic array or list
    /// (e.g. <c>T[]</c>, <c>List&lt;T&gt;</c>) whose element count can change at runtime.
    /// When <c>true</c>, <see cref="ElementType"/> is non-null and add/remove element
    /// operations are permitted in the send panel.
    /// </summary>
    public bool IsArrayField { get; }

    /// <summary>
    /// Gets a value indicating whether this field represents a C# fixed-size buffer
    /// (<c>public unsafe fixed T Name[N]</c>).  The getter returns a <c>T[]</c> snapshot;
    /// the setter writes a <c>T[]</c> back into the buffer.
    /// <see cref="FixedArrayLength"/> gives the buffer length.
    /// </summary>
    public bool IsFixedSizeArray { get; }

    /// <summary>
    /// Gets the element type for array fields, or <c>null</c> for scalar fields.
    /// </summary>
    public Type? ElementType { get; }

    /// <summary>
    /// Gets the fixed number of elements for a <see cref="IsFixedSizeArray"/> field,
    /// or <c>-1</c> for scalar and dynamic-array fields.
    /// </summary>
    public int FixedArrayLength { get; }

    // ── Union-specific metadata (ME1-T08) ─────────────────────────────────────

    /// <summary>
    /// Gets the dot-separated path of the discriminator field that controls whether
    /// this union arm is shown.  <c>null</c> for non-union fields.
    /// </summary>
    public string? DependentDiscriminatorPath { get; }

    /// <summary>
    /// Gets the discriminator value (from <c>[DdsCase(value)]</c>) that activates this arm.
    /// <c>null</c> for non-arm fields and for the <c>[DdsDefaultCase]</c> arm.
    /// </summary>
    public object? ActiveWhenDiscriminatorValue { get; }

    /// <summary>
    /// Gets a value indicating whether this arm is the default union case
    /// (<c>[DdsDefaultCase]</c>), shown when no explicit case matches.
    /// </summary>
    public bool IsDefaultUnionCase { get; }

    /// <summary>
    /// Gets a value indicating whether this field is the union discriminator
    /// (decorated with <c>[DdsDiscriminator]</c>).
    /// </summary>
    public bool IsDiscriminatorField { get; }

    /// <summary>
    /// Gets a value indicating whether this field is explicitly marked as optional
    /// (annotated with <c>[DdsOptional]</c> or declared as <c>Nullable&lt;T&gt;</c>).
    /// Only optional fields display a null/include checkbox in the Send Sample panel.
    /// </summary>
    public bool IsOptional { get; }
}
