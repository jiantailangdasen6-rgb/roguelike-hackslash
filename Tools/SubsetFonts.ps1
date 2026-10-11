# Builds the bundled Japanese fonts from the full fonts in Tools/FontSubset/cache (not in git),
# keeping only the characters used in the game's source code. Browser builds can't use OS fonts,
# so ANY new Japanese text needs this to be re-run (missing glyphs are simply not drawn).
#   powershell -File Tools/SubsetFonts.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$cache = "$root\Tools\FontSubset\cache"
$out = "$root\Assets\_Project\Resources\Fonts"
New-Item -ItemType Directory -Force $out | Out-Null

# Every character in the gameplay/UI code (strings and comments alike - a few spare glyphs are cheap).
$sb = New-Object System.Text.StringBuilder
Get-ChildItem "$root\Assets\_Project\Scripts\Core", "$root\Assets\_Project\Scripts\Gameplay" -Recurse -Filter *.cs |
    ForEach-Object { [void]$sb.Append([System.IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::UTF8)) }
for ($c = 0x20; $c -le 0x7E; $c++) { [void]$sb.Append([char]$c) }
[void]$sb.Append([System.IO.File]::ReadAllText("$root\Tools\FontSubset\extra-chars.txt", [System.Text.Encoding]::UTF8))

$chars = ($sb.ToString().ToCharArray() | Where-Object { [int]$_ -ge 0x20 } | Sort-Object -Unique -CaseSensitive) -join ''
$list = "$root\Tools\FontSubset\chars.txt"
[System.IO.File]::WriteAllText($list, $chars, (New-Object System.Text.UTF8Encoding $false))
Write-Host "characters: $($chars.Length)"

dotnet run --project "$root\Tools\FontSubset" -c Release -- "$cache\NotoSerifJP-Bold-full.ttf" $list "$out\Body.ttf"
dotnet run --project "$root\Tools\FontSubset" -c Release -- "$cache\KaiseiTokumin-ExtraBold-full.ttf" $list "$out\Title.ttf"
Get-ChildItem $out -Filter *.ttf | ForEach-Object { "{0}  {1:N0} KB" -f $_.Name, ($_.Length / 1KB) }
