# NuGet Packaging Project - Onboarding Guide

Welcome to the CycloneDDS.NET NuGet packaging project! This guide will help you get started quickly.

---

## What We're Building

**Goal:** Transform the CycloneDDS C# Bindings into a professional, publishable NuGet package called **CycloneDDS.NET** that developers can install and use with zero manual configuration.

**Current State:**
- C# wrapper library over native Cyclone DDS (C library)
- Multiple assemblies: Core, Runtime, Schema
- Internal code generation tool (`CycloneDDS.CodeGen`) that runs during build
- Manual CLI tool (`CycloneDDS.IdlImporter`) for IDL conversion
- Native dependencies from `artifacts/native/win-x64/`:
  - **ddsc.dll** - Main DDS runtime library
  - **idlc.exe** - IDL compiler used during code generation
  - **cycloneddsidl*.dll** - IDL support libraries
  - **VC++ Runtime DLLs** - msvcp140*.dll, vcruntime140*.dll, concrt140.dll
- Works locally, but not packaged for distribution

**Target State:**
- Published on NuGet.org as `CycloneDDS.NET`
- "Install and forget" experience: `dotnet add package CycloneDDS.NET` → everything works
- Automatic code generation for users
- Native dependencies (DLL + tools) included and deployed automatically
- CI/CD pipeline for automated testing and releases
- Professional OSS project with documentation and contributor guidelines

---

## Project Documents (Start Here!)

### Essential Reading (in order)

1. **[Design Talk Document](./NuGet%20Versioning%20and%20CI.md)** - Original design conversation (comprehensive)
   - Read this COMPLETELY to understand the full context
   - Contains detailed explanations of all decisions

2. **[DESIGN.md](./DESIGN.md)** - Structured design document
   - Distills the design talk into organized sections
   - Your primary reference for implementation details

3. **[TASK-DETAIL.md](./TASK-DETAIL.md)** - Detailed task specifications
   - Step-by-step implementation instructions for each task
   - Success criteria and acceptance tests

4. **[TASK-TRACKER.md](./TASK-TRACKER.md)** - Progress tracking
   - Current status of all tasks
   - Check this to see what's done and what's next

### Development Guide

After reading the design documents, read:

- **DEV-GUIDE.md** (if exists) - Development workflow, coding standards, PR process

---

## Repository Structure

### Key Areas for NuGet Packaging Work

```
/FastCycloneDdsCsharpBindings/
│
├── docs/versioning/              ← YOU ARE HERE
│   ├── Design-From-Talk.md       ← Meta-instructions (how to create docs)
│   ├── NuGet Versioning and CI.md ← Original design conversation
│   ├── DESIGN.md                 ← Structured design document
│   ├── TASK-DETAIL.md            ← Detailed task specifications
│   ├── TASK-TRACKER.md           ← Progress tracker
│   └── ONBOARDING.md             ← This file
│
├── src/                          ← Managed C# source projects
│   ├── CycloneDDS.Core/          ← Core DDS interop layer
│   ├── CycloneDDS.Runtime/       ← Runtime APIs (main package)
│   └── CycloneDDS.Schema/        ← Schema attributes and types
│
├── tools/                        ← Build-time and manual CLI tools
│   ├── CycloneDDS.CodeGen/       ← Internal code generator (runs during build)
│   ├── CycloneDDS.IdlImporter/   ← Manual CLI tool for IDL→C# conversion
│   └── CycloneDDS.Compiler.Common/ ← Shared compiler utilities
│
├── tests/                        ← Test projects (xUnit)
│   ├── CycloneDDS.Core.Tests/
│   ├── CycloneDDS.Runtime.Tests/
│   ├── CycloneDDS.CodeGen.Tests/
│   └── (others...)
│
├── cyclonedds/                   ← Git submodule: Native Cyclone DDS (C)
├── cyclonedds-cxx/               ← Git submodule: C++ bindings (reference)
│
├── build/                        ← NEW: Build scripts we'll create
│   ├── native-win.ps1            ← Builds native DLL and tools
│   ├── pack.ps1                  ← Orchestrates build → test → pack
│   └── targets/                  ← NEW: MSBuild targets for NuGet
│       └── CycloneDDS.targets    ← Auto-magic code generation
│
├── artifacts/                    ← NEW: Build outputs (not in git)
│   ├── native/                   ← Native binaries (win-x64, linux-x64)
│   ├── nuget/                    ← Packed .nupkg files
│   └── test-results/             ← Test outputs (TRX files)
│
├── .github/workflows/            ← NEW: CI/CD pipelines
│   ├── test.yml                  ← Run tests on PR/push
│   └── release.yml               ← Build & publish on tag push
│
├── version.json                  ← NEW: NBGV version configuration
├── Directory.Build.props         ← NEW: Shared package metadata
├── build\build-and-test.ps1      ← Build & Test All (Developer Workflow)
└── CycloneDDS.NET.sln          ← Solution file
```

