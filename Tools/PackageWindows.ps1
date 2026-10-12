# Zips the release Windows build (PlayerBuild.BuildWindowsRelease) for friends: Builds/LabyrinthSurvivor-<stamp>.zip
# Leaves out the folders Unity says not to ship and adds a Japanese readme.
#   powershell -File Tools/PackageWindows.ps1
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$src = "$projectPath\Builds\Release\LabyrinthSurvivor"
if (-not (Test-Path "$src\LabyrinthSurvivor.exe")) { Write-Host "No release build: run PlayerBuild.BuildWindowsRelease first"; exit 1 }

$stamp = (Get-Content "$projectPath\Assets\_Project\Resources\BuildStamp.txt" -Raw).Trim()
$stage = "$projectPath\Builds\Release\stage\LabyrinthSurvivor"
if (Test-Path "$projectPath\Builds\Release\stage") { Remove-Item -Recurse -Force "$projectPath\Builds\Release\stage" }
New-Item -ItemType Directory -Force $stage | Out-Null
Get-ChildItem $src | Where-Object { $_.Name -notmatch 'DoNotShip|ButDontShipItWithYourGame' } |
    ForEach-Object { Copy-Item $_.FullName -Destination $stage -Recurse }

# The readme is UTF-8 Japanese: kept in Tools/ReleaseReadme.txt so this script stays ASCII.
Copy-Item "$projectPath\Tools\ReleaseReadme.txt" "$stage\README.txt"
[IO.File]::AppendAllText("$stage\README.txt", "`r`nver $stamp`r`n", (New-Object Text.UTF8Encoding($false)))

$zip = "$projectPath\Builds\LabyrinthSurvivor-$stamp.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $stage -DestinationPath $zip -CompressionLevel Optimal
$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Packaged: $zip ($mb MB)"
