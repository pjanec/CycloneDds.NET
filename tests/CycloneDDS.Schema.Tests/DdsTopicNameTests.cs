using System;
using CycloneDDS.Schema;
using Xunit;

namespace CycloneDDS.Schema.Tests.TopicNames
{
    [DdsTopic]
    public struct TopLevelTopic { }

    public class Outer
    {
        [DdsTopic]
        public struct InnerTopic { }

        public class Middle
        {
            [DdsTopic]
            public struct DeepTopic { }
        }
    }

    [DdsTopic("Explicit_Name")]
    public struct ExplicitTopic { }

    [DdsTopic("  ")]
    public struct WhitespaceTopic { }

    public struct NotATopic { }

    public class DdsTopicNameTests
    {
        [Fact]
        public void TopLevel_NamespaceAndName_JoinedWithUnderscore()
        {
            Assert.Equal("CycloneDDS_Schema_Tests_TopicNames_TopLevelTopic", DdsTopicName.Of<TopLevelTopic>());
        }

        [Fact]
        public void Nested_IncludesContainingTypes_NoPlus()
        {
            Assert.Equal("CycloneDDS_Schema_Tests_TopicNames_Outer_InnerTopic", DdsTopicName.Of<Outer.InnerTopic>());
            Assert.Equal("CycloneDDS_Schema_Tests_TopicNames_Outer_Middle_DeepTopic", DdsTopicName.Of(typeof(Outer.Middle.DeepTopic)));
        }

        [Fact]
        public void ExplicitName_Wins()
        {
            Assert.Equal("Explicit_Name", DdsTopicName.Of<ExplicitTopic>());
        }

        [Fact]
        public void WhitespaceName_CountsAsNone()
        {
            Assert.Equal("CycloneDDS_Schema_Tests_TopicNames_WhitespaceTopic", DdsTopicName.Of<WhitespaceTopic>());
        }

        [Fact]
        public void MissingAttribute_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => DdsTopicName.Of<NotATopic>());
        }

        [Fact]
        public void StringCore_MatchesTypeOverload()
        {
            Assert.Equal("A_B_Outer_Inner", DdsTopicName.Of("A.B", new[] { "Outer", "Inner" }));
            Assert.Equal("Inner", DdsTopicName.Of(null, new[] { "Inner" }));
            Assert.Equal("Inner", DdsTopicName.Of("", new[] { "Inner" }));
        }
    }
}
