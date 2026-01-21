using System;
using CycloneDDS.Runtime;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    public partial struct StringKeyMessage
    {
        private static readonly uint[] _ops = new uint[] {67108864, 17104897, 0, 17104896, 8, 0, 117440513, 1};

        public static uint[] GetDescriptorOps() => _ops;

        private static readonly DdsKeyDescriptor[] _keys = new DdsKeyDescriptor[]
        {
            new DdsKeyDescriptor { Name = "KeyId", Offset = 6, Index = 0 },
        };
        public static DdsKeyDescriptor[] GetKeyDescriptors() => _keys;
    }
}
