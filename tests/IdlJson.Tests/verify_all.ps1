$ErrorActionPreference = "Stop"

Write-Host "=================================================="
Write-Host "Verifying All Atomic Test Topics"
Write-Host "=================================================="

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir
$WorkspaceRoot = (Get-Item "$ScriptDir\..\..").FullName

# Add idlc to path
$env:PATH = "$WorkspaceRoot\cyclone-compiled\bin;$env:PATH"

# Set-Location -Path "tests/IdlJson.Tests" # Already there


# Regenerate everything
Write-Host "[Step 1] Generating C header..."
idlc verification.idl
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "[Step 2] Generating JSON metadata..."
idlc -l json verification.idl
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "[Step 3] Building verifier..."
if (-not (Test-Path "build")) { New-Item -ItemType Directory -Path "build" | Out-Null }
Set-Location -Path "build"
cmake ..
if ($LASTEXITCODE -ne 0) { exit 1 }
cmake --build . --config Debug
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "[Step 4] Running verification..."
.\Debug\verify_layout.exe ..\verification.json
if ($LASTEXITCODE -ne 0) { 
    Write-Host "=================================================="
Write-Host "X VERIFICATION FAILED"
Write-Host "=================================================="
exit 1 
}

Write-Host "=================================================="
Write-Host "V ALL TOPICS VERIFIED SUCCESSFULLY"
Write-Host "=================================================="
exit 0
