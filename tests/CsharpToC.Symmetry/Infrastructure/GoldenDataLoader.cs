using System;
using System.IO;
using CsharpToC.Symmetry.Native;

namespace CsharpToC.Symmetry.Infrastructure
{
    /// <summary>
    /// Manages loading and generation of golden CDR data files.
    /// Golden data represents the "truth" from the native CycloneDDS implementation.
    /// </summary>
    public static class GoldenDataLoader
    {
        private const string GoldenDataFolder = "GoldenData";
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets golden CDR bytes for a topic. If the file doesn't exist, generates it from native DLL.
        /// </summary>
        /// <param name="topicName">Fully qualified topic name (e.g., "AtomicTests::CharTopic")</param>
        /// <param name="seed">Seed value for deterministic data generation</param>
        /// <returns>CDR byte array</returns>
        public static byte[] GetOrGenerate(string topicName, int seed)
        {
            string fileName = GetFileName(topicName);
            
            // Thread-safe check-and-generate
            lock (_lock)
            {
                // Try to load existing file
                if (File.Exists(fileName))
                {
                    return LoadFromFile(fileName);
                }

                // File doesn't exist - generate from native
                Console.WriteLine($"[GoldenData] Generating golden data for {topicName} (seed: {seed})...");
                byte[] goldenBytes = GenerateFromNative(topicName, seed);
                
                // Save for future runs
                SaveToFile(fileName, goldenBytes);
                Console.WriteLine($"[GoldenData] Saved to {Path.GetFileName(fileName)} ({goldenBytes.Length} bytes)");
                
                return goldenBytes;
            }
        }

        /// <summary>
        /// Loads existing golden data from file (does not generate if missing).
        /// </summary>
        public static byte[]? TryLoad(string topicName)
        {
            string fileName = GetFileName(topicName);
            return File.Exists(fileName) ? LoadFromFile(fileName) : null;
        }

        /// <summary>
        /// Deletes the golden data file for a topic, forcing regeneration on next access.
        /// </summary>
        public static void Delete(string topicName)
        {
            string fileName = GetFileName(topicName);
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
        }

        /// <summary>
        /// Deletes all golden data files.
        /// </summary>
        public static void DeleteAll()
        {
            string folderPath = GetFolderPath();
            if (Directory.Exists(folderPath))
            {
                foreach (var file in Directory.GetFiles(folderPath, "*.txt"))
                {
                    File.Delete(file);
                }
            }
        }

        #region Private Helpers

        private static string GetFileName(string topicName)
        {
            // Convert topic name to safe filename: "AtomicTests::CharTopic" -> "AtomicTests_CharTopic.txt"
            string safeName = topicName.Replace("::", "_").Replace(":", "_");
            string folderPath = GetFolderPath();
            
            // Ensure folder exists
            Directory.CreateDirectory(folderPath);
            
            return Path.Combine(folderPath, $"{safeName}.txt");
        }

        private static string GetFolderPath()
        {
            // Use base directory (bin/Debug/net8.0/) and navigate to GoldenData folder
            return Path.Combine(AppContext.BaseDirectory, GoldenDataFolder);
        }

        private static byte[] LoadFromFile(string fileName)
        {
            try
            {
                string hexContent = File.ReadAllText(fileName);
                return HexUtils.FromHexString(hexContent);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to load golden data from {Path.GetFileName(fileName)}: {ex.Message}", ex);
            }
        }

        private static void SaveToFile(string fileName, byte[] bytes)
        {
            try
            {
                string hexContent = HexUtils.ToHexString(bytes);
                File.WriteAllText(fileName, hexContent);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to save golden data to {Path.GetFileName(fileName)}: {ex.Message}", ex);
            }
        }

        private static byte[] GenerateFromNative(string topicName, int seed)
        {
            try
            {
                return NativeWrapper.GeneratePayload(topicName, seed);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to generate golden data for {topicName} using native DLL. " +
                    $"Ensure ddsc_test_lib.dll is present and the topic name is correct.", ex);
            }
        }

        #endregion
    }
}
