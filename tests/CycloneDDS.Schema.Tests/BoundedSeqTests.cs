using System;
using Xunit;
using CycloneDDS.Schema;

namespace CycloneDDS.Schema.Tests;

public class BoundedSeqTests
{
    [Fact]
    public void Constructor_SetsCapacity()
    {
        var seq = new BoundedSeq<int>(10);
        Assert.Equal(10, seq.Capacity);
        Assert.Equal(0, seq.Count);
    }

    [Fact]
    public void Constructor_NegativeCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedSeq<int>(-1));
    }

    [Fact]
    public void Add_WithinCapacity_Works()
    {
        var seq = new BoundedSeq<int>(2);
        seq.Add(1);
        seq.Add(2);
        Assert.Equal(2, seq.Count);
        Assert.Equal(1, seq[0]);
        Assert.Equal(2, seq[1]);
    }

    [Fact]
    public void Add_ExceedsCapacity_Throws()
    {
        var seq = new BoundedSeq<int>(1);
        seq.Add(1);
        Assert.Throws<InvalidOperationException>(() => seq.Add(2));
    }

    [Fact]
    public void Uninitialized_ThrowsOnAdd()
    {
        BoundedSeq<int> seq = default;
        Assert.Throws<InvalidOperationException>(() => seq.Add(1));
    }

    [Fact]
    public void Clear_ResetsCount()
    {
        var seq = new BoundedSeq<int>(5);
        seq.Add(1);
        seq.Clear();
        Assert.Equal(0, seq.Count);
        seq.Add(2); // Can add again
        Assert.Equal(1, seq.Count);
    }

    [Fact]
    public void AsSpan_ReturnsCorrectSpan()
    {
        var seq = new BoundedSeq<int>(5);
        seq.Add(10);
        seq.Add(20);
        var span = seq.AsSpan();
        Assert.Equal(2, span.Length);
        Assert.Equal(10, span[0]);
        Assert.Equal(20, span[1]);
    }

    [Fact]
    public void Copy_SharesStorage()
    {
        var seq1 = new BoundedSeq<int>(5);
        seq1.Add(1);
        
        var seq2 = seq1;
        seq2.Add(2);
        
        Assert.Equal(2, seq1.Count); 
        Assert.Equal(2, seq2.Count);
    }
    
    [Fact]
    public void GetEnumerator_Works()
    {
        var seq = new BoundedSeq<int>(3);
        seq.Add(1);
        seq.Add(2);
        
        int sum = 0;
        foreach (var x in seq)
        {
            sum += x;
        }
        Assert.Equal(3, sum);
    }
}
