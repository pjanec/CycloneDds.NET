using System;
using CycloneDDS.Runtime;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    public partial struct MixedKeyMessage
    {
        private static readonly uint[] _ops = new uint[] {67108864, 16973829, 0, 17104897, 8, 17104896, 16, 0, 117440513, 1, 117440513, 3};

        public static uint[] GetDescriptorOps() => _ops;

        private static readonly DdsKeyDescriptor[] _keys = new DdsKeyDescriptor[]
        {
            new DdsKeyDescriptor { Name = "Id", Offset = 8, Index = 0 },
            new DdsKeyDescriptor { Name = "Name", Offset = 10, Index = 1 },
        };
        public static DdsKeyDescriptor[] GetKeyDescriptors() => _keys;
    }
}
