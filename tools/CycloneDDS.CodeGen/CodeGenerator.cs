using System;
using System.IO;

namespace CycloneDDS.CodeGen
{
    public class CodeGenerator
    {
        private readonly SchemaDiscovery _discovery = new SchemaDiscovery();
        
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
                Console.WriteLine($"  - {topic.FullName}");
                // Code generation in next batch
                // For now, let's write a dummy file to prove we can write to output
                File.WriteAllText(Path.Combine(outputDir, $"{topic.Name}.txt"), $"Discovered {topic.FullName}");
            }
            
            Console.WriteLine($"Output will go to: {outputDir}");
        }
    }
}
