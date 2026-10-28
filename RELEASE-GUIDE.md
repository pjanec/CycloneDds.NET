# Release Guide

This document provides step-by-step instructions for releasing CycloneDDS.NET to NuGet.org.

## Prerequisites

Before you begin, ensure you have:

1. **Maintainer Access** to the GitHub repository
2. **NuGet API Key** with push permissions for the CycloneDDS.NET package
3. **Development Environment** with:
   - **.NET 10 SDK** — the code uses C# 13 language features, so building requires it; the produced binaries still target `net8.0`. (The .NET 8 runtime is also handy for running the `net8.0` tests.)
   - **CMake 3.16+**
   - **Windows:** PowerShell and Visual Studio 2022 (*C++ Desktop Development* workload) for native compilation.
   - **Linux:** a C toolchain and `patchelf` — `sudo apt-get install -y cmake build-essential patchelf`.
   - Git command-line tools

## Building & Testing a Multi-Platform Package Locally

The published `CycloneDDS.NET` and `CycloneDDS.NET.DdsMonitor` packages ship native
assets for **both** `win-x64` and `linux-x64`. Every green CI run already produces
these as artifacts, so **you rarely need to build them yourself** — the options
below are ordered easiest first.

> **How the cross-platform package is assembled.** The pack step includes whatever
> native artifacts are present under `artifacts/native/<rid>/` (Windows-only entries
> are `Exists`-guarded), so a cross-platform package needs *both* platforms' natives
> on one pack host. Because the packages bundle the Windows apphost `.exe` launchers
> for the build-time tools, the final cross-platform pack runs on **Windows**.
> Packing on Linux is fully supported but yields a `linux-x64`-only package.

CI publishes three artifacts per run: **`nuget-packages`** (the finished
cross-platform `.nupkg` + `.snupkg`), **`native-win-x64`**, and
**`native-linux-x64`**. Download them with the GitHub CLI (`gh`, recommended —
`winget install GitHub.cli` on Windows) or from the Actions run page.

### Option A — Download the finished package from CI (easiest, no build)

```bash
# Grab the cross-platform packages from the latest green run on main:
gh run download --repo pjanec/CycloneDds.NET -n nuget-packages -D artifacts/nuget
build/test-package.sh          # smoke-test them on this OS  (PowerShell: .\build\test-package.ps1)
```

Then publish `artifacts/nuget/*.nupkg` manually (see *Publishing* below). Run
`test-package` on both a Windows and a Linux box against the same package before
publishing.

### Option B — Assemble the package locally without a C++ toolchain

Download **both** natives from CI and pack — no Visual Studio / CMake needed, just
the .NET 10 SDK:

```powershell
gh run download --repo pjanec/CycloneDds.NET -n native-win-x64   -D artifacts\native\win-x64
gh run download --repo pjanec/CycloneDds.NET -n native-linux-x64 -D artifacts\native\linux-x64
dotnet build CycloneDDS.NET.Core.slnf -c Release
dotnet pack src\CycloneDDS.Runtime\CycloneDDS.Runtime.csproj      -c Release --no-build -o artifacts\nuget
dotnet pack tools\DdsMonitor\DdsMonitor.Blazor\DdsMonitor.csproj  -c Release --no-build -o artifacts\nuget
.\build\test-package.ps1
```

### Option C — Full local build from source

```powershell
# Windows (needs VS 2022 C++ workload + CMake): builds win native, downloads linux native, packs both
.\build\native-win.ps1
gh run download --repo pjanec/CycloneDds.NET -n native-linux-x64 -D artifacts\native\linux-x64
.\build\pack.ps1
.\build\test-package.ps1
```

```bash
# Linux (needs cmake/gcc/patchelf): produces a linux-x64-only package, enough to validate the Linux side
build/native-linux.sh Release
dotnet build CycloneDDS.NET.Core.slnf -c Release
dotnet pack src/CycloneDDS.Runtime/CycloneDDS.Runtime.csproj     -c Release --no-build -o artifacts/nuget
dotnet pack tools/DdsMonitor/DdsMonitor.Blazor/DdsMonitor.csproj -c Release --no-build -o artifacts/nuget
build/test-package.sh
```

