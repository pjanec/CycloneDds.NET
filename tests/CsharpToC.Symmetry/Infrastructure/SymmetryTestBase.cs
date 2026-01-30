using System;
using Xunit;
using CycloneDDS.Core;

namespace CsharpToC.Symmetry.Infrastructure
{
    public delegate T DeserializeDelegate<T>(ref CdrReader reader);
    public delegate void SerializeDelegate<T>(T obj, ref CdrWriter writer);

    /// <summary>
    /// Abstract base class for symmetry tests.
    /// Provides the core verification logic for serialize/deserialize symmetry.
    /// </summary>
    public abstract class SymmetryTestBase
    {
        /// <summary>
        /// Verifies symmetry: Golden CDR -> Deserialize -> Serialize -> Compare with Golden
        /// </summary>
        /// <typeparam name="T">The topic type</typeparam>
        /// <param name="topicName">Fully qualified topic name</param>
        /// <param name="seed">Seed for data generation</param>
        /// <param name="deserializer">Deserialization function</param>
        /// <param name="serializer">Serialization action</param>
        /// <param name="encoding">CDR encoding (auto-detected if null)</param>
        protected void VerifySymmetry<T>(
            string topicName,
            int seed,
            DeserializeDelegate<T> deserializer,
            SerializeDelegate<T> serializer,
            CdrEncoding? encoding = null)
        {
            // Phase 1: Load Golden Data
            byte[] goldenBytes;
            try
            {
                goldenBytes = GoldenDataLoader.GetOrGenerate(topicName, seed);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"[{topicName}] Failed to load or generate golden data", ex);
            }

            Assert.NotEmpty(goldenBytes); // Sanity check

            // Phase 2: Detect Encoding (if not specified)
            CdrEncoding actualEncoding = encoding ?? DetectEncoding(goldenBytes);

            // Phase 3: Deserialize Golden Data
            T obj;
            try
            {
                var reader = new CdrReader(goldenBytes, actualEncoding);
                
                // HEADER HANDLING: 
                // Golden data includes 4-byte encapsulation header (via dds_takecdr).
                // CdrReader does NOT skip header when initialized with explicit encoding.
                // We must manually skip it.
                if (reader.Remaining >= 4)
                {
                    reader.ReadInt32(); // Consume header
                }

                // NOTE: reader is a ref struct, passed by reference.
                obj = deserializer(ref reader);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"[{topicName}] Deserialization failed. Golden data may be corrupt or deserializer has bugs.\n" +
                    $"Golden bytes: {HexUtils.ToHexString(goldenBytes)}", ex);
            }

            Assert.NotNull(obj); // Sanity check

            // Phase 4: Serialize Back
            byte[] serializedBytes;
            try
            {
                // Allocate buffer (2x golden size to detect overruns)
                byte[] buffer = new byte[goldenBytes.Length * 2];
                var writer = new CdrWriter(buffer, actualEncoding);
                
                // HEADER HANDLING:
                // CdrWriter does NOT automatically write the header.
                // We must write it manually to match the golden data.
                // We simply copy the 4 bytes from golden data.
                if (goldenBytes.Length >= 4)
                {
                    writer.WriteBytes(goldenBytes.AsSpan(0, 4));
                }

                serializer(obj, ref writer);
                
                // Native CycloneDDS implementation appears to pad top-level topics to 4 bytes.
                // To detect symmetry correctly, we must match this padding.
                writer.Align(4);
                
                // Extract actual written bytes
                serializedBytes = new byte[writer.Position];
                Array.Copy(buffer, 0, serializedBytes, 0, writer.Position);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"[{topicName}] Serialization failed.\n" +
                    $"Golden bytes: {HexUtils.ToHexString(goldenBytes)}", ex);
            }

            // Phase 5: Compare Bytes (The Golden Check)
            AssertBytesEqual(topicName, goldenBytes, serializedBytes);
        }

        /// <summary>
        /// Detects CDR encoding from the encapsulation header (byte 1).
        /// </summary>
        private static CdrEncoding DetectEncoding(byte[] bytes)
        {
            if (bytes.Length < 2)
                throw new ArgumentException("CDR data too short to detect encoding");

            // Byte 0: 0x00 or 0x01 (endianness)
            // Byte 1: Encoding identifier
            //   0x00 = XCDR1
            //   0x02 = XCDR2 (PL_CDR2_LE for @appendable)
            //   0x06-0x0A = XCDR2 variants

            byte encodingId = bytes[1];
            
            return encodingId switch
            {
                0x00 => CdrEncoding.Xcdr1,
                0x01 => CdrEncoding.Xcdr1, 
                0x02 => CdrEncoding.Xcdr2,
                >= 0x06 and <= 0x0A => CdrEncoding.Xcdr2,
                _ => throw new NotSupportedException($"Unknown CDR encoding identifier: 0x{encodingId:X2}")
            };
        }

        /// <summary>
        /// Asserts that two byte arrays are identical, with detailed error message on mismatch.
        /// </summary>
        private static void AssertBytesEqual(string topicName, byte[] expected, byte[] actual)
        {
            // Check length first
            if (expected.Length != actual.Length)
            {
                string expectedHex = HexUtils.ToHexString(expected);
                string actualHex = HexUtils.ToHexString(actual);
                
                Assert.Fail(
                    $"[{topicName}] Length Mismatch!\n" +
                    $"Expected Length: {expected.Length} bytes\n" +
                    $"Actual Length:   {actual.Length} bytes\n" +
                    $"Expected Bytes:  {expectedHex}\n" +
                    $"Actual Bytes:    {actualHex}");
            }

            // Check content byte-by-byte
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                {
                    // Find range of differences for context
                    int diffStart = Math.Max(0, i - 4);
                    int diffEnd = Math.Min(expected.Length, i + 5);
                    
                    string expectedContext = HexUtils.ToHexString(expected[diffStart..diffEnd]);
                    string actualContext = HexUtils.ToHexString(actual[diffStart..diffEnd]);
                    string expectedFull = HexUtils.ToHexString(expected);
                    string actualFull = HexUtils.ToHexString(actual);
                    
                    Assert.Fail(
                        $"[{topicName}] Byte Mismatch at offset {i}!\n" +
                        $"Expected[{i}]: 0x{expected[i]:X2}\n" +
                        $"Actual[{i}]:   0x{actual[i]:X2}\n" +
                        $"\nContext (bytes {diffStart}-{diffEnd-1}):\n" +
                        $"Expected: {expectedContext}\n" +
                        $"Actual:   {actualContext}\n" +
                        $"\nFull Comparison:\n" +
                        $"Expected: {expectedFull}\n" +
                        $"Actual:   {actualFull}");
                }
            }

            // All bytes match
            Assert.True(true, $"[{topicName}] Symmetry verified successfully ({expected.Length} bytes)");
        }
    }
}
