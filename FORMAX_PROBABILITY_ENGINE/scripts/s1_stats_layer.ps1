# Pre-match feature layer - STEP 1
# Extract the match statistics that already exist in the football-data source into a SEPARATE
# layer keyed by the canonical MatchId. Master schema is not touched. Odds go to their own file.
$ErrorActionPreference = 'Stop'
$CI    = [Globalization.CultureInfo]::InvariantCulture
$Root  = 'C:\Users\dikim\Desktop\FORMAX_Backend'
$Hist  = Join-Path $Root 'FORMAX_HISTORICAL_MASTER'
$Stats = Join-Path $Root 'FORMAX_HISTORICAL_MATCH_STATS'
$Prob  = Join-Path $Root 'FORMAX_PROBABILITY_ENGINE'
New-Item -ItemType Directory -Force -Path $Stats | Out-Null
$TR = 'S' + [char]0x00FC + 'per Lig'

# ---------- canonical team lookup (current registry, post identity resolution) ----------
$noise = @('fc','cf','sc','ac','fk','sk','ks','kf','nk','bk','sv','tsv','pfc','cs','ss','ue','us','as','ca','if','ik','ff','club','de','the','afc','ssc','ogc','rc','sco','hsc','fco')
function Norm-Key { param([string]$s)
    if ([string]::IsNullOrWhiteSpace($s)) { return '' }
    $s = [regex]::Replace($s, '\s*\([A-Za-z]{3}\)\s*', ' ')
    $d = $s.Normalize([Text.NormalizationForm]::FormD).ToCharArray() |
         Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne [Globalization.UnicodeCategory]::NonSpacingMark }
    $s = [regex]::Replace(((-join $d).ToLowerInvariant()), '[^a-z0-9]', ' ')
    (@($s -split '\s+' | Where-Object { $_ -and $_.Length -ge 2 -and ($noise -notcontains $_) }) -join ' ')
}
$aliasToId = @{}
foreach ($t in (Import-Csv (Join-Path $Hist 'FORMAX_HISTORICAL_TEAMS.csv'))) {
    foreach ($a in (($t.Aliases -split ' \| ') + $t.CanonicalTeamName)) {
        $k = Norm-Key $a
        if ($k -and -not $aliasToId.ContainsKey($k)) { $aliasToId[$k] = $t.CanonicalTeamId }
    }
}
Write-Host "alias keys: $($aliasToId.Count)"

# ---------- master index ----------
$master = Import-Csv (Join-Path $Hist 'FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv')
$byKey = @{}; $byPair = @{}
foreach ($m in $master) {
    $byKey["$($m.Competition)|$($m.Date)|$($m.HomeTeamId)|$($m.AwayTeamId)"] = $m
    $pk = "$($m.Competition)|$($m.Season)|$($m.HomeTeamId)|$($m.AwayTeamId)"
    if (-not $byPair.ContainsKey($pk)) { $byPair[$pk] = New-Object System.Collections.ArrayList }
    [void]$byPair[$pk].Add($m)
}
Write-Host "master rows indexed: $($master.Count)"

# ---------- stream the source ----------
$divMap = @{ 'E0'='Premier League'; 'E1'='Championship'; 'SP1'='La Liga'; 'I1'='Serie A'
             'D1'='Bundesliga'; 'F1'='Ligue 1'; 'T1'=$TR; 'N1'='Eredivisie' }
$fd = Join-Path $Root 'Data\Historical\Matches.csv'
$hdr = (Get-Content $fd -First 1) -split ','
$ix = @{}; for ($i=0; $i -lt $hdr.Count; $i++) { $ix[$hdr[$i]] = $i }
function V { param($c,$name) if ($ix.ContainsKey($name) -and $ix[$name] -lt $c.Count) { $c[$ix[$name]] } else { '' } }
function I { param($c,$name) $v = V $c $name; if ([string]::IsNullOrWhiteSpace($v)) { '' } else { [int][double]::Parse($v,$CI) } }
function D { param($c,$name) $v = V $c $name; if ([string]::IsNullOrWhiteSpace($v)) { '' } else { [double]::Parse($v,$CI) } }

$statRows   = New-Object System.Collections.ArrayList
$marketRows = New-Object System.Collections.ArrayList
$unmatched  = New-Object System.Collections.ArrayList
$srcTotal = 0; $matchedExact = 0; $matchedNear = 0

