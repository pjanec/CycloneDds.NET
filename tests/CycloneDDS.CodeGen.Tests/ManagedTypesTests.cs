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
            var serializerCode = serializerEmitter.EmitSerializer(type, new GlobalTypeRegistry(), false);
            
            var deserializerEmitter = new DeserializerEmitter();
            var deserializerCode = deserializerEmitter.EmitDeserializer(type, new GlobalTypeRegistry(), false);
            
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

            var assembly = CompileToAssembly("ManagedStringAssembly", code);
            
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
            var serializerCode = serializerEmitter.EmitSerializer(type, new GlobalTypeRegistry(), false);
            
            var deserializerEmitter = new DeserializerEmitter();
            var deserializerCode = deserializerEmitter.EmitDeserializer(type, new GlobalTypeRegistry(), false);

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

            var assembly = CompileToAssembly("ManagedListAssembly", code);
            
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
            var serializerCode = serializerEmitter.EmitSerializer(type, new GlobalTypeRegistry(), false);
            var deserializerEmitter = new DeserializerEmitter();
            var deserializerCode = deserializerEmitter.EmitDeserializer(type, new GlobalTypeRegistry(), false);

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
            
            var assembly = CompileToAssembly("ManagedStringNullAssembly", code);
            
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
            var serializerCode = serializerEmitter.EmitSerializer(type, new GlobalTypeRegistry(), false);
            var deserializerEmitter = new DeserializerEmitter();
            var deserializerCode = deserializerEmitter.EmitDeserializer(type, new GlobalTypeRegistry(), false);

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

            var assembly = CompileToAssembly("ManagedListEmptyAssembly", code);
            
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
                Extensibility = CycloneDDS.Schema.DdsExtensibilityKind.Final,
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Numbers", TypeName = "List<int>", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } }
                }
            };
            
            var serializerEmitter = new SerializerEmitter();
            var serializerCode = serializerEmitter.EmitSerializer(type, new GlobalTypeRegistry(), false);
            var deserializerEmitter = new DeserializerEmitter();
            var deserializerCode = deserializerEmitter.EmitDeserializer(type, new GlobalTypeRegistry(), false);

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

            var assembly = CompileToAssembly("ManagedListLargeAssembly", code);
            
            var instance = Instantiate(assembly, "TestManaged.ManagedListStruct");
            var largeList = Enumerable.Range(0, 10000).ToList();
            SetField(instance, "Numbers", largeList);
            
            var helperType = assembly.GetType("TestManaged.TestHelper");
            var serializeMethod = helperType.GetMethod("Serialize");
            var deserializeMethod = helperType.GetMethod("Deserialize");
            var buffer = new ArrayBufferWriter<byte>();

            // Warmup JIT
            for(int i=0; i<5; i++)
            {
                buffer.Clear();
                serializeMethod.Invoke(null, new object[] { instance, buffer });
            }

            buffer.Clear();
            var sw = Stopwatch.StartNew();
            serializeMethod.Invoke(null, new object[] { instance, buffer });
            var serializeMs = sw.ElapsedMilliseconds;
            
            _output.WriteLine($"Large List (10k ints): Serialize {serializeMs}ms");
            
            // Should be very fast now (< 50ms implies optimization works)
            Assert.True(serializeMs < 100, $"Serialization took too long: {serializeMs}ms");

            var result = deserializeMethod.Invoke(null, new object[] { buffer.WrittenMemory });
            
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
            var serializerCode = serializerEmitter.EmitSerializer(type, new GlobalTypeRegistry(), false);
            var deserializerEmitter = new DeserializerEmitter();
            var deserializerCode = deserializerEmitter.EmitDeserializer(type, new GlobalTypeRegistry(), false);

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
            
            var assembly = CompileToAssembly("ManagedStringListAssembly", code);
            
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
            var serializerCode = serializerEmitter.EmitSerializer(type, new GlobalTypeRegistry(), false);
            var deserializerEmitter = new DeserializerEmitter();
            var deserializerCode = deserializerEmitter.EmitDeserializer(type, new GlobalTypeRegistry(), false);

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

            var assembly = CompileToAssembly("MixedStructAssembly", code);

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

        [Fact]
        public void TypeManaged_StringField_NoFieldAttribute_Validates()
        {
            var type = new TypeInfo
            {
                Name = "LogEvent",
                Namespace = "TestManaged",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Message", TypeName = "string" }  // No field attribute
                }
            };
            
            var validator = new ManagedTypeValidator();
            var diagnostics = validator.Validate(type);
            
            Assert.Empty(diagnostics); 
        }

        [Fact]
        public void TypeManaged_GeneratedCode_Compiles()
        {
            var type = new TypeInfo
            {
                Name = "CompileLog",
                Namespace = "TestManaged",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Message", TypeName = "string" },
                    new FieldInfo { Name = "Names", TypeName = "List<string>" }
                }
            };
            
            var serializerEmitter = new SerializerEmitter();
            var serializerCode = serializerEmitter.EmitSerializer(type, new GlobalTypeRegistry(), false);
            var deserializerEmitter = new DeserializerEmitter();
            var deserializerCode = deserializerEmitter.EmitDeserializer(type, new GlobalTypeRegistry(), false);
            
            string structDef = @"
