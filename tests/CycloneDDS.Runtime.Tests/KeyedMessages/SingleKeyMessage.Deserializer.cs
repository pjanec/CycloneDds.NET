using CycloneDDS.Core;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    public partial struct SingleKeyMessage
    {
        public static SingleKeyMessage Deserialize(ref CdrReader reader)
        {
            var view = new SingleKeyMessage();
            // DHEADER
            reader.Align(4);
            uint dheader = reader.ReadUInt32();
            int endPos = reader.Position + (int)dheader;
            if (reader.Position < endPos)
            {
                view.DeviceId = reader.ReadInt32();
            }
            if (reader.Position < endPos)
            {
                view.Value = reader.ReadInt32();
            }
            if (reader.Position < endPos)
            {
                view.Timestamp = reader.ReadInt64();
            }

            if (reader.Position < endPos)
            {
                reader.Seek(endPos);
            }
            return view;
        }
        public SingleKeyMessage ToOwned()
        {
            return this;
        }
    }
}
