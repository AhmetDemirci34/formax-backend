# FORMAX Historical Master — STAGE 3: merge to one canonical row per match + quality/conflict reports.
$ErrorActionPreference = 'Stop'
$CI   = [Globalization.CultureInfo]::InvariantCulture
$Work = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\master_work'
$Out  = 'C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'
New-Item -ItemType Directory -Force -Path $Out | Out-Null

$stage   = Import-Csv (Join-Path $Work 'staging.csv')
$teamMap = @{}; Import-Csv (Join-Path $Work 'team_name_map.csv') | ForEach-Object { $teamMap[$_.RawName] = $_.CanonicalTeamId }
$teamNm  = @{}; Import-Csv (Join-Path $Work 'teams.csv')         | ForEach-Object { $teamNm[$_.CanonicalTeamId] = $_.CanonicalTeamName }
$cellPri = @{}; Import-Csv (Join-Path $Work 'cell_primary.csv')  | ForEach-Object { $cellPri[$_.Cell] = $_.PrimarySource }

$sha1 = [Security.Cryptography.SHA1]::Create()
function Match-Id { param([string]$s)
    return 'FMXM' + (([BitConverter]::ToString($sha1.ComputeHash([Text.Encoding]::UTF8.GetBytes($s))) -replace '-','').Substring(0,16))
}
function NInt { param($v) if ([string]::IsNullOrWhiteSpace($v)) { $null } else { [int]$v } }
$statusMap = @{ 'FT'='FT'; 'AET'='AET'; 'PEN'='PEN'; 'AWARDED'='AWARDED'; 'AWD'='AWARDED'; 'WO'='AWARDED'
                'CANCELLED'='CANCELLED'; 'CANC'='CANCELLED'; 'POSTPONED'='POSTPONED'; 'PST'='POSTPONED'
                'ABANDONED'='ABANDONED'; 'ABD'='ABANDONED'; 'NS'='SCHEDULED'; 'TBD'='SCHEDULED'; 'UNKNOWN'='UNKNOWN' }

# ---- attach canonical team identity -------------------------------------------------------------
$unknownTeam = New-Object System.Collections.ArrayList
foreach ($r in $stage) {
    $hid = $teamMap[$r.HomeRaw]; $aid = $teamMap[$r.AwayRaw]
    if (-not $hid -or -not $aid) {
        [void]$unknownTeam.Add([PSCustomObject]@{ Kind='TEAM_ID_MISSING'; Source=$r.SourceKey; Competition=$r.Competition; Season=$r.Season; Home=$r.HomeRaw; Away=$r.AwayRaw })
    }
    Add-Member -InputObject $r -NotePropertyName HomeTeamId -NotePropertyValue $hid -Force
    Add-Member -InputObject $r -NotePropertyName AwayTeamId -NotePropertyValue $aid -Force
    Add-Member -InputObject $r -NotePropertyName Cell -NotePropertyValue ('{0}|{1}|{2}' -f $r.Competition,$r.Season,$r.CompetitionType) -Force
}
Write-Host "rows without canonical team id: $($unknownTeam.Count)"

# ---- split primary / secondary -------------------------------------------------------------------
$primaryRows   = New-Object System.Collections.ArrayList
$secondaryRows = New-Object System.Collections.ArrayList
foreach ($r in $stage) {
    if ($r.SourceKey -eq $cellPri[$r.Cell]) { [void]$primaryRows.Add($r) } else { [void]$secondaryRows.Add($r) }
}
Write-Host "primary rows: $($primaryRows.Count)   secondary rows: $($secondaryRows.Count)"

# ---- build master rows ---------------------------------------------------------------------------
$master   = New-Object System.Collections.ArrayList
$byKey    = @{}    # exact key -> master row
$byPair   = @{}    # comp|season|home|away -> list of master rows
$dupRows  = New-Object System.Collections.ArrayList