namespace TestManaged
{
    [DdsManaged]
    public partial struct CompileLog
    {
        public string Message;
        public List<string> Names;
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

             var assembly = CompileToAssembly("CompileLogAssembly", code);
             Assert.NotNull(assembly);
        }

        [Fact]
        public void TypeManaged_Roundtrip_Preserves()
        {
            var type = new TypeInfo
            {
                Name = "RoundtripLog",
                Namespace = "TestManaged",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Message", TypeName = "string" },
                    new FieldInfo { Name = "Tags", TypeName = "List<string>" }
                }
            };
            
            var serializerEmitter = new SerializerEmitter();
            var serializerCode = serializerEmitter.EmitSerializer(type, new GlobalTypeRegistry(), false);
            
            var deserializerEmitter = new DeserializerEmitter();
            var deserializerCode = deserializerEmitter.EmitDeserializer(type, new GlobalTypeRegistry(), false);
            
            string structDef = @"
namespace TestManaged
{
    [DdsManaged]
    public partial struct RoundtripLog
    {
        public string Message;
        public List<string> Tags;
    }
    
    public static class RTLogger
    {
         public static void Serialize(object instance, IBufferWriter<byte> buffer)
        {
            var typed = (RoundtripLog)instance;
            var writer = new CycloneDDS.Core.CdrWriter(buffer);
            typed.Serialize(ref writer);
            writer.Complete();
        }

        public static object Deserialize(ReadOnlyMemory<byte> buffer)
        {
            var reader = new CycloneDDS.Core.CdrReader(buffer.Span);
            var view = RoundtripLog.Deserialize(ref reader);
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

           var assembly = CompileToAssembly("RoundtripLogAssembly", code);
           
           var logType = assembly.GetType("TestManaged.RoundtripLog");
           var instance = Activator.CreateInstance(logType);
           SetField(instance, "Message", "Valid Message");
           SetField(instance, "Tags", new List<string> { "Tag1", "Tag2" });
           
           var helper = assembly.GetType("TestManaged.RTLogger");
           var buffer = new ArrayBufferWriter<byte>();
           helper.GetMethod("Serialize").Invoke(null, new object[] { instance, buffer });
           
           var bytes = buffer.WrittenMemory;
           var result = helper.GetMethod("Deserialize").Invoke(null, new object[] { bytes });
           
           Assert.Equal("Valid Message", GetField(result, "Message"));
           var tags = (List<string>)GetField(result, "Tags");
           Assert.Equal(2, tags.Count);
           Assert.Equal("Tag1", tags[0]);
        }
    }
}

