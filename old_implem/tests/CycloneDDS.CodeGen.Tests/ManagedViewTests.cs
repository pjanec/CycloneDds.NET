using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;
using CycloneDDS.CodeGen.Emitters;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.IO;
using System;

namespace CycloneDDS.CodeGen.Tests;

public class ManagedViewTests
{
    private TypeDeclarationSyntax ParseType(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }

    [Fact]
    public void ManagedView_IsRefStruct()
    {
        var csCode = @"
[DdsTopic(""TestTopic"")]
public partial class TestType
{
    public int A;
}";
        
        var type = ParseType(csCode);
        var emitter = new ManagedViewEmitter();
        var managedCode = emitter.GenerateManagedView(type, "TestNamespace");
        
        Assert.Contains("public ref struct TestTypeManaged", managedCode);
    }

    [Fact]
    public void ManagedView_HasPrimitiveProperties()
    {
        var csCode = @"
[DdsTopic(""TestTopic"")]
public partial class TestType
{
    public int A;
    public double B;
}";
        
        var type = ParseType(csCode);
        var emitter = new ManagedViewEmitter();
        var managedCode = emitter.GenerateManagedView(type, "TestNamespace");
        
        Assert.Contains("public int A => _native.A;", managedCode);
        Assert.Contains("public double B => _native.B;", managedCode);
    }

    [Fact]
    public void ManagedView_FixedStringReturnsReadOnlySpan()
    {
        var csCode = @"
[DdsTopic(""TestTopic"")]
public partial class TestType
{
    public FixedString32 Name;
}";
        
        var type = ParseType(csCode);
        var emitter = new ManagedViewEmitter();
        var managedCode = emitter.GenerateManagedView(type, "TestNamespace");
        
        Assert.Contains("public unsafe ReadOnlySpan<byte> Name", managedCode);
        Assert.Contains("return new ReadOnlySpan<byte>(ptr, 32);", managedCode);
    }

    [Fact]
    public void ManagedView_GuidProperty()
    {
        var csCode = @"
using System;
[DdsTopic(""TestTopic"")]
public partial class TestType
{
    public Guid Id;
}";
        
        var type = ParseType(csCode);
        var emitter = new ManagedViewEmitter();
        var managedCode = emitter.GenerateManagedView(type, "TestNamespace");
        
        Assert.Contains("public unsafe Guid Id", managedCode);
        Assert.Contains("return *(Guid*)ptr;", managedCode);
    }

    [Fact]
    public void ManagedView_DateTimeProperty()
    {
        var csCode = @"
using System;
[DdsTopic(""TestTopic"")]
public partial class TestType
{
    public DateTime Timestamp;
}";
        
        var type = ParseType(csCode);
        var emitter = new ManagedViewEmitter();
        var managedCode = emitter.GenerateManagedView(type, "TestNamespace");
        
        Assert.Contains("public DateTime Timestamp => new DateTime(_native.Timestamp);", managedCode);
    }
}
