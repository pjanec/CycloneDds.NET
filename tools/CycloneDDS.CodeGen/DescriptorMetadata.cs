namespace CycloneDDS.CodeGen
{
    public class KeyDescriptorInfo 
    {
        public string Name { get; set; } = string.Empty;
        public uint Offset { get; set; }
        public uint Index { get; set; }
    }

    public class DescriptorMetadata
    {
        public string TypeName { get; set; } = string.Empty;
        public string OpsArrayName { get; set; } = string.Empty;
        public uint[] OpsValues { get; set; } = System.Array.Empty<uint>();
        public string KeysArrayName { get; set; } = string.Empty;
        public uint[] KeysValues { get; set; } = System.Array.Empty<uint>();
        public KeyDescriptorInfo[] KeyDescriptors { get; set; } = System.Array.Empty<KeyDescriptorInfo>();
    }
}
