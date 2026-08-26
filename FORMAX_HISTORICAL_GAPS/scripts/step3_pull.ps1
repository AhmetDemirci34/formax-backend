. "$PSScriptRoot\af_common.ps1"

$targets = @()
foreach ($s in 2017..2023) { $targets += @{ id = 2;   season = $s } }
foreach ($s in 2017..2023) { $targets += @{ id = 3;   season = $s } }
foreach ($s in 2021..2023) { $targets += @{ id = 848; season = $s } }

foreach ($t in $targets) {
    $r = Invoke-AF -Path 'fixtures' -Query @{ league = $t.id; season = $t.season } -SaveAs ("fixtures_{0}_{1}.json" -f $t.id, $t.season)
    $rounds = ($r.response | Select-Object -ExpandProperty league | Select-Object -ExpandProperty round | Sort-Object -Unique) -join ' | '
    Write-Host ("league={0} season={1} fixtures={2}" -f $t.id, $t.season, $r.results)
    Write-Host ("    rounds: {0}" -f $rounds)
}
