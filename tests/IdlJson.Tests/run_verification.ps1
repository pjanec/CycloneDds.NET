$ErrorActionPreference = "Stop"
$BuildDir = "$PSScriptRoot\build"
$BinDir = "D:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin"

if (-not (Test-Path $BuildDir)) {
    New-Item -Path $BuildDir -ItemType Directory | Out-Null
}

Set-Location $BuildDir

# Run CMake
cmake .. -DCMAKE_BUILD_TYPE=Debug
if ($LASTEXITCODE -ne 0) { throw "CMake configuration failed" }

# Build
cmake --build . --config Debug
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# Add DLLs to Path
$env:PATH = "$BinDir;$env:PATH"

# Locate Executable
$Exe = ".\Debug\verify_layout.exe"
if (-not (Test-Path $Exe)) {
    $Exe = ".\verify_layout.exe"
}

if (-not (Test-Path $Exe)) {
    throw "Executable not found at $Exe"
}

# Run with generated JSON
$JsonFile = "verification.json" 
# verification.json is generated in CMAKE_CURRENT_BINARY_DIR which is $BuildDir
if (-not (Test-Path $JsonFile)) {
    throw "verification.json not found in $BuildDir"
}

Write-Host "Running Verifier..." -ForegroundColor Cyan
& $Exe $JsonFile
