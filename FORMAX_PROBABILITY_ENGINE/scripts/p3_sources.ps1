# Probability Engine prep - STEP 3: does ANY owned source carry match statistics, odds, lineups
# or pre-match context? Measured, not assumed. Sources are read only.
$ErrorActionPreference = 'Stop'
$CI    = [Globalization.CultureInfo]::InvariantCulture
$Root  = 'C:\Users\dikim\Desktop\FORMAX_Backend'
$Hist  = Join-Path $Root 'FORMAX_HISTORICAL_MASTER'
$Gaps  = Join-Path $Root 'FORMAX_HISTORICAL_GAPS'
$Work  = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\prob_work'

Write-Host "=== A) the model dataset itself"
$cols = (Import-Csv (Join-Path $Hist 'FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv') | Select-Object -First 1).PSObject.Properties.Name
foreach ($probe in 'xG','xGA','Shots','ShotsOnTarget','Possession','Corners','Fouls','Yellow','Red','Lineup','Injur','Suspen','Odd','Market','Weather','Referee','Manager','Attendance','Venue') {
    $hit = @($cols | Where-Object { $_ -like "*$probe*" })
    Write-Host ("   {0,-16} -> {1}" -f $probe, $(if ($hit.Count) { $hit -join ',' } else { 'NOT PRESENT (0 columns)' }))
}

Write-Host ""
Write-Host "=== B) football-data.co.uk source (Data/Historical/Matches.csv) - FORMAX scope only"
# header index map
$fd = Join-Path $Root 'Data\Historical\Matches.csv'
$hdr = (Get-Content $fd -First 1) -split ','
$idx = @{}; for ($i=0; $i -lt $hdr.Count; $i++) { $idx[$hdr[$i]] = $i }
$divs = @('E0','E1','SP1','I1','D1','F1','T1','N1')
$probe = 'HomeElo','AwayElo','Form5Home','Form5Away','HomeShots','AwayShots','HomeTarget','AwayTarget',
         'HomeFouls','AwayFouls','HomeCorners','AwayCorners','HomeYellow','AwayYellow','HomeRed','AwayRed',
         'OddHome','OddDraw','OddAway','MaxHome','Over25','Under25','HandiSize'
$have = @{}; foreach ($p in $probe) { $have[$p] = 0 }
$n = 0
$sr = New-Object IO.StreamReader($fd, [Text.Encoding]::UTF8)
[void]$sr.ReadLine()
while (($line = $sr.ReadLine()) -ne $null) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $c = $line.Split(',')
    if ($divs -notcontains $c[0]) { continue }
    if ($c[1] -lt '2017-07-01') { continue }
    $n++
    foreach ($p in $probe) { if ($idx.ContainsKey($p) -and $idx[$p] -lt $c.Count -and $c[$idx[$p]] -ne '') { $have[$p]++ } }
}
$sr.Close()
Write-Host ("   rows in FORMAX scope: {0}" -f $n)
$srcRows = New-Object System.Collections.ArrayList
foreach ($p in $probe) {
    $pct = [Math]::Round(100.0*$have[$p]/$n,2)
    Write-Host ("   {0,-14} {1,7} / {2}  = {3,6}%" -f $p, $have[$p], $n, $pct)
    [void]$srcRows.Add([PSCustomObject]@{ Source='football-data.co.uk (Data/Historical/Matches.csv)'; Field=$p; Rows=$have[$p]; ScopeRows=$n; CoveragePct=$pct })
}
$srcRows | Export-Csv (Join-Path $Work 'source_field_coverage.csv') -NoTypeInformation -Encoding UTF8

Write-Host ""
Write-Host "=== C) api-football raw responses already on disk - which blocks do they contain?"
foreach ($dir in @((Join-Path $Gaps 'raw'), (Join-Path $Hist 'api_raw'))) {
    if (-not (Test-Path $dir)) { continue }
    $f = Get-ChildItem $dir -Filter 'fixtures_*.json','fx_*.json' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $f) { $f = Get-ChildItem $dir -Filter '*.json' | Where-Object { $_.Name -like '*f*' } | Select-Object -First 1 }
    if (-not $f) { continue }
    $j = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $one = $j.response | Select-Object -First 1
    if (-not $one) { continue }
    Write-Host ("   {0}\{1}" -f (Split-Path $dir -Leaf), $f.Name)
    Write-Host ("      top-level blocks : {0}" -f (($one.PSObject.Properties.Name) -join ', '))
    foreach ($b in 'statistics','events','lineups','players','odds') {
        $present = $one.PSObject.Properties.Name -contains $b
        Write-Host ("      {0,-12} : {1}" -f $b, $(if ($present) { 'present' } else { 'NOT REQUESTED / NOT PRESENT' }))
    }
}

Write-Host ""
Write-Host "=== D) does the FORMAX production database carry any of it? (schema check only, read-only)"
$sql = Get-ChildItem $Root -Filter '*.sql' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
Write-Host ("   migration file found: {0}" -f $(if ($sql) { $sql.Name } else { 'none' }))
foreach ($t in 'MatchMarketOdds','TeamPlayerIntelligence','MatchNewsArticles','MatchEvidenceRecords','LeagueStandings','MatchStatistics','Lineup') {
    $hits = @(Select-String -Path (Join-Path $Root 'migration_20M.sql') -Pattern $t -SimpleMatch -ErrorAction SilentlyContinue | Select-Object -First 1)
    Write-Host ("   table '{0,-24}' referenced in migration: {1}" -f $t, $(if ($hits.Count) { 'yes' } else { 'no' }))
}
