using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CycloneDDS.CodeGen
{
    public class SchemaDiscovery
    {
        public List<TypeInfo> DiscoverTopics(string sourceDirectory)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");
            }

            // 1. Find all .cs files
            var files = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories);
            
            if (files.Length == 0)
            {
                 return new List<TypeInfo>();
            }

            // 2. Parse into syntax trees
            var syntaxTrees = files.Select(f => 
                CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f)).ToList();
            
            // 3. Create compilation (not strictly used for simple syntax check but good practice)
            var compilation = CSharpCompilation.Create("Discovery")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(syntaxTrees);
            
            var topics = new List<TypeInfo>();
            
            foreach (var tree in syntaxTrees)
            {
                var root = tree.GetRoot();
                var typeDecls = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
                
                foreach (var typeDecl in typeDecls)
                {
                    // Check attributes
                    foreach (var attrList in typeDecl.AttributeLists)
                    {
                        foreach (var attr in attrList.Attributes)
                        {
                            var name = attr.Name.ToString();
                            // Simple name check for now. 
                            // In a full implementation we would use SemanticModel to resolve the type.
                            if (name.EndsWith("DdsTopic") || name.EndsWith("DdsTopicAttribute"))
                            {
                                var ns = GetNamespace(typeDecl);
                                topics.Add(new TypeInfo 
                                { 
                                    Name = typeDecl.Identifier.Text,
                                    Namespace = ns
                                });
                            }
                        }
                    }
                }
            }
            
            return topics;
        }

        private string GetNamespace(SyntaxNode node)
        {
            var nsNode = node.Parent;
            while (nsNode != null && !(nsNode is BaseNamespaceDeclarationSyntax))
            {
                nsNode = nsNode.Parent;
            }

            if (nsNode is BaseNamespaceDeclarationSyntax nsDecl)
            {
                var currentNs = nsDecl.Name.ToString();
                var parentNs = GetNamespace(nsDecl);
                return string.IsNullOrEmpty(parentNs) ? currentNs : $"{parentNs}.{currentNs}";
            }
            
            return string.Empty;
        }
    }
}
