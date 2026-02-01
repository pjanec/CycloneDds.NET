// Manually fixed
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CycloneDDS.Schema;

namespace Probe
{
    [DdsUnion]
    public partial struct MyUnion
    {
        [DdsDiscriminator]
        public bool _d;

        [DdsCase(true)]
        public byte Vala;

        [DdsCase(false)]
        public byte Valb;
    }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct UnionProbeStruct
    {
        public int P1;
        public Probe.MyUnion U;
        public int P2;
    }
}
