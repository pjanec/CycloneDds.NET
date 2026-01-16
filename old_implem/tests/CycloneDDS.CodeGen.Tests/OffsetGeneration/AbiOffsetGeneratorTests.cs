using Xunit;
using CycloneDDS.Runtime.Descriptors;

namespace CycloneDDS.CodeGen.Tests.OffsetGeneration;

public class AbiOffsetGeneratorTests
{
    [Fact]
    public void AbiOffsets_GeneratedFile_HasRequiredConstants()
    {
        // Verify AbiOffsets.g.cs was generated correctly
        Assert.True(AbiOffsets.DescriptorSize > 0);
        Assert.True(AbiOffsets.Size >= 0);
        Assert.True(AbiOffsets.TypeName >= 0);
    }

    [Fact]
    public void AbiOffsets_DescriptorSize_MatchesExpected()
    {
        // For x64, descriptor should be ~88-96 bytes
        // On my machine (Windows x64) it was 96 bytes.
        Assert.InRange(AbiOffsets.DescriptorSize, 80, 120);
    }
}
