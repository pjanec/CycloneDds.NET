using System;
using System.Collections.Concurrent;
using CycloneDDS.Schema;

namespace DdsMonitor.Engine.Ui;

/// <summary>
/// Default implementation of <see cref="ITypeDrawerRegistry"/> that pre-registers
/// sentinel stubs for common primitive CLR types so that <see cref="HasDrawer"/> and
/// <see cref="GetDrawer"/> return correct results before a UI adapter overrides them
/// with framework-specific factories (Blazor <c>RenderFragment</c>, Avalonia <c>Control</c>, etc.).
/// </summary>
public sealed class TypeDrawerRegistry : ITypeDrawerRegistry
{
    /// <summary>
    /// Shared sentinel factory used for all built-in primitive registrations.
    /// Returns <c>null</c> — UI adapters must replace these with real factories.
    /// </summary>
    private static readonly Func<DrawerContext, object?> s_uiStub = _ => null;

    private readonly ConcurrentDictionary<Type, Func<DrawerContext, object?>> _drawers = new();

    /// <summary>
    /// Initializes a new <see cref="TypeDrawerRegistry"/> and registers built-in
    /// sentinel stubs for primitive types.  Call <see cref="Register"/> to replace
    /// them with actual UI-specific factories.
    /// </summary>
    public TypeDrawerRegistry()
    {
        RegisterBuiltIns();
    }

    /// <inheritdoc />
    public void Register(Type type, Func<DrawerContext, object?> drawer)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (drawer == null) throw new ArgumentNullException(nameof(drawer));
        _drawers[type] = drawer;
    }

    /// <inheritdoc />
    public Func<DrawerContext, object?>? GetDrawer(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        // Direct lookup first.
        if (_drawers.TryGetValue(type, out var drawer))
        {
            return drawer;
        }

        // Enum types: register the stub and cache it (same instance returned on repeat calls).
        if (type.IsEnum)
        {
            _drawers[type] = s_uiStub;
            return s_uiStub;
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
        // Register UI-agnostic stubs for all built-in primitive types so that
        // HasDrawer() and GetDrawer() return correct results.  UI-specific hosts
        // (Blazor, Avalonia) must replace these with real factories via Register().
        Register(typeof(string),       s_uiStub);
        Register(typeof(int),          s_uiStub);
        Register(typeof(long),         s_uiStub);
        Register(typeof(short),        s_uiStub);
        Register(typeof(byte),         s_uiStub);
        Register(typeof(uint),         s_uiStub);
        Register(typeof(ulong),        s_uiStub);
        Register(typeof(ushort),       s_uiStub);
        Register(typeof(float),        s_uiStub);
        Register(typeof(double),       s_uiStub);
        Register(typeof(decimal),      s_uiStub);
        Register(typeof(bool),         s_uiStub);
        Register(typeof(char),         s_uiStub);
        Register(typeof(DateTime),     s_uiStub);
        Register(typeof(Guid),         s_uiStub);
        Register(typeof(FixedString32),  s_uiStub);
        Register(typeof(FixedString64),  s_uiStub);
        Register(typeof(FixedString128), s_uiStub);
        Register(typeof(FixedString256), s_uiStub);
    }
}
