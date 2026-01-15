using System;

namespace CycloneDDS.CodeGen.Runtime;

public class TopicMetadata
{
    public required string TopicName { get; init; }
    public required Type NativeType { get; init; }
    public required Type ManagedType { get; init; }
    public string? Descriptor { get; init; } // IDL descriptor string?
    public IntPtr BuiltinTopicHandle { get; init; } = IntPtr.Zero;
    // Add other fields as needed
}
