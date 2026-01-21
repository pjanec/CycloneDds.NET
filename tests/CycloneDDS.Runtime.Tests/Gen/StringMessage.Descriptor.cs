using System;
using CycloneDDS.Runtime;

namespace CycloneDDS.Runtime.Tests
{
    public partial struct StringMessage
    {
        private static readonly uint[] _ops = new uint[] {67108864, 16973828, 0, 17104896, 8, 0};

        public static uint[] GetDescriptorOps() => _ops;
        public static DdsKeyDescriptor[] GetKeyDescriptors() => null;
    }
}
