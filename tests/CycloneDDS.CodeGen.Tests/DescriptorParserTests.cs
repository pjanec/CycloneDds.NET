using System;
using System.IO;
using Xunit;
using CycloneDDS.CodeGen;

namespace CycloneDDS.CodeGen.Tests
{
    public class DescriptorParserTests : IDisposable
    {
        private readonly string _tempDir;

        public DescriptorParserTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, true); } catch { }
            }
        }

        private string CreateTempFile(string name, string content)
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void ParseDescriptor_ExtractsOpsArray()
        {
            string cCode = @"
#include <stdint.h>
static const uint32_t TestData_ops[] = {
    0x40000004,  // DDS_OP_ADR | DDS_OP_TYPE_4BY
    0x00000000,  // offset
    0x00000001   // DDS_OP_RTS
};
";
            var file = CreateTempFile("test.c", cCode);
            var parser = new DescriptorParser();
            var metadata = parser.ParseDescriptor(file);

            Assert.Equal("TestData_ops", metadata.OpsArrayName);
            Assert.Equal("TestData", metadata.TypeName);
            Assert.Equal(3, metadata.OpsValues.Length);
            Assert.Equal(0x40000004u, metadata.OpsValues[0]);
            Assert.Equal(0u, metadata.OpsValues[1]);
            Assert.Equal(1u, metadata.OpsValues[2]);
        }

        [Fact]
        public void ParseDescriptor_HandlesMacros()
        {
            // Note: CppAst might not expand macros if headers are missing, but our parser handles raw macro names
            string cCode = @"
static const uint32_t MacroData_ops[] = {
    DDS_OP_ADR | DDS_OP_TYPE_4BY,
    0,
    DDS_OP_RTS
};
";
            var file = CreateTempFile("macro.c", cCode);
            var parser = new DescriptorParser();
            var metadata = parser.ParseDescriptor(file);

            Assert.Equal("MacroData_ops", metadata.OpsArrayName);
            // DDS_OP_ADR (0x01<<24) | DDS_OP_TYPE_4BY (0x03<<16) = 0x01030000
            // Wait, my OpConstants:
            // DDS_OP_ADR = 0x01000000
            // DDS_OP_TYPE_4BY = 0x00030000
            // Result = 0x01030000
            
            uint expected = (0x01u << 24) | (0x03u << 16);
            Assert.Equal(expected, metadata.OpsValues[0]);
        }

        [Fact]
        public void ParseDescriptor_CalculatesOffsetOf()
        {
            // Simulate:
            // int32 a; (4 bytes)
            // int32 b; (4 bytes)
            // offsetof(T, b) should be 4
            
            string cCode = @"
static const uint32_t OffsetData_ops[] = {
    DDS_OP_ADR | DDS_OP_TYPE_4BY, offsetof(T, a),
    DDS_OP_ADR | DDS_OP_TYPE_4BY, offsetof(T, b),
    DDS_OP_RTS
};
";
            var file = CreateTempFile("offset.c", cCode);
            var parser = new DescriptorParser();
            var metadata = parser.ParseDescriptor(file);

            // Item 0: ADR | 4BY
            // Item 1: offsetof(a) -> currentOffset is 0. pendingSize=4, pendingAlign=4.
            // After Item 1: currentOffset = 0 + 4 = 4.
            
            // Item 2: ADR | 4BY
            // Item 3: offsetof(b) -> currentOffset is 4. pendingSize=4, pendingAlign=4.
            // After Item 3: currentOffset = 4 + 4 = 8.

            Assert.Equal(0u, metadata.OpsValues[1]); // offset of a
            Assert.Equal(4u, metadata.OpsValues[3]); // offset of b
        }

        [Fact]
        public void ParseDescriptor_ExtractsKeys()
        {
            string cCode = @"
static const uint32_t KeyData_keys[] = {
    1, 2, 3
};
";
            var file = CreateTempFile("keys.c", cCode);
            var parser = new DescriptorParser();
            var metadata = parser.ParseDescriptor(file);

            Assert.Equal("KeyData_keys", metadata.KeysArrayName);
            Assert.Equal(3, metadata.KeysValues.Length);
            Assert.Equal(1u, metadata.KeysValues[0]);
        }
        
        [Fact]
        public void ParseDescriptor_HandlesAlignment()
        {
            // byte a; (1 byte)
            // int32 b; (4 bytes, align 4)
            // offsetof(b) should be 4 (padding 3 bytes)
            
            string cCode = @"
static const uint32_t AlignData_ops[] = {
    DDS_OP_ADR | DDS_OP_TYPE_1BY, offsetof(T, a),
    DDS_OP_ADR | DDS_OP_TYPE_4BY, offsetof(T, b),
    DDS_OP_RTS
};
";
            var file = CreateTempFile("align.c", cCode);
            var parser = new DescriptorParser();
            var metadata = parser.ParseDescriptor(file);

            // Item 0: ADR | 1BY
            // Item 1: offsetof(a) -> 0. pendingSize=1, pendingAlign=1.
            // After Item 1: currentOffset = 1.
            
            // Item 2: ADR | 4BY -> pendingSize=4, pendingAlign=4.
            // Item 3: offsetof(b).
            //   pendingAlign=4. currentOffset=1.
            //   mask=3. (1 & 3) != 0.
            //   currentOffset = (1 + 3) & ~3 = 4.
            //   Value added: 4.
            
            Assert.Equal(0u, metadata.OpsValues[1]);
            Assert.Equal(4u, metadata.OpsValues[3]);
        }
    }
}

