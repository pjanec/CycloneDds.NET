using System;
using System.Collections.Concurrent;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Layout;

namespace DdsMonitor.Avalonia.Core;

/// <summary>
/// Thread-safe implementation of <see cref="IAvaloniaTypeDrawerRegistry"/>.
/// Unknown types fall back to a reflection-based generic editor that renders
/// a <see cref="StackPanel"/> with one <see cref="TextBox"/> per publicly-readable property.
/// </summary>
public sealed class AvaloniaTypeDrawerRegistry : IAvaloniaTypeDrawerRegistry
{
    private readonly ConcurrentDictionary<Type, Func<AvaloniaDrawerContext, object>> _factories = new();

    /// <inheritdoc/>
    public void Register(Type type, Func<AvaloniaDrawerContext, object> factory)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        _factories[type] = factory;
    }

    /// <inheritdoc/>
    public Control Build(AvaloniaDrawerContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        if (_factories.TryGetValue(ctx.TargetType, out var factory))
        {
            var result = factory(ctx);
            if (result is not Control control)
                throw new InvalidCastException(
                    $"The drawer factory registered for type '{ctx.TargetType.FullName}' returned " +
                    $"'{result?.GetType().FullName ?? "null"}' which is not an Avalonia Control.");
            return control;
        }

        return BuildFallback(ctx);
    }

    // ── Reflection-walker fallback ────────────────────────────────────────────

    /// <summary>
    /// Builds a generic editor for unknown types by reflecting over their public properties
    /// and creating a labelled <see cref="TextBox"/> for each string-convertible one.
    /// </summary>
    private static Control BuildFallback(AvaloniaDrawerContext ctx)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };

        var props = ctx.TargetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in props)
        {
            if (!prop.CanRead) continue;

            var label = new TextBlock { Text = prop.Name + ":" };

            string currentText;
            try
            {
                var val = ctx.Value != null ? prop.GetValue(ctx.Value) : null;
                currentText = val?.ToString() ?? string.Empty;
            }
            catch
            {
                currentText = string.Empty;
            }

            var box = new TextBox { Text = currentText, Watermark = prop.Name };
            var capturedProp = prop;

            box.LostFocus += (_, _) =>
            {
                // Best-effort: try to convert the text back to the property type.
                try
                {
                    var converted = Convert.ChangeType(box.Text, capturedProp.PropertyType);
                    // We can't mutate ctx.Value's property here without a setter, but we
                    // propagate OnChange with the new string for display purposes.
                    ctx.OnChange(box.Text);
                }
                catch { /* leave current value */ }
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            row.Children.Add(label);
            row.Children.Add(box);
            panel.Children.Add(row);
        }

        if (panel.Children.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"({ctx.TargetType.Name})",
                FontStyle = global::Avalonia.Media.FontStyle.Italic
            });
        }

        return panel;
    }
}
