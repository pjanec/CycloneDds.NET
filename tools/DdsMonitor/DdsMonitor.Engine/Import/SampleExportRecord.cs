using System;
using System.Text.Json;

namespace DdsMonitor.Engine.Import;

/// <summary>
/// Intermediate data-transfer record used when reading or writing a JSON export file.
/// </summary>
internal sealed class SampleExportRecord
{
    public long Ordinal { get; set; }

    public string? TopicTypeName { get; set; }

    public DateTime Timestamp { get; set; }

    public int SizeBytes { get; set; }

    /// <summary>
    /// Serialized <see cref="CycloneDDS.Runtime.DdsInstanceState"/> name.
    /// Absent in Batch-24 exports; defaults to <c>Alive</c> on import.
    /// </summary>
    public string? InstanceState { get; set; }

    public SenderExportRecord? Sender { get; set; }

    /// <summary>
    /// Raw JSON element; deserialized into the concrete topic type during import.
    /// </summary>
    public JsonElement Payload { get; set; }
}

/// <summary>
/// Intermediate data-transfer record for <see cref="SenderIdentity"/>.
/// </summary>
internal sealed class SenderExportRecord
{
    public uint ProcessId { get; set; }

    public string? MachineName { get; set; }

    public string? IpAddress { get; set; }
}
