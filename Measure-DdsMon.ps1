param(
    [string]$Rate = "idle",
    [int]$Duration = 30
)

# Kill any existing DdsMonitor processes
Get-Process -Name "DdsMonitor" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

$exe = "d:\Work\FastCycloneDdsCsharpBindings\tools\DdsMonitor\DdsMonitor.Blazor\bin\Release\net8.0\DdsMonitor.exe"
$baseArgs = "--BrowserLifecycle:ConnectTimeout=3600 --BrowserLifecycle:DisconnectTimeout=3600"

$args = switch ($Rate) {
    "idle"  { $baseArgs }
    "10hz"  { "$baseArgs --DdsSettings:SelfSendEnabled=true --DdsSettings:SelfSendRateHz=10" }
    "100hz" { "$baseArgs --DdsSettings:SelfSendEnabled=true --DdsSettings:SelfSendRateHz=100" }
    "1khz"  { "$baseArgs --DdsSettings:SelfSendEnabled=true --DdsSettings:SelfSendRateHz=1000" }
    "10khz" { "$baseArgs --DdsSettings:SelfSendEnabled=true --DdsSettings:SelfSendRateHz=10000" }
    default { $baseArgs }
}

Write-Host "Starting DdsMonitor with args: $args"
$proc = Start-Process $exe -ArgumentList $args -PassThru
$apid = $proc.Id
Write-Host "PID=$apid  Rate=$Rate  Duration=${Duration}s"

Write-Host "Waiting 8s for startup..."
Start-Sleep -Seconds 8

# Check alive
$chk = Get-Process -Id $apid -ErrorAction SilentlyContinue
if (-not $chk) {
    Write-Host "ERROR: Process exited during startup!"
    exit 1
}
Write-Host "Process alive. Starting measurement..."

# Measure
$readings = @()
$intervalSec = 2
$samples = [int]($Duration / $intervalSec)

for ($i = 0; $i -lt $samples; $i++) {
    $p = Get-Process -Id $apid -ErrorAction SilentlyContinue
    if (-not $p) {
        Write-Host "Process exited at sample $i"
        break
    }
    $readings += [PSCustomObject]@{
        CpuSec = $p.TotalProcessorTime.TotalSeconds
        MemMB  = [math]::Round($p.WorkingSet64 / 1MB, 1)
    }
    Start-Sleep -Seconds $intervalSec
}

# Kill app
Stop-Process -Id $apid -Force -ErrorAction SilentlyContinue

# Report
Write-Host ""
Write-Host "=== CPU Report: $Rate ($($readings.Count) samples x ${intervalSec}s) ==="
if ($readings.Count -lt 2) {
    Write-Host "Not enough samples"
    exit 1
}

$cpuPcts = @()
for ($i = 1; $i -lt $readings.Count; $i++) {
    $pct = [math]::Round(($readings[$i].CpuSec - $readings[$i-1].CpuSec) / $intervalSec * 100, 2)
    $cpuPcts += $pct
    Write-Host "  s$($i*$intervalSec): CPU=$pct%  WS=$($readings[$i].MemMB)MB"
}

$avg = [math]::Round(($cpuPcts | Measure-Object -Average).Average, 2)
$max = [math]::Round(($cpuPcts | Measure-Object -Maximum).Maximum, 2)
$min = [math]::Round(($cpuPcts | Measure-Object -Minimum).Minimum, 2)
Write-Host ""
Write-Host "=== Summary: avg=$avg%  min=$min%  max=$max% ==="
