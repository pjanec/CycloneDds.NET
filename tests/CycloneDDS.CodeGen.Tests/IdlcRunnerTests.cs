using System;
using System.IO;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Compiler.Common;

namespace CycloneDDS.CodeGen.Tests
{
    public class IdlcRunnerTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _mockIdlcPath;

        public IdlcRunnerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            
            // Create a mock idlc (batch file) for testing
            _mockIdlcPath = Path.Combine(_tempDir, "idlc.bat");
            File.WriteAllText(_mockIdlcPath, @"
@echo off
echo Mock IDLC running...
set outputDir=%4
set outputDir=%outputDir:""=%
set idlFile=%5
set idlFile=%idlFile:""=%

rem Extract filename without extension (simple approximation for batch)
for %%F in (%idlFile%) do set filename=%%~nF

echo Generating %outputDir%\%filename%.json
echo { ""Types"": [] } > ""%outputDir%\%filename%.json""
exit /b 0
");
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, true); } catch { }
            }
        }

        [Fact]
        public void FindIdlc_Throws_WhenNotFound()
        {
            var runner = new IdlcRunner();
            runner.IdlcPathOverride = Path.Combine(_tempDir, "nonexistent_idlc.exe");
            Assert.Throws<FileNotFoundException>(() => runner.FindIdlc());
        }

        [Fact]
        public void FindIdlc_ReturnsOverride_WhenExists()
        {
            var runner = new IdlcRunner();
            var dummyIdlc = Path.Combine(_tempDir, "idlc.exe");
            File.WriteAllText(dummyIdlc, "dummy");
            
            runner.IdlcPathOverride = dummyIdlc;
            var path = runner.FindIdlc();
            
            Assert.Equal(dummyIdlc, path);
        }

        [Fact]
        public void FindIdlc_ChecksCurrentDirectory()
        {
            // Create a dummy idlc in the current directory (where tests run)
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var dummyIdlc = Path.Combine(currentDir, "idlc.exe");
            
            // Only run this test if idlc doesn't already exist there (to avoid messing up environment)
            if (!File.Exists(dummyIdlc))
            {
                File.WriteAllText(dummyIdlc, "dummy");
                try
                {
                    var runner = new IdlcRunner();
                    var path = runner.FindIdlc();
                    Assert.Equal(dummyIdlc, path);
                }
                finally
                {
                    File.Delete(dummyIdlc);
                }
            }
        }

        [Fact]
        public void RunIdlc_ExecutesProcess_AndFindsFiles()
        {
            // Setup
            var idlPath = Path.Combine(_tempDir, "TestTopic.idl");
            File.WriteAllText(idlPath, "struct TestTopic {};");
            
            var outputDir = Path.Combine(_tempDir, "Output");
            
            var runner = new IdlcRunner();
            runner.IdlcPathOverride = _mockIdlcPath; // Use our mock batch file
            
            // Act
            var result = runner.RunIdlc(idlPath, outputDir);
            
            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Mock IDLC running", result.StandardOutput);
            Assert.Contains(Path.Combine(outputDir, "TestTopic.json"), result.GeneratedFiles);
            Assert.True(File.Exists(Path.Combine(outputDir, "TestTopic.json")));
        }
    }
}

