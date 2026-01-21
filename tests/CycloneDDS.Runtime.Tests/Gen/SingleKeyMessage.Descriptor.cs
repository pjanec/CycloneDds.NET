using System;
using CycloneDDS.Runtime;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    public partial struct SingleKeyMessage
    {
        private static readonly uint[] _ops = new uint[] {67108864, 16973829, 0, 16973828, 4, 17039364, 8, 0, 117440513, 1};

        public static uint[] GetDescriptorOps() => _ops;

        private static readonly DdsKeyDescriptor[] _keys = new DdsKeyDescriptor[]
        {
            new DdsKeyDescriptor { Name = "DeviceId", Offset = 8, Index = 0 },
        };
        public static DdsKeyDescriptor[] GetKeyDescriptors() => _keys;
    }
}
