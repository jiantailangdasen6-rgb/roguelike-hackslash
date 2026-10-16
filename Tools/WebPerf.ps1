# Measures the local WebGL build (Builds/WebGL) in a headless Edge: a solo bot run with -perf.
# Prints the [Perf] lines (frame time, sprites, lights, memory) and how long the page took to reach the camp.
#   powershell -File Tools/WebPerf.ps1 [-Seconds 90] [-Class warrior] [-Extra "&startroom=3"]
param([int]$Seconds = 90, [string]$Class = 'warrior', [string]$Extra = '', [int]$Port = 8099)
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'Logs\webperf'
New-Item -ItemType Directory -Force $out | Out-Null
Remove-Item "$out\edge.log" -ErrorAction SilentlyContinue

$server = Start-Process powershell -PassThru -WindowStyle Hidden -ArgumentList @('-ExecutionPolicy', 'Bypass', '-File', "`"$root\Tools\ServeWeb.ps1`"", '-Port', "$Port")
Start-Sleep -Seconds 2
$profileDir = Join-Path $env:TEMP 'roguelike-webperf-edge'
Remove-Item -Recurse -Force $profileDir -ErrorAction SilentlyContinue
$edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
$page = "http://localhost:$Port/?autosolo&botmove&perf&autoquit=$Seconds&class=$Class$Extra"
$start = Get-Date
$browser = Start-Process $edge -PassThru -ArgumentList @('--headless=new', "--user-data-dir=`"$profileDir`"",
    '--window-size=1280,720', '--mute-audio', '--enable-logging=stderr', '--v=0', '--enable-unsafe-swiftshader', "`"$page`"") -RedirectStandardError "$out\edge.log"

$ready = $null
$deadline = $start.AddSeconds($Seconds + 120)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 1
    if (-not $ready -and (Select-String -Path "$out\edge.log" -Pattern '\[Perf\]' -Quiet -ErrorAction SilentlyContinue)) {
        $ready = ((Get-Date) - $start).TotalSeconds
    }
    if (Select-String -Path "$out\edge.log" -Pattern 'FINAL' -Quiet -ErrorAction SilentlyContinue) { break }
}
Get-Process msedge -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $edge -and $_.StartTime -ge $browser.StartTime } | Stop-Process -Force -ErrorAction SilentlyContinue
Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" | Where-Object { $_.CommandLine -like '*ServeWeb.ps1*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

Select-String -Path "$out\edge.log" -Pattern '\[Perf\]|FINAL' | ForEach-Object {
    $line = $_.Line
    $i = $line.IndexOf('[Perf]'); if ($i -lt 0) { $i = $line.IndexOf('[AutoTest]') }
    if ($i -ge 0) { $line.Substring($i) -replace '", source.*$', '' }
}
if ($ready) { Write-Host ("first perf line after {0:0} s (load + camp + run start)" -f $ready) } else { Write-Host 'NO PERF LINES' }