$sr = New-Object IO.StreamReader($fd, [Text.Encoding]::UTF8)
[void]$sr.ReadLine()
while (($line = $sr.ReadLine()) -ne $null) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $c = $line.Split(',')
    if (-not $divMap.ContainsKey($c[0])) { continue }
    if ($c[1] -lt '2017-07-01') { continue }
    $srcTotal++
    $comp = $divMap[$c[0]]; $date = $c[1]
    $hid = $aliasToId[(Norm-Key $c[3])]; $aid = $aliasToId[(Norm-Key $c[4])]
    if (-not $hid -or -not $aid) {
        [void]$unmatched.Add([PSCustomObject]@{ Reason='TEAM_NAME_NOT_IN_REGISTRY'; Division=$c[0]; Date=$date; Home=$c[3]; Away=$c[4] }); continue
    }
    $m = $byKey["$comp|$date|$hid|$aid"]
    if ($m) { $matchedExact++ }
    else {
        # the master may carry a corrected date for this fixture; an ordered pair is unique per season
        $hit = $null
        foreach ($se in @($master | Select-Object -First 0)) { }   # no-op, keeps scope tidy
        foreach ($pk in $byPair.Keys) { }                            # avoided: use season lookup below
        $m = $null
    }
    if (-not $m) {
        # season is not in the source file - find the unique master row for this ordered pair near the date
        $cands = @()
        foreach ($se in @('2017/18','2018/19','2019/20','2020/21','2021/22','2022/23','2023/24','2024/25','2025/26')) {
            $pk = "$comp|$se|$hid|$aid"
            if ($byPair.ContainsKey($pk)) { $cands += $byPair[$pk] }
        }
        $d0 = [datetime]::ParseExact($date,'yyyy-MM-dd',$CI)
        $near = @($cands | Where-Object { [Math]::Abs(([datetime]::ParseExact($_.Date,'yyyy-MM-dd',$CI) - $d0).TotalDays) -le 7 })
        if ($near.Count -eq 1) { $m = $near[0]; $matchedNear++ }
    }
    if (-not $m) {
        [void]$unmatched.Add([PSCustomObject]@{ Reason='NO_MASTER_ROW'; Division=$c[0]; Date=$date; Home=$c[3]; Away=$c[4] }); continue
    }

    $hasStats = ((V $c 'HomeShots') -ne '')
    if ($hasStats) {
        [void]$statRows.Add([PSCustomObject][ordered]@{
            MatchId=$m.MatchId; Season=$m.Season; Competition=$m.Competition; CompetitionType=$m.CompetitionType
            Date=$m.Date; HomeTeam=$m.HomeTeam; AwayTeam=$m.AwayTeam; HomeTeamId=$m.HomeTeamId; AwayTeamId=$m.AwayTeamId
            HomeGoals=$m.HomeGoals; AwayGoals=$m.AwayGoals
            HomeShots=(I $c 'HomeShots'); AwayShots=(I $c 'AwayShots')
            HomeShotsOnTarget=(I $c 'HomeTarget'); AwayShotsOnTarget=(I $c 'AwayTarget')
            HomeCorners=(I $c 'HomeCorners'); AwayCorners=(I $c 'AwayCorners')
            HomeFouls=(I $c 'HomeFouls'); AwayFouls=(I $c 'AwayFouls')
            HomeYellow=(I $c 'HomeYellow'); AwayYellow=(I $c 'AwayYellow')
            HomeRed=(I $c 'HomeRed'); AwayRed=(I $c 'AwayRed')
            StatsSource='football-data.co.uk'; SourceFile='Data/Historical/Matches.csv'
            SourceDivision=$c[0]; SourceMatchDate=$date })
    }
    $hasOdds = ((V $c 'OddHome') -ne '')
    if ($hasOdds) {
        [void]$marketRows.Add([PSCustomObject][ordered]@{
            MatchId=$m.MatchId; Season=$m.Season; Competition=$m.Competition; CompetitionType=$m.CompetitionType
            Date=$m.Date; HomeTeam=$m.HomeTeam; AwayTeam=$m.AwayTeam
            MarketType='1X2'; OddHome=(D $c 'OddHome'); OddDraw=(D $c 'OddDraw'); OddAway=(D $c 'OddAway')
            MaxHome=(D $c 'MaxHome'); MaxDraw=(D $c 'MaxDraw'); MaxAway=(D $c 'MaxAway')
            Over25=(D $c 'Over25'); Under25=(D $c 'Under25'); MaxOver25=(D $c 'MaxOver25'); MaxUnder25=(D $c 'MaxUnder25')
            HandicapLine=(D $c 'HandiSize'); HandicapHome=(D $c 'HandiHome'); HandicapAway=(D $c 'HandiAway')
            OddsSource='football-data.co.uk'; SourceFile='Data/Historical/Matches.csv'
            CaptureTimestamp=''; LeakageSafe='UNKNOWN'; Status='FUTURE_BENCHMARK' })
    }
}
$sr.Close()

