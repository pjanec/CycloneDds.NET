using System;
using System.Collections.Generic;
using System.IO;
using CycloneDDS.Schema;
using Xunit;

namespace CycloneDDS.CodeGen.Tests
{
    /// <summary>
    /// [DdsId(N)] must reach the IDL as @id(N): member ids are on the wire for mutable types, and the
    /// IDL importer emits [DdsId] for every IDL @id.
    /// </summary>
    public class IdlMemberIdTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "CG_ID_" + Guid.NewGuid().ToString("N"));

        public IdlMemberIdTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        private static FieldInfo Field(string name, string type, int? id = null)
        {
            var f = new FieldInfo { Name = name, TypeName = type };
            if (id.HasValue) f.Attributes.Add(new AttributeInfo { Name = "DdsId", Arguments = new List<object> { id.Value } });
            return f;
        }

        [Fact]
        public void StructAndUnionMembers_CarryTheirIds()
        {
            var registry = new GlobalTypeRegistry();
            var st = new TypeInfo
            {
                Name = "M", Namespace = "Ids", IsStruct = true, Extensibility = DdsExtensibilityKind.Mutable,
                Fields = { Field("a", "int", 5), Field("b", "double", 10), Field("c", "int") }
            };
            var disc = new FieldInfo { Name = "_d", TypeName = "int" };
            disc.Attributes.Add(new AttributeInfo { Name = "DdsDiscriminator" });
            var arm = Field("x", "int", 7);
            arm.Attributes.Add(new AttributeInfo { Name = "DdsCase", Arguments = new List<object> { 1 } });
            var un = new TypeInfo { Name = "U", Namespace = "Ids", IsUnion = true, Fields = { disc, arm } };
            registry.RegisterLocal(st, "src.cs", "Ids", "Ids");
            registry.RegisterLocal(un, "src.cs", "Ids", "Ids");

            new IdlEmitter().EmitIdlFiles(registry, _dir);
            var idl = File.ReadAllText(Path.Combine(_dir, "Ids.idl"));

            Assert.Contains("@id(5) int32 a;", idl);
            Assert.Contains("@id(10) double b;", idl);
            Assert.Contains(" int32 c;", idl);
            Assert.DoesNotContain("@id(0)", idl);
            Assert.Contains("@id(7) int32 x;", idl);
        }
    }
}