foreach ($r in $primaryRows) {
    $key = '{0}|{1}|{2}|{3}|{4}|{5}' -f $r.Competition,$r.Season,$r.CompetitionType,$r.Date,$r.HomeTeamId,$r.AwayTeamId
    if ($byKey.ContainsKey($key)) {
        [void]$dupRows.Add([PSCustomObject]@{ Kind='PRIMARY_DUPLICATE'; Key=$key; Source=$r.SourceKey; SourceFile=$r.SourceFile
                                              Home=$r.HomeRaw; Away=$r.AwayRaw; Score="$($r.HomeGoals)-$($r.AwayGoals)" })
        continue
    }
    $st = $statusMap[$r.MatchStatus]; if (-not $st) { $st = 'UNKNOWN' }
    $m = [PSCustomObject][ordered]@{
        MatchId                  = (Match-Id $key)
        Season                   = $r.Season
        Competition              = $r.Competition
        CompetitionType          = $r.CompetitionType
        Round                    = $r.Round
        Date                     = $r.Date
        KickoffLocalTime         = $r.KickoffLocalTime
        KickoffUtc               = $r.KickoffUtc
        SourceTimeZone           = $r.SourceTimeZone
        HomeTeam                 = $teamNm[$r.HomeTeamId]
        AwayTeam                 = $teamNm[$r.AwayTeamId]
        HomeTeamId               = $r.HomeTeamId
        AwayTeamId               = $r.AwayTeamId
        HomeGoals                = (NInt $r.HomeGoals)
        AwayGoals                = (NInt $r.AwayGoals)
        RegularTimeHomeGoals     = (NInt $r.RegularTimeHomeGoals)
        RegularTimeAwayGoals     = (NInt $r.RegularTimeAwayGoals)
        HalfTimeHomeGoals        = (NInt $r.HalfTimeHomeGoals)
        HalfTimeAwayGoals        = (NInt $r.HalfTimeAwayGoals)
        ExtraTimeHomeGoals       = (NInt $r.ExtraTimeHomeGoals)
        ExtraTimeAwayGoals       = (NInt $r.ExtraTimeAwayGoals)
        PenaltyShootoutHome      = (NInt $r.PenaltyShootoutHome)
        PenaltyShootoutAway      = (NInt $r.PenaltyShootoutAway)
        MatchStatus              = $st
        ProviderMatchId          = $r.ProviderMatchId
        ProviderHomeTeamId       = $r.ProviderHomeTeamId
        ProviderAwayTeamId       = $r.ProviderAwayTeamId
        Source                   = $r.SourceKey
        SourceFile               = $r.SourceFile
        SourceCount              = 1
        SourceList               = $r.SourceKey
        DataQualityFlag          = ''
        HomeTeamRawPrimary       = $r.HomeRaw
        AwayTeamRawPrimary       = $r.AwayRaw
    }
    [void]$master.Add($m)
    $byKey[$key] = $m
    $pk = '{0}|{1}|{2}|{3}' -f $r.Competition,$r.Season,$r.HomeTeamId,$r.AwayTeamId
    if (-not $byPair.ContainsKey($pk)) { $byPair[$pk] = New-Object System.Collections.ArrayList }
    [void]$byPair[$pk].Add($m)
}
Write-Host "master rows: $($master.Count)   primary duplicates dropped: $($dupRows.Count)"

# ---- verify with secondary sources ---------------------------------------------------------------
$conflicts = New-Object System.Collections.ArrayList
$unmatched = New-Object System.Collections.ArrayList
$srcMap    = @{}   # MatchId -> hashtable of source -> sourcefile
foreach ($m in $master) { $srcMap[$m.MatchId] = [ordered]@{ $m.Source = $m.SourceFile } }
$matchedCnt = 0
$fillPrio = @{ 'api-football'=1; 'openfootball-json'=2; 'openfootball-txt'=3; 'fixturedownload'=4; 'openfootball-csv'=5; 'football-data'=6 }
$secondaryRows = @($secondaryRows | Sort-Object @{E={ $fillPrio[$_.SourceKey] }})

