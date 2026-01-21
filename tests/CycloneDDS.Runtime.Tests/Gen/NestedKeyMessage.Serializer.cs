using CycloneDDS.Core;
using System.Runtime.InteropServices;
using System.Text;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    public partial struct NestedKeyMessage
    {
        public int GetSerializedSize(int currentOffset)
        {
            return GetSerializedSize(currentOffset, false);
        }

        public int GetSerializedSize(int currentOffset, bool isXcdr2 = false)
        {
            var sizer = new CdrSizer(currentOffset);

            // DHEADER
            sizer.Align(4);
            sizer.WriteUInt32(0);

            // Struct body
            sizer.Align(1); sizer.WriteInt32(0); // InnerId
            sizer.Align(4); sizer.WriteString(this.Data, isXcdr2); // Data

            return sizer.GetSizeDelta(currentOffset);
        }

        public void Serialize(ref CdrWriter writer)
        {
            // DHEADER
            writer.Align(4);
            int dheaderPos = writer.Position;
            writer.WriteUInt32(0);
            int bodyStart = writer.Position;
            // Struct body
            writer.Align(1); writer.WriteInt32(this.InnerId); // InnerId
            writer.Align(4); writer.WriteString(this.Data, writer.IsXcdr2); // Data
            int bodyLen = writer.Position - bodyStart;
            writer.WriteUInt32At(dheaderPos, (uint)bodyLen);
        }
    }
}
