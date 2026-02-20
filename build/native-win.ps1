# ===================================================================================
# build/native-win.ps1
#
# PURPOSE:
#   Compiles the native (C/C++) Cyclone DDS submodule and copies the resulting
#   binaries (DLLs, tools, etc.) to the local 'artifacts' directory.
#   This is a prerequisite for running managed tests or packing the NuGet package.
#
# USAGE:
#   .\build\native-win.ps1 [-Configuration Release|Debug]
# ===================================================================================

param (
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Building Native CycloneDDS ($Configuration)" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# Paths
$RepoRoot = $PSScriptRoot | Split-Path -Parent
$SourceDir = Join-Path $RepoRoot "cyclonedds"
$BuildDir = Join-Path $RepoRoot "build/native"
$InstallDir = Join-Path $RepoRoot "artifacts/native-install" # Intermediate install dir
$ArtifactsDir = Join-Path $RepoRoot "artifacts/native/win-x64"

# Ensure directories exist
if (!(Test-Path $BuildDir)) { New-Item -ItemType Directory -Path $BuildDir | Out-Null }
if (!(Test-Path $InstallDir)) { New-Item -ItemType Directory -Path $InstallDir | Out-Null }
if (!(Test-Path $ArtifactsDir)) { New-Item -ItemType Directory -Path $ArtifactsDir | Out-Null }

# Check for CMake
if (!(Get-Command cmake -ErrorAction SilentlyContinue)) {
    Write-Error "CMake is not installed or not in PATH."
    exit 1
}

# Configure CMake
Write-Host "`n[1/3] Configuring CMake..." -ForegroundColor Yellow
Push-Location $BuildDir
try {
    # -DBUILD_IDLC=ON is required to build changes to IDL compiler
    $cmakeConfigured = $false
    $oldEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    $cmakeGenerators = @(
        "Visual Studio 18 2026",
        "Visual Studio 17 2022",
        "Visual Studio 16 2019",
        "Visual Studio 15 2017"
    )

    $commonCmakeArgs = @(
        "-S", "$SourceDir",
        "-B", ".",
        "-A", "x64",
        "-DCMAKE_INSTALL_PREFIX=$InstallDir",
        "-DBUILD_IDLC=ON",
        "-DBUILD_TESTING=OFF",
        "-DBUILD_EXAMPLES=OFF",
        "-DENABLE_SSL=OFF",
        "-DENABLE_SHM=OFF",
        "-DENABLE_SECURITY=OFF"
    )

    foreach ($gen in $cmakeGenerators) {
        Write-Host "  Trying CMake generator: $gen (x64)..." -ForegroundColor Gray
        
        $cmakeOut = cmake -G $gen @commonCmakeArgs 2>&1

        if ($LASTEXITCODE -eq 0) {
            $cmakeConfigured = $true
            Write-Host "  [+] Successfully configured using $gen" -ForegroundColor Green
            $cmakeOut
            break
        }
    }

    if (-not $cmakeConfigured) {
        Write-Host "  Falling back to default CMake generator with -A x64..." -ForegroundColor Yellow
        if (Test-Path "CMakeCache.txt") { Remove-Item "CMakeCache.txt" -Force }
        $cmakeOut = cmake @commonCmakeArgs 2>&1

        if ($LASTEXITCODE -eq 0) {
            $cmakeConfigured = $true
            Write-Host "  [+] Successfully configured using default generator (x64)" -ForegroundColor Green
            $cmakeOut
        }
    }

    if (-not $cmakeConfigured) {
        Write-Host "  Falling back to default CMake generator..." -ForegroundColor Yellow
        if (Test-Path "CMakeCache.txt") { Remove-Item "CMakeCache.txt" -Force }
        $cmakeOut = cmake @commonCmakeArgs 2>&1

        if ($LASTEXITCODE -eq 0) {
            $cmakeConfigured = $true
            Write-Host "  [+] Successfully configured using default generator" -ForegroundColor Green
            $cmakeOut
        } else {
            $ErrorActionPreference = $oldEap
            $cmakeOut | ForEach-Object { Write-Error $_.ToString() }
            throw "CMake configuration failed."
        }
    }
}
finally {
    $ErrorActionPreference = $oldEap
    Pop-Location
}

# Build & Install
Write-Host "`n[2/3] Building & Installing..." -ForegroundColor Yellow
cmake --build $BuildDir --config $Configuration --target install

if ($LASTEXITCODE -ne 0) { throw "Build failed." }

# Copy Artifacts
Write-Host "`n[3/3] Copying artifacts to $ArtifactsDir..." -ForegroundColor Yellow

$BinDir = Join-Path $InstallDir "bin"
$RequiredFiles = @(
    "ddsc.dll",
    "idlc.exe",
    "cycloneddsidl.dll",
    "cycloneddsidlc.dll",
    "cycloneddsidljson.dll"
)

# Copy required files from bin/
foreach ($file in $RequiredFiles) {
    $src = Join-Path $BinDir $file
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination $ArtifactsDir -Force
        Write-Host "  [+] Copied $file" -ForegroundColor Green
    } else {
        Write-Warning "  [-] Missing $file in output!"
    }
}

# Can we locate VC++ runtime?
# Usually in $env:SystemRoot\System32 but dependent on VS installation.
# However, redistributables might not be legally redistributable by simple copy 
# without an installer merge module, BUT NuGet packages often bundle them anyway.
# For now, let's warn if we can't find them easily, or skip copy if not present in bin output.
# (If cyclonedds build copies them to bin, we are good).

Write-Host "`nNative build complete." -ForegroundColor Green
