using System;
using System.Globalization;
using CycloneDDS.Schema;
using DdsMonitor.Engine.Ui;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace DdsMonitor.Services;

/// <summary>
/// Blazor-specific adapter that wraps <see cref="ITypeDrawerRegistry"/> and provides
/// <see cref="RenderFragment{DrawerContext}"/> factories for all registered types.
/// Also responsible for registering Blazor render-fragment implementations for
/// the built-in primitive types that the Engine stubs out.
/// </summary>
public sealed class BlazorTypeDrawerAdapter
{
    private readonly ITypeDrawerRegistry _registry;

    /// <summary>
    /// Initialises the adapter and registers Blazor-specific drawer factories
    /// for all built-in primitive types.
    /// </summary>
    public BlazorTypeDrawerAdapter(ITypeDrawerRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        RegisterBuiltIns();
    }

    /// <summary>
    /// Returns a Blazor <see cref="RenderFragment{DrawerContext}"/> for the given type,
    /// or <c>null</c> if no drawer is registered.
    /// </summary>
    public RenderFragment<DrawerContext>? GetBlazorDrawer(Type type)
    {
        var factory = _registry.GetDrawer(type);
        if (factory == null) return null;
        return ctx => (RenderFragment)factory(ctx)!;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Built-in Blazor drawer registrations
    // ─────────────────────────────────────────────────────────────────────────

    private void RegisterBuiltIns()
    {
        Register(typeof(string), BuildTextDrawer());
        Register(typeof(int),    BuildNumberDrawer(TryParseInt));
        Register(typeof(long),   BuildNumberDrawer(TryParseLong));
        Register(typeof(short),  BuildNumberDrawer(TryParseShort));
        Register(typeof(byte),   BuildNumberDrawer(TryParseByte));
        Register(typeof(uint),   BuildNumberDrawer(TryParseUInt));
        Register(typeof(ulong),  BuildNumberDrawer(TryParseULong));
        Register(typeof(ushort), BuildNumberDrawer(TryParseUShort));
        Register(typeof(float),  BuildFloatDrawer(TryParseFloat));
        Register(typeof(double), BuildFloatDrawer(TryParseDouble));
        Register(typeof(decimal),BuildFloatDrawer(TryParseDecimal));
        Register(typeof(bool),   BuildCheckboxDrawer());
        Register(typeof(char),   BuildTextDrawer(maxLength: 1, charMode: true));
        Register(typeof(DateTime), BuildDateTimeDrawer());
        Register(typeof(Guid),   BuildGuidDrawer());
        Register(typeof(FixedString32),  BuildFixedStringDrawer(FixedString32.Capacity,  s => new FixedString32(s)));
        Register(typeof(FixedString64),  BuildFixedStringDrawer(FixedString64.Capacity,  s => new FixedString64(s)));
        Register(typeof(FixedString128), BuildFixedStringDrawer(FixedString128.Capacity, s => new FixedString128(s)));
        Register(typeof(FixedString256), BuildFixedStringDrawer(FixedString256.Capacity, s => new FixedString256(s)));
    }

    private void Register(Type type, RenderFragment<DrawerContext> blazorDrawer)
    {
        _registry.Register(type, ctx => (object)blazorDrawer(ctx));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Drawer builders (moved from Engine's TypeDrawerRegistry)
    // ─────────────────────────────────────────────────────────────────────────

    private static RenderFragment<DrawerContext> BuildTextDrawer(int? maxLength = null, bool charMode = false)
    {
        return ctx => builder =>
        {
            var seq = 0;
            var current = ctx.ValueGetter()?.ToString() ?? string.Empty;
            var cb = EventCallback.Factory.Create<ChangeEventArgs>(ctx,
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

    private static RenderFragment<DrawerContext> BuildFixedStringDrawer<T>(int capacity, Func<string, T> fromString)
    {
        return ctx => builder =>
        {
            var seq = 0;
            var current = ctx.ValueGetter()?.ToString() ?? string.Empty;
            var cb = EventCallback.Factory.Create<ChangeEventArgs>(ctx,
                args =>
                {
                    var text = args.Value?.ToString() ?? string.Empty;
                    try
                    {
                        ctx.OnChange(fromString(text));
                        ctx.OnValidationError?.Invoke(null);
                    }
                    catch (ArgumentException ex)
                    {
                        ctx.OnValidationError?.Invoke(ex.Message);
                    }
                });
            builder.OpenElement(seq++, "input");
            builder.AddAttribute(seq++, "type", "text");
            builder.AddAttribute(seq++, "class", "dynamic-form__input");
            builder.AddAttribute(seq++, "value", current);
            builder.AddAttribute(seq++, "maxlength", capacity);
            builder.AddAttribute(seq++, "oninput", cb);
            builder.CloseElement();
        };
    }

    private static RenderFragment<DrawerContext> BuildGuidDrawer()
    {
        return ctx => builder =>
        {
            var seq = 0;
            var current = ctx.ValueGetter()?.ToString() ?? string.Empty;
            var cb = EventCallback.Factory.Create<ChangeEventArgs>(ctx,
                args =>
                {
                    var text = args.Value?.ToString() ?? string.Empty;
                    if (Guid.TryParse(text, out var parsed))
                    {
                        ctx.OnChange(parsed);
                        ctx.OnValidationError?.Invoke(null);
                    }
                    else if (string.IsNullOrEmpty(text))
                    {
                        ctx.OnChange(Guid.Empty);
                        ctx.OnValidationError?.Invoke(null);
                    }
                    else
                    {
                        ctx.OnValidationError?.Invoke("Invalid GUID format (expected: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)");
                    }
                });

            builder.OpenElement(seq++, "input");
            builder.AddAttribute(seq++, "type", "text");
            builder.AddAttribute(seq++, "class", "dynamic-form__input");
            builder.AddAttribute(seq++, "value", current);
            builder.AddAttribute(seq++, "placeholder", "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx");
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
            var cb = EventCallback.Factory.Create<ChangeEventArgs>(ctx,
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
            var rawVal = ctx.ValueGetter();
            var current = (rawVal as IFormattable)
                              ?.ToString(null, CultureInfo.InvariantCulture)
                          ?? rawVal?.ToString()
                          ?? "0";
            var cb = EventCallback.Factory.Create<ChangeEventArgs>(ctx,
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
            var cb = EventCallback.Factory.Create<ChangeEventArgs>(ctx,
                args =>
                {
                    var newValue = args.Value is bool bVal && bVal;
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

            var cb = EventCallback.Factory.Create<ChangeEventArgs>(ctx,
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

    /// <summary>
    /// Builds and registers a Blazor enum drawer for the given type.
    /// Called lazily when GetBlazorDrawer encounters an enum type without a direct registration.
    /// </summary>
    public RenderFragment<DrawerContext>? GetOrBuildEnumDrawer(Type enumType)
    {
        var names = Enum.GetNames(enumType);

        RenderFragment<DrawerContext> blazorDrawer = ctx => builder =>
        {
            var seq = 0;
            var current = ctx.ValueGetter()?.ToString() ?? string.Empty;
            var cb = EventCallback.Factory.Create<ChangeEventArgs>(ctx,
                args =>
                {
                    var text = args.Value?.ToString() ?? string.Empty;
                    try
                    {
                        ctx.OnChange(Enum.Parse(enumType, text));
                    }
                    catch (ArgumentException)
                    {
                        // Ignore invalid values.
                    }
                });

            builder.OpenElement(seq++, "select");
            builder.AddAttribute(seq++, "class", "dynamic-form__select");
            builder.AddAttribute(seq++, "onchange", cb);

            foreach (var name in names)
            {
                builder.OpenElement(seq++, "option");
                builder.AddAttribute(seq++, "value", name);
                if (string.Equals(name, current, StringComparison.Ordinal))
                    builder.AddAttribute(seq++, "selected", true);
                builder.AddContent(seq++, name);
                builder.CloseElement();
            }

            builder.CloseElement(); // select
        };

        _registry.Register(enumType, ctx2 => (object)blazorDrawer(ctx2));
        return blazorDrawer;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Parse helpers
    // ─────────────────────────────────────────────────────────────────────────

    private delegate bool TryParseDelegate(string text, out object? result);

    private static bool TryParseInt(string text, out object? result)
    { if (int.TryParse(text, out var v)) { result = v; return true; } result = null; return false; }

    private static bool TryParseLong(string text, out object? result)
    { if (long.TryParse(text, out var v)) { result = v; return true; } result = null; return false; }

    private static bool TryParseShort(string text, out object? result)
    { if (short.TryParse(text, out var v)) { result = v; return true; } result = null; return false; }

    private static bool TryParseByte(string text, out object? result)
    { if (byte.TryParse(text, out var v)) { result = v; return true; } result = null; return false; }

    private static bool TryParseUInt(string text, out object? result)
    { if (uint.TryParse(text, out var v)) { result = v; return true; } result = null; return false; }

    private static bool TryParseULong(string text, out object? result)
    { if (ulong.TryParse(text, out var v)) { result = v; return true; } result = null; return false; }

    private static bool TryParseUShort(string text, out object? result)
    { if (ushort.TryParse(text, out var v)) { result = v; return true; } result = null; return false; }

    private static bool TryParseFloat(string text, out object? result)
    {
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        { result = v; return true; }
        result = null; return false;
    }

    private static bool TryParseDouble(string text, out object? result)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        { result = v; return true; }
        result = null; return false;
    }

    private static bool TryParseDecimal(string text, out object? result)
    {
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        { result = v; return true; }
        result = null; return false;
    }
}
