using CycloneDDS.Core;
using AtomicTests;

namespace CsharpToC.Symmetry.Infrastructure
{
    public static class ArrayFloat64TopicAppendable_Manual
    {
        public static void Serialize(ArrayFloat64TopicAppendable obj, ref CdrWriter writer)
        {
            // DHeader: 48 (5 doubles * 8 + 4 id + 4 pad)
            writer.WriteUInt32(48);
            
            writer.Align(4);
            writer.WriteInt32(obj.Id);
            
            // Manual Align 8
            // If Origin=4, needs pad. If Origin=0...? Writer uses Buffer Origin usually 4.
            // Pos 12. Needs 4 bytes pad.
            // writer.Align(8);
            
            // Explicit pad to match layout
            writer.WriteInt32(0);
            
            for(int i=0; i<5; ++i)
            {
                writer.WriteDouble(obj.Values[i]);
            }
        }

        public static ArrayFloat64TopicAppendable Deserialize(ref CdrReader reader)
        {
            var view = new ArrayFloat64TopicAppendable();
            
            uint size = reader.ReadUInt32();
            
            reader.Align(4);
            view.Id = reader.ReadInt32();
            
            // Manual Align 8.
            // Reader Origin=0. Pos 8.
            // Gap is at 8-11. Double starts at 12.
            // Need to skip 4 bytes.
            reader.ReadInt32(); // Skip gap
            
            view.Values = new double[5];
            for(int i=0; i<5; ++i)
            {
                view.Values[i] = reader.ReadDouble();
            }
            
            return view;
        }
    }
}
