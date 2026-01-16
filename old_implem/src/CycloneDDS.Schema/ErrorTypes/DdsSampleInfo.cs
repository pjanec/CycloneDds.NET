using System.Runtime.InteropServices;

namespace CycloneDDS.Schema;

/// <summary>
/// Stub - will be completed in FCDC-015.
/// Contains information accompanying a data sample.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct DdsSampleInfo
{
    /// <summary>
    /// Indicates whether the sample contains valid data.
    /// </summary>
    public bool ValidData;
}
