[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$DemoRoot = $PSScriptRoot
$RootDir = (Get-Item "$PSScriptRoot\..\..").FullName
$ToolsDir = Join-Path $RootDir "tools"

# 1. Ensure tools are built
$ImporterProj = Join-Path $RootDir "tools\CycloneDDS.IdlImporter\CycloneDDS.IdlImporter.csproj"
$CodeGenProj = Join-Path $RootDir "tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj"

Write-Host "Building Importer and CodeGen..." -ForegroundColor Cyan
dotnet build $ImporterProj -c Release
dotnet build $CodeGenProj -c Release

$IdlImporterExe = Join-Path $RootDir "tools\CycloneDDS.IdlImporter\bin\Release\net8.0\CycloneDDS.IdlImporter.exe"

if (-not (Test-Path $IdlImporterExe)) {
    Write-Error "Importer executable not found at $IdlImporterExe"
    exit 1
}

# 2. Run Importer
# We process files individually but with the same source-root so includes resolve correctly
# The output will mirror the directory structure relative to source-root
# e.g. CommonLib/common_types.idl -> output/CommonLib/common_types.cs

$SourceRoot = $PSScriptRoot
$OutputRoot = $PSScriptRoot

# Process CommonLib
Write-Host "Importing CommonLib..." -ForegroundColor Green
& $IdlImporterExe "$PSScriptRoot\CommonLib\common_types.idl" --source-root $SourceRoot --output-root $OutputRoot

# Process AppLib
Write-Host "Importing AppLib..." -ForegroundColor Green
& $IdlImporterExe "$PSScriptRoot\AppLib\app_types.idl" --source-root $SourceRoot --output-root $OutputRoot

# 3. Build and Run the App
Write-Host "Running Demo App..." -ForegroundColor Cyan
dotnet run --project "$PSScriptRoot\IdlImportDemoApp\IdlImportDemoApp.csproj" -c Release
