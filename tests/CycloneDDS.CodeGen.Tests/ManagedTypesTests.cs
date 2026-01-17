using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Buffers;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Core;

namespace CycloneDDS.CodeGen.Tests
{
    public class ManagedTypesTests : CodeGenTestBase
    {
        [Fact]
        public void ManagedString_RoundTrip()
        {
            var type = new TypeInfo
            {
                Name = "ManagedStringStruct",
                Namespace = "TestManaged",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo 
                    { 
                        Name = "Text", 
                        TypeName = "string",
                        Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } 
                    }
                }
            };
            
            var serializerEmitter = new SerializerEmitter();
            var serializerCode = serializerEmitter.EmitSerializer(type, false);
            
            var deserializerEmitter = new DeserializerEmitter();
            var deserializerCode = deserializerEmitter.EmitDeserializer(type, false);
            
            string structDef = @"
namespace TestManaged
{
    [DdsManaged]
    public partial struct ManagedStringStruct
    {
        [DdsManaged]
        public string Text;
    }

    public static class TestHelper
    {
        public static void Serialize(object instance, IBufferWriter<byte> buffer)
        {
            var typed = (ManagedStringStruct)instance;
            var writer = new CycloneDDS.Core.CdrWriter(buffer);
            typed.Serialize(ref writer);
            writer.Complete();
        }

        public static object Deserialize(ReadOnlyMemory<byte> buffer)
        {
            var reader = new CycloneDDS.Core.CdrReader(buffer.Span);
            var view = ManagedStringStruct.Deserialize(ref reader);
            return view.ToOwned();
        }
    }
}
";

            string code = @"using CycloneDDS.Core;
using System;
using System.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Buffers;
using CycloneDDS.Schema;
" + serializerCode + "\n" + deserializerCode + "\n" + structDef;

            var assembly = CompileToAssembly(code, "ManagedStringAssembly");
            
            // Instantiate
            var instance = Instantiate(assembly, "TestManaged.ManagedStringStruct");
            SetField(instance, "Text", "Hello World");
            
            // Serialize
            var buffer = new ArrayBufferWriter<byte>();
            var helperType = assembly.GetType("TestManaged.TestHelper");
            helperType.GetMethod("Serialize").Invoke(null, new object[] { instance, buffer });
            
            // Deserialize
            var result = helperType.GetMethod("Deserialize").Invoke(null, new object[] { buffer.WrittenMemory });
            var resultText = GetField(result, "Text");
            
            Assert.Equal("Hello World", resultText);
        }

        [Fact]
        public void ManagedList_RoundTrip()
        {
             // Test List<int>
             var type = new TypeInfo
            {
                Name = "ManagedListStruct",
                Namespace = "TestManaged",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo 
                    { 
                        Name = "Numbers", 
                        TypeName = "List<int>",
                        Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } 
                    }
                }
            };
            
            var serializerEmitter = new SerializerEmitter();
            var serializerCode = serializerEmitter.EmitSerializer(type, false);
            
            var deserializerEmitter = new DeserializerEmitter();
            var deserializerCode = deserializerEmitter.EmitDeserializer(type, false);

            string structDef = @"
namespace TestManaged
{
    [DdsManaged]
    public partial struct ManagedListStruct
    {
        [DdsManaged]
        public List<int> Numbers;
    }

    public static class TestHelper
    {
        public static void Serialize(object instance, IBufferWriter<byte> buffer)
        {
            var typed = (ManagedListStruct)instance;
            var writer = new CycloneDDS.Core.CdrWriter(buffer);
            typed.Serialize(ref writer);
            writer.Complete();
        }

        public static object Deserialize(ReadOnlyMemory<byte> buffer)
        {
            var reader = new CycloneDDS.Core.CdrReader(buffer.Span);
            var view = ManagedListStruct.Deserialize(ref reader);
            return view.ToOwned();
        }
    }
}
";
            string code = @"using CycloneDDS.Core;
using System;
using System.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Buffers;
using CycloneDDS.Schema;
" + serializerCode + "\n" + deserializerCode + "\n" + structDef;

            var assembly = CompileToAssembly(code, "ManagedListAssembly");
            
            var instance = Instantiate(assembly, "TestManaged.ManagedListStruct");
            var numbers = new List<int> { 1, 2, 3, 4, 5 };
            SetField(instance, "Numbers", numbers);
            
            var buffer = new ArrayBufferWriter<byte>();
            var helperType = assembly.GetType("TestManaged.TestHelper");
            helperType.GetMethod("Serialize").Invoke(null, new object[] { instance, buffer });
            
            var result = helperType.GetMethod("Deserialize").Invoke(null, new object[] { buffer.WrittenMemory });
            var resultNumbers = (List<int>)GetField(result, "Numbers");
            
            Assert.Equal(numbers, resultNumbers);
        }
    }
}
