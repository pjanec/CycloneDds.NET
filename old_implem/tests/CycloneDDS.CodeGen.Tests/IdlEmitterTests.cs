using Xunit;
using CycloneDDS.CodeGen.Emitters;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace CycloneDDS.CodeGen.Tests;

public class IdlEmitterTests
{
    private TypeDeclarationSyntax ParseType(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }

    private EnumDeclarationSyntax ParseEnum(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return tree.GetRoot().DescendantNodes().OfType<EnumDeclarationSyntax>().First();
    }

    [Fact]
    public void SimpleStruct_GeneratesCorrectIdl()
    {
        var csCode = @"
[DdsTopic(""SimpleTopic"")]
public partial class SimpleType
{
    public int Id;
    public string Name;
}";
        
        var type = ParseType(csCode);
        var emitter = new IdlEmitter();
        var idl = emitter.GenerateIdl(type, "SimpleTopic");
        
        Assert.Contains("@appendable", idl);
        Assert.Contains("struct SimpleType", idl);
        Assert.Contains("long Id;", idl);
        Assert.Contains("string Name;", idl);
    }
    
    [Fact]
    public void StructWithKeyField_EmitsKeyAnnotation()
    {
        var csCode = @"
[DdsTopic(""KeyedTopic"")]
public partial class KeyedType
{
    [DdsKey]
    public int EntityId;
    public string Data;
}";
        
        var type = ParseType(csCode);
        var emitter = new IdlEmitter();
        var idl = emitter.GenerateIdl(type, "KeyedTopic");
        
        Assert.Contains("@key long EntityId;", idl);
        Assert.Contains("string Data;", idl);
    }

    [Fact]
    public void StructWithOptionalField_EmitsOptionalAnnotation()
    {
        var csCode = @"
[DdsTopic(""OptionalTopic"")]
public partial class OptionalType
{
    public int? OptionalInt;
}";
        
        var type = ParseType(csCode);
        var emitter = new IdlEmitter();
        var idl = emitter.GenerateIdl(type, "OptionalTopic");
        
        Assert.Contains("@optional long OptionalInt;", idl);
    }

    [Fact]
    public void StructWithArray_EmitsSequence()
    {
        var csCode = @"
[DdsTopic(""ArrayTopic"")]
public partial class ArrayType
{
    public int[] IntArray;
}";
        
        var type = ParseType(csCode);
        var emitter = new IdlEmitter();
        var idl = emitter.GenerateIdl(type, "ArrayTopic");
        
        Assert.Contains("sequence<long> IntArray;", idl);
    }

    [Fact]
    public void StructWithFixedString_EmitsOctetArray()
    {
        var csCode = @"
[DdsTopic(""FixedStringTopic"")]
public partial class FixedStringType
{
    public FixedString32 Name;
}";
        
        var type = ParseType(csCode);
        var emitter = new IdlEmitter();
        var idl = emitter.GenerateIdl(type, "FixedStringTopic");
        
        Assert.Contains("octet[32] Name;", idl);
    }

    [Fact]
    public void StructWithGuid_EmitsTypedef()
    {
        var csCode = @"
using System;
[DdsTopic(""GuidTopic"")]
public partial class GuidType
{
    public Guid Id;
}";
        
        var type = ParseType(csCode);
        var emitter = new IdlEmitter();
        var idl = emitter.GenerateIdl(type, "GuidTopic");
        
        Assert.Contains("typedef octet Guid16[16];", idl);
        Assert.Contains("Guid16 Id;", idl); // MapToIdl returns Guid or Guid16?
        // Wait, MapToIdl for "Guid" returns "Guid" (default case), but RequiresTypedef is true.
        // If RequiresTypedef is true, we emit typedef.
        // But what does MapToIdl return for Guid? It returns "Guid" (default).
        // The typedef is "typedef octet Guid16[16];".
        // So the field type in IDL should be "Guid16" or "Guid"?
        // The typedef defines "Guid16". If the field uses "Guid", it won't match.
        // I need to check IdlTypeMapper.MapToIdl logic for Guid.
        // It falls through to MapCustomType -> returns "Guid".
        // So I should probably update MapToIdl to return "Guid16" for Guid if I want to use that typedef.
        // Or change the typedef to "typedef octet Guid[16];".
        // The instruction says: "typedef octet Guid16[16];"
        // So I should probably map Guid to Guid16.
    }

