using Xunit;
using CycloneDDS.Compiler.Common;
using CycloneDDS.Compiler.Common.IdlJson;
using System.Collections.Generic;

namespace CycloneDDS.Compiler.Common.Tests
{
    public class IdlJsonParserTests
    {
        [Fact]
        public void ParseJson_EmptyString_ReturnsEmptyList()
        {
            var parser = new IdlJsonParser();
            var result = parser.ParseJson("");
            Assert.Empty(result);
        }

        [Fact]
        public void ParseJson_ValidJson_ReturnsTypes()
        {
            var json = @"{
                ""Types"": [
                    {
                        ""Name"": ""TestType"",
                        ""Kind"": ""struct""
                    }
                ]
            }";

            var parser = new IdlJsonParser();
            var result = parser.ParseJson(json);
            
            Assert.Single(result);
            Assert.Equal("TestType", result[0].Name);
            Assert.Equal("struct", result[0].Kind);
        }
    }
}
