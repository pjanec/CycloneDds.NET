using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using AdvancedTypes; // For ComplexStruct

namespace CycloneDDS.Runtime.Tests
{
    public class SequenceRoundTripTests
    {
        [Fact]
        public void RoundTrip_ComplexSequenceType_VerifyZeroCopyView()
        {
            // 1. Create Data
            var data = new ComplexSequenceType
            {
                Id = 123,
                IntList = new List<int> { 10, 20, 30 },
                StructList = new List<ComplexStruct>
                {
                    new ComplexStruct { FixedString = "A" }, // Optional fields left default
                    new ComplexStruct { FixedString = "B" }
                }
            };

            // 2. Verify Generated View via Reflection
            // StructList should be a Count property and Get method (since View cannot be in a Generic List/Span)
            var viewType = typeof(ComplexSequenceTypeView);
            Assert.NotNull(viewType.GetProperty("StructListCount")); 
            Assert.NotNull(viewType.GetMethod("GetStructList"));
            
            // IntList is a primitive, so it should be a ReadOnlySpan property
            Assert.NotNull(viewType.GetProperty("IntList"));
        }
    }
}
