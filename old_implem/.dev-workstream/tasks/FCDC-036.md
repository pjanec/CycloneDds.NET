# FCDC-036: MetadataReference for CodeGen Validation

**Task ID:** FCDC-036  
**Phase:** 5 - Advanced Features & Polish  
**Priority:** Low (Polish/Quality)  
**Estimated Effort:** 2 days  
**Dependencies:** FCDC-005 (Generator Infrastructure)  
**Design Reference:** `docs/ADVANCED-OPTIMIZATIONS-DESIGN.md` (External Analysis §5)

---

## Objective

Refactor CodeGen to use Roslyn semantic analysis instead of string matching for more robust attribute validation.

---

## Problem Statement

**Current Validation (Fragile):**
```csharp
// FcdcGenerator.cs - String matching
var hasDdsTopic = classDecl.AttributeLists
    .SelectMany(a => a.Attributes)
    .Any(attr => attr.Name.ToString() == "DdsTopic"); // Fragile!
```

**Issues:**
1. Doesn't handle `[DdsTopicAttribute]` (full name)
2. Doesn't handle `[CycloneDDS.Schema.DdsTopic]` (qualified)
3. Can't validate attribute arguments semantically
4. No IntelliSense-quality error messages

---

## Solution: Semantic Model Analysis

**Using Roslyn MetadataReference:**

```csharp
// Load Schema assembly as reference
var schemaAssembly = typeof(DdsTopicAttribute).Assembly.Location;
var references = new[]
{
    MetadataReference.CreateFromFile(schemaAssembly),
    MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
};

// Create compilation
var compilation = CSharpCompilation.Create(
    "SchemaAnalysis",
    syntaxTrees: new[] { tree },
    references: references);

var semanticModel = compilation.GetSemanticModel(tree);

// Semantic analysis
foreach (var classDecl in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
{
    var symbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
    
    // Robust attribute checking
    var ddsTopic = symbol.GetAttributes()
        .FirstOrDefault(a => a.AttributeClass?.Name == "DdsTopicAttribute" &&
                            a.AttributeClass?.ContainingNamespace?.ToString() == "CycloneDDS.Schema");
    
    if (ddsTopic != null)
    {
        // Access attribute arguments with type safety
        string topicName = ddsTopic.NamedArguments
            .FirstOrDefault(kv => kv.Key == "Name")
            .Value.Value as string;
    }
}
```

---

## Benefits

1. **Robust:** Works with short names, full names, qualified names
2. **Type-Safe:** Attribute arguments strongly typed
3. **Better Errors:** Can report "Missing required property 'Name'" instead of "Invalid syntax"
4. **Validates Inheritance:** Can check `INotifyPropertyChanged` requirements
5. **Future-Proof:** Handles namespace aliases, using directives

---

## Implementation Steps

### Step 1: Add Roslyn References

**File:** `tools/CycloneDDS.CodeGen/CycloneDDS.CodeGen.csproj`

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
</ItemGroup>
```

### Step 2: Create Semantic Analyzer

**File:** `tools/CycloneDDS.CodeGen/Analysis/SemanticSchemaAnalyzer.cs` (NEW)

```csharp
public class SemanticSchemaAnalyzer
{
    private readonly SemanticModel _semanticModel;
    
    public SemanticSchemaAnalyzer(SyntaxTree tree)
    {
        var compilation = CreateCompilation(tree);
        _semanticModel = compilation.GetSemanticModel(tree);
    }
    
    private CSharpCompilation CreateCompilation(SyntaxTree tree)
    {
        var schemaRef = MetadataReference.CreateFromFile(
            typeof(CycloneDDS.Schema.DdsTopicAttribute).Assembly.Location);
        
        var systemRef = MetadataReference.CreateFromFile(
            typeof(object).Assembly.Location);
        
        return CSharpCompilation.Create(
            "SchemaAnalysis",
            new[] { tree },
            new[] { schemaRef, systemRef });
    }
    
