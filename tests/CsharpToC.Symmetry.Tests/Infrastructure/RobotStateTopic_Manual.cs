using CycloneDDS.Core;
using AtomicTests;
using System;

namespace CsharpToC.Symmetry.Infrastructure
{
    public static class RobotStateTopic_Manual
    {
        public static void Serialize(RobotStateTopic obj, ref CdrWriter writer)
        {
            // DHEADER
            int dheaderPos = 0;
            int bodyStart = 0;
            if (writer.Encoding == CdrEncoding.Xcdr2)
            {
                writer.Align(4);
                dheaderPos = writer.Position;
                writer.WriteUInt32(0);
                bodyStart = writer.Position;
            }

            // RobotId
            writer.Align(4); 
            writer.WriteString(obj.RobotId, writer.IsXcdr2);
            
            // TimestampNs
            if (writer.IsXcdr2) writer.Align(4); else writer.Align(8); 
            writer.WriteUInt64(obj.TimestampNs);
            
            // OperationalMode
            writer.Align(4); 
            writer.WriteInt32((int)obj.OperationalMode);
            
            // TransformMatrix
            if (obj.TransformMatrix.Length > 0)
            {
                 if (writer.IsXcdr2) writer.Align(4); else writer.Align(8);
                 var span = new System.ReadOnlySpan<System.Double>(obj.TransformMatrix);
                 var byteSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(span);
                 writer.WriteBytes(byteSpan);
            }
            
            // CurrentPath
            if (writer.IsXcdr2) {
                 writer.Align(4);
                 // Point2D is 16 bytes: 4 bytes for count + N * 16 bytes for elements
                 uint seqByteLength = (uint)(4 + (obj.CurrentPath.Length * 16));
                 writer.WriteUInt32(seqByteLength);
            }
            writer.Align(4);
            writer.WriteUInt32((uint)obj.CurrentPath.Length);
            for (int i = 0; i < obj.CurrentPath.Length; i++)
            {
                obj.CurrentPath[i].Serialize(ref writer);
            };

            // CurrentAction
            obj.CurrentAction.Serialize(ref writer);

            // CargoHold
            if (obj.CargoHold.HasValue)
            {
                writer.WriteByte(1);
                obj.CargoHold.Value.Serialize(ref writer);
            }
            else
            {
                writer.WriteByte(0); 
            }
            
            // BatteryVoltage
            if (obj.BatteryVoltage.HasValue)
            {
                writer.WriteByte(1); 
                if (writer.IsXcdr2) writer.Align(4); else writer.Align(8); 
                writer.WriteDouble(obj.BatteryVoltage.Value);
            }
            else
            {
                writer.WriteByte(0); 
            }

            // DHeader Fixup
            if (writer.Encoding == CdrEncoding.Xcdr2)
            {
                int currentPos = writer.Position;
                int size = currentPos - bodyStart;
                writer.WriteUInt32At(dheaderPos, (uint)size);
            }
        }

        public static RobotStateTopic Deserialize(ref CdrReader reader)
        {
            System.Console.WriteLine($"DEBUG: RobotStateTopic_Manual Deserialize Pos={reader.Position}");
            var view = new RobotStateTopic();
            int endPos = int.MaxValue;
            if (reader.Encoding == CdrEncoding.Xcdr2)
            {
                reader.Align(4);
                uint dheader = reader.ReadUInt32();
                endPos = reader.Position + (int)dheader;
            }
            
            if (reader.Position < endPos) {
                 reader.Align(4); 
                 view.RobotId = reader.ReadString();
            }

            if (reader.Position < endPos) {
                if (reader.IsXcdr2) reader.Align(4); else reader.Align(8); 
                view.TimestampNs = reader.ReadUInt64();
            }
            
            // Explicitly read OperationalMode
            // if (reader.Position < endPos) {
                System.Console.WriteLine($"DEBUG: Reading OperationalMode at {reader.Position} EndPos={endPos}");
                reader.Align(4); 
                view.OperationalMode = (AtomicTests.SimpleEnum)reader.ReadInt32();
            // }

            // TransformMatrix
            // if (reader.Position < endPos)
            {
                System.Console.WriteLine($"DEBUG: Start TransformMatrix at {reader.Position}");
                int lengthTransformMatrix = 9;
                view.TransformMatrix = new System.Double[lengthTransformMatrix];
                for (int i = 0; i < lengthTransformMatrix; i++)
                {
                    if (reader.IsXcdr2) reader.Align(4); else reader.Align(8);
                    
                    // Peek bytes
                    int p = reader.Position;
                    if (p + 8 <= endPos) {
                        try {
                            // Hacky peek
                           // System.Console.WriteLine($"DEBUG: Double {i} Bytes: {System.BitConverter.ToString(reader.ReadBytes(8))}");
                           // reader.Seek(p);
                        } catch {}
                    }
                    
                    view.TransformMatrix[i] = reader.ReadDouble();
                }
                System.Console.WriteLine($"DEBUG: End TransformMatrix at {reader.Position}");
            }

            // CurrentPath
                if (reader.Position < endPos) {
                if (reader.IsXcdr2) {
                     reader.Align(4);
                     uint seqDHeader = reader.ReadUInt32();
                     System.Console.WriteLine($"DEBUG: Skipped Sequence DHEADER: {seqDHeader}");
                 }
                reader.Align(4);
                int lengthCurrentPath = (int)reader.ReadUInt32();
                view.CurrentPath = new AtomicTests.Point2D[lengthCurrentPath];
                for (int i = 0; i < lengthCurrentPath; i++)
                {
                    view.CurrentPath[i] = AtomicTests.Point2D.Deserialize(ref reader);
                }
            }
            
            if (reader.Position < endPos) {
                view.CurrentAction = AtomicTests.SimpleUnion.Deserialize(ref reader);
            }
            
            // Optional CargoHold
            {
                bool isPresent = false;
                if (reader.Remaining >= 1 && reader.Position + 1 <= endPos)
                {
                    isPresent = reader.ReadBoolean();
                }
                if (isPresent)
                {
                    view.CargoHold = AtomicTests.Container.Deserialize(ref reader);
                }
                else
                {
                    view.CargoHold = null;
                }
            }
            
            // Optional BatteryVoltage
            {
                bool isPresent = false;
                if (reader.Remaining >= 1 && reader.Position + 1 <= endPos)
                {
                    isPresent = reader.ReadBoolean();
                }
                if (isPresent)
                {
                    if (reader.IsXcdr2) reader.Align(4); else reader.Align(8); 
                    view.BatteryVoltage = reader.ReadDouble();
                }
                else
                {
                    view.BatteryVoltage = null;
                }
            }
            
            if (endPos != int.MaxValue && reader.Position < endPos)
            {
                reader.Seek(endPos);
            }
            
            return view;
        }
    }
}
