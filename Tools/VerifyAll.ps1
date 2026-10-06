# Full pipeline: regenerate scenes/prefabs, run EditMode + PlayMode tests, build the Windows player,
# then run the 3-instance network test. Stops at the first failing step.
#   powershell -File Tools/VerifyAll.ps1 [-NetSeconds 150]
param([int]$NetSeconds = 150)

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
Write-Host "##### ALL OK"
