# NuGet Packaging - Task Details

**Reference Design:** [DESIGN.md](./DESIGN.md)  
**Task Tracker:** [TASK-TRACKER.md](./TASK-TRACKER.md)

This document contains detailed specifications for each task in the NuGet packaging project.

---

## Stage 0: Foundation Setup

### NUGET-000: Project Analysis and Current State Documentation

**Description:**  
Analyze and document the current project structure, dependencies, and build workflow to establish baseline understanding.

**Reference:** [DESIGN.md - Project Overview](./DESIGN.md#project-overview)

**Tasks:**
1. Map all current `.csproj` files and their dependencies
2. Document current version numbers in each project
3. Identify all native dependencies (DLLs, executables)
4. Document current build process (scripts, manual steps)
5. List all test projects and their dependencies on native assets
6. Document submodule structure (cyclonedds, cyclonedds-cxx)

**Success Conditions:**
- [ ] Dependency graph created showing all project relationships
- [ ] Current versioning approach documented
- [ ] Native dependency list complete (dll names, versions, locations)
- [ ] Build workflow diagram created
- [ ] All findings documented in analysis document

**Acceptance Test:**
- Run `build_and_run_tests.ps1` successfully
- Document each step and its purpose
- Verify all test projects pass

---

## Stage 1: Versioning Infrastructure

### NUGET-001: Nerdbank.GitVersioning Setup

**Description:**  
Install and configure Nerdbank.GitVersioning (NBGV) for Git-tag based versioning.

**Reference:** [DESIGN.md §1.1 - Git-Based Versioning](./DESIGN.md#11-single-source-of-truth-git-based-versioning)

**Prerequisites:** None

**Tasks:**
1. Install NBGV NuGet package to solution
2. Create `version.json` in repository root
3. Configure initial version (e.g., `1.0.0-alpha.1`)
4. Configure version height and release branch settings
5. Test version generation locally
6. Document version bumping process

**Files to Create:**
- `version.json` (root)

**Success Conditions:**
- [ ] `version.json` exists and is properly formatted
- [ ] `dotnet nbgv get-version` executes successfully
- [ ] Version displayed matches expected format (1.0.0-alpha.1)
- [ ] Git tag `v1.0.0-alpha.1` created
- [ ] After tag, `dotnet nbgv get-version` shows exact `1.0.0-alpha.1`
- [ ] Without tag, version shows commit height (e.g., `1.0.0-alpha.1+12`)

**Acceptance Tests:**
```powershell
# Test 1: NBGV tool works
dotnet tool install --global nbgv
nbgv get-version

# Test 2: Version from tag
git tag v1.0.0-test
nbgv get-version | Should -Match "1.0.0-test"

# Test 3: Version without tag includes commit info
git tag -d v1.0.0-test
nbgv get-version | Should -Match "alpha"
```

### NUGET-002: Directory.Build.props Creation

**Description:**  
Create centralized build properties file with shared package metadata.

**Reference:** [DESIGN.md §1.2 - Centralized Package Metadata](./DESIGN.md#12-centralized-package-metadata)

**Prerequisites:** 
- NUGET-001 complete

**Tasks:**
1. Create `Directory.Build.props` in repository root
2. Configure common package metadata (Authors, Description, etc.)
3. Configure SourceLink for debuggability
4. Configure symbol package generation
5. Remove version numbers from all `.csproj` files
6. Test that versions are correctly inherited

**Files to Create:**
- `Directory.Build.props`

**Files to Modify:**
- All `.csproj` files (remove `<Version>` elements)

**Success Conditions:**
- [ ] `Directory.Build.props` exists in repo root
- [ ] Contains all required metadata fields
- [ ] SourceLink configured (`Microsoft.SourceLink.GitHub` package)
- [ ] `IncludeSymbols=true` and `SymbolPackageFormat=snupkg`
- [ ] All `.csproj` files have `<Version>` removed
- [ ] Build succeeds and version is applied from NBGV
- [ ] `dotnet pack` produces `.snupkg` symbol packages

**Acceptance Tests:**
```powershell
# Test 1: Build inherits properties
dotnet build src/CycloneDDS.Core/CycloneDDS.Core.csproj
# Verify no warnings about missing metadata

# Test 2: Pack produces correct metadata
dotnet pack src/CycloneDDS.Core/CycloneDDS.Core.csproj -o test-output
# Inspect .nupkg - should have Authors, License, Repo URL

# Test 3: Symbol package generated
Test-Path test-output/*.snupkg | Should -Be $true
```

### NUGET-003: Remove Hardcoded Versions from Projects

**Description:**  
Update all `.csproj` files to rely on NBGV and `Directory.Build.props` for versioning.

**Reference:** [DESIGN.md §1.2 - Centralized Package Metadata](./DESIGN.md#12-centralized-package-metadata)

**Prerequisites:**
- NUGET-002 complete

**Tasks:**
1. Identify all `.csproj` files with `<Version>` elements
2. Remove `<Version>` from each project
3. Remove duplicate metadata (Authors, Description if generic)
4. Test each project builds correctly
5. Verify version is correctly applied via NBGV

**Files to Modify:**
- `src/CycloneDDS.Core/CycloneDDS.Core.csproj`
- `src/CycloneDDS.Runtime/CycloneDDS.Runtime.csproj`
- `src/CycloneDDS.Schema/CycloneDDS.Schema.csproj`
- `tools/CycloneDDS.CodeGen/CycloneDDS.CodeGen.csproj`
- `tools/CycloneDDS.IdlImporter/CycloneDDS.IdlImporter.csproj`
- (All other projects)

**Success Conditions:**
- [ ] No `.csproj` contains `<Version>` element
- [ ] All projects build successfully
- [ ] `dotnet pack` on any project shows version from NBGV
- [ ] No duplicate metadata between projects and `Directory.Build.props`

**Acceptance Tests:**
```powershell
# Test: No hardcoded versions remain
Select-String -Path **/*.csproj -Pattern "<Version>" | Should -Be $null

# Test: All projects build
dotnet build CycloneDDS.NET.sln -c Release

# Test: Packing uses NBGV version
$version = nbgv get-version -v NuGetPackageVersion
dotnet pack src/CycloneDDS.Core/CycloneDDS.Core.csproj -o test
Test-Path "test/CycloneDDS.Core.$version.nupkg" | Should -Be $true
```

---

## Stage 2: Build Script Infrastructure

### NUGET-004: Native Build Script (Windows)

**Description:**  
Create PowerShell script to build native Cyclone DDS artifacts for Windows x64.

**Reference:** [DESIGN.md §4.2 - Build Script Architecture](./DESIGN.md#42-build-script-architecture)

**Prerequisites:**
- None (foundational)

**Tasks:**
1. Create `build/native-win.ps1` script
2. Accept Configuration parameter (Debug/Release)
3. Configure CMake for Cyclone DDS submodule
4. Build native DLL and idlc executable
5. Copy outputs to `artifacts/native/win-x64/`
6. Add error handling and logging
7. Test script locally

**Files to Create:**
- `build/native-win.ps1`

**Directory Structure:**
```
/build/
    native-win.ps1
/artifacts/
    /native/
        /win-x64/
            ddsc.dll
            idlc.exe
            cycloneddsidl.dll
            cycloneddsidlc.dll
            cycloneddsidljson.dll
            ddsperf.exe
            msvcp140*.dll (VC++ runtime)
            vcruntime140*.dll (VC++ runtime)
            concrt140.dll (VC++ runtime)
```

**Success Conditions:**
- [ ] Script executes without errors
- [ ] Outputs appear in `artifacts/native/win-x64/`
- [ ] Core DLL ddsc.dll is valid Win64 binary
- [ ] idlc.exe is valid Win64 executable
- [ ] All dependency DLLs present (cycloneddsidl.dll, cycloneddsidlc.dll, cycloneddsidljson.dll)
- [ ] VC++ runtime DLLs included
- [ ] Script works from any working directory
- [ ] Configuration parameter works (Debug/Release)
- [ ] Idempotent (can run multiple times)

**Acceptance Tests:**
```powershell
# Test 1: Script runs successfully
.\build\native-win.ps1 -Configuration Release
$LASTEXITCODE | Should -Be 0

# Test 2: Core outputs exist
Test-Path artifacts\native\win-x64\ddsc.dll | Should -Be $true
Test-Path artifacts\native\win-x64\idlc.exe | Should -Be $true

# Test 3: IDL dependencies exist
Test-Path artifacts\native\win-x64\cycloneddsidl.dll | Should -Be $true
Test-Path artifacts\native\win-x64\cycloneddsidlc.dll | Should -Be $true
Test-Path artifacts\native\win-x64\cycloneddsidljson.dll | Should -Be $true

# Test 4: VC++ runtime DLLs exist
Test-Path artifacts\native\win-x64\msvcp140.dll | Should -Be $true
Test-Path artifacts\native\win-x64\vcruntime140.dll | Should -Be $true
```

### NUGET-005: Unified Pack Script

**Description:**  
Create PowerShell script that orchestrates the entire build and pack process.

**Reference:** [DESIGN.md §4.2 - Build Script Architecture](./DESIGN.md#42-build-script-architecture)

**Prerequisites:**
- NUGET-004 complete (native build script)

**Tasks:**
1. Create `build/pack.ps1` script
2. Call `native-win.ps1` to build native assets
3. Run `dotnet restore`
4. Run `dotnet build -c Release`
5. Run `dotnet test -c Release --no-build`
6. Run `dotnet pack -c Release -o artifacts/nuget`
7. Add error handling at each step
8. Add progress logging

**Files to Create:**
- `build/pack.ps1`

**Success Conditions:**
- [ ] Script runs all steps in correct order
- [ ] Stops on first error
- [ ] Outputs clear progress messages
- [ ] Produces `.nupkg` files in `artifacts/nuget/`
- [ ] All tests pass before packing
- [ ] Script works from clean state (fresh clone)

**Acceptance Tests:**
```powershell
# Test 1: Full pack cycle
.\build\pack.ps1
$LASTEXITCODE | Should -Be 0

# Test 2: Artifacts created
Test-Path artifacts\nuget\*.nupkg | Should -Be $true

# Test 3: Test failure prevents packing
# (mock a failing test and verify pack doesn't run)
```

---

## Stage 3: NuGet Package Structure

### NUGET-006: Artifact Staging for Packaging

**Description:**  
Ensure native artifacts are correctly staged before packing and configure projects to include them.

**Reference:** [DESIGN.md §4.1 - Artifact Staging Architecture](./DESIGN.md#41-artifact-staging-architecture)

**Prerequisites:**
- NUGET-004 complete (native build)

**Tasks:**
1. Verify `artifacts/native/win-x64/` structure is correct
2. Identify exact list of files to include in package:
   - **Runtime:** ddsc.dll
   - **Build tools:** idlc.exe
   - **IDL support:** cycloneddsidl.dll, cycloneddsidlc.dll, cycloneddsidljson.dll
   - **VC++ Runtime:** msvcp140*.dll, vcruntime140*.dll, concrt140.dll
   - **Optional:** ddsperf.exe (performance tool)
3. Document any additional native dependencies needed
4. Create script to validate artifact completeness
5. Add staging validation to pack script

**Success Conditions:**
- [ ] `artifacts/native/win-x64/` contains all required files
- [ ] File list documented:
  - ddsc.dll (runtime)
  - idlc.exe (build tool)
  - cycloneddsidl.dll, cycloneddsidlc.dll, cycloneddsidljson.dll (IDL support)
  - VC++ runtime DLLs (msvcp140*, vcruntime140*, concrt140)
  - ddsperf.exe (optional)
- [ ] No missing dependencies
- [ ] Staging directory can be validated programmatically

**Acceptance Tests:**
```powershell
# Test: Required files exist
$required = @(
    "ddsc.dll",
    "idlc.exe",
    "cycloneddsidl.dll",
    "cycloneddsidlc.dll",
    "cycloneddsidljson.dll",
    "msvcp140.dll",
    "vcruntime140.dll"
)
foreach ($file in $required) {
    Test-Path "artifacts\native\win-x64\$file" | Should -Be $true
}
```

### NUGET-007: Configure Runtime Package Structure

**Description:**  
Configure `CycloneDDS.Runtime.csproj` (or main project) to produce NuGet package with correct folder layout.

**Reference:** [DESIGN.md §2.2 - NuGet Folder Structure](./DESIGN.md#22-nuget-folder-structure-cycloneddsnet)

**Prerequisites:**
- NUGET-006 complete (artifact staging)

**Tasks:**
1. Determine primary package ID: `CycloneDDS.NET` or keep `CycloneDDS.Runtime`
2. Add `<IsPackable>true</IsPackable>`
3. Include managed assemblies in `lib/net8.0/`
4. Include native assets from artifacts folder with `PackagePath="runtimes/win-x64/native/"`
5. Configure package to include Core, Runtime, and Schema assemblies
6. Test pack output with NuGet Package Explorer

**Files to Modify:**
- `src/CycloneDDS.Runtime/CycloneDDS.Runtime.csproj` (or chosen primary project)

**Package Structure:**
```
/lib/net8.0/
    CycloneDDS.Core.dll
    CycloneDDS.Runtime.dll
    CycloneDDS.Schema.dll
/runtimes/win-x64/native/
    ddsc.dll
    idlc.exe
    cycloneddsidl.dll
    cycloneddsidlc.dll
    cycloneddsidljson.dll
    ddsperf.exe
    msvcp140*.dll
    vcruntime140*.dll
    concrt140.dll
```

**Success Conditions:**
- [ ] `dotnet pack` succeeds
- [ ] `.nupkg` file created
- [ ] Package contains `/lib/net8.0/*.dll` (all three assemblies)
- [ ] Package contains `/runtimes/win-x64/native/ddsc.dll`
- [ ] Package contains `/runtimes/win-x64/native/idlc.exe`
- [ ] Package contains IDL support DLLs (cycloneddsidl*.dll)
- [ ] Package contains VC++ runtime DLLs
- [ ] Package metadata is correct (ID, version, authors, license)

**Acceptance Tests:**
```powershell
# Test 1: Pack succeeds
dotnet pack src/CycloneDDS.Runtime/CycloneDDS.Runtime.csproj -c Release -o test-pack

# Test 2: Package structure is correct
# Extract .nupkg (it's a zip) and verify folder structure
Expand-Archive test-pack/*.nupkg test-extract -Force
Test-Path test-extract/lib/net8.0/CycloneDDS.Core.dll | Should -Be $true
Test-Path test-extract/runtimes/win-x64/native/ddsc.dll | Should -Be $true
Test-Path test-extract/runtimes/win-x64/native/idlc.exe | Should -Be $true
Test-Path test-extract/runtimes/win-x64/native/cycloneddsidl.dll | Should -Be $true
```

---

## Stage 4: MSBuild Integration (Code Generation)

### NUGET-008: Create MSBuild Targets File

**Description:**  
Create `CycloneDDS.targets` file to be included in the NuGet package for automatic code generation.

**Reference:** [DESIGN.md §3 - MSBuild Integration](./DESIGN.md#3-msbuild-integration-auto-magic-code-generation)

**Prerequisites:**
- NUGET-007 complete (package structure)

**Tasks:**
1. Create `build/targets/CycloneDDS.targets` file
2. Implement RID defaulting logic (default to win-x64)
3. Implement target to locate native tools from package cache
4. Implement code generation target (placeholder/stub initially)
5. Implement native asset copy target
6. Include targets file in package with `PackagePath="buildTransitive/CycloneDDS.targets"`

**Files to Create:**
- `build/targets/CycloneDDS.targets`

**Files to Modify:**
- Main package `.csproj` to include targets file

**Target Structure:**
```xml
<Project>
  <!-- Default RID -->
  <PropertyGroup>
    <CycloneDdsRid Condition="'$(RuntimeIdentifier)'==''">win-x64</CycloneDdsRid>
    <CycloneDdsRid Condition="'$(RuntimeIdentifier)'!=''">$(RuntimeIdentifier)</CycloneDdsRid>
  </PropertyGroup>
  
  <!-- Locate tools -->
  <PropertyGroup>
    <CycloneDdsToolsPath>$(MSBuildThisFileDirectory)../runtimes/$(CycloneDdsRid)/native/</CycloneDdsToolsPath>
  </PropertyGroup>
  
  <!-- Code generation target (stub) -->
  <Target Name="CycloneDdsGenerate" BeforeTargets="CoreCompile">
    <Message Text="CycloneDDS: Code generation will be implemented here" />
  </Target>
  
  <!-- Copy native assets to output -->
  <Target Name="CycloneDdsCopyNative" AfterTargets="Build">
    <ItemGroup>
      <NativeFiles Include="$(CycloneDdsToolsPath)*.dll" />
      <NativeFiles Include="$(CycloneDdsToolsPath)*.exe" />
    </ItemGroup>
    <Copy SourceFiles="@(NativeFiles)" DestinationFolder="$(OutputPath)" SkipUnchangedFiles="true" />
  </Target>
</Project>
```

**Success Conditions:**
- [ ] Targets file is valid XML
- [ ] Included in package under `buildTransitive/`
- [ ] When package installed, targets are imported automatically
- [ ] RID defaults to win-x64 when unspecified
- [ ] Native assets copied to output directory on build

**Acceptance Tests:**
```powershell
# Test 1: Pack includes targets
dotnet pack src/CycloneDDS.Runtime -o test-pack
Expand-Archive test-pack/*.nupkg test-extract -Force
Test-Path test-extract/buildTransitive/CycloneDDS.targets | Should -Be $true

# Test 2: Targets work in consuming project
dotnet new console -o TestConsumer
cd TestConsumer
dotnet add package CycloneDDS.NET --source ../test-pack
dotnet build -v detailed | Should -Match "CycloneDDS"
Test-Path bin/Debug/net8.0/ddsc.dll | Should -Be $true
Test-Path bin/Debug/net8.0/idlc.exe | Should -Be $true
```

### NUGET-009: Implement Code Generation in Targets

**Description:**  
Implement actual code generation logic in MSBuild targets, calling existing CodeGen tooling.

**Reference:** [DESIGN.md §3.1 - Code Generation Workflow](./DESIGN.md#31-code-generation-workflow)

**Prerequisites:**
- NUGET-008 complete (targets file exists)

**Tasks:**
1. Analyze existing `CycloneDDS.targets` in tools/CycloneDDS.CodeGen/
2. Extract generation logic requirements
3. Implement `CycloneDdsGenerate` target to:
   - Discover schema files or trigger CodeGen tool
   - Execute code generation
   - Output to `$(IntermediateOutputPath)CycloneDdsGenerated\`
   - Include generated files in compilation
4. Handle incremental build (initial: run every time)
5. Test in consuming project

**Current Implementation Reference:**
- Existing: `tools/CycloneDDS.CodeGen/CycloneDDS.targets`

**Success Conditions:**
- [ ] Code generation runs automatically on build
- [ ] Generated files appear in obj folder
- [ ] Generated files compiled with project
- [ ] Works in consuming project referencing package
- [ ] Does not generate into source tree
- [ ] Errors in generation fail the build with clear messages

**Acceptance Tests:**
```csharp
// TestConsumer/SampleType.cs
using CycloneDDS.Schema;

[DdsTopic("TestTopic")]
public partial struct TestData
{
    [DdsKey]
    public int Id;
    public string Message;
}

// Build should generate serialization code automatically
```

```powershell
# Test: Build generates code
dotnet build
Test-Path obj/Debug/net8.0/CycloneDdsGenerated/*.cs | Should -Be $true

# Test: Application can use generated code
dotnet run  # Should compile and run without errors
```

### NUGET-010: Design-Time Build Optimization

**Description:**  
Optimize code generation to work well during design-time builds (IntelliSense).

**Reference:** [DESIGN.md §3.3 - Design-Time Build Considerations](./DESIGN.md#33-design-time-build-considerations)

**Prerequisites:**
- NUGET-009 complete (code generation working)

**Tasks:**
1. Analyze impact of generation on IntelliSense performance
2. Decide: always run, skip during design-time, or conditional
3. If skipping: detect `$(DesignTimeBuild)` and skip generation
4. If skipping: ensure generated files from last full build are still included
5. Test IntelliSense experience in Visual Studio and VS Code

**Success Conditions:**
- [ ] IntelliSense response time acceptable (<2 seconds)
- [ ] Generated code visible to IntelliSense
- [ ] No errors in IDE for code using generated types
- [ ] Decision documented in DESIGN.md

**Acceptance Tests:**
- Manual testing in Visual Studio
- Verify IntelliSense shows generated types
- Verify no errors squiggles for valid code

---

## Stage 5: IdlImporter Tool Packaging

### NUGET-011: Configure IdlImporter as Dotnet Tool

**Description:**  
Configure `CycloneDDS.IdlImporter` project to be packable as a dotnet global tool.

**Reference:** [DESIGN.md §4.4 - Packing IdlImporter as Dotnet Tool](./DESIGN.md#44-packing-idlimporter-as-dotnet-tool)

**Prerequisites:**
- NUGET-002 complete (Directory.Build.props)

**Tasks:**
1. Add `<PackAsTool>true</PackAsTool>` to IdlImporter.csproj
2. Set `<ToolCommandName>cyclonedds-idl</ToolCommandName>`
3. Set `<PackageId>CycloneDDS.IdlImporter</PackageId>`
4. Include native dependencies in tool package
5. Update tool code to locate native assets relative to tool path
6. Test local tool installation and execution

**Files to Modify:**
- `tools/CycloneDDS.IdlImporter/CycloneDDS.IdlImporter.csproj`

**Success Conditions:**
- [ ] `dotnet pack` produces tool package
- [ ] `dotnet tool install -g CycloneDDS.IdlImporter --add-source ./artifacts/nuget` succeeds
- [ ] `cyclonedds-idl --help` works
- [ ] Tool can find and invoke native idlc
- [ ] Tool works from any directory

**Acceptance Tests:**
```powershell
# Test 1: Pack as tool
dotnet pack tools/CycloneDDS.IdlImporter -o test-pack

# Test 2: Install tool locally
dotnet tool install --global --add-source test-pack CycloneDDS.IdlImporter

# Test 3: Tool executes
cyclonedds-idl --version
$LASTEXITCODE | Should -Be 0

# Test 4: Tool can import IDL
# Create test IDL file
@"
module Test {
    struct Sample {
        long id;
    };
};
"@ | Out-File test.idl
cyclonedds-idl import test.idl -o output/
Test-Path output/*.cs | Should -Be $true

# Cleanup
dotnet tool uninstall -g CycloneDDS.IdlImporter
```

---

## Stage 6: Continuous Integration Setup

### NUGET-012: GitHub Actions Test Workflow

**Description:**  
Create GitHub Actions workflow to run tests on every PR and push to main.

**Reference:** [DESIGN.md §5.2 - Test Workflow](./DESIGN.md#52-test-workflow)

**Prerequisites:**
- NUGET-005 complete (pack script)

**Tasks:**
1. Create `.github/workflows/` directory
2. Create `test.yml` workflow file
3. Configure triggers (push, pull_request)
4. Add job steps:
   - Checkout with submodules
   - Setup .NET 8
   - Setup CMake
   - Build native assets
   - Build solution
   - Run tests
   - Upload test results
5. Configure environment variables for test native asset discovery
6. Test workflow on branch

**Files to Create:**
- `.github/workflows/test.yml`

**Workflow Key Steps:**
```yaml
- name: Build Native
  run: .\build\native-win.ps1 -Configuration Release
  
- name: Set Native Path
  run: echo "CYCLONEDDS_NATIVE_DIR=${{ github.workspace }}\artifacts\native\win-x64" >> $GITHUB_ENV

- name: Build Solution
  run: dotnet build -c Release

- name: Run Tests
  run: dotnet test -c Release --no-build --logger trx
```

**Success Conditions:**
- [ ] Workflow file is valid YAML
- [ ] Workflow triggers on push and PR
- [ ] All steps execute successfully
- [ ] Tests pass in CI environment
- [ ] Test results uploaded as artifacts
- [ ] Workflow completes in <10 minutes

**Acceptance Tests:**
- Create branch, push to trigger workflow
- Verify workflow runs and passes
- Check uploaded test result artifacts
- Verify native assets are available to tests

### NUGET-013: GitHub Actions Release Workflow

**Description:**  
Create GitHub Actions workflow to build, pack, and publish NuGet packages when a version tag is pushed.

**Reference:** [DESIGN.md §5.3 - Release Workflow](./DESIGN.md#53-release-workflow)

**Prerequisites:**
- NUGET-012 complete (test workflow)

**Tasks:**
1. Create `release.yml` workflow file
2. Configure trigger: `push: tags: v*.*.*`
3. Add job steps:
   - Checkout with fetch-depth: 0 (for NBGV)
   - Setup .NET 8
   - Restore NBGV
   - Build native
   - Build and test solution
   - Pack packages
   - Publish to NuGet.org (using secret API key)
   - Create GitHub Release
   - Attach packages to release
4. Add secret `NUGET_API_KEY` to repository
5. Test workflow with test tag

**Files to Create:**
- `.github/workflows/release.yml`

**Workflow Key Steps:**
```yaml
- name: Pack
  run: dotnet pack -c Release -o artifacts/nuget

- name: Publish to NuGet
  run: dotnet nuget push artifacts/nuget/*.nupkg -s https://api.nuget.org/v3/index.json -k ${{ secrets.NUGET_API_KEY }} --skip-duplicate

- name: Create GitHub Release
  uses: softprops/action-gh-release@v1
  with:
    files: artifacts/nuget/*
```

**Success Conditions:**
- [ ] Workflow triggers only on version tags
- [ ] Pack step produces correct packages
- [ ] Publish step succeeds (with valid API key)
- [ ] GitHub Release created automatically
- [ ] Packages attached to Release

**Acceptance Tests:**
- Create test tag: `git tag v1.0.0-alpha.1-test`
- Push tag and verify workflow runs
- Check NuGet.org for published package (or use test feed first)
- Verify GitHub Release exists with attachments

### NUGET-014: NuGet.org Account and API Key Setup

**Description:**  
Set up NuGet.org account, configure 2FA, create API key, and add to GitHub secrets.

**Reference:** [DESIGN.md §6.2 - NuGet.org Account Setup](./DESIGN.md#62-nugetorg-account-setup)

**Prerequisites:**
- None (manual task)

**Tasks:**
1. Create or verify NuGet.org account
2. Enable Two-Factor Authentication
3. Create API key:
   - Scopes: Push + Push new packages
   - Glob pattern: `CycloneDDS.*`
   - Expiration: 365 days
4. Add API key to GitHub repository secrets as `NUGET_API_KEY`
5. Test API key locally (dry run)
6. Document key rotation process

**Success Conditions:**
- [ ] NuGet.org account active with 2FA enabled
- [ ] API key created with correct scopes
- [ ] API key added to GitHub Secrets
- [ ] Test push succeeds (or dry-run validation)

**Acceptance Tests:**
```powershell
# Test: API key works (dry run)
dotnet nuget push test-pack/*.nupkg --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json --skip-duplicate --dry-run
```

### NUGET-015: Status Badges in README

**Description:**  
Add build status and NuGet version badges to README.md.

**Reference:** [DESIGN.md §5.4 - Badge Display](./DESIGN.md#54-badge-display)

**Prerequisites:**
- NUGET-012 complete (test workflow)
- NUGET-013 complete (release workflow)

**Tasks:**
1. Generate badge URLs for GitHub Actions workflows
2. Generate badge URL for NuGet package version
3. Add badges to README.md header
4. Verify badges display correctly
5. Add badge for license

**Files to Modify:**
- `README.md`

**Badge Examples:**
```markdown
[![Tests](https://github.com/USER/REPO/actions/workflows/test.yml/badge.svg)](...)
[![NuGet](https://img.shields.io/nuget/v/CycloneDDS.NET)](https://www.nuget.org/packages/CycloneDDS.NET/)
[![License](https://img.shields.io/github/license/USER/REPO)](LICENSE)
```

**Success Conditions:**
- [ ] All badges render correctly
- [ ] Badges link to appropriate pages
- [ ] NuGet badge updates when new version published
- [ ] Test badge reflects current build status

---

## Stage 7: Package Validation and Testing

### NUGET-016: Local Package Installation Test

**Description:**  
Create test procedure to validate package installation and functionality before publishing.

**Reference:** [DESIGN.md §11.1 - Local Package Testing](./DESIGN.md#111-local-package-testing)

**Prerequisites:**
- NUGET-007 complete (package structure)
- NUGET-009 complete (code generation)

**Tasks:**
1. Create test script `test-package.ps1`
2. Pack packages locally
3. Create new blank console project
4. Add package from local source
5. Define test data type with schema attributes
6. Build and run test project
7. Verify generated code exists
8. Verify native DLL loads
9. Automate this as validation script

**Files to Create:**
- `build/test-package.ps1`

**Test Project Structure:**
```csharp
// TestInstall/Program.cs
using CycloneDDS.Schema;
using CycloneDDS.Runtime;

[DdsTopic("TestTopic")]
public partial struct TestMessage
{
    [DdsKey]
    public int Id;
    public string Text;
}

// Should compile and run without errors
var participant = DdsParticipant.Create();
var writer = participant.CreateWriter<TestMessage>();
Console.WriteLine("Package works!");
```

**Success Conditions:**
- [ ] Test script creates clean environment
- [ ] Package installs successfully
- [ ] Test project compiles
- [ ] Code generation occurs
- [ ] Test project runs without errors
- [ ] Native DLL found and loaded

**Acceptance Tests:**
```powershell
# Run test script
.\build\test-package.ps1
$LASTEXITCODE | Should -Be 0
```

### NUGET-017: Package Content Inspection

**Description:**  
Verify package contents match specification before first publish.

**Reference:** [DESIGN.md §6.3 - Package Validation](./DESIGN.md#63-package-validation)

**Prerequisites:**
- NUGET-007 complete

**Tasks:**
1. Pack packages locally
2. Install NuGet Package Explorer or use 7-Zip
3. Extract and inspect `.nupkg` file
4. Verify folder structure matches design
5. Verify all metadata fields present
6. Verify dependencies are correct
7. Verify native assets included with correct paths
8. Document inspection checklist

**Inspection Checklist:**
- [ ] `/lib/net8.0/` contains managed assemblies
- [ ] `/runtimes/win-x64/native/` contains DLL and EXE
- [ ] `/buildTransitive/CycloneDDS.targets` exists
- [ ] Package metadata: ID, Version, Authors, License, Repo URL
- [ ] Symbol package (.snupkg) exists
- [ ] Dependencies listed correctly
- [ ] No unexpected files included
- [ ] File sizes reasonable

**Tools:**
- NuGet Package Explorer (Windows GUI)
- `7z x package.nupkg` (command line)
- `dotnet nuget verify` (if available)

**Acceptance Tests:**
```powershell
# Extract and inspect
dotnet pack src/CycloneDDS.Runtime -o test-pack
7z x test-pack/*.nupkg -otest-inspect
Test-Path test-inspect/lib/net8.0/*.dll | Should -Be $true
Test-Path test-inspect/runtimes/win-x64/native/*.dll | Should -Be $true
Test-Path test-inspect/buildTransitive/*.targets | Should -Be $true
```

### NUGET-018: CI Package Installation Test

**Description:**  
Add step to CI workflows that tests installing the newly-built package in a fresh project.

**Reference:** [DESIGN.md §11.2 - CI Package Testing](./DESIGN.md#112-ci-package-testing)

**Prerequisites:**
- NUGET-012 complete (test workflow)
- NUGET-016 complete (test script)

**Tasks:**
1. Add workflow step after pack
2. Create temporary test project
3. Add package from artifacts folder
4. Build test project
5. Run test project (if applicable)
6. Fail workflow if test fails

**Workflow Addition:**
```yaml
- name: Test Package Installation
  run: |
    dotnet new console -o PackageInstallTest
    cd PackageInstallTest
    dotnet add package CycloneDDS.NET --source ../artifacts/nuget
    # Add minimal test code
    dotnet build
    dotnet run
```

**Success Conditions:**
- [ ] Added to test.yml workflow
- [ ] Test runs after successful pack
- [ ] Detects broken packages before publish
- [ ] Fails workflow if installation fails

---

## Stage 8: Documentation and Project Hygiene

### NUGET-019: Add Repository Documentation Files

**Description:**  
Add standard open-source project documentation files.

**Reference:** [DESIGN.md §9.1 - Project Hygiene Files](./DESIGN.md#91-project-hygiene-files)

**Prerequisites:**
- None (foundational)

**Tasks:**
1. Create or update `LICENSE` file (choose: MIT or Apache-2.0)
2. Create `SECURITY.md` with vulnerability reporting instructions
3. Create `CONTRIBUTING.md` with build and contribution guidelines
4. Create `CODE_OF_CONDUCT.md` (use Contributor Covenant template)
5. Create or update `CHANGELOG.md` with release history
6. Update `.gitignore` to exclude build artifacts
7. Create `.editorconfig` for C# style consistency

**Files to Create/Update:**
- `LICENSE`
- `SECURITY.md`
- `CONTRIBUTING.md`
- `CODE_OF_CONDUCT.md`
- `CHANGELOG.md`
- `.gitignore`
- `.editorconfig`

**Success Conditions:**
- [ ] All files present and complete
- [ ] LICENSE matches NuGet package metadata
- [ ] CONTRIBUTING.md includes build instructions
- [ ] SECURITY.md explains reporting process
- [ ] CHANGELOG.md started with version 0.1.0 or appropriate

### NUGET-020: GitHub Repository Configuration

**Description:**  
Configure GitHub repository settings for professional OSS project.

**Reference:** [DESIGN.md §9.2 - GitHub Configuration](./DESIGN.md#92-github-configuration)

**Prerequisites:**
- NUGET-019 complete (docs in place)

**Tasks:**
1. Create issue templates:
   - Bug report template
   - Feature request template
   - Documentation improvement template
2. Create pull request template
3. Configure labels: `bug`, `enhancement`, `breaking`, `good first issue`, `help wanted`
4. Configure branch protection rules for `main`:
   - Require PR reviews
   - Require status checks (Tests workflow must pass)
   - Require branches be up to date
5. Add repository description and topics
6. Enable Discussions (optional)

**Files to Create:**
- `.github/ISSUE_TEMPLATE/bug_report.md`
- `.github/ISSUE_TEMPLATE/feature_request.md`
- `.github/PULL_REQUEST_TEMPLATE.md`

**Success Conditions:**
- [ ] Issue templates appear when creating new issue
- [ ] PR template appears when creating PR
- [ ] Branch protection active on main
- [ ] Labels created and organized
- [ ] Repository well-described

### NUGET-021: Update README for NuGet

**Description:**  
Update README.md with NuGet installation instructions and badges.

**Reference:** [DESIGN.md §13.1 - README.md Updates](./DESIGN.md#131-readmemd-updates)

**Prerequisites:**
- NUGET-015 complete (badges)

**Tasks:**
1. Add installation section with NuGet command
2. Add platform requirements (Windows x64, .NET 8)
3. Add quick-start example showing package usage
4. Add link to full documentation
5. Add section about IdlImporter tool
6. Update build instructions to mention native prereqs
7. Add badges at top

**Files to Modify:**
- `README.md`

**New Sections:**
```markdown
## Installation

```powershell
dotnet add package CycloneDDS.NET
```

## Platform Support
- Windows x64 (current)
- Linux x64 (planned)
- .NET 8.0 or later

## Quick Start
\`\`\`csharp
// Define data type
[DdsTopic("SensorData")]
public partial struct SensorData
{
    [DdsKey] public int Id;
    public double Value;
}

// Publish
var participant = DdsParticipant.Create();
var writer = participant.CreateWriter<SensorData>();
writer.Write(new SensorData { Id = 1, Value = 42.0 });
\`\`\`
```

**Success Conditions:**
- [ ] Installation instructions clear
- [ ] Platform requirements stated
- [ ] Quick-start example works
- [ ] Links to detailed docs
- [ ] Professional appearance

### NUGET-022: Create Package README

**Description:**  
Create `PackageReadme.md` to be included in the NuGet package itself.

**Reference:** [DESIGN.md §13.2 - Package README](./DESIGN.md#132-package-readme)

**Prerequisites:**
- NUGET-021 complete

**Tasks:**
1. Create `PackageReadme.md` file
2. Write brief description
3. Add installation command
4. Add basic usage example
5. Link to full documentation
6. Link to GitHub repository
7. Configure package to include this file

**Files to Create:**
- `PackageReadme.md`

**Files to Modify:**
- Main package `.csproj` to include `<PackageReadmeFile>PackageReadme.md</PackageReadmeFile>`

**Success Conditions:**
- [ ] PackageReadme.md exists and is concise
- [ ] Included in .nupkg
- [ ] Displays on NuGet.org package page
- [ ] Links work

**Acceptance Tests:**
```powershell
# Pack and verify readme included
dotnet pack src/CycloneDDS.Runtime -o test-pack
7z x test-pack/*.nupkg -otest-inspect
Test-Path test-inspect/PackageReadme.md | Should -Be $true
```

---

## Stage 9: First Release

### NUGET-023: Pre-Release Validation

**Description:**  
Perform final validation before pushing first version to NuGet.org.

**Reference:** [DESIGN.md §10 - Release Process](./DESIGN.md#10-release-process-sop)

**Prerequisites:**
- All previous tasks complete

**Tasks:**
1. Run full build and test cycle locally
2. Run package installation test
3. Inspect package contents
4. Test in sample application
5. Review all metadata
6. Verify version number is appropriate (suggest 0.1.0-beta.1)
7. Update CHANGELOG.md with release notes
8. Check all documentation up-to-date

**Validation Checklist:**
- [ ] All tests pass locally
- [ ] Package installs in test project
- [ ] Generated code compiles
- [ ] Native assets load at runtime
- [ ] Metadata correct (license, authors, repo)
- [ ] README complete
- [ ] CHANGELOG updated
- [ ] Version number appropriate

**Success Conditions:**
- [ ] Validation checklist complete
- [ ] No blocking issues found
- [ ] Ready for release

### NUGET-024: First Official Release (v0.1.0-beta.1)

**Description:**  
Execute first official release to NuGet.org.

**Reference:** [DESIGN.md §10.1 - Preparing a Release](./DESIGN.md#101-preparing-a-release)

**Prerequisites:**
- NUGET-023 complete (validation)

**Tasks:**
1. Ensure `version.json` has correct version
2. Create Git tag: `git tag v0.1.0-beta.1`
3. Push tag: `git push origin v0.1.0-beta.1`
4. Monitor GitHub Actions release workflow
5. Verify packages appear on NuGet.org
6. Test installation from NuGet.org
7. Announce release (if appropriate)

**Success Conditions:**
- [ ] Tag created and pushed
- [ ] CI release workflow runs successfully
- [ ] Packages published to NuGet.org
- [ ] Packages installable from NuGet.org
- [ ] GitHub Release created

**Acceptance Tests:**
```powershell
# Test installation from NuGet.org
dotnet new console -o FreshTest
cd FreshTest
dotnet add package CycloneDDS.NET --version 0.1.0-beta.1
dotnet build
```

### NUGET-025: Post-Release Monitoring

**Description:**  
Monitor release for issues and respond to feedback.

**Reference:** [DESIGN.md §10.4 - Post-Release](./DESIGN.md#104-post-release)

**Prerequisites:**
- NUGET-024 complete (release published)

**Tasks:**
1. Watch GitHub Issues for reports
2. Monitor NuGet.org download stats
3. Check package displays correctly on NuGet.org
4. Respond to any immediate issues
5. Document any problems for next release
6. Update version in `version.json` for next development cycle

**Success Conditions:**
- [ ] No critical issues reported within 48 hours
- [ ] Package metadata displays correctly
- [ ] Downloads working
- [ ] Issues triaged and responded to

---

## Stage 10: Future Enhancements (Post-Release)

### NUGET-026: Linux x64 Support (Future)

**Description:**  
Add support for Linux x64 platform.

**Reference:** [DESIGN.md §8 - Multi-Platform Strategy](./DESIGN.md#8-multi-platform-strategy-future-linux-x64)

**Prerequisites:**
- First release complete and stable

**Tasks:**
1. Create `build/native-linux.sh` script
2. Set up Linux build environment (local or CI)
3. Build native assets for linux-x64
4. Update MSBuild targets with OS detection
5. Test on Linux
6. Add Linux CI workflow
7. Update documentation

**Status:** Not started (future work)

### NUGET-027: Incremental Build Support

**Description:**  
Implement MSBuild incremental build for code generation.

**Reference:** [DESIGN.md §3.4 - Incremental Build Support](./DESIGN.md#34-incremental-build-support-future)

**Prerequisites:**
- Initial release stable

**Tasks:**
1. Identify inputs to code generation (schema files, tool version)
2. Implement Inputs/Outputs in MSBuild target
3. Create timestamp/marker files
4. Test incremental behavior
5. Measure performance improvement

**Status:** Not started (future work)

### NUGET-028: Package Signing

**Description:**  
Implement NuGet package signing for enhanced trust.

**Reference:** Not in design doc (enhancement)

**Prerequisites:**
- Stable release cadence established

**Tasks:**
1. Obtain code signing certificate
2. Configure signing in CI
3. Update documentation
4. Test signed packages

**Status:** Not started (optional future work)

---

## Appendix: Testing Matrix

### Package Installation Test Matrix

Test the package installation in various scenarios:

| Scenario | Project Type | RID | Expected Outcome |
|----------|-------------|-----|------------------|
| Basic Console | console | (none) | Defaults to win-x64, builds successfully |
| Specific RID | console | win-x64 | Uses specified RID, builds successfully |
| Class Library | classlib | (none) | Works, but native assets not copied (as expected) |
| Web API | webapi | win-x64 | Works, native assets deployed |
| Test Project | xunit | (none) | Works, tests can run with native deps |

### CI Test Matrix (per Workflow Run)

| Test Category | Scope | Expected Duration |
|---------------|-------|-------------------|
| Unit Tests | All test projects | 1-2 minutes |
| Native Build | CMake build | 3-5 minutes |
| Integration Tests | End-to-end scenarios | 2-3 minutes |
| Package Install Test | Fresh project | 1 minute |
| **Total** | | **7-11 minutes** |

---

## Task Dependencies Graph

```
NUGET-000 (Analysis)
    └─> NUGET-001 (NBGV)
            └─> NUGET-002 (Directory.Build.props)
                    └─> NUGET-003 (Remove hardcoded versions)

NUGET-004 (Native build script)
    └─> NUGET-005 (Pack script)
    └─> NUGET-006 (Artifact staging)
            └─> NUGET-007 (Package structure)
                    └─> NUGET-008 (MSBuild targets)
                            └─> NUGET-009 (Code generation)
                                    └─> NUGET-010 (Design-time optimization)

NUGET-002 → NUGET-011 (IdlImporter tool)

NUGET-005 → NUGET-012 (Test workflow)
                  └─> NUGET-013 (Release workflow)
                            └─> NUGET-014 (NuGet.org setup)
                  └─> NUGET-015 (Badges)

NUGET-007, NUGET-009 → NUGET-016 (Local package test)
                              └─> NUGET-017 (Package inspection)
                              └─> NUGET-018 (CI package test)

NUGET-019 (Docs)
    └─> NUGET-020 (GitHub config)
    └─> NUGET-021 (README update)
            └─> NUGET-022 (Package README)

ALL → NUGET-023 (Pre-release validation)
          └─> NUGET-024 (First release)
                  └─> NUGET-025 (Post-release monitoring)
```

---

**End of Task Details**
