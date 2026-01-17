using System;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime
{
    public class DdsException : Exception
    {
        public DdsApi.DdsReturnCode ReturnCode { get; }

        public DdsException(DdsApi.DdsReturnCode code, string message) 
            : base($"{message} (ReturnCode: {code})")
        {
            ReturnCode = code;
        }

        public static void Check(DdsApi.DdsReturnCode code, string operation)
        {
            if (code != DdsApi.DdsReturnCode.Ok)
            {
                throw new DdsException(code, $"DDS operation '{operation}' failed");
            }
        }
    }
}
