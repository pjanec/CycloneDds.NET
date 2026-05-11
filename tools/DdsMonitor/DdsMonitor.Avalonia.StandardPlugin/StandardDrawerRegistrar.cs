using Avalonia.Controls;
using DdsMonitor.Avalonia.Core;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// Registers standard Avalonia drawer controls for primitive CLR types into
/// an <see cref="IAvaloniaTypeDrawerRegistry"/>.
/// </summary>
public static class StandardDrawerRegistrar
{
    public static void Register(IAvaloniaTypeDrawerRegistry registry)
    {
        // ── Integer types ─────────────────────────────────────────────────────

        registry.Register(typeof(int), ctx =>
        {
            var upDown = CreateNumericUpDown(Convert.ToDecimal(ctx.Value ?? 0), 0);
            upDown.ValueChanged += (_, _) => ctx.OnChange((int)(upDown.Value ?? 0));
            return upDown;
        });

        registry.Register(typeof(uint), ctx =>
        {
            var upDown = CreateNumericUpDown(Convert.ToDecimal(ctx.Value ?? 0u), 0, min: 0);
            upDown.ValueChanged += (_, _) => ctx.OnChange((uint)(upDown.Value ?? 0));
            return upDown;
        });

        registry.Register(typeof(long), ctx =>
        {
            var upDown = CreateNumericUpDown(Convert.ToDecimal(ctx.Value ?? 0L), 0);
            upDown.ValueChanged += (_, _) => ctx.OnChange((long)(upDown.Value ?? 0));
            return upDown;
        });

        registry.Register(typeof(ulong), ctx =>
        {
            var upDown = CreateNumericUpDown(Convert.ToDecimal(ctx.Value ?? 0UL), 0, min: 0);
            upDown.ValueChanged += (_, _) => ctx.OnChange((ulong)(upDown.Value ?? 0));
            return upDown;
        });

        registry.Register(typeof(short), ctx =>
        {
            var upDown = CreateNumericUpDown(Convert.ToDecimal(ctx.Value ?? (short)0), 0,
                min: short.MinValue, max: short.MaxValue);
            upDown.ValueChanged += (_, _) => ctx.OnChange((short)(upDown.Value ?? 0));
            return upDown;
        });

        registry.Register(typeof(ushort), ctx =>
        {
            var upDown = CreateNumericUpDown(Convert.ToDecimal(ctx.Value ?? (ushort)0), 0,
                min: 0, max: ushort.MaxValue);
            upDown.ValueChanged += (_, _) => ctx.OnChange((ushort)(upDown.Value ?? 0));
            return upDown;
        });

        registry.Register(typeof(byte), ctx =>
        {
            var upDown = CreateNumericUpDown(Convert.ToDecimal(ctx.Value ?? (byte)0), 0,
                min: byte.MinValue, max: byte.MaxValue);
            upDown.ValueChanged += (_, _) => ctx.OnChange((byte)(upDown.Value ?? 0));
            return upDown;
        });

        registry.Register(typeof(sbyte), ctx =>
        {
            var upDown = CreateNumericUpDown(Convert.ToDecimal(ctx.Value ?? (sbyte)0), 0,
                min: sbyte.MinValue, max: sbyte.MaxValue);
            upDown.ValueChanged += (_, _) => ctx.OnChange((sbyte)(upDown.Value ?? 0));
            return upDown;
        });

        // ── Floating-point types ──────────────────────────────────────────────

        registry.Register(typeof(float), ctx =>
        {
            var upDown = CreateNumericUpDown(Convert.ToDecimal(ctx.Value ?? 0f), 6);
            upDown.ValueChanged += (_, _) => ctx.OnChange((float)(upDown.Value ?? 0));
            return upDown;
        });

        registry.Register(typeof(double), ctx =>
        {
            var upDown = CreateNumericUpDown(Convert.ToDecimal(ctx.Value ?? 0d), 6);
            upDown.ValueChanged += (_, _) => ctx.OnChange((double)(upDown.Value ?? 0));
            return upDown;
        });

        registry.Register(typeof(decimal), ctx =>
        {
            var upDown = CreateNumericUpDown((decimal)(ctx.Value ?? 0m), 6);
            upDown.ValueChanged += (_, _) => ctx.OnChange(upDown.Value ?? 0m);
            return upDown;
        });

        // ── Boolean ───────────────────────────────────────────────────────────

        registry.Register(typeof(bool), ctx =>
        {
            var toggle = new ToggleSwitch
            {
                IsChecked = ctx.Value is bool b && b,
            };
            toggle.IsCheckedChanged += (_, _) => ctx.OnChange(toggle.IsChecked ?? false);
            return toggle;
        });

        // ── String ────────────────────────────────────────────────────────────

        registry.Register(typeof(string), ctx =>
        {
            var box = new TextBox { Text = ctx.Value as string ?? string.Empty };
            box.TextChanged += (_, _) => ctx.OnChange(box.Text ?? string.Empty);
            return box;
        });

        // ── Char ──────────────────────────────────────────────────────────────

        registry.Register(typeof(char), ctx =>
        {
            var box = new TextBox
            {
                Text = ctx.Value is char c ? c.ToString() : string.Empty,
                MaxLength = 1,
            };
            box.TextChanged += (_, _) =>
            {
                var text = box.Text ?? string.Empty;
                ctx.OnChange(text.Length > 0 ? text[0] : '\0');
            };
            return box;
        });
    }

    private static NumericUpDown CreateNumericUpDown(
        decimal initialValue,
        int decimalPlaces,
        decimal? min = null,
        decimal? max = null)
    {
        var upDown = new NumericUpDown
        {
            Value = initialValue,
            FormatString = decimalPlaces > 0 ? $"F{decimalPlaces}" : "F0",
        };
        if (min.HasValue) upDown.Minimum = min.Value;
        if (max.HasValue) upDown.Maximum = max.Value;
        return upDown;
    }
}
