using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CycloneDDS.CodeGen.Validation;

public class SchemaFingerprint
{
    public string TypeName { get; }
    public string Hash { get; }
    public List<MemberInfo> Members { get; }
    
    public SchemaFingerprint(string typeName, string hash, List<MemberInfo> members)
    {
        TypeName = typeName;
        Hash = hash;
        Members = members;
    }
    
    public record MemberInfo(int Index, string Name, string Type);
    
    public static SchemaFingerprint Compute(TypeDeclarationSyntax type)
    {
        var typeName = type.Identifier.Text;
        var members = new List<MemberInfo>();
        
        var fields = type.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => !f.Modifiers.Any(m => m.Text == "const"))
            .ToList();
            
        for (int i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            var fieldType = field.Declaration.Type.ToString();
            var fieldName = field.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "?";
            
            members.Add(new MemberInfo(i, fieldName, fieldType));
        }
        
        // Compute hash from member order + names + types
        var sb = new StringBuilder();
        foreach (var member in members)
        {
            sb.Append($"{member.Index}:{member.Name}:{member.Type};");
        }
        
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        var hash = Convert.ToHexString(hashBytes);
        
        return new SchemaFingerprint(typeName, hash, members);
    }
    
    public static EvolutionResult CompareForEvolution(SchemaFingerprint old, SchemaFingerprint @new)
    {
        var result = new EvolutionResult();
        
        if (old.TypeName != @new.TypeName)
        {
            result.AddError($"Type name changed from '{old.TypeName}' to '{@new.TypeName}'");
            return result;
        }
        
        // Check for removed members
        for (int i = 0; i < old.Members.Count; i++)
        {
            var oldMember = old.Members[i];
            var newMember = i < @new.Members.Count ? @new.Members[i] : null;
            
            if (newMember == null)
            {
                result.AddError($"Member '{oldMember.Name}' at index {i} was removed");
                continue;
            }
            
            if (oldMember.Name != newMember.Name)
            {
                result.AddError($"Member at index {i} renamed from '{oldMember.Name}' to '{newMember.Name}' or reordered");
            }
            
            if (oldMember.Type != newMember.Type)
            {
                result.AddError($"Member '{oldMember.Name}' type changed from '{oldMember.Type}' to '{newMember.Type}'");
            }
        }
        
        // Check for inserted members (not at end)
        if (@new.Members.Count > old.Members.Count)
        {
            result.MembersAdded = @new.Members.Count - old.Members.Count;
            result.AddInfo($"{result.MembersAdded} new member(s) added (appendable-safe)");
        }
        
        return result;
    }
}

public class EvolutionResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Info { get; } = new();
    public int MembersAdded { get; set; }
    
    public bool HasBreakingChanges => Errors.Any();
    
    public void AddError(string message) => Errors.Add(message);
    public void AddWarning(string message) => Warnings.Add(message);
    public void AddInfo(string message) => Info.Add(message);
}
