using CycloneDDS.Core;
using AtomicTests;

namespace CsharpToC.Symmetry.Infrastructure
{
    public static class OptionalEnumTopic_Manual
    {
        public static void Serialize(OptionalEnumTopic obj, ref CdrWriter writer)
        {
            // Struct body
            writer.Align(4); writer.WriteInt32(obj.Id); // Id
            
            // Log the value
            // System.Console.WriteLine($"DEBUG: Serialize OptionalEnumTopic Opt_enum={(int)obj.Opt_enum}");

            if (true)
            {
                writer.WriteByte(1); // Present flag
                writer.Align(4); 
                writer.WriteInt32((int)obj.Opt_enum);
            }
            else
            {
                writer.WriteByte(0); // Not present flag
            }
        }

        public static OptionalEnumTopic Deserialize(ref CdrReader reader)
        {
            System.Console.WriteLine("DEBUG: OptionalEnumTopic_Manual Deserialize Pos=" + reader.Position);
            var view = new OptionalEnumTopic();
            int endPos = int.MaxValue;
            if (reader.Encoding == CdrEncoding.Xcdr2)
            {
                // XCDR2 has no length header for @final structs usually, unless top level?
                // But let's assume standard behavior.
                // Actually the generated code didn't have XCDR2 logic (except generic Reader usage).
            }

            reader.Align(4); view.Id = reader.ReadInt32();
            
            // Optional Opt_enum
            {
                bool isPresent = false;
                if (reader.Remaining >= 1 && reader.Position + 1 <= endPos)
                {
                    isPresent = reader.ReadBoolean();
                }
                
                System.Console.WriteLine($"DEBUG: OptionalEnumTopic_Manual isPresent={isPresent} Pos={reader.Position}");

                if (isPresent)
                {
                    reader.Align(4); // Align before reading int
                    int rawVal = reader.ReadInt32();
                    System.Console.WriteLine($"DEBUG: Read Opt_enum raw={rawVal:X8} ({rawVal})");
                    view.Opt_enum = (AtomicTests.SimpleEnum)rawVal;
                }
                else
                {
                }
            }
            return view;
        }
    }
}
