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
    public FieldMetadata(
        string structuredName,
        string displayName,
        Type valueType,
        Func<object, object?> getter,
        Action<object, object?> setter,
        bool isSynthetic)
    {
        StructuredName = structuredName ?? throw new ArgumentNullException(nameof(structuredName));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        Getter = getter ?? throw new ArgumentNullException(nameof(getter));
        Setter = setter ?? throw new ArgumentNullException(nameof(setter));
        IsSynthetic = isSynthetic;
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
}
