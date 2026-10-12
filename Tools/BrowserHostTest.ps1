# Browser-hosted online test: a headless Edge opens the published page with ?onlinehost=2 and creates a Relay
# room (the page prints "[Lobby] Room code XXXX" to the console); a Windows client joins with that code.
# Success = both sides report 2 players in the run. The page and the Windows build must come from the same
# builder run (BuildWeb -> BuildWindows -> DeployWeb, then wait for Pages).
#   powershell -File Tools/BrowserHostTest.ps1 [-Seconds 120] [-Url https://jiantailangdasen6-rgb.github.io/roguelike-web/]
param(
    [int]$Seconds = 120,
    [string]$Url = 'https://jiantailangdasen6-rgb.github.io/roguelike-web/'
)
$project = Split-Path -Parent $PSScriptRoot
$out = "$project\Logs\online"
New-Item -ItemType Directory -Force $out | Out-Null
Remove-Item "$out\bcode.txt", "$out\bclient.log", "$out\bedge.log" -ErrorAction SilentlyContinue

$profileDir = Join-Path $env:TEMP 'roguelike-browserhost-edge'
if (Test-Path $profileDir) { Remove-Item $profileDir -Recurse -Force }
$edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
$page = "$($Url)?onlinehost=2&botmove&profile=webhost&mute&autoquit=$($Seconds + 30)"
$browser = Start-Process $edge -PassThru -ArgumentList @('--headless=new', "--user-data-dir=`"$profileDir`"",
    '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--autoplay-policy=no-user-gesture-required',
    '--window-size=1280,720', '--mute-audio', '--enable-logging=stderr', '--v=0', "`"$page`"") -RedirectStandardError "$out\bedge.log"

$code = $null
for ($i = 0; $i -lt 180 -and -not $code; $i++) {
    Start-Sleep -Milliseconds 500
    if (Test-Path "$out\bedge.log") {
        $m = Select-String -Path "$out\bedge.log" -Pattern '\[Lobby\] Room code ([A-Z0-9]+)' | Select-Object -First 1
        if ($m) { $code = $m.Matches[0].Groups[1].Value }
    }
}
if (-not $code) {
    Write-Host 'BROWSER HOST TEST FAILED: the page never created a room'
    Get-Process msedge -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $edge -and $_.StartTime -ge $browser.StartTime } | Stop-Process -Force -ErrorAction SilentlyContinue
    exit 1
}
Write-Host "room code (browser host): $code"
Set-Content -Path "$out\bcode.txt" -Value $code -Encoding ascii

$client = Start-Process "$project\Builds\Windows\LabyrinthSurvivor.exe" -PassThru -ArgumentList @(
    '-botmove', '-mute', '-screen-fullscreen', '0', '-screen-width', '960', '-screen-height', '540',
    '-onlinejoinfile', "`"$out\bcode.txt`"", '-profile', 'bclient', '-class', 'mage',
    '-autoquit', "$Seconds", '-logFile', "`"$out\bclient.log`"")
$client.WaitForExit(($Seconds + 60) * 1000) | Out-Null
Get-Process msedge -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $edge -and $_.StartTime -ge $browser.StartTime } | Stop-Process -Force -ErrorAction SilentlyContinue

$final = Select-String -Path "$out\bclient.log" -Pattern '\[AutoTest\] FINAL' | Select-Object -Last 1
if ($final) { $final.Line }
$hostSaw = Select-String -Path "$out\bedge.log" -Pattern '\[AutoTest\].*role=host peers=2' -Quiet
$clientIn = $final -ne $null -and $final.Line -match 'role=client' -and $final.Line -match 'started=True' -and $final.Line -match 'players=2'
if ($hostSaw -and $clientIn) { Write-Host 'BROWSER HOST TEST OK: a desktop player joined the browser host'; exit 0 }
Write-Host "BROWSER HOST TEST FAILED (host saw 2: $hostSaw, client in the run: $clientIn)"
exit 1
