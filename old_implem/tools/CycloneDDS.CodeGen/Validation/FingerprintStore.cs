using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;

namespace CycloneDDS.CodeGen.Validation;

public class FingerprintStore
{
    private readonly string _storeFilePath;
    private Dictionary<string, SchemaFingerprint> _fingerprints = new();
    
    public FingerprintStore(string sourceDirectory)
    {
        // Store in Generated/ folder
        var generatedDir = Path.Combine(sourceDirectory, "Generated");
        Directory.CreateDirectory(generatedDir);
        _storeFilePath = Path.Combine(generatedDir, ".schema-fingerprints.json");
        
        Load();
    }
    
    public void Load()
    {
        if (!File.Exists(_storeFilePath))
        {
            _fingerprints = new();
            return;
        }
        
        try
        {
            var json = File.ReadAllText(_storeFilePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, FingerprintData>>(json);
            
            if (data != null)
            {
                _fingerprints = data.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new SchemaFingerprint(
                        kvp.Value.TypeName,
                        kvp.Value.Hash,
                        kvp.Value.Members.Select(m => new SchemaFingerprint.MemberInfo(m.Index, m.Name, m.Type)).ToList()
                    )
                );
            }
        }
        catch
        {
            // Corrupted file, start fresh
            _fingerprints = new();
        }
    }
    
    public void Save()
    {
        var data = _fingerprints.ToDictionary(
            kvp => kvp.Key,
            kvp => new FingerprintData
            {
                TypeName = kvp.Value.TypeName,
                Hash = kvp.Value.Hash,
                Members = kvp.Value.Members.Select(m => new MemberData 
                { 
                    Index = m.Index, 
                    Name = m.Name, 
                    Type = m.Type 
                }).ToList()
            }
        );
        
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_storeFilePath, json);
    }
    
    public SchemaFingerprint? GetPrevious(string typeName)
    {
        _fingerprints.TryGetValue(typeName, out var fingerprint);
        return fingerprint;
    }
    
    public void Update(string typeName, SchemaFingerprint fingerprint)
    {
        _fingerprints[typeName] = fingerprint;
    }
    
    private class FingerprintData
    {
        public string TypeName { get; set; } = "";
        public string Hash { get; set; } = "";
        public List<MemberData> Members { get; set; } = new();
    }
    
    private class MemberData
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
    }
}
