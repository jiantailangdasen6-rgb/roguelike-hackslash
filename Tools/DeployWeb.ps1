# Publishes Builds/WebGL to GitHub Pages: public repo jiantailangdasen6-rgb/roguelike-web, branch gh-pages
# (build output only - the source repo stays private/local). Creates the repo and enables Pages on first run.
#   powershell -File Tools/DeployWeb.ps1
$ErrorActionPreference = 'Stop'
$repo = 'jiantailangdasen6-rgb/roguelike-web'
$url = 'https://jiantailangdasen6-rgb.github.io/roguelike-web/'
$project = Split-Path -Parent $PSScriptRoot
$build = Join-Path $project 'Builds\WebGL'
if (-not (Test-Path "$build\index.html")) { throw "No WebGL build in $build (run Tools/BuildWeb.ps1)" }

gh repo view $repo *> $null
if ($LASTEXITCODE -ne 0) {
    gh repo create $repo --public --description 'Labyrinth of the Dead (working title) - browser build only'
    if ($LASTEXITCODE -ne 0) { throw 'repo create failed' }
    $created = $true
}

$work = Join-Path $env:TEMP 'roguelike-web-deploy'
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory $work | Out-Null
Push-Location $work
try {
    git init -q -b gh-pages
    Copy-Item "$build\*" $work -Recurse
    New-Item -ItemType File '.nojekyll' | Out-Null
    $rev = git -C $project rev-parse --short HEAD
    git add -A
    git -c user.name='jiantailangdasen6-rgb' -c user.email='jiantailangdasen6@gmail.com' commit -q -m "Deploy WebGL build from $rev`n`nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
    git remote add origin "https://github.com/$repo.git"
    git push -q -f origin gh-pages
    if ($LASTEXITCODE -ne 0) { throw 'push failed' }
} finally {
    Pop-Location
}

gh api "repos/$repo/pages" *> $null
if ($LASTEXITCODE -ne 0) {
    gh api -X POST "repos/$repo/pages" -f 'source[branch]=gh-pages' -f 'source[path]=/' | Out-Null
}
Write-Host "Deployed: $url"
