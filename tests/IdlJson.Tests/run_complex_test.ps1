$ErrorActionPreference = "Stop"

$workDir = $PSScriptRoot

# Run IDLC on complex.idl
$cycloneDir = "D:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled"
$idlcExe = "$cycloneDir\bin\idlc.exe"
$idlFile = "$workDir\complex.idl"

if (-not (Test-Path $idlcExe)) {
    Write-Error "IDLC not found at $idlcExe"
    exit 1
}

Write-Host "Running IDLC on complex.idl..."
& $idlcExe -l json $idlFile
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Done. Check complex.json"
