# CsharpToC.Symmetry - Rebuild and Test Script
# Full cycle: Clean -> Build (triggers CodeGen) -> Run Tests
#
# Usage:
#   .\rebuild_and_test.ps1                    # Run all tests
#   .\rebuild_and_test.ps1 -Filter "Part1"   # Run specific category
#   .\rebuild_and_test.ps1 -Filter "TestCharTopic"  # Run single test
#   .\rebuild_and_test.ps1 -Verbose          # Detailed output

param(
    [string]$Filter = "",
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$ProjectPath = "CsharpToC.Symmetry.csproj"
$TestConfig = "Debug"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "CsharpToC.Symmetry - Full Rebuild & Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# Step 1: Clean
Write-Host "[1/3] Cleaning previous build..." -ForegroundColor Yellow
try {
    if ($Verbose) {
        dotnet clean $ProjectPath -c $TestConfig -v normal
    } else {
        dotnet clean $ProjectPath -c $TestConfig -v minimal | Out-Null
    }
    Write-Host "  ✓ Clean complete" -ForegroundColor Green
}
catch {
    Write-Host "  ✗ Clean failed: $_" -ForegroundColor Red
    exit 1
}

# Step 2: Build (triggers CodeGen)
Write-Host "`n[2/3] Building (CodeGen + Compile)..." -ForegroundColor Yellow
try {
    if ($Verbose) {
        dotnet build $ProjectPath -c $TestConfig -v normal
    } else {
        dotnet build $ProjectPath -c $TestConfig -v minimal
    }
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    
    Write-Host "  ✓ Build complete" -ForegroundColor Green
}
catch {
    Write-Host "  ✗ Build failed: $_" -ForegroundColor Red
    Write-Host "`nTip: Run with -Verbose flag for detailed error messages" -ForegroundColor Yellow
    exit 1
}

# Step 3: Run Tests
Write-Host "`n[3/3] Running tests..." -ForegroundColor Yellow

$testArgs = @(
    "test",
    $ProjectPath,
    "--no-build",
    "-c", $TestConfig,
    "--logger", "console;verbosity=normal"
)

if ($Filter) {
    $testArgs += "--filter"
    $testArgs += $Filter
    Write-Host "  Filter: $Filter" -ForegroundColor Cyan
}

try {
    & dotnet $testArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n  ✗ Some tests failed" -ForegroundColor Red
        $exitCode = 1
    } else {
        Write-Host "`n  ✓ All tests passed!" -ForegroundColor Green
        $exitCode = 0
    }
}
catch {
    Write-Host "  ✗ Test execution failed: $_" -ForegroundColor Red
    exit 1
}

$stopwatch.Stop()
$elapsed = $stopwatch.Elapsed.TotalSeconds

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total Time: $([math]::Round($elapsed, 1)) seconds" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

exit $exitCode
