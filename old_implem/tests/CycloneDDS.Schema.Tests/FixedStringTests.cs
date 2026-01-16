using System;
using System.Text;
using Xunit;
using CycloneDDS.Schema;

namespace CycloneDDS.Schema.Tests;

public class FixedStringTests
{
    [Fact]
    public void FixedString32_ValidString_Works()
    {
        var fs = new FixedString32("Hello");
        Assert.Equal(5, fs.Length);
        Assert.Equal("Hello", fs.ToStringAllocated());
    }

    [Fact]
    public void FixedString32_MaxString_Works()
    {
        string maxStr = new string('a', 32);
        var fs = new FixedString32(maxStr);
        Assert.Equal(32, fs.Length);
        Assert.Equal(maxStr, fs.ToStringAllocated());
    }

    [Fact]
    public void FixedString32_TooLong_Throws()
    {
        string longStr = new string('a', 33);
        Assert.Throws<ArgumentException>(() => new FixedString32(longStr));
        
        Assert.False(FixedString32.TryFrom(longStr, out _));
    }

    [Fact]
    public void FixedString32_InvalidUtf8_Throws()
    {
        // Construct illegal UTF-8 sequence
        // 0xFF cannot appear in UTF-8
         // Wait, string in C# is UTF-16.
         // If I have a string with surrogate pair split?
         string invalid = "\uD800"; // Lone surrogate
         // Encoding.UTF8.GetByteCount will generally allow this via replacement IF not strict.
         // But we used strict encoding.
         
         // StrictUtf8.GetByteCount throws ArgumentException or EncoderFallbackException on lone surrogate if Fallback is Exception.
         
         Assert.Throws<ArgumentException>(() => new FixedString32(invalid));
         Assert.False(FixedString32.TryFrom(invalid, out _));
    }

    [Fact]
    public void FixedString32_AsUtf8Span_CorrectContent()
    {
        var fs = new FixedString32("ABC");
        var span = fs.AsUtf8Span();
        Assert.Equal(3, span.Length);
        Assert.Equal((byte)'A', span[0]);
        Assert.Equal((byte)'B', span[1]);
        Assert.Equal((byte)'C', span[2]);
    }

    [Fact]
    public void FixedString64_Works()
    {
        string str = new string('x', 64);
        var fs = new FixedString64(str);
        Assert.Equal(64, fs.Length);
        Assert.Equal(str, fs.ToStringAllocated());
        
        Assert.False(FixedString64.TryFrom(new string('x', 65), out _));
    }

    [Fact]
    public void FixedString128_Works()
    {
        string str = new string('x', 128);
        var fs = new FixedString128(str);
        Assert.Equal(128, fs.Length);
        Assert.Equal(str, fs.ToStringAllocated());
        
        Assert.False(FixedString128.TryFrom(new string('x', 129), out _));
    }

    [Fact]
    public void FixedString32_EmptyString_Length0()
    {
        var fs = new FixedString32("");
        Assert.Equal(0, fs.Length);
        Assert.Equal("", fs.ToStringAllocated());
    }
    
    [Fact]
    public void FixedString32_Default_Length0()
    {
        FixedString32 fs = default;
        Assert.Equal(0, fs.Length);
        Assert.Equal("", fs.ToStringAllocated());
    }

    [Fact]
    public void FixedString32_Null_TreatsAsEmpty()
    {
        Assert.True(FixedString32.TryFrom(null, out var fs));
        Assert.Equal(0, fs.Length);
    }
    [Fact]
    public void FixedString32_MultiByteAtBoundary_Rejects()
    {
        // 30 ASCII chars (30 bytes) + ü (2 bytes) = 32 bytes total (fits)
        string validAt32 = new string('a', 30) + "ü";
        Assert.True(FixedString32.TryFrom(validAt32, out var fs));
        Assert.Equal(32, fs.Length);
        
        // 30 ASCII chars (30 bytes) + € (3 bytes) = 33 bytes total (exceeds)
        string invalidAt33 = new string('a', 30) + "€";
        Assert.False(FixedString32.TryFrom(invalidAt33, out _));
        
        // 29 ASCII + 3-byte char (e.g. €) = 32 bytes exactly
        string euroAt32 = new string('a', 29) + "€";
        Assert.True(FixedString32.TryFrom(euroAt32, out var fs2));
        Assert.Equal(32, fs2.Length);
        Assert.Equal(euroAt32, fs2.ToStringAllocated());
        
        // 28 ASCII + 4-byte emoji = 32 bytes exactly
        string emojiAt32 = new string('a', 28) + "😀";
        Assert.True(FixedString32.TryFrom(emojiAt32, out var fs3));
        Assert.Equal(32, fs3.Length);
        Assert.Equal(emojiAt32, fs3.ToStringAllocated());
    }

    [Fact]
    public void FixedString_CapacityConstants_Correct()
    {
        Assert.Equal(32, FixedString32.Capacity);
        Assert.Equal(64, FixedString64.Capacity);
        Assert.Equal(128, FixedString128.Capacity);
    }
}
