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
        public void MapPrimitive_ReturnsCorrectCSharpType(string idlType, string expectedCsType)
        {
            Assert.Equal(expectedCsType, new TypeMapper().MapPrimitive(idlType));
        }

        private static MemberMapping Map(string idlType, TypeMapper? mapper = null, List<int>? dims = null, bool optional = false) =>
            (mapper ?? new TypeMapper()).MapMember(new JsonMember { Name = "m", IdlType = idlType, Dimensions = dims, IsOptional = optional }, "Mod::S");

        [Theory]
        [InlineData("int8", "sbyte")]
        [InlineData("uint8", "byte")]
        [InlineData("int16", "short")]
        [InlineData("uint64", "ulong")]
        [InlineData("float", "float")]
        [InlineData("Mod::Point", "Mod.Point")]
        public void MapMember_Single(string idlType, string expected)
        {
            var m = Map(idlType);
            Assert.Equal(expected, m.CsType);
            Assert.False(m.IsManaged);
        }

        [Fact]
        public void MapMember_Char_IsAByte_MarkedDdsChar()
        {
            var m = Map("char");
            Assert.Equal(("byte", true, false), (m.CsType, m.IsChar, m.IsUInt8));
        }

        [Fact]
        public void MapMember_Uint8_IsAByte_MarkedDdsUInt8()
        {
            var m = Map("uint8");
            Assert.Equal(("byte", false, true), (m.CsType, m.IsChar, m.IsUInt8));
            Assert.False(Map("octet").IsUInt8);
        }

        [Fact]
        public void MapMember_UnboundedSequence()
        {
            var m = Map("sequence<int32>");
            Assert.Equal("List<int>", m.CsType);
            Assert.True(m.IsManaged);
            Assert.Equal(0, m.SequenceBound);
        }

        [Fact]
        public void MapMember_BoundedSequence_TakesDdsBound()
        {
            var m = Map("sequence<Mod::Point, 10>");
            Assert.Equal(("List<Mod.Point>", 10, 0), (m.CsType, m.SequenceBound, m.StringBound));
        }

        [Fact]
        public void MapMember_BoundedString()
        {
            var m = Map("string<256>");
            Assert.Equal(("string", true, 256), (m.CsType, m.IsManaged, m.StringBound));
        }

        [Fact]
        public void MapMember_PrimitiveArray_IsAFixedBuffer_WithEveryDimension()
        {
            var m = Map("double", dims: new List<int> { 2, 3 });
            Assert.True(m.IsFixedBuffer);
            Assert.Equal("double", m.CsType);
            Assert.Equal(new[] { 2, 3 }, m.Dimensions);
            Assert.Equal(6, m.ElementCount);
        }

        [Fact]
        public void MapMember_Optional_ValueTypesBecomeNullable_ReferenceTypesStayAsTheyAre()
        {
            Assert.Equal("int?", Map("int32", optional: true).CsType);
            Assert.Equal("Mod.Point?", Map("Mod::Point", optional: true).CsType);
            Assert.Equal("string", Map("string", optional: true).CsType);
            Assert.Equal("List<int>", Map("sequence<int32>", optional: true).CsType);
        }

        [Fact]
        public void MapMember_Typedefs_AreResolved_ThroughAChain_CollectingArrayDimensions()
        {
            var mapper = new TypeMapper();
            mapper.RegisterType(new JsonTypeDefinition { Name = "T_Short", ScopedName = "M::T_Short", Kind = "alias", IdlType = "string<8>" });
            mapper.RegisterType(new JsonTypeDefinition { Name = "T_Short2", ScopedName = "M::T_Short2", Kind = "alias", IdlType = "M::T_Short" });
            mapper.RegisterType(new JsonTypeDefinition { Name = "T_Seq", ScopedName = "M::T_Seq", Kind = "sequence", IdlType = "sequence<int32, 5>" });
            mapper.RegisterType(new JsonTypeDefinition { Name = "T_Row", ScopedName = "M::T_Row", Kind = "alias", IdlType = "int32", Dimensions = new List<int> { 4 } });

            Assert.Equal(("string", 8), (Map("M::T_Short2", mapper).CsType, Map("M::T_Short2", mapper).StringBound));
            Assert.Equal(("List<int>", 5), (Map("M::T_Seq", mapper).CsType, Map("M::T_Seq", mapper).SequenceBound));
            // M::T_Row grid[3] is int32 grid[3][4]
            Assert.Equal(new[] { 3, 4 }, Map("M::T_Row", mapper, new List<int> { 3 }).Dimensions);
        }

        [Theory]
        [InlineData("wstring", "wstring")]
        [InlineData("wchar", "wchar")]
        [InlineData("long double", "long double")]
        [InlineData("sequence<sequence<int32>>", "a sequence of sequences")]
        [InlineData("sequence<string<8>>", "a sequence of bounded strings")]
        public void MapMember_Unsupported_SaysWhatAndWhere(string idlType, string what)
        {
            var ex = Assert.Throws<UnsupportedIdlException>(() => Map(idlType));
            Assert.Contains("'Mod::S.m'", ex.Message);
            Assert.Contains(what, ex.Message);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("Mod::Point")]
        [InlineData("sequence<int32>")]
        public void MapMember_ArrayOfNonPrimitives_IsUnsupported(string element)
        {
            var ex = Assert.Throws<UnsupportedIdlException>(() => Map(element, dims: new List<int> { 2 }));
            Assert.Contains("use a sequence instead", ex.Message);
        }

        [Fact]
        public void MapMember_Bitmask_IsUnsupported()
        {
            var mapper = new TypeMapper();
            mapper.RegisterType(new JsonTypeDefinition { Name = "M_Flags", ScopedName = "M::Flags", Kind = "bitmask", BitBound = 16 });
            var ex = Assert.Throws<UnsupportedIdlException>(() => Map("M::Flags", mapper));
            Assert.Contains("bitmask", ex.Message);
        }

        [Theory]
        [InlineData(null, "int")]
        [InlineData(32, "int")]
        [InlineData(16, "short")]
        [InlineData(8, "byte")]
        public void EnumUnderlyingType_FollowsTheBitBound(int? bits, string expected)
        {
            Assert.Equal(expected, TypeMapper.EnumUnderlyingType(new JsonTypeDefinition { Name = "E", Kind = "enum", BitBound = bits }));
        }

        [Fact]
        public void EnumUnderlyingType_OtherBitBounds_AreUnsupported()
        {
            Assert.Throws<UnsupportedIdlException>(() => TypeMapper.EnumUnderlyingType(new JsonTypeDefinition { Name = "E", Kind = "enum", BitBound = 12 }));
        }

        [Theory]
        [InlineData("sequence<string<8>, 4>", "sequence<string<8>, 4>")]
        [InlineData("sequence<sequence<Mod::P>>", "sequence<sequence<Mod::P>>")]
        [InlineData("long double", "long double")]
        public void IdlTypeSpec_ParsesWhatThePluginPrints(string text, string roundTrip)
        {
            Assert.Equal(roundTrip, IdlTypeSpec.Parse(text).ToString());
        }
    }
}
