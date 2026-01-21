using System;
using CycloneDDS.Runtime;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    public partial struct KeyLastMessage
    {
        private static readonly uint[] _ops = new uint[] {67108864, 17104896, 0, 16973829, 8, 0, 117440513, 3};

        public static uint[] GetDescriptorOps() => _ops;

        private static readonly DdsKeyDescriptor[] _keys = new DdsKeyDescriptor[]
        {
            new DdsKeyDescriptor { Name = "Id", Offset = 6, Index = 0 },
        };
        public static DdsKeyDescriptor[] GetKeyDescriptors() => _keys;
    }
}
