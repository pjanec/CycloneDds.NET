using CycloneDDS.Core;
using System.Runtime.InteropServices;
using System.Text;

namespace CycloneDDS.Runtime.Tests
{
    public partial struct TestMessage
    {
        public int GetSerializedSize(int currentOffset)
        {
            var sizer = new CdrSizer(currentOffset);

            // Struct body
            sizer.Align(4); sizer.WriteInt32(0); // Id
            sizer.Align(4); sizer.WriteInt32(0); // Value

            return sizer.GetSizeDelta(currentOffset);
        }

        public void Serialize(ref CdrWriter writer)
        {
            // Struct body
            writer.Align(4); writer.WriteInt32(this.Id); // Id
            writer.Align(4); writer.WriteInt32(this.Value); // Value
        }
    }
}
