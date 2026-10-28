using System;
using System.Collections.Generic;
using System.Threading;
using CycloneDDS.Schema;
using Xunit;

// "Module" and "Sequence" are IDL keywords (IDL compares keywords case-insensitively).
namespace CycloneDDS.Runtime.Tests.Module
{
    public enum Mode { Default, Interface, Normal }

    // Members named with C# keywords (IDL names an IDL importer keeps) and with IDL keywords.
    [DdsTopic("KeywordNameSample")]
    [DdsQos(Reliability = DdsReliability.Reliable, HistoryKind = DdsHistoryKind.KeepAll)]
    [DdsManaged]
    public partial struct Sequence
    {
        [DdsKey] public int @base;
        public double @params;
        public string @lock;
        public List<int> @object;
        public int Fixed;
        public Mode Interface;
        public int after;
    }
}

namespace CycloneDDS.Runtime.Tests
{
    using KeywordModule = CycloneDDS.Runtime.Tests.Module;

    public class KeywordNameTests
    {
        [Fact]
        public void CSharpAndIdlKeywordNames_RoundTrip()
        {
            string topic = "Kw_" + Guid.NewGuid().ToString("N");
            using var participant = new DdsParticipant();
            using var reader = new DdsReader<KeywordModule.Sequence>(participant, topic);
            using var writer = new DdsWriter<KeywordModule.Sequence>(participant, topic);
            Assert.True(writer.WaitForReaderAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());

            writer.Write(new KeywordModule.Sequence
            {
                @base = 7, @params = 1.5, @lock = "x", @object = new List<int> { 1, 2 },
                Fixed = 9, Interface = KeywordModule.Mode.Normal, after = 3,
            });

            KeywordModule.Sequence? got = null;
            for (int i = 0; i < 50 && got == null; i++)
            {
                using (var loan = reader.Take()) foreach (var s in loan) if (s.IsValid) got = s.Data;
                if (got == null) Thread.Sleep(100);
            }
            Assert.NotNull(got);
            var v = got.Value;
            Assert.Equal((7, 1.5, "x", 9, KeywordModule.Mode.Normal, 3), (v.@base, v.@params, v.@lock, v.Fixed, v.Interface, v.after));
            Assert.Equal(new[] { 1, 2 }, v.@object);
        }
    }
}
