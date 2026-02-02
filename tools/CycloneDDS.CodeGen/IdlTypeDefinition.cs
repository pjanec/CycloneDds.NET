namespace CycloneDDS.CodeGen
{
    public class IdlTypeDefinition
    {
        public required string CSharpFullName { get; set; }     // "Corp.Common.Point3D"
        public required string TargetIdlFile { get; set; }      // "MathDefs" (no extension)
        public required string TargetModule { get; set; }       // "Math::Geo"
        public TypeInfo? TypeInfo { get; set; }                 // Full metadata (optional for external)
        public bool IsExternal { get; set; }           // From referenced assembly?
        public string SourceFile { get; set; } = string.Empty; // C# filename for defaults
        public bool IsAlias { get; set; }
        public string BaseType { get; set; } = string.Empty;
    }
}