function Add-Flag { param($Row,[string]$Flag)
    if ($Row.DataQualityFlag -notlike "*$Flag*") {
        $Row.DataQualityFlag = (@($Row.DataQualityFlag,$Flag) | Where-Object { $_ }) -join ';'
    }
}

foreach ($r in $secondaryRows) {
    $hit = $null; $dateDiff = 0
    $key = '{0}|{1}|{2}|{3}|{4}|{5}' -f $r.Competition,$r.Season,$r.CompetitionType,$r.Date,$r.HomeTeamId,$r.AwayTeamId
    if ($byKey.ContainsKey($key)) { $hit = $byKey[$key] }
    if (-not $hit) {
        $pk = '{0}|{1}|{2}|{3}' -f $r.Competition,$r.Season,$r.HomeTeamId,$r.AwayTeamId
        if ($byPair.ContainsKey($pk)) {
            $cands = $byPair[$pk]
            $d0 = [datetime]::ParseExact($r.Date,'yyyy-MM-dd',$CI)
            $near = @($cands | Where-Object { [Math]::Abs(([datetime]::ParseExact($_.Date,'yyyy-MM-dd',$CI) - $d0).TotalDays) -le 3 })
            if ($near.Count -eq 1) { $hit = $near[0] }
            elseif ($cands.Count -eq 1) {
                # only accept a single-candidate fallback inside a sane window; a bigger gap means a
                # DIFFERENT fixture between the same two clubs (play-off leg, replay), not this one.
                $dd = [Math]::Abs(([datetime]::ParseExact($cands[0].Date,'yyyy-MM-dd',$CI) - $d0).TotalDays)
                if ($dd -le 14) { $hit = $cands[0] }
            }
        }
    }
    if (-not $hit) {
        [void]$unmatched.Add([PSCustomObject]@{ Kind='SECONDARY_UNMATCHED'; Source=$r.SourceKey; SourceFile=$r.SourceFile
                                                Competition=$r.Competition; Season=$r.Season; CompetitionType=$r.CompetitionType
                                                Date=$r.Date; Home=$r.HomeRaw; Away=$r.AwayRaw
                                                Score="$($r.HomeGoals)-$($r.AwayGoals)"; Status=$r.MatchStatus })
        continue
    }
    $matchedCnt++
    $dateDiff = [Math]::Abs(([datetime]::ParseExact($hit.Date,'yyyy-MM-dd',$CI) - [datetime]::ParseExact($r.Date,'yyyy-MM-dd',$CI)).TotalDays)
    if (-not $srcMap[$hit.MatchId].Contains($r.SourceKey)) { $srcMap[$hit.MatchId][$r.SourceKey] = $r.SourceFile }

    $sh = NInt $r.HomeGoals; $sa = NInt $r.AwayGoals

    # fill gaps the primary source does not carry (real values from a real source, never invented)
    if ([string]::IsNullOrWhiteSpace($hit.Round) -and -not [string]::IsNullOrWhiteSpace($r.Round)) {
        $hit.Round = $r.Round
        Add-Flag $hit 'ROUND_FILLED_FROM_SECONDARY'
    }
    if ($null -eq $hit.HomeGoals -and $null -ne $sh) {
        $hit.HomeGoals = $sh; $hit.AwayGoals = $sa
        $hit.RegularTimeHomeGoals = (NInt $r.RegularTimeHomeGoals); $hit.RegularTimeAwayGoals = (NInt $r.RegularTimeAwayGoals)
        $hit.HalfTimeHomeGoals    = (NInt $r.HalfTimeHomeGoals);    $hit.HalfTimeAwayGoals    = (NInt $r.HalfTimeAwayGoals)
        $hit.ExtraTimeHomeGoals   = (NInt $r.ExtraTimeHomeGoals);   $hit.ExtraTimeAwayGoals   = (NInt $r.ExtraTimeAwayGoals)
        $hit.PenaltyShootoutHome  = (NInt $r.PenaltyShootoutHome);  $hit.PenaltyShootoutAway  = (NInt $r.PenaltyShootoutAway)
        $st2 = $statusMap[$r.MatchStatus]; if ($st2) { $hit.MatchStatus = $st2 }
        Add-Flag $hit 'SCORE_FILLED_FROM_SECONDARY'
    }
    if ([string]::IsNullOrWhiteSpace($hit.HalfTimeHomeGoals) -and -not [string]::IsNullOrWhiteSpace($r.HalfTimeHomeGoals)) {
        $hit.HalfTimeHomeGoals = (NInt $r.HalfTimeHomeGoals); $hit.HalfTimeAwayGoals = (NInt $r.HalfTimeAwayGoals)
        Add-Flag $hit 'HALFTIME_FILLED_FROM_SECONDARY'
    }

    if ($null -ne $sh -and $null -ne $hit.HomeGoals -and ($sh -ne $hit.HomeGoals -or $sa -ne $hit.AwayGoals)) {
        [void]$conflicts.Add([PSCustomObject]@{
            MatchKey   = ('{0}|{1}|{2}|{3}|{4}' -f $hit.Competition,$hit.Season,$hit.Date,$hit.HomeTeam,$hit.AwayTeam)
            MatchId=$hit.MatchId; Competition=$hit.Competition; CompetitionType=$hit.CompetitionType; Season=$hit.Season
            Date=$hit.Date; HomeTeam=$hit.HomeTeam; AwayTeam=$hit.AwayTeam
            SourceA=$hit.Source; ScoreA="$($hit.HomeGoals)-$($hit.AwayGoals)"; StatusA=$hit.MatchStatus
            SourceB=$r.SourceKey; ScoreB="$sh-$sa"; StatusB=$r.MatchStatus; SourceBFile=$r.SourceFile
            DateDiffDays=$dateDiff
            Resolution='UNRESOLVED - master keeps the primary-source value; no automatic pick' })
        $hit.DataQualityFlag = 'DATA_CONFLICT'
    }
    if ($dateDiff -gt 0) {
        if ($hit.DataQualityFlag -notlike '*DATE_DIFF*') {
            $hit.DataQualityFlag = (@($hit.DataQualityFlag,'DATE_DIFF_BETWEEN_SOURCES') | Where-Object { $_ }) -join ';'
        }
    }
}
# ---- promote genuinely missing matches ------------------------------------------------------------
# A secondary row that matched nothing is either (a) a real match the primary source does not carry,
# or (b) a fixture that was never played. (a) must not be lost; (b) must not enter as a result.
$promoted = New-Object System.Collections.ArrayList
$notPlayed = New-Object System.Collections.ArrayList
$stillUnmatched = New-Object System.Collections.ArrayList
$unmatchedRowsBySource = @{}
foreach ($r in $secondaryRows) {
    $k = '{0}|{1}|{2}|{3}|{4}|{5}' -f $r.Competition,$r.Season,$r.CompetitionType,$r.Date,$r.HomeTeamId,$r.AwayTeamId
    $unmatchedRowsBySource[$k] = $r
}
$secPrio = @{ 'api-football'=1; 'openfootball-json'=2; 'openfootball-txt'=3; 'fixturedownload'=4; 'openfootball-csv'=5; 'football-data'=6 }
$cands = @()
foreach ($u in $unmatched) {
    $row = $secondaryRows | Where-Object { $_.SourceKey -eq $u.Source -and $_.Date -eq $u.Date -and $_.HomeRaw -eq $u.Home -and $_.AwayRaw -eq $u.Away -and $_.Competition -eq $u.Competition -and $_.Season -eq $u.Season } | Select-Object -First 1
    if ($row) { $cands += $row }
}
$cands = @($cands | Sort-Object @{E={ $secPrio[$_.SourceKey] }})
foreach ($r in $cands) {
    if ([string]::IsNullOrWhiteSpace($r.HomeGoals)) {
        [void]$notPlayed.Add([PSCustomObject]@{ Competition=$r.Competition; Season=$r.Season; Date=$r.Date
                                                Home=$r.HomeRaw; Away=$r.AwayRaw; Status=$r.MatchStatus
                                                Source=$r.SourceKey; SourceFile=$r.SourceFile
                                                Note='fixture carries no result in this source and is absent from the primary source - NOT added to master' })
        continue
    }
    # strict duplicate guard: same competition/season/team-pair within +/-10 days already in master
    $pk = '{0}|{1}|{2}|{3}' -f $r.Competition,$r.Season,$r.HomeTeamId,$r.AwayTeamId
    $dupe = $false
    if ($byPair.ContainsKey($pk)) {
        $d0 = [datetime]::ParseExact($r.Date,'yyyy-MM-dd',$CI)
        foreach ($c in $byPair[$pk]) {
            # a double round-robin league can hold an ordered pair only once -> any existing league row
            # for this pair means this is the same fixture (the secondary just dates it differently)
            if ($r.CompetitionType -eq 'DOMESTIC_LEAGUE' -and $c.CompetitionType -eq 'DOMESTIC_LEAGUE') { $dupe = $true; break }
            if ([Math]::Abs(([datetime]::ParseExact($c.Date,'yyyy-MM-dd',$CI) - $d0).TotalDays) -le 10) { $dupe = $true; break }
        }
    }
    if ($dupe) {
        [void]$stillUnmatched.Add([PSCustomObject]@{ Competition=$r.Competition; Season=$r.Season; Date=$r.Date
                                                     Home=$r.HomeRaw; Away=$r.AwayRaw; Source=$r.SourceKey
                                                     Note='not added: a master row with the same teams exists within 10 days (possible date/identity mismatch)' })
        continue
    }
    $key = '{0}|{1}|{2}|{3}|{4}|{5}' -f $r.Competition,$r.Season,$r.CompetitionType,$r.Date,$r.HomeTeamId,$r.AwayTeamId
    if ($byKey.ContainsKey($key)) { continue }
    $st = $statusMap[$r.MatchStatus]; if (-not $st) { $st = 'UNKNOWN' }
    $m = [PSCustomObject][ordered]@{
        MatchId=(Match-Id $key); Season=$r.Season; Competition=$r.Competition; CompetitionType=$r.CompetitionType
        Round=$r.Round; Date=$r.Date; KickoffLocalTime=$r.KickoffLocalTime; KickoffUtc=$r.KickoffUtc; SourceTimeZone=$r.SourceTimeZone
        HomeTeam=$teamNm[$r.HomeTeamId]; AwayTeam=$teamNm[$r.AwayTeamId]; HomeTeamId=$r.HomeTeamId; AwayTeamId=$r.AwayTeamId
        HomeGoals=(NInt $r.HomeGoals); AwayGoals=(NInt $r.AwayGoals)
        RegularTimeHomeGoals=(NInt $r.RegularTimeHomeGoals); RegularTimeAwayGoals=(NInt $r.RegularTimeAwayGoals)
        HalfTimeHomeGoals=(NInt $r.HalfTimeHomeGoals); HalfTimeAwayGoals=(NInt $r.HalfTimeAwayGoals)
        ExtraTimeHomeGoals=(NInt $r.ExtraTimeHomeGoals); ExtraTimeAwayGoals=(NInt $r.ExtraTimeAwayGoals)
        PenaltyShootoutHome=(NInt $r.PenaltyShootoutHome); PenaltyShootoutAway=(NInt $r.PenaltyShootoutAway)
        MatchStatus=$st; ProviderMatchId=$r.ProviderMatchId; ProviderHomeTeamId=$r.ProviderHomeTeamId; ProviderAwayTeamId=$r.ProviderAwayTeamId
        Source=$r.SourceKey; SourceFile=$r.SourceFile; SourceCount=1; SourceList=$r.SourceKey
        DataQualityFlag='ADDED_FROM_SECONDARY_SOURCE'; HomeTeamRawPrimary=$r.HomeRaw; AwayTeamRawPrimary=$r.AwayRaw
    }
    [void]$master.Add($m); $byKey[$key] = $m
    if (-not $byPair.ContainsKey($pk)) { $byPair[$pk] = New-Object System.Collections.ArrayList }
    [void]$byPair[$pk].Add($m)
    $srcMap[$m.MatchId] = [ordered]@{ $r.SourceKey = $r.SourceFile }
    [void]$promoted.Add($m)
}
Write-Host "promoted from secondary: $($promoted.Count)   never-played fixtures kept out: $($notPlayed.Count)   still unmatched: $($stillUnmatched.Count)"

