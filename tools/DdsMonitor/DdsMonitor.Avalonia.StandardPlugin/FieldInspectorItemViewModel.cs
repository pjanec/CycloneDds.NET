namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// A single row in the detail inspector field tree.
/// Represents a flattened field from <see cref="DdsMonitor.Engine.TopicMetadata.AllFields"/>.
/// </summary>
public sealed class FieldInspectorItemViewModel
{
    public string Name { get; init; } = "";
    public string ValueText { get; init; } = "<null>";
    public bool IsNested { get; init; }
    public int Depth { get; init; }
}
