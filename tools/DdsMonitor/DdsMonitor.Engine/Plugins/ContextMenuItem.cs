namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Defines a single item in a context menu contributed by a plugin or the host UI.
/// </summary>
public sealed record ContextMenuItem(string Label, string? Icon, Func<Task> Action);
