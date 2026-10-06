# Captures showcase screenshots from the Windows build into Logs/shots/ (opens game windows briefly).
#   powershell -File Tools/Screenshots.ps1
$projectPath = Split-Path -Parent $PSScriptRoot
$exe = "$projectPath\Builds\Windows\LabyrinthSurvivors.exe"
$out = "$projectPath\Logs\shots"
New-Item -ItemType Directory -Force $out | Out-Null

$window = @('-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720')
$runs = @(
    @{ Name = 'lobby'; Args = @('-autoquit', '7', '-screenshotat', '4') },
    @{ Name = 'bag'; Args = @('-profile', 'host', '-openbag', '-autoquit', '7', '-screenshotat', '4') },
    @{ Name = 'solo'; Args = @('-autosolo', '-botmove', '-profile', 'solo', '-autoquit', '75', '-screenshotat', '70') }
)
foreach ($run in $runs) {
    $p = Start-Process $exe -PassThru -ArgumentList ($window + $run.Args + @(
        '-screenshot', "`"$out\$($run.Name).png`"", '-logFile', "`"$out\$($run.Name).log`""))
    if (-not $p.WaitForExit(120000)) { $p.Kill() }
    Write-Host "$($run.Name): $(Test-Path "$out\$($run.Name).png")"
}
