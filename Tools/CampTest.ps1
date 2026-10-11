# The party stays together between runs: a host and two clients (Windows build, loopback) gather in the camp,
# set out, get wiped (-wipeat), return to the camp from the result screen (-autoreturn) and set out again.
# Checks that the whole party of 3 came back to the camp and that two runs started with 3 players,
# and that both clients were still connected at the end. Logs in Logs/camp/.
#   powershell -File Tools/CampTest.ps1 [-Seconds 75]
param([int]$Seconds = 75)

$projectPath = Split-Path -Parent $PSScriptRoot
$exe = "$projectPath\Builds\Windows\LabyrinthSurvivor.exe"
$out = "$projectPath\Logs\camp"
New-Item -ItemType Directory -Force $out | Out-Null
Remove-Item "$out\*" -ErrorAction SilentlyContinue

$common = @('-botmove', '-localonly', '-batchmode', '-nographics')
$hostProc = Start-Process $exe -PassThru -ArgumentList ($common + @(
    '-autohost', '3', '-profile', 'camphost', '-class', 'warrior', '-wipeat', '28', '-autoreturn',
    '-autoquit', "$Seconds", '-logFile', "`"$out\host.log`""))
Start-Sleep -Seconds 4
$c1 = Start-Process $exe -PassThru -ArgumentList ($common + @(
    '-autojoin', '127.0.0.1', '-profile', 'campc1', '-class', 'mage', '-autoquit', "$($Seconds - 9)", '-logFile', "`"$out\client1.log`""))
$c2 = Start-Process $exe -PassThru -ArgumentList ($common + @(
    '-autojoin', '127.0.0.1', '-profile', 'campc2', '-class', 'ranger', '-autoquit', "$($Seconds - 10)", '-logFile', "`"$out\client2.log`""))

foreach ($p in @($hostProc, $c1, $c2)) {
    if (-not $p.WaitForExit(($Seconds + 60) * 1000)) { $p.Kill(); Write-Host "killed $($p.Id) (timeout)" }
}

$ok = $true
$camps = (Select-String -Path "$out\host.log" -Pattern 'Party of 3 in the camp').Count
$runs = (Select-String -Path "$out\host.log" -Pattern 'Run started with 3 player').Count
$wiped = (Select-String -Path "$out\host.log" -Pattern 'wiped the party').Count
Write-Host "host: camp gatherings of 3 = $camps, runs with 3 = $runs, wipes = $wiped"
# The first gathering logs fewer (friends join after the host is already in the camp); the return must have all 3.
if ($camps -lt 1 -or $runs -lt 2 -or $wiped -lt 1) { $ok = $false }
foreach ($name in 'host', 'client1', 'client2') {
    $log = "$out\$name.log"
    $final = Select-String -Path $log -Pattern '\[AutoTest\] FINAL' | Select-Object -Last 1
    Write-Host "=== $name"
    if ($final) { Write-Host $final.Line } else { Write-Host '  (no FINAL line)'; $ok = $false }
    # Clients quit a few seconds before the host, so each must still be in the session when it does.
    if ($name -ne 'host' -and $final -and $final.Line -notmatch 'role=client') { $ok = $false }
    Select-String -Path $log -Pattern 'Exception|\[Netcode\].*(Error|error)' | Select-Object -First 6 | ForEach-Object { "  ! " + $_.Line }
}
if ($ok) { Write-Host 'CAMP TEST OK' } else { Write-Host 'CAMP TEST FAILED'; exit 1 }
