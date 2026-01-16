# Generate ABI offsets from Cyclone DDS source
param(
    [string]$CycloneSourcePath = "$PSScriptRoot\..\cyclonedds",
    [string]$OutputPath = "$PSScriptRoot\..\src\CycloneDDS.Runtime\Descriptors\AbiOffsets.g.cs",
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

Write-Host "Checking prerequisites..." -ForegroundColor Cyan

if (!(Test-Path $CycloneSourcePath)) {
    Write-Error "Cyclone source path not found: $CycloneSourcePath"
    exit 1
}

$projectPath = Join-Path $PSScriptRoot "CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj"
if (!(Test-Path $projectPath)) {
    Write-Error "Codegen project not found at: $projectPath"
    exit 1
}

Write-Host "Building code generator ($Configuration)..." -ForegroundColor Cyan
dotnet build $projectPath -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build code generator."
    exit 1
}

# Find the built executable (handle potential path differences)
$binDir = Join-Path $PSScriptRoot "CycloneDDS.CodeGen\bin\$Configuration\net8.0"
$generatorDll = Join-Path $binDir "CycloneDDS.CodeGen.dll"

if (!(Test-Path $generatorDll)) {
    Write-Error "Generator DLL not found at: $generatorDll"
    exit 1
}

Write-Host "Generating ABI offsets..." -ForegroundColor Cyan
Write-Host "  Cyclone source: $CycloneSourcePath"
Write-Host "  Output: $OutputPath"

try {
    dotnet $generatorDll generate-offsets --source $CycloneSourcePath --output $OutputPath
    if ($LASTEXITCODE -ne 0) {
        throw "Generator exited with code $LASTEXITCODE"
    }
}
catch {
    Write-Error "Failed to generate offsets: $_"
    exit 1
}

Write-Host "Done! Offsets written to $OutputPath" -ForegroundColor Green