---

## How the System Works

### Development Workflow (Current)

1. Developer builds `CycloneDDS.CodeGen` first
2. Other projects reference CodeGen and run it during build
3. CodeGen:
   - Discovers C# types with `[DdsTopic]` attributes
   - Generates IDL files
   - Calls native `idlc -l json` to parse IDL
   - Generates C# serialization code
4. Generated code compiled with project
5. Runtime uses native `cyclonedds.dll` for DDS operations

### Target Workflow (After NuGet Packaging)

1. User installs NuGet package: `dotnet add package CycloneDDS.NET`
2. User defines data types with schema attributes
3. User builds project:
   - MSBuild targets (from package) run automatically
   - Code generation happens transparently
   - Native DLL copied to output
4. User runs application → everything works

**Key Challenge:** Move build-time tools (CodeGen, idlc) into package and make them work from NuGet cache.

---

## Building the Project (Current State)

### Prerequisites

1. **Windows x64** (Linux support planned but not yet implemented)
2. **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
3. **CMake 3.16+** - Required for building native Cyclone DDS
4. **Visual Studio 2022** (or VS Build Tools) - For native compilation
5. **Git** with submodule support

### First-Time Setup

```powershell
# Clone repository
git clone https://github.com/<YOUR_ORG>/FastCycloneDdsCsharpBindings.git
cd FastCycloneDdsCsharpBindings

# Initialize submodules (native Cyclone DDS source)
git submodule update --init --recursive

# Build native Cyclone DDS
# Option 1: Use existing batch files
.\build\native-win.ps1

# Option 2: Use build/native-win.ps1 which wraps CMake
.\build\native-win.ps1
```

### Build Managed Projects

```powershell
# Build and run tests
.\build\build-and-test.ps1 -Configuration Release

# Or manually:
dotnet build CycloneDDS.NET.sln -c Release
dotnet test CycloneDDS.NET.sln -c Release --no-build
```

### What You'll See

- Native DLL: `artifacts/native/win-x64/ddsc.dll`
- Native tool: `artifacts/native/win-x64/idlc.exe`
- IDL libraries: `artifacts/native/win-x64/cycloneddsidl*.dll`
- VC++ runtime: `artifacts/native/win-x64/msvcp140*.dll`, `vcruntime140*.dll`, etc.
- Managed assemblies: `src/*/bin/Release/net8.0/*.dll`
- Tests pass (multiple xUnit projects)

---

## What You'll Be Implementing

See [TASK-TRACKER.md](./TASK-TRACKER.md) for complete task list. Here's the high-level flow:

### Phase 1: Foundation (Weeks 1-2)
- Set up Nerdbank.GitVersioning for Git-based versioning
- Create `Directory.Build.props` for shared metadata
- Create build scripts (`build/native-win.ps1`, `build/pack.ps1`)

### Phase 2: Packaging (Weeks 2-3)
- Configure projects to produce properly-structured NuGet packages
- Include native assets in package (runtimes/win-x64/native)
- Create MSBuild targets for automatic code generation

### Phase 3: CI/CD (Week 3)
- Create GitHub Actions workflows (test, release)
- Set up NuGet.org account and API keys
- Implement automated package publishing on tag push

### Phase 4: Validation & Release (Week 4)
- Test package installation in fresh projects
- Create documentation (README updates, package readme)
- Execute first beta release

---

## Common Tasks

### Running Tests

```powershell
# All tests
dotnet test -c Release

# Specific project
dotnet test tests/CycloneDDS.Core.Tests -c Release

# With filter
dotnet test --filter "FullyQualifiedName~SerializationTests"
```

### Packing a Package Locally

```powershell
# Pack specific project
dotnet pack src/CycloneDDS.Runtime/CycloneDDS.Runtime.csproj -c Release -o ./test-packages

# Inspect package (use 7-Zip or NuGet Package Explorer)
7z x test-packages/*.nupkg -opackage-contents
```

### Testing a Package Locally

```powershell
# Create test project
mkdir PackageTest
cd PackageTest
dotnet new console

# Add local package source
dotnet nuget add source ../test-packages -n LocalTest

# Install package
dotnet add package CycloneDDS.NET --version 1.0.0-alpha

# Build and run
dotnet build
dotnet run
```

---

## Getting Help

### Resources

