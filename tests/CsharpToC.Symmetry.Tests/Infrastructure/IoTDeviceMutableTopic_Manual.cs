using System;
using CycloneDDS.Core;
using CycloneDDS.Schema;

namespace AtomicTests
{
    public partial struct IoTDeviceMutableTopic
    {
        public int GetSerializedSize(int currentOffset)
        {
            return GetSerializedSize(currentOffset, CdrEncoding.Xcdr1);
        }

        public int GetSerializedSize(int currentOffset, CdrEncoding encoding)
        {
             // Rough estimate
             return 96;
        }

        public void Serialize(ref CdrWriter writer)
        {
            if (writer.Encoding == CdrEncoding.Xcdr2)
            {
                writer.Align(4);
                writer.WriteUInt32(88); // DHEADER (Fixed 88)
            }

            // DeviceSerial (10)
            writer.Align(4);
            writer.WriteByte(10); writer.WriteByte(0); writer.WriteByte(0); writer.WriteByte(0xA0); 
            writer.WriteInt32(this.DeviceSerial);
            
            // Temperature (50)
            writer.WriteByte(50); writer.WriteByte(0); writer.WriteByte(0); writer.WriteByte(0x20); 
            writer.WriteFloat(this.Temperature);

            // LocationLabel (60)
            if (this.LocationLabel != null)
            {
                writer.WriteByte(60); writer.WriteByte(0); writer.WriteByte(0); writer.WriteByte(0x50); 
                writer.WriteString(this.LocationLabel);
            }
            writer.Align(4);

            // StatusLeds (70)
            if (this.StatusLeds != null && this.StatusLeds.Length > 0)
            {
                writer.WriteByte(70); writer.WriteByte(0); writer.WriteByte(0); writer.WriteByte(0x50); 
                writer.WriteInt32(this.StatusLeds.Length * 4); // Length in bytes
                foreach(var x in this.StatusLeds) writer.WriteInt32(x);
            }
            writer.Align(4);

            // LastPingGeo (80)
            {
                writer.WriteByte(80); writer.WriteByte(0); writer.WriteByte(0); writer.WriteByte(0x40); 
                writer.WriteInt32(24); // Length 24
                this.LastPingGeo.Serialize(ref writer);
            }
        }

        public static IoTDeviceMutableTopic Deserialize(ref CdrReader reader)
        {
            var view = new IoTDeviceMutableTopic();

            int endPos = int.MaxValue;
            if (reader.Encoding == CdrEncoding.Xcdr2)
            {
                reader.Align(4);
                uint dheader = reader.ReadUInt32(); 
                endPos = reader.Position + (int)dheader;
            }
            
            while (true)
            {
                if (endPos != int.MaxValue && reader.Position >= endPos) break;
                if (reader.Remaining < 4) break;

                reader.Align(4); 
                
                int headerPos = reader.Position;
                byte idByte = reader.ReadByte();
                reader.Seek(headerPos + 4); 
                
                if (idByte == 10)
                {
                    view.DeviceSerial = reader.ReadInt32();
                }
                else if (idByte == 50)
                {
                    view.Temperature = reader.ReadFloat();
                }
                else if (idByte == 60)
                {
                    view.LocationLabel = reader.ReadString();
                }
                else if (idByte == 70)
                {
                     int len_or_bytes = reader.ReadInt32();
                     int count = len_or_bytes / 4; 
                     view.StatusLeds = new int[count];
                     for(int i=0; i<count; i++) view.StatusLeds[i] = reader.ReadInt32();
                }
                else if (idByte == 80)
                {
                     // Explicit length consumption for 0x40/0x50 types if not handled by Deserialize
                     reader.ReadInt32(); // Consume Length (24)
                     view.LastPingGeo = AtomicTests.Point3D.Deserialize(ref reader);
                }
                else if (idByte == 1)
                {
                     break;
                }
                else 
                {
                    break; 
                }
            }
            return view;
        }
    }
}