foreach ($m in $master) {
    $s = $srcMap[$m.MatchId]
    $m.SourceCount = $s.Count
    $m.SourceList  = ($s.Keys | Sort-Object) -join ';'
}
Write-Host "secondary matched: $matchedCnt   unmatched: $($unmatched.Count)   conflicts: $($conflicts.Count)"

# ---- extra-time column normalisation ---------------------------------------------------------------
# CONTRACT: ExtraTime*Goals = goals scored DURING extra time only.
# api-football is inconsistent across seasons (older rows put the after-ET total in `score.extratime`),
# so the value is recomputed exactly from two authoritative fields: final score - 90' score.
# Nothing is invented; disagreements with the raw provider value are counted and reported.
$etFixed = 0
foreach ($m in $master) {
    if ($m.MatchStatus -in @('AET','PEN') -and $null -ne $m.HomeGoals -and $null -ne $m.RegularTimeHomeGoals) {
        $eh = $m.HomeGoals - $m.RegularTimeHomeGoals
        $ea = $m.AwayGoals - $m.RegularTimeAwayGoals
        if ($m.ExtraTimeHomeGoals -ne $eh -or $m.ExtraTimeAwayGoals -ne $ea) { $etFixed++ }
        $m.ExtraTimeHomeGoals = $eh; $m.ExtraTimeAwayGoals = $ea
    } elseif ($m.MatchStatus -notin @('AET','PEN')) {
        $m.ExtraTimeHomeGoals = $null; $m.ExtraTimeAwayGoals = $null
    }
}
Write-Host "extra-time values recomputed to the ET-only contract (differed from raw provider value): $etFixed"

