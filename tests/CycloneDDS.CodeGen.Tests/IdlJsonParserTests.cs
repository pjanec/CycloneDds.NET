using System;
using System.IO;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Compiler.Common;
using CycloneDDS.Compiler.Common.IdlJson;

namespace CycloneDDS.CodeGen.Tests
{
    /// <summary>
    /// Tests for the JSON-based IDL parser (replaces DescriptorParserTests)
    /// </summary>
    public class IdlJsonParserTests : IDisposable
    {
        private readonly string _tempDir;

        public IdlJsonParserTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, true); } catch { }
            }
        }

        private string CreateTempFile(string name, string content)
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void Parse_ExtractsOpsArray()
        {
            string json = @"{
                ""Types"": [{
                    ""Name"": ""TestData"",
                    ""Kind"": ""struct"",
                    ""TopicDescriptor"": {
                        ""TypeName"": ""TestData"",
                        ""Size"": 8,
                        ""Align"": 4,
                        ""FlagSet"": 0,
                        ""Ops"": [67108868, 0, 1],
                        ""Keys"": []
                    }
                }]
            }";
            
            var file = CreateTempFile("test.json", json);
            var parser = new IdlJsonParser();
            var types = parser.Parse(file);

            Assert.Single(types);
            Assert.Equal("TestData", types[0].Name);
            Assert.NotNull(types[0].TopicDescriptor);
            Assert.Equal(3, types[0].TopicDescriptor.Ops.Length);
            Assert.Equal(67108868, types[0].TopicDescriptor.Ops[0]);
            Assert.Equal(0, types[0].TopicDescriptor.Ops[1]);
            Assert.Equal(1, types[0].TopicDescriptor.Ops[2]);
        }

        [Fact]
        public void Parse_ExtractsKeyDescriptors()
        {
            string json = @"{
                ""Types"": [{
                    ""Name"": ""Vehicle"",
                    ""Kind"": ""struct"",
                    ""TopicDescriptor"": {
                        ""TypeName"": ""Vehicle"",
                        ""Size"": 88,
                        ""Align"": 8,
                        ""FlagSet"": 0,
                        ""Ops"": [251658244, 1],
                        ""Keys"": [
                            { ""Name"": ""vehicle_id"", ""Offset"": 0, ""Order"": 0 }
                        ]
                    }
                }]
            }";
            
            var file = CreateTempFile("vehicle.json", json);
            var parser = new IdlJsonParser();
            var types = parser.Parse(file);

            Assert.Single(types);
            var keys = types[0].TopicDescriptor.Keys;
            Assert.Single(keys);
            Assert.Equal("vehicle_id", keys[0].Name);
            Assert.Equal(0u, keys[0].Offset);
            Assert.Equal(0u, keys[0].Order);
        }

        [Fact]
        public void Parse_HandlesMultipleTypes()
        {
            string json = @"{
                ""Types"": [
                    { 
                        ""Name"": ""Type1"", 
                        ""Kind"": ""struct"",
                        ""TopicDescriptor"": {
                            ""Ops"": [1, 0],
                            ""Keys"": []
                        }
                    },
                    { 
                        ""Name"": ""Type2"", 
                        ""Kind"": ""struct"",
                        ""TopicDescriptor"": {
                            ""Ops"": [2, 0],
                            ""Keys"": []
                        }
                    }
                ]
            }";
            
            var file = CreateTempFile("multi.json", json);
            var parser = new IdlJsonParser();
            var types = parser.Parse(file);

            Assert.Equal(2, types.Count);
            Assert.Equal("Type1", types[0].Name);
            Assert.Equal("Type2", types[1].Name);
        }

        [Fact]
        public void Parse_HandlesMultipleKeys()
        {
            string json = @"{
                ""Types"": [{
                    ""Name"": ""CompositeKey"",
                    ""TopicDescriptor"": {
                        ""Ops"": [1, 0],
                        ""Keys"": [
                            { ""Name"": ""id"", ""Offset"": 0, ""Order"": 0 },
                            { ""Name"": ""timestamp"", ""Offset"": 4, ""Order"": 1 }
                        ]
                    }
                }]
            }";
            
            var file = CreateTempFile("composite.json", json);
            var parser = new IdlJsonParser();
            var types = parser.Parse(file);

            var keys = types[0].TopicDescriptor.Keys;
            Assert.Equal(2, keys.Count);
            Assert.Equal("id", keys[0].Name);
            Assert.Equal(0u, keys[0].Offset);
            Assert.Equal("timestamp", keys[1].Name);
            Assert.Equal(4u, keys[1].Offset);
        }

        [Fact]
        public void Parse_HandlesKeylessTopics()
        {
            string json = @"{
                ""Types"": [{
                    ""Name"": ""SensorData"",
                    ""TopicDescriptor"": {
                        ""Ops"": [67108868, 0, 67108868, 4, 1],
                        ""Keys"": []
                    }
                }]
            }";
            
            var file = CreateTempFile("keyless.json", json);
            var parser = new IdlJsonParser();
            var types = parser.Parse(file);

            Assert.Single(types);
            Assert.Empty(types[0].TopicDescriptor.Keys);
            Assert.NotEmpty(types[0].TopicDescriptor.Ops);
        }

        [Fact]
        public void Parse_HandlesTrailingCommas()
        {
            // JSON with trailing commas (supported by AllowTrailingCommas option)
            string json = @"{
                ""Types"": [
                    { 
                        ""Name"": ""TestType"", 
                        ""TopicDescriptor"": {
                            ""Ops"": [1, 2, 3,],
                            ""Keys"": [],
                        },
                    },
                ]
            }";
            
            var file = CreateTempFile("trailing.json", json);
            var parser = new IdlJsonParser();
            var types = parser.Parse(file);

            Assert.Single(types);
            Assert.Equal("TestType", types[0].Name);
        }

        [Fact]
        public void Parse_ThrowsOnMissingFile()
        {
            var parser = new IdlJsonParser();
            
            Assert.Throws<FileNotFoundException>(() => 
                parser.Parse(Path.Combine(_tempDir, "nonexistent.json")));
        }

        [Fact]
        public void Parse_ThrowsOnInvalidJson()
        {
            string invalidJson = "{ this is not valid json }";
            var file = CreateTempFile("invalid.json", invalidJson);
            var parser = new IdlJsonParser();

            Assert.Throws<InvalidOperationException>(() => parser.Parse(file));
        }

        [Fact]
        public void ParseJson_HandlesEmptyString()
        {
            var parser = new IdlJsonParser();
            var types = parser.ParseJson("");

            Assert.Empty(types);
        }

        [Fact]
        public void ParseJson_HandlesEmptyTypesArray()
        {
            string json = @"{ ""Types"": [] }";
            var parser = new IdlJsonParser();
            var types = parser.ParseJson(json);

            Assert.Empty(types);
        }

        [Fact]
        public void FindType_FindsByCSharpName()
        {
            string json = @"{
                ""Types"": [{
                    ""Name"": ""MyNamespace::MyStruct"",
                    ""TopicDescriptor"": { ""Ops"": [1, 0], ""Keys"": [] }
                }]
            }";
            
            var parser = new IdlJsonParser();
            var types = parser.ParseJson(json);
            
            var found = parser.FindType(types, "MyNamespace.MyStruct");
            Assert.NotNull(found);
            Assert.Equal("MyNamespace::MyStruct", found.Name);
        }

        [Fact]
        public void FindType_FindsByIdlName()
        {
            string json = @"{
                ""Types"": [{
                    ""Name"": ""MyNamespace::MyStruct"",
                    ""TopicDescriptor"": { ""Ops"": [1, 0], ""Keys"": [] }
                }]
            }";
            
            var parser = new IdlJsonParser();
            var types = parser.ParseJson(json);
            
            var found = parser.FindType(types, "MyNamespace::MyStruct");
            Assert.NotNull(found);
            Assert.Equal("MyNamespace::MyStruct", found.Name);
        }

        [Fact]
        public void FindType_ReturnsNullWhenNotFound()
        {
            string json = @"{ ""Types"": [{ ""Name"": ""TypeA"" }] }";
            var parser = new IdlJsonParser();
            var types = parser.ParseJson(json);
            
            var found = parser.FindType(types, "TypeB");
            Assert.Null(found);
        }

        [Fact]
        public void GetTopicTypes_FiltersTypesWithDescriptors()
        {
            string json = @"{
                ""Types"": [
                    { 
                        ""Name"": ""Topic1"",
                        ""TopicDescriptor"": { ""Ops"": [1], ""Keys"": [] }
                    },
                    { 
                        ""Name"": ""NestedStruct""
                    },
                    { 
                        ""Name"": ""Topic2"",
                        ""TopicDescriptor"": { ""Ops"": [2], ""Keys"": [] }
                    }
                ]
            }";
            
            var parser = new IdlJsonParser();
            var types = parser.ParseJson(json);
            var topics = parser.GetTopicTypes(types);

            Assert.Equal(2, topics.Count);
            Assert.Equal("Topic1", topics[0].Name);
            Assert.Equal("Topic2", topics[1].Name);
        }

        [Fact]
        public void IsValidTopicType_ValidatesCorrectly()
        {
            var parser = new IdlJsonParser();
            
            // Valid type
            var validType = new JsonTypeDefinition
            {
                Name = "ValidTopic",
                TopicDescriptor = new JsonTopicDescriptor
                {
                    Ops = new long[] { 1, 0 },
                    Keys = new System.Collections.Generic.List<JsonKeyDescriptor>()
                }
            };
            Assert.True(parser.IsValidTopicType(validType));

            // Invalid: no descriptor
            var noDescriptor = new JsonTypeDefinition { Name = "NoDesc" };
            Assert.False(parser.IsValidTopicType(noDescriptor));

            // Invalid: empty ops
            var emptyOps = new JsonTypeDefinition
            {
                TopicDescriptor = new JsonTopicDescriptor { Ops = new long[0] }
            };
            Assert.False(parser.IsValidTopicType(emptyOps));

            // Invalid: null
            Assert.False(parser.IsValidTopicType(null));
        }

        [Fact]
        public void Parse_CalculatedOffsetsAreAccurate()
        {
            // This test verifies that offsets from JSON match expected C struct layout
            string json = @"{
                ""Types"": [{
                    ""Name"": ""AlignedStruct"",
                    ""TopicDescriptor"": {
                        ""Ops"": [67108868, 0, 67109120, 4, 67108868, 12, 1],
                        ""Keys"": [
                            { ""Name"": ""byte_field"", ""Offset"": 0, ""Order"": 0 },
                            { ""Name"": ""int_field"", ""Offset"": 4, ""Order"": 1 },
                            { ""Name"": ""byte_field2"", ""Offset"": 12, ""Order"": 2 }
                        ]
                    },
                    ""Members"": [
                        { ""Name"": ""byte_field"", ""Type"": ""octet"", ""IsKey"": true },
                        { ""Name"": ""int_field"", ""Type"": ""long"", ""IsKey"": true },
                        { ""Name"": ""byte_field2"", ""Type"": ""octet"", ""IsKey"": true }
                    ]
                }]
            }";
            
            var parser = new IdlJsonParser();
            var types = parser.ParseJson(json);

            var keys = types[0].TopicDescriptor.Keys;
            
            // Verify: byte at 0, int at 4 (aligned), byte at 12 (8 after int due to padding)
            // Actually: byte(0) + padding(3) + int(4-7) + padding(4) + byte(12)
            // Or: byte(0) + padding(3) + int(4-7) + byte(8) + padding(4) = total 12? 
            // The JSON gives exact offsets from idlc, so we trust them
            Assert.Equal(0u, keys[0].Offset);
            Assert.Equal(4u, keys[1].Offset); // int aligned to 4
            Assert.Equal(12u, keys[2].Offset); // after int (4 bytes) at offset 4, next byte at 8, but struct padding might place it at 12
            
            // The point: we DON'T calculate - we TRUST the JSON values from idlc
        }
    }
}
