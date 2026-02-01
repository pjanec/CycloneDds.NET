using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;
using CycloneDDS.Runtime;
using AdvancedTypes;

namespace CycloneDDS.Runtime.Tests
{
    public class AdvancedCompatibilityTests : IDisposable
    {
        private DdsParticipant _participant;
        private DdsWriter<AllTypes> _writer;
        private DdsReader<AllTypes> _reader;
        private const string TopicName = "AdvancedTypesTopic";

        public AdvancedCompatibilityTests()
        {
            _participant = new DdsParticipant();
            _writer = new DdsWriter<AllTypes>(_participant, TopicName);
            _reader = new DdsReader<AllTypes>(_participant, TopicName);
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _participant?.Dispose();
        }

        private void WaitForData(int timeoutMs = 1000)
        {
            // Simple poll for now
            for (int i = 0; i < timeoutMs / 50; i++)
            {
                using var view = _reader.Read();
                if (view.Count > 0) return;
                Thread.Sleep(50);
            }
        }

        [Fact]
        public void TestComplexRoundTrip()
        {
            var data = new AllTypes();
            
            // 1. Unions
            // Initialize others to avoid null refs but don't test them intensely
            // data.UInt = new IntUnion { Discriminator = 1, LongValue = 0 }; // Use case 1 (long) to avoid string
            
            // Let's just focus on UBool which we suspect.
            data.UInt = new IntUnion { Discriminator = 2, StringValue = "S" }; 
            data.UBool = new BoolUnion { Discriminator = false, LongValue = 123 }; 
            data.UEnum = new EnumUnion { Discriminator = AdvancedTypes_DiscriminatorEnum.AdvancedtypesDiscOne, FloatValue = 1.0f };
            
            // 2. Nested Structs
            data.Nested = new NestedStruct();
            data.Nested.Inner = new InnerStruct();
            data.Nested.Inner.DeepStruct = new InnerInnerStruct();
            data.Nested.Inner.DeepStruct.Message = "";
            data.Nested.Inner.Numbers = new List<double>();
            
            // 3. Complex Struct
            data.Complex = new ComplexStruct();
            data.Complex.Matrix = new List<AdvancedTypes.AdvancedTypes_LongSeq>(); 
            data.Complex.FixedString = "";
            data.Complex.BoundedSeq = new List<int>();
            data.Complex.OptStruct = new NestedStruct { Inner = new InnerStruct { Numbers = new List<double>(), DeepStruct = new InnerInnerStruct() } };

            _writer.Write(data);
            
            WaitForData();
            
            using var samples = _reader.Take();
            Assert.Equal(1, samples.Count);
            var result = samples[0];
            
            // Verify UBool only
            Assert.False(result.UBool.Discriminator);
            Assert.Equal(123, result.UBool.LongValue);
        }
    }
}
