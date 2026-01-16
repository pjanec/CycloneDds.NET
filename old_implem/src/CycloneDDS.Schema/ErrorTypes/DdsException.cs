using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Exception thrown when a DDS operation fails.
/// </summary>
public class DdsException : Exception
{
    /// <summary>
    /// Gets the error code associated with this exception.
    /// </summary>
    public DdsReturnCode ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsException"/> class.
    /// </summary>
    /// <param name="code">The DDS return code.</param>
    /// <param name="message">The exception message.</param>
    public DdsException(DdsReturnCode code, string message)
        : base($"DDS Error {code}: {message}")
    {
        ErrorCode = code;
    }
}
