using System.Text.Json;
using System.Text.Json.Serialization;
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
///
/// ME1-C05: All options also register <see cref="JsonStringEnumConverter"/> so
/// that enum values are serialized as strings rather than numeric literals.
/// </summary>
public static class DdsJsonOptions
{
    /// <summary>
    /// Options used by <see cref="Export.ExportService"/>:
    /// fields included, fixed-buffer converter registered, enums as strings.
    /// </summary>
    public static readonly JsonSerializerOptions Export = Build(indented: false);

    /// <summary>
    /// Like <see cref="Export"/>, but fixed-size byte buffers are written as one base64 string
    /// (headless <c>RecordBytesAsBase64</c>). <see cref="Import"/> reads both forms.
    /// </summary>
    public static readonly JsonSerializerOptions ExportBase64Bytes = Build(indented: false, bytesAsBase64: true);

    /// <summary>
    /// Options used by <see cref="Import.ImportService"/>:
    /// case-insensitive, fields included, fixed-buffer converter registered, enums as strings.
    /// </summary>
    public static readonly JsonSerializerOptions Import = Build(indented: false, caseInsensitive: true);

    /// <summary>
    /// Options used for in-panel JSON display (pretty-printed):
    /// fields included, fixed-buffer converter registered, enums as strings, indented.
    /// </summary>
    public static readonly JsonSerializerOptions Display = Build(indented: true);

    private static JsonSerializerOptions Build(bool indented, bool caseInsensitive = false, bool bytesAsBase64 = false)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            IncludeFields = true,
            PropertyNameCaseInsensitive = caseInsensitive
        };

        // ME1-C05: serialize enums as string names, not integer literals
        options.Converters.Add(new JsonStringEnumConverter());
        // ME1-C08: strip inactive union arms from serialized output
        options.Converters.Add(DdsUnionJsonConverterFactory.Instance);
        // FixedStringN: serialize as JSON string (not {"Length":N} which is useless)
        options.Converters.Add(FixedStringJsonConverterFactory.Instance);
        options.Converters.Add(bytesAsBase64 ? FixedBufferJsonConverterFactory.Base64Bytes : FixedBufferJsonConverterFactory.Instance);
        return options;
    }
}
