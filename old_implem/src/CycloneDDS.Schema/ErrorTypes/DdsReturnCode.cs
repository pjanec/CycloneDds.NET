namespace CycloneDDS.Schema;

/// <summary>
/// Status codes returned by the DDS API.
/// </summary>
public enum DdsReturnCode
{
    /// <summary>
    /// Success.
    /// </summary>
    Ok = 0,

    /// <summary>
    /// Unspecified error.
    /// </summary>
    Error = -1,

    /// <summary>
    /// Operation not supported.
    /// </summary>
    Unsupported = -2,

    /// <summary>
    /// Invalid parameter supplied.
    /// </summary>
    BadParameter = -3,

    /// <summary>
    /// Precondition for operation not met.
    /// </summary>
    PreconditionNotMet = -4,

    /// <summary>
    /// System ran out of resources.
    /// </summary>
    OutOfResources = -5,

    /// <summary>
    /// Entity is not enabled.
    /// </summary>
    NotEnabled = -6,

    /// <summary>
    /// Policy is immutable.
    /// </summary>
    ImmutablePolicy = -7,

    /// <summary>
    /// Policy is inconsistent.
    /// </summary>
    InconsistentPolicy = -8,

    /// <summary>
    /// Entity has effectively been deleted.
    /// </summary>
    AlreadyDeleted = -9,

    /// <summary>
    /// Operation timed out.
    /// </summary>
    Timeout = -10,

    /// <summary>
    /// No data available.
    /// </summary>
    NoData = -11,

    /// <summary>
    /// Illegal operation.
    /// </summary>
    IllegalOperation = -12
}
