namespace CycloneDDS.CodeGen
{
    public class IdlcResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; } = string.Empty;
        public string StandardError { get; set; } = string.Empty;
        public string[] GeneratedFiles { get; set; } = System.Array.Empty<string>();
    }
}
