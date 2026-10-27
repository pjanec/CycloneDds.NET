<#
.SYNOPSIS
  Smoke-test the CycloneDDS.NET NuGet package (and the DdsMonitor global tool)
  from a local feed - whether built locally (build\pack.ps1) or downloaded from a
  CI run (the 'nuget-packages' artifact).

.DESCRIPTION
  Picks the NEWEST matching package by LastWriteTime (so a cluttered
  artifacts\nuget with many historical builds is fine), then consumes it as a real
  PackageReference via examples\PackageSmokeTest: restore -> run the bundled code
  generator (idlc) -> publish/subscribe round-trip. Optionally installs the
  ddsmonitor tool, confirms it serves HTTP, and uninstalls it.

.EXAMPLE
  .\build\test-package.ps1
  .\build\test-package.ps1 -FeedDir C:\downloads\nuget-packages -NoDdsMon
#>
[CmdletBinding()]
param(
    [string]$FeedDir = "artifacts/nuget",
    [switch]$NoDdsMon
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Feed = (Resolve-Path $FeedDir).Path

function Get-Newest($pattern) {
    Get-ChildItem -Path (Join-Path $Feed $pattern) -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

# 'dotnet tool uninstall' exits non-zero and writes to stderr when the tool is not installed. Under
# $ErrorActionPreference = 'Stop', Windows PowerShell 5.1 turns that redirected stderr into a terminating
# NativeCommandError - so run it with 'Continue' and reset the exit code.
function Uninstall-DdsMonitorTool {
    $old = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { dotnet tool uninstall --global CycloneDDS.NET.DdsMonitor *> $null } finally { $ErrorActionPreference = $old }
    $global:LASTEXITCODE = 0
}

$rt = Get-Newest 'CycloneDDS.NET.[0-9]*.nupkg'
if (-not $rt) { throw "No CycloneDDS.NET.<version>.nupkg found in $Feed" }
$rtVer = $rt.Name -replace '^CycloneDDS\.NET\.(.+)\.nupkg$', '$1'

Write-Host "============================================================"
Write-Host "  Package smoke test"
Write-Host "  feed:    $Feed"
Write-Host "  package: $($rt.Name)  (version $rtVer)"
Write-Host "============================================================"

Remove-Item -Recurse -Force `
    "$env:USERPROFILE\.nuget\packages\cyclonedds.net", `
    "$env:USERPROFILE\.nuget\packages\cyclonedds.net.ddsmonitor" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force `
    "$RepoRoot\examples\PackageSmokeTest\bin", `
    "$RepoRoot\examples\PackageSmokeTest\obj" -ErrorAction SilentlyContinue

Write-Host "`n[1/2] Running examples/PackageSmokeTest against $rtVer ..."
dotnet run --project "$RepoRoot\examples\PackageSmokeTest" -c Release `
    -p:SmokePkgVersion=$rtVer -p:RestoreAdditionalSources=$Feed
if ($LASTEXITCODE -ne 0) { throw "runtime package smoke test FAILED" }
Write-Host "  [+] runtime package smoke test PASSED"

if ($NoDdsMon) { Write-Host "`nSkipping DdsMonitor tool check (-NoDdsMon)."; Write-Host "All good."; exit 0 }

$mon = Get-Newest 'CycloneDDS.NET.DdsMonitor.*.nupkg'
if (-not $mon) { Write-Host "`nNo DdsMonitor package in feed - skipping tool check."; Write-Host "All good."; exit 0 }
$monVer = $mon.Name -replace '^CycloneDDS\.NET\.DdsMonitor\.(.+)\.nupkg$', '$1'

Write-Host "`n[2/2] Testing the ddsmonitor global tool $monVer ..."
Uninstall-DdsMonitorTool
dotnet tool install --global --add-source $Feed --version $monVer CycloneDDS.NET.DdsMonitor | Out-Null

# Invoke by absolute path: the global-tools dir may not be on PATH in this session
# (e.g. right after `dotnet tool install --global` in CI).
$toolExe = Join-Path $env:USERPROFILE ".dotnet\tools\ddsmonitor.exe"
if (-not (Test-Path $toolExe)) { $toolExe = "ddsmonitor" }

$out = [System.IO.Path]::GetTempFileName()
$err = [System.IO.Path]::GetTempFileName()
$proc = Start-Process -FilePath $toolExe -ArgumentList "--NoBrowser true" `
    -RedirectStandardOutput $out -RedirectStandardError $err -PassThru -NoNewWindow

$ok = $false; $port = $null
for ($i = 0; $i -lt 30; $i++) {
    $txt = (Get-Content $out, $err -Raw -ErrorAction SilentlyContinue) -join "`n"
    if ($txt -match 'Now listening on: http://127\.0\.0\.1:(\d+)') { $port = $Matches[1] }
    if ($txt -match 'Application started') { $ok = $true; break }
    if ($proc.HasExited) { break }
    Start-Sleep -Milliseconds 500
}

$code = $null
if ($port) {
    try { $code = (Invoke-WebRequest -Uri "http://127.0.0.1:$port/" -UseBasicParsing -TimeoutSec 5).StatusCode } catch {}
}

if (-not $proc.HasExited) { $proc.Kill(); $proc.WaitForExit() }
Uninstall-DdsMonitorTool

if ($ok -and $code -eq 200) {
    Write-Host "  [+] ddsmonitor started (port $port) and served HTTP 200 - native loaded OK"
    Write-Host "`nAll good."
    exit 0
}

Write-Host "  [-] ddsmonitor check FAILED (started=$ok, http=$code). Log tail:" -ForegroundColor Red
Get-Content $out, $err -Tail 20 -ErrorAction SilentlyContinue
exit 1
