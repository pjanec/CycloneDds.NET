using System;

namespace CycloneDDS.Runtime;

public enum DdsReturnCode
{
    Ok = 0,
    Error = -1,
    Timeout = -2,
    OutOfResources = -3,
    BadParameter = -4,
    PreconditionNotMet = -5,
    OutOfMemory = -6,
    NotEnabled = -7,
    ImmutablePolicy = -8,
    InconsistentPolicy = -9,
    AlreadyDeleted = -10,
    IllegalOperation = -11,
    NoData = -12,
}

public class DdsException : Exception
{
    public DdsReturnCode Code { get; }
    
    public DdsException(string message, DdsReturnCode code) 
        : base($"{message} (Code: {code})")
    {
        Code = code;
    }
}
