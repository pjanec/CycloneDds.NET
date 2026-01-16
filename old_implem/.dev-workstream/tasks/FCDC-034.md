# FCDC-034: Replace Regex with CppAst in DescriptorExtractor

**Task ID:** FCDC-034  
**Phase:** 5 - Advanced Features & Polish  
**Priority:** Medium  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-018A  
**Design Reference:** `docs/ADVANCED-OPTIMIZATIONS-DESIGN.md` §4

---

## Objective

Refactor `DescriptorExtractor.cs` to use CppAst for parsing idlc-generated C code instead of fragile Regex patterns.

---

## Problem Statement

**Current Implementation:**
```csharp
// DescriptorExtractor.cs - FRAGILE!
var opsRegex = new Regex(@"_ops\s*\[\]\s*=\s*\{([\s\S]*?)\};");
var keysRegex = new Regex(@"static const dds_key_descriptor_t\s+(\w+)\s*\[\d+\]\s*=\s*\{([\s\S]*?)\};");
```

**Risk:** Format changes in idlc output will break extraction entirely.

**Examples of Breakage:**
1. idlc adds comments → regex fails
2. idlc changes whitespace → regex fails
3. idlc uses macros → regex fails

---

## Solution: CppAst Parsing

**Concept:** Parse C code as Abstract Syntax Tree using CppAst (already dependency for AbiOffsets).

**Implementation Steps:**

### Step 1: Parse idlc Output

```csharp
using CppAst;

public static DescriptorData ExtractFromIdlcOutput(string cFilePath)
{
    var options = new CppParserOptions
    {
        ParseMacros = true,
        IncludeFolders = { cycloneIncludePath }
    };
    
    var compilation = CppParser.ParseFile(cFilePath, options);
    
    if (compilation.HasErrors)
    {
        throw new InvalidOperationException(
            $"Failed to parse {cFilePath}: {string.Join(", ", compilation.Diagnostics)}");
    }
    
    return ExtractDescriptorFromAst(compilation);
}
```

### Step 2: Find Descriptor Variables

```csharp
private static DescriptorData ExtractDescriptorFromAst(CppCompilation compilation)
{
    // Find all global variables of type dds_topic_descriptor_t
    var descriptors = compilation.Globals.OfType<CppVariable>()
        .Where(v => v.Type.GetDisplayName().Contains("dds_topic_descriptor_t"))
        .ToList();
    
    if (descriptors.Count == 0)
        throw new InvalidOperationException("No dds_topic_descriptor_t found");
    
    if (descriptors.Count > 1)
        throw new InvalidOperationException("Multiple topic descriptors found");
    
    return ParseDescriptorInit(descriptors[0]);
}
```

### Step 3: Extract Initializer Values

```csharp
private static DescriptorData ParseDescriptorInit(CppVariable descriptor)
{
    // CppAst provides structured initializer access
    var init = descriptor.InitValue as CppStructInitializer;
    
    var data = new DescriptorData
    {
        TypeName = GetStringField(init, "m_typename"),
        NKeys = GetUintField(init, "m_nkeys"),
        NOps = GetUintField(init, "m_nops")
    };
    
    // Parse ops array
    var opsArray = GetArrayField(init, "m_ops");
    data.Ops = ParseOpsFromAst(opsArray);
    
    // Parse keys array
    if (data.NKeys > 0)
    {
        var keysArray = GetArrayField(init, "m_keys");
        data.Keys = ParseKeysFromAst(keysArray);
    }
    
    // Parse type_information
    var typeInfo = GetStructField(init, "type_information");
    data.TypeInfo = ParseTypeInfoFromAst(typeInfo);
    
    return data;
}
```

### Step 4: Parse Nested Structures

```csharp
private static KeyDescriptor[] ParseKeysFromAst(CppArrayInitializer keys)
{
    var result = new List<KeyDescriptor>();
    
    foreach (var element in keys.Elements)
    {
        var keyInit = element as CppStructInitializer;
        
        result.Add(new KeyDescriptor
        {
            Name = GetStringField(keyInit, "m_name"),
            Flags = (ushort)GetUintField(keyInit, "m_offset"),
            Index = (ushort)GetUintField(keyInit, "m_index")
        });
    }
    
    return result.ToArray();
}

private static uint[] ParseOpsFromAst(CppArrayInitializer ops)
{
    var result = new List<uint>();
    
    foreach (var element in ops.Elements)
    {
        // Handle expressions (DDS_OP_ADR | DDS_OP_TYPE_4BY | offsetof(...))
        uint value = EvaluateConstExpression(element);
        result.Add(value);
    }
    
    return result.ToArray();
}
```

---

## Benefits

1. **Robust:** Immune to formatting changes in idlc output
2. **Consistent:** Same parsing toolchain as AbiOffsets
3. **Maintainable:** Leverages well-tested libclang parser
4. **Better Errors:** Distinguishes syntax errors from logic errors

---

## Testing Requirements

1. **Unit Tests:**
   - Parse all existing test message descriptors
   - Compare results to current Regex-based extraction
   - Verify byte-perfect match

2. **Malformed Input Tests:**
   - Missing fields → clear error
   - Invalid C syntax → parse error
   - Comments/whitespace variations → still works

3. **Regression Tests:**
   - Re-run all BATCH-13/14 tests
   - Verify descriptor generation unchanged
   - No test failures

---

## Implementation Checklist

- [ ] Add CppAst field extraction helpers
- [ ] Implement `ExtractFromIdlcOutput` with CppParser
- [ ] Implement `ParseDescriptorInit` for struct initializers
- [ ] Implement `ParseOpsFromAst` with expression evaluation
- [ ] Implement `ParseKeysFromAst`
- [ ] Add error handling for parse failures
- [ ] Unit test: Parse SimpleMessage descriptor
- [ ] Unit test: Parse AllPrimitivesMessage descriptor
- [ ] Unit test: Parse SensorData (composite keys)
- [ ] Unit test: Malformed C code handling
- [ ] Integration test: Full code generation pipeline
- [ ] Remove old Regex-based code
- [ ] Update documentation

---

## Acceptance Criteria

1. ✅ All existing test types generate identical descriptors
2. ✅ Code handles comments/whitespace variations
3. ✅ Clear error messages for invalid C code
4. ✅ No performance regression (< 100ms overhead)
5. ✅ All BATCH-13/14 tests still pass

---

## Design Reference

See `docs/ADVANCED-OPTIMIZATIONS-DESIGN.md` Section 4: Robust Descriptor Extraction

**Key Design Points:**
- Uses CppParser.ParseFile() for idlc output
- Extracts initializers via CppStructInitializer
- Evaluates constant expressions for ops array
- Consistent with AbiOffset generation approach
