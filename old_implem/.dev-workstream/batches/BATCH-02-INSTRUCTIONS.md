# BATCH-02: Roslyn Source Generator Infrastructure

**Batch Number:** BATCH-02  
**Tasks:** FCDC-005  
**Phase:** Phase 2 - Roslyn Source Generator  
**Estimated Effort:** 4-6 hours  
**Priority:** CRITICAL (Generator Foundation)  
**Dependencies:** BATCH-01 (approved)

---

## 📋 Onboarding & Workflow

### Developer Instructions

You are setting up the **Roslyn IIncrementalGenerator infrastructure** for the FastCycloneDDS source generator. This is the discovery mechanism that finds [DdsTopic], [DdsUnion], and [DdsTypeMap] attributes from user code and prepares them for code generation.

**Phase 2 generates code. Phase 3 (runtime) uses it.**

### Required Reading (IN ORDER)

1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Task Definition:** `docs/FCDC-TASK-MASTER.md` - See FCDC-005
3. **Detailed Task:** `tasks/FCDC-005.md` - Most detailed guidance (STUDY THIS)
4. **Design Document:** `docs/FCDC-DETAILED-DESIGN.md` - §5.1 Roslyn Source Generator Flow
5. **BATCH-01 Review:** `.dev-workstream/reviews/BATCH-01.1-REVIEW.md` - Learn from previous batch

### Source Code Location

**You will create:**
- **Project:** `src/CycloneDDS.Generator/CycloneDDS.Generator.csproj`
- **Test Project:** `tests/CycloneDDS.Generator.Tests/CycloneDDS.Generator.Tests.csproj`

### Report Submission

**When done, submit to:**  
`.dev-workstream/reports/BATCH-02-REPORT.md`

**If questions:**  
`.dev-workstream/questions/BATCH-02-QUESTIONS.md`

---

## Context

This batch establishes the **generator discovery pipeline**. It does NOT generate code yet—just discovers types and proves discovery works with placeholder output.

**Related Task:**
- [FCDC-005](../tasks/FCDC-005.md) - Generator Infrastructure (CRITICAL: Read this file completely)

**Why This Matters:**
- Foundation for all code generation in Phases 2-3
- Must be incremental-generation-correct (caching)
- Sets diagnostic reporting pattern
- Discovery precision is critical

---

## 🎯 Batch Objectives

1. ✅ Create CycloneDDS.Generator project (netstandard2.0, Roslyn requirements)
2. ✅ Implement IIncrementalGenerator with 3 discovery pipelines
3. ✅ Discover types with [DdsTopic] attribute
4. ✅ Discover types with [DdsUnion] attribute  
5. ✅ Discover assembly-level [DdsTypeMap] attributes
6. ✅ Establish diagnostic reporting (error codes FCDC0001-FCDC9999)
7. ✅ Create internal model classes (SchemaTopicType, SchemaUnionType, etc.)
8. ✅ Emit placeholder generated code proving discovery works
9. ✅ Test incremental generation caching
10. ✅ Test discovery with multiple schemas

---

## ✅ Tasks

### Task 1: Create Generator Project

**File:** `src/CycloneDDS.Generator/CycloneDDS.Generator.csproj` (NEW)

**Requirements:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <IsRoslynComponent>true</IsRoslynComponent>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.4" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\CycloneDDS.Schema\CycloneDDS.Schema.csproj" />
  </ItemGroup>
