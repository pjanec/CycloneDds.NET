using System;
using System.Collections.Generic;
using System.IO;
using CycloneDDS.Compiler.Common.IdlJson;
using Xunit;

namespace CycloneDDS.IdlImporter.Tests
{
    public class CSharpEmitterTests
    {
        [Fact]
        public void GenerateCSharp_GeneratesSimpleStruct_WithPascalCaseFields()
        {
            // Arrange
            var types = new List<JsonTypeDefinition>
            {
                new JsonTypeDefinition
                {
                    Name = "Module::MyStruct",
                    Kind = "struct",
                    Members = new List<JsonMember>
                    {
                        new JsonMember { Name = "my_field", Type = "long", Offset = 0 }
                    }
                }
            };
            
            var emitter = new CSharpEmitter(new TypeMapper());
            string tempFile = Path.GetTempFileName();
            
            try
            {
                // Act
                emitter.GenerateCSharp(types, "original.idl", tempFile);
                
                // Assert
                string content = File.ReadAllText(tempFile);
                
                Assert.Contains("namespace Module", content); // Converted namespace
                Assert.Contains("public partial struct MyStruct", content);
                Assert.Contains("public int MyField;", content); // PascalCase verification
                Assert.Contains("[DdsStruct(\"Module::MyStruct\")]", content);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateCSharp_GeneratesEnum_WithPascalCaseMembers()
        {
            // Arrange
            var types = new List<JsonTypeDefinition>
            {
                new JsonTypeDefinition
                {
                    Name = "Colors",
                    Kind = "enum",
                    Members = new List<JsonMember>
                    {
                        new JsonMember { Name = "RED_COLOR", Value = 0 },
                        new JsonMember { Name = "green_color", Value = 1 }
                    }
                }
            };

            var emitter = new CSharpEmitter(new TypeMapper());
            string tempFile = Path.GetTempFileName();
            
            try
            {
                // Act
                emitter.GenerateCSharp(types, "enum.idl", tempFile);
                string content = File.ReadAllText(tempFile);
                
                // Assert
                Assert.Contains("public enum Colors : int", content);
                Assert.Contains("RedColor = 0", content); 
                Assert.Contains("GreenColor = 1", content); 
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
        
        [Fact]
        public void GenerateCSharp_CorrectlyMapsIdlPrimitives()
        {
            // Arrange
            var types = new List<JsonTypeDefinition>
            {
                new JsonTypeDefinition
                {
                    Name = "Primitives",
                    Kind = "struct",
                    Members = new List<JsonMember>
                    {
                        new JsonMember { Name = "f1", Type = "int32_t" },
                        new JsonMember { Name = "f2", Type = "uint32_t" },
                        new JsonMember { Name = "f3", Type = "boolean" }
                    }
                }
            };
            
            var emitter = new CSharpEmitter(new TypeMapper());
            string tempFile = Path.GetTempFileName();
            
            try
            {
                emitter.GenerateCSharp(types, "primitives.idl", tempFile);
                string content = File.ReadAllText(tempFile);
                
                Assert.Contains("public int F1;", content);
                Assert.Contains("public uint F2;", content);
                Assert.Contains("public bool F3;", content);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
