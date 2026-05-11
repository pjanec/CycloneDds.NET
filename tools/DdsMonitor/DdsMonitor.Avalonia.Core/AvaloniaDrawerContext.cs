using System;
using Avalonia.Controls;

namespace DdsMonitor.Avalonia.Core;

/// <summary>
/// Context passed to Avalonia drawer factories.
/// Mirrors Engine's <c>DrawerContext</c> without any Blazor dependency.
/// </summary>
public sealed class AvaloniaDrawerContext
{
    /// <summary>Gets the display label for this field.</summary>
    public string Label { get; }

    /// <summary>Gets the CLR type of the field being edited.</summary>
    public Type TargetType { get; }

    /// <summary>Gets the current field value.</summary>
    public object? Value { get; }

    /// <summary>Invoked when the user commits a new value.</summary>
    public Action<object?> OnChange { get; }

    /// <summary>Invoked when input fails validation.  Pass <c>null</c> to clear the error.</summary>
    public Action<string?> OnValidationError { get; }

    /// <summary>Creates a new <see cref="AvaloniaDrawerContext"/>.</summary>
    public AvaloniaDrawerContext(
        string label,
        Type targetType,
        object? value,
        Action<object?> onChange,
        Action<string?>? onValidationError = null)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        Value = value;
        OnChange = onChange ?? throw new ArgumentNullException(nameof(onChange));
        OnValidationError = onValidationError ?? (_ => { });
    }
}
