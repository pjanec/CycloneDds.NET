# NuGet Packaging and Release Strategy Design

## Project Overview

**Project Name:** CycloneDDS.NET  
**Description:** A modern, high-performance .NET 8 wrapper library for Eclipse Cyclone DDS (native C library) with zero-allocation serialization and code generation capabilities.

**Current Architecture:**
- Managed C# assemblies (.NET 8): Core, Runtime, Schema
- Native DLLs: Cyclone DDS libraries (CMake-based, built from submodule)
  - Runtime: ddsc.dll (main DDS library)
  - IDL tooling: cycloneddsidl.dll, cycloneddsidlc.dll, cycloneddsidljson.dll
  - VC++ Runtime: msvcp140*.dll, vcruntime140*.dll, concrt140.dll
- Internal native tools:
  - idlc.exe (required during build for code generation)
  - ddsperf.exe (performance testing utility)
- Manual CLI tools: `CycloneDDS.IdlImporter` (.NET console app)
- Multiple xUnit test projects

**Native Artifacts Location:** `cyclone-compiled/bin/`

**Target Platforms:**
- **Now:** Windows x64 only
- **Future:** Linux x64 support planned

---

## 1. Versioning Strategy

### 1.1 Single Source of Truth: Git-Based Versioning

**Tool:** Nerdbank.GitVersioning (NBGV)

**Rationale:**
- One version number for all packages and assemblies
- Git tags drive releases (`v1.2.3` → produces `1.2.3`)
- CI/CD friendly and reproducible
- Supports SemVer including pre-release suffixes

**Files:**
- **`version.json`** (repo root) - Primary version configuration
- **`Directory.Build.props`** (repo root) - Shared package metadata (NO version numbers)

**Version Flow:**
1. Developer edits `version.json` to set next version
2. Create and push Git tag `vX.Y.Z`
3. CI builds from tag → produces exactly `X.Y.Z`
4. Non-tag builds → `X.Y.Z-alpha.g<commit-sha>`

### 1.2 Centralized Package Metadata

All common metadata in `Directory.Build.props`:
- `Authors`, `Company`
- `RepositoryUrl`, `RepositoryType`
- `PackageLicenseExpression` (e.g., MIT, Apache-2.0)
- `PackageReadmeFile`, `PackageIcon`
- `PackageTags`, `Description` (can be overridden per project)
- SourceLink configuration
- `IncludeSymbols=true`, `SymbolPackageFormat=snupkg`
- `ContinuousIntegrationBuild=true` (in CI only)

Individual `.csproj` files should NOT contain version numbers or duplicate metadata.

### 1.3 SemVer Versioning Rules

**Major version (X.0.0):** Breaking API changes
**Minor version (0.X.0):** New features, backward compatible
**Patch version (0.0.X):** Bug fixes only

---

## 2. Package Architecture

### 2.1 Package Split Strategy

**Primary Package: `CycloneDDS.NET`** (or `CycloneDDS.Runtime`)
- Managed assemblies: `CycloneDDS.Core.dll`, `CycloneDDS.Runtime.dll`, `CycloneDDS.Schema.dll`
- Native DLL: `cyclonedds.dll` (Windows) / `libcyclonedds.so` (Linux later)
- Internal build tool: `idlc.exe` (Windows) / `idlc` (Linux later)
- MSBuild targets for automatic code generation
- **Consumer experience:** `<PackageReference Include="CycloneDDS.NET" Version="1.0.0" />` → everything works

**Secondary Package: `CycloneDDS.IdlImporter` (dotnet tool)**
- Manual CLI tool for IDL → C# conversion
- Self-contained with native dependencies
- **Consumer experience:** `dotnet tool install -g CycloneDDS.IdlImporter`

### 2.2 NuGet Folder Structure (CycloneDDS.NET)

```
/lib/net8.0/
    CycloneDDS.Core.dll
    CycloneDDS.Runtime.dll
    CycloneDDS.Schema.dll

/buildTransitive/
    CycloneDDS.targets
    CycloneDDS.props (optional)

/runtimes/win-x64/native/
    ddsc.dll                    (main DDS runtime)
    idlc.exe                    (IDL compiler tool)
    cycloneddsidl.dll           (IDL library)
    cycloneddsidlc.dll          (IDL compiler library)
    cycloneddsidljson.dll       (IDL JSON support)
    ddsperf.exe                 (performance tool)
    msvcp140.dll                (VC++ runtime)
    msvcp140_1.dll
    msvcp140_2.dll
    msvcp140_atomic_wait.dll
    msvcp140_codecvt_ids.dll
    vcruntime140.dll
    vcruntime140_1.dll
    concrt140.dll

/runtimes/linux-x64/native/  (future)
    libddsc.so
    idlc
    libcycloneddsidl.so
    libcycloneddsidlc.so
    libcycloneddsidljson.so
    (Linux native dependencies)
```

