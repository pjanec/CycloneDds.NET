using System;
using System.Runtime.CompilerServices;
using Xunit;
using AdvancedTypes;
using CycloneDDS.Core;

namespace CycloneDDS.Runtime.Tests
{
    public class DebugSizes
    {
        [Fact]
        public void PrintSizes()
        {
            int complexHead = ComplexStruct.GetNativeHeadSize();
            int nestedHead = NestedStruct.GetNativeHeadSize();
            int innerHead = InnerStruct.GetNativeHeadSize();
            int innerInnerHead = InnerInnerStruct.GetNativeHeadSize();
            
            // We can't access _Native types directly as they are internal, but GetNativeHeadSize returns Unsafe.SizeOf<_Native>().
            
            // Verify sequence size?
            // DdsSequenceNative is public.
            int seqSize = Unsafe.SizeOf<DdsSequenceNative>();
            
            throw new Exception($"Sizes: Complex={complexHead}, Nested={nestedHead}, Inner={innerHead}, InnerInner={innerInnerHead}, Seq={seqSize}");
        }
    }
}
