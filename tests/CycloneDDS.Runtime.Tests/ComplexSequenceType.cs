using System;
using System.Collections.Generic;
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests
{
    [DdsStruct]
    [DdsManaged]
    public partial struct ComplexSequenceType
    {
        public int Id { get; set; }

        public List<AdvancedTypes.ComplexStruct> StructList { get; set; }

        public List<int> IntList { get; set; }
    }
}
