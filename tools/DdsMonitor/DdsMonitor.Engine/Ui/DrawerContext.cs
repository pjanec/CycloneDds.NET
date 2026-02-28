using System;
using Microsoft.AspNetCore.Components;

namespace DdsMonitor.Engine.Ui;

/// <summary>
/// Context passed to a type-drawer RenderFragment, carrying the current value and
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
    /// <param name="receiver">
    /// Optional Blazor event receiver used to schedule a state-change notification after
    /// an input event fires. Pass <c>this</c> from the hosting component so that
    /// Blazor automatically re-renders after every mutation.
    /// </param>
    public DrawerContext(
        string label,
        Type fieldType,
        Func<object?> valueGetter,
        Action<object?> onChange,
        IHandleEvent? receiver = null)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        FieldType = fieldType ?? throw new ArgumentNullException(nameof(fieldType));
        ValueGetter = valueGetter ?? throw new ArgumentNullException(nameof(valueGetter));
        OnChange = onChange ?? throw new ArgumentNullException(nameof(onChange));
        Receiver = receiver;
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
    /// Optional Blazor component that receives event callbacks so that Blazor can
    /// trigger a re-render after input events. May be <c>null</c> in tests or non-UI contexts.
    /// </summary>
    public IHandleEvent? Receiver { get; }
}
