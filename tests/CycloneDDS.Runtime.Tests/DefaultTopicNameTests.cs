using System;
using System.Threading;
using Xunit;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests
{
    [DdsTopic]
    public partial struct DefaultTopicNameMsg
    {
        [DdsId(0)]
        public int Value;
    }

    [DdsTopic("   ")]
    public partial struct WhitespaceTopicNameMsg
    {
        [DdsId(0)]
        public int Value;
    }

    [DdsTopic("ExplicitRuntimeTopicName")]
    public partial struct ExplicitTopicNameMsg
    {
        [DdsId(0)]
        public int Value;
    }

    /// <summary>
    /// A [DdsTopic] without a name opens its topic under the derived name (DdsTopicName).
    /// </summary>
    public class DefaultTopicNameTests
    {
        [Fact]
        public void NamelessTopic_WriterAndReader_ExchangeOverDerivedName()
        {
            using var participant = new DdsParticipant();
            using var writer = new DdsWriter<DefaultTopicNameMsg>(participant);
            using var reader = new DdsReader<DefaultTopicNameMsg>(participant);
            // A reader opened by the spelled-out name proves which name the writer used.
            using var namedReader = new DdsReader<DefaultTopicNameMsg>(participant, "CycloneDDS_Runtime_Tests_DefaultTopicNameMsg");

            writer.Write(new DefaultTopicNameMsg { Value = 5 });

            Assert.Equal(5, TakeOne(reader).Value);
            Assert.Equal(5, TakeOne(namedReader).Value);
        }

        [Fact]
        public void WhitespaceTopicName_CountsAsNone()
        {
            using var participant = new DdsParticipant();
            using var writer = new DdsWriter<WhitespaceTopicNameMsg>(participant);
            using var namedReader = new DdsReader<WhitespaceTopicNameMsg>(participant, "CycloneDDS_Runtime_Tests_WhitespaceTopicNameMsg");

            writer.Write(new WhitespaceTopicNameMsg { Value = 6 });

            Assert.Equal(6, TakeOne(namedReader).Value);
        }

        [Fact]
        public void ExplicitTopicName_StillWins()
        {
            using var participant = new DdsParticipant();
            using var writer = new DdsWriter<ExplicitTopicNameMsg>(participant);
            using var namedReader = new DdsReader<ExplicitTopicNameMsg>(participant, "ExplicitRuntimeTopicName");

            writer.Write(new ExplicitTopicNameMsg { Value = 7 });

            Assert.Equal(7, TakeOne(namedReader).Value);
        }

        [Fact]
        public void DdsTopicName_Of_MatchesRuntimeRule()
        {
            Assert.Equal("CycloneDDS_Runtime_Tests_DefaultTopicNameMsg", DdsTopicName.Of<DefaultTopicNameMsg>());
            Assert.Equal("CycloneDDS_Runtime_Tests_WhitespaceTopicNameMsg", DdsTopicName.Of<WhitespaceTopicNameMsg>());
            Assert.Equal("ExplicitRuntimeTopicName", DdsTopicName.Of<ExplicitTopicNameMsg>());
        }

        private static T TakeOne<T>(DdsReader<T> reader) where T : new()
        {
            for (int i = 0; i < 100; i++)
            {
                using var scope = reader.Take();
                if (scope.Count > 0) return scope[0];
                Thread.Sleep(20);
            }
            throw new TimeoutException("no sample received");
        }
    }
}