    [Fact]
    public void Union_GeneratesCorrectIdl()
    {
        var csCode = @"
[DdsUnion]
public partial class MyUnion
{
    [DdsDiscriminator]
    public int D;
    [DdsCase(1)]
    public int A;
}";
        
        var type = ParseType(csCode);
        var emitter = new IdlEmitter();
        var idl = emitter.GenerateUnionIdl(type);
        
        Assert.Contains("union MyUnion switch(long) {", idl);
        Assert.Contains("case 1: long A;", idl);
    }

    [Fact]
    public void UnionWithDefaultCase_EmitsDefaultCase()
    {
        var csCode = @"
[DdsUnion]
public partial class MyUnion
{
    [DdsDiscriminator]
    public int D;
    [DdsDefaultCase]
    public int Default;
}";
        
        var type = ParseType(csCode);
        var emitter = new IdlEmitter();
        var idl = emitter.GenerateUnionIdl(type);
        
        Assert.Contains("default: long Default;", idl);
    }

    [Fact]
    public void Enum_GeneratesCorrectIdl()
    {
        var csCode = @"
public enum MyEnum : short
{
    A = 1,
    B = 2
}";
        
        var enumDecl = ParseEnum(csCode);
        var emitter = new IdlEmitter();
        var idl = emitter.GenerateEnumIdl(enumDecl);
        
        Assert.Contains("enum MyEnum : int16 {", idl);
        Assert.Contains("A = 1,", idl);
        Assert.Contains("B = 2", idl);
    }

    [Fact]
    public void NestedStruct_EmitsNestedType()
    {
        // This test assumes that nested types are handled by simply using their name.
        // The emitter doesn't recursively emit nested types definitions inside the struct, 
        // but assumes they are defined elsewhere (e.g. in the same file or another file).
        // This test just checks if the field type is correctly emitted as the custom type name.
        var csCode = @"
[DdsTopic(""NestedTopic"")]
public partial class ParentType
{
    public ChildType Child;
}";
        
        var type = ParseType(csCode);
        var emitter = new IdlEmitter();
        var idl = emitter.GenerateIdl(type, "NestedTopic");
        
        Assert.Contains("ChildType Child;", idl);
    }

    [Fact]
    public void ComplexSchema_GeneratesValidIdl()
    {
        var csCode = @"
using System;
[DdsTopic(""ComplexTopic"")]
public partial class ComplexType
{
    [DdsKey]
    public int Id;
    public string Name;
    public double? Value;
    public int[] Data;
    public Guid Uuid;
    public FixedString32 ShortName;
    public MyEnum Status;
}";
        
        var type = ParseType(csCode);
        var emitter = new IdlEmitter();
        var idl = emitter.GenerateIdl(type, "ComplexTopic");
        
        Assert.Contains("@appendable", idl);
        Assert.Contains("module Default {", idl);
        Assert.Contains("typedef octet Guid16[16];", idl);
        Assert.Contains("struct ComplexType {", idl);
        Assert.Contains("@key long Id;", idl);
        Assert.Contains("string Name;", idl);
        Assert.Contains("@optional double Value;", idl);
        Assert.Contains("sequence<long> Data;", idl);
        Assert.Contains("Guid16 Uuid;", idl);
        Assert.Contains("octet[32] ShortName;", idl);
        Assert.Contains("MyEnum Status;", idl);
    }

    [Fact]
    public void StructWithQuaternion_EmitsStructDefinition()
    {
        var csCode = @"
[DdsTopic(""QuaternionTopic"")]
public partial class QuaternionType
{
    public Quaternion Rotation;
}";
        
        var type = ParseType(csCode);
        var emitter = new IdlEmitter();
        var idl = emitter.GenerateIdl(type, "QuaternionTopic");
        
        Assert.Contains("struct QuaternionF32x4 { float x; float y; float z; float w; };", idl);
        Assert.Contains("QuaternionF32x4 Rotation;", idl);
    }

    [Fact]
    public void StructWithBoundedSeq_EmitsBoundedSequence()
    {
        var csCode = @"
[DdsTopic(""BoundedSeqTopic"")]
public partial class BoundedSeqType
{
    public BoundedSeq<int, 100> LimitedData;
}";
        
        var type = ParseType(csCode);
        var emitter = new IdlEmitter();
        var idl = emitter.GenerateIdl(type, "BoundedSeqTopic");
        
        // Should emit: sequence<long, 100> LimitedData;
        Assert.Contains("sequence<long, 100> LimitedData;", idl);
    }
}