    public IEnumerable<TopicClass> FindTopicClasses(SyntaxNode root)
    {
        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var symbol = _semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
            if (symbol == null) continue;
            
            var topicAttr = GetDdsTopicAttribute(symbol);
            if (topicAttr == null) continue;
            
            yield return new TopicClass
            {
                Symbol = symbol,
                Declaration = classDecl,
                TopicAttribute = topicAttr
            };
        }
    }
    
    private AttributeData GetDdsTopicAttribute(INamedTypeSymbol symbol)
    {
        return symbol.GetAttributes()
            .FirstOrDefault(a => 
                a.AttributeClass?.Name == "DdsTopicAttribute" &&
                a.AttributeClass?.ContainingNamespace?.ToDisplayString() == "CycloneDDS.Schema");
    }
}
```

### Step 3: Update Generator to Use Semantic Analysis

**File:** `tools/CycloneDDS.CodeGen/FcdcGenerator.cs`

```csharp
public void Generate(string schemaFile, string outputDir)
{
    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(schemaFile));
    var analyzer = new SemanticSchemaAnalyzer(tree);
    
    foreach (var topicClass in analyzer.FindTopicClasses(tree.GetRoot()))
    {
        // Validate with semantic info
        ValidateTopicClass(topicClass);
        
        // Generate code
        GenerateForTopic(topicClass);
    }
}

private void ValidateTopicClass(TopicClass topic)
{
    // Check required attribute properties
    var topicName = topic.TopicAttribute.NamedArguments
        .FirstOrDefault(kv => kv.Key == "Name")
        .Value.Value as string;
    
    if (string.IsNullOrEmpty(topicName))
    {
        throw new GeneratorException(
            $"Class '{topic.Symbol.Name}' has [DdsTopic] but missing Name property",
            topic.Declaration.GetLocation());
    }
    
    // Check implements required interfaces (if needed)
    // ... etc
}
```

---

## Testing Requirements

### Unit Tests

```csharp
[Fact]
public void SemanticAnalyzer_FindsDdsTopicAttribute_ShortName()
{
    var code = @"
        using CycloneDDS.Schema;
        
        [DdsTopic(Name = ""Test"")]
        public class Message { }
    ";
    
    var tree = CSharpSyntaxTree.ParseText(code);
    var analyzer = new SemanticSchemaAnalyzer(tree);
    
    var topics = analyzer.FindTopicClasses(tree.GetRoot()).ToList();
    
    Assert.Single(topics);
    Assert.Equal("Message", topics[0].Symbol.Name);
}

[Fact]
public void SemanticAnalyzer_FindsDdsTopicAttribute_FullName()
{
    var code = @"
        [CycloneDDS.Schema.DdsTopicAttribute(Name = ""Test"")]
        public class Message { }
    ";
    
    // Should still find it
    var topics = analyzer.FindTopicClasses(tree.GetRoot()).ToList();
    Assert.Single(topics);
}

[Fact]
public void SemanticAnalyzer_ValidatesRequiredProperties()
{
    var code = @"
        using CycloneDDS.Schema;
        
        [DdsTopic] // Missing Name!
        public class Message { }
    ";
    
    Assert.Throws<GeneratorException>(() => generator.Generate(code));
}
```

---

## Acceptance Criteria

1. ✅ Finds `[DdsTopic]` (short name)
2. ✅ Finds `[DdsTopicAttribute]` (full name)
3. ✅ Finds `[CycloneDDS.Schema.DdsTopic]` (qualified)
4. ✅ Validates attribute arguments with type safety
5. ✅ Better error messages with line numbers
6. ✅ All existing tests still pass
7. ✅ No performance regression

---

## Design Reference

See `docs/EXTERNAL-ARCHITECTURE-ANALYSIS-RESPONSE.md` - CodeGen Improvements

**Key Points:**
- Uses Roslyn MetadataReference for Schema assembly
- Semantic model provides symbol information
- Type-safe attribute argument access
- Better validation and error messages
