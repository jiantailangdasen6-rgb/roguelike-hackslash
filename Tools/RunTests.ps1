# Runs Unity tests in batch mode. The Unity Editor must NOT have this project open.
#   powershell -File Tools/RunTests.ps1 -Platform EditMode
#   powershell -File Tools/RunTests.ps1 -Platform PlayMode
param(
    [ValidateSet('EditMode', 'PlayMode')]
    [string]$Platform = 'EditMode'
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$version = (Select-String -Path "$projectPath\ProjectSettings\ProjectVersion.txt" -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
$results = "$projectPath\TestResults-$Platform.xml"
$log = "$projectPath\Logs\tests-$Platform.log"
if (Test-Path $results) { Remove-Item $results } # stale results would hide a compile failure

$p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList @(
    '-batchmode', '-projectPath', "`"$projectPath`"",
    '-runTests', '-testPlatform', $Platform, '-assemblyNames', "Game.Tests.$Platform",
    '-testResults', "`"$results`"", '-logFile', "`"$log`"")

if (-not (Test-Path $results)) {
    Write-Host "No test results (exit $($p.ExitCode)). Compile errors?"
    Select-String -Path $log -Pattern 'error CS' | Select-Object -First 20 | ForEach-Object { $_.Line }
    exit 1
}

[xml]$xml = Get-Content $results
$run = $xml.'test-run'
Write-Host "$Platform : $($run.result)  passed=$($run.passed) failed=$($run.failed) total=$($run.total)"
$xml.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
    Write-Host "FAILED $($_.fullname)"
    Write-Host "  $($_.failure.message.InnerText.Trim())"
}
exit $p.ExitCode
