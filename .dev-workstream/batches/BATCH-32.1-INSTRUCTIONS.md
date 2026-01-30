# BATCH-32.1: IDL Importer - Corrections

**Batch Number:** BATCH-32.1 (Corrective)
**Parent Batch:** BATCH-32
**Estimated Effort:** 2 hours
**Priority:** HIGH (Corrective)

---

## 📋 Onboarding & Workflow

### Background
This is a **corrective batch** addressing issues found in BATCH-32 review.
Specifically, **test files were missing** from the submission, making it impossible to verify the logic.

**Original Batch:** `.dev-workstream/batches/BATCH-32-INSTRUCTIONS.md`
**Review with Issues:** `.dev-workstream/reviews/BATCH-32-REVIEW.md`

Please read both before starting.

---

## 🎯 Objectives

1.  **Commit Missing Tests:** Add `ImporterTests.cs` and `CSharpEmitterTests.cs`.
2.  **Fix Naming:** Ensure C# fields are generated in `PascalCase`.
3.  **Verify:** Run the tests to prove the crawling engine works.

---

## ✅ Tasks

### Task 1: Add Missing Tests (Fix Issue 1 & 3)

**File:** `tools/CycloneDDS.IdlImporter.Tests/ImporterTests.cs`, `CSharpEmitterTests.cs`

**Instructions:**
1.  Restore/Create the test files that were described in BATCH-32 Report.
2.  **ImporterTests:**
    -   Must create temporary IDL files on disk.
    -   Must run `Importer.Import`.
    -   Must assert that output files are created in the correct structure.
    -   Must assert that dependencies are found and processed.
3.  **CSharpEmitterTests:**
    -   Must verify generated string contains correct syntax (Namespace, Struct, Enum).

**Critical:** These tests must run successfully with `dotnet test`.

---

### Task 2: Standardize Field Names (Fix Issue 2)

**File:** `tools/CycloneDDS.IdlImporter/CSharpEmitter.cs`

**Original Implementation:**
```csharp
sb.AppendLine($"{indent}public {csType} {member.Name};");
```

**Required Change:**
```csharp
sb.AppendLine($"{indent}public {csType} {ToPascalCase(member.Name)};");

// Helper
private string ToPascalCase(string name) {
    // lowercase -> Uppercase first letter
    // snake_case -> PascalCase
}
```

**Why This Matters:** Adherence to C# coding standards.

**Tests Required:**
-   Update `CSharpEmitterTests` to expect PascalCase names (e.g. `member_id` -> `MemberId`).

---

## 🧪 Testing Requirements

**Success Criteria:**
1.  ✅ `tools/CycloneDDS.IdlImporter.Tests` contains at least 3 test classes (`TypeMapperTests`, `ImporterTests`, `CSharpEmitterTests`).
2.  ✅ All tests pass.
3.  ✅ Generated fields use PascalCase.

---

## 🎯 Success Criteria

This batch is DONE when all tests are present and passing.

**Report to:** `.dev-workstream/reports/BATCH-32.1-REPORT.md`
