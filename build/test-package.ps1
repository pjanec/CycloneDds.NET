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

if (-not ($ok -and $code -eq 200)) {
    Uninstall-DdsMonitorTool
    Write-Host "  [-] ddsmonitor check FAILED (started=$ok, http=$code). Log tail:" -ForegroundColor Red
    Get-Content $out, $err -Tail 20 -ErrorAction SilentlyContinue
    exit 1
}
Write-Host "  [+] ddsmonitor started (port $port) and served HTTP 200 - native loaded OK"

# ---------------------------------------------------------------------------------
# DDS round trip through the installed tool: the smoke app publishes, a headless ddsmonitor
# records. Proves the packaged native library works for real pub/sub between processes, not
# just that it loads. Then the same with DDS Security on, plus an unsecured recorder that must
# receive nothing. Mirrors build/test-package.sh.
# ---------------------------------------------------------------------------------
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("ddsmon-pkgtest-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force $work | Out-Null
$smokeDir = Join-Path $RepoRoot "examples\PackageSmokeTest\bin\Release\net8.0"
$smokeExe = Join-Path $smokeDir "PackageSmokeTest.exe"   # the apphost: Windows looks for the security plugins beside the started exe, not beside dotnet.exe
$secDir = Join-Path $RepoRoot "tests\CycloneDDS.Runtime.Tests\Resources\Security"
$recSeconds = 12

# Start a headless recorder (runs RecordDurationSeconds, then stops cleanly). $uri = CYCLONEDDS_URI or "".
# $extraPath is prepended to PATH for this child only (OpenSSL for the secured ones).
function Start-Recorder($name, $domain, $uri, $extraPath) {
    $saved = $env:PATH
    if ($uri) { $env:CYCLONEDDS_URI = $uri } else { Remove-Item Env:CYCLONEDDS_URI -ErrorAction SilentlyContinue }
    if ($extraPath) { $env:PATH = "$extraPath;$saved" }
    try {
        $a = @("--DdsSettings:DomainId=$domain", "--AppSettings:ConfigFolder=$work\cfg-$name",
               "--AppSettings:TopicSources:0=$smokeDir", "--DdsSettings:HeadlessMode=Record",
               "--DdsSettings:HeadlessFilePath=$work\$name.jsonl", "--DdsSettings:RecordDurationSeconds=$recSeconds")
        $p = Start-Process -FilePath $toolExe -ArgumentList $a -RedirectStandardOutput "$work\$name.log" `
            -RedirectStandardError "$work\$name.err" -PassThru -WindowStyle Hidden
        $null = $p.Handle   # Windows PowerShell 5.1: without the handle cached, ExitCode stays empty after the process ends
        $p
    } finally {
        $env:PATH = $saved
        Remove-Item Env:CYCLONEDDS_URI -ErrorAction SilentlyContinue
    }
}

# Start the smoke app publishing for 8 s.
function Start-Publisher($name, $domain, $uri, $extraPath) {
    $saved = $env:PATH
    if ($uri) { $env:CYCLONEDDS_URI = $uri } else { Remove-Item Env:CYCLONEDDS_URI -ErrorAction SilentlyContinue }
    if ($extraPath) { $env:PATH = "$extraPath;$saved" }
    try {
        $p = Start-Process -FilePath $smokeExe -ArgumentList @("publish", "$domain", "8") -RedirectStandardOutput "$work\$name.log" `
            -RedirectStandardError "$work\$name.err" -PassThru -WindowStyle Hidden
        $null = $p.Handle
        $p
    } finally {
        $env:PATH = $saved
        Remove-Item Env:CYCLONEDDS_URI -ErrorAction SilentlyContinue
    }
}

# Samples recorded by an instance: complete lines of its JSON Lines capture.
function Get-Recorded($name) {
    $f = "$work\$name.jsonl"
    if (Test-Path $f) { @(Get-Content $f | Where-Object { $_ }).Count } else { 0 }
}

# True when every process exited by itself with the expected code (recorders and the publisher stop on their own).
function Wait-AllExit($procs) {
    foreach ($p in $procs) { if (-not $p.WaitForExit(60000)) { $p.Kill(); return $false } }
    $true
}

function Show-Logs($names) {
    foreach ($n in $names) {
        foreach ($ext in 'log', 'err') {
            $f = "$work\$n.$ext"
            if ((Test-Path $f) -and (Get-Item $f).Length -gt 0) { Write-Host "--- $n.$ext"; Get-Content $f -Tail 15 }
        }
    }
}

# OpenSSL 3 runtime (libcrypto-3-x64.dll): OPENSSL_BIN, else the usual install folders, else already on PATH.
function Find-OpenSslBin {
    $cands = @($env:OPENSSL_BIN, "C:\Program Files\OpenSSL-Win64\bin", "C:\Program Files\OpenSSL\bin", "C:\OpenSSL-Win64\bin") +
             ($env:PATH -split ';')
    foreach ($c in $cands) { if ($c -and (Test-Path (Join-Path $c "libcrypto-3-x64.dll"))) { return $c } }
    $null
}

$failed = $false
try {
    # Random domain per run, so concurrent runs on one machine do not see each other.
    $domain = Get-Random -Minimum 100 -Maximum 190

    $rec = Start-Recorder "rec" $domain "" $null
    Start-Sleep -Seconds 3
    $pub = Start-Publisher "pub" $domain "" $null
    $ran = Wait-AllExit @($pub, $rec)
    $got = Get-Recorded "rec"
    if ($ran -and $pub.ExitCode -eq 0 -and $rec.ExitCode -eq 0 -and $got -gt 0) {
        Write-Host "  [+] DDS round trip, smoke app -> ddsmonitor (topic type loaded via TopicSources): $got samples received"
    } else {
        Write-Host "  [-] ddsmonitor DDS round trip FAILED (received: $got, exit codes pub/rec: $($pub.ExitCode)/$($rec.ExitCode)). Logs:" -ForegroundColor Red
        Show-Logs @("pub", "rec")
        $failed = $true
    }

    if (-not $failed) {
        $ossl = Find-OpenSslBin
        if ((Test-Path (Join-Path $secDir "identity_cert.pem")) -and $ossl) {
            $w = $work -replace '\\', '/'
            $s = $secDir -replace '\\', '/'
            # The configuration file is given as file://D:/... ; the certificate paths inside it as file:D:/...
            @"
<CycloneDDS><Domain id="any"><Security>
  <Authentication>
    <IdentityCertificate>file:$s/identity_cert.pem</IdentityCertificate>
    <IdentityCA>file:$s/ca_cert.pem</IdentityCA>
    <PrivateKey>file:$s/identity_key.pem</PrivateKey>
  </Authentication>
  <AccessControl>
    <PermissionsCA>file:$s/ca_cert.pem</PermissionsCA>
    <Governance>file:$s/governance.p7s</Governance>
    <Permissions>file:$s/permissions.p7s</Permissions>
  </AccessControl>
</Security></Domain></CycloneDDS>
"@ | Set-Content -Encoding ASCII "$work\secure.xml"
            $uri = "file://$w/secure.xml"
            $domain = $domain + 1
            # Only OpenSSL is added to PATH: the monitor must find its own security plugins (the tool's store
            # folder is not the folder of the ddsmonitor.exe launcher).
            $srec = Start-Recorder "srec" $domain $uri $ossl
            $splain = Start-Recorder "splain" $domain "" $null
            Start-Sleep -Seconds 3
            $spub = Start-Publisher "spub" $domain $uri $ossl
            $ran = Wait-AllExit @($spub, $srec, $splain)
            $gotSecured = Get-Recorded "srec"
            $gotPlain = Get-Recorded "splain"
            if ($ran -and $spub.ExitCode -eq 0 -and $srec.ExitCode -eq 0 -and $splain.ExitCode -eq 0 -and $gotSecured -gt 0 -and $gotPlain -eq 0) {
                Write-Host "  [+] ddsmonitor DDS Security: secured recorder got $gotSecured samples, unsecured got none"
            } else {
                Write-Host "  [-] ddsmonitor DDS Security check FAILED (secured: $gotSecured, unsecured: $gotPlain, exit codes spub/srec/splain: $($spub.ExitCode)/$($srec.ExitCode)/$($splain.ExitCode)). Logs:" -ForegroundColor Red
                Show-Logs @("spub", "srec", "splain")
                $failed = $true
            }
        } else {
            Write-Host "  [ ] DDS Security check skipped (no test fixtures or no OpenSSL 3; set OPENSSL_BIN to the folder holding libcrypto-3-x64.dll)"
        }
    }
} finally {
    Uninstall-DdsMonitorTool
    if (-not $failed) { Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue }
}

if ($failed) { Write-Host "  (logs kept in $work)"; exit 1 }
Write-Host "`nAll good."
exit 0