### The smoke test (`build/test-package.{sh,ps1}`)

Both scripts pick the **newest** package in the feed by write-time (so a cluttered
`artifacts/nuget` with old builds is fine — do **not** name-sort), then:

1. Consume `CycloneDDS.NET` as a real NuGet **package** via `examples/PackageSmokeTest`
   — which declares a `[DdsTopic]` (so the packaged code generator + `idlc` run at
   build time) and does a publish/subscribe round-trip. Expect
   `[smoke] PASS: round-trip Id=42 ...`.
2. Install the `ddsmonitor` global tool, confirm it serves HTTP 200 (native loaded),
   and uninstall it.

```
build/test-package.sh [FEED_DIR]        # default artifacts/nuget; NO_DDSMON=1 to skip the tool
.\build\test-package.ps1 [-FeedDir <d>] [-NoDdsMon]
```

Run the smoke test on **both** a Windows and a Linux box before publishing. Both
were validated during development — Linux locally + in CI, Windows in the CI
`build` job.

## Version Management

This project uses **Nerdbank.GitVersioning (NBGV)** for automatic semantic versioning.

### Understanding NBGV Versioning

- **Automatic Version Calculation:** NBGV determines the version based on:
  - Git tags (e.g., `v1.0.0`)
  - Git commit height since the last tag
  - Branch name
  
- **Version Format:** `{Major}.{Minor}.{Patch}-{Prerelease}+{GitCommitId}`
  - Example: `1.0.5-alpha.3+g7e041a2787`

### Version Configuration

Version settings are stored in the root `version.json`, which both packages
(`CycloneDDS.NET` and `CycloneDDS.NET.DdsMonitor`) share, so they always carry the
same version:
```json
{
  "version": "1.2",
  "publicReleaseRefSpec": [
    "^refs/heads/main$",
    "^refs/tags/v\\d+\\.\\d+"
  ]
}
```

## Release Types

### 1. Pre-Release (Alpha/Beta)

Pre-releases are automatically created by CI for every commit to `main`.

**Process:**
1. Merge your PR to `main`
2. CI automatically builds and publishes (if configured)
3. Package version: `1.0.x-alpha+{commitId}`

**Manual Pre-Release:**
```powershell
# Build the package
.\build\pack.ps1

# Packages will be in artifacts/nuget/
# Example: CycloneDDS.NET.1.0.5-alpha-g7e041a2787.nupkg
```

### 2. Stable Release

Stable releases are created by tagging a commit.

**Process:**

#### Step 1: Prepare the Release

1. **Update CHANGELOG.md:**
   ```markdown
   ## [1.0.0] - 2026-02-14
   
   ### Added
   - Feature descriptions
   
   ### Fixed
   - Bug fixes
   ```

2. **Commit the changelog:**
   ```bash
   git add CHANGELOG.md
   git commit -m "Prepare release v1.0.0"
   git push origin main
   ```

#### Step 2: Create a Git Tag

1. **Tag the release commit:**
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. **Verify the tag:**
   ```bash
   git describe --tags
   # Should output: v1.0.0
   ```

#### Step 3: Build and Verify

The CI pipeline should automatically build the tagged version.

**To build locally:**
```powershell
# Clean previous builds
Remove-Item -Recurse -Force artifacts/ -ErrorAction SilentlyContinue

# Build the package
.\build\pack.ps1

# Verify the version
# Check artifacts/nuget/ for CycloneDDS.NET.1.0.0.nupkg (no prerelease suffix)
```

#### Step 4: Inspect the Package

Before publishing, inspect the package contents:

```powershell
# Option 1: Use NuGet Package Explorer (GUI)
# Download from: https://github.com/NuGetPackageExplorer/NuGetPackageExplorer

# Option 2: Extract and inspect manually
Expand-Archive artifacts/nuget/CycloneDDS.NET.1.0.0.nupkg -DestinationPath temp_inspect

# Verify contents:
# - icon.png is present
# - README.md is present
# - ThirdPartyNotices.txt is present
# - Native binaries are in runtimes/ folder
# - Build tools are in build/ or buildTransitive/ folder
# - lib/net8.0/ has *.dll AND matching *.pdb for Runtime, Core and Schema
#   (SourceLink debugging), and a .snupkg was produced alongside the .nupkg
```

