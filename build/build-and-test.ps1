# ===================================================================================
# build/build-and-test.ps1
#
# PURPOSE:
#   The primary developer entry point.
#   1. Checks for native artifacts (builds them if missing).
#   2. Builds the entire Managed Solution (CycloneDDS.NET.sln).
#   3. Runs all tests in the solution.
#
# USAGE:
#   .\build\build-and-test.ps1 [-Configuration Release|Debug] [-Clean]
# ===================================================================================

param(
    [string]$Configuration = "Release",
    [string]$Filter = "",
    [switch]$SkipNative = $false,
    [switch]$Clean = $false
)

$ErrorActionPreference = "Stop"

$RepoRoot = $PSScriptRoot | Split-Path -Parent

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  CycloneDDS.NET: Build & Test ($Configuration)" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# 0. Check for Native Artifacts
$NativeArtifacts = Join-Path $RepoRoot "artifacts/native/win-x64/ddsc.dll"
if (-not (Test-Path $NativeArtifacts)) {
    if ($SkipNative) {
        Write-Warning "Native artifacts not found at $NativeArtifacts. Tests may fail."
    } else {
        Write-Host "`n[0/3] Native artifacts missing. Building native..." -ForegroundColor Yellow
        & (Join-Path $PSScriptRoot "native-win.ps1") -Configuration $Configuration
        if ($LASTEXITCODE -ne 0) { throw "Native build failed." }
    }
} elseif (-not $SkipNative) {
    # Optional: We could check if they are stale, but for now we assume if they exist, they are good.
    # To force rebuild, user would clean or we could add a -ForceNative flag.
    Write-Host "Native artifacts found. Skipping native build (use -Clean to rebuild)." -ForegroundColor DarkGray
}

if ($Clean) {
    Write-Host "`nCleaning..." -ForegroundColor Yellow
    dotnet clean "$RepoRoot\CycloneDDS.NET.sln" -c $Configuration -v m
}

# 1. Build CodeGen (Critical Dependency)
# Note: Solution build usually handles project dependencies, but building explicit tools ensuring they are ready is often safer.
# However, CodeGen is referenced by projects, so Solution Build SHOULD handle it.
# We will just build the Solution.

# 2. Build Entire Solution
Write-Host "`n[1/3] Building Solution (Managed)..." -ForegroundColor Yellow
dotnet build "$RepoRoot\CycloneDDS.NET.sln" -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Solution build failed." }

# 3. Run All Tests
Write-Host "`n[2/3] Executing Test Suite (All Projects)..." -ForegroundColor Yellow

$TestArgs = @("test", "$RepoRoot\CycloneDDS.NET.sln", "-c", $Configuration, "--no-build", "--logger", "console;verbosity=normal")

if (![string]::IsNullOrWhiteSpace($Filter)) {
    $TestArgs += "--filter"
    $TestArgs += $Filter
    Write-Host "Filter Applied: $Filter" -ForegroundColor Magenta
}

Write-Host "Running: dotnet $TestArgs" -ForegroundColor DarkGray
& dotnet $TestArgs

if ($LASTEXITCODE -ne 0) { 
    throw "Tests FAILED."
}

Write-Host "`n✅ All Tests Passed Successfully!" -ForegroundColor Green
