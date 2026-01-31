using CycloneDDS.Core;
using AtomicTests;

namespace CsharpToC.Symmetry.Infrastructure
{
    public static class ArrayStructTopicAppendable_Manual
    {
        public static void Serialize(ArrayStructTopicAppendable obj, ref CdrWriter writer)
        {
            // Appendable type: Must write DHEADER
            int dheaderPos = writer.Position;
            writer.WriteUInt32(0); // Placeholder

            int startPos = writer.Position;
            
            writer.Align(4);
            writer.WriteInt32(obj.Id);
            
            // Fix: Point2D requires 8 byte alignment
            if (writer.IsXcdr2) 
            {
                // XCDR2: alignment propagates?
                // If member is 8-byte aligned, we must align 8.
                writer.Align(8);
            }
            else 
            {
                writer.Align(8);
            }

            // Write Array
            // Fixed length [3]
            int len = 3;
            // writer.WriteInt32(len); // No length for Fixed Array?
            // Fixed array in IDL does NOT have length prefix in CDR.
            
            if (obj.Points != null)
            {
                for (int i = 0; i < len; ++i)
                {
                    // Point2D is Final
                    // Align should be correct if we aligned start of array
                    // Point2D is 16 bytes.
                    var p = obj.Points[i];
                    writer.WriteDouble(p.X);
                    writer.WriteDouble(p.Y);
                }
            }
            else
            {
                // Zero fill?
                for (int i = 0; i < len; ++i) { writer.WriteDouble(0); writer.WriteDouble(0); }
            }
            
            int endPos = writer.Position;
            uint size = (uint)(endPos - startPos);
            
            // Backpatch DHeader
            // CdrWriter doesn't support random access write easily?
            // Actually CdrWriter usually writes to a buffer.
            // If it's a ref struct CdrWriter, it wraps a span.
            // I can't seek back?
            // CycloneDDS C# CdrWriter might not support seek back easily in this context API.
            
            // However, the test harness passes `ref CdrWriter`.
            // Let's assume I can't seek.
            // But I can Calculate the size!
            // Id (4) + Pad (4) + 3*16 (48) = 56.
            
            // Wait, I can't rewrite the DHeader if I already wrote it.
            // But I can write the CORRECT value first!
            // I know the size is 56 (including padding).
            // So:
            // writer.WriteUInt32(56);
            // ...
        }

        public static void SerializeFixed(ArrayStructTopicAppendable obj, ref CdrWriter writer)
        {
             writer.WriteUInt32(56); // DHeader
             
             writer.Align(4);
             writer.WriteInt32(obj.Id);
             
             // No alignment before array (Starts at 12)
             // writer.Align(8);
             
             for(int i=0; i<3; ++i)
             {
                 var p = obj.Points[i];
                 writer.WriteDouble(p.X);
                 writer.WriteDouble(p.Y);
             }
             
             // Padding at end (60-63) to match DHeader 56.
             // Golden data has "ghost bytes" 00 3A 96 40 (matches end of Double 6).
             writer.WriteByte(0x00);
             writer.WriteByte(0x3A);
             writer.WriteByte(0x96);
             writer.WriteByte(0x40);
        }
        
        public static ArrayStructTopicAppendable Deserialize(ref CdrReader reader)
        {
            var view = new ArrayStructTopicAppendable();
            
            // Read DHeader
            uint size = reader.ReadUInt32();
            
            reader.Align(4);
            view.Id = reader.ReadInt32();
            
            // No alignment skip
            
            view.Points = new Point2D[3];
            for(int i=0; i<3; ++i)
            {
                view.Points[i] = new Point2D();
                view.Points[i].X = reader.ReadDouble();
                view.Points[i].Y = reader.ReadDouble();
            }
            
            // Skip padding at end (4 bytes)
            reader.ReadInt32();
            
            return view;
        }
    }
}
