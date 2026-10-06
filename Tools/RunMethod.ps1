# Runs a static editor method in batch mode and prints compile errors / exceptions / tagged log lines.
# The Unity Editor must NOT have this project open.
#   powershell -File Tools/RunMethod.ps1 -Method Game.EditorTools.PrototypeBuilder.Build
param(
    [Parameter(Mandatory = $true)][string]$Method,
    [string]$Tag = '\[(PrototypeBuilder|PlayerBuild)\]'
)

$projectPath = Split-Path -Parent $PSScriptRoot
$version = (Select-String -Path "$projectPath\ProjectSettings\ProjectVersion.txt" -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
$log = "$projectPath\Logs\batch-$($Method.Split('.')[-1]).log"

$p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList @(
    '-batchmode', '-quit', '-projectPath', "`"$projectPath`"",
    '-executeMethod', $Method, '-logFile', "`"$log`"")

Write-Host "exit: $($p.ExitCode)  log: $log"
Select-String -Path $log -Pattern "error CS|Exception|$Tag" |
    ForEach-Object { $_.Line } | Select-Object -Unique -First 40
exit $p.ExitCode
