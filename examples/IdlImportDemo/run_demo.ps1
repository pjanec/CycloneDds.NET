[CmdletBinding()]
param (
    [string]$Configuration = "Debug"
)

$RootDir = "$PSScriptRoot\..\..\.."
$IdlImporterProj = "$RootDir\tools\CycloneDDS.IdlImporter\CycloneDDS.IdlImporter.csproj"
$IdlImporterExe = "$RootDir\tools\CycloneDDS.IdlImporter\bin\$Configuration\net8.0\CycloneDDS.IdlImporter.exe"

# 1. Build the IdlImporter tool
Write-Host "Building IdlImporter..." -ForegroundColor Cyan
dotnet build $IdlImporterProj -c $Configuration

if (-not (Test-Path $IdlImporterExe)) {
    Write-Error "IdlImporter executable not found at $IdlImporterExe"
    exit 1
}

# 2. Run IdlImporter for CommonLib
#    SourceRoot must include both libs so AppLib includes work later
$SourceRoot = "$PSScriptRoot"
$CommonIdl = "$PSScriptRoot\CommonLib\common_types.idl"
$CommonOut = "$PSScriptRoot\CommonLib\Generated"

Write-Host "Generating CommonLib code..." -ForegroundColor Cyan
& $IdlImporterExe $CommonIdl --source-root $SourceRoot --output-root $CommonOut

# 3. Run IdlImporter for AppLib
#    This IDL includes CommonLib/common_types.idl
$AppIdl = "$PSScriptRoot\AppLib\app_types.idl"
$AppOut = "$PSScriptRoot\AppLib\Generated"

Write-Host "Generating AppLib code..." -ForegroundColor Cyan
& $IdlImporterExe $AppIdl --source-root $SourceRoot --output-root $AppOut

# 4. Build and Run the Demo App
Write-Host "Building and Running Demo App..." -ForegroundColor Cyan
dotnet run --project "$PSScriptRoot\IdlImportDemoApp\IdlImportDemoApp.csproj" -c $Configuration
