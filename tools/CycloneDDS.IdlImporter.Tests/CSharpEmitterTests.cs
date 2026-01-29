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
                Assert.Contains("[DdsStruct]", content);
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

        [Fact]
        public void CSharpEmitter_GeneratesUnboundedSequence()
        {
            var member = new JsonMember 
            { 
                Name = "values", 
                Type = "long", 
                CollectionType = "sequence" 
            };
            
            var types = new List<JsonTypeDefinition>
            {
                new()
                {
                    Name = "TestType",
                    Kind = "struct",
                    Members = new() { member }
                }
            };
            
            var emitter = new CSharpEmitter(new TypeMapper());
            string tempFile = Path.GetTempFileName();
            try {
                emitter.GenerateCSharp(types, "test.idl", tempFile);
                var output = File.ReadAllText(tempFile);
                
                Assert.Contains("[DdsManaged]", output);
                Assert.Contains("public List<int> Values;", output);
            } finally { if(File.Exists(tempFile)) File.Delete(tempFile); }
        }

        [Fact]
        public void CSharpEmitter_GeneratesBoundedSequence()
        {
            var member = new JsonMember 
            { 
                Name = "values", 
                Type = "long", 
                CollectionType = "sequence",
                Bound = 10
            };
            
            var types = new List<JsonTypeDefinition>
            {
                new()
                {
                    Name = "TestType",
                    Kind = "struct",
                    Members = new() { member }
                }
            };
            
            var emitter = new CSharpEmitter(new TypeMapper());
            string tempFile = Path.GetTempFileName();
            try {
                emitter.GenerateCSharp(types, "test.idl", tempFile);
                var output = File.ReadAllText(tempFile);
                
                Assert.Contains("[MaxLength(10)]", output);
                Assert.Contains("[DdsManaged]", output);
                Assert.Contains("public List<int> Values;", output);
            } finally { if(File.Exists(tempFile)) File.Delete(tempFile); }
        }

        [Fact]
        public void CSharpEmitter_GeneratesFixedArray()
        {
            var member = new JsonMember 
            { 
                Name = "matrix", 
                Type = "double", 
                CollectionType = "array",
                Dimensions = new List<int> { 5 }
            };
            
            var types = new List<JsonTypeDefinition>
            {
                new()
                {
                    Name = "TestType",
                    Kind = "struct",
                    Members = new() { member }
                }
            };
            
            var emitter = new CSharpEmitter(new TypeMapper());
            string tempFile = Path.GetTempFileName();
            try {
                emitter.GenerateCSharp(types, "test.idl", tempFile);
                var output = File.ReadAllText(tempFile);
                
                Assert.Contains("[ArrayLength(5)]", output);
                Assert.Contains("[DdsManaged]", output);
                Assert.Contains("public double[] Matrix;", output);
            } finally { if(File.Exists(tempFile)) File.Delete(tempFile); }
        }

        [Fact]
        public void CSharpEmitter_GeneratesUnion()
        {
            var types = new List<JsonTypeDefinition>
            {
                new()
                {
                    Name = "TestUnion",
                    Kind = "union",
                    Discriminator = "long",
                    Members = new List<JsonMember>
                    {
                        new() { Name = "_d", Type = "long" }, // Discriminator
                        new() { Name = "int_val", Type = "long", Labels = new List<string> { "0" } },
                        new() { Name = "str_val", Type = "string", Labels = new List<string> { "1" } },
                        new() { Name = "def_val", Type = "double", Labels = new List<string> { "default" } }
                    }
                }
            };
            
            var emitter = new CSharpEmitter(new TypeMapper());
            string tempFile = Path.GetTempFileName();
            
            try 
            {
                emitter.GenerateCSharp(types, "test.idl", tempFile);
                var output = File.ReadAllText(tempFile);
                
                Assert.Contains("[DdsUnion]", output);
                Assert.Contains("[DdsDiscriminator]", output);
                Assert.Contains("public int _d;", output);
                
                Assert.Contains("[DdsCase(0)]", output);
                Assert.Contains("public int IntVal;", output);
                
                Assert.Contains("[DdsCase(1)]", output);
                Assert.Contains("[DdsManaged]", output);
                Assert.Contains("public string StrVal;", output);
                
                Assert.Contains("[DdsDefaultCase]", output);
                Assert.Contains("public double DefVal;", output);
            } 
            finally 
            { 
                if(File.Exists(tempFile)) File.Delete(tempFile); 
            }
        }

        [Fact]
        public void GeneratesOptionalMember()
        {
            // Arrange
            var types = new List<JsonTypeDefinition>
            {
                new JsonTypeDefinition
                {
                    Name = "OptionalStruct",
                    Kind = "struct",
                    Members = new List<JsonMember>
                    {
                        new JsonMember { Name = "opt_long", Type = "long", IsOptional = true },
                        new JsonMember { Name = "opt_str", Type = "string", IsOptional = true }
                    }
                }
            };
            
            var emitter = new CSharpEmitter(new TypeMapper());
            string tempFile = Path.GetTempFileName();
            
            try
            {
                // Act
                emitter.GenerateCSharp(types, "opt.idl", tempFile);
                string content = File.ReadAllText(tempFile);
                
                // Assert
                Assert.Contains("[DdsOptional]", content);
                Assert.Contains("public int? OptLong;", content);
                Assert.Contains("public string OptStr;", content);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void GeneratesMemberIds()
        {
            // Arrange
            var types = new List<JsonTypeDefinition>
            {
                new JsonTypeDefinition
                {
                    Name = "MutableStruct",
                    Kind = "struct",
                    Extensibility = "mutable",
                    Members = new List<JsonMember>
                    {
                        new JsonMember { Name = "v1", Type = "long", Id = 1 },
                        new JsonMember { Name = "v2", Type = "long", Id = 100 }
                    }
                }
            };
            
            var emitter = new CSharpEmitter(new TypeMapper());
            string tempFile = Path.GetTempFileName();
            
            try
            {
                // Act
                emitter.GenerateCSharp(types, "mutable.idl", tempFile);
                string content = File.ReadAllText(tempFile);
                
                // Assert
                Assert.Contains("[DdsId(1)]", content);
                Assert.Contains("[DdsId(100)]", content);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
