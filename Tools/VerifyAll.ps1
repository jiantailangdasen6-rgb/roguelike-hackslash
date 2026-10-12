# Full pipeline: regenerate scenes/prefabs, run EditMode + PlayMode tests, build the Windows player,
# then run the 3-instance network test. Stops at the first failing step.
#   powershell -File Tools/VerifyAll.ps1 [-NetSeconds 150] [-SkipOnline]
# The online step joins through Unity Relay (needs the internet) with a 4 s host stall, which used to
# overflow the transport queues and freeze co-op runs.
param([int]$NetSeconds = 150, [switch]$SkipOnline)

$tools = $PSScriptRoot
function Step($name, [scriptblock]$body) {
    Write-Host "##### $name"
    & $body
    if ($LASTEXITCODE -ne 0) { Write-Host "##### FAILED: $name"; exit 1 }
}

Step 'Build prototype' { powershell -NoProfile -ExecutionPolicy Bypass -File "$tools\RunMethod.ps1" -Method Game.EditorTools.PrototypeBuilder.Build }
Step 'EditMode tests' { powershell -NoProfile -ExecutionPolicy Bypass -File "$tools\RunTests.ps1" -Platform EditMode }
Step 'PlayMode tests' { powershell -NoProfile -ExecutionPolicy Bypass -File "$tools\RunTests.ps1" -Platform PlayMode }
Step 'Windows player' { powershell -NoProfile -ExecutionPolicy Bypass -File "$tools\RunMethod.ps1" -Method Game.EditorTools.PlayerBuild.BuildWindows | Select-String 'PlayerBuild|error CS|exit:' }
Step 'Network test' { powershell -NoProfile -ExecutionPolicy Bypass -File "$tools\NetTest.ps1" -Seconds $NetSeconds }
if (-not $SkipOnline) { Step "Online test (Relay, host stall)" { powershell -NoProfile -ExecutionPolicy Bypass -File "$tools\OnlineTest.ps1" -Seconds 120 -HostExtra "-freezeat 50 -freezefor 4" } }
Write-Host "##### ALL OK"
