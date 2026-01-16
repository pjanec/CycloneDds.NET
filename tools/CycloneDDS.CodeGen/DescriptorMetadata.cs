namespace CycloneDDS.CodeGen
{
    public class DescriptorMetadata
    {
        public string TypeName { get; set; } = string.Empty;
        public string OpsArrayName { get; set; } = string.Empty;
        public uint[] OpsValues { get; set; } = System.Array.Empty<uint>();
        public string KeysArrayName { get; set; } = string.Empty;
        public uint[] KeysValues { get; set; } = System.Array.Empty<uint>();
    }
}
