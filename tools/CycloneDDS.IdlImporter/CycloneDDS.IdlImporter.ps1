<#
.SYNOPSIS
    Recursively scans for IDL files and runs the CycloneDDS.IdlImporter tool.

.DESCRIPTION
    This script recursively scans the specified source root folder (defaulting to current directory)
    for folders containing .idl files. 

    When a folder containing .idl files is found:
    1. It determines the "master" IDL file.
       - If there is only one .idl file, that is the master.
       - If there are multiple, it looks for one with the same base name as the folder.
       - If no matching file is found, an error is logged and the folder is skipped.
    2. It calls CycloneDDS.IdlImporter.exe (expected to be in the same directory as this script)
       with the master IDL, specifying the global --source-root and --output-root.
    3. It stops recursing into subdirectories of that folder.

.PARAMETER SourceRoot
    The root directory to start the scan. Defaults to the current working directory.

.PARAMETER OutputRoot
    The directory where generated C# files will be placed. Defaults to the current working directory.
#>

param(
    [string]$SourceRoot = ".",
    [string]$OutputRoot = ".",
    [string]$IdlcArgs = ""
)

$ErrorActionPreference = "Stop"

# Use absolute paths
$SourceRoot = Resolve-Path $SourceRoot | Select-Object -ExpandProperty Path
# Create OutputRoot if needed
if (-not (Test-Path $OutputRoot)) {
    New-Item -ItemType Directory -Path $OutputRoot | Out-Null
}
$OutputRoot = Resolve-Path $OutputRoot | Select-Object -ExpandProperty Path

$ImporterTool = Join-Path $PSScriptRoot "CycloneDDS.IdlImporter.exe"

if (-not (Test-Path $ImporterTool)) {
    Write-Error "Detailed Error: CycloneDDS.IdlImporter.exe was not found at expected path: $ImporterTool"
    exit 1
}

function Invoke-IdlImport {
    param (
        [string]$Directory
    )

    $idlFiles = Get-ChildItem -Path $Directory -Filter "*.idl" -File

    if ($idlFiles.Count -gt 0) {
        $masterIdl = $null
        
        if ($idlFiles.Count -eq 1) {
            $masterIdl = $idlFiles[0]
        } else {
            $dirName = Split-Path $Directory -Leaf
            $masterIdl = $idlFiles | Where-Object { $_.BaseName -eq $dirName } | Select-Object -First 1
            
            if (-not $masterIdl) {
                Write-Host "Error: Folder '$Directory' contains multiple IDL files but none match the folder name '$dirName'. Skipping." -ForegroundColor Red
                return
            }
        }

        Write-Host "Importing Master IDL: $($masterIdl.Name) in $Directory" -ForegroundColor Cyan

        # Run the importer tool
        # passing the IDL file path, and the GLOBAL source-root and output-root
        if ($IdlcArgs) {
            & $ImporterTool $masterIdl.FullName --source-root $SourceRoot --output-root $OutputRoot --idlc-args $IdlcArgs
        } else {
            & $ImporterTool $masterIdl.FullName --source-root $SourceRoot --output-root $OutputRoot
        }

        if ($LASTEXITCODE -ne 0) {
            Write-Host "Error: Import failed for $($masterIdl.FullName) with exit code $LASTEXITCODE" -ForegroundColor Red
        }

    } else {
        # Recurse into subdirectories if no IDLs found in current directory
        $subDirs = Get-ChildItem -Path $Directory -Directory
        foreach ($subDir in $subDirs) {
            Invoke-IdlImport -Directory $subDir.FullName
        }
    }
}

Invoke-IdlImport -Directory $SourceRoot
