using System.Collections.Generic;

namespace DdsMonitor.Engine;

/// <summary>
/// Emitted when a sample row is selected in a panel.
/// </summary>
public sealed record SampleSelectedEvent(string SourcePanelId, SampleData Sample);

/// <summary>
/// Requests cloning and sending a sample payload.
/// </summary>
public sealed record CloneAndSendRequestEvent(TopicMetadata TopicMeta, object Payload);

/// <summary>
/// Requests spawning a new panel.
/// </summary>
public sealed record SpawnPanelEvent(string PanelTypeName, Dictionary<string, object>? State);

/// <summary>
/// Requests adding a column to a target panel.
/// </summary>
public sealed record AddColumnRequestEvent(string TargetPanelId, string FieldPath);