# ---- outputs --------------------------------------------------------------------------------------
$master = @($master | Sort-Object Date, Competition, HomeTeam)
$master | Select-Object MatchId,Season,Competition,CompetitionType,Round,Date,KickoffLocalTime,KickoffUtc,SourceTimeZone,
                        HomeTeam,AwayTeam,HomeTeamId,AwayTeamId,HomeGoals,AwayGoals,
                        RegularTimeHomeGoals,RegularTimeAwayGoals,HalfTimeHomeGoals,HalfTimeAwayGoals,
                        ExtraTimeHomeGoals,ExtraTimeAwayGoals,PenaltyShootoutHome,PenaltyShootoutAway,
                        MatchStatus,ProviderMatchId,ProviderHomeTeamId,ProviderAwayTeamId,
                        Source,SourceCount,SourceList,DataQualityFlag |
    Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MASTER.csv') -NoTypeInformation -Encoding UTF8

$conflicts | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_CONFLICTS.csv') -NoTypeInformation -Encoding UTF8

$master | ForEach-Object {
    [PSCustomObject]@{ MatchId=$_.MatchId; Competition=$_.Competition; CompetitionType=$_.CompetitionType; Season=$_.Season
                       Date=$_.Date; HomeTeam=$_.HomeTeam; AwayTeam=$_.AwayTeam
                       PrimarySource=$_.Source; PrimarySourceFile=$_.SourceFile
                       SourceCount=$_.SourceCount; SourceList=$_.SourceList }
} | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_SOURCE_MAP.csv') -NoTypeInformation -Encoding UTF8

