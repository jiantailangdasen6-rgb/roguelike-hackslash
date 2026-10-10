# Launches a host and two clients of the Windows build on this PC (loopback only), lets bots play,
# then prints each instance's final status line. Host and client 1 run windowed and save screenshots
# to Logs/net/; client 2 runs headless.
#   powershell -File Tools/NetTest.ps1 [-Seconds 70]
param([int]$Seconds = 70)

$projectPath = Split-Path -Parent $PSScriptRoot
$exe = "$projectPath\Builds\Windows\LabyrinthOfTheDead.exe"
$out = "$projectPath\Logs\net"
New-Item -ItemType Directory -Force $out | Out-Null
Remove-Item "$out\*" -ErrorAction SilentlyContinue

$common = @('-botmove', '-localonly', '-screen-fullscreen', '0', '-screen-width', '960', '-screen-height', '540')
$shotAt = [Math]::Max(10, $Seconds - 25)

$hostProc = Start-Process $exe -PassThru -ArgumentList ($common + @(
    '-autohost', '3', '-profile', 'host', '-class', 'warrior', '-autoquit', "$Seconds",
    '-screenshot', "`"$out\host.png`"", '-screenshotat', "$shotAt", '-logFile', "`"$out\host.log`""))
Start-Sleep -Seconds 4
$c1 = Start-Process $exe -PassThru -ArgumentList ($common + @(
    '-autojoin', '127.0.0.1', '-profile', 'c1', '-class', 'mage', '-autoquit', "$($Seconds - 3)",
    '-screenshot', "`"$out\client1.png`"", '-screenshotat', "$($shotAt - 3)", '-logFile', "`"$out\client1.log`""))
$c2 = Start-Process $exe -PassThru -ArgumentList @(
    '-batchmode', '-nographics', '-botmove', '-autojoin', '127.0.0.1', '-profile', 'c2', '-class', 'ranger',
    '-autoquit', "$($Seconds - 4)", '-logFile', "`"$out\client2.log`"")

foreach ($p in @($hostProc, $c1, $c2)) {
    if (-not $p.WaitForExit(($Seconds + 60) * 1000)) { $p.Kill(); Write-Host "killed $($p.Id) (timeout)" }
}

foreach ($name in 'host', 'client1', 'client2') {
    $log = "$out\$name.log"
    Write-Host "=== $name"
    Select-String -Path $log -Pattern '\[AutoTest\] FINAL' | ForEach-Object { $_.Line }
    Select-String -Path $log -Pattern 'Exception|\[Netcode\].*(Error|error)' | Select-Object -First 8 | ForEach-Object { "  ! " + $_.Line }
}
