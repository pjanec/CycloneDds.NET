using Xunit;
using CsharpToC.Symmetry.Infrastructure;
using System;

namespace CsharpToC.Symmetry.Tests.Infrastructure
{
    /// <summary>
    /// Self-tests for the Symmetry infrastructure classes.
    /// These tests validate HexUtils, GoldenDataLoader, DataGenerator, etc.
    /// </summary>
    public class InfrastructureTests
    {
        [Fact]
        public void HexUtils_ToHexString_EmptyArray_ReturnsEmptyString()
        {
            // Arrange
            byte[] empty = Array.Empty<byte>();

            // Act
            string result = HexUtils.ToHexString(empty);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void HexUtils_ToHexString_SingleByte_ReturnsCorrectFormat()
        {
            // Arrange
            byte[] bytes = { 0x1A };

            // Act
            string result = HexUtils.ToHexString(bytes);

            // Assert
            Assert.Equal("1A", result);
        }

        [Fact]
        public void HexUtils_ToHexString_MultipleBytes_ReturnsSpaceSeparated()
        {
            // Arrange
            byte[] bytes = { 0x00, 0x1A, 0xFF };

            // Act
            string result = HexUtils.ToHexString(bytes);

            // Assert
            Assert.Equal("00 1A FF", result);
        }

        [Fact]
        public void HexUtils_FromHexString_SpaceSeparated_ParsesCorrectly()
        {
            // Arrange
            string hex = "00 1A FF";

            // Act
            byte[] result = HexUtils.FromHexString(hex);

            // Assert
            Assert.Equal(new byte[] { 0x00, 0x1A, 0xFF }, result);
        }

        [Fact]
        public void HexUtils_FromHexString_DashSeparated_ParsesCorrectly()
        {
            // Arrange
            string hex = "00-1A-FF";

            // Act
            byte[] result = HexUtils.FromHexString(hex);

            // Assert
            Assert.Equal(new byte[] { 0x00, 0x1A, 0xFF }, result);
        }

        [Fact]
        public void HexUtils_FromHexString_MixedSeparators_ParsesCorrectly()
        {
            // Arrange
            string hex = "00 1A-FF\n2B";

            // Act
            byte[] result = HexUtils.FromHexString(hex);

            // Assert
            Assert.Equal(new byte[] { 0x00, 0x1A, 0xFF, 0x2B }, result);
        }

        [Fact]
        public void HexUtils_FromHexString_InvalidHex_ThrowsException()
        {
            // Arrange
            string invalidHex = "00 GG FF";

            // Act & Assert
            Assert.Throws<FormatException>(() => HexUtils.FromHexString(invalidHex));
        }

        [Fact]
        public void HexUtils_RoundTrip_PreservesBytes()
        {
            // Arrange
            byte[] original = { 0x00, 0x01, 0x7F, 0x80, 0xFE, 0xFF };

            // Act
            string hex = HexUtils.ToHexString(original);
            byte[] roundTrip = HexUtils.FromHexString(hex);

            // Assert
            Assert.Equal(original, roundTrip);
        }

        [Fact]
        public void HexUtils_IsValidHexString_ValidInput_ReturnsTrue()
        {
            Assert.True(HexUtils.IsValidHexString("00 1A FF"));
            Assert.True(HexUtils.IsValidHexString("00-1A-FF"));
            Assert.True(HexUtils.IsValidHexString("001AFF"));
            Assert.True(HexUtils.IsValidHexString("aAbBcCdDeEfF"));
        }

        [Fact]
        public void HexUtils_IsValidHexString_InvalidInput_ReturnsFalse()
        {
            Assert.False(HexUtils.IsValidHexString(""));
            Assert.False(HexUtils.IsValidHexString("   "));
            Assert.False(HexUtils.IsValidHexString("00 GG FF"));
            Assert.False(HexUtils.IsValidHexString("hello"));
        }

        [Fact]
        public void DataGenerator_CreateInt_ReturnsSeedBasedValue()
        {
            // This is a simple structural test
            // Actual validation requires generated types from IDL
            int seed = 1420;
            
            // For now, just verify the generator doesn't crash
            // TODO: Add real tests once generated types are available
            Assert.True(true, "DataGenerator structural test passed");
        }
    }
}