# quality table
$q = New-Object System.Collections.ArrayList
foreach ($g in ($master | Group-Object Competition,CompetitionType,Season)) {
    $rows = $g.Group
    $comp,$ctype,$season = $g.Name -split ', '
    $conf = @($conflicts | Where-Object { $_.Competition -eq $comp -and $_.CompetitionType -eq $ctype -and $_.Season -eq $season }).Count
    $missScore = @($rows | Where-Object { $null -eq $_.HomeGoals -or $null -eq $_.AwayGoals }).Count
    $missDate  = @($rows | Where-Object { [string]::IsNullOrWhiteSpace($_.Date) }).Count
    $unres     = @($unmatched | Where-Object { $_.Competition -eq $comp -and $_.CompetitionType -eq $ctype -and $_.Season -eq $season }).Count
    $teams     = @(($rows | ForEach-Object { $_.HomeTeamId; $_.AwayTeamId }) | Sort-Object -Unique).Count
    $multi     = @($rows | Where-Object { $_.SourceCount -gt 1 }).Count
    $st = if ($missScore -eq 0 -and $conf -eq 0 -and $unres -eq 0) { 'OK' }
          elseif ($missScore -gt 0) { 'MISSING_SCORE' } elseif ($conf -gt 0) { 'HAS_CONFLICT' } else { 'HAS_UNMATCHED_SECONDARY' }
    [void]$q.Add([PSCustomObject][ordered]@{
        Competition=$comp; CompetitionType=$ctype; Season=$season; MatchCount=$rows.Count; DistinctTeams=$teams
        DuplicateCount=0; ConflictCount=$conf; UnresolvedCount=$unres; MissingScoreCount=$missScore; MissingDateCount=$missDate
        MultiSourceVerified=$multi; PrimarySource=($rows[0].Source); Status=$st })
}
$q = @($q | Sort-Object Competition, CompetitionType, Season)
$q | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_DATA_QUALITY.csv') -NoTypeInformation -Encoding UTF8