</Project>
```

**Critical:** Must target `netstandard2.0` (Roslyn requirement).

---

### Task 2: Implement IIncrementalGenerator

**File:** `src/CycloneDDS.Generator/FcdcGenerator.cs` (NEW)

**See:** `tasks/FCDC-005.md` lines 56-163 for complete implementation example

**Key Requirements:**
1. `[Generator]` attribute on class
2. Implement `IIncrementalGenerator.Initialize()`
3. Three pipelines:
   - `ForAttributeWithMetadataName` for `DdsTopicAttribute`
   - `ForAttributeWithMetadataName` for `DdsUnionAttribute`
   - `CompilationProvider.Select` for assembly `DdsTypeMapAttribute`
4. Combine results and register source output

**Placeholder Output:**
For now, generate simple placeholder files proving discovery works:
```csharp
// For each discovered topic
context.AddSource($"{topic.Symbol.Name}.Discovery.g.cs", 
    $@"// Auto-generated for topic: {topic.TopicName}
namespace {topic.Symbol.ContainingNamespace}
{{
    partial class {topic.Symbol.Name}
    {{
        // FCDC-005: Discovery placeholder
    }}
}}");
```

---

### Task 3: Create Internal Models

**Files to Create:**
- `src/CycloneDDS.Generator/Models/SchemaTopicType.cs` (NEW)
- `src/CycloneDDS.Generator/Models/SchemaUnionType.cs` (NEW)  
- `src/CycloneDDS.Generator/Models/SchemaField.cs` (NEW)
- `src/CycloneDDS.Generator/Models/GlobalTypeMapping.cs` (NEW)
- `src/CycloneDDS.Generator/Models/DdsQosSettings.cs` (NEW)

**See:** `tasks/FCDC-005.md` lines 165-195 for model structure

**Key Requirements:**
- Use `required` properties where appropriate
- Store `INamedTypeSymbol` and `IFieldSymbol` references
- Models are internal, not public
- Fields to be populated in FCDC-006 validation can be nullable/default for now

---

### Task 4: Implement Diagnostics

**Files to Create:**
- `src/CycloneDDS.Generator/Diagnostics/DiagnosticDescriptors.cs` (NEW)
- `src/CycloneDDS.Generator/Diagnostics/DiagnosticIds.cs` (NEW)

**Requirements:**
- Define `TopicDiscovered` (FCDC0001) info diagnostic
- Define `TopicNameMissing` (FCDC0002) error diagnostic
- Reserve ID ranges per `tasks/FCDC-005.md` lines 197-220

---

## 🧪 Testing Requirements

**Minimum:** 10+ integration tests

**Test Categories:**

1. **Discovery Tests** (5 tests minimum)
   - Single topic type discovered
   - Multiple topic types discovered
   - Topic with [DdsUnion] discovered
   - Assembly with [DdsTypeMap] discovered
   - No schemas (no-op behavior)

2. **Incremental Generation Tests** (3 tests minimum)
   - Changing unrelated file doesn't regenerate (cached)
   - Changing schema file causes regeneration
   - Adding new schema triggers generation only for new type

3. **Diagnostic Tests** (2 tests minimum)
   - TopicDiscovered diagnostic emitted
   - TopicNameMissing diagnostic for invalid topic

**Test Infrastructure:**
Use Roslyn test harness pattern (see `tasks/FCDC-005.md` lines 267-312)

**Example Test Structure:**
```csharp
[Fact]
public void DiscoversSingleTopicType()
{
    var source = @"
using CycloneDDS.Schema;

[DdsTopic(""TestTopic"")]
public partial class TestType { }
";
    var (compilation, diagnostics) = RunGenerator(source);
    
    Assert.Contains(diagnostics, d => d.Id == "FCDC0001" && d.GetMessage().Contains("TestTopic"));
    Assert.Single(compilation.SyntaxTrees.Where(t => t.FilePath.EndsWith(".Discovery.g.cs")));
}
```

---

## 📊 Report Requirements

**REMINDER: Detailed report required. See BATCH-01 review feedback.**

### Required Sections

1. **Executive Summary**
2. **Implementation Summary**
   - Files created
   - Design decisions (e.g., how you structured transform methods)
   - Deviations from spec

3. **Test Results**
   - Total count (10+ expected)
   - Categories tested
   - Coverage highlights

4. **Developer Insights**

   **Q1:** What challenges did you encounter with Roslyn IIncrementalGenerator API? How did you resolve them?

   **Q2:** What design decisions did you make for the transform pipeline? Why did you structure it that way?

   **Q3:** What are the weak points in the current generator infrastructure? What would you improve?

   **Q4:** What edge cases for discovery did you handle that weren't in the spec?

   **Q5:** Did you identify any performance concerns with the discovery pipelines?

   **Q6:** What guidance would you give for BATCH-03 (Validation Logic) regarding how to extend these pipelines?

5. **Build Instructions**
6. **Code Quality Checklist**

---

## 🎯 Success Criteria

- [ ] **FCDC-005 Complete:** Generator discovers [DdsTopic], [DdsUnion], [DdsTypeMap]
- [ ] **Project compiles:** netstandard2.0, zero warnings
- [ ] **Placeholder generation works:** .Discovery.g.cs files emitted
- [ ] **Diagnostics work:** FCDC0001 info messages appear
- [ ] **10+ tests passing:** Discovery, incremental, diagnostics
- [ ] **Incremental caching verified:** Unchanged inputs don't retrigger
- [ ] **Report submitted:** All sections complete with detailed answers

---

## ⚠️ Common Pitfalls to Avoid

### 1. Target Framework
**Pitfall:** Using `net8.0` instead of `netstandard2.0`.  
**Solution:** Generators MUST target netstandard2.0.

### 2. ForAttributeWithMetadataName Predicate
**Pitfall:** Predicate too broad (e.g., allowing interfaces).  
**Fix:** `node is ClassDeclarationSyntax or StructDeclarationSyntax`

### 3. Null Symbol References
**Pitfall:** `context.TargetSymbol as INamedTypeSymbol` can be null.  
**Fix:** Check `symbol is null` and return null from transform.

### 4. Incremental Caching
**Pitfall:** Not testing that unchanged files don't regenerate.  
**Solution:** Write test verifying cached behavior (see tasks/FCDC-005.md#integration-tests).

### 5. Diagnostic Category
**Pitfall:** Using inconsistent category names.  
**Fix:** All diagnostics use category "CycloneDDS.Generator".

---

## ⚠️ Quality Standards

### ❗ CODE QUALITY
- **REQUIRED:** netstandard2.0 target
- **REQUIRED:** XML comments on public generator class
- **REQUIRED:** Null checks on symbol casts
- **REQUIRED:** Deterministic output (same input → same output)

### ❗ TEST QUALITY
- **REQUIRED:** Test incremental caching behavior
- **REQUIRED:** Test with multiple schemas
- **REQUIRED:** Test no-op case (no schemas)
- **NOT ACCEPTABLE:** Tests that only check "code generated" without verifying content

### ❗ REPORT QUALITY
- **REQUIRED:** Detailed answers to all 6 questions
- **REQUIRED:** Document Roslyn-specific challenges
- **REQUIRED:** Explain pipeline structuring decisions

---

## 📚 Reference Materials

- **CRITICAL:** [tasks/FCDC-005.md](../tasks/FCDC-005.md) - Complete implementation guide
- **Task Master:** [docs/FCDC-TASK-MASTER.md](../docs/FCDC-TASK-MASTER.md)
- **Design Doc:** [docs/FCDC-DETAILED-DESIGN.md](../docs/FCDC-DETAILED-DESIGN.md) - §5.1
- **Roslyn Cookbook:** [Microsoft Incremental Generators](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md)
- **BATCH-01 Lessons:** `.dev-workstream/reviews/BATCH-01.1-REVIEW.md`

---

## 🔄 Next Steps After Completion

After approval:
1. Commit using provided message
2. BATCH-03: Schema Validation Logic (FCDC-006)

**This is the generator foundation. Get it right—everything else builds on this.** 🚀
