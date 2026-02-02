using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CycloneDDS.CodeGen;
using CycloneDDS.Core; using CycloneDDS.Runtime; using System.Runtime.CompilerServices;
using System.Buffers;

namespace CycloneDDS.CodeGen.Tests
{
    public class CodeGenTestBase
    {
        protected Assembly CompileToAssembly(string assemblyName, params string[] sources)
        {
            var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();
            
            // Gather references
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CycloneDDS.Core.NativeArena).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CycloneDDS.Runtime.DdsParticipant).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CycloneDDS.Schema.BoundedSeq<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IBufferWriter<>).Assembly.Location), // System.Memory
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location)
            };

            var compilation = CSharpCompilation.Create(assemblyName)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true))
                .AddReferences(references)
                .AddSyntaxTrees(trees);

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic => 
                    diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                
                var errorMsg = string.Join("\n", failures.Select(d => $"{d.Id}: {d.GetMessage()} at {d.Location}"));
                throw new Exception($"Compilation failed:\n{errorMsg}\n\nCode:\n{string.Join("\n\n---\n\n", sources)}");
            }

            ms.Seek(0, SeekOrigin.Begin);
            return AssemblyLoadContext.Default.LoadFromStream(ms);
        }

        protected string ToHex(byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", " ");
        }

        protected string BytesToHex(ReadOnlyMemory<byte> data)
        {
             return BitConverter.ToString(data.ToArray()).Replace("-", " "); // Space separation
        }

        protected object Instantiate(Assembly assembly, string typeName)
        {
            var type = assembly.GetType(typeName);
            if (type == null) throw new Exception($"Type {typeName} not found in assembly.");
            return Activator.CreateInstance(type);
        }

        protected void SetField(object instance, string fieldName, object value)
        {
            var type = instance.GetType();
            var field = type.GetField(fieldName);
            if (field == null) throw new Exception($"Field {fieldName} not found on type {type.Name}.");
            field.SetValue(instance, value);
        }

        protected object GetField(object instance, string fieldName)
        {
            var type = instance.GetType();
            var field = type.GetField(fieldName);
            if (field == null) throw new Exception($"Field {fieldName} not found on type {type.Name}.");
            return field.GetValue(instance);
        }
        
        protected string ExtractBody(string code)
        {
            // Extract content inside namespace { ... }
            int start = code.IndexOf("namespace");
            if (start == -1) return code;
            start = code.IndexOf("{", start) + 1;
            int end = code.LastIndexOf("}");
            return code.Substring(start, end - start);
        }


    }
}

