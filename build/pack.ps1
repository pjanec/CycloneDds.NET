# ===================================================================================
# build/pack.ps1
#
# PURPOSE:
#   Full release pipeline script:
#   1. Rebuilds Native assets.
#   2. Restores & Rebuilds the managed Solution.
#   3. Runs all Tests.
#   4. Packs the main CycloneDDS.NET NuGet package.
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

# Ensure output dir exists
if (!(Test-Path $NuGetDir)) { New-Item -ItemType Directory -Path $NuGetDir | Out-Null }

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Unified Build & Pack" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# 1. Native Build
Write-Host "`n[1/5] Building Native Assets..." -ForegroundColor Yellow
$NativeScript = Join-Path $PSScriptRoot "native-win.ps1"
& $NativeScript -Configuration Release
if ($LASTEXITCODE -ne 0) { throw "Native build failed." }

# 2. Restore
Write-Host "`n[2/5] Restoring..." -ForegroundColor Yellow
dotnet restore "$RepoRoot\CycloneDDS.NET.sln"
if ($LASTEXITCODE -ne 0) { throw "Restore failed." }

# 3. Build Managed
Write-Host "`n[3/5] Building Managed (Release)..." -ForegroundColor Yellow
dotnet build "$RepoRoot\CycloneDDS.NET.sln" -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

# 4. Test
Write-Host "`n[4/5] Running Essential Tests (Runtime)..." -ForegroundColor Yellow
# Using --filter slightly to avoid overly long tests if necessary, but default is all
dotnet test "$RepoRoot\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj" -c Release --no-build --logger "console;verbosity=normal"
if ($LASTEXITCODE -ne 0) { throw "Tests failed." }

# 5. Pack
Write-Host "`n[5/5] Packing..." -ForegroundColor Yellow

# Pack ONLY the main package (CycloneDDS.NET)
# Note: Since we disabled IsPackable on Core/Schema, just packing solution MIGHT skip them or pack only enabled ones.
# But specifying project is safer.
$ProjectToPack = "$RepoRoot\src\CycloneDDS.Runtime\CycloneDDS.Runtime.csproj"

dotnet pack $ProjectToPack -c Release -o $NuGetDir --no-build /p:IncludeSymbols=true /p:SymbolPackageFormat=snupkg

if ($LASTEXITCODE -ne 0) { throw "Pack failed." }

Write-Host "`nBuild & Pack Complete!" -ForegroundColor Green
Write-Host "Artifacts are in: $NuGetDir" -ForegroundColor White
Get-ChildItem $NuGetDir
