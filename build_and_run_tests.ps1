param(
    [string]$Configuration = "Release",
    [string]$Filter = ""
)

$ErrorActionPreference = "Stop"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  CycloneDDS C# Bindings: Build & Test ($Configuration)" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# 1. Build CodeGen (Critical Dependency - Must be built before projects that use it)
Write-Host "`n[1/3] Building CodeGen Tool..." -ForegroundColor Yellow
dotnet build tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj -c $Configuration
if ($LASTEXITCODE -ne 0) { Write-Error "CodeGen build failed"; exit 1 }

# 2. Build Entire Solution
Write-Host "`n[2/3] Building Solution..." -ForegroundColor Yellow
dotnet build CycloneDDS.NET.sln -c $Configuration
if ($LASTEXITCODE -ne 0) { Write-Error "Solution build failed"; exit 1 }

# 3. Run All Tests
Write-Host "`n[3/3] Executing Test Suite (All Projects)..." -ForegroundColor Yellow

# Construct Test Args
# Note: We use the SLN file to target all test projects contained within it.
$TestArgs = @("test", "CycloneDDS.NET.sln", "-c", $Configuration, "--no-build", "--logger", "console;verbosity=normal")

if (![string]::IsNullOrWhiteSpace($Filter)) {
    $TestArgs += "--filter"
    $TestArgs += $Filter
    Write-Host "Filter Applied: $Filter" -ForegroundColor Magenta
}

Write-Host "Running: dotnet $TestArgs" -ForegroundColor DarkGray
& dotnet $TestArgs

if ($LASTEXITCODE -ne 0) { 
    Write-Error "Tests FAILED."
    exit 1 
}

Write-Host "`n✅ All Tests Passed Successfully!" -ForegroundColor Green
exit 0
