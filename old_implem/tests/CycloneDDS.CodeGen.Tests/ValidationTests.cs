using Xunit;
using CycloneDDS.CodeGen.Validation;
using CycloneDDS.CodeGen.Diagnostics;
using DiagnosticSeverity = CycloneDDS.CodeGen.Diagnostics.DiagnosticSeverity;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace CycloneDDS.CodeGen.Tests;

public class ValidationTests
{
    private readonly SchemaValidator _validator = new();

    private TypeDeclarationSyntax ParseType(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }

    [Fact]
    public void TopicWithoutAttribute_ReportsError()
    {
        var code = "public class MyTopic { }";
        var type = ParseType(code);
        
        _validator.ValidateTopicType(type, "Test.cs");
        
        Assert.Contains(_validator.Diagnostics, d => 
            d.Code == DiagnosticCode.MissingTopicAttribute && 
            d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void EmptyTopicName_ReportsError()
    {
        var code = @"[DdsTopic("""")] public class MyTopic { }";
        var type = ParseType(code);
        
        _validator.ValidateTopicType(type, "Test.cs");
        
        Assert.Contains(_validator.Diagnostics, d => 
            d.Code == DiagnosticCode.InvalidTopicName && 
            d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void InvalidTopicName_ReportsError()
    {
        var code = @"[DdsTopic(""Invalid Name!"")] public class MyTopic { }";
        var type = ParseType(code);
        
        _validator.ValidateTopicType(type, "Test.cs");
        
        Assert.Contains(_validator.Diagnostics, d => 
            d.Code == DiagnosticCode.InvalidTopicName && 
            d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void MissingQoS_ReportsWarning()
    {
        var code = @"[DdsTopic(""MyTopic"")] public class MyTopic { }";
        var type = ParseType(code);
        
        _validator.ValidateTopicType(type, "Test.cs");
        
        Assert.Contains(_validator.Diagnostics, d => 
            d.Code == DiagnosticCode.MissingQosAttribute && 
            d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void UnsupportedFieldType_ReportsError()
    {
        var code = @"
[DdsTopic(""MyTopic"")] 
public class MyTopic 
{ 
    public List<int> MyList; 
}";
        var type = ParseType(code);
        
        _validator.ValidateTopicType(type, "Test.cs");
        
        Assert.Contains(_validator.Diagnostics, d => 
            d.Code == DiagnosticCode.UnsupportedFieldType && 
            d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void UnionWithoutDiscriminator_ReportsError()
    {
        var code = @"[DdsUnion] public class MyUnion { }";
        var type = ParseType(code);
        
        _validator.ValidateUnionType(type, "Test.cs");
        
        Assert.Contains(_validator.Diagnostics, d => 
            d.Code == DiagnosticCode.MissingDiscriminator && 
            d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void UnionWithMultipleDiscriminators_ReportsError()
    {
        var code = @"
[DdsUnion] 
public class MyUnion 
{ 
    [DdsDiscriminator] public int D1;
    [DdsDiscriminator] public int D2;
}";
        var type = ParseType(code);
        
        _validator.ValidateUnionType(type, "Test.cs");
        
        Assert.Contains(_validator.Diagnostics, d => 
            d.Code == DiagnosticCode.MultipleDiscriminators && 
            d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void DuplicateUnionCase_ReportsError()
    {
        var code = @"
[DdsUnion] 
public class MyUnion 
{ 
    [DdsDiscriminator] public int D;
    [DdsCase(1)] public int A;
    [DdsCase(1)] public int B;
}";
        var type = ParseType(code);
        
        _validator.ValidateUnionType(type, "Test.cs");
        
        Assert.Contains(_validator.Diagnostics, d => 
            d.Code == DiagnosticCode.DuplicateCaseValue && 
            d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void MultipleDefaultCases_ReportsError()
    {
        var code = @"
[DdsUnion] 
public class MyUnion 
{ 
    [DdsDiscriminator] public int D;
    [DdsDefaultCase] public int A;
    [DdsDefaultCase] public int B;
}";
        var type = ParseType(code);
        
        _validator.ValidateUnionType(type, "Test.cs");
        
        Assert.Contains(_validator.Diagnostics, d => 
            d.Code == DiagnosticCode.MultipleDefaultCases && 
            d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ValidTopicSchema_PassesValidation()
    {
        var code = @"
[DdsTopic(""MyTopic"")]
[DdsQos]
public class MyTopic 
{ 
    public int Id;
    public string Name;
}";
        var type = ParseType(code);
        
        _validator.ValidateTopicType(type, "Test.cs");
        
        Assert.DoesNotContain(_validator.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ValidUnionSchema_PassesValidation()
    {
        var code = @"
[DdsUnion]
public class MyUnion 
{ 
    [DdsDiscriminator] public int D;
    [DdsCase(1)] public int A;
    [DdsDefaultCase] public int B;
}";
        var type = ParseType(code);
        
        _validator.ValidateUnionType(type, "Test.cs");
        
        Assert.DoesNotContain(_validator.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void MemberAdded_AllowedAppendable()
    {
        var oldCode = @"
[DdsTopic(""T"")] public class T { public int A; }";
        var newCode = @"
[DdsTopic(""T"")] public class T { public int A; public int B; }";

        var oldType = ParseType(oldCode);
        var newType = ParseType(newCode);

        var oldFp = SchemaFingerprint.Compute(oldType);
        var newFp = SchemaFingerprint.Compute(newType);

        var result = SchemaFingerprint.CompareForEvolution(oldFp, newFp);

        Assert.False(result.HasBreakingChanges);
        Assert.Equal(1, result.MembersAdded);
    }

    [Fact]
    public void MemberRemoved_ReportsEvolutionError()
    {
        var oldCode = @"
[DdsTopic(""T"")] public class T { public int A; public int B; }";
        var newCode = @"
[DdsTopic(""T"")] public class T { public int A; }";

        var oldType = ParseType(oldCode);
        var newType = ParseType(newCode);

        var oldFp = SchemaFingerprint.Compute(oldType);
        var newFp = SchemaFingerprint.Compute(newType);

        var result = SchemaFingerprint.CompareForEvolution(oldFp, newFp);

        Assert.True(result.HasBreakingChanges);
        Assert.Contains(result.Errors, e => e.Contains("removed"));
    }

    [Fact]
    public void MemberReordered_ReportsEvolutionError()
    {
        var oldCode = @"
[DdsTopic(""T"")] public class T { public int A; public int B; }";
        var newCode = @"
[DdsTopic(""T"")] public class T { public int B; public int A; }";

        var oldType = ParseType(oldCode);
        var newType = ParseType(newCode);

        var oldFp = SchemaFingerprint.Compute(oldType);
        var newFp = SchemaFingerprint.Compute(newType);

        var result = SchemaFingerprint.CompareForEvolution(oldFp, newFp);

        Assert.True(result.HasBreakingChanges);
        Assert.Contains(result.Errors, e => e.Contains("renamed") || e.Contains("reordered"));
    }

    [Fact]
    public void MemberTypeChanged_ReportsEvolutionError()
    {
        var oldCode = @"
[DdsTopic(""T"")] public class T { public int A; }";
        var newCode = @"
[DdsTopic(""T"")] public class T { public string A; }";

        var oldType = ParseType(oldCode);
        var newType = ParseType(newCode);

        var oldFp = SchemaFingerprint.Compute(oldType);
        var newFp = SchemaFingerprint.Compute(newType);

        var result = SchemaFingerprint.CompareForEvolution(oldFp, newFp);

        Assert.True(result.HasBreakingChanges);
        Assert.Contains(result.Errors, e => e.Contains("type changed"));
    }
}
