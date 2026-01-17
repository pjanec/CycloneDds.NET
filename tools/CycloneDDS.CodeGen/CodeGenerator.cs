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

            // Managed Type Validation
            var managedValidator = new ManagedTypeValidator();
            var allDiagnostics = new List<ValidationMessage>();

            foreach (var topic in topics)
            {
                var validationErrors = managedValidator.Validate(topic);
                allDiagnostics.AddRange(validationErrors);
            }

            if (allDiagnostics.Any(d => d.Severity == ValidationSeverity.Error))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                foreach (var diagnostic in allDiagnostics.Where(d => d.Severity == ValidationSeverity.Error))
                {
                    Console.WriteLine($"ERROR: {diagnostic.Message}");
                }
                Console.ResetColor();
                Console.WriteLine("Generation failed due to validation errors.");
                return;
            }
            
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
                string idlPath = Path.Combine(outputDir, $"{topic.Name}.idl");
                File.WriteAllText(idlPath, idl);
                Console.WriteLine($"    Generated {topic.Name}.idl");

                // --- Descriptor Generation ---
                try
                {
                    var idlcRunner = new IdlcRunner();
                    // Try to find idlc relative to workspace if not found
                    // Assuming we are in tools/CycloneDDS.CodeGen/bin/Debug/net8.0
                    // And cyclonedds-bin is in root/cyclone-bin/Release
                    // But simpler to rely on IdlcRunner logic, maybe set env var if needed?
                    // For now, let's try to detect if we can find it.
                    // Or explicitly set it if we know where we are.
                    
                    // Actually, let's just run it. If it fails, we catch exception.
                    // We need a temp dir for C output to avoid polluting Gen folder? 
                    // Or just use Gen folder and delete .c/.h files?
                    string tempCGroup = Path.Combine(outputDir, "temp_c");
                    if (!Directory.Exists(tempCGroup)) Directory.CreateDirectory(tempCGroup);

                    var result = idlcRunner.RunIdlc(idlPath, tempCGroup);
                    if (result.ExitCode != 0)
                    {
                         Console.Error.WriteLine($"    idlc failed: {result.StandardError}");
                    }
                    else
                    {
                        // Parse C file
                        string cFile = Path.Combine(tempCGroup, $"{topic.Name}.c");
                        if (File.Exists(cFile))
                        {
                            var parser = new DescriptorParser();
                            var metadata = parser.ParseDescriptor(cFile);
                            
                            // Generate Descriptor Code
                            var descCode = GenerateDescriptorCode(topic, metadata);
                             File.WriteAllText(Path.Combine(outputDir, $"{topic.Name}.Descriptor.cs"), descCode);
                             Console.WriteLine($"    Generated {topic.Name}.Descriptor.cs");
                        }
                        else
                        {
                            Console.Error.WriteLine($"    Could not find generated C file: {cFile}");
                        }
                    }
                }
                catch (Exception ex)
                {
                     Console.Error.WriteLine($"    Descriptor generation failed: {ex.Message}");
                     // Don't fail the whole build for now, but warn
                }
                // -----------------------------

                var serializerEmitter = new SerializerEmitter();
                var serializerCode = serializerEmitter.EmitSerializer(topic);
                File.WriteAllText(Path.Combine(outputDir, $"{topic.Name}.Serializer.cs"), serializerCode);
                Console.WriteLine($"    Generated {topic.Name}.Serializer.cs");

                var deserializerEmitter = new DeserializerEmitter();
                var deserializerCode = deserializerEmitter.EmitDeserializer(topic);
                File.WriteAllText(Path.Combine(outputDir, $"{topic.Name}.Deserializer.cs"), deserializerCode);
                Console.WriteLine($"    Generated {topic.Name}.Deserializer.cs");
            }
            
            Console.WriteLine($"Output will go to: {outputDir}");
        }

        private string GenerateDescriptorCode(TypeInfo topic, DescriptorMetadata metadata)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using CycloneDDS.Runtime;"); // Assuming generated code usage
            sb.AppendLine();
            sb.AppendLine($"namespace {topic.Namespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial struct {topic.Name}");
            sb.AppendLine("    {");
            
            // Ops
            sb.Append("        private static readonly uint[] _ops = new uint[] {");
            if (metadata.OpsValues != null && metadata.OpsValues.Length > 0)
            {
                 sb.Append(string.Join(", ", metadata.OpsValues));
            }
            sb.AppendLine("};");

            sb.AppendLine();
            sb.AppendLine("        public static uint[] GetDescriptorOps() => _ops;");
            
            // Add IDL string for reference if needed?
            // sb.AppendLine($"        public const string Idl = @\"{topic.Idl}\";");
            
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString(); 
        }
    }
}
