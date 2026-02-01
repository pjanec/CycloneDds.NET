using System;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using CycloneDDS.Core;

namespace CycloneDDS.Core.Tests
{
    public class NativeArenaTests
    {
        [Fact]
        public void Constructor_ZerosHeadRegion()
        {
            byte[] buffer = new byte[100];
            // Fill with garbage
            Array.Fill(buffer, (byte)0xFF);
            
            unsafe 
            {
                fixed (byte* ptr = buffer)
                {
                    var arena = new NativeArena(buffer, (IntPtr)ptr, 10);
                    
                    for(int i=0; i<10; i++)
                    {
                        Assert.Equal(0, buffer[i]);
                    }
                    Assert.Equal(0xFF, buffer[10]); // Should be untouched
                }
            }
        }

        [Fact]
        public void CreateString_HandlesNull()
        {
             byte[] buffer = new byte[100];
             unsafe 
             {
                 fixed (byte* ptr = buffer)
                 {
                     var arena = new NativeArena(buffer, (IntPtr)ptr, 0);
                     Assert.Equal(IntPtr.Zero, arena.CreateString(null));
                 }
             }
        }

        [Fact]
        public void CreateString_EncodesUtf8()
        {
             byte[] buffer = new byte[100];
             unsafe 
             {
                 fixed (byte* ptr = buffer)
                 {
                     var arena = new NativeArena(buffer, (IntPtr)ptr, 0);
                     IntPtr strPtr = arena.CreateString("Hello");
                     
                     Assert.Equal((IntPtr)ptr, strPtr);
                     Assert.Equal(0x48, buffer[0]); // H
                     Assert.Equal(0x65, buffer[1]); // e
                     Assert.Equal(0x6C, buffer[2]); // l
                     Assert.Equal(0x6C, buffer[3]); // l
                     Assert.Equal(0x6F, buffer[4]); // o
                     Assert.Equal(0x00, buffer[5]); // \0
                 }
             }
        }

        [Fact]
        public void CreateSequence_AlignsTail()
        {
             byte[] buffer = new byte[100];
             unsafe 
             {
                 fixed (byte* ptr = buffer)
                 {
                     // Head size 1 to force misalignment
                     var arena = new NativeArena(buffer, (IntPtr)ptr, 1);
                     
                     // Helper to check current tail would be hard as it is private,
                     // but we can check where the buffer starts.
                     
                     var data = new double[] { 1.1 };
                     var seq = arena.CreateSequence<double>(data);
                     
                     // Previous tail was 1. Align to 8. Pointer should be at 8.
                     long offset = seq.Buffer.ToInt64() - (long)ptr;
                     Assert.Equal(8, offset);
                 }
             }
        }

         [Fact]
        public void CreateSequence_CopiesData()
        {
             byte[] buffer = new byte[100];
             unsafe 
             {
                 fixed (byte* ptr = buffer)
                 {
                     var arena = new NativeArena(buffer, (IntPtr)ptr, 0);
                     var data = new double[] { 1.1, 2.2 };
                     var seq = arena.CreateSequence<double>(data);
                     
                     Assert.Equal(2u, seq.Length);
                     Assert.Equal(2u, seq.Maximum);
                     
                     double* dPtr = (double*)seq.Buffer;
                     Assert.Equal(1.1, dPtr[0]);
                     Assert.Equal(2.2, dPtr[1]);
                 }
             }
        }

        [Fact]
        public void AllocateArray_AlignsTail()
        {
             byte[] buffer = new byte[100];
             unsafe 
             {
                 fixed (byte* ptr = buffer)
                 {
                     var arena = new NativeArena(buffer, (IntPtr)ptr, 3); // Start at 3
                     
                     var span = arena.AllocateArray<int>(1);
                     
                     // Address of span[0] should be aligned to 8 (as per implementation)
                     fixed(int* pSpan = span)
                     {
                         long offset = (long)pSpan - (long)ptr;
                         Assert.Equal(8, offset); 
                     }
                 }
             }
        }

        [Fact]
        public void AllocateArray_ZerosMemory()
        {
             byte[] buffer = new byte[100];
             Array.Fill(buffer, (byte)0xFF);
             
             unsafe 
             {
                 fixed (byte* ptr = buffer)
                 {
                     var arena = new NativeArena(buffer, (IntPtr)ptr, 0);
                     var span = arena.AllocateArray<int>(2);
                     
                     Assert.Equal(0, span[0]);
                     Assert.Equal(0, span[1]);
                 }
             }
        }

        [Fact]
        public void BoundsCheck_ThrowsOnOverflow()
        {
             byte[] buffer = new byte[10];
             unsafe 
             {
                 fixed (byte* ptr = buffer)
                 {
                     var arena = new NativeArena(buffer, (IntPtr)ptr, 0);
                     try 
                     {
                        arena.CreateString("This string is too long");
                        Assert.Fail("Expected IndexOutOfRangeException");
                     }
                     catch (IndexOutOfRangeException)
                     {
                        // Passed
                     }
                 }
             }
        }
    }
}
