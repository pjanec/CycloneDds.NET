using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DdsMonitor.Engine;

/// <summary>
/// Root document model for the workspace JSON file.
/// Wraps the active panel states together with the global excluded-topics list so that
/// both pieces of state are persisted in one file.
/// </summary>
public sealed class WorkspaceDocument
{
    /// <summary>Gets or sets the list of active panel states.</summary>
    public List<PanelState> Panels { get; set; } = new();

    /// <summary>
    /// Gets or sets the fully-qualified CLR type names of topics that the user has
    /// explicitly unsubscribed from.  On startup the app re-subscribes to every known
    /// topic <em>except</em> those listed here, restoring the subscription state from
    /// the previous interactive session.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? ExcludedTopics { get; set; }
}
