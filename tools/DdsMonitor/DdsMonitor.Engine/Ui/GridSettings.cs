using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DdsMonitor.Engine.Ui;

/// <summary>
/// Serialisable snapshot of a SamplesPanel layout that can be exported/imported
/// as a <c>.samplepanelsettings</c> JSON file.
/// </summary>
public sealed class GridSettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Gets or sets the active filter expression text.</summary>
    public string FilterText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ordered list of selected column structured-names
    /// (payload field names such as <c>"Position.X"</c>).
    /// </summary>
    public List<string> ColumnKeys { get; set; } = new();

    /// <summary>Gets or sets per-column fractional width weights.</summary>
    public Dictionary<string, double> ColumnWeights { get; set; } = new();

    /// <summary>Gets or sets the field being sorted on (empty means unsorted).</summary>
    public string SortFieldKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the sort direction.</summary>
    public SortDirection SortDirection { get; set; } = SortDirection.Ascending;

    // -----------------------------------------------------------------------
    // Serialisation helpers
    // -----------------------------------------------------------------------

    /// <summary>Serialises this instance to a JSON string.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    /// <summary>
    /// Deserialises a <see cref="GridSettings"/> from the provided JSON string.
    /// Returns <c>null</c> if the JSON is invalid or empty.
    /// </summary>
    public static GridSettings? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GridSettings>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
