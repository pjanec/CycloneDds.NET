using System;
using System.Linq;

namespace CsharpToC.Symmetry.Infrastructure
{
    /// <summary>
    /// Utility class for converting between byte arrays and human-readable hex strings.
    /// Used for storing and loading golden CDR data in text format.
    /// </summary>
    public static class HexUtils
    {
        /// <summary>
        /// Converts a byte array to a space-separated hex string.
        /// </summary>
        /// <param name="bytes">The byte array to convert</param>
        /// <returns>Hex string like "00 1A FF 2B"</returns>
        public static string ToHexString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;
            
            return BitConverter.ToString(bytes).Replace("-", " ");
        }

        /// <summary>
        /// Parses a hex string back to a byte array.
        /// Supports multiple separators: space, dash, newline, carriage return.
        /// </summary>
        /// <param name="hex">Hex string like "00 1A FF" or "00-1A-FF"</param>
        /// <returns>Byte array</returns>
        public static byte[] FromHexString(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return Array.Empty<byte>();

            try
            {
                return hex.Split(new[] { ' ', '-', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => Convert.ToByte(s.Trim(), 16))
                          .ToArray();
            }
            catch (FormatException ex)
            {
                throw new FormatException($"Invalid hex string: {hex}", ex);
            }
        }

        /// <summary>
        /// Validates that a string contains valid hex characters.
        /// </summary>
        public static bool IsValidHexString(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return false;

            var cleaned = hex.Replace(" ", "").Replace("-", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");
            return cleaned.All(c => "0123456789ABCDEFabcdef".Contains(c));
        }
    }
}
