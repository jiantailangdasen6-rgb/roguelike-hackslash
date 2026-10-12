# Balance check: one headless solo bot run per class (muted, no window, all at once). Prints each run's FINAL line:
# room reached, team level, kills, gear by rarity (loot=common/fine/rare/legendary) and legendary procs.
# Fresh profiles each time (empty stash, so empty slots get loaners).
#   powershell -File Tools/BalanceRun.ps1 [-Seconds 300] [-Difficulty normal] [-Extra "-lootrate 1"]
param([int]$Seconds = 300, [string]$Difficulty = 'normal', [string]$Extra = '')
$projectPath = Split-Path -Parent $PSScriptRoot
$exe = "$projectPath\Builds\Windows\LabyrinthSurvivor.exe"
$out = "$projectPath\Logs\balance"
New-Item -ItemType Directory -Force $out | Out-Null
Get-ChildItem $out -File -ErrorAction SilentlyContinue | Remove-Item -ErrorAction SilentlyContinue

$classes = 'warrior', 'mage', 'ranger', 'guardian', 'assassin', 'alchemist'
$stamp = Get-Date -Format 'HHmmss'
$procs = @()
foreach ($c in $classes) {
    $args = @('-batchmode', '-nographics', '-autosolo', '-botmove', '-unlockall', '-class', $c, '-difficulty', $Difficulty,
        '-profile', "bal$c$stamp", '-autoquit', "$Seconds", '-logFile', "`"$out\$c.log`"") + @($Extra -split ' ' | Where-Object { $_ })
    $procs += Start-Process $exe -PassThru -ArgumentList $args
}
foreach ($p in $procs) { if (-not $p.WaitForExit(($Seconds + 90) * 1000)) { $p.Kill() } }

foreach ($c in $classes) {
    $line = Select-String -Path "$out\$c.log" -Pattern '\[AutoTest\] FINAL' | Select-Object -Last 1
    $text = if ($line) { $line.Line -replace '^.*FINAL ', '' } else { '(no FINAL line)' }
    $m = [regex]::Match($text, 'phase=(\S+) room=(\d+) level=(\d+).*kills=(\d+).*loot=(\S+) procs=(\d+)')
    if ($m.Success) { Write-Host ("{0,-9} phase={1} room={2} level={3} kills={4} loot={5} procs={6}" -f $c, $m.Groups[1], $m.Groups[2], $m.Groups[3], $m.Groups[4], $m.Groups[5], $m.Groups[6]) }
    else { Write-Host "$c $text" }
}