**Why `buildTransitive/`?**
- Ensures MSBuild targets apply to consuming projects AND their dependencies
- Required for code generation to work automatically

**Why RID-specific layout?**
- Enables multi-platform support without code changes
- Standard NuGet runtime asset resolution
- Clear separation of platform-specific binaries

### 2.3 Native Asset Deployment Strategy

**Build-time:**
- MSBuild targets locate native tools from package cache
- Execute `idlc.exe` for code generation before compilation
- `idlc.exe` requires: cycloneddsidl.dll, cycloneddsidlc.dll, cycloneddsidljson.dll

**Runtime:**
- Native DLLs must be in application output directory
- MSBuild targets copy all native assets from `runtimes/<rid>/native/` to `$(OutputPath)`
- Runtime P/Invokes: `ddsc.dll` (main DDS library)
- The DllImport in DdsApi.cs uses name `"ddsc"` which resolves to `ddsc.dll` on Windows

---

## 3. MSBuild Integration (Auto-Magic Code Generation)

### 3.1 Code Generation Workflow

**Goal:** Users install package and build → code generation happens automatically

**Current Behavior:**
- `CycloneDDS.CodeGen` tool runs during build
- Generates C# serialization code from schema annotations
- Calls native `idlc -l json` to parse IDL
- Generated files become part of compilation

**Target Behavior (Package):**
1. User installs `CycloneDDS.NET` package
2. On build, MSBuild target runs automatically
3. Locates `idlc.exe` from package's `runtimes/<rid>/native/`
4. Generates C# code into `$(IntermediateOutputPath)\CycloneDdsGenerated\`
5. Includes generated `.cs` files in compilation
6. Copies native DLL + idlc to output for runtime

### 3.2 MSBuild Targets Design

**File:** `buildTransitive/CycloneDDS.targets`

**Key Responsibilities:**
1. **Default RuntimeIdentifier** (RID) handling
   - If `$(RuntimeIdentifier)` is empty → default to `win-x64`
   - Future: detect OS to choose `win-x64` or `linux-x64`
   
2. **Locate Packaged Tools**
   - `$(MSBuildThisFileDirectory)` → package's buildTransitive folder
   - Native tools folder: `$(MSBuildThisFileDirectory)../runtimes/$(RuntimeIdentifier)/native/`
   
