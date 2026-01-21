using CycloneDDS.Core;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    public partial struct StringKeyMessage
    {
        public static StringKeyMessage Deserialize(ref CdrReader reader)
        {
            var view = new StringKeyMessage();
            // DHEADER
            reader.Align(4);
            uint dheader = reader.ReadUInt32();
            int endPos = reader.Position + (int)dheader;
            if (reader.Position < endPos)
            {
                reader.Align(4); view.KeyId = reader.ReadString();
            }
            if (reader.Position < endPos)
            {
                reader.Align(4); view.Message = reader.ReadString();
            }

            if (reader.Position < endPos)
            {
                reader.Seek(endPos);
            }
            return view;
        }
        public StringKeyMessage ToOwned()
        {
            return this;
        }
    }
}
