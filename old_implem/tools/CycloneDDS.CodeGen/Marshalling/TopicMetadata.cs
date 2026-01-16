using System;

namespace CycloneDDS.CodeGen.Marshalling;

/// <summary>
/// Metadata describing a DDS topic.
/// </summary>
public class TopicMetadata
{
    public string TopicName { get; init; } = "";
    public string TypeName { get; init; } = "";
    public Type ManagedType { get; init; } = typeof(object);
    public Type NativeType { get; init; } = typeof(object);
    public Type MarshallerType { get; init; } = typeof(object);
    public int[] KeyFieldIndices { get; init; } = Array.Empty<int>();
}
