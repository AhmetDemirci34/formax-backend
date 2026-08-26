$ErrorActionPreference='Stop'
$Root='C:\Users\dikim\Desktop\FORMAX_Backend'
$Hist=Join-Path $Root 'FORMAX_HISTORICAL_MASTER'
$Gaps=Join-Path $Root 'FORMAX_HISTORICAL_GAPS'

Write-Host "=== C) api-football raw responses on disk - which data blocks were ever requested?"
foreach ($dir in @((Join-Path $Gaps 'raw'), (Join-Path $Hist 'api_raw'))) {
    if (-not (Test-Path $dir)) { continue }
    $f = Get-ChildItem $dir -Filter '*.json' | Where-Object { $_.Name -like 'f*' } | Select-Object -First 1
    if (-not $f) { continue }
    $j = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $one = $j.response | Select-Object -First 1
    if (-not $one) { continue }
    Write-Host ("   {0}\{1}" -f (Split-Path $dir -Leaf), $f.Name)
    Write-Host ("      blocks present : {0}" -f (($one.PSObject.Properties.Name) -join ', '))
    foreach ($b in 'statistics','events','lineups','players','odds') {
        Write-Host ("      {0,-12} : {1}" -f $b, $(if ($one.PSObject.Properties.Name -contains $b) { 'present' } else { 'NOT REQUESTED - endpoint never called' }))
    }
}

Write-Host ""
Write-Host "=== D) production DB schema (read-only grep of migration_20M.sql)"
$mig = Join-Path $Root 'migration_20M.sql'
if (Test-Path $mig) {
    foreach ($t in 'MatchMarketOdds','TeamPlayerIntelligence','MatchNewsArticles','MatchEvidenceRecords','LeagueStandings','Lineups','MatchStatistics','Injur') {
        $hit = @(Select-String -Path $mig -Pattern $t -SimpleMatch -ErrorAction SilentlyContinue)
        Write-Host ("   '{0,-24}' in migration: {1}" -f $t, $(if ($hit.Count) { "yes ($($hit.Count) hits)" } else { 'no' }))
    }
} else { Write-Host '   migration_20M.sql not found' }

Write-Host ""
Write-Host "=== E) how much of the MODEL dataset could receive football-data statistics?"
$m = Import-Csv (Join-Path $Hist 'FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv')
$tot = $m.Count
$fd  = @($m | Where-Object { $_.Source -eq 'football-data' })
Write-Host ("   model rows                          : {0}" -f $tot)
Write-Host ("   rows whose primary source is football-data : {0}  ({1}%)" -f $fd.Count, [Math]::Round(100.0*$fd.Count/$tot,2))
foreach ($g in ($m | Group-Object CompetitionType)) {
    $n = @($g.Group | Where-Object { $_.Source -eq 'football-data' }).Count
    Write-Host ("      {0,-28} {1,6}/{2,-6} = {3,6}%" -f $g.Name, $n, $g.Count, [Math]::Round(100.0*$n/$g.Count,2))
}
Write-Host ""
Write-Host "   by source overall:"
$m | Group-Object Source | Sort-Object Count -Descending | ForEach-Object { Write-Host ("      {0,-20} {1,6}" -f $_.Name, $_.Count) }
Write-Host ""
Write-Host "   domestic rows per season that come from football-data (statistics reachable):"
foreach ($g in ($m | Where-Object { $_.CompetitionType -eq 'DOMESTIC_LEAGUE' } | Group-Object Season | Sort-Object Name)) {
    $n = @($g.Group | Where-Object { $_.Source -eq 'football-data' }).Count
    Write-Host ("      {0}  {1,6}/{2,-6} = {3,6}%" -f $g.Name, $n, $g.Count, [Math]::Round(100.0*$n/$g.Count,2))
}
