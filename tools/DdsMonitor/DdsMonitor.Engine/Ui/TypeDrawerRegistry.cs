using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace DdsMonitor.Engine.Ui;

/// <summary>
/// Default implementation of <see cref="ITypeDrawerRegistry"/> that ships with
/// built-in input drawers for the common primitive CLR types.
/// </summary>
public sealed class TypeDrawerRegistry : ITypeDrawerRegistry
{
    private readonly ConcurrentDictionary<Type, RenderFragment<DrawerContext>> _drawers
        = new();

    /// <summary>
    /// Initializes a new <see cref="TypeDrawerRegistry"/> and registers the
    /// built-in primitive drawers.
    /// </summary>
    public TypeDrawerRegistry()
    {
        RegisterBuiltIns();
    }

    /// <inheritdoc />
    public void Register(Type type, RenderFragment<DrawerContext> drawer)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (drawer == null) throw new ArgumentNullException(nameof(drawer));
        _drawers[type] = drawer;
    }

    /// <inheritdoc />
    public RenderFragment<DrawerContext>? GetDrawer(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        // Direct lookup first.
        if (_drawers.TryGetValue(type, out var drawer))
        {
            return drawer;
        }

        // Enum types: build a <select> on the fly and cache it.
        if (type.IsEnum)
        {
            var enumDrawer = BuildEnumDrawer(type);
            _drawers[type] = enumDrawer;
            return enumDrawer;
        }

        // Nullable<T>: forward to inner type drawer.
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            return GetDrawer(underlying);
        }

        return null;
    }

    /// <inheritdoc />
    public bool HasDrawer(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        if (_drawers.ContainsKey(type)) return true;
        if (type.IsEnum) return true;
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) return HasDrawer(underlying);
        return false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Built-in drawer registrations
    // ─────────────────────────────────────────────────────────────────────────

    private void RegisterBuiltIns()
    {
        Register(typeof(string), BuildTextDrawer());
        Register(typeof(int), BuildNumberDrawer(TryParseInt));
        Register(typeof(long), BuildNumberDrawer(TryParseLong));
        Register(typeof(short), BuildNumberDrawer(TryParseShort));
        Register(typeof(byte), BuildNumberDrawer(TryParseByte));
        Register(typeof(uint), BuildNumberDrawer(TryParseUInt));
        Register(typeof(ulong), BuildNumberDrawer(TryParseULong));
        Register(typeof(ushort), BuildNumberDrawer(TryParseUShort));
        Register(typeof(float), BuildFloatDrawer(TryParseFloat));
        Register(typeof(double), BuildFloatDrawer(TryParseDouble));
        Register(typeof(decimal), BuildFloatDrawer(TryParseDecimal));
        Register(typeof(bool), BuildCheckboxDrawer());
        Register(typeof(char), BuildTextDrawer(maxLength: 1, charMode: true));
        Register(typeof(DateTime), BuildDateTimeDrawer());
        Register(typeof(Guid), BuildTextDrawer());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Drawer builders
    // ─────────────────────────────────────────────────────────────────────────

    private static RenderFragment<DrawerContext> BuildTextDrawer(int? maxLength = null, bool charMode = false)
    {
        return ctx => builder =>
        {
            var seq = 0;
            var current = ctx.ValueGetter()?.ToString() ?? string.Empty;
            var cb = EventCallback.Factory.Create<ChangeEventArgs>(ctx.Receiver!,
                args =>
                {
                    var text = args.Value?.ToString() ?? string.Empty;
                    object? value = charMode
                        ? (text.Length > 0 ? (object)text[0] : '\0')
                        : text;
                    ctx.OnChange(value);
                });

            builder.OpenElement(seq++, "input");
            builder.AddAttribute(seq++, "type", "text");
            builder.AddAttribute(seq++, "class", "dynamic-form__input");
            builder.AddAttribute(seq++, "value", current);
            if (maxLength.HasValue)
            {
                builder.AddAttribute(seq++, "maxlength", maxLength.Value);
            }
            builder.AddAttribute(seq++, "oninput", cb);
            builder.CloseElement();
        };
    }

    private static RenderFragment<DrawerContext> BuildNumberDrawer(TryParseDelegate tryParse)
    {
        return ctx => builder =>
        {
            var seq = 0;
            var current = ctx.ValueGetter()?.ToString() ?? "0";
            var cb = EventCallback.Factory.Create<ChangeEventArgs>(ctx.Receiver!,
                args =>
                {
                    var text = args.Value?.ToString() ?? string.Empty;
                    if (tryParse(text, out var parsed))
                    {
                        ctx.OnChange(parsed);
                    }
                });

            builder.OpenElement(seq++, "input");
            builder.AddAttribute(seq++, "type", "number");
            builder.AddAttribute(seq++, "class", "dynamic-form__input");
            builder.AddAttribute(seq++, "value", current);
            builder.AddAttribute(seq++, "oninput", cb);
            builder.CloseElement();
        };
    }

    private static RenderFragment<DrawerContext> BuildFloatDrawer(TryParseDelegate tryParse)
    {
        return ctx => builder =>
        {
            var seq = 0;
            var current = ctx.ValueGetter()?.ToString() ?? "0";
            var cb = EventCallback.Factory.Create<ChangeEventArgs>(ctx.Receiver!,
                args =>
                {
                    var text = args.Value?.ToString() ?? string.Empty;
                    if (tryParse(text, out var parsed))
                    {
                        ctx.OnChange(parsed);
                    }
                });

            builder.OpenElement(seq++, "input");
            builder.AddAttribute(seq++, "type", "number");
            builder.AddAttribute(seq++, "class", "dynamic-form__input");
            builder.AddAttribute(seq++, "step", "any");
            builder.AddAttribute(seq++, "value", current);
            builder.AddAttribute(seq++, "oninput", cb);
            builder.CloseElement();
        };
    }

    private static RenderFragment<DrawerContext> BuildCheckboxDrawer()
    {
        return ctx => builder =>
        {
            var seq = 0;
            var current = ctx.ValueGetter() is bool b && b;
            var cb = EventCallback.Factory.Create<ChangeEventArgs>(ctx.Receiver!,
                args =>
                {
                    var newValue = args.Value is bool bVal ? bVal : false;
                    ctx.OnChange(newValue);
                });

            builder.OpenElement(seq++, "input");
            builder.AddAttribute(seq++, "type", "checkbox");
            builder.AddAttribute(seq++, "class", "dynamic-form__checkbox");
            if (current)
            {
                builder.AddAttribute(seq++, "checked", true);
            }
            builder.AddAttribute(seq++, "onchange", cb);
            builder.CloseElement();
        };
    }

    private static RenderFragment<DrawerContext> BuildDateTimeDrawer()
    {
        return ctx => builder =>
        {
            var seq = 0;
            var raw = ctx.ValueGetter();
            var current = raw is DateTime dt
                ? dt.ToString("yyyy-MM-ddTHH:mm:ss")
                : string.Empty;

            var cb = EventCallback.Factory.Create<ChangeEventArgs>(ctx.Receiver!,
                args =>
                {
                    var text = args.Value?.ToString() ?? string.Empty;
                    if (DateTime.TryParse(text, out var parsed))
                    {
                        ctx.OnChange(parsed);
                    }
                });

            builder.OpenElement(seq++, "input");
            builder.AddAttribute(seq++, "type", "datetime-local");
            builder.AddAttribute(seq++, "class", "dynamic-form__input");
            builder.AddAttribute(seq++, "value", current);
            builder.AddAttribute(seq++, "oninput", cb);
            builder.CloseElement();
        };
    }

    private static RenderFragment<DrawerContext> BuildEnumDrawer(Type enumType)
    {
        var names = Enum.GetNames(enumType);
        var values = Enum.GetValues(enumType);

        return ctx => builder =>
        {
            var seq = 0;
            var current = ctx.ValueGetter()?.ToString() ?? string.Empty;
            var cb = EventCallback.Factory.Create<ChangeEventArgs>(ctx.Receiver!,
                args =>
                {
                    var text = args.Value?.ToString() ?? string.Empty;
                    try
                    {
                        var parsed = Enum.Parse(enumType, text);
                        ctx.OnChange(parsed);
                    }
                    catch (ArgumentException)
                    {
                        // Ignore invalid values.
                    }
                });

            builder.OpenElement(seq++, "select");
            builder.AddAttribute(seq++, "class", "dynamic-form__select");
            builder.AddAttribute(seq++, "onchange", cb);

            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];
                builder.OpenElement(seq++, "option");
                builder.AddAttribute(seq++, "value", name);
                if (string.Equals(name, current, StringComparison.Ordinal))
                {
                    builder.AddAttribute(seq++, "selected", true);
                }
                builder.AddContent(seq++, name);
                builder.CloseElement();
            }

            builder.CloseElement(); // select
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Parse helpers
    // ─────────────────────────────────────────────────────────────────────────

    private delegate bool TryParseDelegate(string text, out object? result);

    private static bool TryParseInt(string text, out object? result)
    {
        if (int.TryParse(text, out var v)) { result = v; return true; }
        result = null; return false;
    }

    private static bool TryParseLong(string text, out object? result)
    {
        if (long.TryParse(text, out var v)) { result = v; return true; }
        result = null; return false;
    }

    private static bool TryParseShort(string text, out object? result)
    {
        if (short.TryParse(text, out var v)) { result = v; return true; }
        result = null; return false;
    }

    private static bool TryParseByte(string text, out object? result)
    {
        if (byte.TryParse(text, out var v)) { result = v; return true; }
        result = null; return false;
    }

    private static bool TryParseUInt(string text, out object? result)
    {
        if (uint.TryParse(text, out var v)) { result = v; return true; }
        result = null; return false;
    }

    private static bool TryParseULong(string text, out object? result)
    {
        if (ulong.TryParse(text, out var v)) { result = v; return true; }
        result = null; return false;
    }

    private static bool TryParseUShort(string text, out object? result)
    {
        if (ushort.TryParse(text, out var v)) { result = v; return true; }
        result = null; return false;
    }

    private static bool TryParseFloat(string text, out object? result)
    {
        if (float.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v))
        { result = v; return true; }
        result = null; return false;
    }

    private static bool TryParseDouble(string text, out object? result)
    {
        if (double.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v))
        { result = v; return true; }
        result = null; return false;
    }

    private static bool TryParseDecimal(string text, out object? result)
    {
        if (decimal.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v))
        { result = v; return true; }
        result = null; return false;
    }
}

