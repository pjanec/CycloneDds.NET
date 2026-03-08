using System;
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

/// <summary>
/// Requests applying a filter to a target samples panel.
/// </summary>
public sealed record ApplyFilterRequestEvent(string TargetPanelId, string FilterText);

/// <summary>
/// Emitted by a Replay-Samples panel whenever its filter predicate changes.
/// The Replay Engine subscribes to this to restrict which samples are played back.
/// </summary>
public sealed record ReplayFilterChangedEvent(Func<SampleData, bool>? Predicate);

