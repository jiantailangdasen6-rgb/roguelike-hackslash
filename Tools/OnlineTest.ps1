# Online (Unity Relay, room code) test with the Windows build: the host writes its room code to a file,
# two clients read it and join. Bots play; prints each instance's final status and any errors.
#   powershell -File Tools/OnlineTest.ps1 [-Seconds 90]
param([int]$Seconds = 90)

$projectPath = Split-Path -Parent $PSScriptRoot
$exe = "$projectPath\Builds\Windows\LabyrinthSurvivor.exe"
$out = "$projectPath\Logs\online"
New-Item -ItemType Directory -Force $out | Out-Null
Remove-Item "$out\*" -ErrorAction SilentlyContinue
$code = "$out\code.txt"

$common = @('-botmove', '-screen-fullscreen', '0', '-screen-width', '960', '-screen-height', '540')
$hostProc = Start-Process $exe -PassThru -ArgumentList ($common + @(
    '-onlinehost', '3', '-codefile', "`"$code`"", '-profile', 'ohost', '-class', 'warrior', '-autoquit', "$Seconds",
    '-screenshot', "`"$out\host.png`"", '-screenshotat', "$($Seconds - 20)", '-logFile', "`"$out\host.log`""))
$c1 = Start-Process $exe -PassThru -ArgumentList ($common + @(
    '-onlinejoinfile', "`"$code`"", '-profile', 'oc1', '-class', 'mage', '-autoquit', "$($Seconds - 2)",
    '-screenshot', "`"$out\client1.png`"", '-screenshotat', "$($Seconds - 22)", '-logFile', "`"$out\client1.log`""))
$c2 = Start-Process $exe -PassThru -ArgumentList @(
    '-batchmode', '-nographics', '-botmove', '-onlinejoinfile', "`"$code`"", '-profile', 'oc2', '-class', 'ranger',
    '-autoquit', "$($Seconds - 3)", '-logFile', "`"$out\client2.log`"")

foreach ($p in @($hostProc, $c1, $c2)) {
    if (-not $p.WaitForExit(($Seconds + 60) * 1000)) { $p.Kill(); Write-Host "killed $($p.Id) (timeout)" }
}

if (Test-Path $code) { Write-Host "room code: $(Get-Content $code)" } else { Write-Host 'room code: (none)' }
foreach ($name in 'host', 'client1', 'client2') {
    $log = "$out\$name.log"
    Write-Host "=== $name"
    Select-String -Path $log -Pattern '\[AutoTest\] FINAL' | ForEach-Object { $_.Line }
    Select-String -Path $log -Pattern 'Exception|\[Lobby\]|Relay|Authentication' | Select-Object -First 8 | ForEach-Object { '  ! ' + $_.Line }
}
