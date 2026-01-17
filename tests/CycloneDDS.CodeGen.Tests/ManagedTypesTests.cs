using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Buffers;
using System.Collections;
using System.Diagnostics;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Core;
using CycloneDDS.Schema;
using Xunit.Abstractions;

namespace CycloneDDS.CodeGen.Tests
{
    public class ManagedTypesTests : CodeGenTestBase
    {
        private readonly ITestOutputHelper _output;

        public ManagedTypesTests(ITestOutputHelper output)
        {
            _output = output;
        }

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

        [Fact]
        public void ManagedString_Null_RoundTrip()
        {
            var type = new TypeInfo
            {
                Name = "ManagedStringStruct",
                Namespace = "TestManaged",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Text", TypeName = "string", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } }
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
            
            var assembly = CompileToAssembly(code, "ManagedStringNullAssembly");
            
            var instance = Instantiate(assembly, "TestManaged.ManagedStringStruct");
            SetField(instance, "Text", null);
            
            var helperType = assembly.GetType("TestManaged.TestHelper");
            var buffer = new ArrayBufferWriter<byte>();
            helperType.GetMethod("Serialize").Invoke(null, new object[] { instance, buffer });
            
            var result = helperType.GetMethod("Deserialize").Invoke(null, new object[] { buffer.WrittenMemory });
            var resultText = (string)GetField(result, "Text");
            
            Assert.Equal(string.Empty, resultText);
        }

        [Fact]
        public void ManagedList_Empty_RoundTrip()
        {
            var type = new TypeInfo
            {
                Name = "ManagedListStruct",
                Namespace = "TestManaged",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Numbers", TypeName = "List<int>", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } }
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

            var assembly = CompileToAssembly(code, "ManagedListEmptyAssembly");
            
            var instance = Instantiate(assembly, "TestManaged.ManagedListStruct");
            SetField(instance, "Numbers", new List<int>());
            
            var helperType = assembly.GetType("TestManaged.TestHelper");
            var buffer = new ArrayBufferWriter<byte>();
            helperType.GetMethod("Serialize").Invoke(null, new object[] { instance, buffer });
            
            var result = helperType.GetMethod("Deserialize").Invoke(null, new object[] { buffer.WrittenMemory });
            var resultNumbers = (List<int>)GetField(result, "Numbers");
            
            Assert.NotNull(resultNumbers);
            Assert.Empty(resultNumbers);
        }

        [Fact]
        public void ManagedList_Large_PerformanceTest()
        {
            var type = new TypeInfo
            {
                Name = "ManagedListStruct",
                Namespace = "TestManaged",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Numbers", TypeName = "List<int>", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } }
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

            var assembly = CompileToAssembly(code, "ManagedListLargeAssembly");
            
            var instance = Instantiate(assembly, "TestManaged.ManagedListStruct");
            var largeList = Enumerable.Range(0, 10000).ToList();
            SetField(instance, "Numbers", largeList);
            
            var helperType = assembly.GetType("TestManaged.TestHelper");
            var serializeMethod = helperType.GetMethod("Serialize");
            var deserializeMethod = helperType.GetMethod("Deserialize");
            var buffer = new ArrayBufferWriter<byte>();

            var sw = Stopwatch.StartNew();
            serializeMethod.Invoke(null, new object[] { instance, buffer });
            var serializeMs = sw.ElapsedMilliseconds;
            
            sw.Restart();
            var result = deserializeMethod.Invoke(null, new object[] { buffer.WrittenMemory });
            var deserializeMs = sw.ElapsedMilliseconds;
            
            _output.WriteLine($"Large List (10k ints): Serialize {serializeMs}ms, Deserialize {deserializeMs}ms");
            
            var resultNumbers = (List<int>)GetField(result, "Numbers");
            Assert.Equal(10000, resultNumbers.Count);
        }

        [Fact]
        public void ManagedList_Strings_RoundTrip()
        {
            var type = new TypeInfo
            {
                Name = "StringListStruct",
                Namespace = "TestManaged",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Messages", TypeName = "List<string>", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } }
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
    public partial struct StringListStruct
    {
        [DdsManaged]
        public List<string> Messages;
    }
    
    public static class TestHelper
    {
        public static void Serialize(object instance, IBufferWriter<byte> buffer)
        {
            var typed = (StringListStruct)instance;
            var writer = new CycloneDDS.Core.CdrWriter(buffer);
            typed.Serialize(ref writer);
            writer.Complete();
        }

        public static object Deserialize(ReadOnlyMemory<byte> buffer)
        {
            var reader = new CycloneDDS.Core.CdrReader(buffer.Span);
            var view = StringListStruct.Deserialize(ref reader);
            return view.ToOwned();
        }
    }
}";
             string code = @"using CycloneDDS.Core;
using System;
using System.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Buffers;
using CycloneDDS.Schema;
" + serializerCode + "\n" + deserializerCode + "\n" + structDef;
            
            var assembly = CompileToAssembly(code, "ManagedStringListAssembly");
            
            var instance = Instantiate(assembly, "TestManaged.StringListStruct");
            var strings = new List<string> { "Alpha", "Beta", "Gamma", "Delta" };
            SetField(instance, "Messages", strings);
            
            var helperType = assembly.GetType("TestManaged.TestHelper");
            var buffer = new ArrayBufferWriter<byte>();
            helperType.GetMethod("Serialize").Invoke(null, new object[] { instance, buffer });
            
            var result = helperType.GetMethod("Deserialize").Invoke(null, new object[] { buffer.WrittenMemory });
            var resultMessages = (List<string>)GetField(result, "Messages");
            
            Assert.Equal(4, resultMessages.Count);
            Assert.Equal("Alpha", resultMessages[0]);
            Assert.Equal("Delta", resultMessages[3]);
        }

        [Fact]
        public void MixedManagedUnmanaged_RoundTrip()
        {
            var type = new TypeInfo
            {
                Name = "MixedStruct",
                Namespace = "TestManaged",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Id", TypeName = "int" },
                    new FieldInfo 
                    { 
                        Name = "Name", 
                        TypeName = "string",
                        Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } 
                    },
                    new FieldInfo { Name = "Numbers", TypeName = "BoundedSeq<int>" },
                    new FieldInfo 
                    { 
                        Name = "Tags", 
                        TypeName = "List<string>",
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
    public partial struct MixedStruct
    {
        public int Id;
        [DdsManaged]
        public string Name;
        public BoundedSeq<int> Numbers;
        [DdsManaged]
        public List<string> Tags;
    }
    
    public static class TestHelper {
        public static void Serialize(object instance, IBufferWriter<byte> buffer) {
             var typed = (MixedStruct)instance;
             var writer = new CycloneDDS.Core.CdrWriter(buffer);
             typed.Serialize(ref writer);
             writer.Complete();
        }
        public static object Deserialize(ReadOnlyMemory<byte> buffer) {
             var reader = new CycloneDDS.Core.CdrReader(buffer.Span);
             var view = MixedStruct.Deserialize(ref reader);
             return view.ToOwned();
        }
    }
}";
            string code = @"using CycloneDDS.Core;
using System;
using System.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Buffers;
using CycloneDDS.Schema;
" + serializerCode + "\n" + deserializerCode + "\n" + structDef;

            var assembly = CompileToAssembly(code, "MixedStructAssembly");

            var instance = Instantiate(assembly, "TestManaged.MixedStruct");
            SetField(instance, "Id", 999);
            SetField(instance, "Name", "Mixed");
            
            var boundedType = typeof(BoundedSeq<int>);
            var bounded = Activator.CreateInstance(boundedType, new object[] { 5 }); 
            var addMethod = boundedType.GetMethod("Add");
            addMethod.Invoke(bounded, new object[] { 1 });
            addMethod.Invoke(bounded, new object[] { 2 });
            addMethod.Invoke(bounded, new object[] { 3 });
            
            SetField(instance, "Numbers", bounded);
            SetField(instance, "Tags", new List<string> { "test", "managed" });
            
            var helperType = assembly.GetType("TestManaged.TestHelper");
            var buffer = new ArrayBufferWriter<byte>();
            helperType.GetMethod("Serialize").Invoke(null, new object[] { instance, buffer });
            
            var result = helperType.GetMethod("Deserialize").Invoke(null, new object[] { buffer.WrittenMemory });
            
            Assert.Equal(999, GetField(result, "Id"));
            Assert.Equal("Mixed", GetField(result, "Name"));
            
            var resNumbers = GetField(result, "Numbers");
            var countProp = resNumbers.GetType().GetProperty("Count");
            Assert.Equal(3, countProp.GetValue(resNumbers));
            
            var resTags = (List<string>)GetField(result, "Tags");
            Assert.Equal(2, resTags.Count);
        }

        [Fact]
        public void UnmarkedManagedType_FailsValidation()
        {
            var type = new TypeInfo
            {
                Name = "UnmarkedStruct",
                Namespace = "TestManaged",
                // NO [DdsManaged] attribute
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Text", TypeName = "string" }  // Managed type, but no attribute
                }
            };
            
            var validator = new ManagedTypeValidator();
            var diagnostics = validator.Validate(type);
            
            Assert.NotEmpty(diagnostics);
            Assert.Contains(diagnostics, d => d.Severity == ValidationSeverity.Error);
            Assert.Contains(diagnostics, d => d.Message.Contains("[DdsManaged]"));
            Assert.Contains(diagnostics, d => d.Message.Contains("Text"));
        }
    }
}