**Debugging support (SourceLink).** The package embeds SourceLink so consumers can
step into the library sources straight from GitHub. Symbols ship two ways: the
`lib/net8.0/*.pdb` travel inside the `.nupkg` (Visual Studio loads them directly),
and a `.snupkg` is published to the NuGet symbol server. Verify with the
[`sourcelink`](https://www.nuget.org/packages/sourcelink) tool, e.g.
`sourcelink print-urls lib\net8.0\CycloneDDS.Runtime.pdb` should list
`https://raw.githubusercontent.com/pjanec/CycloneDds.NET/<commit>/...` URLs.
Publish **both** the `.nupkg` and the `.snupkg` (see below). SourceLink URLs only
resolve for commits pushed to GitHub, so publish from a CI build on `main`/a tag.

**Key Things to Check:**
- [ ] Package version is correct (no `-alpha` suffix for stable)
- [ ] `icon.png` is at package root
- [ ] `README.md` is at package root
- [ ] `ThirdPartyNotices.txt` is at package root
- [ ] Native libraries: `runtimes/win-x64/native/ddsc.dll`
- [ ] Build tools: `tools/` or `build/` folder with code generators
- [ ] Symbol package (`.snupkg`) is generated

## Publishing to NuGet.org

### Option A: Automatic Publishing (CI/CD) — recommended

CI publishes to NuGet.org automatically when you push a **version tag** (`vX.Y.Z`),
but only after the cross-platform package has passed the Windows and Linux
verification jobs. The `publish` job pushes both `CycloneDDS.NET` and
`CycloneDDS.NET.DdsMonitor` (plus their `.snupkg` symbols).

**One-time setup — store the NuGet API key as a repository secret:**

1. Create an API key at <https://www.nuget.org/account/apikeys> with **"Push new
   packages and package versions"**. Scope the *Package glob* to `CycloneDDS.NET*`
   so it covers both packages. (For the very first push of a brand-new package id,
   a key scoped to *all* packages / your account may be required until the id
   exists.)
2. In GitHub: **Repo → Settings → Secrets and variables → Actions → New repository
   secret**. Name it exactly **`NUGET_API_KEY`** and paste the key. GitHub encrypts
   it and masks it in logs; the workflow reads it as `secrets.NUGET_API_KEY`. You
   never commit the key.

**Releasing:**

1. Make sure `main` is green and at the commit you want to ship.
2. Check the version NBGV will produce for that commit:
   ```bash
   nbgv get-version            # e.g. 1.2.5  (dotnet tool install -g nbgv)
   ```
   The published version is `version.json` base + git height — the tag name does
   **not** set it, so name the tag to match (e.g. `v1.2.5`). Both packages get this
   same version.
3. **Create a GitHub Release** with tag `vX.Y.Z` targeting that commit (or
   `git tag v1.2.5 && git push origin v1.2.5`). The tag push runs CI; on success
   the `publish` job pushes to NuGet.org.
4. **Verify on NuGet.org** that the new version (and the DdsMonitor tool) appear.

> The tag is added to `publicReleaseRefSpec` in `version.json`, so a tag build gets
> a clean version (no `-gSHA` prerelease suffix). SourceLink URLs resolve because
> the tagged commit is on GitHub.

### Option B: Manual Publishing

#### Step 1: Set Up NuGet API Key

**One-time setup:**
```powershell
# Get your API key from: https://www.nuget.org/account/apikeys
# Create a key with "Push new packages and package versions" permission

# Store the key (safer than using it directly in commands)
$apiKey = "your-api-key-here"

# Or use the dotnet tool to store it:
dotnet nuget add source https://api.nuget.org/v3/index.json `
    --name nuget.org `
    --api-key $apiKey
```

**Security Best Practice:** Store API keys as GitHub Secrets for CI, never commit them to the repository.

#### Step 2: Push the Package

```powershell
# Navigate to the package directory
cd artifacts/nuget

# Push the main package
dotnet nuget push CycloneDDS.NET.1.0.0.nupkg `
    --source https://api.nuget.org/v3/index.json `
    --api-key $apiKey

# Push the symbol package
dotnet nuget push CycloneDDS.NET.1.0.0.snupkg `
    --source https://api.nuget.org/v3/index.json `
    --api-key $apiKey
```

#### Step 3: Verify the Release

1. **Check NuGet.org:** https://www.nuget.org/packages/CycloneDDS.NET/
   - Verify the new version appears
   - Check that the icon displays correctly
   - Verify the README is visible on the package page

2. **Test Installation:**
   ```bash
   mkdir test-install
   cd test-install
   dotnet new console
   dotnet add package CycloneDDS.NET --version 1.0.0
   dotnet build
   ```

3. **Verify Package Contents:**
   - Native libraries are copied to output
   - Code generation works during build

## Post-Release Tasks

### 1. Create GitHub Release

1. Go to: https://github.com/pjanec/CycloneDds.NET/releases/new
2. Select the tag: `v1.0.0`
3. Release title: `CycloneDDS.NET v1.0.0`
4. Description: Copy relevant sections from `CHANGELOG.md`
5. Attach build artifacts (optional):
   - `CycloneDDS.NET.1.0.0.nupkg`
   - `CycloneDDS.NET.1.0.0.snupkg`
6. Click "Publish release"

### 2. Update Documentation

- Update README badges if necessary
- Announce the release (social media, mailing lists, etc.)
- Close any related milestone on GitHub

### 3. Prepare for Next Release

Update `CHANGELOG.md` with a new `[Unreleased]` section:
```markdown
## [Unreleased]

### Added

### Changed

### Fixed
```

## Troubleshooting

### Version Not Incrementing

**Problem:** NBGV is not detecting the new tag.

**Solution:**
```bash
# Verify tags are correct
git tag -l

# Ensure you've pushed the tag
git push origin v1.0.0

# Check NBGV version calculation
nbgv get-version
```

### Package Already Exists

**Problem:** `409 Conflict - The package version already exists.`

**Solution:**
- NuGet.org does **not allow overwriting** published packages
- You must increment the version (even for bug fixes)
- Hotfix: Create a new patch version (e.g., `v1.0.1`)

### Missing Files in Package

**Problem:** `icon.png` or `README.md` not in the package.

**Solution:**
1. Verify `Directory.Build.props` has the `<None Include=...>` elements
2. Check file paths are correct (use `$(MSBuildThisFileDirectory)`)
3. Clean and rebuild:
   ```powershell
   Remove-Item -Recurse artifacts/
   .\build\pack.ps1
   ```

### Symbol Package Errors

**Problem:** Symbol package fails to upload.

**Solution:**
- Ensure PDB files are embedded or included
- Check `<IncludeSymbols>true</IncludeSymbols>` in `Directory.Build.props`
- Symbol packages are optional; main package can be published without them

## Reference Links

- **NuGet.org Account:** https://www.nuget.org/account
- **API Key Management:** https://www.nuget.org/account/apikeys
- **NBGV Documentation:** https://github.com/dotnet/Nerdbank.GitVersioning/blob/main/doc/nbgv-cli.md
- **NuGet Package Explorer:** https://github.com/NuGetPackageExplorer/NuGetPackageExplorer

## Emergency Rollback

If a release has critical issues:

1. **Unlist (deprecate) the package version** on NuGet.org:
   - Go to the package page -> Manage Package
   - Select the version and click "Unlist"
   - This hides it from search but existing dependencies still work

2. **Publish a hotfix version** immediately:
   - Create a fix branch
   - Tag a new version (e.g., `v1.0.1`)
   - Follow the release process

3. **Communicate the issue:**
   - Update the GitHub release with a warning
   - Post an issue describing the problem

---

**Last Updated:** February 2026  
**Maintained By:** CycloneDDS.NET Authors
