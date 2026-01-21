using CycloneDDS.Core;
using System.Runtime.InteropServices;
using System.Text;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    public partial struct StringKeyMessage
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
            sizer.Align(4); sizer.WriteString(this.KeyId, isXcdr2); // KeyId
            sizer.Align(4); sizer.WriteString(this.Message, isXcdr2); // Message

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
            writer.Align(4); writer.WriteString(this.KeyId, writer.IsXcdr2); // KeyId
            writer.Align(4); writer.WriteString(this.Message, writer.IsXcdr2); // Message
            int bodyLen = writer.Position - bodyStart;
            writer.WriteUInt32At(dheaderPos, (uint)bodyLen);
        }
    }
}
