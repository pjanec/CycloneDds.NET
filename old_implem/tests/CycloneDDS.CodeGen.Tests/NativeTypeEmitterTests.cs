using Xunit;
using CycloneDDS.CodeGen.Emitters;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace CycloneDDS.CodeGen.Tests;

public class NativeTypeEmitterTests
{
    private TypeDeclarationSyntax ParseType(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }
    
    [Fact]
    public void SimplePrimitives_GeneratesCorrectNativeStruct()
    {
        var csCode = @"
[DdsTopic(""SimpleTopic"")]
public partial class SimpleType
{
    public int Id;
    public float Value;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        Assert.Contains("struct SimpleTypeNative", nativeCode);
        Assert.Contains("[StructLayout(LayoutKind.Sequential, Pack = 1)]", nativeCode);
        Assert.Contains("public int Id;", nativeCode);
        Assert.Contains("public float Value;", nativeCode);
        Assert.Contains("unsafe", nativeCode);
    }
    
    [Fact]
    public void FixedString_GeneratesFixedBuffer()
    {
        var csCode = @"
[DdsTopic(""StringTopic"")]
public partial class StringType
{
    public FixedString32 Name;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        Assert.Contains("public fixed byte Name[32];", nativeCode);
    }
    
    [Fact]
    public void Guid_GeneratesFixedByteArray16()
    {
        var csCode = @"
using System;
[DdsTopic(""GuidTopic"")]
public partial class GuidType
{
    public Guid Id;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        Assert.Contains("public fixed byte Id[16];", nativeCode);
    }

    [Fact]
    public void DateTime_GeneratesInt64Field()
    {
        var csCode = @"
using System;
[DdsTopic(""DateTimeTopic"")]
public partial class DateTimeType
{
    public DateTime Timestamp;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        Assert.Contains("public long Timestamp;", nativeCode);
    }

    [Fact]
    public void UnboundedString_GeneratesPtrAndLength()
    {
        var csCode = @"
[DdsTopic(""UnboundedTopic"")]
public partial class UnboundedType
{
    public string Description;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        Assert.Contains("IntPtr Description_Ptr;", nativeCode);
        Assert.Contains("int Description_Length;", nativeCode);
    }

    [Fact]
    public void UnboundedArray_GeneratesPtrAndLength()
    {
        var csCode = @"
[DdsTopic(""ArrayTopic"")]
public partial class ArrayType
{
    public int[] Data;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        Assert.Contains("IntPtr Data_Ptr;", nativeCode);
        Assert.Contains("int Data_Length;", nativeCode);
    }

    [Fact]
    public void MixedFields_CorrectLayout()
    {
        var csCode = @"
[DdsTopic(""MixedTopic"")]
public partial class MixedType
{
    public int Id;
    public string Name;
    public FixedString32 Code;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        Assert.Contains("public int Id;", nativeCode);
        Assert.Contains("public IntPtr Name_Ptr;", nativeCode);
        Assert.Contains("public int Name_Length;", nativeCode);
        Assert.Contains("public fixed byte Code[32];", nativeCode);
    }

    [Fact]
    public void NestedStruct_ReferencesNativeType()
    {
        var csCode = @"
[DdsTopic(""NestedTopic"")]
public partial class ParentType
{
    public ChildType Child;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        Assert.Contains("public ChildTypeNative Child;", nativeCode);
    }

    [Fact]
    public void StructLayoutAttribute_Present()
    {
        var csCode = @"
[DdsTopic(""SimpleTopic"")]
public partial class SimpleType
{
    public int Id;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        Assert.Contains("[StructLayout(LayoutKind.Sequential, Pack = 1)]", nativeCode);
    }

    [Fact]
    public void UnsafeKeyword_Present()
    {
        var csCode = @"
[DdsTopic(""SimpleTopic"")]
public partial class SimpleType
{
    public int Id;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        Assert.Contains("unsafe struct", nativeCode);
    }
}
