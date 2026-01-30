# CsharpToC.Symmetry - Generate Golden Data Script
# Deletes existing golden data files and regenerates them from native DLL
#
# Usage:
#   .\generate_golden_data.ps1 -Force                # Regenerate all
#   .\generate_golden_data.ps1 -Filter "Part1" -Force  # Regenerate Part1 only
#   .\generate_golden_data.ps1 -Filter "CharTopic" -Force  # Regenerate specific test

param(
    [string]$Filter = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$ProjectPath = "CsharpToC.Symmetry.csproj"
$TestConfig = "Debug"
$GoldenDataPath = "bin\$TestConfig\net8.0\GoldenData"

Write-Host "========================================" -ForegroundColor Magenta
Write-Host "Golden Data Regeneration" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta

# Warning prompt unless -Force is used
if (-not $Force) {
    Write-Host "⚠️  WARNING: This will delete existing golden data files!" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Golden data represents the 'truth' from native DLL." -ForegroundColor Yellow
    Write-Host "Only regenerate if:" -ForegroundColor Yellow
    Write-Host "  - Native DLL has been updated" -ForegroundColor Yellow
    Write-Host "  - Test seeds have changed" -ForegroundColor Yellow
    Write-Host "  - Golden data is suspected to be corrupt" -ForegroundColor Yellow
    Write-Host ""
    
    $confirmation = Read-Host "Continue? (y/N)"
    if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
        Write-Host "Cancelled." -ForegroundColor Gray
        exit 0
    }
}

Write-Host ""

# Step 1: Check if project is built
if (-not (Test-Path "bin\$TestConfig")) {
    Write-Host "[1/4] Project not built - building first..." -ForegroundColor Yellow
    try {
        dotnet build $ProjectPath -c $TestConfig -v minimal
        Write-Host "  ✓ Build complete" -ForegroundColor Green
    }
    catch {
        Write-Host "  ✗ Build failed: $_" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "[1/4] Project already built" -ForegroundColor Green
}

# Step 2: Delete existing golden data
Write-Host "`n[2/4] Deleting existing golden data..." -ForegroundColor Yellow

if (Test-Path $GoldenDataPath) {
    $txtFiles = Get-ChildItem -Path $GoldenDataPath -Filter "*.txt"
    $fileCount = $txtFiles.Count
    
    if ($fileCount -gt 0) {
        foreach ($file in $txtFiles) {
            Remove-Item $file.FullName -Force
        }
        Write-Host "  ✓ Deleted $fileCount file(s)" -ForegroundColor Green
    } else {
        Write-Host "  ℹ No existing files to delete" -ForegroundColor Cyan
    }
} else {
    Write-Host "  ℹ GoldenData folder does not exist yet" -ForegroundColor Cyan
}

# Step 3: Run tests to trigger regeneration
Write-Host "`n[3/4] Running tests to regenerate golden data..." -ForegroundColor Yellow
Write-Host "     (This will call native DLL for each test)" -ForegroundColor DarkGray
Write-Host ""

$testArgs = @(
    "test",
    $ProjectPath,
    "--no-build",
    "-c", $TestConfig,
    "--logger", "console;verbosity=minimal"
)

if ($Filter) {
    $testArgs += "--filter"
    $testArgs += $Filter
    Write-Host "  Filter: $Filter" -ForegroundColor Cyan
}

try {
    & dotnet $testArgs
    Write-Host ""
}
catch {
    Write-Host "  ✗ Test execution failed: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Possible causes:" -ForegroundColor Yellow
    Write-Host "  - Native DLL (ddsc_test_lib.dll) is missing" -ForegroundColor Yellow
    Write-Host "  - Topic names don't match native implementation" -ForegroundColor Yellow
    exit 1
}

# Step 4: Verify files were created
Write-Host "[4/4] Verifying golden data files..." -ForegroundColor Yellow

if (Test-Path $GoldenDataPath) {
    $newFiles = Get-ChildItem -Path $GoldenDataPath -Filter "*.txt"
    $newCount = $newFiles.Count
    
    if ($newCount -gt 0) {
        Write-Host "  ✓ Generated $newCount file(s)" -ForegroundColor Green
        
        # Show file sizes
        $totalSize = ($newFiles | Measure-Object -Property Length -Sum).Sum
        $avgSize = [math]::Round($totalSize / $newCount, 0)
        Write-Host "  ℹ Total size: $([math]::Round($totalSize / 1024, 2)) KB (avg: $avgSize bytes/file)" -ForegroundColor Cyan
        
    } else {
        Write-Host "  ⚠️  Warning: No files were generated!" -ForegroundColor Yellow
        Write-Host "     Check test output for errors." -ForegroundColor Yellow
    }
} else {
    Write-Host "  ⚠️  Warning: GoldenData folder was not created!" -ForegroundColor Yellow
}

Write-Host "========================================" -ForegroundColor Magenta
Write-Host "Golden Data Regeneration Complete" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
