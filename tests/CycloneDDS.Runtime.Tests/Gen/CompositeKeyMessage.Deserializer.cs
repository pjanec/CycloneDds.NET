using CycloneDDS.Core;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    public partial struct CompositeKeyMessage
    {
        public static CompositeKeyMessage Deserialize(ref CdrReader reader)
        {
            var view = new CompositeKeyMessage();
            // DHEADER
            reader.Align(4);
            uint dheader = reader.ReadUInt32();
            int endPos = reader.Position + (int)dheader;
            if (reader.Position < endPos)
            {
                view.SensorId = reader.ReadInt32();
            }
            if (reader.Position < endPos)
            {
                view.LocationId = reader.ReadInt32();
            }
            if (reader.Position < endPos)
            {
                view.Temperature = reader.ReadDouble();
            }

            if (reader.Position < endPos)
            {
                reader.Seek(endPos);
            }
            return view;
        }
        public CompositeKeyMessage ToOwned()
        {
            return this;
        }
    }
}
