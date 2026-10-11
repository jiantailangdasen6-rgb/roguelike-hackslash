# Late join, reconnect and host-away test on this PC (LAN loopback, bots, muted):
#   host (warrior) starts with client 1 (mage); client 1 drops out at ~20 s and comes back by itself;
#   the host "goes away" (as if its browser tab were hidden) for 6 s around 30 s into the run;
#   client 2 (ranger) joins the run already in progress at ~45 s.
# Screenshots + logs in Logs/join/. Prints the lines that prove each step.
#   powershell -File Tools/JoinTest.ps1
$projectPath = Split-Path -Parent $PSScriptRoot
$exe = "$projectPath\Builds\Windows\LabyrinthSurvivor.exe"
$out = "$projectPath\Logs\join"
New-Item -ItemType Directory -Force $out | Out-Null
Get-ChildItem $out -File -ErrorAction SilentlyContinue | Remove-Item -ErrorAction SilentlyContinue

$common = @('-botmove', '-localonly', '-screen-fullscreen', '0', '-screen-width', '960', '-screen-height', '540')
$hostProc = Start-Process $exe -PassThru -ArgumentList ($common + @(
    '-autohost', '2', '-profile', 'jhost', '-class', 'warrior', '-autoquit', '80', '-awayat', '30', '-awayfor', '6',
    '-screenshot', "`"$out\host.png`"", '-screenshotat', '60', '-logFile', "`"$out\host.log`""))
Start-Sleep -Seconds 4
$c1 = Start-Process $exe -PassThru -ArgumentList ($common + @(
    '-autojoin', '127.0.0.1', '-profile', 'jc1', '-class', 'mage', '-autoquit', '74', '-dropat', '20',
    '-screenshot', "`"$out\client1.png`"", '-screenshotat', '34,50', '-logFile', "`"$out\client1.log`""))
Start-Sleep -Seconds 41
$c2 = Start-Process $exe -PassThru -ArgumentList ($common + @(
    '-autojoin', '127.0.0.1', '-profile', 'jc2', '-class', 'ranger', '-autoquit', '30',
    '-screenshot', "`"$out\client2.png`"", '-screenshotat', '12', '-logFile', "`"$out\client2.log`""))

foreach ($p in @($hostProc, $c1, $c2)) {
    if (-not $p.WaitForExit(140000)) { $p.Kill(); Write-Host "killed $($p.Id) (timeout)" }
}

Write-Host '=== host'
Select-String -Path "$out\host.log" -Pattern 'Run started|Joined mid-run|Rejoined|\[AutoTest\] FINAL' | ForEach-Object { $_.Line }
Select-String -Path "$out\host.log" -Pattern 'Exception' | Select-Object -First 5 | ForEach-Object { '  ! ' + $_.Line }
foreach ($name in 'client1', 'client2') {
    Write-Host "=== $name"
    Select-String -Path "$out\$name.log" -Pattern 'dropping out|\[AutoTest\] FINAL' | ForEach-Object { $_.Line }
    Select-String -Path "$out\$name.log" -Pattern 'Exception' | Select-Object -First 5 | ForEach-Object { '  ! ' + $_.Line }
}
