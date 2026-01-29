using Xunit;
using CycloneDDS.Compiler.Common;
using System.IO;

namespace CycloneDDS.Compiler.Common.Tests
{
    public class IdlcRunnerTests
    {
        [Fact]
        public void CanInstantiate()
        {
            var runner = new IdlcRunner();
            Assert.NotNull(runner);
        }

        [Fact]
        public void FindIdlc_WithOverride_ReturnsPath()
        {
            var runner = new IdlcRunner();
            // Create a dummy file
            var dummyPath = Path.GetTempFileName();
            try 
            {
                runner.IdlcPathOverride = dummyPath;
                Assert.Equal(dummyPath, runner.FindIdlc());
            }
            finally
            {
                if (File.Exists(dummyPath)) File.Delete(dummyPath);
            }
        }

        [Fact]
        public void GetArguments_IncludesIncludePath_WhenProvided()
        {
            var runner = new IdlcRunner();
            string args = runner.GetArguments("test.idl", "out", "includes");
            Assert.Contains("-I \"includes\"", args);
        }

        [Fact]
        public void GetArguments_HandlesSpacesInIncludePath()
        {
            var runner = new IdlcRunner();
            string args = runner.GetArguments("test.idl", "out", "path with spaces");
            Assert.Contains("-I \"path with spaces\"", args);
        }

        [Fact]
        public void GetArguments_NoIncludePath_DoesNotContainI()
        {
            var runner = new IdlcRunner();
            string args = runner.GetArguments("test.idl", "out", null);
            Assert.DoesNotContain("-I", args);
        }
    }
}
