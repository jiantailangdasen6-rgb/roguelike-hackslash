# Resume test over Unity Relay: three Windows instances play; at -FailAt seconds the host acts as if its
# connection broke (-failat), keeps the run, opens a new room straight into it (-autoresume) and writes the
# new code; the clients (back at the title) rejoin with it and get their characters back.
#   powershell -File Tools/ResumeTest.ps1 [-Seconds 150] [-FailAt 70]
param([int]$Seconds = 150, [int]$FailAt = 70)

$projectPath = Split-Path -Parent $PSScriptRoot
$exe = "$projectPath\Builds\Windows\LabyrinthSurvivor.exe"
$out = "$projectPath\Logs\resume"
New-Item -ItemType Directory -Force $out | Out-Null
Remove-Item "$out\*" -ErrorAction SilentlyContinue
$code = "$out\code.txt"

$common = @('-botmove', '-mute', '-screen-fullscreen', '0', '-screen-width', '960', '-screen-height', '540')
$hostProc = Start-Process $exe -PassThru -ArgumentList ($common + @(
    '-onlinehost', '3', '-codefile', "`"$code`"", '-profile', 'rhost', '-class', 'warrior', '-autoquit', "$Seconds",
    '-failat', "$FailAt", '-autoresume', '-logFile', "`"$out\host.log`""))
$c1 = Start-Process $exe -PassThru -ArgumentList ($common + @(
    '-onlinejoinfile', "`"$code`"", '-profile', 'rc1', '-class', 'mage', '-autoquit', "$($Seconds - 2)",
    '-screenshot', "`"$out\client1.png`"", '-screenshotat', "$($Seconds - 8)", '-logFile', "`"$out\client1.log`""))
$c2 = Start-Process $exe -PassThru -ArgumentList @(
    '-batchmode', '-nographics', '-botmove', '-mute', '-onlinejoinfile', "`"$code`"", '-profile', 'rc2', '-class', 'ranger',
    '-autoquit', "$($Seconds - 3)", '-logFile', "`"$out\client2.log`"")

foreach ($p in @($hostProc, $c1, $c2)) {
    if (-not $p.WaitForExit(($Seconds + 60) * 1000)) { $p.Kill(); Write-Host "killed $($p.Id) (timeout)" }
}

$failed = $false
Select-String -Path "$out\host.log" -Pattern '\[RunDirector\] (Run suspended|Run resumed|Rejoined|Joined)' | ForEach-Object { $_.Line }
if (-not (Select-String -Path "$out\host.log" -Pattern 'Run resumed' -Quiet)) { Write-Host 'FAIL: the host did not resume'; $failed = $true }
$rejoined = (Select-String -Path "$out\host.log" -Pattern 'Rejoined' | Measure-Object).Count
if ($rejoined -lt 2) { Write-Host "FAIL: only $rejoined of 2 clients came back with their characters"; $failed = $true }
foreach ($name in 'host', 'client1', 'client2') {
    $final = Select-String -Path "$out\$name.log" -Pattern '\[AutoTest\] FINAL' | Select-Object -Last 1
    Write-Host "=== $name"
    if ($final -ne $null) { $final.Line }
    if ($final -eq $null -or $final.Line -notmatch 'started=True') { Write-Host "FAIL: $name not in the run at the end"; $failed = $true }
}
if ($failed) { exit 1 }
Write-Host 'RESUME OK'