1. **Design Documents** - Your primary reference (docs/versioning/)
2. **GitHub Issues** - Check for existing issues or create new ones
3. **Pull Request Discussions** - Review recent PRs for context
4. **Microsoft Docs** - [NuGet Packaging](https://learn.microsoft.com/en-us/nuget/create-packages/creating-a-package-msbuild)
5. **Nerdbank.GitVersioning** - [Official Docs](https://github.com/dotnet/Nerdbank.GitVersioning)

### Questions to Ask

- "What are we trying to achieve with this task?" → Check DESIGN.md
- "How do I implement this?" → Check TASK-DETAIL.md
- "What's already done?" → Check TASK-TRACKER.md
- "How does X currently work?" → Explore existing code, especially `tools/CycloneDDS.CodeGen/`

---

## Development Workflow

### Before Starting a Task

1. Read task details in TASK-DETAIL.md
2. Understand success criteria
3. Check dependencies (prerequisite tasks)
4. Create a feature branch: `git checkout -b feature/NUGET-XXX-task-name`

### While Working

1. Follow acceptance tests as a guide
2. Test incrementally (don't wait until the end)
3. Update TASK-TRACKER.md when complete
4. Document deviations or issues

### Submitting Work

1. Ensure all acceptance tests pass
2. Update documentation if needed
3. Create pull request with:
   - Clear description
   - Link to task (NUGET-XXX)
   - Test results
4. Request review

---

## Project Conventions

### Naming

- **Package IDs:** `CycloneDDS.*` (e.g., `CycloneDDS.NET`, `CycloneDDS.IdlImporter`)
- **Task IDs:** `NUGET-XXX` (3-digit zero-padded)
- **Branches:** `feature/NUGET-XXX-short-description` or `fix/issue-description`
- **Git Tags:** `vX.Y.Z` (e.g., `v0.1.0-beta.1`)

### Code Style

- C# using `.editorconfig` (will be created)
- Follow existing code conventions
- XML documentation comments on public APIs

### Commit Messages

```
[NUGET-XXX] Brief description of change

Longer explanation if needed. Reference task detail document
for full context.

Closes #123 (if applicable)
```

---

## Troubleshooting

### "idlc not found" during build

**Cause:** Native tools not built or not in output directory

**Solution:**
```powershell
# Rebuild native components
.\build\native-win.ps1

# Verify outputs
Test-Path artifacts\native\win-x64\idlc.exe
Test-Path artifacts\native\win-x64\ddsc.dll
```

### "Cannot find CodeGen tool" during build

**Cause:** CodeGen must be built before other projects

**Solution:**
```powershell
# Build CodeGen first
dotnet build tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj -c Release

# Then build solution
dotnet build CycloneDDS.NET.sln -c Release
```

### Tests fail with "DLL not found"

**Cause:** Tests need native DLL in output or PATH

**Solution:**
```powershell
# Copy all native DLLs to test output
Copy-Item artifacts\native\win-x64\*.dll tests\CycloneDDS.Core.Tests\bin\Release\net8.0\

# Or set environment variable
$env:PATH = "$(pwd)\artifacts\native\win-x64;$env:PATH"
dotnet test
```

### CMake configuration fails

**Cause:** Missing Visual Studio build tools or CMake

**Solution:**
1. Install VS Build Tools 2022
2. Install CMake 3.16+
3. Ensure `cmake` is in PATH
4. Run from "Developer PowerShell for VS 2022"

---

## Current Project Status

**As of:** February 14, 2026

- **Phase:** Planning Complete
- **Next Milestone:** Stage 1 (Versioning Infrastructure)
- **Next Task:** NUGET-000 (Project Analysis)

**What Works:**
- ✅ Local development build
- ✅ All tests passing
- ✅ Code generation during build
- ✅ Manual packaging (via dotnet pack)

**What Doesn't Work Yet:**
- ❌ Automated versioning
- ❌ Proper NuGet package structure
- ❌ Auto-magic code generation from package
- ❌ CI/CD pipelines
- ❌ Published to NuGet.org

**Your Mission:**
Help us get from "works locally" to "published and production-ready"!

---

## Next Steps

1. **Read the design documents** (especially [DESIGN.md](./DESIGN.md))
2. **Build the project locally** to ensure your environment works
3. **Review TASK-TRACKER.md** to see current status
4. **Pick up the next task** (or ask maintainers for assignment)
5. **Ask questions** if anything is unclear

---

## Contact

- **GitHub Issues:** Technical questions, bug reports
- **Pull Requests:** Code contributions, documentation fixes
- **Discussions:** General questions, design feedback

---

## Important Reminders

⚠️ **Read ALL documentation before starting implementation**  
⚠️ **Follow the task order** - many tasks have dependencies  
⚠️ **Test incrementally** - don't wait until everything is done  
⚠️ **Update TASK-TRACKER.md** as you complete tasks  
⚠️ **Ask for help early** if stuck - don't spin your wheels  

---

Welcome aboard, and happy coding! 🚀
