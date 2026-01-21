using CycloneDDS.Core;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    public partial struct NestedKeyMessage
    {
        public static NestedKeyMessage Deserialize(ref CdrReader reader)
        {
            var view = new NestedKeyMessage();
            // DHEADER
            reader.Align(4);
            uint dheader = reader.ReadUInt32();
            int endPos = reader.Position + (int)dheader;
            if (reader.Position < endPos)
            {
                view.InnerId = reader.ReadInt32();
            }
            if (reader.Position < endPos)
            {
                reader.Align(4); view.Data = reader.ReadString();
            }

            if (reader.Position < endPos)
            {
                reader.Seek(endPos);
            }
            return view;
        }
        public NestedKeyMessage ToOwned()
        {
            return this;
        }
    }
}
