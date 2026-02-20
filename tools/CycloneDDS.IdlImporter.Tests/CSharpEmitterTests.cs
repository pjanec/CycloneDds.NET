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
        public void GenerateCSharp_GeneratesSimpleStruct_RetainsFieldCase()
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
                Assert.Contains("public int my_field;", content); // Case retention verification
                Assert.Contains("[DdsStruct]", content);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateCSharp_GeneratesEnum_RetainsMemberCase()
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
                Assert.Contains("RED_COLOR = 0", content); 
                Assert.Contains("green_color = 1", content); 
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
                
                Assert.Contains("public int f1;", content);
                Assert.Contains("public uint f2;", content);
                Assert.Contains("public bool f3;", content);
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
                Assert.Contains("public List<int> values;", output);
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
                Assert.Contains("public List<int> values;", output);
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
                Assert.Contains("public double[] matrix;", output);
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
                Assert.Contains("public int int_val;", output);
                
                Assert.Contains("[DdsCase(1)]", output);
                Assert.Contains("[DdsManaged]", output);
                Assert.Contains("public string str_val;", output);
                
                Assert.Contains("[DdsDefaultCase]", output);
                Assert.Contains("public double def_val;", output);
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
                Assert.Contains("public int? opt_long;", content);
                Assert.Contains("public string opt_str;", content);
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

        [Fact]
        public void GenerateCSharp_RetainsMixedCaseFieldNames()
        {
            // Arrange – various casing styles that IDL authors use
            var types = new List<JsonTypeDefinition>
            {
                new JsonTypeDefinition
                {
                    Name = "MixedCaseStruct",
                    Kind = "struct",
                    Members = new List<JsonMember>
                    {
                        new JsonMember { Name = "camelCaseField",    Type = "long" },
                        new JsonMember { Name = "SCREAMING_SNAKE",   Type = "long" },
                        new JsonMember { Name = "PascalAlready",     Type = "long" },
                        new JsonMember { Name = "lower_snake_case",  Type = "long" },
                        new JsonMember { Name = "mixedCamel_Snake",  Type = "long" }
                    }
                }
            };

            var emitter = new CSharpEmitter(new TypeMapper());
            string tempFile = Path.GetTempFileName();

            try
            {
                emitter.GenerateCSharp(types, "mixed.idl", tempFile);
                string content = File.ReadAllText(tempFile);

                // Every name must appear exactly as declared – no PascalCase transformation
                Assert.Contains("public int camelCaseField;",   content);
                Assert.Contains("public int SCREAMING_SNAKE;",  content);
                Assert.Contains("public int PascalAlready;",    content);
                Assert.Contains("public int lower_snake_case;", content);
                Assert.Contains("public int mixedCamel_Snake;", content);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateCSharp_UnionMembersRetainCase()
        {
            // Arrange
            var types = new List<JsonTypeDefinition>
            {
                new JsonTypeDefinition
                {
                    Name = "CaseUnion",
                    Kind = "union",
                    Discriminator = "long",
                    Members = new List<JsonMember>
                    {
                        new JsonMember { Name = "snake_value",   Type = "long",   Labels = new List<string> { "0" } },
                        new JsonMember { Name = "camelValue",    Type = "double", Labels = new List<string> { "1" } },
                        new JsonMember { Name = "UPPER_VALUE",   Type = "long",   Labels = new List<string> { "default" } }
                    }
                }
            };

            var emitter = new CSharpEmitter(new TypeMapper());
            string tempFile = Path.GetTempFileName();

            try
            {
                emitter.GenerateCSharp(types, "union.idl", tempFile);
                string content = File.ReadAllText(tempFile);

                Assert.Contains("public int snake_value;",  content);
                Assert.Contains("public double camelValue;",content);
                Assert.Contains("public int UPPER_VALUE;",  content);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateCSharp_SanitizesKeywords()
        {
            // Arrange
            var types = new List<JsonTypeDefinition>
            {
                new JsonTypeDefinition
                {
                    Name = "KeywordStruct",
                    Kind = "struct",
                    Members = new List<JsonMember>
                    {
                        new JsonMember { Name = "class", Type = "long" },
                        new JsonMember { Name = "int", Type = "long" },
                        new JsonMember { Name = "virtual", Type = "boolean" }
                    }
                }
            };
            
            var emitter = new CSharpEmitter(new TypeMapper());
            string tempFile = Path.GetTempFileName();
            
            try
            {
                // Act
                emitter.GenerateCSharp(types, "keywords.idl", tempFile);
                string content = File.ReadAllText(tempFile);
                
                // Assert
                Assert.Contains("public int @class;", content);
                Assert.Contains("public int @int;", content);
                Assert.Contains("public bool @virtual;", content);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
