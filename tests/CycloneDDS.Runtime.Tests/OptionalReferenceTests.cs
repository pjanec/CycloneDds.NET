using System;
using System.Collections.Generic;
using System.Threading;
using CycloneDDS.Schema;
using Xunit;

namespace CycloneDDS.Runtime.Tests
{
    public enum OptRefKind { None, One, Two = 5 }

    // @optional string / sequence / enum, as the IDL importer emits them (no nullable annotations).
    [DdsTopic("OptionalReferenceSample")]
    [DdsQos(Reliability = DdsReliability.Reliable, HistoryKind = DdsHistoryKind.KeepAll)]
    [DdsManaged]
    public partial struct OptionalReferenceSample
    {
        [DdsKey] public int Id;
        [DdsOptional] public string Text;
        [DdsOptional] public List<int> Numbers;
        [DdsOptional] public string[] Words;
        [DdsOptional] public OptRefKind? Kind;
        public int After;
    }

    public class OptionalReferenceTests
    {
        private static OptionalReferenceSample RoundTrip(OptionalReferenceSample sample)
        {
            string topic = "OptRef_" + Guid.NewGuid().ToString("N");
            using var participant = new DdsParticipant();
            using var reader = new DdsReader<OptionalReferenceSample>(participant, topic);
            using var writer = new DdsWriter<OptionalReferenceSample>(participant, topic);
            Assert.True(writer.WaitForReaderAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());
            writer.Write(sample);
            for (int i = 0; i < 50; i++)
            {
                using (var loan = reader.Take())
                    foreach (var s in loan)
                        if (s.IsValid) return s.Data;
                Thread.Sleep(100);
            }
            throw new TimeoutException("no sample");
        }

        [Fact]
        public void PresentOptionalReferenceMembers_RoundTrip()
        {
            var got = RoundTrip(new OptionalReferenceSample
            {
                Id = 1, Text = "hello", Numbers = new List<int> { 1, 2, 3 }, Words = new[] { "a", "bc" },
                Kind = OptRefKind.Two, After = 42,
            });
            Assert.Equal("hello", got.Text);
            Assert.Equal(new[] { 1, 2, 3 }, got.Numbers);
            Assert.Equal(new[] { "a", "bc" }, got.Words);
            Assert.Equal(OptRefKind.Two, got.Kind);
            Assert.Equal(42, got.After);
        }

        [Fact]
        public void AbsentOptionalReferenceMembers_RoundTripAsNull()
        {
            var got = RoundTrip(new OptionalReferenceSample { Id = 2, After = 7 });
            Assert.Null(got.Text);
            Assert.Null(got.Numbers);
            Assert.Null(got.Words);
            Assert.Null(got.Kind);
            Assert.Equal(7, got.After);
        }
    }
}
