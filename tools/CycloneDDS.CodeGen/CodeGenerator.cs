using System;
using System.IO;

namespace CycloneDDS.CodeGen
{
    public class CodeGenerator
    {
        private readonly SchemaDiscovery _discovery = new SchemaDiscovery();
        private readonly SchemaValidator _validator = new SchemaValidator();
        private readonly IdlEmitter _idlEmitter = new IdlEmitter();
        
        public void Generate(string sourceDir, string outputDir)
        {
            Console.WriteLine($"Discovering topics in: {sourceDir}");
            var topics = _discovery.DiscoverTopics(sourceDir);
            
            Console.WriteLine($"Found {topics.Count} topic(s)");
            
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            foreach (var topic in topics)
            {
                var validationResult = _validator.Validate(topic);
                if (!validationResult.IsValid)
                {
                    Console.Error.WriteLine($"Validation failed for {topic.FullName}:");
                    foreach (var error in validationResult.Errors)
                    {
                        Console.Error.WriteLine($"  - {error}");
                    }
                    continue; // Skip invalid topics
                }

                Console.WriteLine($"  - {topic.FullName}");
                
                var idl = _idlEmitter.EmitIdl(topic);
                File.WriteAllText(Path.Combine(outputDir, $"{topic.Name}.idl"), idl);
                Console.WriteLine($"    Generated {topic.Name}.idl");

                var serializerEmitter = new SerializerEmitter();
                var serializerCode = serializerEmitter.EmitSerializer(topic);
                File.WriteAllText(Path.Combine(outputDir, $"{topic.Name}.Serializer.cs"), serializerCode);
                Console.WriteLine($"    Generated {topic.Name}.Serializer.cs");
            }
            
            Console.WriteLine($"Output will go to: {outputDir}");
        }
    }
}
