using System;

namespace CycloneDDS.CodeGen.DescriptorExtraction;

public class DescriptorData
{
    public string TypeName { get; set; } = "";
    public uint Size { get; set; }
    public uint Align { get; set; }
    public uint Flagset { get; set; }
    public uint NKeys { get; set; }
    public uint NOps { get; set; }
    public uint[] Ops { get; set; } = Array.Empty<uint>();
    public byte[] TypeInfo { get; set; } = Array.Empty<byte>();
    public byte[] TypeMap { get; set; } = Array.Empty<byte>();
    public KeyDescriptor[] Keys { get; set; } = Array.Empty<KeyDescriptor>();
    public string Meta { get; set; } = "";
}

public class KeyDescriptor
{
    public string Name { get; set; } = "";
    public ushort Flags { get; set; }
    public ushort Index { get; set; }
}
