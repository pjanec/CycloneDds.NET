# ===================================================================================
# build/pack.ps1
#
# PURPOSE:
#   Full release pipeline script:
#   1. Rebuilds Native assets.
#   2. Restores & Rebuilds the managed Solution (core only, no examples).
#   3. Runs all Tests.
#   4. Packs the main CycloneDDS.NET NuGet package.
#   5. Restores & builds examples (which consume CycloneDDS.NET as a NuGet package).
#
#   This is what runs in CI to produce release artifacts.
#
# USAGE:
#   .\build\pack.ps1
# ===================================================================================

$ErrorActionPreference = "Stop"

$RepoRoot = $PSScriptRoot | Split-Path -Parent
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$NuGetDir = Join-Path $ArtifactsDir "nuget"

# Solution filter that excludes examples/ projects.
# Examples reference CycloneDDS.NET as a NuGet package, which doesn't exist until
# after the Pack step, so they must be restored/built separately (see step 6 below).
$CoreSlnf = "$RepoRoot\CycloneDDS.NET.Core.slnf"

# Ensure output dir exists
if (!(Test-Path $NuGetDir)) { New-Item -ItemType Directory -Path $NuGetDir | Out-Null }

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Unified Build & Pack" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# 1. Native Build
Write-Host "`n[1/6] Building Native Assets..." -ForegroundColor Yellow
$NativeScript = Join-Path $PSScriptRoot "native-win.ps1"
& $NativeScript -Configuration Release
if ($LASTEXITCODE -ne 0) { throw "Native build failed." }

# 2. Restore
Write-Host "`n[2/6] Restoring (core - examples excluded)..." -ForegroundColor Yellow
# Use the solution filter so that example projects (which reference CycloneDDS.NET
# as a NuGet package) are not restored here - the package doesn't exist yet.
dotnet restore "$CoreSlnf"
if ($LASTEXITCODE -ne 0) { throw "Restore failed." }

# 3. Build Managed
Write-Host "`n[3/6] Building Managed (Release)..." -ForegroundColor Yellow
dotnet build "$CoreSlnf" -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

# 4. Test
Write-Host "`n[4/6] Running Essential Tests (Runtime)..." -ForegroundColor Yellow
# Using --filter slightly to avoid overly long tests if necessary, but default is all
dotnet test "$RepoRoot\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj" -c Release --no-build --logger "console;verbosity=normal"
if ($LASTEXITCODE -ne 0) { throw "Tests failed." }

# 5. Pack
Write-Host "`n[5/6] Packing..." -ForegroundColor Yellow

# Pack ONLY the main package (CycloneDDS.NET)
# Note: Since we disabled IsPackable on Core/Schema, just packing solution MIGHT skip them or pack only enabled ones.
# But specifying project is safer.
$ProjectToPack = "$RepoRoot\src\CycloneDDS.Runtime\CycloneDDS.Runtime.csproj"

dotnet pack $ProjectToPack -c Release -o $NuGetDir --no-build /p:IncludeSymbols=true /p:SymbolPackageFormat=snupkg

if ($LASTEXITCODE -ne 0) { throw "Pack failed." }

# 6. Restore & build examples
# Now that CycloneDDS.NET is in $NuGetDir (the local-artifacts source), the example
# projects that reference it as a NuGet package can be restored successfully.
Write-Host "`n[6/6] Restoring & building examples..." -ForegroundColor Yellow
$ExamplesDir = "$RepoRoot\examples"
dotnet restore "$ExamplesDir\HelloWorld\HelloWorld.csproj"
if ($LASTEXITCODE -ne 0) { throw "Examples restore failed." }
dotnet build "$ExamplesDir\HelloWorld\HelloWorld.csproj" -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Examples build failed." }

Write-Host "`nBuild, Pack & Examples Complete!" -ForegroundColor Green
Write-Host "Artifacts are in: $NuGetDir" -ForegroundColor White
Get-ChildItem $NuGetDir
