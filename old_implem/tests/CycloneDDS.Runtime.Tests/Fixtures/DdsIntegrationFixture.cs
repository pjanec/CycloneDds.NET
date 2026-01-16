using System;
using CycloneDDS.Runtime;
using CycloneDDS.CodeGen.Runtime; // Runtime Registry
using TestDataRegistry = CycloneDDS.Schema.TestData.MetadataRegistry; // Schema Registry
using Xunit;

namespace CycloneDDS.Runtime.Tests.Fixtures;

public class DdsIntegrationFixture : IDisposable
{
    public DdsParticipant Participant { get; private set; }

    public DdsIntegrationFixture()
    {
        // Register test types
        foreach (var topic in TestDataRegistry.GetAllTopics())
        {
            MetadataRegistry.Register(topic);
        }

        // Create a domain participant on the default domain
        // Ensure we load the native library first if needed, but DdsParticipant constructor should handle it if static initializer runs.
        Participant = new DdsParticipant(0); // Default domain
    }


    public void Dispose()
    {
        Participant?.Dispose();
    }
}

[CollectionDefinition("DDS Integration")]
public class DdsIntegrationCollection : ICollectionFixture<DdsIntegrationFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
