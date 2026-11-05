using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CycloneDDS.CodeGen.Tests;

public sealed class FrameworkReferenceEnumTests : IDisposable
{
    private readonly string _sourceDir;

    public FrameworkReferenceEnumTests()
    {
        _sourceDir = Path.Combine(Path.GetTempPath(), "CG_FWREF_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sourceDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_sourceDir, true); } catch { }
    }

    [Fact]
    public void DiscoverTopics_WithFrameworkReferenceAssemblies_PreservesEnumMembers()
    {
        File.WriteAllText(
            Path.Combine(_sourceDir, "ValueTypeDiscriminator.cs"),
            """
            namespace Repro
            {
                public enum ValueTypeDiscriminator : int
                {
                    Empty_ = 0,
                    Object_ = 1,
                    Boolean_ = 2,
                    Int_ = 3,
                    UInt_ = 4,
                    Float_ = 5,
                    String_ = 6,
                    ByteArray_ = 7,
                    SingleArray_ = 8,
                    DoubleArray_ = 9,
                    Waveform_ = 10,
                    DateTime_ = 11,
                }
            }
            """);

        var discovery = new SchemaDiscovery();
        var types = discovery.DiscoverTopics(_sourceDir, FindFrameworkReferenceAssemblies());
        var discriminator = Assert.Single(types, t => t.Name == "ValueTypeDiscriminator");

        Assert.Equal(
            new[]
            {
                "Empty_", "Object_", "Boolean_", "Int_", "UInt_", "Float_",
                "String_", "ByteArray_", "SingleArray_", "DoubleArray_", "Waveform_", "DateTime_",
            },
            discriminator.EnumMembers);
        Assert.Equal(Enumerable.Range(0, 12).Select(i => (long)i), discriminator.EnumMemberValues);
    }

    private static IReadOnlyList<string> FindFrameworkReferenceAssemblies()
    {
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)
            ?? throw new InvalidOperationException("Cannot locate the .NET runtime directory.");
        var dotnetRoot = Path.GetFullPath(Path.Combine(runtimeDir, "..", "..", ".."));
        var targetingPacks = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");

        Assert.True(Directory.Exists(targetingPacks),
            $"Expected framework reference assemblies under '{targetingPacks}'.");

        var referenceDir = Directory.EnumerateDirectories(targetingPacks)
            .Select(versionDir => Path.Combine(versionDir, "ref"))
            .Where(Directory.Exists)
            .SelectMany(Directory.EnumerateDirectories)
            .Where(dir => File.Exists(Path.Combine(dir, "System.Runtime.dll")))
            .OrderByDescending(dir => ParseFrameworkVersion(Path.GetFileName(dir)))
            .First();

        return Directory.GetFiles(referenceDir, "*.dll");
    }

    private static Version ParseFrameworkVersion(string directoryName)
    {
        var value = directoryName.StartsWith("net", StringComparison.Ordinal)
            ? directoryName[3..]
            : directoryName;
        return Version.TryParse(value, out var version) ? version : new Version();
    }
}
