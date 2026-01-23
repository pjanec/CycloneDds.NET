$ErrorActionPreference = "Stop"

$workDir = $PSScriptRoot

# 2. Run IDLC
$cycloneDir = "D:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled"
$idlcExe = "$cycloneDir\bin\idlc.exe"
$idlFile = "$workDir\verification.idl"

if (-not (Test-Path $idlcExe)) {
    Write-Error "IDLC not found at $idlcExe"
    exit 1
}

Write-Host "Running IDLC..."
& $idlcExe -l json $idlFile
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