Write-Host ""
Write-Host ("source rows in FORMAX scope : {0}" -f $srcTotal)
Write-Host ("matched on exact key        : {0}" -f $matchedExact)
Write-Host ("matched on unique pair+-7d  : {0}" -f $matchedNear)
Write-Host ("unmatched                   : {0}" -f $unmatched.Count)
$unmatched | Group-Object Reason | ForEach-Object { Write-Host ("    {0,-28} {1}" -f $_.Name, $_.Count) }
Write-Host ("stat rows written           : {0}" -f $statRows.Count)
Write-Host ("market rows written         : {0}" -f $marketRows.Count)

$statRows   | Export-Csv (Join-Path $Stats 'FORMAX_HISTORICAL_MATCH_STATS.csv')  -NoTypeInformation -Encoding UTF8
$marketRows | Export-Csv (Join-Path $Stats 'FORMAX_HISTORICAL_MARKET_DATA.csv')  -NoTypeInformation -Encoding UTF8
$unmatched  | Export-Csv (Join-Path $Stats 'FORMAX_HISTORICAL_MATCH_STATS_UNMATCHED.csv') -NoTypeInformation -Encoding UTF8

# ---------- coverage report ----------
$statIds = @{}; foreach ($s in $statRows) { $statIds[$s.MatchId] = 1 }
$mktIds  = @{}; foreach ($s in $marketRows) { $mktIds[$s.MatchId] = 1 }
$cov = New-Object System.Collections.ArrayList
function Add-Cov { param($Scope,$Group,$Field,$N,$D)
    [void]$cov.Add([PSCustomObject]@{ Scope=$Scope; Group=$Group; Field=$Field; Rows=$N; MasterRows=$D
                                      CoveragePct=$(if ($D -eq 0) { 0 } else { [Math]::Round(100.0*$N/$D,2) }) })
}
Add-Cov 'OVERALL' 'all model-eligible' 'match_statistics' $statIds.Count $master.Count
Add-Cov 'OVERALL' 'all model-eligible' 'market_odds'      $mktIds.Count  $master.Count
foreach ($g in ($master | Group-Object CompetitionType)) {
    $n = @($g.Group | Where-Object { $statIds.ContainsKey($_.MatchId) }).Count
    $o = @($g.Group | Where-Object { $mktIds.ContainsKey($_.MatchId) }).Count
    Add-Cov 'COMPETITION_TYPE' $g.Name 'match_statistics' $n $g.Count
    Add-Cov 'COMPETITION_TYPE' $g.Name 'market_odds'      $o $g.Count
}
foreach ($g in ($master | Where-Object { $_.CompetitionType -eq 'DOMESTIC_LEAGUE' } | Group-Object Competition)) {
    $n = @($g.Group | Where-Object { $statIds.ContainsKey($_.MatchId) }).Count
    Add-Cov 'COMPETITION' $g.Name 'match_statistics' $n $g.Count
}
foreach ($g in ($master | Group-Object Season | Sort-Object Name)) {
    $n = @($g.Group | Where-Object { $statIds.ContainsKey($_.MatchId) }).Count
    $o = @($g.Group | Where-Object { $mktIds.ContainsKey($_.MatchId) }).Count
    Add-Cov 'SEASON' $g.Name 'match_statistics' $n $g.Count
    Add-Cov 'SEASON' $g.Name 'market_odds'      $o $g.Count
}
# per-field completeness inside the stats layer
foreach ($f in 'HomeShots','HomeShotsOnTarget','HomeCorners','HomeFouls','HomeYellow','HomeRed') {
    $n = @($statRows | Where-Object { $_.$f -ne '' }).Count
    Add-Cov 'FIELD_WITHIN_STATS_LAYER' $f 'non_null' $n $statRows.Count
}
$cov | Export-Csv (Join-Path $Prob 'historical_match_stats_coverage.csv') -NoTypeInformation -Encoding UTF8
Write-Host ""
$cov | Where-Object { $_.Scope -in @('OVERALL','COMPETITION_TYPE') } | Format-Table Scope,Group,Field,Rows,MasterRows,CoveragePct -AutoSize
Write-Host "--- by season"
$cov | Where-Object { $_.Scope -eq 'SEASON' -and $_.Field -eq 'match_statistics' } | Format-Table Group,Rows,MasterRows,CoveragePct -AutoSize
Write-Host "DONE_S1"