# unresolved (teams + unmatched secondary rows + rows without canonical id)
$allUnres = New-Object System.Collections.ArrayList
Import-Csv (Join-Path $Work 'team_unresolved.csv')     | ForEach-Object { [void]$allUnres.Add([PSCustomObject]@{ Kind='TEAM_LINK_UNRESOLVED'; Detail=($_ | ConvertTo-Json -Compress) }) }
Import-Csv (Join-Path $Work 'team_merge_rejected.csv') | ForEach-Object { [void]$allUnres.Add([PSCustomObject]@{ Kind='TEAM_MERGE_REJECTED'; Detail=($_ | ConvertTo-Json -Compress) }) }
$unmatched      | ForEach-Object { [void]$allUnres.Add([PSCustomObject]@{ Kind='SECONDARY_UNMATCHED'; Detail=($_ | ConvertTo-Json -Compress) }) }
$notPlayed      | ForEach-Object { [void]$allUnres.Add([PSCustomObject]@{ Kind='FIXTURE_NEVER_PLAYED_EXCLUDED'; Detail=($_ | ConvertTo-Json -Compress) }) }
$stillUnmatched | ForEach-Object { [void]$allUnres.Add([PSCustomObject]@{ Kind='SECONDARY_NOT_PROMOTED'; Detail=($_ | ConvertTo-Json -Compress) }) }
$promoted       | ForEach-Object { [void]$allUnres.Add([PSCustomObject]@{ Kind='PROMOTED_FROM_SECONDARY'; Detail=("{0} {1} {2} {3} v {4} {5}-{6} src={7}" -f $_.Competition,$_.Season,$_.Date,$_.HomeTeam,$_.AwayTeam,$_.HomeGoals,$_.AwayGoals,$_.Source) }) }
$unknownTeam | ForEach-Object { [void]$allUnres.Add([PSCustomObject]@{ Kind='TEAM_ID_MISSING';     Detail=($_ | ConvertTo-Json -Compress) }) }
$dupRows     | ForEach-Object { [void]$allUnres.Add([PSCustomObject]@{ Kind='PRIMARY_DUPLICATE';   Detail=($_ | ConvertTo-Json -Compress) }) }
$allUnres | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_UNRESOLVED.csv') -NoTypeInformation -Encoding UTF8

