using System.Collections.Generic;
using Xunit;
using CycloneDDS.CodeGen;

namespace CycloneDDS.CodeGen.Tests
{
    public class SchemaValidatorTests
    {
        private SchemaValidator CreateValidator(params TypeInfo[] types)
        {
            return new SchemaValidator(types);
        }

        [Fact]
        public void ValidStruct_WithPrimitives_Passes()
        {
            var type = new TypeInfo
            {
                Name = "TestStruct",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "F1", TypeName = "int" },
                    new FieldInfo { Name = "F2", TypeName = "double" }
                }
            };
            
            var validator = CreateValidator(type);
            var result = validator.Validate(type);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validation_UnknownStruct_EmitsError()
        {
            var type = new TypeInfo
            {
                Name = "TestStruct",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "F1", TypeName = "UnknownType" }
                }
            };

            var validator = CreateValidator(type);
            var result = validator.Validate(type);

            Assert.False(result.IsValid);
            Assert.Contains("uses type 'UnknownType'", result.Errors[0]);
            Assert.Contains("forget to add [DdsStruct]", result.Errors[0]);
        }

        [Fact]
        public void Validation_KnownStruct_Passes()
        {
             var helper = new TypeInfo { Name = "Helper", IsStruct = true };
             var main = new TypeInfo 
             { 
                 Name = "Main", 
                 Fields = new List<FieldInfo>
                 {
                     new FieldInfo { Name = "H", TypeName = "Helper", Type = helper }
                 }
             };
             
             var validator = CreateValidator(helper, main);
             var result = validator.Validate(main);
             
             Assert.True(result.IsValid);
        }

        [Fact]
        public void Validation_NestedSequence_UnknownType_EmitsError()
        {
            var type = new TypeInfo
            {
                Name = "TestStruct",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Seq", TypeName = "BoundedSeq<Unknown>" }
                }
            };

            var validator = CreateValidator(type);
            var result = validator.Validate(type);

            Assert.False(result.IsValid);
            Assert.Contains("uses collection of type 'Unknown'", result.Errors[0]);
        }

        [Fact]
        public void Validation_NestedSequence_KnownType_Passes()
        {
            var helper = new TypeInfo { Name = "Helper", IsStruct = true };
            var type = new TypeInfo
            {
                Name = "TestStruct",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Seq", TypeName = "BoundedSeq<Helper>" }
                }
            };

            var validator = CreateValidator(helper, type);
            var result = validator.Validate(type);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void CircularDependency_Detected()
        {
            var typeA = new TypeInfo { Name = "A", Namespace = "Test" };
            var typeB = new TypeInfo { Name = "B", Namespace = "Test" };

            typeA.Fields.Add(new FieldInfo { Name = "FieldB", TypeName = "Test.B", Type = typeB });
            typeB.Fields.Add(new FieldInfo { Name = "FieldA", TypeName = "Test.A", Type = typeA });

            var validator = CreateValidator(typeA, typeB);
            var result = validator.Validate(typeA);

            Assert.False(result.IsValid);
            Assert.Contains("Circular dependency", result.Errors[0]);
        }
        
        [Fact]
        public void Union_WithoutDiscriminator_Fails()
        {
            var type = new TypeInfo
            {
                Name = "TestUnion",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsUnion" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Case1", TypeName = "int" }
                }
            };

            var validator = CreateValidator(type);
            var result = validator.Validate(type);

            Assert.False(result.IsValid);
            Assert.Contains("must have exactly one [DdsDiscriminator]", result.Errors[0]);
        }
    }
}

