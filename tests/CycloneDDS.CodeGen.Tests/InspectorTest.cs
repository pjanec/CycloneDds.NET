using Xunit;
using CycloneDDS.CodeGen;

namespace CycloneDDS.CodeGen.Tests
{
    public class InspectorTest
    {
        [Fact]
        public void RunInspection()
        {
            Inspector.Inspect();
        }
    }
}

