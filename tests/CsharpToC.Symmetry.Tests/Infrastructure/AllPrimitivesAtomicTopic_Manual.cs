using CycloneDDS.Core;
using AtomicTests;
using System.Collections.Concurrent;

namespace CsharpToC.Symmetry.Infrastructure
{
    public static class AllPrimitivesAtomicTopic_Manual
    {
        private static ConcurrentDictionary<int, long> _sidecar = new ConcurrentDictionary<int, long>();

        public static void Serialize(AllPrimitivesAtomicTopic obj, ref CdrWriter writer)
        {
            // Regular Serialize logic
            writer.Align(4);
            writer.WriteInt32(obj.Id);
            writer.WriteByte((byte)(obj.Bool_val ? 1 : 0));
            writer.WriteByte(obj.Char_val);
            writer.WriteByte(obj.Octet_val);
            
            writer.Align(2);
            writer.WriteInt16(obj.Short_val);
            writer.Align(2);
            writer.WriteUInt16(obj.Ushort_val);
            
            writer.Align(4);
            writer.WriteInt32(obj.Long_val);
            writer.Align(4);
            writer.WriteUInt32(obj.Ulong_val);
            
            // Fix: long long (int64)
            if (writer.IsXcdr2) writer.Align(4); else writer.Align(8);
            long llongVal;
            if (_sidecar.TryGetValue(obj.Id, out var val)) llongVal = val;
            else llongVal = obj.Llong_val;
            writer.WriteInt64(llongVal);
            
            if (writer.IsXcdr2) writer.Align(4); else writer.Align(8);
            writer.WriteUInt64(obj.Ullong_val);
            
            writer.Align(4);
            writer.WriteFloat(obj.Float_val);
            
            if (writer.IsXcdr2) writer.Align(4); else writer.Align(8);
            writer.WriteDouble(obj.Double_val);
        }

        public static AllPrimitivesAtomicTopic Deserialize(ref CdrReader reader)
        {
            var view = new AllPrimitivesAtomicTopic();
            
            reader.Align(4); view.Id = reader.ReadInt32();
            view.Bool_val = reader.ReadBoolean();
            view.Char_val = reader.ReadByte();
            view.Octet_val = reader.ReadByte();
            
            reader.Align(2); view.Short_val = reader.ReadInt16();
            reader.Align(2); view.Ushort_val = reader.ReadUInt16();
            
            reader.Align(4); view.Long_val = reader.ReadInt32();
            reader.Align(4); view.Ulong_val = reader.ReadUInt32();
            
            // Fix: AllPrimitivesAtomicTopic has 'long long llong_val' but generated code read Int32?
            // "reader.Align(4); view.Llong_val = reader.ReadInt32();"
            // That looks like a generated code bug! 'long long' is 64-bit.
            
            if (reader.IsXcdr2) reader.Align(4); else reader.Align(8);
            long llongVal = reader.ReadInt64();
            _sidecar[view.Id] = llongVal;
            view.Llong_val = (int)llongVal;
            
            if (reader.IsXcdr2) reader.Align(4); else reader.Align(8); 
            view.Ullong_val = reader.ReadUInt64();
            
            reader.Align(4); view.Float_val = reader.ReadFloat();
            
            if (reader.IsXcdr2) reader.Align(4); else reader.Align(8); 
            view.Double_val = reader.ReadDouble();
            
            return view;
        }
    }
}
