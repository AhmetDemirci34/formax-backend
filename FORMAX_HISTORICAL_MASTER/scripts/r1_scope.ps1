$ErrorActionPreference = 'Stop'
$O    = 'C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'
$Work = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\master_work'

$look   = Import-Csv (Join-Path $O 'FORMAX_HISTORICAL_TEAM_LOOKALIKES.csv')
$teams  = Import-Csv (Join-Path $O 'FORMAX_HISTORICAL_TEAMS.csv')
$master = Import-Csv (Join-Path $O 'FORMAX_HISTORICAL_MASTER.csv')

Write-Host ("lookalike pairs total: {0}" -f $look.Count)
$look | Group-Object Status | Format-Table Count,Name -AutoSize

$byId = @{}; foreach ($t in $teams) { $byId[$t.CanonicalTeamId] = $t }

# where does each identity appear, and does it already carry a provider id?
$ctx = @{}
foreach ($r in $master) {
    foreach ($side in @(@($r.HomeTeamId,$r.ProviderHomeTeamId), @($r.AwayTeamId,$r.ProviderAwayTeamId))) {
        $id = $side[0]; $prov = $side[1]
        if (-not $ctx.ContainsKey($id)) {
            $ctx[$id] = [PSCustomObject]@{ Provider=New-Object 'System.Collections.Generic.HashSet[string]'
                                           CompSeason=New-Object 'System.Collections.Generic.HashSet[string]'
                                           Matches=0 }
        }
        if ("$prov" -ne '') { [void]$ctx[$id].Provider.Add("$prov") }
        [void]$ctx[$id].CompSeason.Add("$($r.Competition)|$($r.Season)")
        $ctx[$id].Matches++
    }
}

$rows = New-Object System.Collections.ArrayList
$i = 0
foreach ($p in ($look | Where-Object { $_.Status -like 'NEEDS*' })) {
    $i++
    $a = $ctx[$p.IdA]; $b = $ctx[$p.IdB]
    [void]$rows.Add([PSCustomObject]@{
        No=$i; IdA=$p.IdA; NameA=$p.NameA; IdB=$p.IdB; NameB=$p.NameB
        ProvA=(($a.Provider | Sort-Object) -join ','); ProvB=(($b.Provider | Sort-Object) -join ',')
        MatchesA=$a.Matches; MatchesB=$b.Matches
        CompSeasonA=(($a.CompSeason | Sort-Object) -join ' ; ')
        CompSeasonB=(($b.CompSeason | Sort-Object) -join ' ; ')
        AliasesA=$byId[$p.IdA].Aliases; AliasesB=$byId[$p.IdB].Aliases })
}
$rows | Export-Csv (Join-Path $Work 'lookalike_context.csv') -NoTypeInformation -Encoding UTF8
Write-Host ("pairs needing review: {0}" -f $rows.Count)
Write-Host ("  both sides already have a provider id : {0}" -f @($rows | Where-Object { $_.ProvA -and $_.ProvB }).Count)
Write-Host ("  only one side has a provider id       : {0}" -f @($rows | Where-Object { ($_.ProvA -and -not $_.ProvB) -or (-not $_.ProvA -and $_.ProvB) }).Count)
Write-Host ("  neither side has a provider id        : {0}" -f @($rows | Where-Object { -not $_.ProvA -and -not $_.ProvB }).Count)
Write-Host ""
$rows | Format-Table No,NameA,NameB,ProvA,ProvB,MatchesA,MatchesB -AutoSize

Write-Host ""
Write-Host "--- competition-seasons that need a provider team list (sides without a provider id):"
$need = @{}
foreach ($r in $rows) {
    if (-not $r.ProvA) { foreach ($cs in ($r.CompSeasonA -split ' ; ')) { $need[$cs] = 1 } }
    if (-not $r.ProvB) { foreach ($cs in ($r.CompSeasonB -split ' ; ')) { $need[$cs] = 1 } }
}
$need.Keys | Sort-Object | ForEach-Object { Write-Host "   $_" }
Write-Host ("   distinct competition-seasons: {0}" -f $need.Count)
