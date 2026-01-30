# CsharpToC.Symmetry - Run Tests Only (Hot-Patch Mode)
# Fast iteration: Run tests without rebuilding (picks up manual edits to obj/Generated/)
#
# Usage:
#   .\run_tests_only.ps1                         # Run all tests
#   .\run_tests_only.ps1 -Filter "Part1"        # Run specific category
#   .\run_tests_only.ps1 -Filter "TestCharTopic"  # Run single test

param(
    [string]$Filter = ""
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$ProjectPath = "CsharpToC.Symmetry.csproj"
$TestConfig = "Debug"
$BinPath = "bin\$TestConfig\net8.0"

Write-Host "`n========================================" -ForegroundColor Yellow
Write-Host "🚀 HOT-PATCH MODE: Running Tests Only" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "(No rebuild - using existing binaries)" -ForegroundColor DarkYellow
Write-Host ""

# Check if project has been built
if (-not (Test-Path $BinPath)) {
    Write-Host "❌ Error: Project not built yet!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please run rebuild_and_test.ps1 first:" -ForegroundColor Yellow
    Write-Host "  .\rebuild_and_test.ps1" -ForegroundColor Cyan
    Write-Host ""
    exit 1
}

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# Run tests with --no-build flag
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
    Write-Host "Filter: $Filter" -ForegroundColor Cyan
    Write-Host ""
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

Write-Host "`n========================================" -ForegroundColor Yellow
Write-Host "⚡ Completed in $([math]::Round($elapsed, 2)) seconds" -ForegroundColor Yellow
Write-Host "========================================`n" -ForegroundColor Yellow

if ($exitCode -eq 0) {
    Write-Host "Tip: To make changes permanent, update the emitter code and run:" -ForegroundColor DarkGray
    Write-Host "     .\rebuild_and_test.ps1" -ForegroundColor DarkGray
    Write-Host ""
}

exit $exitCode
