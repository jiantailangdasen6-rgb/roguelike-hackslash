# Cross-platform online test: a Windows host creates a Relay room, a headless Edge opens the published
# browser build with ?onlinejoin=CODE&botmove and joins. Success = the host sees 2 players in the run.
#   powershell -File Tools/CrossTest.ps1 [-Seconds 100] [-Url https://jiantailangdasen6-rgb.github.io/roguelike-web/]
param(
    [int]$Seconds = 100,
    [string]$Url = 'https://jiantailangdasen6-rgb.github.io/roguelike-web/'
)
$project = Split-Path -Parent $PSScriptRoot
$out = "$project\Logs\online"
New-Item -ItemType Directory -Force $out | Out-Null
Remove-Item "$out\xcode.txt", "$out\xhost.log" -ErrorAction SilentlyContinue

$hostProc = Start-Process "$project\Builds\Windows\LabyrinthOfTheDead.exe" -PassThru -ArgumentList @(
    '-botmove', '-screen-fullscreen', '0', '-screen-width', '960', '-screen-height', '540',
    '-onlinehost', '2', '-codefile', "`"$out\xcode.txt`"", '-profile', 'xhost', '-class', 'warrior',
    '-autoquit', "$Seconds", '-logFile', "`"$out\xhost.log`"")
for ($i = 0; $i -lt 60 -and -not (Test-Path "$out\xcode.txt"); $i++) { Start-Sleep -Milliseconds 500 }
if (-not (Test-Path "$out\xcode.txt")) { Write-Host 'host did not create a room'; exit 1 }
$code = (Get-Content "$out\xcode.txt").Trim()
Write-Host "room code: $code"

$profileDir = Join-Path $env:TEMP 'roguelike-crosstest-edge'
$edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
$page = "$($Url)?onlinejoin=$code&botmove&profile=web"
$browser = Start-Process $edge -PassThru -ArgumentList @('--headless=new', "--user-data-dir=`"$profileDir`"",
    '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--autoplay-policy=no-user-gesture-required',
    '--window-size=1280,720', '--enable-logging=stderr', '--v=0', "`"$page`"") -RedirectStandardError "$out\edge.log"

$hostProc.WaitForExit(($Seconds + 30) * 1000) | Out-Null
Get-Process msedge -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $edge -and $_.StartTime -ge $browser.StartTime } | Stop-Process -Force -ErrorAction SilentlyContinue

Select-String -Path "$out\xhost.log" -Pattern '\[AutoTest\]' | Select-Object -Last 4 | ForEach-Object { $_.Line }
$joined = Select-String -Path "$out\xhost.log" -Pattern 'peers=2' -Quiet
Write-Host $(if ($joined) { 'CROSS TEST OK: browser joined the desktop host' } else { 'CROSS TEST FAILED: browser never joined' })
