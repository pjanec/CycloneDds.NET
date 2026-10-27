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
# native/ wraps the unmodified Cyclone DDS submodule (cyclonedds/) plus our idljson plugin.
$SourceDir = Join-Path $RepoRoot "native"
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

# ===================================================================================
# Locate OpenSSL - needed to BUILD the DDS Security plugins (ENABLE_SECURITY=ON).
#
# OpenSSL is only a build-time dependency here: with ENABLE_TCP_TLS=OFF ddsc.dll does
# not import it, and the OpenSSL DLLs are deliberately NOT shipped. Applications that
# configure <Security> provide their own OpenSSL 3 at runtime (see README).
#
# The GitHub windows-latest image ships OpenSSL 3.x under "C:\Program Files\OpenSSL".
# Passing -DOPENSSL_ROOT_DIR explicitly makes a missing install fail here, with an
# actionable message, rather than deep inside the build.
# ===================================================================================
$opensslCandidates = @()
if ($env:OPENSSL_ROOT_DIR) { $opensslCandidates += $env:OPENSSL_ROOT_DIR }
$opensslCandidates += @(
    "C:\Program Files\OpenSSL",
    "C:\Program Files\OpenSSL-Win64",
    "C:\OpenSSL-Win64"
)

$OpenSslRoot = $null
foreach ($candidate in $opensslCandidates) {
    if (Test-Path (Join-Path $candidate "include\openssl\ssl.h")) {
        $OpenSslRoot = (Resolve-Path $candidate).Path
        break
    }
}

if (-not $OpenSslRoot) {
    throw @"
OpenSSL development files were not found; they are needed to build the DDS Security plugins.

Searched:
  $($opensslCandidates -join "`n  ")

Install OpenSSL 3.x with headers and libraries (e.g. the full "Win64 OpenSSL v3"
installer from https://slproweb.com/products/Win32OpenSSL.html), or point OPENSSL_ROOT_DIR at an existing installation.
"@
}

Write-Host "Using OpenSSL at: $OpenSslRoot" -ForegroundColor Green

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
        "-DENABLE_SHM=OFF",
        # DDS Security plugins on; TCP+TLS off so ddsc.dll itself does not import OpenSSL.
        "-DENABLE_SECURITY=ON",
        "-DENABLE_SSL=ON",
        "-DENABLE_TCP_TLS=OFF",
        # Forward slashes: CMake treats backslashes in -D values as escapes.
        "-DOPENSSL_ROOT_DIR=$($OpenSslRoot -replace '\\', '/')"
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

# DDS Security plugins. ddsc.dll loads these by bare name (LoadLibrary) only when the
# domain configuration has a <Security> section. They import OpenSSL (libcrypto-3-x64.dll),
# which is NOT shipped: the application provides it (see README).
foreach ($file in @("dds_security_auth.dll", "dds_security_ac.dll", "dds_security_crypto.dll")) {
    $src = Join-Path $BinDir $file
    if (-not (Test-Path $src)) { throw "Missing DDS Security plugin $file in $BinDir." }
    Copy-Item -Path $src -Destination $ArtifactsDir -Force
    Write-Host "  [+] Copied $file" -ForegroundColor Green
}

# Can we locate VC++ runtime?
# Usually in $env:SystemRoot\System32 but dependent on VS installation.
# However, redistributables might not be legally redistributable by simple copy 
# without an installer merge module, BUT NuGet packages often bundle them anyway.
# For now, let's warn if we can't find them easily, or skip copy if not present in bin output.
# (If cyclonedds build copies them to bin, we are good).

Write-Host "`nNative build complete." -ForegroundColor Green