3. **Code Generation Target**
   - Name: `CycloneDdsGenerate`
   - Timing: `BeforeTargets="CoreCompile"`
   - Creates output directory: `$(IntermediateOutputPath)CycloneDdsGenerated\`
   - Executes code generator (either in-package or via exe)
   - Includes generated files: `<Compile Include="$(IntermediateOutputPath)CycloneDdsGenerated\**\*.cs" />`
   
4. **Native Asset Deployment Target**
   - Name: `CycloneDdsCopyNativeAssets`
   - Timing: `AfterTargets="Build"`
   - Copies `runtimes/<rid>/native/*` → `$(OutputPath)`
   - Ensures native DLL and idlc are available at runtime

### 3.3 Design-Time Build Considerations

**Challenge:** Code generation during design-time builds can slow IntelliSense

**Solution Options:**
1. Run generation during design-time (ensures IntelliSense sees generated code)
2. Skip during design-time: `<CycloneDdsGenerate Condition="'$(DesignTimeBuild)'!='true'" />`
3. Generate into source tree for IntelliSense, but keep as intermediate output officially

**Recommendation:** Start with option 1 (always generate) for simplicity. Optimize later if performance issues arise.

### 3.4 Incremental Build Support (Future)

**Current:** Runs every build
**Future:** Add MSBuild `Inputs`/`Outputs` tracking

```xml
<Target Name="CycloneDdsGenerate"
        BeforeTargets="CoreCompile"
        Inputs="@(CycloneDdsSchemaFiles);$(MSBuildProjectFile)"
        Outputs="$(IntermediateOutputPath)CycloneDdsGenerated\.timestamp">
  <!-- Generation logic -->
  <Touch Files="$(IntermediateOutputPath)CycloneDdsGenerated\.timestamp" AlwaysCreate="true" />
</Target>
```

---

## 4. Build and Packaging Workflow

### 4.1 Artifact Staging Architecture

**Problem:** Native outputs from CMake need to be included in NuGet packages

**Solution:** Standardized artifact folder structure

```
/artifacts/
    /native/
        /win-x64/
            cyclonedds.dll
            idlc.exe
        /linux-x64/  (future)
            libcyclonedds.so
            idlc
    /nuget/
        CycloneDDS.NET.1.0.0.nupkg
        CycloneDDS.NET.1.0.0.snupkg
        CycloneDDS.IdlImporter.1.0.0.nupkg
    /test-results/
        *.trx
```

### 4.2 Build Script Architecture

**Philosophy:** Single scripts work both locally and in CI

**Scripts:**

1. **`build/native-win.ps1`**
   - Builds native Cyclone DDS via CMake
   - Outputs to `artifacts/native/win-x64/`
   - Accepts configuration parameter (Debug/Release)

2. **`build/native-linux.sh`** (future)
   - Same as above for Linux

3. **`build/pack.ps1`**
   - Builds native (calls native-win.ps1)
   - Restores managed dependencies
   - Builds solution (`dotnet build -c Release`)
   - Runs tests (`dotnet test -c Release --no-build`)
   - Packs NuGet packages (`dotnet pack -c Release -o artifacts/nuget`)

4. **`build/publish.ps1`** (optional)
   - Pushes packages to NuGet.org
   - Used by CI or manual releases

### 4.3 Packing Managed Assemblies + Native Assets

**In `CycloneDDS.Runtime.csproj` (main package):**

```xml
<PropertyGroup>
  <IsPackable>true</IsPackable>
  <PackageId>CycloneDDS.NET</PackageId>
  <!-- Version comes from NBGV, not here -->
</PropertyGroup>

<!-- Include MSBuild targets -->
<ItemGroup>
  <None Include="..\..\build\targets\CycloneDDS.targets" 
        Pack="true" 
        PackagePath="buildTransitive\CycloneDDS.targets" />
</ItemGroup>

<!-- Include all native assets for Windows -->
<ItemGroup>
  <!-- Runtime DLL -->
  <None Include="..\..\artifacts\native\win-x64\ddsc.dll"
        Pack="true"
        PackagePath="runtimes\win-x64\native\" />
  
  <!-- Build-time tools -->
  <None Include="..\..\artifacts\native\win-x64\idlc.exe"
        Pack="true"
        PackagePath="runtimes\win-x64\native\" />
  
  <!-- IDL libraries (required by idlc.exe) -->
  <None Include="..\..\artifacts\native\win-x64\cycloneddsidl.dll"
        Pack="true"
        PackagePath="runtimes\win-x64\native\" />
  <None Include="..\..\artifacts\native\win-x64\cycloneddsidlc.dll"
        Pack="true"
        PackagePath="runtimes\win-x64\native\" />
  <None Include="..\..\artifacts\native\win-x64\cycloneddsidljson.dll"
        Pack="true"
        PackagePath="runtimes\win-x64\native\" />
  
  <!-- VC++ Runtime dependencies -->
  <None Include="..\..\artifacts\native\win-x64\msvcp140*.dll"
        Pack="true"
        PackagePath="runtimes\win-x64\native\" />
  <None Include="..\..\artifacts\native\win-x64\vcruntime140*.dll"
        Pack="true"
        PackagePath="runtimes\win-x64\native\" />
  <None Include="..\..\artifacts\native\win-x64\concrt140.dll"
        Pack="true"
        PackagePath="runtimes\win-x64\native\" />
  
  <!-- Optional: Performance tool -->
  <None Include="..\..\artifacts\native\win-x64\ddsperf.exe"
        Pack="true"
        PackagePath="runtimes\win-x64\native\" />
</ItemGroup>

<!-- Future: Linux assets -->
<ItemGroup Condition="Exists('..\..\artifacts\native\linux-x64')">
  <None Include="..\..\artifacts\native\linux-x64\*"
        Pack="true"
        PackagePath="runtimes\linux-x64\native\" />
</ItemGroup>
```

### 4.4 Packing IdlImporter as Dotnet Tool

**In `CycloneDDS.IdlImporter.csproj`:**

```xml
<PropertyGroup>
  <IsPackable>true</IsPackable>
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>cyclonedds-idl</ToolCommandName>
  <PackageId>CycloneDDS.IdlImporter</PackageId>
</PropertyGroup>

<!-- Self-contained: include native deps in tool package -->
<ItemGroup>
  <None Include="..\..\artifacts\native\win-x64\*"
        Pack="true"
        PackagePath="tools\net8.0\any\native\" />
</ItemGroup>
```

**Runtime Resolution:**
- Tool code uses `AppContext.BaseDirectory` or `Process.GetCurrentProcess().MainModule.FileName`
- Locates native assets relative to tool installation

---

## 5. Continuous Integration (GitHub Actions)

### 5.1 CI Strategy

**Triggers:**
- **On tag push `v*`:** Full build + publish to NuGet.org
- **On PR / push to main:** Build + test (validation only)

**Matrix (future):**
```yaml
strategy:
  matrix:
    os: [windows-latest, ubuntu-latest]
```

### 5.2 Test Workflow

**File:** `.github/workflows/test.yml`

**Responsibilities:**
1. Checkout with submodules
2. Build native dependencies (CMake)
3. Build managed solution
4. Run all xUnit tests
5. Upload test results (TRX) as artifacts
6. Display test status badge in README

**Key Requirements:**
- Tests depend on native DLL + idlc → must build native first
- Set `CYCLONEDDS_NATIVE_DIR` environment variable pointing to staged native outputs
- Add to PATH so tools can find idlc at runtime

**Test Discovery:**
- Native assets must be discoverable during tests
- Options:
  1. Copy native artifacts to each test project output
  2. Set environment variable + modify PATH
  3. Use NuGet package references (even during development)

**Recommendation:** Option 2 (env var) for CI; option 1 (copy) for local dev

### 5.3 Release Workflow

**File:** `.github/workflows/release.yml`

**Trigger:** Git tag `v*.*.*`

**Steps:**
1. Checkout with submodules + full history (`fetch-depth: 0` for NBGV)
2. Setup .NET 8
3. Setup CMake
4. Restore NBGV tool
5. Build native (Windows x64)
6. Build solution (`dotnet build -c Release`)
7. Run tests (`dotnet test -c Release --no-build`)
8. Pack NuGet packages (`dotnet pack -c Release`)
9. Publish to NuGet.org (`dotnet nuget push` with `NUGET_API_KEY`)
10. Create GitHub Release
11. Attach `.nupkg` and `.snupkg` files to release

**Secrets Required:**
- `NUGET_API_KEY` - scoped to `CycloneDDS.*` packages

### 5.4 Badge Display

**In README.md:**
```markdown
[![Tests](https://github.com/<OWNER>/<REPO>/actions/workflows/test.yml/badge.svg)](...)
[![NuGet](https://img.shields.io/nuget/v/CycloneDDS.NET)](...)
```

---

## 6. NuGet.org Publishing Requirements

### 6.1 Package Metadata (Professional Standard)

**Required Fields:**
- ✅ `PackageId`: `CycloneDDS.NET` (unique, stable)
- ✅ `Version`: from NBGV (Git tag)
- ✅ `Authors`: Project maintainer name(s)
- ✅ `Description`: Clear, concise summary
- ✅ `RepositoryUrl`: GitHub repo URL
- ✅ `RepositoryType`: `git`
- ✅ `PackageLicenseExpression`: `Apache-2.0` or `MIT`
- ✅ `PackageReadmeFile`: `README.md`
- ✅ `PackageIcon`: `icon.png` (128x128 or larger)
- ✅ `PackageTags`: `dds;cyclone;realtime;pub-sub;iot`

**Symbol Packages:**
- ✅ `IncludeSymbols=true`
- ✅ `SymbolPackageFormat=snupkg`
- ✅ SourceLink configured (`Microsoft.SourceLink.GitHub`)

**Continuous Integration Build:**
- ✅ Set `ContinuousIntegrationBuild=true` in CI (enables deterministic builds)

### 6.2 NuGet.org Account Setup

1. **Create account** at nuget.org
2. **Enable 2FA** (required for package publishing)
3. **Create API key**:
   - Scope: `Push` + `Push new packages and package versions`
   - Glob pattern: `CycloneDDS.*`
   - Expiration: 1 year (renewable)
4. **Add as GitHub Secret**: `NUGET_API_KEY`

### 6.3 Package Validation

**Before First Publish:**
- Test pack locally: `dotnet pack -o test-pack`
- Inspect `.nupkg` with NuGet Package Explorer
- Verify folder structure (lib, runtimes, buildTransitive)
- Test installation in blank project
- Verify code generation works
- Verify native DLL loads at runtime

---

## 7. Consumer Experience

### 7.1 Library Installation (Target Experience)

**User Action:**
```xml
<PackageReference Include="CycloneDDS.NET" Version="1.0.0" />
```

**What Happens:**
1. NuGet restores package to local cache
2. Managed assemblies added to compilation
3. MSBuild targets imported automatically
4. On build:
   - Code generation runs (transparent to user)
   - Generated C# code included in compilation
   - Native DLL + idlc copied to output directory
5. Application runs → P/Invoke finds native DLL

**Zero manual steps** - true "install and forget"

### 7.2 CLI Tool Installation

**User Action:**
```bash
dotnet tool install -g CycloneDDS.IdlImporter
```

**Usage:**
```bash
cyclonedds-idl import MyTypes.idl -o Output/
```

---

## 8. Multi-Platform Strategy (Future: Linux x64)

### 8.1 RID Resolution in MSBuild Targets

**Current:** Default to `win-x64`

**Future Logic:**
```xml
<PropertyGroup>
  <CycloneDdsRuntimeIdentifier Condition="'$(RuntimeIdentifier)'!=''">$(RuntimeIdentifier)</CycloneDdsRuntimeIdentifier>
  <CycloneDdsRuntimeIdentifier Condition="'$(CycloneDdsRuntimeIdentifier)'=='' AND '$([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform($([System.Runtime.InteropServices.OSPlatform]::Windows)))' == 'true'">win-x64</CycloneDdsRuntimeIdentifier>
  <CycloneDdsRuntimeIdentifier Condition="'$(CycloneDdsRuntimeIdentifier)'=='' AND '$([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform($([System.Runtime.InteropServices.OSPlatform]::Linux)))' == 'true'">linux-x64</CycloneDdsRuntimeIdentifier>
</PropertyGroup>
```

### 8.2 Linux-Specific Considerations

**Native Build:**
- Use CMake with Linux toolchain
- Output to `artifacts/native/linux-x64/`

**Executable Permissions:**
- Linux executables must have execute bit
- NuGet may not preserve permissions
- Solution: In MSBuild targets, add `chmod +x` on first use:
  ```xml
  <Exec Condition="'$(OS)'=='Unix'" Command="chmod +x $(IdlcExePath)" />
  ```

**Library Loading:**
- Use `DllImport` with library base name: `"ddsc"`
- Windows: resolves to `ddsc.dll`
- Linux: resolves to `libddsc.so`
- Current code already uses correct pattern: `[DllImport("ddsc")]`

---

## 9. Repository Structure & Maintenance

### 9.1 Project Hygiene Files

**Root-level files to add:**
- ✅ `LICENSE` - Apache 2.0 or MIT
- ✅ `SECURITY.md` - Vulnerability reporting process
- ✅ `CONTRIBUTING.md` - How to contribute, build instructions
- ✅ `CODE_OF_CONDUCT.md` - Use Contributor Covenant
- ✅ `CHANGELOG.md` - Release notes (or use GitHub Releases)
- ✅ `.gitignore` - Exclude build artifacts, bin/obj, etc.
- ✅ `.editorconfig` - C# style consistency

### 9.2 GitHub Configuration

**Issue Templates:**
- Bug report
- Feature request
- Documentation improvement

**PR Template:**
- Checklist: tests added, documentation updated, version impact considered
- Link to related issue

**Labels:**
- `bug`, `enhancement`, `breaking`, `documentation`
- `good first issue`, `help wanted`

**Branch Protection (main):**
- Require PR reviews
- Require status checks (Tests must pass)
- Require branch to be up to date
- Disallow force push

### 9.3 Documentation Structure

```
/docs/
    /versioning/
        DESIGN.md (this file)
        TASK-DETAIL.md
        TASK-TRACKER.md
        ONBOARDING.md
    /api/
        API-REFERENCE.md
    README.md
```

---

## 10. Release Process (SOP)

### 10.1 Preparing a Release

1. **Update CHANGELOG.md** with release notes
2. **Review and merge PRs** targeting the release
3. **Run full test suite locally**
4. **Update `version.json`** if needed (or let NBGV auto-increment)
5. **Create Git tag**: `git tag v1.2.3`
6. **Push tag**: `git push origin v1.2.3`

### 10.2 Automated Release (CI)

1. GitHub Actions detects tag push
2. Workflow runs: build → test → pack → publish
3. Packages appear on NuGet.org within minutes
4. GitHub Release created automatically

### 10.3 Manual Release (Fallback)

```powershell
# Build and pack
.\build\pack.ps1

# Publish to NuGet
dotnet nuget push artifacts\nuget\*.nupkg --source https://api.nuget.org/v3/index.json --api-key $env:NUGET_API_KEY
```

### 10.4 Post-Release

1. **Verify package** on NuGet.org (metadata, download)
2. **Test installation** in fresh project
3. **Announce release** (GitHub Discussions, social media, etc.)
4. **Monitor issues** for breakage reports

---

## 11. Testing Strategy for Packaging

### 11.1 Local Package Testing

**Before pushing to NuGet.org:**

```powershell
# Pack locally
dotnet pack -c Release -o ./test-packages

# Create test project
mkdir PackageTest
cd PackageTest
dotnet new console

# Add local package source
dotnet nuget add source ../test-packages -n LocalTest

# Install package
dotnet add package CycloneDDS.NET --version 1.0.0-alpha1

# Build and run
dotnet build
dotnet run
```

### 11.2 CI Package Testing

**Add workflow step:**
```yaml
- name: Test Package Installation
  run: |
    dotnet new console -o PackageTest
    cd PackageTest
    dotnet add package CycloneDDS.NET --source ../artifacts/nuget --version ${{ steps.nbgv.outputs.NuGetPackageVersion }}
    dotnet build
```

---

## 12. Rollback and Hotfix Strategy

### 12.1 Unlisting a Bad Package

**If critical bug found after publish:**
1. Unlist package version on NuGet.org (doesn't delete, but hides from search)
2. Fix bug
3. Publish patch version (e.g., 1.2.4)
4. Announce issue and resolution

**Note:** Cannot delete packages from NuGet.org once published

### 12.2 Hotfix Workflow

1. Create branch from release tag: `git checkout -b hotfix/1.2.4 v1.2.3`
2. Apply fix
3. Update `version.json` to `1.2.4`
4. Commit and tag: `git tag v1.2.4`
5. Push tag → CI releases

---

## 13. Documentation Requirements

### 13.1 README.md Updates

**Add sections:**
- Installation instructions (NuGet package reference)
- Platform requirements (Windows x64, .NET 8)
- Quick-start example
- Link to API documentation
- Badges: Build status, NuGet version, License

### 13.2 Package README

**Create `PackageReadme.md`** (included in package):
- Brief description
- Installation command
- Basic usage example
- Link to full documentation
- Link to GitHub repo

### 13.3 API Documentation

**Options:**
1. DocFX - generates static site from XML comments
2. GitHub Wiki
3. Inline XML comments + NuGet package symbols

**Recommendation:** Start with good XML comments + GitHub README

---

## Summary of Key Decisions

| Decision Point | Choice | Rationale |
|----------------|--------|-----------|
| Versioning Tool | Nerdbank.GitVersioning | Git-tag driven, CI-friendly, standard in .NET |
| Package Split | Single main package + tool package | Simplicity; most users need runtime only |
| Native Asset Strategy | RID-specific package layout | Enables multi-platform, standard NuGet pattern |
| Code Generation | Automatic via MSBuild targets | "Install and forget" user experience |
| RID Default | win-x64 now, OS-detection later | Pragmatic; can extend without breaking changes |
| CLI Tool | Dotnet tool | First-class .NET experience, self-contained |
| CI Platform | GitHub Actions | Free for OSS, integrated with repo |
| Build Script Philosophy | Same scripts locally and in CI | Reproducibility, easier debugging |
| Test Strategy | Build native first, set env vars | Tests require native dependencies |

---

## Next Steps

See [TASK-TRACKER.md](./TASK-TRACKER.md) for implementation phases and detailed task breakdown.
