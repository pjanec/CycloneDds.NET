using System;
using System.Text;
using System.Collections.Generic;
using CycloneDDS.CodeGen;
using CycloneDDS.CodeGen.Emitters;
using Xunit;

namespace CycloneDDS.CodeGen.Tests
{
    public class ViewComplexTypesTests
    {
        private ViewEmitter _emitter;

        public ViewComplexTypesTests()
        {
            _emitter = new ViewEmitter();
        }

        [Fact]
        public void EmitPrimitiveSequenceProperty_GeneratesSpanAccessor()
        {
            // Simulate: sequence<double> values;
            var type = new TypeInfo { Name = "TestType", Namespace = "Space" };
            type.Fields.Add(new FieldInfo 
            { 
                Name = "values", 
                TypeName = "double[]", 
                Type = null 
            });

            var registry = new GlobalTypeRegistry();
            var code = _emitter.EmitViewStruct(type, registry);

            Assert.Contains("public unsafe ReadOnlySpan<double> Values", code);
            Assert.Contains("return new ReadOnlySpan<double>", code);
            Assert.Contains("(void*)_ptr->values.Buffer", code);
            Assert.Contains("(int)_ptr->values.Length", code);
        }

        [Fact]
        public void EmitStructSequenceProperty_GeneratesCountAndIndexer()
        {
            // Simulate: sequence<Point3D> points;
            var pointType = new TypeInfo { Name = "Point3D", IsStruct = true };
            var type = new TypeInfo { Name = "TestType" };
            
            type.Fields.Add(new FieldInfo 
            { 
                Name = "points", 
                TypeName = "Point3D[]", 
                GenericType = pointType
            });

            var registry = new GlobalTypeRegistry();
            registry.RegisterLocal(pointType, "test.cs", "test.idl", "test");
            
            var code = _emitter.EmitViewStruct(type, registry);

            Assert.Contains("public int PointsCount", code);
            Assert.Contains("_ptr->points.Length", code);
            Assert.Contains("public unsafe Point3DView GetPoints(int index)", code);
            Assert.Contains("Point3D_Native* arr = (Point3D_Native*)_ptr->points.Buffer", code);
            Assert.Contains("return new Point3DView(&arr[index])", code);
        }

        [Fact]
        public void EmitStringSequenceProperty_GeneratesCountAndBothIndexers()
        {
            // Simulate: sequence<string> messages;
            var type = new TypeInfo { Name = "TestType" };
            type.Fields.Add(new FieldInfo 
            { 
                Name = "messages", 
                TypeName = "string[]"
            });

            var registry = new GlobalTypeRegistry();
            var code = _emitter.EmitViewStruct(type, registry);

            Assert.Contains("public int MessagesCount", code);
            Assert.Contains("public unsafe ReadOnlySpan<byte> GetMessagesRaw(int index)", code);
            Assert.Contains("public unsafe string? GetMessages(int index)", code);
        }
        
        [Fact]
        public void EmitUnionProperty_GeneratesDiscriminatorAndAccessors()
        {
            var unionType = new TypeInfo { Name = "MyUnion", IsUnion = true };
            unionType.Fields.Add(new FieldInfo { Name = "int_val", TypeName = "int" });
            unionType.Fields.Add(new FieldInfo { Name = "dbl_val", TypeName = "double" });

            var type = new TypeInfo { Name = "TestType" };
            type.Fields.Add(new FieldInfo 
            { 
                Name = "myU", 
                TypeName = "MyUnion", 
                Type = unionType 
            });

            var registry = new GlobalTypeRegistry();
            // registry.Add(unionType); // Mock registry logic not fully implemented in test context, reliant on type.Fields being populated
            
            // In ViewEmitter, it checks type.Fields.Type property first.
            var code = _emitter.EmitViewStruct(type, registry);

            Assert.Contains("public MyUnionDiscriminator MyUKind", code);
            Assert.Contains("public int? MyUAsInt_val", code);
            Assert.Contains("public double? MyUAsDbl_val", code);
        }
    }
}

