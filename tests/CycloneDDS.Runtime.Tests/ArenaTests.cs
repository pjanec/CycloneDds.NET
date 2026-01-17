using System;
using Xunit;
using CycloneDDS.Runtime.Memory;

namespace CycloneDDS.Runtime.Tests
{
    public class ArenaTests
    {
        [Fact]
        public void Rent_ReturnsBufferOfAtLeastRequestedSize()
        {
            int requested = 100;
            var buffer = Arena.Rent(requested);
            Assert.True(buffer.Length >= requested);
            Arena.Return(buffer);
        }

        [Fact]
        public void Return_AcceptsBufferWithoutError()
        {
            var buffer = Arena.Rent(50);
            Arena.Return(buffer);
        }

        [Fact]
        public void RentReturnCycle_Works()
        {
            var buffer1 = Arena.Rent(1024);
            int length1 = buffer1.Length;
            Arena.Return(buffer1);
            
            var buffer2 = Arena.Rent(1024);
            // ArrayPool guarantees returning a buffer that fits. It might be same instance.
            Assert.True(buffer2.Length >= 1024);
            Arena.Return(buffer2);
        }
        
        [Fact]
        public void Rent_LargeBuffer()
        {
             var buffer = Arena.Rent(1024 * 1024); // 1 MB
             Assert.True(buffer.Length >= 1024 * 1024);
             Arena.Return(buffer);
        }
    }
}
