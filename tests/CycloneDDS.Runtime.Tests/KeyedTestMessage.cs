using System;
using CycloneDDS.Schema;
using CycloneDDS.Runtime;
using CycloneDDS.Core;

namespace CycloneDDS.Runtime.Tests
{
    [DdsTopic("KeyedTestTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct KeyedTestMessage
    {
        [DdsKey] public int Id;
        public double Value;
        [DdsManaged] public string? Message;
    }
}
