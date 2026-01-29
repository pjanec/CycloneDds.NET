using Xunit;
using CycloneDDS.IdlImporter;
using CycloneDDS.Compiler.Common.IdlJson;
using System.Collections.Generic;

namespace CycloneDDS.IdlImporter.Tests
{
    public class TypeMapperTests
    {
        [Theory]
        [InlineData("long", "int")]
        [InlineData("unsigned long", "uint")]
        [InlineData("double", "double")]
        [InlineData("boolean", "bool")]
        [InlineData("string", "string")]
        [InlineData("octet", "byte")]
        [InlineData("long long", "long")]
        public void MapPrimitive_CorrectlyMapsBasicTypes(string idlType, string expectedCsType)
        {
            var mapper = new TypeMapper();
            var result = mapper.MapPrimitive(idlType);
            Assert.Equal(expectedCsType, result);
        }

        [Fact]
        public void MapMember_UnboundedSequence()
        {
            var mapper = new TypeMapper();
            var member = new JsonMember
            {
                Name = "mySeq",
                Type = "long",
                CollectionType = "sequence"
            };

            var (csType, isManaged, arrayLen, bound) = mapper.MapMember(member);

            Assert.Equal("List<int>", csType);
            Assert.True(isManaged);
            Assert.Equal(0, bound);
        }

        [Fact]
        public void MapMember_FixedArray()
        {
            var mapper = new TypeMapper();
            var member = new JsonMember
            {
                Name = "myArray",
                Type = "long",
                CollectionType = "array",
                Dimensions = new List<int> { 10 }
            };

            var (csType, isManaged, arrayLen, bound) = mapper.MapMember(member);

            Assert.Equal("int[]", csType);
            Assert.True(isManaged);
            Assert.Equal(10, arrayLen);
        }
        
        [Fact]
        public void MapMember_BoundedString()
        {
            var mapper = new TypeMapper();
            var member = new JsonMember
            {
                Name = "myStr",
                Type = "string",
                Bound = 256
            };
            
             var (csType, isManaged, arrayLen, bound) = mapper.MapMember(member);
             
             Assert.Equal("string", csType);
             Assert.True(isManaged);
             Assert.Equal(256, bound);
        }
    }
}
