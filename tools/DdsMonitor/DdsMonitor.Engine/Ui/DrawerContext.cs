using System;

namespace DdsMonitor.Engine.Ui;

/// <summary>
/// Context passed to a type-drawer factory, carrying the current value and
/// change notification callback needed for two-way data binding.
/// </summary>
public sealed class DrawerContext
{
    /// <summary>
    /// Initializes a new instance of <see cref="DrawerContext"/>.
    /// </summary>
    /// <param name="label">Display label for the field.</param>
    /// <param name="fieldType">CLR type of the field value.</param>
    /// <param name="valueGetter">Returns the current field value from the model instance.</param>
    /// <param name="onChange">Called with the new parsed value when the user edits the input.</param>
    /// <param name="onValidationError">
    /// Optional callback invoked when validation state changes.
    /// Pass <c>null</c> to clear a previous error; pass a non-null string to set an error message.
    /// </param>
    public DrawerContext(
        string label,
        Type fieldType,
        Func<object?> valueGetter,
        Action<object?> onChange,
        Action<string?>? onValidationError = null)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        FieldType = fieldType ?? throw new ArgumentNullException(nameof(fieldType));
        ValueGetter = valueGetter ?? throw new ArgumentNullException(nameof(valueGetter));
        OnChange = onChange ?? throw new ArgumentNullException(nameof(onChange));
        OnValidationError = onValidationError;
    }

    /// <summary>Gets the display label for the field.</summary>
    public string Label { get; }

    /// <summary>Gets the CLR type of the field value.</summary>
    public Type FieldType { get; }

    /// <summary>Gets the current field value from the underlying model.</summary>
    public Func<object?> ValueGetter { get; }

    /// <summary>Called with the converted new value each time the user changes the input.</summary>
    public Action<object?> OnChange { get; }

    /// <summary>
    /// Optional callback invoked when the validation state of this field changes.
    /// Called with <c>null</c> to clear a previous error, or a non-null error message string.
    /// May be <c>null</c> when no validation reporting is needed (e.g. in tests).
    /// </summary>
    public Action<string?>? OnValidationError { get; }
}
