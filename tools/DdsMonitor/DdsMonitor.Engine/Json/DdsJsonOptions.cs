using System.Text.Json;
using DdsMonitor.Engine.Json;

namespace DdsMonitor.Engine;

/// <summary>
/// Provides pre-configured <see cref="JsonSerializerOptions"/> instances that
/// are shared across export, import and the detail-panel display.
///
/// All options automatically register <see cref="FixedBufferJsonConverterFactory"/>
/// so that compiler-generated fixed-buffer structs
/// (<c>public unsafe fixed T Name[N]</c>) round-trip as proper JSON arrays
/// instead of the broken <c>{"FixedElementField": 0}</c> representation.
/// </summary>
public static class DdsJsonOptions
{
    /// <summary>
    /// Options used by <see cref="Export.ExportService"/>:
    /// fields included, fixed-buffer converter registered.
    /// </summary>
    public static readonly JsonSerializerOptions Export = Build(indented: false);

    /// <summary>
    /// Options used by <see cref="Import.ImportService"/>:
    /// case-insensitive, fields included, fixed-buffer converter registered.
    /// </summary>
    public static readonly JsonSerializerOptions Import = Build(indented: false, caseInsensitive: true);

    /// <summary>
    /// Options used for in-panel JSON display (pretty-printed):
    /// fields included, fixed-buffer converter registered, indented.
    /// </summary>
    public static readonly JsonSerializerOptions Display = Build(indented: true);

    private static JsonSerializerOptions Build(bool indented, bool caseInsensitive = false)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            IncludeFields = true,
            PropertyNameCaseInsensitive = caseInsensitive
        };

        options.Converters.Add(FixedBufferJsonConverterFactory.Instance);
        return options;
    }
}
