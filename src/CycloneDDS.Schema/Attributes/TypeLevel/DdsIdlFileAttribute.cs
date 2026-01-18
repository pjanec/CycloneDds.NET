namespace CycloneDDS.Schema
{
    using System;

    /// <summary>
    /// Specifies the IDL file name this type should be generated into.
    /// If omitted, defaults to the C# source filename.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false)]
    public sealed class DdsIdlFileAttribute : Attribute
    {
        /// <summary>
        /// The name of the IDL file (without extension).
        /// </summary>
        public string FileName { get; }
        
        /// <param name="fileName">IDL file name WITHOUT extension (e.g., "CommonTypes")</param>
        public DdsIdlFileAttribute(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be empty", nameof(fileName));
            
            if (fileName.Contains(".") || fileName.Contains("/") || fileName.Contains("\\"))
                throw new ArgumentException("File name must not contain extension or path separators", nameof(fileName));
                
            FileName = fileName;
        }
    }
}
