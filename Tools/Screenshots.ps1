# Captures showcase screenshots from the Windows build into Logs/shots/ (opens game windows briefly).
#   powershell -File Tools/Screenshots.ps1 [-Class warrior] [-Times "8,16,24,32,45,58"]
param(
    [string]$Class = 'warrior',
    [string]$Times = '8,16,24,32,45,58',
    [string]$Name = '',
    [string]$Extra = ''
)
$projectPath = Split-Path -Parent $PSScriptRoot
$exe = "$projectPath\Builds\Windows\LabyrinthSurvivor.exe"
$out = "$projectPath\Logs\shots"
New-Item -ItemType Directory -Force $out | Out-Null

$last = [int](($Times -split ',')[-1]) + 4
$window = @('-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720')
$runs = @(
    @{ Name = 'lobby'; Args = @('-profile', 'shots', '-autoquit', '6', '-screenshotat', '4') },
    @{ Name = $(if ($Name) { $Name } else { "solo_$Class" }); Args = @('-autosolo', '-botmove', '-profile', 'shots', '-class', $Class, '-autoquit', "$last", '-screenshotat', $Times) + @($Extra -split ' ' | Where-Object { $_ }) }
)
foreach ($run in $runs) {
    $p = Start-Process $exe -PassThru -ArgumentList ($window + $run.Args + @(
        '-screenshot', "`"$out\$($run.Name).png`"", '-logFile', "`"$out\$($run.Name).log`""))
    if (-not $p.WaitForExit(240000)) { $p.Kill() }
    Write-Host "$($run.Name): done"
    Select-String -Path "$out\$($run.Name).log" -Pattern '\[AutoTest\] FINAL|Exception' | Select-Object -First 6 | ForEach-Object { "  " + $_.Line }
}
