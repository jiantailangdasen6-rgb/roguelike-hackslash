# Regenerates the game, runs the tests, and builds the browser (WebGL) player into Builds/WebGL.
#   powershell -File Tools/BuildWeb.ps1 [-SkipTests]
param([switch]$SkipTests)
$tools = $PSScriptRoot
function Step($name, [scriptblock]$body) {
    Write-Host "##### $name"
    & $body
    if ($LASTEXITCODE -ne 0) { Write-Host "##### FAILED: $name"; exit 1 }
}
Step 'Fonts' { powershell -NoProfile -ExecutionPolicy Bypass -File "$tools\SubsetFonts.ps1" | Select-String 'characters|KB' }
Step 'Build game' { powershell -NoProfile -ExecutionPolicy Bypass -File "$tools\RunMethod.ps1" -Method Game.EditorTools.PrototypeBuilder.Build }
if (-not $SkipTests) {
    Step 'EditMode tests' { powershell -NoProfile -ExecutionPolicy Bypass -File "$tools\RunTests.ps1" -Platform EditMode }
    Step 'PlayMode tests' { powershell -NoProfile -ExecutionPolicy Bypass -File "$tools\RunTests.ps1" -Platform PlayMode }
}
Step 'WebGL player' { powershell -NoProfile -ExecutionPolicy Bypass -File "$tools\RunMethod.ps1" -Method Game.EditorTools.PlayerBuild.BuildWebGL -Tag '\[PlayerBuild\]' }
Write-Host "##### WEB BUILD OK"
