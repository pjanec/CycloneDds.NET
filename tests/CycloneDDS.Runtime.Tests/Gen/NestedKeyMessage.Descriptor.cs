using System;
using CycloneDDS.Runtime;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    public partial struct NestedKeyMessage
    {
        private static readonly uint[] _ops = new uint[] {67108864, 16973829, 0, 17104896, 8, 0, 117440513, 1};

        public static uint[] GetDescriptorOps() => _ops;

        private static readonly DdsKeyDescriptor[] _keys = new DdsKeyDescriptor[]
        {
            new DdsKeyDescriptor { Name = "InnerId", Offset = 6, Index = 0 },
        };
        public static DdsKeyDescriptor[] GetKeyDescriptors() => _keys;
    }
}
