using System;
using System.IO;
using System.Linq;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CycloneDDS.Schema;
using System.Collections.Generic;

namespace CycloneDDS.IdlImporter.Tests
{
    public class UnflatteningTests : IDisposable
    {
        private readonly string _testRoot;
        private readonly string _sourceRoot;
        private readonly string _outputRoot;

        public UnflatteningTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "UnflatteningTests_" + Guid.NewGuid());
            _sourceRoot = Path.Combine(_testRoot, "src");
            _outputRoot = Path.Combine(_testRoot, "out");

            Directory.CreateDirectory(_sourceRoot);
            Directory.CreateDirectory(_outputRoot);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testRoot))
            {
                try { Directory.Delete(_testRoot, true); } catch { }
            }
        }

        [Fact]
        public void Import_BagiraEnum_ShouldBeScoped()
        {
            // Setup similar to user's report
            string idlPath = Path.Combine(_sourceRoot, "UnflatteningTest.idl");
            File.WriteAllText(idlPath, @"
module TopLevelModule
{
	module DDS
	{
		module DM
		{
			module UnflatteningTest
			{

				enum DistanceTypeEnum
				{
					Meter,
					Feet,
					KM,
					Mile
				};
			};
		};
	};
};
");

            // Act
            var importer = new Importer();
            importer.Import(idlPath, _sourceRoot, _outputRoot);

            // Assert
            string csContent = File.ReadAllText(Path.Combine(_outputRoot, "UnflatteningTest.cs"));
            
            // Check for correct namespace for the enum
            // We expect: namespace Bagira.DDS.DM.BagiraIL { public enum DistanceTypeEnum ... }
            
            bool hasCorrectNamespace = csContent.Contains("namespace TopLevelModule.DDS.DM.UnflatteningTest");
            Assert.True(hasCorrectNamespace, "Should contain the full namespace path");
            
            // Analyze the content to be sure where the enum is
            // We can search for "enum DistanceTypeEnum"
            bool hasEnumShortName = csContent.Contains("enum DistanceTypeEnum");
            Assert.True(hasEnumShortName, "Should have short enum name 'DistanceTypeEnum'");
            
        }
    }
}
