using System;
using System.Runtime.InteropServices;
using CycloneDDS.Runtime.Memory;
using Xunit;

namespace CycloneDDS.Runtime.Tests.Memory;

public class ArenaTests
{
    [Fact]
    public void Arena_Constructor_AllocatesInitialCapacity()
    {
        using var arena = new Arena(1024);
        Assert.Equal(1024, arena.Capacity);
        Assert.Equal(0, arena.Position);
        Assert.Equal(1024, arena.Available);
    }

    [Fact]
    public void Arena_Allocate_ReturnValidPointer()
    {
        using var arena = new Arena();
        var ptr = arena.Allocate(100);
        Assert.NotEqual(IntPtr.Zero, ptr);
        Assert.Equal(104, arena.Position); // 100 aligned to 8 bytes = 104
    }

    [Fact]
    public void Arena_Allocate_AlignsTo8Bytes()
    {
        using var arena = new Arena();
        
        // Allocate 1 byte, should take 8 bytes
        var ptr1 = arena.Allocate(1);
        Assert.Equal(8, arena.Position);
        
        // Allocate 9 bytes, should take 16 bytes
        var ptr2 = arena.Allocate(9);
        Assert.Equal(24, arena.Position); // 8 + 16 = 24
        
        // Verify ptr2 is 8 bytes after ptr1
        Assert.Equal(IntPtr.Add(ptr1, 8), ptr2);
    }

    [Fact]
    public void Arena_Allocate_GrowsWhenNeeded()
    {
        using var arena = new Arena(128);
        
        // Fill initial capacity
        arena.Allocate(120);
        var initialCapacity = arena.Capacity;
        
        // Allocate more to trigger growth
        arena.Allocate(20);
        
        Assert.True(arena.Capacity > initialCapacity);
        Assert.True(arena.Capacity >= 140); // 120 + 20
    }

    [Fact]
    public void Arena_Reset_ReusesBuffer()
    {
        using var arena = new Arena();
        var ptr1 = arena.Allocate(100);
        
        arena.Reset();
        Assert.Equal(0, arena.Position);
        
        var ptr2 = arena.Allocate(100);
        Assert.Equal(ptr1, ptr2); // Should be same address
    }

    [Fact]
    public void Arena_GetMark_Rewind_Works()
    {
        using var arena = new Arena();
        arena.Allocate(100);
        var mark = arena.GetMark();
        
        arena.Allocate(50);
        Assert.True(arena.Position > mark);
        
        arena.Rewind(mark);
        Assert.Equal(mark, arena.Position);
    }

    [Fact]
    public void Arena_Trim_ReducesCapacity()
    {
        // Start small but grow large
        using var arena = new Arena(1024);
        
        // Force growth beyond 1MB (MaxRetainedCapacity)
        // 1MB = 1024 * 1024 bytes
        // Allocate 2MB
        arena.Allocate(2 * 1024 * 1024);
        
        Assert.True(arena.Capacity >= 2 * 1024 * 1024);
        
        arena.Reset();
        arena.Trim();
        
        Assert.Equal(1024 * 1024, arena.Capacity);
        Assert.Equal(0, arena.Position);
    }

    [Fact]
    public void Arena_Dispose_FreesMemory()
    {
        var arena = new Arena();
        arena.Allocate(100);
        arena.Dispose();
        
        Assert.Throws<ObjectDisposedException>(() => arena.Allocate(10));
    }

    [Fact]
    public void Arena_TypedAllocate_CorrectSize()
    {
        using var arena = new Arena();
        
        // Allocate array of 10 ints (40 bytes)
        // Aligned to 8 bytes -> 40 is already aligned
        arena.Allocate<int>(10);
        
        Assert.Equal(40, arena.Position);
        
        // Allocate array of 3 bytes
        // 3 bytes -> aligned to 8 bytes
        arena.Allocate<byte>(3);
        
        Assert.Equal(48, arena.Position);
    }

    [Fact]
    public void Arena_MultipleAllocations_Sequential()
    {
        using var arena = new Arena();
        
        var ptr1 = arena.Allocate(8);
        var ptr2 = arena.Allocate(8);
        var ptr3 = arena.Allocate(8);
        
        Assert.Equal(IntPtr.Add(ptr1, 8), ptr2);
        Assert.Equal(IntPtr.Add(ptr2, 8), ptr3);
    }
}
