namespace CycloneDDS.CodeGen.Diagnostics;

public static class DiagnosticCode
{
    // Schema structure errors
    public const string MissingTopicAttribute = "FCDC1001";
    public const string MissingQosAttribute = "FCDC1002";
    public const string InvalidTopicName = "FCDC1003";
    public const string DuplicateTopicName = "FCDC1004";
    
    // Type validation errors
    public const string UnsupportedFieldType = "FCDC1010";
    public const string MissingKeyField = "FCDC1011";  // Warning
    public const string InvalidKeyFieldType = "FCDC1012";
    
    // Union validation errors
    public const string MissingDiscriminator = "FCDC1020";
    public const string MultipleDiscriminators = "FCDC1021";
    public const string DuplicateCaseValue = "FCDC1022";
    public const string MultipleDefaultCases = "FCDC1023";
    public const string InvalidDiscriminatorType = "FCDC1024";
    public const string UnusedEnumValue = "FCDC1025";  // Warning
    
    // Bounded type errors
    public const string ExcessiveBound = "FCDC1030";
    public const string InvalidBoundValue = "FCDC1031";
    
    // Evolution errors (breaking changes)
    public const string MemberRemoved = "FCDC2001";
    public const string MemberReordered = "FCDC2002";
    public const string MemberTypeChanged = "FCDC2003";
    public const string MemberInsertedNotAtEnd = "FCDC2004";
}
