using Xunit;
using CycloneDDS.CodeGen.Layout;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace CycloneDDS.CodeGen.Tests;

public class LayoutCalculatorTests
{
    private TypeDeclarationSyntax ParseType(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }
    
    [Fact]
    public void SimpleStruct_CalculatesCorrectLayout()
    {
        var csCode = @"
public partial class SimpleStruct
{
    public byte B;   // offset 0, size 1
    public int I;    // offset 4 (aligned to 4), size 4
    public short S;  // offset 8, size 2
}";
        
        var type = ParseType(csCode);
        var calculator = new StructLayoutCalculator();
        var layout = calculator.CalculateLayout(type);
        
        Assert.Equal(3, layout.Fields.Count);
        
        // byte B: offset 0
        Assert.Equal(0, layout.Fields[0].Offset);
        Assert.Equal(1, layout.Fields[0].Size);
        
        // int I: aligned to 4, so offset 4 (3 bytes padding after B)
        Assert.Equal(4, layout.Fields[1].Offset);
        Assert.Equal(3, layout.Fields[1].PaddingBefore);
        
        // short S: aligned to 2, offset 8
        Assert.Equal(8, layout.Fields[2].Offset);
        
        // Max alignment is 4 (from int), so total size = 12 (10 + 2 trailing)
        Assert.Equal(4, layout.MaxAlignment);
        Assert.Equal(12, layout.TotalSize);
    }
    
    [Fact]
    public void StructWithPadding_InsertsCorrectPadding()
    {
        var csCode = @"
public partial class PaddingStruct
{
    public byte A;
    public long B; // 8 bytes, aligned to 8
}";
        
        var type = ParseType(csCode);
        var calculator = new StructLayoutCalculator();
        var layout = calculator.CalculateLayout(type);

        Assert.Equal(0, layout.Fields[0].Offset);
        Assert.Equal(8, layout.Fields[1].Offset); // 7 bytes padding
        Assert.Equal(7, layout.Fields[1].PaddingBefore);
        Assert.Equal(16, layout.TotalSize);
    }

    [Fact]
    public void StructWithTrailingPadding_AlignsToMaxField()
    {
        var csCode = @"
public partial class TrailingStruct
{
    public long A; // size 8, align 8
    public byte B; // size 1, align 1
}";
        
        var type = ParseType(csCode);
        var calculator = new StructLayoutCalculator();
        var layout = calculator.CalculateLayout(type);

        Assert.Equal(0, layout.Fields[0].Offset);
        Assert.Equal(8, layout.Fields[1].Offset);
        Assert.Equal(16, layout.TotalSize); // 9 bytes data + 7 bytes trailing padding
        Assert.Equal(7, layout.TrailingPadding);
    }

    [Fact]
    public void StructWithInt64_AlignedTo8Bytes()
    {
        var csCode = @"
public partial class Int64Struct
{
    public int A;
    public long B;
}";
        
        var type = ParseType(csCode);
        var calculator = new StructLayoutCalculator();
        var layout = calculator.CalculateLayout(type);

        Assert.Equal(0, layout.Fields[0].Offset);
        Assert.Equal(8, layout.Fields[1].Offset); // 4 bytes padding
        Assert.Equal(16, layout.TotalSize);
    }

    [Fact]
    public void StructWithMixedTypes_CorrectOffsets()
    {
        var csCode = @"
public partial class MixedStruct
{
    public byte A;
    public short B;
    public int C;
    public long D;
}";
        
        var type = ParseType(csCode);
        var calculator = new StructLayoutCalculator();
        var layout = calculator.CalculateLayout(type);

        Assert.Equal(0, layout.Fields[0].Offset); // size 1
        Assert.Equal(2, layout.Fields[1].Offset); // size 2, align 2 (1 byte padding)
        Assert.Equal(4, layout.Fields[2].Offset); // size 4, align 4
        Assert.Equal(8, layout.Fields[3].Offset); // size 8, align 8
        Assert.Equal(16, layout.TotalSize);
    }