# teams registry
Import-Csv (Join-Path $Work 'teams.csv') | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAMS.csv') -NoTypeInformation -Encoding UTF8
Import-Csv (Join-Path $Work 'source_files.csv') | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_SOURCE_FILES.csv') -NoTypeInformation -Encoding UTF8

# coverage matrix (17 FORMAX scopes x season)
$scopes = @(
 @('Premier League','DOMESTIC_LEAGUE'), @('La Liga','DOMESTIC_LEAGUE'), @('Serie A','DOMESTIC_LEAGUE'),
 @('Bundesliga','DOMESTIC_LEAGUE'), @('Ligue 1','DOMESTIC_LEAGUE'), @(('S'+[char]0x00FC+'per Lig'),'DOMESTIC_LEAGUE'),
 @('Championship','DOMESTIC_LEAGUE'), @('Eredivisie','DOMESTIC_LEAGUE'),
 @('UEFA Champions League','UEFA_MAIN'), @('UEFA Europa League','UEFA_MAIN'), @('UEFA Conference League','UEFA_MAIN'),
 @('UEFA Champions League','UEFA_QUALIFIER'), @('UEFA Champions League','UEFA_QUALIFICATION_PLAYOFF'),
 @('UEFA Europa League','UEFA_QUALIFIER'), @('UEFA Europa League','UEFA_QUALIFICATION_PLAYOFF'),
 @('UEFA Conference League','UEFA_QUALIFIER'), @('UEFA Conference League','UEFA_QUALIFICATION_PLAYOFF'))
$seasons = 2017..2025 | ForEach-Object { '{0}/{1}' -f $_, (($_+1) % 100).ToString('00') }
$cov = New-Object System.Collections.ArrayList
$idx = @{}
foreach ($m in $master) { $k = "$($m.Competition)|$($m.CompetitionType)|$($m.Season)"; if (-not $idx.ContainsKey($k)) { $idx[$k]=0 }; $idx[$k]++ }
$no = 0
foreach ($s in $scopes) {
    $no++
    $row = [ordered]@{ No=$no; Competition=$s[0]; CompetitionType=$s[1] }
    foreach ($se in $seasons) {
        $k = "$($s[0])|$($s[1])|$se"
        $row[$se] = if ($idx.ContainsKey($k)) { $idx[$k] } else { 0 }
    }
    [void]$cov.Add([PSCustomObject]$row)
}
$cov | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_COVERAGE_MATRIX.csv') -NoTypeInformation -Encoding UTF8

# ---- console summary -------------------------------------------------------------------------------
Write-Host ""
Write-Host "================ SUMMARY ================"
Write-Host ("Total source files      : {0}" -f (Import-Csv (Join-Path $Work 'source_files.csv')).Count)
Write-Host ("Total raw (staged) rows : {0}" -f $stage.Count)
Write-Host ("Unique canonical matches: {0}" -f $master.Count)
Write-Host ("Duplicate rows dropped  : {0}" -f $dupRows.Count)
Write-Host ("Conflicts               : {0}" -f $conflicts.Count)
Write-Host ("Unmatched secondary     : {0}" -f $unmatched.Count)
Write-Host ("Multi-source verified   : {0}" -f @($master | Where-Object { $_.SourceCount -gt 1 }).Count)
Write-Host ""
$cov | Format-Table -AutoSize
