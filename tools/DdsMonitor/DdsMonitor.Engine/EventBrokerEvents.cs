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

/// <summary>
/// Emitted when the set of active DDS participants is changed via the Participant Editor.
/// </summary>
public sealed record ParticipantsChangedEvent(IReadOnlyList<ParticipantConfig> CurrentParticipants);

/// <summary>
/// Emitted just before the workspace is serialised to JSON.
/// Subscribers may write their plugin-specific data into <see cref="PluginSettings"/>
/// under a unique key; the dictionary is then persisted as <c>"PluginSettings"</c> in
/// the workspace file.
/// </summary>
public sealed record WorkspaceSavingEvent(Dictionary<string, object> PluginSettings);

/// <summary>
/// Emitted after the workspace JSON has been deserialised and the panel state restored.
/// Subscribers read their plugin-specific data from <see cref="PluginSettings"/> using
/// the same key they used when saving.  The dictionary is empty when the loaded workspace
/// did not contain a <c>"PluginSettings"</c> section.
/// </summary>
public sealed record WorkspaceLoadedEvent(IReadOnlyDictionary<string, object> PluginSettings);

/// <summary>
/// Published by any service that has modified persistent state and needs the workspace
/// file to be re-saved.  <c>WorkspacePersistenceService</c> subscribes to this event
/// and calls <c>RequestSave()</c> which triggers the debounced save logic.
/// </summary>
public sealed record WorkspaceSaveRequestedEvent;