    [Fact]
    public void StructWithFixedArray_CalculatesCorrectSize()
    {
        var csCode = @"
public partial class ArrayStruct
{
    public FixedString32 Name; // octet[32]
    public int Id;
}";
        
        var type = ParseType(csCode);
        var calculator = new StructLayoutCalculator();
        var layout = calculator.CalculateLayout(type);

        Assert.Equal(0, layout.Fields[0].Offset);
        Assert.Equal(32, layout.Fields[0].Size);
        Assert.Equal(32, layout.Fields[1].Offset); // 32 is multiple of 4
        Assert.Equal(36, layout.TotalSize);
    }

    [Fact]
    public void Union_CalculatesPayloadOffset()
    {
        var csCode = @"
[DdsUnion]
public partial class MyUnion
{
    [DdsDiscriminator]
    public byte D;       // 1 byte, alignment 1
    [DdsCase(1)]
    public long Arm64;   // 8 bytes, alignment 8
}";
        
        var type = ParseType(csCode);
        var calculator = new UnionLayoutCalculator();
        var layout = calculator.CalculateLayout(type);
        
        // Discriminator is 1 byte
        Assert.Equal(1, layout.DiscriminatorSize);
        
        // Payload must be aligned to 8 (max arm alignment)
        // So payload offset = AlignUp(1, 8) = 8
        Assert.Equal(8, layout.PayloadOffset);
        
        // Total size = 8 (payload offset) + 8 (arm size) = 16
        Assert.Equal(16, layout.TotalSize);
    }

    [Fact]
    public void UnionWithInt64Arm_PayloadAlignedTo8()
    {
        var csCode = @"
[DdsUnion]
public partial class Int64Union
{
    [DdsDiscriminator]
    public int D;
    [DdsCase(1)]
    public long Val;
}";
        
        var type = ParseType(csCode);
        var calculator = new UnionLayoutCalculator();
        var layout = calculator.CalculateLayout(type);

        Assert.Equal(4, layout.DiscriminatorSize);
        Assert.Equal(8, layout.PayloadOffset); // AlignUp(4, 8) = 8
        Assert.Equal(16, layout.TotalSize);
    }

    [Fact]
    public void UnionWithSmallDiscriminator_HasPadding()
    {
        var csCode = @"
[DdsUnion]
public partial class SmallDiscUnion
{
    [DdsDiscriminator]
    public byte D;
    [DdsCase(1)]
    public int Val;
}";
        
        var type = ParseType(csCode);
        var calculator = new UnionLayoutCalculator();
        var layout = calculator.CalculateLayout(type);

        Assert.Equal(1, layout.DiscriminatorSize);
        Assert.Equal(4, layout.PayloadOffset); // AlignUp(1, 4) = 4
        Assert.Equal(8, layout.TotalSize);
    }

    [Fact]
    public void UnionWithLargeArm_CalculatesCorrectTotalSize()
    {
        var csCode = @"
[DdsUnion]
public partial class LargeArmUnion
{
    [DdsDiscriminator]
    public int D;
    [DdsCase(1)]
    public FixedString32 Name; // 32 bytes
}";
        
        var type = ParseType(csCode);
        var calculator = new UnionLayoutCalculator();
        var layout = calculator.CalculateLayout(type);

        Assert.Equal(4, layout.DiscriminatorSize);
        Assert.Equal(4, layout.PayloadOffset); // AlignUp(4, 1) = 4 (octet array align is 1)
        Assert.Equal(36, layout.TotalSize); // 4 + 32 = 36. Max align is 4 (int). 36 is multiple of 4.
    }

    [Fact]
    public void AlignmentCalculator_AlignUpWorksCorrectly()
    {
        Assert.Equal(0, AlignmentCalculator.AlignUp(0, 4));
        Assert.Equal(4, AlignmentCalculator.AlignUp(1, 4));
        Assert.Equal(4, AlignmentCalculator.AlignUp(4, 4));
        Assert.Equal(8, AlignmentCalculator.AlignUp(5, 4));
        Assert.Equal(8, AlignmentCalculator.AlignUp(8, 8));
    }

    [Fact]
    public void AlignmentCalculator_CalculatesPaddingCorrectly()
    {
        Assert.Equal(0, AlignmentCalculator.CalculatePadding(0, 4));
        Assert.Equal(3, AlignmentCalculator.CalculatePadding(1, 4));
        Assert.Equal(0, AlignmentCalculator.CalculatePadding(4, 4));
        Assert.Equal(3, AlignmentCalculator.CalculatePadding(5, 4));
    }
}
